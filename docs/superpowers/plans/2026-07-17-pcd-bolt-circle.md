# PCD Bolt-Circle Measurement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a `ToolType="pcd"` recipe tool that measures a bolt-hole circle's count / PCD (mm) / angular uniformity / radial roundness and judges PASS/FAIL, reusing the arc-ROI-in-recipe infrastructure.

**Architecture:** Part A is a pure-Domain `PcdAnalyzer` (hole centroids → algebraic Kåsa circle fit → PCD/uniformity/roundness stats + four-condition judgment, all judgment kept in Domain by passing `pixelSizeUm` in), fully unit-tested on synthetic centroids. Part B wires it as a recipe tool that reuses the merged arc infra (`ArcRoi`, `ArcRoiTransform`, the editor arc panel) plus a NEW blob-detection adapter (`IHoleDetector`/`HalconHoleDetector`) and follows the GD&T/gear precedent for a multi-condition tool (four `ItemJudgment`s → 4 CSV rows).

**Tech Stack:** .NET Framework 4.8, WinForms, HALCON 17.12, old-style `.csproj` (new files need explicit `<Compile Include>`), console-style test suites.

**Spec:** `docs/superpowers/specs/2026-07-17-pcd-bolt-circle-design.md`.

**Branch:** create `feature/pcd-bolt-circle` before Task 1.

**Build/test (PowerShell — the /p: flag is REQUIRED; close the app first or DLLs lock → MSB3026):**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe"; "EXITCODE=$LASTEXITCODE"
& ".\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe"; "EXITCODE=$LASTEXITCODE"
```

## Key difference from gear (read first)
Gear scans arc EDGES for angular tooth positions. PCD needs each hole's 2D CENTER, then fits a circle → true PCD. So the detection pipeline is **blob detection in the annulus** (`HalconHoleDetector`), NOT `DetectEdgesOnArc`. Reused from arc infra: `ArcRoi` (as the annular search region), `ArcRoiTransform` (pose), the editor arc panel, the overlay/CSV multi-judgment framework.

## Lessons-from-gear/arc/audit checklist — baked into the tasks
- schema-version test knock-on (Task 2, 5 asserts 8→9).
- `DeepCopyTool` must copy `Pcd` AND `ArcRoi` (Task 6).
- `MeasurementWorkflow` dedicated 4-judgment branch BEFORE `GetMeasuredValue` (Task 5).
- editor panel visibility + trial + band reuse (Task 6).
- overlay leaves `Roi` null → no (0,0) box (Task 7).
- other-pass exclusions (Task 4).
- measurement failure counts as NG: already fixed in `MeasurementWorkflow` (audit #1); pcd inherits it via `ToolRunResult.IsOk=null` on failure.
- pcd is NOT added to `DoubleSidedToleranceTypes` (four-condition, no double-sided Tolerance; audit #10 consistency).

---

## File Structure
**Create:**
- `src/FlashMeasurementSystem.Domain/PcdAnalysis/HolePoint.cs`
- `src/FlashMeasurementSystem.Domain/PcdAnalysis/PcdAnalysisParameters.cs`
- `src/FlashMeasurementSystem.Domain/PcdAnalysis/PcdAnalysisResult.cs`
- `src/FlashMeasurementSystem.Domain/PcdAnalysis/PcdAnalyzer.cs`
- `src/FlashMeasurementSystem.Domain/HoleDetection/HoleDetectionResult.cs`
- `src/FlashMeasurementSystem.Application/HoleDetection/IHoleDetector.cs`
- `src/FlashMeasurementSystem.Halcon/HoleDetection/HalconHoleDetector.cs`
- `tests/FlashMeasurementSystem.Tests/PcdAnalysisDomainTests.cs`
- `tests/FlashMeasurementSystem.Tests/PcdRecipeToolDomainTests.cs`

**Modify:** `MeasurementTool.cs` (+Pcd), `Recipe.cs` (v9), `RecipeValidator.cs` (pcd rule), the 5 schema-version tests; `RecipeRunner.cs` (Pass 1.4 + ToolRunResult.Pcd + ctor); `MeasurementWorkflow.cs` (pcd branch); `RecipeEditor.cs` (pcd panel); `MainWindow.cs` (overlay + DI wiring); Domain/Application/Halcon/Tests `.csproj` + `EdgeDetectionDomainTests.cs` Main wiring.

---

## Task 1: Part A — Domain DTOs + PcdAnalyzer (fully unit-tested)

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/PcdAnalysis/HolePoint.cs`, `PcdAnalysisParameters.cs`, `PcdAnalysisResult.cs`, `PcdAnalyzer.cs`
- Create: `tests/FlashMeasurementSystem.Tests/PcdAnalysisDomainTests.cs`
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`, `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`, `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`

- [ ] **Step 1: Create branch** `git checkout -b feature/pcd-bolt-circle`

- [ ] **Step 2: Write `HolePoint.cs`**
```csharp
namespace FlashMeasurementSystem.Domain.PcdAnalysis
{
    /// <summary>單一孔質心（像素座標）。純 DTO。</summary>
    public class HolePoint
    {
        public double Row { get; set; }
        public double Col { get; set; }
    }
}
```

- [ ] **Step 3: Write `PcdAnalysisParameters.cs`**
```csharp
namespace FlashMeasurementSystem.Domain.PcdAnalysis
{
    /// <summary>PCD 螺栓孔圈分析參數（純 DTO，無 HALCON）。距離公差以 mm、角度以度。</summary>
    public class PcdAnalysisParameters
    {
        public int    NominalHoleCount   = 6;
        public double NominalPcdMm        = 0.0;    // 標稱節圓直徑（mm）；使用者需設定
        public double PcdToleranceMm      = 0.1;    // |PCD−標稱| ≤ 此值
        public double AngularToleranceDeg = 1.0;    // 相鄰孔角距對均值的最大偏差
        public double RadialToleranceMm   = 0.05;   // 孔心徑向偏差上限
        public bool   HoleIsDark          = true;   // 背光穿孔＝暗（偵測層用；分析器忽略）
        public double MinHoleAreaPx       = 20.0;   // blob 最小面積濾雜訊（偵測層用；分析器忽略）

        public static PcdAnalysisParameters Default() => new PcdAnalysisParameters();
    }
}
```

- [ ] **Step 4: Write `PcdAnalysisResult.cs`**
```csharp
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.PcdAnalysis
{
    /// <summary>PCD 分析結果（純 DTO）。Success=false 表流程失敗（見 Message）。</summary>
    public class PcdAnalysisResult
    {
        public bool Success { get; set; }
        public bool IsPass { get; set; }
        public int  HoleCount { get; set; }

        public double PcdMm { get; set; }
        public double PcdPx { get; set; }
        public double CenterRow { get; set; }   // 擬合圓心（px，供 overlay）
        public double CenterCol { get; set; }

        public double AngularMeanDeg { get; set; }
        public double AngularMaxDevDeg { get; set; }
        public double RadialMaxDevMm { get; set; }
        public double RadialMaxDevPx { get; set; }

        public bool CountOk { get; set; }
        public bool PcdOk { get; set; }
        public bool AngularOk { get; set; }
        public bool RadialOk { get; set; }

        public List<HolePoint> Holes { get; set; } = new List<HolePoint>();      // 依角度排序
        public List<double> MissingHoleHintsDeg { get; set; } = new List<double>();
        public string Message { get; set; } = "";

        public static PcdAnalysisResult Failed(string message) =>
            new PcdAnalysisResult { Success = false, IsPass = false, Message = message };
    }
}
```

- [ ] **Step 5: Write the failing test `tests/FlashMeasurementSystem.Tests/PcdAnalysisDomainTests.cs`**

Helper builds N hole centroids evenly spaced on a circle of radius `Rpx` about `(Cr,Cc)`; `pixelSizeUm=10` ⇒ 1px=0.01mm, so PcdMm = PcdPx/100. Perturbations: drop a hole, shift one hole radially, shift one hole angularly.
```csharp
using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.PcdAnalysis;

