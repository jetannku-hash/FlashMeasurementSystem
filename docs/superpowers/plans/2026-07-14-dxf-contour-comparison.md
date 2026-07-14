# DXF/CAD Contour Comparison (v1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a standalone "DXF/CAD 輪廓度比對" action that loads a DXF nominal contour, locates the part via a DXF-derived scaled shape model, extracts the actual contour, computes per-point deviation, and reports profile-tolerance PASS/FAIL — all in pixel space, no hardware calibration.

**Architecture:** Feature-adapter pattern across the project's four layers. Pure statistics/judgment live in Domain (`DxfDeviationEvaluator`, fully unit-tested). HALCON pipeline lives in one adapter (`HalconDxfContourComparer`, manually verified on synthetic data). A standalone WinForms Form (`DxfComparisonForm`, hand-written layout like `RecipeEditor`) drives it, launched from one programmatically-added MainWindow button (avoids the `MainWindow.Designer.cs` regeneration landmine).

**Tech Stack:** .NET Framework 4.8, WinForms, HALCON 17.12 (`halcondotnet.dll`), old-style `.csproj` (new files need explicit `<Compile Include>`). Console-style test suites (throw on failure, wired into `Main()`).

**Spec:** `docs/superpowers/specs/2026-07-14-dxf-contour-comparison-design.md`

**Branch:** create `feature/dxf-contour-comparison` before Task 1.

**Build/test commands (Windows PowerShell):**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe"; "EXITCODE=$LASTEXITCODE"
```
Close the running app before building (it locks output DLLs → MSB3026).

---

## File Structure

**Create:**
- `src/FlashMeasurementSystem.Domain/DxfComparison/DxfComparisonParameters.cs` — input DTO (tolerance, scale range/seed, edge + band + DXF-sampling params) + `Default()`.
- `src/FlashMeasurementSystem.Domain/DxfComparison/DxfComparisonResult.cs` — output DTO (pass/fail, stats, pose, message) + `Failed()`.
- `src/FlashMeasurementSystem.Domain/DxfComparison/DxfDeviationEvaluator.cs` — pure: deviations[] + tolerance → stats + pass. **The unit-tested core.**
- `src/FlashMeasurementSystem.Application/DxfComparison/IDxfContourComparer.cs` — generic interface over Domain types.
- `src/FlashMeasurementSystem.Halcon/DxfComparison/HalconDxfContourComparer.cs` — the HALCON pipeline adapter.
- `tests/FlashMeasurementSystem.Tests/DxfComparisonDomainTests.cs` — console suite for Domain.
- `src/FlashMeasurementSystem.App.Wpf/DxfComparisonForm.cs` — standalone Form (hand-written layout).

**Modify:**
- Each project's `.csproj` — add `<Compile Include>` for the new files.
- `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs` — wire `DxfComparisonDomainTests.Run()` into `Main()`.
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs` — add `_dxfComparer` field + a launch button created programmatically in `OnLoad`.

---

## Task 1: Domain DTOs + Deviation Evaluator (fully unit-tested core)

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/DxfComparison/DxfComparisonParameters.cs`
- Create: `src/FlashMeasurementSystem.Domain/DxfComparison/DxfComparisonResult.cs`
- Create: `src/FlashMeasurementSystem.Domain/DxfComparison/DxfDeviationEvaluator.cs`
- Test: `tests/FlashMeasurementSystem.Tests/DxfComparisonDomainTests.cs`
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`

- [ ] **Step 1: Create the branch**

```bash
git checkout -b feature/dxf-contour-comparison
```

- [ ] **Step 2: Write `DxfComparisonParameters.cs`**

```csharp
namespace FlashMeasurementSystem.Domain.DxfComparison
{
    /// <summary>
    /// DXF 輪廓度比對輸入參數（純 DTO，無 HALCON）。像素空間；mm 僅顯示用。
    /// </summary>
    public class DxfComparisonParameters
    {
        public double TolerancePx { get; set; } = 2.0;       // 輪廓度公差 T（px）
        public double MinScore { get; set; } = 0.5;          // find_scaled_shape_model 最低分
        public double ScaleMin { get; set; } = 0.5;          // scale 搜尋下界（無種子時用）
        public double ScaleMax { get; set; } = 2.0;          // scale 搜尋上界（無種子時用）
        public double ScaleSeedPxPerMm { get; set; } = 0.0;  // >0 時作為 scale 種子（px/mm），收在 ±30%
        public double EdgeAlpha { get; set; } = 1.0;         // edges_sub_pix 濾波 alpha
        public double EdgeLowThreshold { get; set; } = 20.0;
        public double EdgeHighThreshold { get; set; } = 40.0;
        public double BandWidthPx { get; set; } = 10.0;      // 邊緣框帶半徑（≈ 數倍 T）
        public int MinNumPoints { get; set; } = 20;          // DXF 曲線最小取樣點
        public double MaxApproxError { get; set; } = 0.25;   // DXF 曲線近似最大誤差（px）

        public static DxfComparisonParameters Default() => new DxfComparisonParameters();
    }
}
```

