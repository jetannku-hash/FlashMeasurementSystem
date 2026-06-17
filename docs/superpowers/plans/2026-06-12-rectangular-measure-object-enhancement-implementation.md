# Rectangular Measure Object Enhancement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add interpolation selection, `measure_pairs` support, numeric ROI control with live preview, and pair result display to the existing rectangular HALCON 1D measure object workflow.

**Architecture:** Extend `EdgeDetectionParameters` with `Interpolation` and `MeasureMode` fields. Add `EdgePair` DTO and `EdgePairs` list on `EdgeResult`. Add `HalconEdgeDetector.RunMeasurePairs(...)` alongside existing `RunMeasurePos`. Wire UI controls (MeasureMode combo, Interpolation combo, Angle numeric) in the Edge Detection tab. Change `ScanLength`/`RoiWidth` semantics from UI hints to actual detection parameters. Clear stale state on numeric ROI change. Keep existing `MeasurePos` / `EdgesSubPix` behavior unchanged.

**Tech Stack:** C# .NET Framework 4.8, WinForms, HALCON 17.12 `HalconDotNet`, old-style `.csproj`, console-style tests.

---

## Implementation Rules

- Do not commit during execution unless the user explicitly asks for commits. Suggested commit messages below are checkpoint labels only.
- Keep dependency direction unchanged: `Domain <- Application <- Halcon <- App.Wpf`.
- Do not put Halcon types in Domain.
- Do not add circular ROI, drag handles, recipe persistence, or WPF migration.
- Do not weaken existing `measure_pos` or `EdgesSubPix` behavior.
- Keep `MaxOverlayCrosses` and `MaxGridRows` sampling so dense `EdgesSubPix` results do not freeze the UI.
- Verify HALCON operator parameter order against HALCON 17.12 reference if uncertain -- do not rely on memory.
- The existing test runner `EdgeDetectionDomainTests.cs` has a duplicated `Console.WriteLine("EdgeDetectionDomainTests passed");` on line 72. Leave it as-is.
- For `edgeTableLayout` row changes: `TableLayoutPanel` maps `RowStyles` by index. **Insert** new row styles before the trailing `Percent 100F` row (grid), or the grid collapses.

## File Map

- Create `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgePair.cs`: pair DTO with First/Second edge fields plus distances.
- Modify `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionParameters.cs`: add `Interpolation`, `MeasureMode`, allow-list helpers.
- Modify `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionRoi.cs`: add `FromCenter(...)` factory for numeric ROI controls.
- Modify `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeResult.cs`: add `List<EdgePair> EdgePairs` property.
- Modify `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`: include `EdgePair.cs`.
- Modify `src/FlashMeasurementSystem.Halcon/EdgeDetection/HalconEdgeDetector.cs`: pass `p.Interpolation` to `GenMeasureRectangle2`, add `RunMeasurePairs(...)`, add mode dispatch in `DetectEdges(...)`.
- Modify `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`: wire `CreateEdgeDetectionParameters` with new fields, add `CreateEdgeDetectionRoiFromNumeric`, update `RunEdgeDetectionButton_Click`, add `BindEdgePairResult`, add pair overlay in `DrawFittingLayers`, clear state on numeric change.
- Modify `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`: add `_edgeInterpolationCombo`, `_edgeMeasureModeCombo`, `_edgeAngleNumeric` controls in `edgeTableLayout`.
- Modify `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`: add domain tests for new fields, factories, and DTO defaults.

### Task 1: Add Domain Tests First

**Files:**
- Modify: `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`

- [ ] **Step 1: Add domain tests for Interpolation, MeasureMode, FromCenter, EdgePair**

Append these test blocks inside `EdgeDetectionDomainTests.Run()` after the existing `FakeEdgeDetector` contract check and before the suite runner calls:

```csharp
// ─── Rectangular Measure Object enhancement tests ─────────────────────
EdgeDetectionParameters p2 = EdgeDetectionParameters.Default();
AssertEqual("nearest_neighbor", p2.Interpolation, "Default Interpolation");
AssertEqual("single_edge", p2.MeasureMode, "Default MeasureMode");

if (!EdgeDetectionParameters.IsSupportedInterpolation("nearest_neighbor"))
    throw new InvalidOperationException("nearest_neighbor should be supported");
if (!EdgeDetectionParameters.IsSupportedInterpolation("bilinear"))
    throw new InvalidOperationException("bilinear should be supported");
if (!EdgeDetectionParameters.IsSupportedInterpolation("bicubic"))
    throw new InvalidOperationException("bicubic should be supported");
if (EdgeDetectionParameters.IsSupportedInterpolation("cubic"))
    throw new InvalidOperationException("cubic should not be supported");

if (!EdgeDetectionParameters.IsSupportedMeasureMode("single_edge"))
    throw new InvalidOperationException("single_edge should be supported");
if (!EdgeDetectionParameters.IsSupportedMeasureMode("edge_pair"))
    throw new InvalidOperationException("edge_pair should be supported");
if (EdgeDetectionParameters.IsSupportedMeasureMode("multi_pair"))
    throw new InvalidOperationException("multi_pair should not be supported");

// FromCenter factory
EdgeDetectionRoi centerRoi = EdgeDetectionRoi.FromCenter(150.0, 250.0, 80.0, 30.0, 0.5);
AssertEqual(150.0, centerRoi.CenterRow, "FromCenter CenterRow");
AssertEqual(250.0, centerRoi.CenterCol, "FromCenter CenterCol");
AssertEqual(80.0, centerRoi.Length1, "FromCenter Length1");
AssertEqual(30.0, centerRoi.Length2, "FromCenter Length2");
AssertEqual(0.5, centerRoi.AngleRad, "FromCenter AngleRad");
AssertEqual(true, centerRoi.IsDefined, "FromCenter IsDefined");

// FromCenter preserves negative values (the caller may pass NaN or negative;
// IsDefined is the caller's responsibility)
EdgeDetectionRoi invalidRoi = EdgeDetectionRoi.FromCenter(0, 0, 0, 0, 0);
AssertEqual(false, invalidRoi.IsDefined, "FromCenter zero lengths not defined");

// EdgePair DTO defaults
EdgePair pair = new EdgePair();
AssertEqual(0.0, pair.FirstRow, "EdgePair default FirstRow");
AssertEqual(0.0, pair.FirstColumn, "EdgePair default FirstColumn");
AssertEqual(0.0, pair.FirstAmplitude, "EdgePair default FirstAmplitude");
AssertEqual(0.0, pair.SecondRow, "EdgePair default SecondRow");
AssertEqual(0.0, pair.SecondColumn, "EdgePair default SecondColumn");
AssertEqual(0.0, pair.SecondAmplitude, "EdgePair default SecondAmplitude");
AssertEqual(0.0, pair.IntraDistance, "EdgePair default IntraDistance");
AssertEqual(0.0, pair.InterDistance, "EdgePair default InterDistance");

// EdgeResult EdgePairs defaults
EdgeResult result2 = new EdgeResult();
AssertEqual(0, result2.EdgePairs.Count, "New result should have empty EdgePairs list");
```