namespace FlashMeasurementSystem.Tests
{
    public static class PcdAnalysisDomainTests
    {
        private const double Cr = 500, Cc = 500, R = 250, Px = 10.0; // 1px=0.01mm

        // N 孔均分於半徑 R 的圓；dropHole 移除該孔；dR[i] 徑向位移；dDeg[i] 角度位移。
        private static List<HolePoint> Ring(int n, int dropHole = -1, double[] dR = null, double[] dDeg = null)
        {
            var pts = new List<HolePoint>();
            for (int i = 0; i < n; i++)
            {
                if (i == dropHole) continue;
                double deg = i * 360.0 / n + (dDeg != null ? dDeg[i] : 0.0);
                double rr = R + (dR != null ? dR[i] : 0.0);
                double th = deg * Math.PI / 180.0;
                pts.Add(new HolePoint { Row = Cr + rr * Math.Sin(th), Col = Cc + rr * Math.Cos(th) });
            }
            return pts;
        }

        public static void Run()
        {
            var p = new PcdAnalysisParameters { NominalHoleCount = 6, NominalPcdMm = 5.0,
                PcdToleranceMm = 0.05, AngularToleranceDeg = 0.5, RadialToleranceMm = 0.02 };

            // 完美 6 孔於 R=250px → 孔數 6、PcdPx=500、PcdMm=5.0、角/徑偏差≈0、PASS
            var r = PcdAnalyzer.Analyze(Ring(6), Px, p);
            AssertEqual(true, r.Success, "perfect Success");
            AssertEqual(6, r.HoleCount, "perfect count 6");
            AssertClose(500.0, r.PcdPx, 1e-6, "perfect PcdPx 500");
            AssertClose(5.0, r.PcdMm, 1e-6, "perfect PcdMm 5.0");
            AssertClose(0.0, r.AngularMaxDevDeg, 1e-6, "perfect angular dev 0");
            AssertClose(0.0, r.RadialMaxDevMm, 1e-6, "perfect radial dev 0");
            AssertEqual(true, r.IsPass, "perfect PASS");
            AssertEqual(6, r.Holes.Count, "perfect holes list");

            // 缺一孔 → 孔數 5、CountOk false、FAIL、缺孔提示落在缺孔角度（3×60=180°）
            var rm = PcdAnalyzer.Analyze(Ring(6, dropHole: 3), Px, p);
            AssertEqual(5, rm.HoleCount, "missing count 5");
            AssertEqual(false, rm.CountOk, "missing CountOk false");
            AssertEqual(false, rm.IsPass, "missing FAIL");
            bool hint = false; foreach (double h in rm.MissingHoleHintsDeg) if (Math.Abs(h - 180.0) < 1e-6) hint = true;
            if (!hint) throw new InvalidOperationException("missing hole hint at ~180deg");

            // 一孔偏半徑 +5px（=0.05mm > 0.02 公差）→ RadialOk false，角度仍 OK
            var dr = new double[6]; dr[2] = 5.0;
            var rr2 = PcdAnalyzer.Analyze(Ring(6, dR: dr), Px, p);
            AssertEqual(false, rr2.RadialOk, "radial dev → RadialOk false");
            AssertEqual(true, rr2.AngularOk, "radial-only → AngularOk true");

            // 一孔偏角 +2° → AngularOk false
            var dd = new double[6]; dd[4] = 2.0;
            var ra = PcdAnalyzer.Analyze(Ring(6, dDeg: dd), Px, p);
            AssertEqual(false, ra.AngularOk, "angular dev → AngularOk false");

            // PCD 邊界：標稱設成剛好內側 / 剛好外側夾 ≤ inclusive
            var pIn = new PcdAnalysisParameters { NominalHoleCount = 6, NominalPcdMm = 5.05,
                PcdToleranceMm = 0.05, AngularToleranceDeg = 5, RadialToleranceMm = 5 }; // |5.0-5.05|=0.05 ≤ 0.05
            AssertEqual(true, PcdAnalyzer.Analyze(Ring(6), Px, pIn).PcdOk, "PCD just-inside inclusive");
            var pOut = new PcdAnalysisParameters { NominalHoleCount = 6, NominalPcdMm = 5.06,
                PcdToleranceMm = 0.05, AngularToleranceDeg = 5, RadialToleranceMm = 5 }; // |5.0-5.06|=0.06 > 0.05
            AssertEqual(false, PcdAnalyzer.Analyze(Ring(6), Px, pOut).PcdOk, "PCD just-outside excluded");

            // 失敗路徑
            AssertEqual(false, PcdAnalyzer.Analyze(null, Px, p).Success, "null fail");
            AssertEqual(false, PcdAnalyzer.Analyze(new List<HolePoint>(), Px, p).Success, "empty fail");
            AssertEqual(false, PcdAnalyzer.Analyze(Ring(2), Px, p).Success, "<3 holes fail");
            AssertEqual(false, PcdAnalyzer.Analyze(Ring(6), 0.0, p).Success, "pixelSize<=0 fail");
            AssertEqual(false, PcdAnalyzer.Analyze(Ring(6), Px,
                new PcdAnalysisParameters { NominalHoleCount = 0 }).Success, "nominal<=0 fail");
            // 共線三點 → 退化擬合失敗
            var collinear = new List<HolePoint> {
                new HolePoint { Row = 100, Col = 100 }, new HolePoint { Row = 100, Col = 200 },
                new HolePoint { Row = 100, Col = 300 } };
            AssertEqual(false, PcdAnalyzer.Analyze(collinear, Px, p).Success, "collinear fail");
        }

