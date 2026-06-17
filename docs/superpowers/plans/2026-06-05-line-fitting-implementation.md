# Line Fitting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add layered Line Fitting from manual section 4.4 and expose it as a testable WinForms action using the current Edge Detection result.

**Architecture:** Add pure Line Fitting models in Domain, an Application interface, and a HALCON adapter that calls `FitLineContourXld`. The existing WinForms `MainWindow` gets a compact Fit Line action in the Edge Detection tab and draws the fitted line overlay.

**Tech Stack:** C# .NET Framework 4.8, WinForms, HALCON 17.12 `HalconDotNet`, old-style `.csproj`, console-style tests.

---

## File Map

- Create `src/FlashMeasurementSystem.Domain/LineFitting/LineFittingParameters.cs`: supported algorithm names and defaults.
- Create `src/FlashMeasurementSystem.Domain/LineFitting/LineFittingResult.cs`: pure result DTO for fitted line output.
- Modify `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`: include new Domain files.
- Create `src/FlashMeasurementSystem.Application/LineFitting/ILineFitter.cs`: Application contract using Domain types only.
- Modify `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`: include `ILineFitter.cs`.
- Create `src/FlashMeasurementSystem.Halcon/LineFitting/HalconLineFitter.cs`: HALCON adapter implementation.
- Modify `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`: include adapter file.
- Modify `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`: instantiate fitter, store latest edge result, run Fit Line handler.
- Modify `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`: add Fit Line button/result controls to Edge Detection UI.
- Modify `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs` only if no existing line drawing method exists.
- Create `tests/FlashMeasurementSystem.Tests/LineFittingDomainTests.cs`: console-style tests for defaults and contract types.
- Modify `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`: include new test file.

## Task 1: Add Domain Tests First

**Files:**
- Create: `tests/FlashMeasurementSystem.Tests/LineFittingDomainTests.cs`
- Modify: `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`

- [ ] **Step 1: Write failing domain tests**

Create `tests/FlashMeasurementSystem.Tests/LineFittingDomainTests.cs`:

```csharp
using System;
using FlashMeasurementSystem.Domain.LineFitting;

namespace FlashMeasurementSystem.Tests
{
    public static class LineFittingDomainTests
    {
        public static void Run()
        {
            LineFittingParameters parameters = LineFittingParameters.Default();

            AssertEqual("tukey", parameters.Algorithm, "Default algorithm");
            AssertEqual(-1, parameters.MaxNumPoints, "Default MaxNumPoints");
            AssertEqual(0, parameters.ClippingEndPoints, "Default ClippingEndPoints");
            AssertEqual(2.0, parameters.ClippingFactor, "Default ClippingFactor");
            AssertEqual(3, parameters.Iterations, "Default Iterations");
            AssertEqual(2, parameters.MinPoints, "Default MinPoints");

            if (!LineFittingParameters.IsSupportedAlgorithm("regression"))
                throw new InvalidOperationException("regression should be supported");
            if (!LineFittingParameters.IsSupportedAlgorithm("tukey"))
                throw new InvalidOperationException("tukey should be supported");
            if (LineFittingParameters.IsSupportedAlgorithm("ransac"))
                throw new InvalidOperationException("ransac should not be supported by FitLineContourXld");

            LineFittingResult result = new LineFittingResult();
            AssertEqual(false, result.Success, "Default Success");
            AssertEqual(string.Empty, result.ErrorMessage, "Default ErrorMessage");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
            }
        }
    }
}
```

- [ ] **Step 2: Wire test runner**

`EdgeDetectionDomainTests.cs` contains the entry point `public static int Main()` that
returns 0 on success and ends with `Console.WriteLine("EdgeDetectionDomainTests passed");`.
Insert the new test call **before** the existing `WriteLine`/`return 0`, and add a matching
success line so the run output reports both suites:

```csharp
LineFittingDomainTests.Run();
Console.WriteLine("LineFittingDomainTests passed");
```

If `LineFittingDomainTests.Run()` throws, the exception propagates out of `Main` and the
process exits non-zero — that is the failure signal we want.