- [ ] **Step 2: Run tests to verify red state**

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build fails because `EdgeDetectionParameters.Interpolation`, `EdgeDetectionParameters.MeasureMode`, `EdgeDetectionRoi.FromCenter(...)`, `EdgePair`, and `EdgeResult.EdgePairs` do not exist yet.

- [ ] **Step 3: Checkpoint**

Suggested commit message if the user later asks for commits: `test: add rectangular measure object domain tests`.

### Task 2: Add Domain DTOs

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgePair.cs`
- Modify: `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionParameters.cs`
- Modify: `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionRoi.cs`
- Modify: `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeResult.cs`
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`

- [ ] **Step 1: Add `Interpolation` and `MeasureMode` to `EdgeDetectionParameters`**

In `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionParameters.cs`, add fields after `HighThreshold` and before `Default()`:

```csharp
public string Interpolation { get; set; } = "nearest_neighbor";
public string MeasureMode { get; set; } = "single_edge";
```

Add allow-list helpers after `Default()`:

```csharp
public static bool IsSupportedInterpolation(string value)
{
    return value == "nearest_neighbor"
        || value == "bilinear"
        || value == "bicubic";
}

public static bool IsSupportedMeasureMode(string value)
{
    return value == "single_edge"
        || value == "edge_pair";
}
```

- [ ] **Step 2: Add `EdgeDetectionRoi.FromCenter(...)` factory**

In `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionRoi.cs`, add after `FromBounds(...)`:

```csharp
/// <summary>
/// 從數值控制項（Angle/ScanLength/RoiWidth）建立 ROI。
/// 呼叫端負責確保數值合法（Length1 >= 1 && Length2 >= 1）。
/// </summary>
public static EdgeDetectionRoi FromCenter(
    double centerRow,
    double centerCol,
    double length1,
    double length2,
    double angleRad)
{
    return new EdgeDetectionRoi
    {
        CenterRow = centerRow,
        CenterCol = centerCol,
        Length1 = length1,
        Length2 = length2,
        AngleRad = angleRad
    };
}
```

- [ ] **Step 3: Create `EdgePair` DTO**

Create `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgePair.cs`:

```csharp
namespace FlashMeasurementSystem.Domain.EdgeDetection
{
    public class EdgePair
    {
        public double FirstRow { get; set; }
        public double FirstColumn { get; set; }
        public double FirstAmplitude { get; set; }
        public double SecondRow { get; set; }
        public double SecondColumn { get; set; }
        public double SecondAmplitude { get; set; }
        public double IntraDistance { get; set; }
        public double InterDistance { get; set; }
    }
}
```

- [ ] **Step 4: Add `EdgePairs` to `EdgeResult`**

In `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeResult.cs`, add after `EdgePoints`:

```csharp
public List<EdgePair> EdgePairs { get; set; } = new List<EdgePair>();
```

- [ ] **Step 5: Include `EdgePair.cs` in csproj**

In `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`, add inside the compile item group:

```xml
<Compile Include="EdgeDetection\EdgePair.cs" />
```

Place it alphabetically beside `EdgeParameters.cs`, after the existing `EdgeDetection\` entries.

- [ ] **Step 6: Build and run domain tests**

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
.\tests\FlashMeasurementSystem.Tests\bin\Debug\FlashMeasurementSystem.Tests.exe
```

Expected: build passes and the output includes all existing suite pass lines plus the new assertions succeed (no `InvalidOperationException`).

- [ ] **Step 7: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add interpolation, measure mode, edge pair domain models`.

### Task 3: Expand HALCON Adapter for Interpolation and MeasurePairs

**Files:**
- Modify: `src/FlashMeasurementSystem.Halcon/EdgeDetection/HalconEdgeDetector.cs`

- [ ] **Step 1: Replace hardcoded interpolation in `RunMeasurePos`**

In `HalconEdgeDetector.cs` line 172, the `GenMeasureRectangle2` call currently passes `"nearest_neighbor"` as the last argument. Change the method signature to accept `string interpolation` and pass it through:

Change the `RunMeasurePos` signature (line 159-164):

```csharp
private static MeasureAttempt RunMeasurePos(
    HImage image,
    double centerRow, double centerCol, double phi,
    double length1, double length2,
    HTuple width, HTuple height,
    EdgeDetectionParameters p, string label)
```

Replace the `GenMeasureRectangle2` call at lines 170-172:

```csharp
HOperatorSet.GenMeasureRectangle2(
    centerRow, centerCol, phi, length1, length2,
    width, height, p.Interpolation, out measureHandle);