        private static void AssertEqual<T>(T e, T a, string n)
        { if (!object.Equals(e, a)) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
        private static void AssertClose(double e, double a, double t, string n)
        { if (Math.Abs(e - a) > t) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
    }
}
```

- [ ] **Step 6: Register 4 Domain files + test; wire suite into `Main()`**
In `FlashMeasurementSystem.Domain.csproj` (near other `<Compile Include>`):
```xml
    <Compile Include="PcdAnalysis\HolePoint.cs" />
    <Compile Include="PcdAnalysis\PcdAnalysisParameters.cs" />
    <Compile Include="PcdAnalysis\PcdAnalysisResult.cs" />
    <Compile Include="PcdAnalysis\PcdAnalyzer.cs" />
```
In `FlashMeasurementSystem.Tests.csproj`: `<Compile Include="PcdAnalysisDomainTests.cs" />`
In `EdgeDetectionDomainTests.cs` `Main()`, after the `GearAnalysisDomainTests` block:
```csharp
            PcdAnalysisDomainTests.Run();
            Console.WriteLine("PcdAnalysisDomainTests passed");
```

- [ ] **Step 7: Build → expect COMPILE ERROR** (`PcdAnalyzer` missing). Confirm it fails to compile.

- [ ] **Step 8: Write `PcdAnalyzer.cs`**
```csharp
using System;
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.PcdAnalysis
{
    /// <summary>
    /// 純 PCD 分析（無 HALCON）。孔質心 → Kåsa 代數圓擬合 → 節圓直徑/角度均勻/徑向真圓度 + 四條件判定。
    /// 判定全在此層（pixelSizeUm 由呼叫端傳入，PCD/徑向轉 mm），合成質心可全驗。
    /// </summary>
    public static class PcdAnalyzer
    {
        public static PcdAnalysisResult Analyze(
            System.Collections.Generic.IList<HolePoint> holes, double pixelSizeUm, PcdAnalysisParameters parameters)
        {
            var p = parameters ?? PcdAnalysisParameters.Default();
            if (p.NominalHoleCount <= 0 || p.PcdToleranceMm <= 0 || p.AngularToleranceDeg <= 0 || p.RadialToleranceMm <= 0)
                return PcdAnalysisResult.Failed("PCD 參數無效（標稱孔數與公差需 > 0）");
            if (holes == null || holes.Count < 3)
                return PcdAnalysisResult.Failed("孔數不足（圓擬合需 ≥ 3 個孔）");
            if (pixelSizeUm <= 0)
                return PcdAnalysisResult.Failed("像素尺寸無效");

            // Kåsa 代數圓擬合：x²+y²+D·x+E·y+F=0（x=Col, y=Row）；解 3×3 正規方程。
            int n = holes.Count;
            double Sx = 0, Sy = 0, Sxx = 0, Syy = 0, Sxy = 0, Sxz = 0, Syz = 0, Sz = 0;
            foreach (HolePoint h in holes)
            {
                double x = h.Col, y = h.Row, z = x * x + y * y;
                Sx += x; Sy += y; Sxx += x * x; Syy += y * y; Sxy += x * y;
                Sxz += x * z; Syz += y * z; Sz += z;
            }
            // 正規方程 A·[D;E;F] = b，b = -[Sxz; Syz; Sz]
            double[,] A = { { Sxx, Sxy, Sx }, { Sxy, Syy, Sy }, { Sx, Sy, n } };
            double[] b = { -Sxz, -Syz, -Sz };
            if (!Solve3x3(A, b, out double D, out double E, out double F))
                return PcdAnalysisResult.Failed("孔心無法擬合圓（共線或退化）");
            double cc = -D / 2.0, cr = -E / 2.0;
            double rsq = D * D / 4.0 + E * E / 4.0 - F;
            if (double.IsNaN(rsq) || rsq <= 0)
                return PcdAnalysisResult.Failed("孔心無法擬合圓（退化）");
            double R = Math.Sqrt(rsq);

            const double TwoPi = 2.0 * Math.PI;
            double mmPerPx = pixelSizeUm / 1000.0;

            var result = new PcdAnalysisResult { Success = true, HoleCount = n };
            result.CenterRow = cr; result.CenterCol = cc;
            result.PcdPx = 2.0 * R; result.PcdMm = result.PcdPx * mmPerPx;

            // 角度/徑距
            var angs = new double[n];
            double radialMaxDevPx = 0;
            var byAngle = new List<KeyValuePair<double, HolePoint>>(n);
            foreach (HolePoint h in holes)
            {
                double th = Math.Atan2(h.Row - cr, h.Col - cc); if (th < 0) th += TwoPi;
                double radial = Math.Sqrt((h.Row - cr) * (h.Row - cr) + (h.Col - cc) * (h.Col - cc));
                double dev = Math.Abs(radial - R); if (dev > radialMaxDevPx) radialMaxDevPx = dev;
                byAngle.Add(new KeyValuePair<double, HolePoint>(th, h));
            }
            byAngle.Sort((a, b2) => a.Key.CompareTo(b2.Key));
            for (int i = 0; i < n; i++) { angs[i] = byAngle[i].Key; result.Holes.Add(byAngle[i].Value); }

            result.RadialMaxDevPx = radialMaxDevPx;
            result.RadialMaxDevMm = radialMaxDevPx * mmPerPx;

            // 相鄰角距（環繞、和為 2π）→ 對均值 2π/n 的最大偏差
            double d2 = 180.0 / Math.PI;
            var pitches = new double[n];
            for (int i = 0; i < n; i++)
            {
                double d = angs[(i + 1) % n] - angs[i]; if (d <= 0) d += TwoPi;
                pitches[i] = d;
            }
            double meanPitch = TwoPi / n;
            double angMaxDev = 0;
            for (int i = 0; i < n; i++) { double dv = Math.Abs(pitches[i] - meanPitch); if (dv > angMaxDev) angMaxDev = dv; }
            result.AngularMeanDeg = meanPitch * d2;
            result.AngularMaxDevDeg = angMaxDev * d2;

            // 缺孔：角距 > 1.5×中位數 → 提示點放間隙中點
            double median = Median(pitches);
            for (int i = 0; i < n; i++)
                if (pitches[i] > 1.5 * median)
                {
                    double mid = angs[i] + pitches[i] / 2.0; if (mid >= TwoPi) mid -= TwoPi;
                    result.MissingHoleHintsDeg.Add(mid * d2);
                }

            result.CountOk = n == p.NominalHoleCount;
            result.PcdOk = Math.Abs(result.PcdMm - p.NominalPcdMm) <= p.PcdToleranceMm;
            result.AngularOk = result.AngularMaxDevDeg <= p.AngularToleranceDeg;
            result.RadialOk = result.RadialMaxDevMm <= p.RadialToleranceMm;
            result.IsPass = result.CountOk && result.PcdOk && result.AngularOk && result.RadialOk;
            result.Message = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "孔數={0}(標稱{1}) PCD={2:F3}mm 角偏差={3:F2}° 徑偏差={4:F3}mm → {5}",
                n, p.NominalHoleCount, result.PcdMm, result.AngularMaxDevDeg, result.RadialMaxDevMm,
                result.IsPass ? "PASS" : "FAIL");
            return result;
        }

        // 3×3 線性解（高斯消去 + 部分主元）；奇異回 false。
        private static bool Solve3x3(double[,] A, double[] b, out double x0, out double x1, out double x2)
        {
            x0 = x1 = x2 = 0;
            double[,] m = { { A[0, 0], A[0, 1], A[0, 2], b[0] },
                            { A[1, 0], A[1, 1], A[1, 2], b[1] },
                            { A[2, 0], A[2, 1], A[2, 2], b[2] } };
            for (int col = 0; col < 3; col++)
            {
                int piv = col; double best = Math.Abs(m[col, col]);
                for (int r = col + 1; r < 3; r++) { double v = Math.Abs(m[r, col]); if (v > best) { best = v; piv = r; } }
                if (best < 1e-12) return false;
                if (piv != col) for (int c = 0; c < 4; c++) { double t = m[col, c]; m[col, c] = m[piv, c]; m[piv, c] = t; }
                for (int r = 0; r < 3; r++)
                {
                    if (r == col) continue;
                    double f = m[r, col] / m[col, col];
                    for (int c = col; c < 4; c++) m[r, c] -= f * m[col, c];
                }
            }
            x0 = m[0, 3] / m[0, 0]; x1 = m[1, 3] / m[1, 1]; x2 = m[2, 3] / m[2, 2];
            return true;
        }

        private static double Median(double[] v)
        {
            var s = (double[])v.Clone(); Array.Sort(s);
            int m = s.Length / 2;
            return (s.Length % 2 == 0) ? (s[m - 1] + s[m]) / 2.0 : s[m];
        }
    }
}
```

- [ ] **Step 9: Build + run tests → PASS.** Expected: build 0/0; `PcdAnalysisDomainTests passed`; `EXITCODE=0`. (If a synthetic case is off by rounding, adjust the TEST tolerance — NOT the analyzer — and note it.)

- [ ] **Step 10: Commit**
```bash
git add src/FlashMeasurementSystem.Domain/PcdAnalysis tests/FlashMeasurementSystem.Tests/PcdAnalysisDomainTests.cs src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
git commit -m "feat(pcd): add pure Domain PCD analyzer (Kasa circle fit) with synthetic tests"
```

---

## Task 2: Schema v9 (MeasurementTool.Pcd) + Validator + fix schema-version tests

**Files:** `MeasurementTool.cs`, `Recipe.cs`, `RecipeValidator.cs`, `ArcRecipeToolDomainTests.cs`, `GearRecipeToolDomainTests.cs`, `MetrologyModelDomainTests.cs`, `RoiDomainTests.cs`, new `PcdRecipeToolDomainTests.cs` + csproj/Main wiring.

- [ ] **Step 1: Write the failing test `tests/FlashMeasurementSystem.Tests/PcdRecipeToolDomainTests.cs`**
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.PcdAnalysis;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    public static class PcdRecipeToolDomainTests
    {
        public static void Run()
        {
            AssertEqual(9, Recipe.Default().SchemaVersion, "SchemaVersion is 9");
            AssertEqual(null, new MeasurementTool().Pcd, "Default Pcd is null");

            var recipe = Recipe.Default();
            recipe.Tools = new List<MeasurementTool>
            {
                new MeasurementTool
                {
                    Id = "P1", Name = "螺栓圈", ToolType = "pcd",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 500, CenterCol = 500, Radius = 250,
                        AngleStart = 0, AngleExtent = 2 * Math.PI, AnnulusRadius = 30 },
                    Pcd = new PcdAnalysisParameters { NominalHoleCount = 6, NominalPcdMm = 5.0,
                        PcdToleranceMm = 0.1, AngularToleranceDeg = 1.0, RadialToleranceMm = 0.05,
                        HoleIsDark = true, MinHoleAreaPx = 25 }
                }
            };
            string path = Path.Combine(Path.GetTempPath(), "fms_pcd_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                new RecipeStore().Save(recipe, path);
                Recipe rt = new RecipeStore().Load(path);
                PcdAnalysisParameters g = rt.Tools[0].Pcd;
                if (g == null) throw new InvalidOperationException("round-trip Pcd null");
                AssertEqual(6, g.NominalHoleCount, "rt NominalHoleCount");
                AssertClose(5.0, g.NominalPcdMm, 1e-9, "rt NominalPcdMm");
                AssertClose(0.1, g.PcdToleranceMm, 1e-9, "rt PcdTol");
                AssertClose(0.05, g.RadialToleranceMm, 1e-9, "rt RadialTol");
                AssertEqual(true, g.HoleIsDark, "rt HoleIsDark");
                if (rt.Tools[0].ArcRoi == null) throw new InvalidOperationException("rt ArcRoi null");
                AssertClose(250, rt.Tools[0].ArcRoi.Radius, 1e-9, "rt ArcRoi radius");
            }
            finally { if (File.Exists(path)) File.Delete(path); }

            // 向後相容：舊 JSON 無 Pcd → null
            string oldPath = Path.Combine(Path.GetTempPath(), "fms_pcd_old_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(oldPath, "{ \"SchemaVersion\": 8, \"Tools\": [ { \"Id\": \"C1\", \"ToolType\": \"circle\" } ] }");
                Recipe old = new RecipeStore().Load(oldPath);
                AssertEqual(null, old.Tools[0].Pcd, "old Pcd null");
            }
            finally { if (File.Exists(oldPath)) File.Delete(oldPath); }

            // Validator：合法 pcd → 0 error；缺 Pcd / 缺 ArcRoi / 標稱孔數 0 → error
            AssertEqual(0, Errors(ValidPcdRecipe()), "valid pcd no error");
            var noPcd = ValidPcdRecipe(); noPcd.Tools[0].Pcd = null;
            if (Errors(noPcd) == 0) throw new InvalidOperationException("pcd without Pcd → error");
            var noArc = ValidPcdRecipe(); noArc.Tools[0].ArcRoi = null;
            if (Errors(noArc) == 0) throw new InvalidOperationException("pcd without ArcRoi → error");
            var badN = ValidPcdRecipe(); badN.Tools[0].Pcd.NominalHoleCount = 0;
            if (Errors(badN) == 0) throw new InvalidOperationException("pcd NominalHoleCount 0 → error");
        }

        private static Recipe ValidPcdRecipe()
        {
            var r = Recipe.Default();
            r.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "P1", Name = "螺栓圈", ToolType = "pcd",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 500, CenterCol = 500, Radius = 250,
                        AngleStart = 0, AngleExtent = 2 * Math.PI, AnnulusRadius = 30 },
                    Pcd = new PcdAnalysisParameters { NominalHoleCount = 6, NominalPcdMm = 5.0,
                        PcdToleranceMm = 0.1, AngularToleranceDeg = 1.0, RadialToleranceMm = 0.05 } }
            };
            return r;
        }
        private static int Errors(Recipe r)
        {
            int n = 0; foreach (RecipeIssue i in RecipeValidator.Validate(r, 1600, 1600)) if (i.Severity == RecipeIssueSeverity.Error) n++; return n;
        }
        private static void AssertEqual<T>(T e, T a, string n)
        { if (!object.Equals(e, a)) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
        private static void AssertClose(double e, double a, double t, string n)
        { if (Math.Abs(e - a) > t) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
    }
}
```
Register in tests `.csproj` (`<Compile Include="PcdRecipeToolDomainTests.cs" />`) and wire `PcdRecipeToolDomainTests.Run();` + `Console.WriteLine("PcdRecipeToolDomainTests passed");` into `Main()` after the pcd analyzer line.

- [ ] **Step 2: Add `Pcd` to `MeasurementTool.cs`** (nullable additive, mirror `Gear`):
```csharp
using FlashMeasurementSystem.Domain.PcdAnalysis;  // add near the other usings
```
```csharp
        // v9：PCD 螺栓孔圈分析參數（重用 PcdAnalysisParameters DTO）。null＝非 pcd 工具。
        // pcd 工具（ToolType="pcd"）必填；量測環帶用 ArcRoi。
        public PcdAnalysisParameters Pcd { get; set; } = null;
```

- [ ] **Step 3: Bump schema to v9 in `Recipe.cs`** — `SchemaVersion = 9;` + version-comment:
```csharp
        // v9：PCD 螺栓孔圈工具（MeasurementTool.Pcd，加性 nullable 欄）+ ToolType="pcd"。
        //     純加欄位、向後相容、無遷移碼：舊檔載入 Pcd=null、無 pcd 工具，行為不變。
```

- [ ] **Step 4: Fix the FIVE stale schema-version assertions (8→9).** Change only the literal `8`→`9` on these lines:
  - `tests/FlashMeasurementSystem.Tests/ArcRecipeToolDomainTests.cs` — the `AssertEqual(8, ... SchemaVersion ...)` line.
  - `tests/FlashMeasurementSystem.Tests/GearRecipeToolDomainTests.cs` — `AssertEqual(8, Recipe.Default().SchemaVersion, ...)`.
  - `tests/FlashMeasurementSystem.Tests/MetrologyModelDomainTests.cs` — the `SchemaVersion == 8` assert (and message text).
  - `tests/FlashMeasurementSystem.Tests/RoiDomainTests.cs` — BOTH `AssertEqual(8, ... SchemaVersion ...)` lines (there are two).
  (Grep each file for `SchemaVersion` + `8` to find the exact line; change only the literal.)

- [ ] **Step 5: Add the pcd rule to `RecipeValidator.cs`** — add `"pcd"` to `KnownTypes` (NOT `RoiElementTypes`, NOT `DoubleSidedToleranceTypes`), and in the per-tool loop (mirror the gear block):
```csharp
                if (tool.ToolType == "pcd")
                {
                    if (tool.Pcd == null)
                        issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name, "PCD 工具缺少 PCD 參數（Pcd）"));
                    else if (tool.Pcd.NominalHoleCount <= 0 || tool.Pcd.PcdToleranceMm <= 0
                             || tool.Pcd.AngularToleranceDeg <= 0 || tool.Pcd.RadialToleranceMm <= 0)
                        issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name, "PCD 參數無效（標稱孔數與公差需 > 0）"));
                    if (tool.ArcRoi == null || !tool.ArcRoi.IsDefined)
                        issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                            "PCD 工具的量測環帶無效：" + (tool.ArcRoi == null ? "缺少 ArcRoi" : tool.ArcRoi.ValidationError)));
                }
```

- [ ] **Step 6: Build + run tests → PASS** (`PcdRecipeToolDomainTests passed`; arc/gear/roi/metrology suites still green with 9). `EXITCODE=0`.

- [ ] **Step 7: Commit**
```bash
git add src/FlashMeasurementSystem.Domain/Roi/MeasurementTool.cs src/FlashMeasurementSystem.Domain/Roi/Recipe.cs src/FlashMeasurementSystem.Domain/Roi/RecipeValidator.cs tests/FlashMeasurementSystem.Tests/PcdRecipeToolDomainTests.cs tests/FlashMeasurementSystem.Tests/ArcRecipeToolDomainTests.cs tests/FlashMeasurementSystem.Tests/GearRecipeToolDomainTests.cs tests/FlashMeasurementSystem.Tests/MetrologyModelDomainTests.cs tests/FlashMeasurementSystem.Tests/RoiDomainTests.cs tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
git commit -m "feat(pcd): add Pcd to recipe schema v9 + validator (fix stale schema-version asserts)"
```

---

## Task 3: IHoleDetector interface + HalconHoleDetector adapter (blob detection)

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/HoleDetection/HoleDetectionResult.cs`
- Create: `src/FlashMeasurementSystem.Application/HoleDetection/IHoleDetector.cs`
- Create: `src/FlashMeasurementSystem.Halcon/HoleDetection/HalconHoleDetector.cs`
- Modify: Domain/Application/Halcon `.csproj` (compile includes)

- [ ] **Step 1: Write `HoleDetectionResult.cs`** (Domain, mirrors EdgeResult shape)
```csharp
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.PcdAnalysis;

namespace FlashMeasurementSystem.Domain.HoleDetection
{
    /// <summary>環帶內孔 blob 偵測結果（純 DTO）。Success=false 見 ErrorMessage。</summary>
    public class HoleDetectionResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public List<HolePoint> Holes { get; set; } = new List<HolePoint>();
    }
}
```
Register in `FlashMeasurementSystem.Domain.csproj`: `<Compile Include="HoleDetection\HoleDetectionResult.cs" />`

- [ ] **Step 2: Write `IHoleDetector.cs`** (Application, over Domain types)
```csharp
using FlashMeasurementSystem.Domain.HoleDetection;
using FlashMeasurementSystem.Domain.PcdAnalysis;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Application.HoleDetection
{
    /// <summary>環狀 ROI 內以 blob 偵測孔並回傳質心。TImage 由 Halcon adapter 綁定 HImage。</summary>
    public interface IHoleDetector<TImage>
    {
        HoleDetectionResult DetectHolesInAnnulus(TImage image, ArcMeasureRoi placedArc, PcdAnalysisParameters parameters);
    }
}
```
Register in `FlashMeasurementSystem.Application.csproj`: `<Compile Include="HoleDetection\IHoleDetector.cs" />`
(Grep tests for `IHoleDetector` — it is brand new, so no test Fake needs updating. If a compile-contract test references it, add a trivial Fake there; otherwise none.)

- [ ] **Step 3: Write `HalconHoleDetector.cs`** (Halcon adapter — the ONLY place HObject is allowed)
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using FlashMeasurementSystem.Application.HoleDetection;
using FlashMeasurementSystem.Domain.HoleDetection;
using FlashMeasurementSystem.Domain.PcdAnalysis;
using FlashMeasurementSystem.Domain.Roi;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.HoleDetection
{
    /// <summary>
    /// 在 ArcRoi 環帶內以 blob 偵測孔：環狀 region reduce_domain → binary_threshold（依 HoleIsDark 取暗/亮）
    /// → connection → select_shape（面積濾雜訊）→ area_center。回傳孔質心（像素）。
    /// v1 假設整圈螺栓圈，直接用整個環（不裁角度扇形）。
    /// </summary>
    public sealed class HalconHoleDetector : IHoleDetector<HImage>
    {
        public HoleDetectionResult DetectHolesInAnnulus(HImage image, ArcMeasureRoi a, PcdAnalysisParameters p)
        {
            var result = new HoleDetectionResult();
            if (image == null) { result.ErrorMessage = "影像為空"; return result; }
            if (a == null || !a.IsDefined) { result.ErrorMessage = "量測環帶無效"; return result; }
            if (p == null) p = PcdAnalysisParameters.Default();

            HObject gray = null, outer = null, inner = null, ring = null, reduced = null,
                    region = null, connected = null, selected = null;
            try
            {
                gray = EnsureSingleChannel(image);
                double rOut = a.Radius + a.AnnulusRadius, rIn = a.Radius - a.AnnulusRadius;
                if (rIn < 0) rIn = 0;
                HOperatorSet.GenCircle(out outer, a.CenterRow, a.CenterCol, rOut);
                HOperatorSet.GenCircle(out inner, a.CenterRow, a.CenterCol, rIn);
                HOperatorSet.Difference(outer, inner, out ring);
                HOperatorSet.ReduceDomain(gray, ring, out reduced);
                HOperatorSet.BinaryThreshold(reduced, out region, "max_separability",
                    p.HoleIsDark ? "dark" : "light", out HTuple _used);
                HOperatorSet.Connection(region, out connected);
                HOperatorSet.SelectShape(connected, out selected, "area", "and",
                    new HTuple(p.MinHoleAreaPx), new HTuple(1e12));
                HOperatorSet.AreaCenter(selected, out HTuple area, out HTuple rows, out HTuple cols);

                int cnt = rows?.Length ?? 0;
                for (int i = 0; i < cnt; i++)
                    result.Holes.Add(new HolePoint { Row = rows[i].D, Col = cols[i].D });
                result.Success = cnt > 0;
                if (!result.Success)
                    result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                        "環帶內未偵測到孔（半徑 {0:F0}±{1:F0}、HoleIsDark={2}、MinArea={3:F0}；請調環帶/極性/面積）",
                        a.Radius, a.AnnulusRadius, p.HoleIsDark, p.MinHoleAreaPx);
            }
            catch (HalconException ex)
            {
                result.Success = false;
                result.ErrorMessage = "孔偵測異常 [" + ex.GetErrorCode() + "]: " + ex.Message;
            }
            finally
            {
                gray?.Dispose(); outer?.Dispose(); inner?.Dispose(); ring?.Dispose();
                reduced?.Dispose(); region?.Dispose(); connected?.Dispose(); selected?.Dispose();
            }
            return result;
        }

        // 比照既有 adapter：多通道轉灰階，單通道直接用（複製以便 using 釋放不影響來源）。
        private static HObject EnsureSingleChannel(HImage image)
        {
            HOperatorSet.CountChannels(image, out HTuple channels);
            if (channels.I >= 3)
            {
                HOperatorSet.Rgb1ToGray(image, out HObject g);
                return g;
            }
            HOperatorSet.CopyObj(image, out HObject copy, 1, -1);
            return copy;
        }
    }
}
```
Register in `FlashMeasurementSystem.Halcon.csproj`: `<Compile Include="HoleDetection\HalconHoleDetector.cs" />`
Note: verify `BinaryThreshold`, `GenCircle`, `Difference`, `ReduceDomain`, `Connection`, `SelectShape`, `AreaCenter`, `Rgb1ToGray`, `CountChannels`, `CopyObj` signatures against `F:\C#\FlashMeasurementSystem\halcon_pdf\reference\reference_hdevelop.txt` (grep `halcon_operator_index.md` for line numbers) before finalizing; adjust arg order if the reference differs and disclose.