- [ ] **Step 3: Include the test file in old-style csproj**

In `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`, add:

```xml
<Compile Include="LineFittingDomainTests.cs" />
```

- [ ] **Step 4: Run tests to verify failure**

Run from solution root:

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: **build (compile) failure** with CS0234 / CS0246 — the namespace
`FlashMeasurementSystem.Domain.LineFitting` and type `LineFittingParameters` do not exist yet.
This is the "red" state of TDD for a console-style test project; the test binary cannot be
produced until Task 2 adds the Domain classes. After Task 2 the same build command should
succeed.

## Task 2: Add Domain Models

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/LineFitting/LineFittingParameters.cs`
- Create: `src/FlashMeasurementSystem.Domain/LineFitting/LineFittingResult.cs`
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`

- [ ] **Step 1: Create `LineFittingParameters`**

```csharp
namespace FlashMeasurementSystem.Domain.LineFitting
{
    public class LineFittingParameters
    {
        public string Algorithm { get; set; }
        public int MaxNumPoints { get; set; }
        public int ClippingEndPoints { get; set; }
        public double ClippingFactor { get; set; }
        public int Iterations { get; set; }
        public int MinPoints { get; set; }

        public static LineFittingParameters Default()
        {
            return new LineFittingParameters
            {
                Algorithm = "tukey",
                MaxNumPoints = -1,
                ClippingEndPoints = 0,
                ClippingFactor = 2.0,
                Iterations = 3,
                MinPoints = 2
            };
        }

        public static bool IsSupportedAlgorithm(string algorithm)
        {
            return algorithm == "regression"
                || algorithm == "gauss"
                || algorithm == "huber"
                || algorithm == "tukey"
                || algorithm == "drop";
        }
    }
}
```

- [ ] **Step 2: Create `LineFittingResult`**

```csharp
namespace FlashMeasurementSystem.Domain.LineFitting
{
    public class LineFittingResult
    {
        public bool Success { get; set; }
        public double Row1 { get; set; }
        public double Column1 { get; set; }
        public double Row2 { get; set; }
        public double Column2 { get; set; }
        public double AngleDeg { get; set; }
        public double Length { get; set; }
        public double ResidualRms { get; set; }
        public int UsedPoints { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 3: Include Domain files in csproj**

In `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`, add near the other feature compile entries:

```xml
<Compile Include="LineFitting\LineFittingParameters.cs" />
<Compile Include="LineFitting\LineFittingResult.cs" />
```

- [ ] **Step 4: Run domain tests**

Run:

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: either PASS for Domain tests or FAIL only because the test entry-point wiring needs adjustment. Fix only the test runner call if needed.

## Task 3: Add Application Interface

**Files:**
- Create: `src/FlashMeasurementSystem.Application/LineFitting/ILineFitter.cs`
- Modify: `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`

- [ ] **Step 1: Create interface**

```csharp
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.LineFitting;

namespace FlashMeasurementSystem.Application.LineFitting
{
    public interface ILineFitter
    {
        LineFittingResult FitLine(IList<EdgePoint> edgePoints, LineFittingParameters parameters);
    }
}
```

- [ ] **Step 2: Include interface in csproj**

In `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`, add:

```xml
<Compile Include="LineFitting\ILineFitter.cs" />
```

- [ ] **Step 3: Build Application project**

Run:

```powershell
dotnet build .\src\FlashMeasurementSystem.Application\FlashMeasurementSystem.Application.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: PASS.

## Task 4: Add HALCON Line Fitter

**Files:**
- Create: `src/FlashMeasurementSystem.Halcon/LineFitting/HalconLineFitter.cs`
- Modify: `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`

- [ ] **Step 1: Create HALCON adapter**