- [ ] **Step 3: Write `DxfComparisonResult.cs`**

```csharp
namespace FlashMeasurementSystem.Domain.DxfComparison
{
    /// <summary>
    /// DXF 輪廓度比對結果（純 DTO）。Success=false 表示流程失敗（見 Message）。
    /// </summary>
    public class DxfComparisonResult
    {
        public bool Success { get; set; }
        public bool IsPass { get; set; }
        public double MaxDevPx { get; set; }
        public double MeanDevPx { get; set; }
        public double RmsDevPx { get; set; }
        public int PointsEvaluated { get; set; }
        public int PointsOverTolerance { get; set; }
        public double MatchScore { get; set; }
        public double PoseRow { get; set; }
        public double PoseCol { get; set; }
        public double PoseAngleRad { get; set; }
        public double PoseScale { get; set; }
        public string Message { get; set; } = "";

        public static DxfComparisonResult Failed(string message) =>
            new DxfComparisonResult { Success = false, IsPass = false, Message = message };
    }
}
```

- [ ] **Step 4: Write the failing test `DxfComparisonDomainTests.cs`**

```csharp
using System;
using FlashMeasurementSystem.Domain.DxfComparison;

namespace FlashMeasurementSystem.Tests
{
    public static class DxfComparisonDomainTests
    {
        public static void Run()
        {
            // ─── DTO 預設值 ───
            var p = DxfComparisonParameters.Default();
            AssertClose(2.0, p.TolerancePx, 1e-9, "Default TolerancePx");
            AssertClose(0.5, p.MinScore, 1e-9, "Default MinScore");
            AssertClose(10.0, p.BandWidthPx, 1e-9, "Default BandWidthPx");

            var failed = DxfComparisonResult.Failed("x");
            AssertEqual(false, failed.Success, "Failed Success false");
            AssertEqual(false, failed.IsPass, "Failed IsPass false");

            // ─── 統計正確性：偏差 {0,1,2,3}，T=2 ───
            // max=3, mean=1.5, rms=sqrt((0+1+4+9)/4)=sqrt(3.5)=1.8708, over(>2)=1(只有3)
            var r = DxfDeviationEvaluator.Evaluate(new double[] { 0, 1, 2, 3 }, 2.0);
            AssertEqual(true, r.Success, "Evaluate Success");
            AssertClose(3.0, r.MaxDevPx, 1e-9, "Max");
            AssertClose(1.5, r.MeanDevPx, 1e-9, "Mean");
            AssertClose(Math.Sqrt(3.5), r.RmsDevPx, 1e-9, "Rms");
            AssertEqual(4, r.PointsEvaluated, "PointsEvaluated");
            AssertEqual(1, r.PointsOverTolerance, "PointsOverTolerance (>2)");
            AssertEqual(false, r.IsPass, "max 3 > T 2 → FAIL");

            // ─── 無號：負偏差取絕對值 ───
            var rn = DxfDeviationEvaluator.Evaluate(new double[] { -3, 1 }, 2.0);
            AssertClose(3.0, rn.MaxDevPx, 1e-9, "Abs max from -3");

            // ─── 邊界含公差：max 恰 = T → PASS ───
            var rb = DxfDeviationEvaluator.Evaluate(new double[] { 0.5, 2.0 }, 2.0);
            AssertEqual(true, rb.IsPass, "max == T is PASS (inclusive)");
            AssertEqual(0, rb.PointsOverTolerance, "boundary not counted as over");

            // ─── 剛超出 ───
            var ro = DxfDeviationEvaluator.Evaluate(new double[] { 2.0001 }, 2.0);
            AssertEqual(false, ro.IsPass, "just over → FAIL");
            AssertEqual(1, ro.PointsOverTolerance, "1 over");

            // ─── 空/null → Success=false ───
            AssertEqual(false, DxfDeviationEvaluator.Evaluate(new double[0], 2.0).Success, "empty → fail");
            AssertEqual(false, DxfDeviationEvaluator.Evaluate(null, 2.0).Success, "null → fail");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }
    }
}
```

