# Circle Fitting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add layered Circle Fitting from manual section 4.5 and expose it as a testable WinForms action using the current Edge Detection result.

**Architecture:** Add pure Circle Fitting models in Domain, an Application interface, and a HALCON adapter that calls official HALCON 17.12 `FitCircleContourXld`. The existing transitional WinForms `MainWindow` gets a compact Fit Circle action in the Edge Detection tab and draws the fitted circle overlay.

**Tech Stack:** C# .NET Framework 4.8, WinForms, HALCON 17.12 `HalconDotNet`, old-style `.csproj`, console-style tests.

---

## Implementation Rules

- Do not commit during execution unless the user explicitly asks for commits. Suggested commit messages below are checkpoint labels only.
- Keep dependency direction unchanged: `Domain <- Application <- Halcon <- App.Wpf`.
- Do not put Halcon types in Domain or Application.
- Do not add `GenMeasureArc`, `MeasurePos`, `MeasurePairs`, 2D Metrology, custom RANSAC, recipe integration, or MeasurementWorkflow integration.
- Keep `src/FlashMeasurementSystem.App.Wpf` as WinForms. Do not migrate to WPF/XAML.

## File Map

- Create `src/FlashMeasurementSystem.Domain/CircleFitting/CircleFittingParameters.cs`: supported HALCON circle algorithms and defaults.
- Create `src/FlashMeasurementSystem.Domain/CircleFitting/CircleFittingResult.cs`: pure result DTO for fitted circle output.
- Modify `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`: include Circle Fitting Domain files.
- Create `src/FlashMeasurementSystem.Application/CircleFitting/ICircleFitter.cs`: Application contract using `EdgePoint` and Circle Fitting Domain types.
- Modify `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`: include `ICircleFitter.cs`.
- Create `src/FlashMeasurementSystem.Halcon/CircleFitting/HalconCircleFitter.cs`: HALCON adapter implementation.
- Modify `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`: include adapter file.
- Create `tests/FlashMeasurementSystem.Tests/CircleFittingDomainTests.cs`: console-style tests for defaults and contract types.
- Modify `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`: include test file.
- Modify `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`: call `CircleFittingDomainTests.Run()`.
- Modify `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`: add `DrawCircle` helper.
- Modify `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`: instantiate fitter, store result, run Fit Circle handler, redraw circle overlay, clear state.
- Modify `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`: add Fit Circle button and result label in Edge Detection tab.

## Task 1: Add Domain Tests First

**Files:**
- Create: `tests/FlashMeasurementSystem.Tests/CircleFittingDomainTests.cs`
- Modify: `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`

- [ ] **Step 1: Create failing Circle Fitting domain tests**

Create `tests/FlashMeasurementSystem.Tests/CircleFittingDomainTests.cs`:

```csharp
using System;
using FlashMeasurementSystem.Domain.CircleFitting;

namespace FlashMeasurementSystem.Tests
{
    public static class CircleFittingDomainTests
    {
        public static void Run()
        {
            CircleFittingParameters parameters = CircleFittingParameters.Default();

            AssertEqual("geotukey", parameters.Algorithm, "Default algorithm");
            AssertEqual(-1, parameters.MaxNumPoints, "Default MaxNumPoints");
            AssertEqual(0.0, parameters.MaxClosureDist, "Default MaxClosureDist");
            AssertEqual(0, parameters.ClippingEndPoints, "Default ClippingEndPoints");
            AssertEqual(3, parameters.Iterations, "Default Iterations");
            AssertEqual(2.0, parameters.ClippingFactor, "Default ClippingFactor");
            AssertEqual(3, parameters.MinPoints, "Default MinPoints");

            if (!CircleFittingParameters.IsSupportedAlgorithm("algebraic"))
                throw new InvalidOperationException("algebraic should be supported");
            if (!CircleFittingParameters.IsSupportedAlgorithm("geotukey"))
                throw new InvalidOperationException("geotukey should be supported");
            if (CircleFittingParameters.IsSupportedAlgorithm("ransac"))
                throw new InvalidOperationException("ransac should not be supported by FitCircleContourXld");

            CircleFittingResult result = new CircleFittingResult();
            AssertEqual(false, result.Success, "Default Success");
            AssertEqual(0.0, result.CenterRow, "Default CenterRow");
            AssertEqual(0.0, result.CenterColumn, "Default CenterColumn");
            AssertEqual(0.0, result.RadiusPx, "Default RadiusPx");
            AssertEqual(0.0, result.DiameterPx, "Default DiameterPx");
            AssertEqual(0.0, result.StartPhi, "Default StartPhi");
            AssertEqual(0.0, result.EndPhi, "Default EndPhi");
            AssertEqual(string.Empty, result.PointOrder, "Default PointOrder");
            AssertEqual(0.0, result.ResidualRms, "Default ResidualRms");
            AssertEqual(0.0, result.Roundness, "Default Roundness");
            AssertEqual(0, result.UsedPoints, "Default UsedPoints");
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

- [ ] **Step 2: Include test file in old-style csproj**

In `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`, add `CircleFittingDomainTests.cs` inside the compile item group:

```xml
<ItemGroup>
  <Compile Include="EdgeDetectionDomainTests.cs" />
  <Compile Include="LineFittingDomainTests.cs" />
  <Compile Include="CircleFittingDomainTests.cs" />
  <Compile Include="Properties\AssemblyInfo.cs" />