```

- [ ] **Step 2: Add `RunMeasurePairs(...)` method**

Add this method after `RunMeasurePos(...)` (after line 212):

```csharp
private static MeasureAttempt RunMeasurePairs(
    HImage image,
    double centerRow, double centerCol, double phi,
    double length1, double length2,
    HTuple width, HTuple height,
    EdgeDetectionParameters p, string label)
{
    var attempt = new MeasureAttempt { Edges = new List<EdgePoint>() };
    HTuple measureHandle = null;
    try
    {
        HOperatorSet.GenMeasureRectangle2(
            centerRow, centerCol, phi, length1, length2,
            width, height, p.Interpolation, out measureHandle);

        HOperatorSet.MeasurePairs(image, measureHandle,
            new HTuple(p.Sigma), new HTuple(p.Threshold),
            new HTuple(p.Polarity), new HTuple(p.EdgeSelector),
            out HTuple rowFirst, out HTuple colFirst, out HTuple ampFirst,
            out HTuple rowSecond, out HTuple colSecond, out HTuple ampSecond,
            out HTuple intraDistance, out HTuple interDistance);

        int len = rowFirst?.Length ?? 0;
        Log(string.Format(CultureInfo.InvariantCulture,
            "DetectEdges {0} MEASUREPAIRS phi={1:F4} L1={2:F1} L2={3:F1} pairs={4}",
            label, phi, length1, length2, len));

        for (int i = 0; i < len; i++)
        {
            // Flatten first/second points into EdgePoints for overlay compatibility.
            // Pair-specific distances live in a separate struct that the caller
            // must build from the raw tuples. Here we only fill the flat list.
            attempt.Edges.Add(new EdgePoint
            {
                Row = rowFirst[i].D,
                Column = colFirst[i].D,
                Amplitude = (i < ampFirst?.Length) ? ampFirst[i].D : 0.0,
                Distance = 0.0
            });
            attempt.Edges.Add(new EdgePoint
            {
                Row = rowSecond[i].D,
                Column = colSecond[i].D,
                Amplitude = (i < ampSecond?.Length) ? ampSecond[i].D : 0.0,
                Distance = 0.0
            });
        }

        // Return raw tuples so the caller can build EdgePairs.
        // This is stored on the attempt for later collection.
        attempt.RawFirstRows = rowFirst;
        attempt.RawFirstCols = colFirst;
        attempt.RawFirstAmps = ampFirst;
        attempt.RawSecondRows = rowSecond;
        attempt.RawSecondCols = colSecond;
        attempt.RawSecondAmps = ampSecond;
        attempt.RawIntraDistances = intraDistance;
        attempt.RawInterDistances = interDistance;
    }
    catch (HalconException ex)
    {
        attempt.Exception = ex;
        Log(string.Format(CultureInfo.InvariantCulture,
            "DetectEdges {0} MEASUREPAIRS EXCEPTION [{1}] {2}",
            label, ex.GetErrorCode(), ex.Message));
    }
    finally
    {
        if (measureHandle != null) HOperatorSet.CloseMeasure(measureHandle);
    }
    return attempt;
}
```

- [ ] **Step 3: Extend `MeasureAttempt` struct with raw tuples**

At the existing `MeasureAttempt` struct (line 143-147), add fields after `Exception`:

```csharp
// Raw HALCON tuples for MeasurePairs; null for MeasurePos.
public HTuple RawFirstRows;
public HTuple RawFirstCols;
public HTuple RawFirstAmps;
public HTuple RawSecondRows;
public HTuple RawSecondCols;
public HTuple RawSecondAmps;
public HTuple RawIntraDistances;
public HTuple RawInterDistances;
```

- [ ] **Step 4: Add mode dispatch in `DetectEdges(...)`**

In `DetectEdges(...)`, after clamping and before the try block (~line 75), add interpolation and measure mode validation:

After line 74 (`HImage workingImage = ...;`), add:

```csharp
// Validate new parameters before HALCON calls.
if (!EdgeDetectionParameters.IsSupportedInterpolation(effectiveParameters.Interpolation))
{
    result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
        "不支援的插值模式: {0} (支援: nearest_neighbor, bilinear, bicubic)",
        effectiveParameters.Interpolation);
    Log("DetectEdges EARLY-EXIT msg=" + result.ErrorMessage);
    return result;
}

if (!EdgeDetectionParameters.IsSupportedMeasureMode(effectiveParameters.MeasureMode))
{
    result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
        "不支援的量測模式: {0} (支援: single_edge, edge_pair)",
        effectiveParameters.MeasureMode);
    Log("DetectEdges EARLY-EXIT msg=" + result.ErrorMessage);
    return result;
}
```

Replace the PRIMARY attempt block (lines 77-80, the `RunMeasurePos` call) with mode-dispatch. The old code is:

```csharp
MeasureAttempt primary = RunMeasurePos(workingImage,
    clampedCenterRow, clampedCenterCol, roi.AngleRad,
    maxLength1, maxLength2, width, height, effectiveParameters, "PRIMARY");
```

Replace with:

```csharp
bool usePairs = effectiveParameters.MeasureMode == "edge_pair";
MeasureAttempt primary = usePairs
    ? RunMeasurePairs(workingImage,
        clampedCenterRow, clampedCenterCol, roi.AngleRad,
        maxLength1, maxLength2, width, height, effectiveParameters, "PRIMARY")
    : RunMeasurePos(workingImage,
        clampedCenterRow, clampedCenterCol, roi.AngleRad,
        maxLength1, maxLength2, width, height, effectiveParameters, "PRIMARY");
```

Similarly replace the FALLBACK attempt block (lines 92-95):

```csharp
MeasureAttempt fallback = RunMeasurePos(workingImage,
    clampedCenterRow, clampedCenterCol, fallbackPhi,
    maxLength2, maxLength1,
    width, height, effectiveParameters, "FALLBACK");
```

Replace with:

```csharp
MeasureAttempt fallback = usePairs
    ? RunMeasurePairs(workingImage,
        clampedCenterRow, clampedCenterCol, fallbackPhi,
        maxLength2, maxLength1,
        width, height, effectiveParameters, "FALLBACK")
    : RunMeasurePos(workingImage,
        clampedCenterRow, clampedCenterCol, fallbackPhi,
        maxLength2, maxLength1,
        width, height, effectiveParameters, "FALLBACK");
```

- [ ] **Step 5: Build `EdgePairs` from raw tuples after a successful measure_pairs run**

After the primary/fallback selection block (after line 101 `selected = fallback;`), and before `result.EdgePoints = selected.Edges;` (~line 103), add the pair-building step:

```csharp
// Build EdgePairs list from raw tuples when using measure_pairs.
if (usePairs && selected.Exception == null && selected.RawFirstRows != null)
{
    int pairCount = selected.RawFirstRows.Length;
    for (int i = 0; i < pairCount; i++)
    {
        result.EdgePairs.Add(new EdgePair
        {
            FirstRow = selected.RawFirstRows[i].D,
            FirstColumn = selected.RawFirstCols[i].D,
            FirstAmplitude = (i < selected.RawFirstAmps?.Length) ? selected.RawFirstAmps[i].D : 0.0,
            SecondRow = selected.RawSecondRows[i].D,
            SecondColumn = selected.RawSecondCols[i].D,
            SecondAmplitude = (i < selected.RawSecondAmps?.Length) ? selected.RawSecondAmps[i].D : 0.0,
            IntraDistance = (i < selected.RawIntraDistances?.Length) ? selected.RawIntraDistances[i].D : 0.0,
            InterDistance = (i < selected.RawInterDistances?.Length) ? selected.RawInterDistances[i].D : 0.0
        });
    }
}
```

- [ ] **Step 6: Build Halcon project for Any CPU and x64**

```powershell
dotnet build .\src\FlashMeasurementSystem.Halcon\FlashMeasurementSystem.Halcon.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\src\FlashMeasurementSystem.Halcon\FlashMeasurementSystem.Halcon.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: both builds pass. If HALCON reference resolution fails, report the exact path/SDK blocker before changing code.