```csharp
using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.LineFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.LineFitting;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.LineFitting
{
    public class HalconLineFitter : ILineFitter
    {
        public LineFittingResult FitLine(IList<EdgePoint> edgePoints, LineFittingParameters parameters)
        {
            LineFittingResult result = new LineFittingResult();
            LineFittingParameters effective = parameters ?? LineFittingParameters.Default();

            if (!LineFittingParameters.IsSupportedAlgorithm(effective.Algorithm))
            {
                result.ErrorMessage = "不支援的直線擬合演算法: " + effective.Algorithm;
                return result;
            }

            // HALCON 17.12 fit_line_contour_xld reference L175845 規定：
            // 「The minimum necessary number of contour points for fitting a line is two.
            //   Therefore, it is required that the number of contour points is at least
            //   2 + 2 * ClippingEndPoints.」
            // 動態算出 effective 最小值：使用者 MinPoints 跟 HALCON 內建最低限取大。
            int halconMinimum = 2 + 2 * Math.Max(0, effective.ClippingEndPoints);
            int requiredMinPoints = Math.Max(effective.MinPoints, halconMinimum);

            if (edgePoints == null || edgePoints.Count < requiredMinPoints)
            {
                int count = edgePoints == null ? 0 : edgePoints.Count;
                result.ErrorMessage = string.Format(
                    "邊緣點不足 (need >= {0}, got {1}; ClippingEndPoints={2})",
                    requiredMinPoints, count, effective.ClippingEndPoints);
                return result;
            }

            try
            {
                int n = edgePoints.Count;
                double[] rows = new double[n];
                double[] columns = new double[n];

                for (int i = 0; i < n; i++)
                {
                    rows[i] = edgePoints[i].Row;
                    columns[i] = edgePoints[i].Column;
                }

                // HXLDCont 持有 HALCON handle；明確 dispose 避免 leak 等到 GC。
                using (HXLDCont contour = new HXLDCont())
                {
                    contour.GenContourPolygonXld(rows, columns);

                    // 參數順序對齊 reference L175794-797：
                    //   Algorithm, MaxNumPoints, ClippingEndPoints, Iterations, ClippingFactor
                    // Iterations 在 ClippingFactor **之前**！順序顛倒會讓 ClippingFactor 跟
                    // Iterations 互換，跑不出預期 tukey 行為。
                    HOperatorSet.FitLineContourXld(
                        contour,
                        effective.Algorithm,
                        effective.MaxNumPoints,
                        effective.ClippingEndPoints,
                        effective.Iterations,
                        effective.ClippingFactor,
                        out HTuple rowBegin,
                        out HTuple colBegin,
                        out HTuple rowEnd,
                        out HTuple colEnd,
                        out HTuple nr,
                        out HTuple nc,
                        out HTuple distance);

                    result.Row1 = rowBegin.D;
                    result.Column1 = colBegin.D;
                    result.Row2 = rowEnd.D;
                    result.Column2 = colEnd.D;
                    result.UsedPoints = n;

                    double deltaRow = result.Row2 - result.Row1;
                    double deltaCol = result.Column2 - result.Column1;
                    result.AngleDeg = Math.Atan2(deltaRow, deltaCol) * 180.0 / Math.PI;
                    result.Length = Math.Sqrt(deltaRow * deltaRow + deltaCol * deltaCol);
                    result.ResidualRms = CalculateResidualRms(edgePoints, result);
                    result.Success = true;
                }
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "直線擬合失敗: " + ex.Message;
            }

            return result;
        }

        // 注意：這個 RMS 是對**所有輸入 edge points**算的，包括 fit_line_contour_xld 內部
        // 排除掉的 outlier（tukey/drop/huber 的核心特色）。所以這個 RMS 可能比 HALCON 內部
        // fit 看到的 inlier RMS 大。對 quality reporting 是合理指標（量「fit 對全部 input
        // 的擬合度」），但不是「HALCON fit 演算法本身的收斂誤差」。
        private static double CalculateResidualRms(IList<EdgePoint> edgePoints, LineFittingResult line)
        {
            double deltaRow = line.Row2 - line.Row1;
            double deltaCol = line.Column2 - line.Column1;
            double denominator = Math.Sqrt(deltaRow * deltaRow + deltaCol * deltaCol);

            if (denominator <= 0.0)
            {
                return 0.0;
            }

            double sumSq = 0.0;

            for (int i = 0; i < edgePoints.Count; i++)
            {
                double pointRow = edgePoints[i].Row;
                double pointCol = edgePoints[i].Column;
                double distance = Math.Abs(deltaRow * (pointCol - line.Column1) - deltaCol * (pointRow - line.Row1)) / denominator;
                sumSq += distance * distance;
            }

            return Math.Sqrt(sumSq / edgePoints.Count);
        }
    }
}
```

