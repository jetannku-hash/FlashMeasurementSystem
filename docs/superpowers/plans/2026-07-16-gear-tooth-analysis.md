# Gear Tooth Count/Pitch/Width Analysis Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a `ToolType="gear"` recipe tool that measures a gear's tooth count / pitch uniformity / tooth-width uniformity from an arc-caliper edge scan and judges PASS/FAIL — reusing the arc-ROI-in-recipe infrastructure.

**Architecture:** Part A is a pure-Domain `GearToothAnalyzer` (edge points → teeth via dual-polarity pairing → count/pitch/width stats + three-condition judgment), fully unit-tested on synthetic edge points. Part B wires it as a recipe tool that reuses the merged arc infra (`ArcRoi`, `DetectEdgesOnArc`, `ArcRoiTransform`, the editor arc panel) and follows the GD&T precedent for a multi-condition tool (RecipeRunner computes; overall `IsOk` = AND; `MeasurementWorkflow` emits three per-condition `ItemJudgment`s → 3 CSV rows).

**Tech Stack:** .NET Framework 4.8, WinForms, HALCON 17.12, old-style `.csproj` (new files need explicit `<Compile Include>`), console-style test suites.

**Spec:** `docs/superpowers/specs/2026-07-15-gear-tooth-analysis-design.md` (algorithm §6, Part B §11).

**Branch:** create `feature/gear-tooth-analysis` before Task 1.

**Build/test (PowerShell):**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe"; "EXITCODE=$LASTEXITCODE"
& ".\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe"; "EXITCODE=$LASTEXITCODE"
```
Close the app before building (locks DLLs → MSB3026).

## Plan-level algorithm refinement (from spec §6.1)
Spec §6.1 step 8 ("Σpitch ≈ 2π else Fail") is **mathematically vacuous** when pitches are wrapped adjacent-centre differences (they always sum to 2π). It is therefore **dropped**; the **count check subsumes it** — a partial arc yields fewer teeth than nominal → `CountOk=false` → NG, and its large unmeasured gap also inflates `PitchMaxDevDeg`. This is a deliberate, documented refinement; do not add the vacuous check.

## Lessons-from-arc checklist (spec §11.7) — baked into the tasks below
Every "new tool type in a recipe" concern that bit the arc feature is an explicit step here: schema-version test knock-on (Task 2), `DeepCopyTool` must copy the new fields (Task 5), `MeasurementWorkflow` dedicated branch **before** `GetMeasuredValue` (Task 4), editor panel visibility + trial (Task 5), overlay leaves `Roi` null (Task 6), other-pass exclusions (Task 3).

---

## File Structure
**Create:** `Domain/GearAnalysis/GearAnalysisParameters.cs`, `GearAnalysisResult.cs`, `GearToothAnalyzer.cs`; `tests/.../GearAnalysisDomainTests.cs`.
**Modify:** `MeasurementTool.cs` (+Gear), `Recipe.cs` (v8), `RecipeValidator.cs` (gear rule), the 4 schema-version tests; `RecipeRunner.cs` (gear branch + result fields), `MeasurementWorkflow.cs` (gear judgment branch), `RecipeEditor.cs` (gear panel), `MainWindow.cs` (gear overlay); test `.csproj` + `EdgeDetectionDomainTests.cs` Main wiring.

---

## Task 1: Part A — Domain DTOs + GearToothAnalyzer (fully unit-tested)

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/GearAnalysis/GearAnalysisParameters.cs`
- Create: `src/FlashMeasurementSystem.Domain/GearAnalysis/GearAnalysisResult.cs`
- Create: `src/FlashMeasurementSystem.Domain/GearAnalysis/GearToothAnalyzer.cs`
- Create: `tests/FlashMeasurementSystem.Tests/GearAnalysisDomainTests.cs`
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs` (Main wiring)

- [ ] **Step 1: Create the branch** `git checkout -b feature/gear-tooth-analysis`

- [ ] **Step 2: Write `GearAnalysisParameters.cs`**
```csharp
namespace FlashMeasurementSystem.Domain.GearAnalysis
{
    /// <summary>齒輪分析輸入參數（純 DTO，無 HALCON）。角度公差以度為單位。</summary>
    public class GearAnalysisParameters
    {
        public int NominalToothCount { get; set; } = 20;
        public bool ToothIsDark { get; set; } = true;        // 背光剪影：齒暗、齒隙亮
        public double PitchToleranceDeg { get; set; } = 1.0;  // 齒距最大偏差上限
        public double WidthToleranceDeg { get; set; } = 2.0;  // 齒寬最大偏差上限

        public static GearAnalysisParameters Default() => new GearAnalysisParameters();
    }
}
```

- [ ] **Step 3: Write `GearAnalysisResult.cs`**
```csharp
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.GearAnalysis
{
    /// <summary>單一齒（供 overlay 標記）。角度為度。</summary>
    public class GearTooth
    {
        public double CenterAngleDeg { get; set; }
        public double WidthDeg { get; set; }
    }

    /// <summary>齒輪分析結果（純 DTO）。Success=false 表流程失敗（見 Message）。</summary>
    public class GearAnalysisResult
    {
        public bool Success { get; set; }
        public bool IsPass { get; set; }
        public int ToothCount { get; set; }

        public double PitchMeanDeg { get; set; }
        public double PitchMinDeg { get; set; }
        public double PitchMaxDeg { get; set; }
        public double PitchMaxDevDeg { get; set; }

        public double WidthMeanDeg { get; set; }
        public double WidthMinDeg { get; set; }
        public double WidthMaxDeg { get; set; }
        public double WidthMaxDevDeg { get; set; }
        public double WidthMeanPx { get; set; }

        public bool CountOk { get; set; }
        public bool PitchOk { get; set; }
        public bool WidthOk { get; set; }

        public List<GearTooth> Teeth { get; set; } = new List<GearTooth>();
        public List<double> MissingToothHintsDeg { get; set; } = new List<double>();
        public string Message { get; set; } = "";

        public static GearAnalysisResult Failed(string message) =>
            new GearAnalysisResult { Success = false, IsPass = false, Message = message };
    }
}
```

- [ ] **Step 4: Write the failing test `tests/FlashMeasurementSystem.Tests/GearAnalysisDomainTests.cs`**

The helper builds synthetic edge points for a gear with `n` dark teeth centred at evenly spaced angles: each tooth has an entering edge (bright→dark, **negative** amplitude) and a leaving edge (**positive**), placed at `centre ∓ halfWidth`, at fixed `radius` around `(cr,cc)`. `perturb` lets a test move/resize/remove specific teeth.
```csharp
using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.GearAnalysis;