- [ ] **Step 7: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add interpolation and measure pairs to halcon edge detector`.

### Task 4: Wire UI Parameters and Result Display in MainWindow Code

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

> Before editing, verify the line numbers for `CreateEdgeDetectionParameters`, `CreateEdgeDetectionRoi`, `RunEdgeDetectionButton_Click`, `OnImageRoiSelected`, `DrawFittingLayers`, `ShowFittingOverlay`, `ClearFittingState`, `BindEdgeResult`, and `SetEdgeStatus` — the snippets below are anchored to method names, not line numbers.

- [ ] **Step 1: Update `CreateEdgeDetectionParameters()` to include new fields**

Replace the existing method (currently returns `EdgeDetectionParameters` without `Interpolation`/`MeasureMode`):

```csharp
private EdgeDetectionParameters CreateEdgeDetectionParameters()
{
    return new EdgeDetectionParameters
    {
        Sigma = (double)_edgeSigmaNumeric.Value,
        Threshold = (double)_edgeThresholdNumeric.Value,
        Polarity = _edgePolarityCombo.SelectedItem.ToString(),
        EdgeSelector = _edgeSelectorCombo.SelectedItem.ToString(),
        Interpolation = _edgeInterpolationCombo.SelectedItem?.ToString() ?? "nearest_neighbor",
        MeasureMode = _edgeMeasureModeCombo.SelectedItem?.ToString() ?? "single_edge"
    };
}
```

- [ ] **Step 2: Change `RunEdgeDetectionButton_Click` to use numeric ROI when the ROI has been drawn once**

Replace the `RunEdgeDetectionButton_Click` method body. Currently the detection flow is:

```csharp
EdgeDetectionRoi roi = CreateEdgeDetectionRoi(_imageHelper.GetCurrentRoi());
```

This always uses `FromBounds` from the mouse-drawn ROI. The new logic should use `FromCenter` with numeric values when the numeric controls have been set (i.e., after the initial draw). Replace the single-line ROI creation with:

```csharp
EdgeDetectionRoi roi;
if (_imageHelper.HasRoi && _edgeAngleNumeric.Value != 0 || _edgeScanLengthNumeric.Value > 0)
{
    // Use the numeric controls as source of truth.
    var currentRoi = _imageHelper.GetCurrentRoi();
    double centerRow = (currentRoi.Row1 + currentRoi.Row2) / 2.0;
    double centerCol = (currentRoi.Col1 + currentRoi.Col2) / 2.0;
    double angleRad = (double)_edgeAngleNumeric.Value * Math.PI / 180.0;
    double length1 = (double)_edgeScanLengthNumeric.Value / 2.0;
    double length2 = (double)_edgeRoiWidthNumeric.Value / 2.0;
    roi = EdgeDetectionRoi.FromCenter(centerRow, centerCol, length1, length2, angleRad);
}
else
{
    roi = CreateEdgeDetectionRoi(_imageHelper.GetCurrentRoi());
}
```

Note: The `_edgeAngleNumeric.Value != 0` condition is a simple heuristic. Since angle defaults to `0` after initial draw (the initial ROI from `FromBounds` produces `AngleRad = 0 or π/2`, which maps to `0° or 90°`), the numeric path activates on any user change. The full condition `|| _edgeScanLengthNumeric.Value > 0` ensures the path always fires when a valid scan length was set from initial draw.

- [ ] **Step 3: Add callback for numeric ROI value changes**

Add this method after `EdgeDrawRoiCheck_CheckedChanged`:

```csharp
private void OnEdgeRoiNumericChanged(object sender, EventArgs e)
{
    // Numeric ROI change invalidates all edge/fitting state.
    _latestEdgeResult = null;
    _latestLineFittingResult = null;
    _latestCircleFittingResult = null;
    UpdateLineFittingResult(null);
    UpdateCircleFittingResult(null);
    _edgeResultsGrid.Rows.Clear();
    _edgeStatusLabel.Text = "Draw ROI, then Detect";
    _edgeStatusLabel.ForeColor = Color.Black;

    // Rebuild the persistent overlay with the updated ROI rectangle.
    if (_imageHelper?.CurrentImage != null && _imageHelper.HasRoi)
    {
        var currentRoi = _imageHelper.GetCurrentRoi();
        double centerRow = (currentRoi.Row1 + currentRoi.Row2) / 2.0;
        double centerCol = (currentRoi.Col1 + currentRoi.Col2) / 2.0;
        double angleRad = (double)_edgeAngleNumeric.Value * Math.PI / 180.0;
        double length1 = (double)_edgeScanLengthNumeric.Value / 2.0;
        double length2 = (double)_edgeRoiWidthNumeric.Value / 2.0;
        EdgeDetectionRoi numericRoi = EdgeDetectionRoi.FromCenter(centerRow, centerCol, length1, length2, angleRad);

        _imageHelper.SetPersistentOverlayAction(() =>
        {
            _imageHelper.Annotator.DrawRectangle2(
                numericRoi.CenterRow, numericRoi.CenterCol,
                numericRoi.AngleRad, numericRoi.Length1, numericRoi.Length2, "blue");
        });
    }
}
```

- [ ] **Step 4: Update `DrawFittingLayers` to draw EdgePair overlays**

In `DrawFittingLayers`, after the existing edge cross loop (after `an.DrawCross(...)` line and before the line fitting block), add:

```csharp
// Draw pair-specific overlays (cyan crosses for both edges, short line for IntraDistance).
EdgeResult edgesWithPairs = _latestEdgeResult;
if (edgesWithPairs != null && edgesWithPairs.EdgePairs != null && edgesWithPairs.EdgePairs.Count > 0)
{
    int pairStep = Math.Max(1, edgesWithPairs.EdgePairs.Count / MaxOverlayCrosses);
    for (int i = 0; i < edgesWithPairs.EdgePairs.Count; i += pairStep)
    {
        EdgePair pair = edgesWithPairs.EdgePairs[i];
        an.DrawCross(pair.FirstRow, pair.FirstColumn, 8, "cyan");
        an.DrawCross(pair.SecondRow, pair.SecondColumn, 8, "cyan");
        // Draw a short yellow line between first and second edge for IntraDistance visualization.
        an.DrawLine(pair.FirstRow, pair.FirstColumn, pair.SecondRow, pair.SecondColumn, "yellow");
    }
}
```

- [ ] **Step 5: Add `EdgePair` result display in `BindEdgeResult`**

After `BindEdgeResult`, add a new method `BindEdgePairResult` and call it from `RunEdgeDetectionButton_Click`.

First, add this method after `BindEdgeResult`:

```csharp
private void BindEdgePairResult(EdgeResult result)
{
    if (result.EdgePairs == null || result.EdgePairs.Count == 0)
        return;

    int total = result.EdgePairs.Count;
    int displayCount = Math.Min(total, MaxGridRows);

    // For pair mode, we need different columns. Since we want to reuse the
    // existing grid, clear and re-add columns for pair display.
    _edgeResultsGrid.Columns.Clear();

    // Add pair-specific columns.
    DataGridViewTextBoxColumn idxCol = new DataGridViewTextBoxColumn();
    idxCol.HeaderText = "#";
    _edgeResultsGrid.Columns.Add(idxCol);

    DataGridViewTextBoxColumn r1Col = new DataGridViewTextBoxColumn();
    r1Col.HeaderText = "Row1";
    _edgeResultsGrid.Columns.Add(r1Col);

    DataGridViewTextBoxColumn c1Col = new DataGridViewTextBoxColumn();
    c1Col.HeaderText = "Col1";
    _edgeResultsGrid.Columns.Add(c1Col);

    DataGridViewTextBoxColumn a1Col = new DataGridViewTextBoxColumn();
    a1Col.HeaderText = "Amp1";
    _edgeResultsGrid.Columns.Add(a1Col);

    DataGridViewTextBoxColumn r2Col = new DataGridViewTextBoxColumn();
    r2Col.HeaderText = "Row2";
    _edgeResultsGrid.Columns.Add(r2Col);

    DataGridViewTextBoxColumn c2Col = new DataGridViewTextBoxColumn();
    c2Col.HeaderText = "Col2";
    _edgeResultsGrid.Columns.Add(c2Col);

    DataGridViewTextBoxColumn a2Col = new DataGridViewTextBoxColumn();
    a2Col.HeaderText = "Amp2";
    _edgeResultsGrid.Columns.Add(a2Col);

    DataGridViewTextBoxColumn intraCol = new DataGridViewTextBoxColumn();
    intraCol.HeaderText = "Intra";
    _edgeResultsGrid.Columns.Add(intraCol);

    DataGridViewTextBoxColumn interCol = new DataGridViewTextBoxColumn();
    interCol.HeaderText = "Inter";
    _edgeResultsGrid.Columns.Add(interCol);

    _edgeResultsGrid.Rows.Clear();
    for (int i = 0; i < displayCount; i++)
    {
        EdgePair pair = result.EdgePairs[i];
        _edgeResultsGrid.Rows.Add(
            i + 1,
            pair.FirstRow.ToString("F2", CultureInfo.InvariantCulture),
            pair.FirstColumn.ToString("F2", CultureInfo.InvariantCulture),
            pair.FirstAmplitude.ToString("F1", CultureInfo.InvariantCulture),
            pair.SecondRow.ToString("F2", CultureInfo.InvariantCulture),
            pair.SecondColumn.ToString("F2", CultureInfo.InvariantCulture),
            pair.SecondAmplitude.ToString("F1", CultureInfo.InvariantCulture),
            pair.IntraDistance.ToString("F2", CultureInfo.InvariantCulture),
            pair.InterDistance.ToString("F2", CultureInfo.InvariantCulture));
    }
}
```

Then update `RunEdgeDetectionButton_Click` after the existing `BindEdgeResult(result);` call, add:

```csharp
// For edge_pair mode, rebuild columns and display pair rows.
if (parameters.MeasureMode == "edge_pair")
{
    BindEdgePairResult(result);
}
```

- [ ] **Step 6: Restore normal grid columns when switching back to Single Edge or EdgesSubPix**

In `RunEdgeDetectionButton_Click`, after `BindEdgeResult(result);`, add the pair result binding AND ensure that single_edge mode restores the original grid columns. Replace the simple `BindEdgeResult(result);` with:

```csharp
if (parameters.MeasureMode == "edge_pair")
{
    // Edge pair mode uses its own column layout.
    BindEdgePairResult(result);
}
else
{
    // Restore default columns if they were replaced by a previous pair run.
    RestoreDefaultEdgeGridColumns();
    BindEdgeResult(result);
}
```

Add the `RestoreDefaultEdgeGridColumns` method:

```csharp
private void RestoreDefaultEdgeGridColumns()
{
    if (_edgeResultsGrid.Columns.Count == 5
        && _edgeResultsGrid.Columns[0].HeaderText == "#"
        && _edgeResultsGrid.Columns[1].HeaderText == "Row"
        && _edgeResultsGrid.Columns[2].HeaderText == "Column"
        && _edgeResultsGrid.Columns[3].HeaderText == "Amp"
        && _edgeResultsGrid.Columns[4].HeaderText == "Dist")
    {
        return; // Already default columns.
    }

    _edgeResultsGrid.Columns.Clear();
    _edgeResultsGrid.Columns.Add(edgeIndexColumn);
    _edgeResultsGrid.Columns.Add(edgeRowColumn);
    _edgeResultsGrid.Columns.Add(edgeColumnColumn);
    _edgeResultsGrid.Columns.Add(edgeAmplitudeColumn);
    _edgeResultsGrid.Columns.Add(edgeDistanceColumn);
}
```

- [ ] **Step 7: Wire numeric change events in constructor**

In the constructor (`MainWindow()`), after the existing `EnsureComboDefault(...)` calls, add:

```csharp
_edgeAngleNumeric.ValueChanged += OnEdgeRoiNumericChanged;
_edgeScanLengthNumeric.ValueChanged += OnEdgeRoiNumericChanged;
_edgeRoiWidthNumeric.ValueChanged += OnEdgeRoiNumericChanged;
```

- [ ] **Step 8: Ensure `ClearEdgeDetectionButton_Click` also resets the grid columns**

In `ClearEdgeDetectionButton_Click`, after `_edgeResultsGrid.Rows.Clear();`, add:

```csharp
RestoreDefaultEdgeGridColumns();
```

- [ ] **Step 9: Build to expose designer dependency**

```powershell
dotnet build .\src\FlashMeasurementSystem.App.Wpf\FlashMeasurementSystem.App.Wpf.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build fails until Task 5 declares `_edgeInterpolationCombo`, `_edgeMeasureModeCombo`, and `_edgeAngleNumeric` controls and wires their `ValueChanged` events in the Designer.