- [ ] **Step 2: Include HALCON adapter in csproj**

In `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`, add:

```xml
<Compile Include="LineFitting\HalconLineFitter.cs" />
```

- [ ] **Step 3: Build HALCON project**

Run:

```powershell
dotnet build .\src\FlashMeasurementSystem.Halcon\FlashMeasurementSystem.Halcon.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: PASS if HALCON 17.12 is installed and referenced correctly. If it fails on HALCON path/license/runtime, record the exact blocker.

## Task 5: Add GUI Wiring

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`
- Modify: `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs` only if needed

- [ ] **Step 1: Add fields and using statements in `MainWindow.cs`**

Add imports:

```csharp
using FlashMeasurementSystem.Domain.LineFitting;
using FlashMeasurementSystem.Halcon.LineFitting;
```

Add fields near the other adapter fields. `_latestEdgeRoi` is needed so that Fit Line can
redraw the ROI rectangle alongside the fitted line (see Step 5):

```csharp
private readonly HalconLineFitter _lineFitter = new HalconLineFitter();
private EdgeDetectionRoi _latestEdgeRoi;
private EdgeResult _latestEdgeResult;
private LineFittingResult _latestLineFittingResult;
```

Also add to the imports section:

```csharp
using FlashMeasurementSystem.Domain.EdgeDetection;
```

- [ ] **Step 2: Store latest edge result + ROI after detection**

In the existing `RunEdgeDetectionButton_Click` handler, after receiving the `EdgeResult` (local
variable is named `result`, not `edgeResult`), add:

```csharp
_latestEdgeRoi = roi;
_latestEdgeResult = result;
_latestLineFittingResult = null;
UpdateLineFittingResult(null);
```

Also reset these in `ClearEdgeDetectionButton_Click` and `LoadAndDisplayImage` to avoid stale
state — otherwise loading a new image or clearing the ROI would leave Fit Line drawing on the
wrong image:

```csharp
_latestEdgeRoi = null;
_latestEdgeResult = null;
_latestLineFittingResult = null;
UpdateLineFittingResult(null);
```

Note that this requires an additional field declared in Step 1:

```csharp
private EdgeDetectionRoi _latestEdgeRoi;
```

- [ ] **Step 3: Add Fit Line handler**

Add this method to `MainWindow.cs`. The catch follows the same pattern as the existing
`RunEdgeDetectionButton_Click` (which catches both `HalconException` and `Exception` as of
2026-06-05 fix A1):

```csharp
private void FitLineButton_Click(object sender, EventArgs e)
{
    if (_latestEdgeResult == null || _latestEdgeResult.EdgePoints == null || _latestEdgeResult.EdgePoints.Count == 0)
    {
        UpdateLineFittingResult(new LineFittingResult { ErrorMessage = "請先執行邊緣檢測" });
        return;
    }

    try
    {
        LineFittingResult result = _lineFitter.FitLine(_latestEdgeResult.EdgePoints, LineFittingParameters.Default());
        _latestLineFittingResult = result;
        UpdateLineFittingResult(result);

        if (result.Success)
        {
            DrawLineFittingOverlay(_latestEdgeRoi, _latestEdgeResult, result);
        }
    }
    catch (HalconException ex)
    {
        UpdateLineFittingResult(new LineFittingResult
        {
            ErrorMessage = "直線擬合失敗 [Halcon " + ex.GetErrorCode() + "]: " + ex.Message
        });
    }
    catch (Exception ex)
    {
        UpdateLineFittingResult(new LineFittingResult
        {
            ErrorMessage = "直線擬合失敗 (unexpected " + ex.GetType().Name + "): " + ex.Message
        });
    }
}
```