- [ ] **Step 4: Build (x64) → 0/0.** No new automated test here (HALCON adapter is GUI-verified per project convention). Confirm the solution compiles with the new interface + adapter.

- [ ] **Step 5: Commit**
```bash
git add src/FlashMeasurementSystem.Domain/HoleDetection src/FlashMeasurementSystem.Application/HoleDetection src/FlashMeasurementSystem.Halcon/HoleDetection src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj
git commit -m "feat(pcd): add IHoleDetector + HalconHoleDetector (annulus blob centroids)"
```

---

## Task 4: RecipeRunner Pass 1.4 (blob detect → analyzer) + DI wiring

**Files:** `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs`, `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

Read the existing Pass 1.3 **gear** branch first — the pcd branch is its sibling but uses `_holeDetector` instead of `_edgeDetector`.

- [ ] **Step 1: Add gear-style pcd field to `ToolRunResult`** (near `Gear`):
```csharp
        // v9 PCD 工具：分析結果（供 overlay 節圓/孔中心/缺孔 + MeasurementWorkflow 四判定）。
        public FlashMeasurementSystem.Domain.PcdAnalysis.PcdAnalysisResult Pcd;
```

- [ ] **Step 2: Inject `IHoleDetector<HImage>` into RecipeRunner.** Add field + ctor param (append to the existing ctor signature; update the one caller in Step 6):
```csharp
        private readonly IHoleDetector<HImage> _holeDetector;