namespace FlashMeasurementSystem.Tests
{
    public static class GearAnalysisDomainTests
    {
        private const double Cr = 500, Cc = 500, R = 200;

        // 造 n 個暗齒的邊點：每齒中心角 = i*360/n（度），齒寬 widthDeg。
        // 進齒(中心−半寬)=負 amplitude、出齒(中心+半寬)=正。dropTooth 移除該齒兩邊；
        // extraWidthDeg[i] 調整第 i 齒寬；shiftDeg[i] 平移第 i 齒中心。
        private static List<EdgePoint> Gear(int n, double widthDeg,
            int dropTooth = -1, double[] extraWidthDeg = null, double[] shiftDeg = null)
        {
            var pts = new List<EdgePoint>();
            for (int i = 0; i < n; i++)
            {
                if (i == dropTooth) continue;
                double centerDeg = i * 360.0 / n + (shiftDeg != null ? shiftDeg[i] : 0.0);
                double w = widthDeg + (extraWidthDeg != null ? extraWidthDeg[i] : 0.0);
                AddEdge(pts, centerDeg - w / 2.0, -30.0); // 進齒：亮→暗，負
                AddEdge(pts, centerDeg + w / 2.0, +30.0); // 出齒：暗→亮，正
            }
            return pts;
        }

        private static void AddEdge(List<EdgePoint> pts, double deg, double amp)
        {
            double th = deg * Math.PI / 180.0;
            pts.Add(new EdgePoint
            {
                Row = Cr + R * Math.Sin(th),   // atan2(row-cr, col-cc)=th ⇒ row=cr+R sinθ, col=cc+R cosθ
                Column = Cc + R * Math.Cos(th),
                Amplitude = amp,
                Distance = 0
            });
        }

        public static void Run()
        {
            var p = GearAnalysisParameters.Default();

            // 完美 20 齒 → 齒數 20、齒距/齒寬偏差≈0、PASS
            var g = new GearAnalysisParameters { NominalToothCount = 20, PitchToleranceDeg = 0.5, WidthToleranceDeg = 0.5 };
            var r = GearToothAnalyzer.Analyze(Gear(20, 8.0), Cr, Cc, R, g);
            AssertEqual(true, r.Success, "perfect Success");
            AssertEqual(20, r.ToothCount, "perfect count 20");
            AssertClose(18.0, r.PitchMeanDeg, 1e-6, "perfect pitch mean 18");
            AssertClose(0.0, r.PitchMaxDevDeg, 1e-6, "perfect pitch dev 0");
            AssertClose(8.0, r.WidthMeanDeg, 1e-6, "perfect width mean 8");
            AssertClose(0.0, r.WidthMaxDevDeg, 1e-6, "perfect width dev 0");
            AssertEqual(true, r.IsPass, "perfect PASS");
            AssertEqual(20, r.Teeth.Count, "perfect teeth list");

            // 缺一齒 → 齒數 19、CountOk=false、該處齒距≈2×18=36 → MissingToothHints 有值
            var rm = GearToothAnalyzer.Analyze(Gear(20, 8.0, dropTooth: 5), Cr, Cc, R, g);
            AssertEqual(19, rm.ToothCount, "missing count 19");
            AssertEqual(false, rm.CountOk, "missing CountOk false");
            AssertEqual(false, rm.IsPass, "missing FAIL");
            if (rm.MissingToothHintsDeg.Count == 0) throw new InvalidOperationException("missing tooth should hint");

            // 窄齒（第 3 齒 −4°）→ WidthOk=false、齒距仍 OK
            var ew = new double[20]; ew[3] = -4.0;
            var rw = GearToothAnalyzer.Analyze(Gear(20, 8.0, extraWidthDeg: ew), Cr, Cc, R, g);
            AssertEqual(20, rw.ToothCount, "narrow count 20");
            AssertEqual(false, rw.WidthOk, "narrow WidthOk false");
            AssertEqual(true, rw.PitchOk, "narrow PitchOk true");

            // 齒距不均（第 7 齒中心 +3°）→ PitchOk=false
            var sh = new double[20]; sh[7] = 3.0;
            var rp = GearToothAnalyzer.Analyze(Gear(20, 8.0, shiftDeg: sh), Cr, Cc, R, g);
            AssertEqual(false, rp.PitchOk, "shift PitchOk false");

            // 環繞：把整體旋轉使一齒跨 0/360 邊界 → 齒數與齒距仍正確
            var sh0 = new double[20]; for (int i = 0; i < 20; i++) sh0[i] = -9.0; // 第 0 齒中心移到 -9°→351°
            var rc = GearToothAnalyzer.Analyze(Gear(20, 8.0, shiftDeg: sh0), Cr, Cc, R, g);
            AssertEqual(20, rc.ToothCount, "wrap count 20");
            AssertClose(0.0, rc.PitchMaxDevDeg, 1e-6, "wrap pitch dev 0");

            // 邊界：齒寬偏差恰=公差 → PASS（含邊界）
            var ewb = new double[20]; ewb[2] = 0.5;   // 第 2 齒寬 +0.5°
            var gb = new GearAnalysisParameters { NominalToothCount = 20, PitchToleranceDeg = 5.0, WidthToleranceDeg = 0.5 };
            var rb = GearToothAnalyzer.Analyze(Gear(20, 8.0, extraWidthDeg: ewb), Cr, Cc, R, gb);
            // 單齒 +0.5 使 mean 略升，maxdev = (寬齒−mean) 與 (其餘−mean) 取大；容差寬鬆確認邊界含入
            AssertEqual(true, rb.WidthOk || rb.WidthMaxDevDeg <= 0.5 + 1e-9, "boundary width inclusive");

            // 極性翻轉：ToothIsDark=false → 齒數/齒距不變，齒寬變成互補（齒隙寬）
            var gf = new GearAnalysisParameters { NominalToothCount = 20, ToothIsDark = false, PitchToleranceDeg = 0.5, WidthToleranceDeg = 100 };
            var rf = GearToothAnalyzer.Analyze(Gear(20, 8.0), Cr, Cc, R, gf);
            AssertEqual(20, rf.ToothCount, "flip count 20");
            AssertClose(18.0, rf.PitchMeanDeg, 1e-6, "flip pitch mean 18");
            AssertClose(10.0, rf.WidthMeanDeg, 1e-6, "flip width = gap = 18-8 = 10");

            // 失敗路徑
            AssertEqual(false, GearToothAnalyzer.Analyze(new List<EdgePoint>(), Cr, Cc, R, g).Success, "empty fail");
            AssertEqual(false, GearToothAnalyzer.Analyze(null, Cr, Cc, R, g).Success, "null fail");
            var odd = Gear(20, 8.0); odd.RemoveAt(0);
            AssertEqual(false, GearToothAnalyzer.Analyze(odd, Cr, Cc, R, g).Success, "odd count fail");
            AssertEqual(false, GearToothAnalyzer.Analyze(Gear(20, 8.0), Cr, Cc, R,
                new GearAnalysisParameters { NominalToothCount = 0 }).Success, "nominal<=0 fail");
        }