- [ ] **Step 4: Add result update helper**

Add this method to `MainWindow.cs` and align control names with the designer controls created in Step 6:

```csharp
private void UpdateLineFittingResult(LineFittingResult result)
{
    if (result == null)
    {
        lineFittingResultLabel.Text = "直線擬合: 尚未執行";
        return;
    }

    if (!result.Success)
    {
        lineFittingResultLabel.Text = "直線擬合失敗: " + result.ErrorMessage;
        return;
    }

    lineFittingResultLabel.Text = string.Format(
        "直線擬合成功 | P1=({0:F2}, {1:F2}) P2=({2:F2}, {3:F2}) Angle={4:F2}° Length={5:F2}px RMS={6:F4}px Points={7}",
        result.Row1,
        result.Column1,
        result.Row2,
        result.Column2,
        result.AngleDeg,
        result.Length,
        result.ResidualRms,
        result.UsedPoints);
}
```

- [ ] **Step 5: Add overlay drawing helper**

`HWindowControlHelper` (field `_imageHelper`) exposes only `SetPersistentOverlayAction(Action)`
which **replaces** the previous overlay action — calling it from Fit Line will erase the ROI
rectangle and edge crosses drawn by `DrawEdgeOverlay`. To preserve all three layers (ROI +
edges + fitted line), the Fit Line handler must repaint everything in one persistent action.

The `MaxOverlayCrosses` constant and `crossSize` logic are the same as `DrawEdgeOverlay`
(2026-06-05 fix A3) — keep them in sync (consider extracting a shared helper later if both
methods diverge).

Add this method to `MainWindow.cs`:

```csharp
private void DrawLineFittingOverlay(EdgeDetectionRoi roi, EdgeResult edges, LineFittingResult line)
{
    if (line == null || !line.Success) return;

    int totalEdges = edges?.EdgePoints?.Count ?? 0;
    int step = totalEdges <= MaxOverlayCrosses ? 1 : (int)Math.Ceiling((double)totalEdges / MaxOverlayCrosses);
    int crossSize = _edgeSubPixRadio.Checked ? 3 : 8;

    _imageHelper.SetPersistentOverlayAction(() =>
    {
        // Layer 1: ROI rectangle (blue) — same as DrawEdgeOverlay
        if (roi != null)
        {
            _imageHelper.Annotator.DrawRectangle2(roi.CenterRow, roi.CenterCol,
                roi.AngleRad, roi.Length1, roi.Length2, "blue");
        }

        // Layer 2: edge crosses (cyan, sampled) — same as DrawEdgeOverlay
        if (edges != null && edges.EdgePoints != null)
        {
            for (int i = 0; i < totalEdges; i += step)
            {
                EdgePoint p = edges.EdgePoints[i];
                _imageHelper.Annotator.DrawCross(p.Row, p.Column, crossSize, "cyan");
            }
        }

        // Layer 3: fitted line (green)
        _imageHelper.Annotator.DrawLine(line.Row1, line.Column1, line.Row2, line.Column2, "green");
    });
}
```

This requires `OverlayAnnotator.DrawLine` — added in Step 8 below.

- [ ] **Step 6: Add designer controls**

In `MainWindow.Designer.cs`, add controls in the existing Edge Detection tab area:

```csharp
private System.Windows.Forms.Button fitLineButton;
private System.Windows.Forms.Label lineFittingResultLabel;
```

Initialize them in the Edge Detection UI setup:

```csharp
this.fitLineButton = new System.Windows.Forms.Button();
this.lineFittingResultLabel = new System.Windows.Forms.Label();

this.fitLineButton.Text = "Fit Line";
this.fitLineButton.Width = 120;
this.fitLineButton.Click += new System.EventHandler(this.FitLineButton_Click);

this.lineFittingResultLabel.AutoSize = false;
this.lineFittingResultLabel.Height = 48;
this.lineFittingResultLabel.Text = "直線擬合: 尚未執行";
```