</ItemGroup>
```

- [ ] **Step 3: Wire test runner**

In `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`, replace the final test-run block with:

```csharp
LineFittingDomainTests.Run();
Console.WriteLine("LineFittingDomainTests passed");
CircleFittingDomainTests.Run();
Console.WriteLine("CircleFittingDomainTests passed");
Console.WriteLine("EdgeDetectionDomainTests passed");
return 0;
```

- [ ] **Step 4: Run tests to verify red state**

Run:

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build fails because `FlashMeasurementSystem.Domain.CircleFitting`, `CircleFittingParameters`, and `CircleFittingResult` do not exist yet.

- [ ] **Step 5: Checkpoint**

Suggested commit message if the user later asks for commits: `test: add circle fitting domain tests`.

## Task 2: Add Domain Models

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/CircleFitting/CircleFittingParameters.cs`
- Create: `src/FlashMeasurementSystem.Domain/CircleFitting/CircleFittingResult.cs`
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`

- [ ] **Step 1: Create `CircleFittingParameters`**

Create `src/FlashMeasurementSystem.Domain/CircleFitting/CircleFittingParameters.cs`:

```csharp
namespace FlashMeasurementSystem.Domain.CircleFitting
{
    public class CircleFittingParameters
    {
        public string Algorithm { get; set; }
        public int MaxNumPoints { get; set; }
        public double MaxClosureDist { get; set; }
        public int ClippingEndPoints { get; set; }
        public int Iterations { get; set; }
        public double ClippingFactor { get; set; }
        public int MinPoints { get; set; }

        public static CircleFittingParameters Default()
        {
            return new CircleFittingParameters
            {
                Algorithm = "geotukey",
                MaxNumPoints = -1,
                MaxClosureDist = 0.0,
                ClippingEndPoints = 0,
                Iterations = 3,
                ClippingFactor = 2.0,
                MinPoints = 3
            };
        }