- [ ] **Step 5: Register new files in the `.csproj` files**

In `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`, add next to the other `<Compile Include>` entries (e.g. after the `Tolerance\` block near line 83):

```xml
    <Compile Include="DxfComparison\DxfComparisonParameters.cs" />
    <Compile Include="DxfComparison\DxfComparisonResult.cs" />
    <Compile Include="DxfComparison\DxfDeviationEvaluator.cs" />
```

In `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`, add near line 66:

```xml
    <Compile Include="DxfComparisonDomainTests.cs" />
```

- [ ] **Step 6: Wire the suite into `Main()`**

In `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`, after the `MetrologyModelDomainTests.Run();` block (around line 150), add:

```csharp
            DxfComparisonDomainTests.Run();
            Console.WriteLine("DxfComparisonDomainTests passed");
```

- [ ] **Step 7: Run the tests to verify they FAIL (evaluator not implemented yet)**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: **compile error** — `DxfDeviationEvaluator` does not exist. (That is the failing state.)

- [ ] **Step 8: Write minimal `DxfDeviationEvaluator.cs`**

```csharp
using System;

namespace FlashMeasurementSystem.Domain.DxfComparison
{
    /// <summary>
    /// 純偏差統計與輪廓度判定（無 HALCON）。輸入逐點偏差（px），輸出 max/mean/RMS/超差點數 + PASS/FAIL。
    /// HALCON adapter 取得 distance 屬性 tuple 後呼叫；判定含邊界（max ≤ T 為 PASS）。
    /// </summary>
    public static class DxfDeviationEvaluator
    {
        public static DxfComparisonResult Evaluate(double[] deviationsPx, double tolerancePx)
        {
            if (deviationsPx == null || deviationsPx.Length == 0)
                return DxfComparisonResult.Failed("無偏差資料（框帶內取不到實際輪廓點）");

            double max = 0.0, sum = 0.0, sumSq = 0.0;
            int over = 0;
            for (int i = 0; i < deviationsPx.Length; i++)
            {
                double d = Math.Abs(deviationsPx[i]); // 無號
                if (d > max) max = d;
                sum += d;
                sumSq += d * d;
                if (d > tolerancePx) over++;
            }
            int n = deviationsPx.Length;

            return new DxfComparisonResult
            {
                Success = true,
                MaxDevPx = max,
                MeanDevPx = sum / n,
                RmsDevPx = Math.Sqrt(sumSq / n),
                PointsEvaluated = n,
                PointsOverTolerance = over,
                IsPass = max <= tolerancePx
            };
        }
    }
}
```

- [ ] **Step 9: Build + run tests to verify PASS**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe"; "EXITCODE=$LASTEXITCODE"
```
Expected: build 0/0; output includes `DxfComparisonDomainTests passed`; `EXITCODE=0`.

- [ ] **Step 10: Commit**

```bash
git add src/FlashMeasurementSystem.Domain/DxfComparison tests/FlashMeasurementSystem.Tests/DxfComparisonDomainTests.cs src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
git commit -m "feat(dxf): add Domain DTOs and pure deviation evaluator with tests"
```

---

## Task 2: Application interface + contract test

**Files:**
- Create: `src/FlashMeasurementSystem.Application/DxfComparison/IDxfContourComparer.cs`
- Modify: `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/DxfComparisonDomainTests.cs`

- [ ] **Step 1: Write `IDxfContourComparer.cs`**

Generic over the image type keeps Application HALCON-free (same convention as `IEdgeDetector<TImage>` / `ITemplateMatcher<TImage,TRegion>`).

```csharp
using FlashMeasurementSystem.Domain.DxfComparison;

namespace FlashMeasurementSystem.Application.DxfComparison
{
    /// <summary>
    /// DXF 輪廓度比對介面。實作載入 DXF、定位工件、取實際輪廓、算偏差並判定。
    /// </summary>
    public interface IDxfContourComparer<TImage>
    {
        DxfComparisonResult Compare(TImage image, string dxfFilePath, DxfComparisonParameters parameters);
    }
}
```

- [ ] **Step 2: Register in Application `.csproj`**

In `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`, near the `Tolerance\IToleranceJudger.cs` entry (line 66):

```xml
    <Compile Include="DxfComparison\IDxfContourComparer.cs" />
```

- [ ] **Step 3: Add a contract test (Fake) to `DxfComparisonDomainTests.Run()`**

Insert before the final `}` of `Run()` (verifies the interface compiles over Domain types, same style as `FakeToleranceJudger`):

```csharp
            // ─── 介面契約（Fake）───
            FlashMeasurementSystem.Application.DxfComparison.IDxfContourComparer<object> fake =
                new FakeDxfComparer();
            var fr = fake.Compare(new object(), "x.dxf", DxfComparisonParameters.Default());
            AssertEqual(true, fr.Success, "Fake comparer satisfies interface contract");
            AssertEqual(true, fr.IsPass, "Fake comparer returns pass");
```

And add this nested class inside `DxfComparisonDomainTests` (after the `AssertClose` helper):

```csharp
        private sealed class FakeDxfComparer
            : FlashMeasurementSystem.Application.DxfComparison.IDxfContourComparer<object>
        {
            public DxfComparisonResult Compare(object image, string dxfFilePath, DxfComparisonParameters parameters)
                => new DxfComparisonResult { Success = true, IsPass = true, Message = "FAKE" };
        }
```

Also add the Application project reference guard: the Tests project already references Application (it uses `IToleranceJudger`), so no new project reference is needed.

- [ ] **Step 4: Build + run tests**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe"; "EXITCODE=$LASTEXITCODE"
```
Expected: build 0/0; `DxfComparisonDomainTests passed`; `EXITCODE=0`.

- [ ] **Step 5: Commit**

```bash
git add src/FlashMeasurementSystem.Application/DxfComparison src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj tests/FlashMeasurementSystem.Tests/DxfComparisonDomainTests.cs
git commit -m "feat(dxf): add IDxfContourComparer application interface + contract test"
```

---

## Task 3: HALCON adapter (`HalconDxfContourComparer`)

HALCON adapters are **not** unit-tested in this project (verified manually). This task builds the pipeline and verifies it on a synthetic DXF + rendered image.

**⚠️ Before writing each HALCON call, confirm its exact signature/parameter order against `halcon_pdf/reference/reference_hdevelop.txt`** (per `CLAUDE.md`). Verified core operators (with ref lines) are in the spec §9. The supporting operators used below (`count_obj`, `hom_mat2d_identity/scale/rotate/translate`, `gen_region_contour_xld`, `dilation_circle`, `reduce_domain`, `get_contour_attrib_xld`, `select_obj`) are standard but **verify their signatures before use**.

**Files:**
- Create: `src/FlashMeasurementSystem.Halcon/DxfComparison/HalconDxfContourComparer.cs`
- Modify: `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`

- [ ] **Step 1: Write `HalconDxfContourComparer.cs`**

```csharp
using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.DxfComparison;
using FlashMeasurementSystem.Domain.DxfComparison;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.DxfComparison
{
    /// <summary>
    /// HALCON DXF 輪廓度比對 adapter。載入 DXF→建 scaled shape model→定位→對位→
    /// 框帶內取實際輪廓→distance_contours_xld 逐點偏差→DxfDeviationEvaluator 判定。
    /// 所有 HALCON 例外轉 failed result（不外拋）；所有 handle 於 finally 釋放。
    /// </summary>
    public class HalconDxfContourComparer : IDxfContourComparer<HImage>
    {
        public DxfComparisonResult Compare(HImage image, string dxfFilePath, DxfComparisonParameters parameters)
        {
            var p = parameters ?? DxfComparisonParameters.Default();
            if (image == null) return DxfComparisonResult.Failed("影像為空");
            if (string.IsNullOrEmpty(dxfFilePath)) return DxfComparisonResult.Failed("未指定 DXF 檔");

            HObject nominal = null, alignedNominal = null, actualEdges = null, distContour = null;
            HRegion bandMargin = null, band = null;
            HImage gray = null, reduced = null;
            HTuple modelId = null;
            try
            {
                // 1. 讀 DXF → 標稱輪廓
                HTuple genNames = new HTuple(new string[] { "min_num_points", "max_approx_error" });
                HTuple genVals = new HTuple();
                genVals = genVals.TupleConcat(p.MinNumPoints).TupleConcat(p.MaxApproxError);
                HOperatorSet.ReadContourXldDxf(out nominal, dxfFilePath, genNames, genVals, out HTuple dxfStatus);
                HOperatorSet.CountObj(nominal, out HTuple nContours);
                if (nContours.Length == 0 || nContours.I == 0)
                    return DxfComparisonResult.Failed("DXF 無可讀輪廓（可能非 AC1009/R12 或實體不支援）");

                // 2. scaled shape model（scale 種子見 spec §6.1）
                double scaleMin = p.ScaleMin, scaleMax = p.ScaleMax;
                if (p.ScaleSeedPxPerMm > 0)
                {
                    scaleMin = p.ScaleSeedPxPerMm * 0.7;
                    scaleMax = p.ScaleSeedPxPerMm * 1.3;
                }
                HOperatorSet.CreateScaledShapeModelXld(nominal, "auto",
                    new HTuple(0.0), new HTuple(2.0 * Math.PI), "auto",
                    scaleMin, scaleMax, "auto",
                    "ignore_local_polarity", 5, out modelId);

                // 3. 單通道
                gray = EnsureSingleChannel(image);
                HImage work = gray ?? image;

                // 4. 定位
                HOperatorSet.FindScaledShapeModel(work, modelId,
                    new HTuple(0.0), new HTuple(2.0 * Math.PI), p.MinScore,
                    1, 0.5, "least_squares", 0, 0.9,
                    out HTuple row, out HTuple col, out HTuple angle, out HTuple scale, out HTuple score);
                if (score.Length == 0)
                    return DxfComparisonResult.Failed("工件未定位（無匹配或 score < MinScore）");

                // 5. 組 hom_mat2d（scale→rotate→translate）並對位標稱到影像 px
                HOperatorSet.HomMat2dIdentity(out HTuple hom);
                HOperatorSet.HomMat2dScale(hom, scale, scale, 0, 0, out hom);
                HOperatorSet.HomMat2dRotate(hom, angle, 0, 0, out hom);
                HOperatorSet.HomMat2dTranslate(hom, row, col, out hom);
                HOperatorSet.AffineTransContourXld(nominal, out alignedNominal, hom);

                // 6. 框帶內取實際輪廓（濾內部/雜訊邊）
                HOperatorSet.GenRegionContourXld(alignedNominal, out bandMargin, "margin");
                HOperatorSet.DilationCircle(bandMargin, out band, p.BandWidthPx);
                HOperatorSet.ReduceDomain(work, band, out reduced);
                HOperatorSet.EdgesSubPix(reduced, out actualEdges, "canny",
                    p.EdgeAlpha, p.EdgeLowThreshold, p.EdgeHighThreshold);
                HOperatorSet.CountObj(actualEdges, out HTuple nEdges);
                if (nEdges.Length == 0 || nEdges.I == 0)
                    return DxfComparisonResult.Failed("框帶內取不到實際輪廓（BandWidthPx/邊緣門檻需調整）");

                // 7. 逐點偏差：實際 → 標稱
                HOperatorSet.DistanceContoursXld(actualEdges, alignedNominal, out distContour, "point_to_segment");

                // 累積所有子輪廓的 distance 屬性
                var devs = new List<double>();
                HOperatorSet.CountObj(distContour, out HTuple nDist);
                for (int i = 1; i <= nDist.I; i++)
                {
                    HOperatorSet.SelectObj(distContour, out HObject one, i);
                    try
                    {
                        HOperatorSet.GetContourAttribXld(one, "distance", out HTuple d);
                        for (int k = 0; k < d.Length; k++) devs.Add(d[k].D);
                    }
                    finally { one?.Dispose(); }
                }

                // 8. 判定（純 Domain）+ 補姿態
                DxfComparisonResult result = DxfDeviationEvaluator.Evaluate(devs.ToArray(), p.TolerancePx);
                result.MatchScore = score[0].D;
                result.PoseRow = row[0].D;
                result.PoseCol = col[0].D;
                result.PoseAngleRad = angle[0].D;
                result.PoseScale = scale[0].D;
                if (result.Success)
                    result.Message = result.IsPass
                        ? string.Format("PASS  max={0:F3}px  mean={1:F3}px  (T={2:F3}px)", result.MaxDevPx, result.MeanDevPx, p.TolerancePx)
                        : string.Format("FAIL  max={0:F3}px > T={1:F3}px  超差 {2} 點", result.MaxDevPx, p.TolerancePx, result.PointsOverTolerance);
                return result;
            }
            catch (HalconException ex)
            {
                return DxfComparisonResult.Failed("DXF 比對錯誤：" + ex.Message);
            }
            finally
            {
                nominal?.Dispose();
                alignedNominal?.Dispose();
                actualEdges?.Dispose();
                distContour?.Dispose();
                bandMargin?.Dispose();
                band?.Dispose();
                gray?.Dispose();
                reduced?.Dispose();
                if (modelId != null) HOperatorSet.ClearShapeModel(modelId);
            }
        }

        // 3ch→rgb1_to_gray，其他→access_channel(1)；null=原圖已單通道。與其他 adapter 相同慣例。
        private static HImage EnsureSingleChannel(HImage source)
        {
            HOperatorSet.CountChannels(source, out HTuple channels);
            int channelCount = (channels != null && channels.Length > 0) ? channels.I : 1;
            if (channelCount <= 1) return null;
            return channelCount == 3 ? source.Rgb1ToGray() : source.AccessChannel(1);
        }
    }
}
```

- [ ] **Step 2: Register in Halcon `.csproj`**

In `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`, near the `TemplateMatching\` entries (line 64-65):

```xml
    <Compile Include="DxfComparison\HalconDxfContourComparer.cs" />
```

- [ ] **Step 3: Verify HALCON operator signatures against the reference, then build x64**

Grep `halcon_pdf/reference/halcon_operator_index.md` for each operator used (`read_contour_xld_dxf`, `create_scaled_shape_model_xld`, `find_scaled_shape_model`, `affine_trans_contour_xld`, `gen_region_contour_xld`, `dilation_circle`, `reduce_domain`, `edges_sub_pix`, `distance_contours_xld`, `get_contour_attrib_xld`, `hom_mat2d_*`, `select_obj`, `count_obj`), open its lines in `reference_hdevelop.txt`, and fix any parameter-order/name mismatch in the code above before building.

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: build 0/0. Fix compile errors (usually `HTuple` overloads or op-name casing) against the reference.

- [ ] **Step 4: Synthetic manual verification**

Create a tiny synthetic fixture (a HDevelop or one-off C# snippet is fine, or reuse the existing test-image tooling under `data/images`):
1. Author a simple R12 DXF (e.g. a 100×60 mm rectangle with a corner arc) — save as `data/dxf/test_rect.dxf` (create the `data/dxf` folder).
2. Render/obtain an image where that shape appears at a known scale (e.g. ~5 px/mm) and known rotation, optionally with a small local bump.
3. In a throwaway console call, run `new HalconDxfContourComparer().Compare(img, "data/dxf/test_rect.dxf", new DxfComparisonParameters { ScaleSeedPxPerMm = 5.0, TolerancePx = 3.0 })`.
4. Confirm: `Success=true`, `PoseScale ≈ 5`, `MaxDevPx` small for the clean case, and rises above `T` when you add the bump. Record the numbers.

- [ ] **Step 5: Commit**

```bash
git add src/FlashMeasurementSystem.Halcon/DxfComparison src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj
git commit -m "feat(dxf): add HALCON contour-comparison adapter (verified on synthetic DXF)"
```

---

## Task 4: WinForms standalone Form + launch button

Build a standalone Form (hand-written layout like `RecipeEditor`, **not** the Designer) and launch it from a button added **programmatically** in `MainWindow.OnLoad` — this keeps `MainWindow.Designer.cs` untouched (avoids the documented regeneration landmine).

**Files:**
- Create: `src/FlashMeasurementSystem.App.Wpf/DxfComparisonForm.cs`
- Modify: `src/FlashMeasurementSystem.App.Wpf/FlashMeasurementSystem.App.Wpf.csproj`
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

- [ ] **Step 1: Write `DxfComparisonForm.cs`**

```csharp
using System;
using System.Globalization;
using System.Windows.Forms;
using FlashMeasurementSystem.Application.DxfComparison;
using FlashMeasurementSystem.Domain.DxfComparison;
using HalconDotNet;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// DXF/CAD 輪廓度比對獨立面板：選 DXF、設公差/最低分/scale 種子、執行 → PASS/FAIL + 統計 + overlay。
    /// 用共用主視窗 HWindowControlHelper 畫 overlay（比照 RecipeEditor 接管共用影像）。
    /// </summary>
    public sealed class DxfComparisonForm : Form
    {
        private readonly HWindowControlHelper _imageHelper;
        private readonly IDxfContourComparer<HImage> _comparer;

        private TextBox _dxfPathBox;
        private NumericUpDown _toleranceNumeric;
        private NumericUpDown _minScoreNumeric;
        private NumericUpDown _scaleSeedNumeric;
        private Button _runButton;
        private Label _resultLabel;

        public DxfComparisonForm(HWindowControlHelper imageHelper, IDxfContourComparer<HImage> comparer)
        {
            _imageHelper = imageHelper ?? throw new ArgumentNullException(nameof(imageHelper));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));

            Text = "DXF/CAD 輪廓度比對";
            Width = 460; Height = 260;

            var browse = new Button { Text = "選 DXF…", Left = 12, Top = 12, Width = 90 };
            browse.Click += OnBrowse;
            _dxfPathBox = new TextBox { Left = 110, Top = 14, Width = 320, ReadOnly = true };

            _toleranceNumeric = LabeledNumeric("公差 T (px)", 12, 50, 2.0m, 0.01m, 0.1m, 100m, out Label t1);
            _minScoreNumeric = LabeledNumeric("最低分", 12, 84, 0.5m, 0.01m, 0.1m, 1.0m, out Label t2);
            _scaleSeedNumeric = LabeledNumeric("scale 種子 (px/mm，0=自動)", 12, 118, 0m, 0.1m, 0m, 1000m, out Label t3);

            _runButton = new Button { Text = "執行比對", Left = 12, Top = 156, Width = 120 };
            _runButton.Click += OnRun;

            _resultLabel = new Label { Left = 150, Top = 156, Width = 280, Height = 50, Text = "" };

            Controls.AddRange(new Control[] { browse, _dxfPathBox, t1, _toleranceNumeric,
                t2, _minScoreNumeric, t3, _scaleSeedNumeric, _runButton, _resultLabel });
        }

        private NumericUpDown LabeledNumeric(string caption, int left, int top, decimal val,
            decimal inc, decimal min, decimal max, out Label label)
        {
            label = new Label { Text = caption, Left = left, Top = top + 2, Width = 170 };
            return new NumericUpDown
            {
                Left = left + 180, Top = top, Width = 90,
                DecimalPlaces = 2, Increment = inc, Minimum = min, Maximum = max, Value = val
            };
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "DXF (*.dxf)|*.dxf", Title = "選擇 DXF 標稱輪廓" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _dxfPathBox.Text = dlg.FileName;
            }
        }

        private void OnRun(object sender, EventArgs e)
        {
            if (_imageHelper.CurrentImage == null)
            {
                MessageBox.Show(this, "請先在主視窗載入影像。", "DXF 比對"); return;
            }
            if (string.IsNullOrEmpty(_dxfPathBox.Text))
            {
                MessageBox.Show(this, "請先選 DXF 檔。", "DXF 比對"); return;
            }

            var pars = new DxfComparisonParameters
            {
                TolerancePx = (double)_toleranceNumeric.Value,
                MinScore = (double)_minScoreNumeric.Value,
                ScaleSeedPxPerMm = (double)_scaleSeedNumeric.Value
            };

            Cursor = Cursors.WaitCursor;
            try
            {
                DxfComparisonResult r = _comparer.Compare(_imageHelper.CurrentImage, _dxfPathBox.Text, pars);
                _resultLabel.Text = r.Message;
                _resultLabel.ForeColor = !r.Success ? System.Drawing.SystemColors.ControlText
                    : (r.IsPass ? System.Drawing.Color.DarkGreen : System.Drawing.Color.DarkRed);
                // overlay：僅在成功時畫對位標稱（v1 先畫標稱；逐點上色留後續增量）
                if (r.Success)
                    DrawNominalOverlay(pars);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "DXF 比對異常：" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        // v1 overlay：重跑一次比對僅為取得對位標稱來畫並不划算；先以文字結果為主，
        // overlay 的逐點上色留待「Halcon adapter 回傳對位後 XLD」的後續增量。此處保留掛勾。
        private void DrawNominalOverlay(DxfComparisonParameters pars)
        {
            // 後續增量：adapter 增加回傳 aligned nominal / 上色實際邊，再於此 SetPersistentOverlayAction。
        }
    }
}
```

- [ ] **Step 2: Register the Form in App.Wpf `.csproj`**

In `src/FlashMeasurementSystem.App.Wpf/FlashMeasurementSystem.App.Wpf.csproj`, near the `RecipeEditor.cs` entry (line 87), add a plain compile entry (no DependentUpon — it has no Designer file):

```xml
    <Compile Include="DxfComparisonForm.cs" />