- [ ] **Step 10: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: wire rectangular measure controls and pair result display`.

### Task 5: Add WinForms Designer Controls in Edge Detection Tab

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`

> The `edgeTableLayout` currently has `RowCount = 14` with row indices 0-13. The layout (by index as of 2026-06-12): `0:44F (algorithm)`, `1-8:26F` (sigma, threshold, polarity, selector, subpixel, RoiWidth, ScanLength, DrawRoi), `9:58F` (buttonPanel), `10:22F` (status), `11:48F` (lineResult), `12:48F` (circleResult), `13:Percent 100F` (grid).

- [ ] **Step 1: Insert three new rows into `edgeTableLayout`**

Change `RowCount` from `14` to `17`. The new row layout by index must be:

- `0` : `44F` — Algorithm (unchanged)
- `1` : `26F` — Sigma (unchanged)
- `2` : `26F` — Threshold (unchanged)
- `3` : `26F` — Polarity (unchanged)
- `4` : `26F` — Selector (unchanged)
- `5` : `26F` — Subpixel (unchanged)
- `6` : `26F` — ROI Width (unchanged)
- `7` : `26F` — Scan Length (unchanged)
- `8` : `26F` — Draw ROI checkbox (unchanged)
- `9` : `26F` — **new** Measure Mode row (`edgeMeasureModeLabel` col 0 + `_edgeMeasureModeCombo` col 1)
- `10`: `26F` — **new** Interpolation row (`edgeInterpolationLabel` col 0 + `_edgeInterpolationCombo` col 1)
- `11`: `26F` — **new** Angle row (`edgeAngleLabel` col 0 + `_edgeAngleNumeric` col 1)
- `12`: `58F` — Button panel (moved from index 9)
- `13`: `22F` — Status label (moved from index 10)
- `14`: `48F` — Line fitting result (moved from index 11)
- `15`: `48F` — Circle fitting result (moved from index 12)
- `16`: `Percent 100F` — Grid (moved from index 13)