```
In the constructor parameter list add `IHoleDetector<HImage> holeDetector` and body `_holeDetector = holeDetector;`. Add `using FlashMeasurementSystem.Application.HoleDetection;` and `using FlashMeasurementSystem.Domain.PcdAnalysis;`.

- [ ] **Step 3: Add a Pass 1.4 pcd loop** immediately after the Pass 1.3 gear loop:
```csharp
            // ── Pass 1.4：PCD 螺栓孔圈（環帶 blob 偵測孔 → 純 Domain 圓擬合/四判定）──
            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null || tool.ToolType != "pcd") continue;
                if (tool.ArcRoi == null || tool.Pcd == null) continue;   // Validator 已擋

                ArcMeasureRoi placed = ArcRoiTransform.TransformArc(_mapper, tool.ArcRoi, transform);
                var res = new ToolRunResult { Name = tool.Name, ToolType = tool.ToolType, PlacedArc = placed, Supported = true };

                HoleDetectionResult hd = _holeDetector.DetectHolesInAnnulus(image, placed, tool.Pcd);
                if (!hd.Success)
                {
                    res.Measured = false;
                    res.ValueText = "PCD 量測失敗";
                    res.Message = string.IsNullOrEmpty(hd.ErrorMessage) ? "孔偵測失敗" : hd.ErrorMessage;
                    results.Add(res); if (!string.IsNullOrEmpty(tool.Id)) byId[tool.Id] = res; continue;
                }

                PcdAnalysisResult pr = PcdAnalyzer.Analyze(hd.Holes, pixelSizeUm, tool.Pcd);
                res.Pcd = pr;
                res.Measured = pr.Success;
                res.ValueText = pr.Message;
                res.IsOk = pr.Success ? pr.IsPass : (bool?)null;
                if (!pr.Success) res.Message = pr.Message;
                results.Add(res); if (!string.IsNullOrEmpty(tool.Id)) byId[tool.Id] = res;
            }