Add both controls to the existing Edge Detection layout near the edge result controls. Preserve existing control order and avoid resizing unrelated sections.

- [ ] **Step 7: Add `OverlayAnnotator.DrawLine` helper**

`OverlayAnnotator` already has `DrawRectangle2`, `DrawCross`, `DrawRoiRectangle`, `DrawText`
but no `DrawLine`. Add this method to `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`
so Step 5 doesn't have to drop down to raw `HOperatorSet.DispLine` (consistency with the
existing draw helpers):

```csharp
public void DrawLine(double row1, double col1, double row2, double col2, string color = null)
{
    HOperatorSet.SetColor(_window, color ?? "yellow");
    HOperatorSet.SetLineWidth(_window, 2);
    HOperatorSet.DispLine(_window, row1, col1, row2, col2);
}
```

- [ ] **Step 8: Build App project**

Run:

```powershell
dotnet build .\src\FlashMeasurementSystem.App.Wpf\FlashMeasurementSystem.App.Wpf.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: PASS.

If the App.Wpf process is currently running it will hold a lock on the dll files and the
build will report MSB3026/MSB3027 errors. Close the running app before rebuilding.

## Task 6: Full Verification

**Files:**
- No new files unless verification exposes defects from this plan.

- [ ] **Step 1: Run solution Any CPU build**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: PASS or a clearly documented pre-existing/HALCON environment blocker.

- [ ] **Step 2: Run solution x64 build**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: PASS for HALCON-sensitive changes.

- [ ] **Step 3: Manual GUI verification**

Run the app from Visual Studio or the built executable, then verify:

1. Select an image from `data/images`.
2. Draw an ROI in the Edge Detection tab.
3. Click Detect and confirm edge points appear.
4. Click Fit Line.
5. Confirm a green fitted line appears on the HALCON window **and** the blue ROI rectangle and cyan edge crosses remain visible (Step 5 must redraw all three layers).
6. Confirm the result label shows angle, length, RMS, and point count.
7. Click Fit Line before Detect and confirm it shows `請先執行邊緣檢測`.
8. Change edge parameters, rerun Detect, then rerun Fit Line and confirm result updates.
9. **Stale state**: load image A → Detect → load image B → click Fit Line. Expected: `請先執行邊緣檢測` (cached edge result was invalidated by the load). Without the invalidation hook from Step 2, Fit Line would draw a misplaced line from image A on image B.
10. **Clear ROI**: Detect → click Clear Edge Detection → click Fit Line. Expected: `請先執行邊緣檢測`.
11. **Subpix scale**: switch the Edge Detection algorithm radio to `EdgesSubPix`, run Detect on a sharp-edged image (the subpix path may return hundreds to thousands of contour points), then Fit Line. Expected: HALCON returns within a reasonable time (~seconds at worst) and the fitted line passes through the densest edge cluster. Watch for high RMS — this is normal for subpix output spanning multiple physical edges (see Spec §Known Limitations).
12. **Insufficient points**: set Edge Detection parameters so it returns 0 or 1 edge (e.g. very high threshold), Detect, then Fit Line. Expected: `邊緣點不足 (need >= N, got M; ClippingEndPoints=K)` message; no exception.

## Self-Review

- Spec coverage: Domain models, Application interface, HALCON adapter, GUI action, overlay, tests, Any CPU/x64 builds, and manual verification are covered.
- Placeholder scan: no `TBD`, `TODO`, `implement later`, or unspecified test instructions remain.
- Type consistency: plan consistently uses `LineFittingParameters`, `LineFittingResult`, `ILineFitter`, `HalconLineFitter`, `FitLine`, `EdgePoint`, `EdgeResult`, and `lineFittingResultLabel`.

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-05-line-fitting-implementation.md`.

Two execution options:

1. **Subagent-Driven (recommended)** - dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** - execute tasks in this session using executing-plans, batch execution with checkpoints.

Use Subagent-Driven unless the user explicitly asks for Inline Execution.