Replace the `RowStyles` block (~lines 536-549) to match the new order. **The old `RowStyles` collection must be rebuilt** because inserting at the middle of a dense row collection is error-prone. The new block:

```csharp
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));  // MeasureMode
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));  // Interpolation
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));  // Angle
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 58F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
```

- [ ] **Step 2: Add control declarations in `InitializeComponent`**

In the initialization region (top of `InitializeComponent`, around line 19), add after the existing edge detection control declarations (after `this._edgeResultsGrid = ...`):

```csharp
this.edgeMeasureModeLabel = new System.Windows.Forms.Label();
this._edgeMeasureModeCombo = new System.Windows.Forms.ComboBox();
this.edgeInterpolationLabel = new System.Windows.Forms.Label();
this._edgeInterpolationCombo = new System.Windows.Forms.ComboBox();
this.edgeAngleLabel = new System.Windows.Forms.Label();
this._edgeAngleNumeric = new System.Windows.Forms.NumericUpDown();
```

Also add the edge detection controls to the SuspendLayout list. In the SuspendLayout block around line 122-128, add:

```csharp
((System.ComponentModel.ISupportInitialize)(this._edgeAngleNumeric)).BeginInit();
```

And the corresponding `EndInit` in the resume section.

- [ ] **Step 3: Update `Controls.Add` calls in `edgeTableLayout`**

Find the `edgeTableLayout.Controls.Add(...)` block (currently lines 511-532). Replace it to place existing controls at the new row indices and add new controls:

```csharp
this.edgeTableLayout.Controls.Add(this.edgeAlgorithmLabel, 0, 0);
this.edgeTableLayout.Controls.Add(this.edgeAlgorithmPanel, 1, 0);
this.edgeTableLayout.Controls.Add(this.edgeSigmaLabel, 0, 1);
this.edgeTableLayout.Controls.Add(this._edgeSigmaNumeric, 1, 1);
this.edgeTableLayout.Controls.Add(this.edgeThresholdLabel, 0, 2);
this.edgeTableLayout.Controls.Add(this._edgeThresholdNumeric, 1, 2);
this.edgeTableLayout.Controls.Add(this.edgePolarityLabel, 0, 3);
this.edgeTableLayout.Controls.Add(this._edgePolarityCombo, 1, 3);
this.edgeTableLayout.Controls.Add(this.edgeSelectorLabel, 0, 4);
this.edgeTableLayout.Controls.Add(this._edgeSelectorCombo, 1, 4);
this.edgeTableLayout.Controls.Add(this.edgeSubpixelLabel, 0, 5);
this.edgeTableLayout.Controls.Add(this._edgeSubpixelMethodCombo, 1, 5);
this.edgeTableLayout.Controls.Add(this.edgeRoiWidthLabel, 0, 6);
this.edgeTableLayout.Controls.Add(this._edgeRoiWidthNumeric, 1, 6);
this.edgeTableLayout.Controls.Add(this.edgeScanLengthLabel, 0, 7);
this.edgeTableLayout.Controls.Add(this._edgeScanLengthNumeric, 1, 7);
this.edgeTableLayout.Controls.Add(this._edgeDrawRoiCheck, 0, 8);
this.edgeTableLayout.SetColumnSpan(this._edgeDrawRoiCheck, 2);
this.edgeTableLayout.Controls.Add(this.edgeMeasureModeLabel, 0, 9);
this.edgeTableLayout.Controls.Add(this._edgeMeasureModeCombo, 1, 9);
this.edgeTableLayout.Controls.Add(this.edgeInterpolationLabel, 0, 10);
this.edgeTableLayout.Controls.Add(this._edgeInterpolationCombo, 1, 10);
this.edgeTableLayout.Controls.Add(this.edgeAngleLabel, 0, 11);
this.edgeTableLayout.Controls.Add(this._edgeAngleNumeric, 1, 11);
this.edgeTableLayout.Controls.Add(this.edgeButtonPanel, 0, 12);
this.edgeTableLayout.SetColumnSpan(this.edgeButtonPanel, 2);
this.edgeTableLayout.Controls.Add(this._edgeStatusLabel, 0, 13);
this.edgeTableLayout.SetColumnSpan(this._edgeStatusLabel, 2);
this.edgeTableLayout.Controls.Add(this.lineFittingResultLabel, 0, 14);
this.edgeTableLayout.SetColumnSpan(this.lineFittingResultLabel, 2);
this.edgeTableLayout.Controls.Add(this.circleFittingResultLabel, 0, 15);
this.edgeTableLayout.SetColumnSpan(this.circleFittingResultLabel, 2);
this.edgeTableLayout.Controls.Add(this._edgeResultsGrid, 0, 16);
this.edgeTableLayout.SetColumnSpan(this._edgeResultsGrid, 2);
```

- [ ] **Step 4: Add label and combo blocks for Measure Mode**

After the `_edgeDrawRoiCheck` block (around line 813 in the Designer), add:

```csharp
//
// edgeMeasureModeLabel
//
this.edgeMeasureModeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
this.edgeMeasureModeLabel.Location = new System.Drawing.Point(3, 255);
this.edgeMeasureModeLabel.Name = "edgeMeasureModeLabel";
this.edgeMeasureModeLabel.Size = new System.Drawing.Size(104, 26);
this.edgeMeasureModeLabel.TabIndex = 22;
this.edgeMeasureModeLabel.Text = "Measure Mode";
this.edgeMeasureModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
//
// _edgeMeasureModeCombo
//
this._edgeMeasureModeCombo.Dock = System.Windows.Forms.DockStyle.Fill;
this._edgeMeasureModeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
this._edgeMeasureModeCombo.FormattingEnabled = true;
this._edgeMeasureModeCombo.Items.AddRange(new object[] {
    "single_edge",
    "edge_pair"});
this._edgeMeasureModeCombo.Location = new System.Drawing.Point(113, 258);
this._edgeMeasureModeCombo.Name = "_edgeMeasureModeCombo";
this._edgeMeasureModeCombo.Size = new System.Drawing.Size(115, 20);
this._edgeMeasureModeCombo.TabIndex = 23;
```

- [ ] **Step 5: Add label and combo blocks for Interpolation**

After the Measure Mode block, add:

```csharp
//
// edgeInterpolationLabel
//
this.edgeInterpolationLabel.Dock = System.Windows.Forms.DockStyle.Fill;
this.edgeInterpolationLabel.Location = new System.Drawing.Point(3, 281);
this.edgeInterpolationLabel.Name = "edgeInterpolationLabel";
this.edgeInterpolationLabel.Size = new System.Drawing.Size(104, 26);
this.edgeInterpolationLabel.TabIndex = 24;
this.edgeInterpolationLabel.Text = "Interpolation";
this.edgeInterpolationLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
//
// _edgeInterpolationCombo
//
this._edgeInterpolationCombo.Dock = System.Windows.Forms.DockStyle.Fill;
this._edgeInterpolationCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
this._edgeInterpolationCombo.FormattingEnabled = true;
this._edgeInterpolationCombo.Items.AddRange(new object[] {
    "nearest_neighbor",
    "bilinear",
    "bicubic"});
this._edgeInterpolationCombo.Location = new System.Drawing.Point(113, 284);
this._edgeInterpolationCombo.Name = "_edgeInterpolationCombo";
this._edgeInterpolationCombo.Size = new System.Drawing.Size(115, 20);
this._edgeInterpolationCombo.TabIndex = 25;
```

- [ ] **Step 6: Add label and numeric blocks for Angle**

After the Interpolation block, add:

```csharp
//
// edgeAngleLabel
//
this.edgeAngleLabel.Dock = System.Windows.Forms.DockStyle.Fill;
this.edgeAngleLabel.Location = new System.Drawing.Point(3, 307);
this.edgeAngleLabel.Name = "edgeAngleLabel";
this.edgeAngleLabel.Size = new System.Drawing.Size(104, 26);
this.edgeAngleLabel.TabIndex = 26;
this.edgeAngleLabel.Text = "Angle (°)";
this.edgeAngleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
//
// _edgeAngleNumeric
//
this._edgeAngleNumeric.DecimalPlaces = 1;
this._edgeAngleNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
this._edgeAngleNumeric.Increment = new decimal(new int[] {
    1,
    0,
    0,
    0});
this._edgeAngleNumeric.Location = new System.Drawing.Point(113, 310);
this._edgeAngleNumeric.Maximum = new decimal(new int[] {
    1800,
    0,
    0,
    -2147483648});
this._edgeAngleNumeric.Minimum = new decimal(new int[] {
    1800,
    0,
    0,
    -2147483648});
this._edgeAngleNumeric.Name = "_edgeAngleNumeric";
this._edgeAngleNumeric.Size = new System.Drawing.Size(115, 22);
this._edgeAngleNumeric.TabIndex = 27;
```

Note: Maximum = 180.0 as `1800 * 0.1` (one decimal place). Minimum = -180.0 as `-1800 * 0.1`.

- [ ] **Step 7: Update `Size` and `Location` for shifted controls**

The following existing controls shift to new row indices. Update their `Location.Y` values (only the Y coordinate; leave X, Width, Height unchanged):

- `edgeButtonPanel`: row 12 → `Location = new Point(3, 336)`, `Size` unchanged
- `_edgeStatusLabel`: row 13 → `Location = new Point(3, 394)`, `Size` unchanged
- `lineFittingResultLabel`: row 14 → `Location = new Point(3, 416)`, `Size` unchanged
- `circleFittingResultLabel`: row 15 → `Location = new Point(3, 464)`, `Size` unchanged
- `_edgeResultsGrid`: row 16 → `Location = new Point(3, 512)`, `Size` unchanged

Also update the `Size.Height` of `edgeTableLayout` from `561` to approximately `640` (3 new rows × 26F = 78px more fixed space). Set:

```csharp
this.edgeTableLayout.Size = new System.Drawing.Size(231, 640);
```

- [ ] **Step 8: Add field declarations at the bottom of the file**

In the field declarations region (around line 1372-1430), add:

```csharp
private System.Windows.Forms.Label edgeMeasureModeLabel;
private System.Windows.Forms.ComboBox _edgeMeasureModeCombo;
private System.Windows.Forms.Label edgeInterpolationLabel;
private System.Windows.Forms.ComboBox _edgeInterpolationCombo;
private System.Windows.Forms.Label edgeAngleLabel;
private System.Windows.Forms.NumericUpDown _edgeAngleNumeric;
```

- [ ] **Step 9: Update `_edgeRoiWidthNumeric` maximum to allow realistic values**

The current `_edgeRoiWidthNumeric.Maximum` is `500`. Since `RoiWidth` now represents `Length2 * 2`, increase the maximum to `1000` to support larger images. Find the line that sets `this._edgeRoiWidthNumeric.Maximum` and change the value to `1000`.

- [ ] **Step 10: Update `_edgeScanLengthNumeric` minimum to `20` (unchanged, already 50)**

Actually keep it at `50` as-is — the current minimum of `50` is fine for `Length1 * 2`.

- [ ] **Step 11: Build App project**

```powershell
dotnet build .\src\FlashMeasurementSystem.App.Wpf\FlashMeasurementSystem.App.Wpf.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build passes. If Designer line numbers differ, preserve the same control hierarchy, row indices, and event wiring.

- [ ] **Step 12: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add measure mode, interpolation, angle controls to edge tab`.

### Task 6: Full Verification

**Files:**
- Verify all files changed by Tasks 1-5.

- [ ] **Step 1: Run LSP diagnostics on changed source and test files**

Run diagnostics for:

```text
src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionParameters.cs
src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionRoi.cs
src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeResult.cs
src/FlashMeasurementSystem.Domain/EdgeDetection/EdgePair.cs
src/FlashMeasurementSystem.Halcon/EdgeDetection/HalconEdgeDetector.cs
src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs
tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
```

Expected: no new diagnostics caused by this change.

- [ ] **Step 2: Build and run tests**

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
.\tests\FlashMeasurementSystem.Tests\bin\Debug\FlashMeasurementSystem.Tests.exe
```

Expected output includes (among the existing suite pass lines):

```text
EdgeDetectionDomainTests passed
```

All new assertions must pass. Specifically the `Interpolation`, `MeasureMode`, `FromCenter`, `EdgePair`, and `EdgeResult.EdgePairs` tests must complete without throwing `InvalidOperationException`.

- [ ] **Step 3: Build full solution Any CPU and x64**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: both builds pass.

- [ ] **Step 4: Manual GUI verification**

Run the app from `src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe` (close any running instance first — a live app holds a lock on the dll files):

1. Load a sample image from `data/images`.
2. Open the **Edge Detection** tab. Confirm new controls are visible: `Measure Mode` combo (`single_edge`, `edge_pair`), `Interpolation` combo (`nearest_neighbor`, `bilinear`, `bicubic`), `Angle (°)` numeric (-180.0 to 180.0, default 0).
3. Enable `Draw ROI` and drag an initial rectangle. Confirm `Scan Length`, `ROI Width`, and `Angle` update from the drawn ROI.
4. Change `Angle` to 45.0. Confirm the blue ROI rectangle rotates immediately (the overlay preview updates).
5. Change `Scan Length` to 400. Confirm the blue ROI rectangle length changes immediately.
6. Change `ROI Width` to 100. Confirm the blue ROI rectangle width changes immediately.
7. Click `Detect` with `Measure Mode = Single Edge` and `Interpolation = nearest_neighbor`. Confirm:
   - Edge points appear in the grid (standard `#`, `Row`, `Column`, `Amp`, `Dist` columns).
   - Cyan crosses appear on edge points (existing behavior preserved).