        public static bool IsSupportedAlgorithm(string algorithm)
        {
            return algorithm == "algebraic"
                || algorithm == "ahuber"
                || algorithm == "atukey"
                || algorithm == "geometric"
                || algorithm == "geohuber"
                || algorithm == "geotukey";
        }
    }
}
```

- [ ] **Step 2: Create `CircleFittingResult`**

Create `src/FlashMeasurementSystem.Domain/CircleFitting/CircleFittingResult.cs`:

```csharp
namespace FlashMeasurementSystem.Domain.CircleFitting
{
    public class CircleFittingResult
    {
        public bool Success { get; set; }
        public double CenterRow { get; set; }
        public double CenterColumn { get; set; }
        public double RadiusPx { get; set; }
        public double DiameterPx { get; set; }
        public double StartPhi { get; set; }
        public double EndPhi { get; set; }
        public string PointOrder { get; set; } = string.Empty;
        public double ResidualRms { get; set; }
        public double Roundness { get; set; }
        public int UsedPoints { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 3: Include Domain files in csproj**

In `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`, add:

```xml
<Compile Include="CircleFitting\CircleFittingParameters.cs" />
<Compile Include="CircleFitting\CircleFittingResult.cs" />
```

- [ ] **Step 4: Run domain tests**

Run:

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build passes if only Tasks 1 and 2 are complete.

- [ ] **Step 5: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add circle fitting domain models`.
## Task 3: Add Application Interface

**Files:**
- Create: `src/FlashMeasurementSystem.Application/CircleFitting/ICircleFitter.cs`
- Modify: `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/CircleFittingDomainTests.cs`

- [ ] **Step 1: Extend tests with interface contract compile check**

Modify `tests/FlashMeasurementSystem.Tests/CircleFittingDomainTests.cs` to add these `using` statements:

```csharp
using System.Collections.Generic;
using FlashMeasurementSystem.Application.CircleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;
```

Add this code at the end of `Run()`:

```csharp
ICircleFitter fitter = new FakeCircleFitter();
CircleFittingResult fakeResult = fitter.FitCircle(new List<EdgePoint>(), parameters);
AssertEqual(true, fakeResult.Success, "Fake circle fitter should satisfy interface contract");
```

Add this nested fake class inside `CircleFittingDomainTests` after `AssertEqual<T>()`:

```csharp
private sealed class FakeCircleFitter : ICircleFitter
{
    public CircleFittingResult FitCircle(IList<EdgePoint> edgePoints, CircleFittingParameters parameters)
    {
        return new CircleFittingResult { Success = true };
    }
}
```

- [ ] **Step 2: Run tests to verify red state**

Run:

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build fails because `FlashMeasurementSystem.Application.CircleFitting` and `ICircleFitter` do not exist yet.

- [ ] **Step 3: Create interface**

Create `src/FlashMeasurementSystem.Application/CircleFitting/ICircleFitter.cs`:

```csharp
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Application.CircleFitting
{
    public interface ICircleFitter
    {
        CircleFittingResult FitCircle(IList<EdgePoint> edgePoints, CircleFittingParameters parameters);
    }
}
```

- [ ] **Step 4: Include interface in csproj**

In `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`, add:

```xml
<Compile Include="CircleFitting\ICircleFitter.cs" />
```

- [ ] **Step 5: Build Application and test projects**

Run:

```powershell
dotnet build .\src\FlashMeasurementSystem.Application\FlashMeasurementSystem.Application.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: both builds pass.

- [ ] **Step 6: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add circle fitting application contract`.

## Task 4: Add HALCON Circle Fitter Adapter

**Files:**
- Create: `src/FlashMeasurementSystem.Halcon/CircleFitting/HalconCircleFitter.cs`
- Modify: `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`

- [ ] **Step 1: Create `HalconCircleFitter`**

Create `src/FlashMeasurementSystem.Halcon/CircleFitting/HalconCircleFitter.cs`:

```csharp
using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.CircleFitting;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.CircleFitting
{
    public class HalconCircleFitter : ICircleFitter
    {
        public CircleFittingResult FitCircle(IList<EdgePoint> edgePoints, CircleFittingParameters parameters)
        {
            CircleFittingResult result = new CircleFittingResult();
            CircleFittingParameters effective = parameters ?? CircleFittingParameters.Default();

            if (!CircleFittingParameters.IsSupportedAlgorithm(effective.Algorithm))
            {
                result.ErrorMessage = "不支援的圓擬合演算法: " + effective.Algorithm;
                return result;
            }

            // HALCON 17.12 fit_circle_contour_xld reference L175525-526 規定：
            // 「The minimum necessary number of contour points for fitting a circle is three.
            //   Therefore, it is required that the number of contour points is at least
            //   3 + 2 * ClippingEndPoints.」
            // 動態算出 effective 最小值：使用者 MinPoints 跟 HALCON 內建最低限取大。
            // 預設 ClippingEndPoints=0 時等於 3，但若 ClippingEndPoints>0 卻只檢查 3，
            // 會傳不足點數讓 HALCON 丟例外。
            int halconMinimum = 3 + 2 * Math.Max(0, effective.ClippingEndPoints);
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

                using (HXLDCont contour = new HXLDCont())
                {
                    contour.GenContourPolygonXld(rows, columns);

                    HOperatorSet.FitCircleContourXld(
                        contour,
                        effective.Algorithm,
                        effective.MaxNumPoints,
                        effective.MaxClosureDist,
                        effective.ClippingEndPoints,
                        effective.Iterations,
                        effective.ClippingFactor,
                        out HTuple row,
                        out HTuple column,
                        out HTuple radius,
                        out HTuple startPhi,
                        out HTuple endPhi,
                        out HTuple pointOrder);

                    result.CenterRow = row.D;
                    result.CenterColumn = column.D;
                    result.RadiusPx = radius.D;
                    result.DiameterPx = result.RadiusPx * 2.0;
                    result.StartPhi = startPhi.D;
                    result.EndPhi = endPhi.D;
                    result.PointOrder = pointOrder.S;
                    result.UsedPoints = n;

                    CalculateResiduals(edgePoints, result);
                    result.Success = true;
                }
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "圓擬合失敗: " + ex.Message;
            }

            return result;
        }

        // 注意：ResidualRms 跟 Roundness 都是對**所有輸入 edge points**算的，包括
        // fit_circle_contour_xld 內部排除掉的 outlier（geotukey/atukey 的核心特色）。
        // 所以這兩個值反映「fit 對全部 input 的擬合度」，不是「HALCON 演算法內部 inlier 誤差」。
        // 對 quality reporting 是合理指標，但 Roundness（max-min 半徑）對單一離群點特別敏感
        // ——一個雜訊點就會撐大 Roundness。判讀時搭配 RMS 一起看。
        private static void CalculateResiduals(IList<EdgePoint> edgePoints, CircleFittingResult circle)
        {
            double sumSq = 0.0;
            double minPointRadius = double.MaxValue;
            double maxPointRadius = double.MinValue;

            for (int i = 0; i < edgePoints.Count; i++)
            {
                double deltaRow = edgePoints[i].Row - circle.CenterRow;
                double deltaColumn = edgePoints[i].Column - circle.CenterColumn;
                double pointRadius = Math.Sqrt(deltaRow * deltaRow + deltaColumn * deltaColumn);
                double radialError = Math.Abs(pointRadius - circle.RadiusPx);

                sumSq += radialError * radialError;
                if (pointRadius < minPointRadius) minPointRadius = pointRadius;
                if (pointRadius > maxPointRadius) maxPointRadius = pointRadius;
            }

            circle.ResidualRms = Math.Sqrt(sumSq / edgePoints.Count);
            circle.Roundness = maxPointRadius - minPointRadius;
        }
    }
}
```

- [ ] **Step 2: Include adapter in csproj**

In `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`, add:

```xml
<Compile Include="CircleFitting\HalconCircleFitter.cs" />
```

- [ ] **Step 3: Build Halcon project in Any CPU and x64**

Run:

```powershell
dotnet build .\src\FlashMeasurementSystem.Halcon\FlashMeasurementSystem.Halcon.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\src\FlashMeasurementSystem.Halcon\FlashMeasurementSystem.Halcon.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: both builds pass. If HALCON reference resolution fails, report the exact path or SDK blocker before changing code.

- [ ] **Step 4: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add halcon circle fitter`.
## Task 5: Add Circle Overlay Helper

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`

- [ ] **Step 1: Add `DrawCircle` helper**

In `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`, add this method after `DrawLine`:

```csharp
public void DrawCircle(double row, double col, double radius, string color = null)
{
    HOperatorSet.SetColor(_window, color ?? "yellow");
    HOperatorSet.SetLineWidth(_window, 2);
    HOperatorSet.SetDraw(_window, "margin");
    HOperatorSet.DispCircle(_window, row, col, radius);
}
```

- [ ] **Step 2: Build App project**

Run:

```powershell
dotnet build .\src\FlashMeasurementSystem.App.Wpf\FlashMeasurementSystem.App.Wpf.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build passes.

- [ ] **Step 3: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add circle overlay helper`.

## Task 6: Wire Circle Fitting Into MainWindow Code

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

- [ ] **Step 1: Add using statements**

At the top of `MainWindow.cs`, add Circle Fitting namespaces beside Line Fitting namespaces:

```csharp
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Halcon.CircleFitting;
```

- [ ] **Step 2: Add fitter and result fields**

In `MainWindow`, add `_circleFitter` beside `_lineFitter`, and `_latestCircleFittingResult` beside `_latestLineFittingResult`:

```csharp
private readonly HalconEdgeDetector _edgeDetector = new HalconEdgeDetector();
private readonly HalconLineFitter _lineFitter = new HalconLineFitter();
private readonly HalconCircleFitter _circleFitter = new HalconCircleFitter();
private EdgeDetectionRoi _latestEdgeRoi;
private EdgeResult _latestEdgeResult;
private LineFittingResult _latestLineFittingResult;
private CircleFittingResult _latestCircleFittingResult;
```

- [ ] **Step 3: Add `FitCircleButton_Click`**

Add this method after `FitLineButton_Click`:

```csharp
private void FitCircleButton_Click(object sender, EventArgs e)
{
    if (_latestEdgeResult == null || _latestEdgeResult.EdgePoints == null)
    {
        UpdateCircleFittingResult(new CircleFittingResult { ErrorMessage = "請先執行邊緣檢測" });
        return;
    }

    try
    {
        CircleFittingResult result = _circleFitter.FitCircle(_latestEdgeResult.EdgePoints, CircleFittingParameters.Default());
        _latestCircleFittingResult = result;
        UpdateCircleFittingResult(result);

        if (result.Success)
        {
            DrawCircleFittingOverlay(_latestEdgeRoi, _latestEdgeResult, result);
        }
    }
    catch (HalconException ex)
    {
        UpdateCircleFittingResult(new CircleFittingResult
        {
            ErrorMessage = "圓擬合失敗 [Halcon " + ex.GetErrorCode() + "]: " + ex.Message
        });
    }
    catch (Exception ex)
    {
        UpdateCircleFittingResult(new CircleFittingResult
        {
            ErrorMessage = "圓擬合失敗 (unexpected " + ex.GetType().Name + "): " + ex.Message
        });
    }
}
```

- [ ] **Step 4: Add `DrawCircleFittingOverlay`**

Add this method after `DrawLineFittingOverlay`:

```csharp
private void DrawCircleFittingOverlay(EdgeDetectionRoi roi, EdgeResult edges, CircleFittingResult circle)
{
    if (circle == null || !circle.Success) return;

    int totalEdges = edges?.EdgePoints?.Count ?? 0;
    int step = totalEdges <= MaxOverlayCrosses ? 1 : (int)Math.Ceiling((double)totalEdges / MaxOverlayCrosses);
    int crossSize = _edgeSubPixRadio.Checked ? 3 : 8;

    _imageHelper.SetPersistentOverlayAction(() =>
    {
        if (roi != null)
        {
            _imageHelper.Annotator.DrawRectangle2(roi.CenterRow, roi.CenterCol, roi.AngleRad, roi.Length1, roi.Length2, "blue");
        }

        if (edges != null && edges.EdgePoints != null)
        {
            for (int i = 0; i < totalEdges; i += step)
            {
                EdgePoint edge = edges.EdgePoints[i];
                _imageHelper.Annotator.DrawCross(edge.Row, edge.Column, crossSize, "cyan");
            }
        }

        _imageHelper.Annotator.DrawCircle(circle.CenterRow, circle.CenterColumn, circle.RadiusPx, "green");
    });
}
```

- [ ] **Step 5: Add `UpdateCircleFittingResult`**

Add this method after `UpdateLineFittingResult`:

```csharp
private void UpdateCircleFittingResult(CircleFittingResult result)
{
    if (result == null)
    {
        circleFittingResultLabel.Text = "圓擬合: 尚未執行";
        circleFittingResultLabel.ForeColor = Color.Black;
        return;
    }

    if (!result.Success)
    {
        circleFittingResultLabel.Text = "圓擬合失敗: " + result.ErrorMessage;
        circleFittingResultLabel.ForeColor = Color.Red;
        return;
    }

    circleFittingResultLabel.Text = string.Format(
        CultureInfo.InvariantCulture,
        "Circle OK | C=({0:F2},{1:F2}) R={2:F2}px D={3:F2}px\nRMS={4:F4}px Round={5:F4}px Pts={6}",
        result.CenterRow,
        result.CenterColumn,
        result.RadiusPx,
        result.DiameterPx,
        result.ResidualRms,
        result.Roundness,
        result.UsedPoints);
    circleFittingResultLabel.ForeColor = Color.Green;
}
```

- [ ] **Step 6: Replace clear state method**

Rename `ClearLineFittingState` to `ClearFittingState`, replace its body, and update **all 3 call
sites** (as of 2026-06-09 they are: the image-load handler ~line 182, `ClearEdgeDetectionButton_Click`
~line 398, and `EdgeDrawRoiCheck`/ROI-draw path ~line 622 — confirm with a grep for
`ClearLineFittingState` before editing):

```csharp
private void ClearFittingState()
{
    _latestEdgeRoi = null;
    _latestEdgeResult = null;
    _latestLineFittingResult = null;
    _latestCircleFittingResult = null;
    UpdateLineFittingResult(null);
    UpdateCircleFittingResult(null);
}
```

- [ ] **Step 7: Clear the circle result when a new edge result is produced**

`RunEdgeDetectionButton_Click` already clears the line-fitting state right after `BindEdgeResult`
(as of 2026-06-09, the block reads `_latestEdgeRoi = roi; _latestEdgeResult = result;
_latestLineFittingResult = null; UpdateLineFittingResult(null);`). Add the **circle** equivalent
to that **same block** so all per-detection invalidation lives in one place — do NOT also edit
`BindEdgeResult`, otherwise the line result gets cleared twice and the two features become
asymmetric:

```csharp
_latestEdgeRoi = roi;
_latestEdgeResult = result;
_latestLineFittingResult = null;
_latestCircleFittingResult = null;      // add this
UpdateLineFittingResult(null);
UpdateCircleFittingResult(null);        // add this
```

- [ ] **Step 8: Build to expose designer dependency**

Run:

```powershell
dotnet build .\src\FlashMeasurementSystem.App.Wpf\FlashMeasurementSystem.App.Wpf.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build fails until Task 7 adds `fitCircleButton` and `circleFittingResultLabel` declarations and event wiring in Designer.

- [ ] **Step 9: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: wire circle fitting window logic`.
## Task 7: Add WinForms Designer Controls

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`

- [ ] **Step 1: Add field declarations and initialization declarations**

Add `fitCircleButton` and `circleFittingResultLabel` beside existing Line Fitting controls. The initialization block should include:

```csharp
this.edgeButtonPanel = new System.Windows.Forms.TableLayoutPanel();
this._runEdgeDetectionButton = new System.Windows.Forms.Button();
this._clearEdgeDetectionButton = new System.Windows.Forms.Button();
this.fitLineButton = new System.Windows.Forms.Button();
this.fitCircleButton = new System.Windows.Forms.Button();
this._edgeStatusLabel = new System.Windows.Forms.Label();
this.lineFittingResultLabel = new System.Windows.Forms.Label();
this.circleFittingResultLabel = new System.Windows.Forms.Label();
this._edgeResultsGrid = new System.Windows.Forms.DataGridView();
```

The class field declarations at the bottom of the file should include:

```csharp
private System.Windows.Forms.Button fitLineButton;
private System.Windows.Forms.Button fitCircleButton;
private System.Windows.Forms.Label lineFittingResultLabel;
private System.Windows.Forms.Label circleFittingResultLabel;
```

- [ ] **Step 2: Change button panel from three to four columns**

Replace the `edgeButtonPanel` setup with:

```csharp
this.edgeButtonPanel.ColumnCount = 4;
this.edgeTableLayout.SetColumnSpan(this.edgeButtonPanel, 2);
this.edgeButtonPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
this.edgeButtonPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
this.edgeButtonPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
this.edgeButtonPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
this.edgeButtonPanel.Controls.Add(this._runEdgeDetectionButton, 0, 0);
this.edgeButtonPanel.Controls.Add(this._clearEdgeDetectionButton, 1, 0);
this.edgeButtonPanel.Controls.Add(this.fitLineButton, 2, 0);
this.edgeButtonPanel.Controls.Add(this.fitCircleButton, 3, 0);
this.edgeButtonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
this.edgeButtonPanel.Location = new System.Drawing.Point(3, 237);
this.edgeButtonPanel.Name = "edgeButtonPanel";
this.edgeButtonPanel.RowCount = 1;
this.edgeButtonPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
this.edgeButtonPanel.Size = new System.Drawing.Size(225, 28);
this.edgeButtonPanel.TabIndex = 17;
```

- [ ] **Step 3: Add Fit Circle button block**

Add this block after the `fitLineButton` block:

```csharp
// 
// fitCircleButton
// 
this.fitCircleButton.Dock = System.Windows.Forms.DockStyle.Fill;
this.fitCircleButton.Location = new System.Drawing.Point(171, 3);
this.fitCircleButton.Name = "fitCircleButton";
this.fitCircleButton.Size = new System.Drawing.Size(51, 22);
this.fitCircleButton.TabIndex = 3;
this.fitCircleButton.Text = "Fit Circle";
this.fitCircleButton.UseVisualStyleBackColor = true;
this.fitCircleButton.Click += new System.EventHandler(this.FitCircleButton_Click);
```

- [ ] **Step 4: Add Circle Fitting result label block**

Add this block after the `lineFittingResultLabel` block:

```csharp
// 
// circleFittingResultLabel
// 
this.edgeTableLayout.SetColumnSpan(this.circleFittingResultLabel, 2);
this.circleFittingResultLabel.Dock = System.Windows.Forms.DockStyle.Fill;
this.circleFittingResultLabel.Location = new System.Drawing.Point(3, 341);
this.circleFittingResultLabel.Name = "circleFittingResultLabel";
this.circleFittingResultLabel.Size = new System.Drawing.Size(225, 48);
this.circleFittingResultLabel.TabIndex = 20;
this.circleFittingResultLabel.Text = "圓擬合: 尚未執行";
this.circleFittingResultLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
```

- [ ] **Step 5: Insert label into edge table layout and move grid**

Find the `edgeTableLayout.Controls.Add(...)` section and make sure it adds both fitting labels before the grid:

```csharp
this.edgeTableLayout.Controls.Add(this._edgeStatusLabel, 0, 10);
this.edgeTableLayout.Controls.Add(this.lineFittingResultLabel, 0, 11);
this.edgeTableLayout.Controls.Add(this.circleFittingResultLabel, 0, 12);
this.edgeTableLayout.Controls.Add(this._edgeResultsGrid, 0, 13);
```

Change the `edgeTableLayout.RowCount` from 13 to 14 and **insert** one `48F` row style for the
new circle label **before** the existing trailing `Percent 100F` row (which belongs to the grid).

> ⚠️ Do NOT simply append two `RowStyles.Add(...)` lines. The current `RowStyles` collection
> already ends with `RowStyle(Percent, 100F)` for the grid (Designer line ~517). If you append
> `Absolute 48F` then `Percent 100F`, the circle label row (index 12) inherits the old
> `Percent 100F` and **expands to fill**, while the grid (index 13) gets squished to `48F`.
> TableLayoutPanel maps RowStyles by index, so the new 48F must land at index 12, before the
> grid's percent row.

Two correct options:

**Option A — insert at the right index** (leaves existing lines untouched):

```csharp
this.edgeTableLayout.RowCount = 14;
// index 11 = lineFittingResultLabel (48F, already present)
// index 12 = circleFittingResultLabel (new, 48F) — insert before the grid's percent row
// index 13 = grid (Percent 100F, already present as the last entry)
this.edgeTableLayout.RowStyles.Insert(12,
    new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
```

**Option B — change `RowCount` to 14 and replace the existing final**
`RowStyles.Add(... Percent, 100F)` line (Designer ~line 517) with two lines in this exact order:

```csharp
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
```

After the change the row styles must read (index : style): `0-8 : 26F`, `9 : 34F`, `10 : 22F`,
`11 : 48F (line label)`, `12 : 48F (circle label)`, `13 : Percent 100F (grid)`.

Update `_edgeResultsGrid` layout:

```csharp
this.edgeTableLayout.SetColumnSpan(this._edgeResultsGrid, 2);
this._edgeResultsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
this._edgeResultsGrid.Location = new System.Drawing.Point(3, 392);
this._edgeResultsGrid.Name = "_edgeResultsGrid";
this._edgeResultsGrid.ReadOnly = true;
this._edgeResultsGrid.RowHeadersVisible = false;
this._edgeResultsGrid.Size = new System.Drawing.Size(225, 166);
this._edgeResultsGrid.TabIndex = 21;
```

- [ ] **Step 6: Build App project**

Run:

```powershell
dotnet build .\src\FlashMeasurementSystem.App.Wpf\FlashMeasurementSystem.App.Wpf.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build passes. If designer line numbers differ, preserve the same control hierarchy and event wiring.

- [ ] **Step 7: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add circle fitting gui action`.

## Task 8: Full Verification

**Files:**
- Verify all files changed by Tasks 1-7.

- [ ] **Step 1: Run LSP diagnostics on changed source and test files**

Run diagnostics for:

```text
src/FlashMeasurementSystem.Domain/CircleFitting/CircleFittingParameters.cs
src/FlashMeasurementSystem.Domain/CircleFitting/CircleFittingResult.cs
src/FlashMeasurementSystem.Application/CircleFitting/ICircleFitter.cs
src/FlashMeasurementSystem.Halcon/CircleFitting/HalconCircleFitter.cs
src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs
src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs
tests/FlashMeasurementSystem.Tests/CircleFittingDomainTests.cs
tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
```

Expected: no new diagnostics caused by this change.

- [ ] **Step 2: Build and run tests**

Run:

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
.\tests\FlashMeasurementSystem.Tests\bin\Debug\FlashMeasurementSystem.Tests.exe
```

Expected output includes:

```text
LineFittingDomainTests passed
CircleFittingDomainTests passed
EdgeDetectionDomainTests passed
```

- [ ] **Step 3: Build full solution Any CPU and x64**

Run:

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: both builds pass.

- [ ] **Step 4: Manual GUI verification**

Run the app and verify (close any running instance first — a live app holds a lock on the dll
files and `dotnet build` will then fail with MSB3026/MSB3027):

1. Load an image from `data/images`.
2. Open the Edge Detection tab.
3. Enable `Draw ROI` and draw a rectangular ROI around a circular or arc-like feature.
4. Click `Detect`.
5. Confirm edge points populate `_edgeResultsGrid`.
6. Click `Fit Circle`.
7. Confirm the result label shows `Circle OK` with center, radius, diameter, RMS, roundness, and point count.
8. Confirm the fitted circle appears in green over the image **and** the blue ROI rectangle and cyan edge crosses remain visible (the overlay redraws all three layers).
9. Click `Clear` and confirm circle result returns to `圓擬合: 尚未執行`.
10. Click `Fit Circle` before Detect and confirm it shows `請先執行邊緣檢測`.
11. **Stale state on new image**: Detect on image A → load image B → click `Fit Circle`. Expected: `請先執行邊緣檢測` (cached edge result was invalidated by `ClearFittingState` in the image-load handler). Without this, the circle would be drawn at image-A coordinates on image B.
12. **Stale state on new ROI**: Detect → draw a different ROI (without Detecting) → click `Fit Circle`. Expected: `請先執行邊緣檢測` (`OnImageRoiSelected` calls `ClearFittingState`).
13. **Subpix scale**: switch the algorithm radio to `EdgesSubPix`, Detect on a sharp circular edge (the subpix path may return hundreds to thousands of contour points), then `Fit Circle`. Expected: completes within ~seconds, the green circle hugs the edge, RMS is small. A large RMS / Roundness hints the input set spans more than one physical circle (see Spec §Known Limitations).
14. **Insufficient points**: raise Threshold so Detect returns fewer than 3 points, then `Fit Circle`. Expected: `邊緣點不足 (need >= 3, got M; ClippingEndPoints=0)`; no exception, no crash.
15. **Arc vs closed**: draw an ROI that crosses only a partial arc of a circle. Expected: a valid center/radius is still returned (fit works on arcs); confirm the green full-circle overlay is plausible. Note `StartPhi`/`EndPhi` are populated for arcs because the default `MaxClosureDist=0` treats most contours as open arcs — this does not affect the center/radius/diameter values.

- [ ] **Step 5: Final diff review**

Confirm:

- Domain files contain no Halcon types.
- Application interface references only Domain types.
- HALCON parameter order matches `FitCircleContourXld`: `Algorithm, MaxNumPoints, MaxClosureDist, ClippingEndPoints, Iterations, ClippingFactor`.
- UI does not rerun Edge Detection inside Fit Circle.
- No `GenMeasureArc`, `MeasurePos`, `MeasurePairs`, 2D Metrology, or custom RANSAC code was added.
- No unrelated formatting or refactors were included.

- [ ] **Step 6: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add circle fitting from edge points`.