```
Add usings if missing: `FlashMeasurementSystem.Domain.HoleDetection`. `pixelSizeUm` is the local computed at the top of `Run()` (`(pixelSizeUmX + pixelSizeUmY)/2.0`) — confirm it is in scope at this point; if the passes are in a helper without it, thread it in the same way the gear/circle passes receive it.

- [ ] **Step 4: Exclude pcd from later passes** (mirror the arc/gear exclusion). In Pass 2's catch-all and any pass whose guard would pick up `pcd`, add `if (tool.ToolType == "pcd") continue;` exactly where arc/gear have one. Trace a pcd tool through the later passes and add exclusions only where a stray `(未支援)` row would appear.

- [ ] **Step 5: Wire the detector in `MainWindow.cs` DI.** Find where `_recipeRunner = new RecipeRunner(...)` is constructed (~line 104) and where `_metrologyRunner` etc. are `new`ed. Add a field `private readonly IHoleDetector<HImage> _holeDetector = new HalconHoleDetector();` and pass `_holeDetector` into the `RecipeRunner(...)` call in the correct new parameter position. Add `using FlashMeasurementSystem.Application.HoleDetection;` and `using FlashMeasurementSystem.Halcon.HoleDetection;`.

- [ ] **Step 6: Build + both suites → 0/0, both `EXITCODE=0`.**

- [ ] **Step 7: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
git commit -m "feat(pcd): run pcd tools in RecipeRunner via annulus blob detect + analyzer"
```

---

## Task 5: MeasurementWorkflow — four per-condition judgments (GD&T/gear precedent)

**Files:** `src/FlashMeasurementSystem.App.Wpf/MeasurementWorkflow.cs`

Read the existing `tool.Gear != null` branch — the pcd branch is its sibling and MUST sit **before** the `tool.Tolerance != null` path.