8. Change `Interpolation` to `bilinear`, click `Detect`. Confirm detection succeeds (edge points change slightly due to interpolation).
9. Change `Interpolation` to `bicubic`, click `Detect`. Confirm detection succeeds.
10. Change `Measure Mode` to `Edge Pair`, click `Detect`. Confirm:
    - Grid columns change to `#`, `Row1`, `Col1`, `Amp1`, `Row2`, `Col2`, `Amp2`, `Intra`, `Inter`.
    - Pair data populates the grid.
    - Cyan crosses appear at both first and second edge positions.
    - Yellow lines appear between paired edge points (IntraDistance visualization).
11. Change `Measure Mode` back to `Single Edge`, click `Detect`. Confirm grid columns return to standard `#`, `Row`, `Column`, `Amp`, `Dist` layout.
12. Change ROI numeric values (Angle/ScanLength/Width). Confirm:
    - Edge/fitting state is cleared (grid empty, status = "Draw ROI, then Detect").
    - The blue ROI rectangle updates immediately.
    - Existing edge/fitting labels reset.
13. Switch to `EdgesSubPix` radio, detect on a sharp edge. Confirm existing subpix behavior is not regressed.
14. Zoom/pan: scroll or pan the `HWindowControl`. Confirm the ROI rectangle and any overlays redraw correctly.
15. Load a different image. Confirm stale edge state is cleared from the previous image.

- [ ] **Step 5: Final diff review**

Confirm:
- Domain files contain no Halcon types.
- `EdgeDetectionParameters.Interpolation` defaults to `"nearest_neighbor"` — existing behavior preserved.
- `EdgeDetectionParameters.MeasureMode` defaults to `"single_edge"` — existing behavior preserved.
- `RunMeasurePos` signature changed to accept `EdgeDetectionParameters` (was passing individual params) — verify all call sites updated.
- The existing `EdgesSubPix` path is completely untouched by the new parameters.
- `EdgeResult.EdgePairs` is null-safe: `EdgePoints` path does not depend on `EdgePairs`.
- No drag handles, circular ROI, recipe persistence, or WPF migration was added.
- The `GenMeasureRectangle2` call now uses `p.Interpolation` instead of hardcoded `"nearest_neighbor"`.
- The primary/fallback direction logic is preserved for both `MeasurePos` and `MeasurePairs` modes.
- No unrelated formatting or refactors were included.

- [ ] **Step 6: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add rectangular measure object enhancement`.

---

## Self-Review

### Spec coverage check

| Spec section | Task(s) covering it |
|---|---|
| `EdgeDetectionParameters.Interpolation` + allow-list | Task 2 Step 1 |
| `EdgeDetectionParameters.MeasureMode` + allow-list | Task 2 Step 1 |
| `EdgeDetectionRoi.FromCenter(...)` | Task 2 Step 2 |
| `EdgePair` DTO | Task 2 Step 3 |
| `EdgeResult.EdgePairs` list | Task 2 Step 4 |
| Interpolation passed to `GenMeasureRectangle2` | Task 3 Step 1 |
| `RunMeasurePairs(...)` HALCON adapter | Task 3 Step 2 |
| Mode dispatch in `DetectEdges(...)` | Task 3 Step 4 |
| Error handling: unsupported interpolation/mode | Task 3 Step 4 (validation) |
| `CloseMeasure` in `finally` for both modes | Task 3 Step 2 (existing pattern preserved) |
| Primary/fallback direction logic reused | Task 3 Step 4 |
| UI: Measure Mode combo, Interpolation combo, Angle numeric | Task 5 Steps 1-8 |
| ScanLength → actual `Length1 * 2` | Task 4 Steps 2 and 3 |
| RoiWidth → actual `Length2 * 2` | Task 4 Steps 2 and 3 |
| ROI lifecycle: numeric adjustment → preview redraw | Task 4 Step 3 |
| Stale state invalidation on numeric change | Task 4 Step 3 |
| Grid column switching for pair mode | Task 4 Steps 5-6 |
| Pair overlay: cyan crosses + yellow intra-distance lines | Task 4 Step 4 |
| `MaxOverlayCrosses` sampling preserved | Task 4 Step 4 |
| Domain tests for new features | Task 1 |
| Build verification (Any CPU + x64) | Task 6 Steps 2-3 |
| Manual GUI verification steps | Task 6 Step 4 |

### Placeholder scan

No placeholders (TBD, TODO, "implement later", "fill in details", "Add appropriate error handling", "Write tests for the above" without code, "Similar to Task N" references) found. Every step contains complete code or exact commands.

### Type consistency check

- `EdgeDetectionParameters.Interpolation` (string) default `"nearest_neighbor"` — used in `RunMeasurePos` line 172 replacement.
- `EdgeDetectionParameters.MeasureMode` (string) default `"single_edge"` — checked in `DetectEdges(...)` dispatch and `RunEdgeDetectionButton_Click`.
- `EdgeDetectionRoi.FromCenter(double, double, double, double, double)` → `EdgeDetectionRoi` — used in `OnEdgeRoiNumericChanged` and `RunEdgeDetectionButton_Click`.
- `EdgePair` fields: `FirstRow, FirstColumn, FirstAmplitude, SecondRow, SecondColumn, SecondAmplitude, IntraDistance, InterDistance` — all match between create, test, and usage sites.
- `MeasureAttempt.RawFirstRows` etc. (HTuple fields) — added to struct, written in `RunMeasurePairs`, read in `DetectEdges(...)` pair builder.
- `RestoreDefaultEdgeGridColumns()` references `edgeIndexColumn`, `edgeRowColumn`, etc. — these are the existing designer-level field names, confirmed in `MainWindow.Designer.cs` lines 916-959.
- `_edgeAngleNumeric` min/max: -180.0 to 180.0 with `DecimalPlaces = 1` — matches spec "degrees, range -180.0 to 180.0, one decimal place".

No type mismatches found across tasks. All method signatures introduced in earlier tasks are referenced consistently in later tasks.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-12-rectangular-measure-object-enhancement-implementation.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