```

- [ ] **Step 3: Add comparer field + launch button in `MainWindow.cs`**

Add the field next to the other adapters (after `private ToolTip _toolTip;`, ~line 82):

```csharp
        private readonly FlashMeasurementSystem.Halcon.DxfComparison.HalconDxfContourComparer _dxfComparer
            = new FlashMeasurementSystem.Halcon.DxfComparison.HalconDxfContourComparer();
```

At the end of `OnLoad` (after the existing setup, before the method's closing brace), add a programmatically-created launch button so `MainWindow.Designer.cs` is not touched:

```csharp
            var dxfButton = new System.Windows.Forms.Button
            {
                Text = "DXF 比對…",
                AutoSize = true,
                Left = 12, Top = 12
            };
            dxfButton.Click += (s, ev) =>
            {
                var form = new DxfComparisonForm(_imageHelper, _dxfComparer);
                form.Show(this);
            };
            Controls.Add(dxfButton);
            dxfButton.BringToFront();
```

> Note: if a more suitable toolbar/panel container exists on `MainWindow`, add `dxfButton` there instead of the form root — but do it in `MainWindow.cs`, never in `MainWindow.Designer.cs`.

- [ ] **Step 4: Build x64**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: build 0/0.

- [ ] **Step 5: Manual GUI verification**

Launch `src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`:
1. Load an image of the part that matches the synthetic DXF (Task 3 fixture).
2. Click **DXF 比對…** → the form opens.
3. Select the DXF, set T and `scale 種子 (px/mm)` to the known value, click **執行比對**.
4. Confirm the result label shows **PASS**/**FAIL** with max/mean, green/red coloring; a degraded/warped part flips it to FAIL.
5. Confirm no crash when: no image loaded, no DXF selected, a non-R12 DXF (should show a clear failure message, not crash).

- [ ] **Step 6: Commit**

```bash
git add src/FlashMeasurementSystem.App.Wpf/DxfComparisonForm.cs src/FlashMeasurementSystem.App.Wpf/FlashMeasurementSystem.App.Wpf.csproj src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
git commit -m "feat(dxf): add standalone DXF comparison form + launch button"
```

---

## Self-Review

**Spec coverage:**
- §2 decisions (profile PASS/FAIL, DXF scaled model, auto edges, standalone, unsigned/symmetric, pixel-space) → Tasks 1 (evaluator/unsigned/pass), 3 (model/find/edges/distance), 4 (standalone form). ✓
- §5 pipeline (read_dxf→model→find→align→band edges→distance→stats) → Task 3 Step 1. ✓
- §6.1 scale seed → Task 3 (`ScaleSeedPxPerMm` → ±30% range) + Task 4 UI field. ✓
- §6.2 edge band → Task 3 (`gen_region_contour_xld`+`dilation_circle`+`reduce_domain`). ✓
- §7 error handling (dxf fail/no match/no edges/HalconException→failed result, single-channel, disposal) → Task 3. ✓
- §8 testing (Domain full unit test; adapter synthetic manual) → Tasks 1 & 3. ✓
- §3 non-goals (no signed, no recipe integration, R12-only) → respected; R12 failure message in Task 3. ✓

**Placeholder scan:** `DrawNominalOverlay` in Task 4 is an intentional v1 no-op hook (documented as deferred to a later increment where the adapter returns the aligned XLD); the text result is the v1 deliverable. All other steps contain concrete code/commands. No TBD/TODO left as work-blocking.

**Type consistency:** `DxfComparisonParameters`/`DxfComparisonResult`/`DxfDeviationEvaluator.Evaluate`/`IDxfContourComparer<TImage>.Compare` names and signatures are identical across Tasks 1-4. `Failed(string)` / `Default()` factories used consistently. ✓

**Known implementation risk flagged for the executor:** the HALCON supporting operators (band generation, hom_mat2d chain, attribute extraction) must be signature-verified against the offline reference during Task 3 Step 3 — the pipeline structure is correct and the core operators are pre-verified (spec §9), but exact HTuple overloads/param order may need adjustment at build time.