- [ ] **Step 1: Add a pcd branch** right after the gear branch (`else if (tool != null && tool.Gear != null) {...}`), before the `tool.Tolerance != null` branch:
```csharp
                else if (tool != null && tool.Pcd != null)
                {
                    // PCD 為四判定（孔數/PCD/角均勻/徑向真圓度），不走單值雙邊判定器（會用 MeasuredValue=0 誤判）。
                    // 以 tool.Pcd != null 為鑰：成功發四列、失敗發一列，兩者都不落入下方 Tolerance/GetMeasuredValue。
                    string baseName = tool.Name ?? r.Name;
                    if (r.Pcd != null && r.Pcd.Success)
                    {
                        var g = r.Pcd;
                        judgments.Add(new ItemJudgment { ToolId = tool.Id ?? "", ToolName = baseName + "-孔數",
                            MeasuredValue = g.HoleCount, Nominal = tool.Pcd.NominalHoleCount,
                            LowerLimit = tool.Pcd.NominalHoleCount, UpperLimit = tool.Pcd.NominalHoleCount, Unit = "count",
                            Deviation = g.HoleCount - tool.Pcd.NominalHoleCount, IsOk = g.CountOk, Message = "孔數" });
                        judgments.Add(new ItemJudgment { ToolId = tool.Id ?? "", ToolName = baseName + "-PCD",
                            MeasuredValue = g.PcdMm, Nominal = tool.Pcd.NominalPcdMm,
                            LowerLimit = tool.Pcd.NominalPcdMm - tool.Pcd.PcdToleranceMm,
                            UpperLimit = tool.Pcd.NominalPcdMm + tool.Pcd.PcdToleranceMm, Unit = "mm",
                            Deviation = g.PcdMm - tool.Pcd.NominalPcdMm, IsOk = g.PcdOk, Message = "節圓直徑" });
                        judgments.Add(new ItemJudgment { ToolId = tool.Id ?? "", ToolName = baseName + "-角均勻",
                            MeasuredValue = g.AngularMaxDevDeg, Nominal = 0, LowerLimit = 0,
                            UpperLimit = tool.Pcd.AngularToleranceDeg, Unit = "deg",
                            Deviation = g.AngularMaxDevDeg, IsOk = g.AngularOk, Message = "角度最大偏差" });
                        judgments.Add(new ItemJudgment { ToolId = tool.Id ?? "", ToolName = baseName + "-徑向真圓度",
                            MeasuredValue = g.RadialMaxDevMm, Nominal = 0, LowerLimit = 0,
                            UpperLimit = tool.Pcd.RadialToleranceMm, Unit = "mm",
                            Deviation = g.RadialMaxDevMm, IsOk = g.RadialOk, Message = "徑向最大偏差" });
                    }
                    else
                    {
                        judgments.Add(new ItemJudgment { ToolId = tool.Id ?? "", ToolName = baseName,
                            MeasuredValue = 0, IsOk = r.IsOk ?? false, Message = r.Message ?? "" });
                    }
                }
```
Note: the tool-level OK/NG increment already ran at the top of the loop from `r.IsOk` (and audit #1 counts a measurement failure as NG). Do NOT add extra counting here.

- [ ] **Step 2: Confirm `GetMeasuredValue` is unreachable for pcd** — the branch owns `tool.Pcd != null` regardless of Success, so a pcd tool never reaches the `Tolerance`/`GetMeasuredValue` path. No `GetMeasuredValue` change needed.

- [ ] **Step 3: Build + both suites → 0/0, both `EXITCODE=0`.**

- [ ] **Step 4: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/MeasurementWorkflow.cs
git commit -m "feat(pcd): emit four per-condition judgments (count/PCD/angular/radial) to the report"
```

---

## Task 6: RecipeEditor pcd panel (reuse arc panel + pcd params)

**Files:** `src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`

Read the **gear** panel wiring first (`AddTool("gear")` defaults, `_gearGroup`/`FillGearGroup`, panel visibility `isGear`, `DeepCopyTool` gear+arc copy, `LoadGearFieldsFromSelectedTool`, trial). The pcd panel is the same shape with pcd params.

- [ ] **Step 1: "+ 螺栓孔圈" toolbar button + AddTool defaults.** Add `_addPcdButton` next to `_addGearButton`; `Click → AddTool("pcd")`; tooltip "PCD 螺栓孔圈：量孔數/節圓直徑/角均勻/真圓度（背光）". In `AddTool`, for `"pcd"` seed a valid default `ArcRoi` (same as arc/gear default) + a default `Pcd`:
```csharp
            if (toolType == "pcd")
            {
                tool.ArcRoi = new ArcMeasureRoi { CenterRow = 200, CenterCol = 200, Radius = 100,
                    AngleStart = 0.0, AngleExtent = 2.0 * Math.PI, AnnulusRadius = 15.0 };
                tool.Pcd = new PcdAnalysisParameters();
            }
```
Do NOT set `tool.Tolerance` for pcd. Add `using FlashMeasurementSystem.Domain.PcdAnalysis;`.

- [ ] **Step 2: Pcd-params group (7 controls).** Add `_pcdGroup` + `FillPcdGroup` built with the same `NewTable`/`AddNumericRow`/`AddRow` helpers `FillGearGroup` uses: `_pcdCountNumeric` (NominalHoleCount, int, `DecimalPlaces=0`, min 1, max 10000), `_pcdNominalNumeric` (NominalPcdMm, mm, `DecimalPlaces=3`, min 0, max 100000), `_pcdTolNumeric` (PcdToleranceMm, mm, `DecimalPlaces=3`, min 0.001), `_pcdAngTolNumeric` (AngularToleranceDeg, deg, `DecimalPlaces=2`, min 0.01, max 360), `_pcdRadTolNumeric` (RadialToleranceMm, mm, `DecimalPlaces=3`, min 0.001), `_pcdDarkCheck` (CheckBox "孔為暗（背光）"), `_pcdMinAreaNumeric` (MinHoleAreaPx, px, `DecimalPlaces=0`, min 1, max 1e7). Each change handler writes to `_selectedTool.Pcd` + `MarkDirty()`, guarded by `_updatingControls` (single `WritePcd()` like `WriteGear`). Chinese tooltips.

- [ ] **Step 3: `LoadPcdFieldsFromSelectedTool()`** — load all seven under a save/restore `_updatingControls` guard (mirror `LoadGearFieldsFromSelectedTool`: `bool prev = _updatingControls; _updatingControls = true; try {...} finally { _updatingControls = prev; }`, and each `WritePcd` early-returns on `_updatingControls`).

- [ ] **Step 4: Panel visibility in `PopulateFromTool`.** Add `bool isPcd = tool.ToolType == "pcd";`. Extend the arc-group show to `_arcGroup.Visible = isArc || isGear || isPcd;`, edge group `_edgeGroup.Visible = isElement || isArc || isGear` (pcd does NOT use edge params → leave edge group hidden for pcd), show `_pcdGroup.Visible = isPcd;`, hide bilateral tolerance `_toleranceGroup.Visible = !isGdt && !isGear && !isPcd;`. Add an `else if (isPcd)` load branch calling `LoadArcFieldsFromSelectedTool()` AND `LoadPcdFieldsFromSelectedTool()`.

- [ ] **Step 5: `DeepCopyTool` must copy `Pcd`.** Add — after the `Gear` deep-copy — a null-safe deep copy of `Pcd` into a new `PcdAnalysisParameters` (all seven fields). **This is the exact class of bug that made arc unshippable (dropped ArcRoi); do it for Pcd.**
```csharp
                Pcd = src.Pcd == null ? null : new PcdAnalysisParameters
                {
                    NominalHoleCount = src.Pcd.NominalHoleCount, NominalPcdMm = src.Pcd.NominalPcdMm,
                    PcdToleranceMm = src.Pcd.PcdToleranceMm, AngularToleranceDeg = src.Pcd.AngularToleranceDeg,
                    RadialToleranceMm = src.Pcd.RadialToleranceMm, HoleIsDark = src.Pcd.HoleIsDark,
                    MinHoleAreaPx = src.Pcd.MinHoleAreaPx
                },
```

- [ ] **Step 6: Capture + trial + band for pcd.** pcd reuses the arc ROI, so: (a) broaden the capture path + `InstallArcBandOverlay` + interactive-edit guards from `isArc || isGear` to also include pcd (i.e. wherever the code keys off `"arc" || "gear"`, add `"pcd"`; the capture/band/`OnToolArcChanged` already key off `_selectedTool.ArcRoi != null`, so they work unchanged); (b) `RefreshTrialButtonEnabled` — allow `"pcd"`; (c) `OnTrialMeasure` — broaden the arc/gear result branch guard to include `"pcd"`, reuse `DrawArcBand` + `result.ValueText`; keep the rect2 (0,0) box gated OFF for pcd (extend the `usesArcRoi`/`isGear` locals to cover pcd).

- [ ] **Step 7: Build x64 → 0/0** (editor UI has no automated coverage; build + no-regression only).

- [ ] **Step 8: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs
git commit -m "feat(pcd): add pcd tool panel (reuse arc ROI + pcd params) to RecipeEditor"
```

---

## Task 7: PCD result overlay + synthetic fixture

**Files:** `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`; plus a throwaway fixture generator (not committed).

Read the existing gear overlay loop in `DrawRecipeResults` first — the pcd loop is its sibling.

- [ ] **Step 1: Draw pcd results in `DrawRecipeResults`.** Inside the existing single overlay lambda, after the gear loop and before `an.DrawResultTable(rows)`, add a pcd loop (do NOT add a second `SetPersistentOverlayAction`):
```csharp
                // PCD 工具結果：Roi 刻意留 null（Pass 1.4），畫框那段不會經過。畫量測環帶 + 擬合節圓
                // + 各孔中心十字 + 缺孔提示（洋紅）+ 名稱/數值。角度→(row,col) 用 row=cr+R·sinθ、col=cc+R·cosθ。
                foreach (ToolRunResult r in results)
                {
                    if (r == null || r.ToolType != "pcd" || r.PlacedArc == null) continue;
                    string pcdColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                    ArcMeasureRoi a = r.PlacedArc;
                    an.DrawArcBand(a.CenterRow, a.CenterCol, a.Radius, a.AngleStart, a.AngleExtent, a.AnnulusRadius);
                    if (r.Pcd != null && r.Pcd.Success)
                    {
                        var g = r.Pcd;
                        an.DrawCircle(g.CenterRow, g.CenterCol, g.PcdPx / 2.0, pcdColor);   // 擬合節圓
                        foreach (var hole in g.Holes)
                            an.DrawCross(hole.Row, hole.Col, 12, pcdColor);
                        foreach (double hintDeg in g.MissingHoleHintsDeg)
                        {
                            double th = hintDeg * Math.PI / 180.0;
                            an.DrawCross(g.CenterRow + (g.PcdPx / 2.0) * Math.Sin(th),
                                         g.CenterCol + (g.PcdPx / 2.0) * Math.Cos(th), 18, "magenta");
                        }
                    }
                    an.DrawText(r.ValueText ?? (r.Name ?? string.Empty), (int)a.CenterRow, (int)a.CenterCol, pcdColor);
                }
```
Confirm the annotator local is the same `an` used by the gear loop, and that `DrawCircle(row, col, radius, color)` exists on the annotator (grep `OverlayAnnotator` — the fitted-circle overlay for the `circle` tool already draws a circle; reuse that method name/signature; if it differs, match the real one and disclose).

- [ ] **Step 2: Build x64 + both suites → 0/0, both `EXITCODE=0`.**

- [ ] **Step 3: Synthetic pcd fixture (for GUI verification; do NOT commit).** Write a Python/PIL script (scratchpad) that renders a bright disk on black with N dark holes (filled circles) evenly spaced on a circle of radius `Rpx` about the image centre, and a second image with one hole removed. Save under `data/images/` (gitignored — confirm via `git check-ignore`). Print the true hole count, centre, `Rpx`, and expected PCD in px (2·Rpx) and mm (2·Rpx·pixelSizeUm/1000 with pixelSizeUm from `data/calibrations/CALIB-DEFAULT.json` = 10 → mm = px/100). Name e.g. `pcd_6_ok.png` / `pcd_5_missing.png`. Do NOT `git add` images or script.

- [ ] **Step 4: Manual GUI verification (human-driven).**
  1. Load `pcd_6_ok.png`. Edit Recipe → **+ 螺栓孔圈** → 擷取弧形 ROI → drag the annulus so it straddles all holes (mid radius on the bolt circle, annulus wide enough to include the holes). Set 標稱孔數 = true count, 標稱 PCD = expected mm, tolerances, 孔為暗 ✓.
  2. Save → reload → pcd fields (count/PCD/tols/dark/min-area) AND the ArcRoi survive the round-trip.
  3. 一鍵量測 → overlay draws band + the fitted PCD circle through the hole centres + a cross at each hole + "孔數=N PCD=…mm …PASS/FAIL"; on-image result table + banner reflect it.
  4. Open the CSV → the pcd tool produced **four rows** (孔數/PCD/角均勻/徑向真圓度), each value/limit/verdict correct and **consistent with the screen**; the tool counts as **one** OK/NG.
  5. Load `pcd_5_missing.png` → count = N−1 → **FAIL (red)**, and a **magenta** marker at the missing hole gap.
  6. (If a matched/reference-pose recipe is available) rotate the part → the band + fitted circle follow the pose.

- [ ] **Step 5: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
git commit -m "feat(pcd): draw pcd results (band + fitted PCD circle + hole markers + missing hints) on the overlay"
```

---

## Self-Review
**Spec coverage:** §4 analyzer (Kåsa fit + 4 conditions + mm via pixelSizeUm + missing-hole) → Task 1. §5.1 schema v9 + Pcd reuses PcdAnalysisParameters → Task 2. §5.2 IHoleDetector/HalconHoleDetector blob pipeline → Task 3. §5.3 RecipeRunner Pass 1.4 reuse ArcRoiTransform + ResolvePixelSize → Task 4. §5.4 four ItemJudgments via GD&T/gear precedent, tool-level OK/NG once → Task 5. §5.5 editor pcd panel reusing arc ROI → Task 6. §5.6 overlay fitted circle + hole markers + missing hints → Task 7. §5.7 validator (+ NOT in DoubleSidedToleranceTypes) → Task 2. §6 tests → Tasks 1,2 + GUI Task 7. §7 lessons checklist: schema-test knock-on (Task 2), DeepCopyTool copies Pcd+ArcRoi (Task 6), GetMeasuredValue unreachable (Task 5), editor/trial (Task 6), overlay Roi-null (Task 7), pass exclusions (Task 4), measurement-failure→NG inherited (audit #1). ✓

**Placeholder scan:** the pass-exclusion tracing (Task 4 Step 4), the schema-version literal lines (Task 2 Step 4), and the HALCON operator-signature verification (Task 3 Step 3) are "read the real code/reference and match" instructions against concrete targets; each ships concrete code + exact required behaviour. No TBD/TODO.

**Type consistency:** `HolePoint`(Row/Col), `PcdAnalysisParameters`(NominalHoleCount/NominalPcdMm/PcdToleranceMm/AngularToleranceDeg/RadialToleranceMm/HoleIsDark/MinHoleAreaPx), `PcdAnalysisResult`(+CenterRow/Col/PcdPx/PcdMm/AngularMaxDevDeg/RadialMaxDevMm/Holes/MissingHoleHintsDeg/Count-Pcd-Angular-RadialOk), `PcdAnalyzer.Analyze(IList<HolePoint>, double pixelSizeUm, PcdAnalysisParameters)`, `HoleDetectionResult`(Success/ErrorMessage/Holes), `IHoleDetector<TImage>.DetectHolesInAnnulus(TImage, ArcMeasureRoi, PcdAnalysisParameters)`, `MeasurementTool.Pcd`, `ToolRunResult.Pcd`, `ToolType=="pcd"` are identical across Tasks 1-7. ✓

**Known risk flagged for the executor:** Task 1's synthetic geometry uses `row=cr+R·sinθ, col=cc+R·cosθ` and the analyzer's `atan2(row−cr,col−cc)`, so measured angle == intended θ — verify the identity when running; if a case is off, fix the TEST tolerance/geometry (never the analyzer) and disclose. Also confirm `pixelSizeUm` is in scope at RecipeRunner Pass 1.4 (Task 4 Step 3) before using it.