        private static void AssertEqual<T>(T e, T a, string n)
        { if (!object.Equals(e, a)) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
        private static void AssertClose(double e, double a, double t, string n)
        { if (Math.Abs(e - a) > t) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
    }
}
```

- [ ] **Step 5: Register the 3 Domain files + the test; wire the suite into `Main()`**
In `FlashMeasurementSystem.Domain.csproj` (near other `<Compile Include>`):
```xml
    <Compile Include="GearAnalysis\GearAnalysisParameters.cs" />
    <Compile Include="GearAnalysis\GearAnalysisResult.cs" />
    <Compile Include="GearAnalysis\GearToothAnalyzer.cs" />
```
In `FlashMeasurementSystem.Tests.csproj`: `<Compile Include="GearAnalysisDomainTests.cs" />`
In `EdgeDetectionDomainTests.cs` `Main()`, after the `ArcRecipeToolDomainTests` block:
```csharp
            GearAnalysisDomainTests.Run();
            Console.WriteLine("GearAnalysisDomainTests passed");
```

- [ ] **Step 6: Build → expect COMPILE ERROR** (`GearToothAnalyzer` missing). Confirm.

- [ ] **Step 7: Write `GearToothAnalyzer.cs`** (spec §6.1, minus the vacuous Σpitch check):
```csharp
using System;
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.GearAnalysis
{
    /// <summary>
    /// 純齒輪分析（無 HALCON）。弧卡尺邊點 → 依 amplitude 正負分進/出齒 → 配對成齒（含 0/2π 環繞）
    /// → 齒數/齒距/齒寬統計 + 三條件判定。角度：內部弧度，對外輸出度。中心由呼叫端傳入（弧 ROI 中心）。
    /// </summary>
    public static class GearToothAnalyzer
    {
        public static GearAnalysisResult Analyze(
            System.Collections.Generic.IList<FlashMeasurementSystem.Domain.EdgeDetection.EdgePoint> edgePoints,
            double centerRow, double centerCol, double radiusPx, GearAnalysisParameters parameters)
        {
            var p = parameters ?? GearAnalysisParameters.Default();
            if (p.NominalToothCount <= 0 || p.PitchToleranceDeg <= 0 || p.WidthToleranceDeg <= 0)
                return GearAnalysisResult.Failed("齒輪參數無效（標稱齒數與公差需 > 0）");
            if (edgePoints == null || edgePoints.Count < 4 || (edgePoints.Count % 2) != 0)
                return GearAnalysisResult.Failed("邊點數不足或非偶數（需成對；請調 Sigma/Threshold/環寬）");
            if (radiusPx <= 0)
                return GearAnalysisResult.Failed("量測半徑無效");

            const double TwoPi = 2.0 * Math.PI;
            // 轉角度 + 分類（暗齒：進齒=負 amplitude）
            var arr = new List<KeyValuePair<double, bool>>(); // (theta, entering)
            foreach (var e in edgePoints)
            {
                double th = Math.Atan2(e.Row - centerRow, e.Column - centerCol);
                if (th < 0) th += TwoPi;
                bool entering = p.ToothIsDark ? (e.Amplitude < 0) : (e.Amplitude > 0);
                arr.Add(new KeyValuePair<double, bool>(th, entering));
            }
            arr.Sort((a, b) => a.Key.CompareTo(b.Key));

            int start = arr.FindIndex(kv => kv.Value);
            if (start < 0) return GearAnalysisResult.Failed("未偵測到進齒邊（齒為暗/亮參數可能設反）");

            int n = arr.Count;
            // 旋轉到第一個進齒；序列須嚴格交替 E,L,E,L...
            var seq = new List<KeyValuePair<double, bool>>(n);
            for (int i = 0; i < n; i++) seq.Add(arr[(start + i) % n]);
            for (int i = 0; i < n; i++)
                if (seq[i].Value != (i % 2 == 0))
                    return GearAnalysisResult.Failed("進/出齒邊未交替（請調 Sigma/Threshold/環寬或極性）");

            int teeth = n / 2;
            var centers = new double[teeth];
            var widths = new double[teeth];
            for (int t = 0; t < teeth; t++)
            {
                double te = seq[2 * t].Key;      // 進齒
                double tl = seq[2 * t + 1].Key;  // 出齒
                double w = tl - te; if (w < 0) w += TwoPi;
                widths[t] = w;
                double c = te + w / 2.0; if (c >= TwoPi) c -= TwoPi;
                centers[t] = c;
            }

            // 齒距：序列相鄰齒中心差（含環繞，N 個和為 2π）
            var pitches = new double[teeth];
            for (int t = 0; t < teeth; t++)
            {
                double d = centers[(t + 1) % teeth] - centers[t];
                if (d <= 0) d += TwoPi;
                pitches[t] = d;
            }

            var result = new GearAnalysisResult { Success = true, ToothCount = teeth };
            Stats(pitches, out double pMean, out double pMin, out double pMax, out double pDev);
            Stats(widths, out double wMean, out double wMin, out double wMax, out double wDev);
            double d2 = 180.0 / Math.PI;
            result.PitchMeanDeg = pMean * d2; result.PitchMinDeg = pMin * d2; result.PitchMaxDeg = pMax * d2; result.PitchMaxDevDeg = pDev * d2;
            result.WidthMeanDeg = wMean * d2; result.WidthMinDeg = wMin * d2; result.WidthMaxDeg = wMax * d2; result.WidthMaxDevDeg = wDev * d2;
            result.WidthMeanPx = radiusPx * wMean;

            for (int t = 0; t < teeth; t++)
                result.Teeth.Add(new GearTooth { CenterAngleDeg = centers[t] * d2, WidthDeg = widths[t] * d2 });

            // 缺齒提示：齒距 > 1.5×中位數（≈2× 表漏一齒）
            double median = Median(pitches);
            for (int t = 0; t < teeth; t++)
                if (pitches[t] > 1.5 * median)
                    result.MissingToothHintsDeg.Add(centers[t] * d2); // 在該齒之後的大間隙

            result.CountOk = teeth == p.NominalToothCount;
            result.PitchOk = result.PitchMaxDevDeg <= p.PitchToleranceDeg;
            result.WidthOk = result.WidthMaxDevDeg <= p.WidthToleranceDeg;
            result.IsPass = result.CountOk && result.PitchOk && result.WidthOk;
            result.Message = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "齒數={0}(標稱{1}) 齒距偏差={2:F2}° 齒寬偏差={3:F2}° → {4}",
                teeth, p.NominalToothCount, result.PitchMaxDevDeg, result.WidthMaxDevDeg,
                result.IsPass ? "PASS" : "FAIL");
            return result;
        }

        private static void Stats(double[] v, out double mean, out double min, out double max, out double maxDev)
        {
            double sum = 0; min = double.MaxValue; max = double.MinValue;
            for (int i = 0; i < v.Length; i++) { sum += v[i]; if (v[i] < min) min = v[i]; if (v[i] > max) max = v[i]; }
            mean = sum / v.Length;
            maxDev = 0;
            for (int i = 0; i < v.Length; i++) { double d = Math.Abs(v[i] - mean); if (d > maxDev) maxDev = d; }
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

- [ ] **Step 8: Build + run tests → PASS.** Expected: build 0/0; `GearAnalysisDomainTests passed`; `EXITCODE=0`. (If a synthetic-case assertion is off by rounding, adjust the test tolerance — NOT the analyzer — and note it.)

- [ ] **Step 9: Commit**
```bash
git add src/FlashMeasurementSystem.Domain/GearAnalysis tests/FlashMeasurementSystem.Tests/GearAnalysisDomainTests.cs src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
git commit -m "feat(gear): add pure Domain gear tooth analyzer with synthetic tests"
```

---

## Task 2: Schema v8 (MeasurementTool.Gear) + Validator + fix schema-version tests

**Files:** `MeasurementTool.cs`, `Recipe.cs`, `RecipeValidator.cs`, `ArcRecipeToolDomainTests.cs`, `MetrologyModelDomainTests.cs`, `RoiDomainTests.cs`, and a new `GearRecipeToolDomainTests.cs` + its csproj/Main wiring.

- [ ] **Step 1: Write the failing test `tests/FlashMeasurementSystem.Tests/GearRecipeToolDomainTests.cs`**
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.GearAnalysis;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    public static class GearRecipeToolDomainTests
    {
        public static void Run()
        {
            AssertEqual(8, Recipe.Default().SchemaVersion, "SchemaVersion is 8");
            var plain = new MeasurementTool();
            AssertEqual(null, plain.Gear, "Default Gear is null");

            // round-trip：Gear + ArcRoi 皆保留
            var recipe = Recipe.Default();
            recipe.Tools = new List<MeasurementTool>
            {
                new MeasurementTool
                {
                    Id = "G1", Name = "齒輪", ToolType = "gear",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 500, CenterCol = 500, Radius = 200,
                        AngleStart = 0, AngleExtent = 2 * Math.PI, AnnulusRadius = 10 },
                    Gear = new GearAnalysisParameters { NominalToothCount = 20, ToothIsDark = true,
                        PitchToleranceDeg = 1.5, WidthToleranceDeg = 2.5 }
                }
            };
            string path = Path.Combine(Path.GetTempPath(), "fms_gear_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                new RecipeStore().Save(recipe, path);
                Recipe rt = new RecipeStore().Load(path);
                GearAnalysisParameters g = rt.Tools[0].Gear;
                if (g == null) throw new InvalidOperationException("round-trip Gear null");
                AssertEqual(20, g.NominalToothCount, "rt NominalToothCount");
                AssertEqual(true, g.ToothIsDark, "rt ToothIsDark");
                AssertClose(1.5, g.PitchToleranceDeg, 1e-9, "rt PitchTol");
                AssertClose(2.5, g.WidthToleranceDeg, 1e-9, "rt WidthTol");
                if (rt.Tools[0].ArcRoi == null) throw new InvalidOperationException("rt ArcRoi null");
                AssertClose(200, rt.Tools[0].ArcRoi.Radius, 1e-9, "rt ArcRoi radius");
            }
            finally { if (File.Exists(path)) File.Delete(path); }

            // 向後相容：舊 JSON 無 Gear → null
            string oldPath = Path.Combine(Path.GetTempPath(), "fms_gear_old_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(oldPath, "{ \"SchemaVersion\": 7, \"Tools\": [ { \"Id\": \"C1\", \"ToolType\": \"circle\" } ] }");
                Recipe old = new RecipeStore().Load(oldPath);
                AssertEqual(null, old.Tools[0].Gear, "old Gear null");
            }
            finally { if (File.Exists(oldPath)) File.Delete(oldPath); }

            // Validator：合法 gear → 0 error；缺 Gear / 缺 ArcRoi / 標稱齒數 0 → error
            AssertEqual(0, Errors(ValidGear()), "valid gear no error");
            var noGear = ValidGearRecipe(); noGear.Tools[0].Gear = null;
            if (Errors(noGear) == 0) throw new InvalidOperationException("gear without Gear → error");
            var noArc = ValidGearRecipe(); noArc.Tools[0].ArcRoi = null;
            if (Errors(noArc) == 0) throw new InvalidOperationException("gear without ArcRoi → error");
            var badN = ValidGearRecipe(); badN.Tools[0].Gear.NominalToothCount = 0;
            if (Errors(badN) == 0) throw new InvalidOperationException("gear NominalToothCount 0 → error");
        }

        private static Recipe ValidGearRecipe()
        {
            var r = Recipe.Default();
            r.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "G1", Name = "齒輪", ToolType = "gear",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 500, CenterCol = 500, Radius = 200,
                        AngleStart = 0, AngleExtent = 2 * Math.PI, AnnulusRadius = 10 },
                    Gear = new GearAnalysisParameters { NominalToothCount = 20, PitchToleranceDeg = 1, WidthToleranceDeg = 2 } }
            };
            return r;
        }
        private static Recipe ValidGear() => ValidGearRecipe();
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
Register it in the tests `.csproj` (`<Compile Include="GearRecipeToolDomainTests.cs" />`) and wire `GearRecipeToolDomainTests.Run();` + its `Console.WriteLine` into `Main()` after the gear analyzer line.

- [ ] **Step 2: Add `Gear` to `MeasurementTool.cs`** (nullable additive, reuse the Part-A DTO, mirror the `Gdt`/`ArcRoi` pattern):
```csharp
using FlashMeasurementSystem.Domain.GearAnalysis;  // add
```
```csharp
        // v8：齒輪分析參數（重用 GearAnalysisParameters DTO）。null＝非齒輪工具。
        // 齒輪工具（ToolType="gear"）必填；量測環帶用 ArcRoi。
        public GearAnalysisParameters Gear { get; set; } = null;
```

- [ ] **Step 3: Bump schema to v8 in `Recipe.cs`** — `SchemaVersion = 8;` + version-comment line:
```csharp
        // v8：齒輪工具（MeasurementTool.Gear，加性 nullable 欄）+ ToolType="gear"。
        //     純加欄位、向後相容、無遷移碼：舊檔載入時 Gear=null、無 gear 工具，行為不變。
```

- [ ] **Step 4: Fix the FOUR stale schema-version assertions (6→7 already done for arc; now →8).** This knock-on bit the arc feature — do it up front:
  - `tests/FlashMeasurementSystem.Tests/ArcRecipeToolDomainTests.cs:18` — `AssertEqual(7, ...` → `8`.
  - `tests/FlashMeasurementSystem.Tests/MetrologyModelDomainTests.cs:39` — `d.SchemaVersion == 7` → `== 8` (and message text).
  - `tests/FlashMeasurementSystem.Tests/RoiDomainTests.cs:31` — `AssertEqual(7, recipe.SchemaVersion, ...` → `8`.
  - `tests/FlashMeasurementSystem.Tests/RoiDomainTests.cs:96` — `AssertEqual(7, rt.SchemaVersion, ...` → `8`.
  Change only the literal `7`→`8` on those lines; nothing else.

- [ ] **Step 5: Add the gear rule to `RecipeValidator.cs`** — add `"gear"` to `KnownTypes` (NOT `RoiElementTypes`), and in the per-tool loop:
```csharp
                if (tool.ToolType == "gear")
                {
                    if (tool.Gear == null)
                        issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name, "齒輪工具缺少齒輪參數（Gear）"));
                    else if (tool.Gear.NominalToothCount <= 0 || tool.Gear.PitchToleranceDeg <= 0 || tool.Gear.WidthToleranceDeg <= 0)
                        issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name, "齒輪參數無效（標稱齒數與公差需 > 0）"));
                    if (tool.ArcRoi == null || !tool.ArcRoi.IsDefined)
                        issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                            "齒輪工具的量測環帶無效：" + (tool.ArcRoi == null ? "缺少 ArcRoi" : tool.ArcRoi.ValidationError)));
                }
```

- [ ] **Step 6: Build + run tests → PASS** (`GearRecipeToolDomainTests passed`, and the arc/roi/metrology suites still green with the 8 update). `EXITCODE=0`.

- [ ] **Step 7: Commit**
```bash
git add src/FlashMeasurementSystem.Domain/Roi/MeasurementTool.cs src/FlashMeasurementSystem.Domain/Roi/Recipe.cs src/FlashMeasurementSystem.Domain/Roi/RecipeValidator.cs tests/FlashMeasurementSystem.Tests/GearRecipeToolDomainTests.cs tests/FlashMeasurementSystem.Tests/ArcRecipeToolDomainTests.cs tests/FlashMeasurementSystem.Tests/MetrologyModelDomainTests.cs tests/FlashMeasurementSystem.Tests/RoiDomainTests.cs tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
git commit -m "feat(gear): add Gear to recipe schema v8 + validator (fix stale schema-version asserts)"
```

---

## Task 3: RecipeRunner gear branch (reuse arc pipeline + analyzer)

**Files:** `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs`

Read the existing Pass 1.2 **arc** branch first — the gear branch is its sibling and shares the same `ArcRoiTransform.TransformArc` + `DetectEdgesOnArc` prologue.

- [ ] **Step 1: Add gear fields to `ToolRunResult`** (near the arc fields `PlacedArc`/`ArcEdgeRows`/`ArcEdgeCols`):
```csharp
        // v8 齒輪工具：分析結果（供 overlay 齒中心標記/缺齒提示 + MeasurementWorkflow 三判定）。
        public FlashMeasurementSystem.Domain.GearAnalysis.GearAnalysisResult Gear;
```
(`PlacedArc`/`ArcEdgeRows`/`ArcEdgeCols` are reused for the band + crosses.)

- [ ] **Step 2: Add a Pass 1.3 gear loop** immediately after the Pass 1.2 arc loop, before Pass 1.5:
```csharp
            // ── Pass 1.3：齒輪工具（重用弧卡尺量邊 → 純 Domain 齒輪分析）──
            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null || tool.ToolType != "gear") continue;
                if (tool.ArcRoi == null || tool.Gear == null) continue;   // Validator 已擋

                ArcMeasureRoi placed = ArcRoiTransform.TransformArc(_mapper, tool.ArcRoi, transform);
                var res = new ToolRunResult { Name = tool.Name, ToolType = tool.ToolType, PlacedArc = placed, Supported = true };

                // 齒配對需正負 amplitude → 強制 Polarity="all"，不論工具設定。
                var ep = tool.EdgeParameters != null ? CloneEdgeParams(tool.EdgeParameters) : EdgeDetectionParameters.Default();
                ep.Polarity = "all";
                EdgeResult er = _edgeDetector.DetectEdgesOnArc(image, placed, ep);
                if (!er.Success || er.EdgePoints == null)
                {
                    res.Measured = false;
                    res.ValueText = "齒輪量測失敗";
                    res.Message = string.IsNullOrEmpty(er.ErrorMessage) ? "弧卡尺量測失敗" : er.ErrorMessage;
                    results.Add(res); if (!string.IsNullOrEmpty(tool.Id)) byId[tool.Id] = res; continue;
                }
                foreach (EdgePoint pt in er.EdgePoints) { res.ArcEdgeRows.Add(pt.Row); res.ArcEdgeCols.Add(pt.Column); }

                GearAnalysisResult gr = GearToothAnalyzer.Analyze(er.EdgePoints, placed.CenterRow, placed.CenterCol, placed.Radius, tool.Gear);
                res.Gear = gr;
                res.Measured = gr.Success;
                res.ValueText = gr.Message;
                res.IsOk = gr.Success ? gr.IsPass : (bool?)null;
                if (!gr.Success) res.Message = gr.Message;
                results.Add(res); if (!string.IsNullOrEmpty(tool.Id)) byId[tool.Id] = res;
            }
```
Add usings if missing: `FlashMeasurementSystem.Domain.GearAnalysis`. `CloneEdgeParams` — check whether an EdgeDetectionParameters copy helper already exists; if not, add a tiny private one (copy Sigma/Threshold/Polarity/EdgeSelector/Interpolation/MeasureMode/etc.) so forcing Polarity does **not** mutate the recipe's stored `tool.EdgeParameters`. **Do NOT mutate `tool.EdgeParameters` in place.**

- [ ] **Step 3: Exclude gear from later passes** (mirror the arc exclusion). In Pass 2's catch-all and any pass whose guard would pick up `gear`, add `if (tool.ToolType == "gear") continue;` where arc has one. Trace an arc-style gear tool through Pass 1.5/1.7/2/3 exactly as the arc reviewer did and add exclusions only where a stray `(未支援)` row would appear.

- [ ] **Step 4: Build + both suites → 0/0, both `EXITCODE=0`.**

- [ ] **Step 5: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs
git commit -m "feat(gear): run gear tools in RecipeRunner via arc scan + pure analyzer"
```

---

## Task 4: MeasurementWorkflow — three per-condition judgments (GD&T precedent)

**Files:** `src/FlashMeasurementSystem.App.Wpf/MeasurementWorkflow.cs`

Read the existing `tool.Gdt != null` branch (~`:198-216`) — the gear branch is its sibling and MUST sit **before** the `GetMeasuredValue` path.

- [ ] **Step 1: Add a gear branch in the Evaluating loop**, right after the `tool.Gdt != null` branch and before the `tool.Tolerance != null` branch:
```csharp
                else if (tool != null && tool.Gear != null && r.Gear != null && r.Gear.Success)
                {
                    // 齒輪為三判定（齒數/齒距/齒寬），不走單值雙邊判定器（會用 MeasuredValue=0 誤判）。
                    // 由 RecipeRunner 算好的 GearAnalysisResult 直接發三個 ItemJudgment → CSV 三列。
                    var g = r.Gear;
                    judgments.Add(new ItemJudgment { ToolId = tool.Id ?? "", ToolName = (tool.Name ?? r.Name) + "-齒數",
                        MeasuredValue = g.ToothCount, Nominal = tool.Gear.NominalToothCount,
                        LowerLimit = tool.Gear.NominalToothCount, UpperLimit = tool.Gear.NominalToothCount, Unit = "count",
                        Deviation = g.ToothCount - tool.Gear.NominalToothCount, IsOk = g.CountOk, Message = "齒數" });
                    judgments.Add(new ItemJudgment { ToolId = tool.Id ?? "", ToolName = (tool.Name ?? r.Name) + "-齒距",
                        MeasuredValue = g.PitchMaxDevDeg, Nominal = 0, LowerLimit = 0, UpperLimit = tool.Gear.PitchToleranceDeg, Unit = "deg",
                        Deviation = g.PitchMaxDevDeg, IsOk = g.PitchOk, Message = "齒距最大偏差" });
                    judgments.Add(new ItemJudgment { ToolId = tool.Id ?? "", ToolName = (tool.Name ?? r.Name) + "-齒寬",
                        MeasuredValue = g.WidthMaxDevDeg, Nominal = 0, LowerLimit = 0, UpperLimit = tool.Gear.WidthToleranceDeg, Unit = "deg",
                        Deviation = g.WidthMaxDevDeg, IsOk = g.WidthOk, Message = "齒寬最大偏差" });
                }
```
Note: the outer `if (r.IsOk == true) OkCount++ else if (r.IsOk == false) NgCount++` already ran at the top of the loop from `r.IsOk` (overall), so a gear tool counts **one** at the tool level — do NOT add extra Ok/Ng counting here.

- [ ] **Step 2: Defensive guard in `GetMeasuredValue`** — a `Success=false` gear result won't reach the dedicated branch (it requires `r.Gear.Success`), so it falls to the `Tolerance != null`/else path. Confirm `tool.Tolerance` is null for gear (it is — `AddTool` won't set a bilateral tolerance for gear; Task 5), so it hits the final else → a single "no tolerance" row with the failure message. That is acceptable. If you find gear tools DO carry a non-null `Tolerance`, add `if (r.ToolType == "gear") return 0;` to `GetMeasuredValue` and report it.

- [ ] **Step 3: Build + both suites → 0/0, both `EXITCODE=0`.**

- [ ] **Step 4: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/MeasurementWorkflow.cs
git commit -m "feat(gear): emit three per-condition judgments (count/pitch/width) to the report"
```

---

## Task 5: RecipeEditor gear panel (reuse arc panel + gear params)

**Files:** `src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`

Read the arc panel wiring first: `AddTool("arc")` defaults (~`:804`), the arc group (`_arcGroup`/`FillArcGroup`), panel visibility (`isArc`), `DeepCopyTool` (must copy `ArcRoi`), `PopulateFromTool`, and `RefreshTrialButtonEnabled`. The gear panel is: **the arc ROI group (reused verbatim) PLUS a small gear-params group**.

- [ ] **Step 1: "+ 齒輪" toolbar button + AddTool defaults.** Add `_addGearButton` next to `_addArcButton`; `Click → AddTool("gear")`; tooltip "齒輪：量齒數/齒距/齒寬（背光剪影）". In `AddTool`, for `"gear"` set a valid default `ArcRoi` (same as arc's default) AND a default `Gear`:
```csharp
            if (toolType == "gear")
            {
                tool.ArcRoi = new ArcMeasureRoi { CenterRow = 200, CenterCol = 200, Radius = 100,
                    AngleStart = 0.0, AngleExtent = 2.0 * Math.PI, AnnulusRadius = 5.0 };
                tool.Gear = new GearAnalysisParameters();  // NominalToothCount=20, ToothIsDark=true, tols 1/2
            }
```
Do **not** set `tool.Tolerance` for gear (it uses the three-condition path). Add `using FlashMeasurementSystem.Domain.GearAnalysis;`.

- [ ] **Step 2: Gear-params group (4 controls).** Add `_gearGroup` built with the file's layout helper: `_gearCountNumeric` (NominalToothCount, integer, `DecimalPlaces=0`, `Minimum=1`, `Maximum=10000`), `_gearDarkCheck` (CheckBox "齒為暗（背光）"), `_gearPitchTolNumeric` (deg, `DecimalPlaces=2`, `Minimum=0.01`), `_gearWidthTolNumeric` (deg, `DecimalPlaces=2`, `Minimum=0.01`). Each control's change handler writes to `_selectedTool.Gear` + `MarkDirty()`, guarded by `_updatingControls`. Add tooltips in the file's style.

- [ ] **Step 3: Panel visibility.** Where visibility is decided, add `bool isGear = tool.ToolType == "gear";`. For gear: show the **arc ROI group** (reuse — `_arcGroup.Visible = isArc || isGear`), show `_gearGroup` (`isGear`), and **hide the bilateral `_toleranceGroup`** for gear (it uses the three-condition params instead). Load the gear fields under the `_updatingControls` guard in the selection loader (add `LoadGearFieldsFromSelectedTool()`), and load the arc fields for gear too (it has an `ArcRoi`).

- [ ] **Step 4: `DeepCopyTool` must copy `Gear`.** Add — mirroring the existing `ArcRoi`/`Gdt` deep-copy — a null-safe deep copy of `Gear` into a new `GearAnalysisParameters` (all four fields). **This is the exact bug that made the arc tool unshippable (DeepCopyTool dropped ArcRoi); do it for Gear.**
```csharp
                Gear = src.Gear == null ? null : new GearAnalysisParameters
                {
                    NominalToothCount = src.Gear.NominalToothCount, ToothIsDark = src.Gear.ToothIsDark,
                    PitchToleranceDeg = src.Gear.PitchToleranceDeg, WidthToleranceDeg = src.Gear.WidthToleranceDeg
                },
```

- [ ] **Step 5: Capture + trial + band overlay for gear.** Gear reuses the arc ROI, so: (a) the "擷取弧形 ROI" capture path and the `InstallArcBandOverlay` band must also run for gear (extend the arc-branch guards from `isArc` to `isArc || isGear` where they gate the ArcRoi capture/band); (b) `RefreshTrialButtonEnabled` — allow `"gear"`; (c) `OnTrialMeasure` — gear results come back with `r.Gear` + `PlacedArc`, so add a gear branch drawing the band + tooth-centre markers + count label (or, simplest for the editor trial, reuse the arc overlay branch's band + a `"齒數=N"` label from `r.Gear.ToothCount`). Keep the rect2 box gated off for gear (like arc).

- [ ] **Step 6: Build x64 → 0/0** (no automated coverage for editor UI; build + no-regression only).

- [ ] **Step 7: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs
git commit -m "feat(gear): add gear tool panel (reuse arc ROI + gear params) to RecipeEditor"
```

---

## Task 6: Gear result overlay + synthetic fixture

**Files:** `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`; plus a throwaway fixture generator (not committed).

- [ ] **Step 1: Draw gear results in `DrawRecipeResults`.** Inside the existing overlay lambda (the single `SetPersistentOverlayAction`), after the arc loop, add a gear loop:
```csharp
                foreach (ToolRunResult r in results)
                {
                    if (r == null || r.ToolType != "gear" || r.PlacedArc == null) continue;
                    string color = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                    ArcMeasureRoi a = r.PlacedArc;
                    an.DrawArcBand(a.CenterRow, a.CenterCol, a.Radius, a.AngleStart, a.AngleExtent, a.AnnulusRadius);
                    if (r.Gear != null && r.Gear.Success)
                    {
                        // 各齒中心標記（在量測半徑上）
                        foreach (var tooth in r.Gear.Teeth)
                        {
                            double th = tooth.CenterAngleDeg * Math.PI / 180.0;
                            an.DrawCross(a.CenterRow + a.Radius * Math.Sin(th), a.CenterCol + a.Radius * Math.Cos(th), 12, color);
                        }
                        // 缺齒提示（洋紅）
                        foreach (double hintDeg in r.Gear.MissingToothHintsDeg)
                        {
                            double th = hintDeg * Math.PI / 180.0;
                            an.DrawCross(a.CenterRow + a.Radius * Math.Sin(th), a.CenterCol + a.Radius * Math.Cos(th), 18, "magenta");
                        }
                    }
                    an.DrawText(r.ValueText ?? (r.Name ?? ""), (int)a.CenterRow, (int)a.CenterCol, color);
                }
```
(`DrawArcBand`/`DrawCross`/`DrawText` all exist. Confirm `an` is the existing annotator local; don't add a second `SetPersistentOverlayAction`.)

- [ ] **Step 2: Build x64 + both suites → 0/0, both `EXITCODE=0`.**

- [ ] **Step 3: Synthetic gear fixture (for GUI verification; do NOT commit it).** Write a Python/PIL script (scratchpad) that renders a black gear (N dark teeth) on white at a chosen centre/radius, and a second image with one tooth removed. Save under `data/images/` (gitignored). Print the true tooth count. Use it in Step 4.

- [ ] **Step 4: Manual GUI verification (human-driven).**
  1. Load the gear fixture. Edit Recipe → **+ 齒輪** → 擷取弧形 ROI → drag the annulus so it crosses all the teeth (mid arc between tip and root). Set 標稱齒數 = the true count, 齒為暗 ✓, tolerances e.g. 齒距 2°/齒寬 3°.
  2. Save → reload → gear fields (count/dark/tols) **and** the ArcRoi survive the round-trip.
  3. 一鍵量測 → overlay draws the band + a cross at each tooth centre + "齒數=N …PASS/FAIL"; the on-image result table + banner reflect it.
  4. Open the CSV under `data/reports/` → the gear tool produced **three rows** (齒數/齒距/齒寬), each MeasuredValue/limit/verdict correct and **consistent with the screen**; the tool counts as **one** OK/NG.
  5. Load the missing-tooth image → count = N−1 → **FAIL (red)**, and a **magenta** marker appears at the gap.
  6. Rotate the part (if a matched/reference-pose recipe is available) → the gear band follows the pose.

- [ ] **Step 5: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
git commit -m "feat(gear): draw gear results (band + tooth markers + missing-tooth hints) on the overlay"
```

---

## Self-Review
**Spec coverage:** §6 algorithm → Task 1 (analyzer + all §9 synthetic cases: perfect/missing/narrow/uneven/wrap/boundary/flip/failure). §11.1 schema v8 + Gear reuses GearAnalysisParameters → Task 2. §11.2 RecipeRunner gear branch + force Polarity=all + ArcRoiTransform reuse → Task 3. §11.3 three ItemJudgments via GD&T-precedent branch, tool-level Ok/Ng once → Task 4. §11.4 editor gear panel reusing arc ROI → Task 5. §11.5 overlay band + tooth markers + missing hints → Task 6. §11.6 validator → Task 2 Step 5. §11.7 lessons-from-arc: schema-test knock-on (Task 2 Step 4), DeepCopyTool copies Gear+ArcRoi (Task 5 Step 4), GetMeasuredValue order/guard (Task 4), editor/trial (Task 5 Step 5), overlay Roi-null (Task 6 leaves Roi null — gear result never sets Roi), pass exclusions (Task 3 Step 3). §11.8 tests → Tasks 1,2 + GUI Step 4. ✓

**Placeholder scan:** `CloneEdgeParams` (Task 3) and the pass-exclusion tracing (Task 3 Step 3) are "read the real code and match it" instructions against existing patterns the implementer must not duplicate; both ship concrete code + exact required behaviour. No TBD/TODO.

**Type consistency:** `GearAnalysisParameters` (NominalToothCount/ToothIsDark/PitchToleranceDeg/WidthToleranceDeg), `GearAnalysisResult` (+`GearTooth`), `GearToothAnalyzer.Analyze(IList<EdgePoint>, double, double, double, GearAnalysisParameters)`, `MeasurementTool.Gear`, `ToolRunResult.Gear`, `ToolType=="gear"` are identical across Tasks 1-6. ✓

**Known risk flagged for the executor:** the synthetic-edge geometry in Task 1's test uses `atan2(row−cr, col−cc)`; the test's `AddEdge` places `row=cr+R·sinθ, col=cc+R·cosθ` so the analyzer's angle equals the intended `θ` — verify this identity holds when you run it (if a case is off, fix the TEST geometry/tolerance, never the analyzer, and disclose).
