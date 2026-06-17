# Rectangular Measure Object Enhancement Design

**Goal:** Strengthen the existing rectangular HALCON 1D measure object workflow without changing the image display control. Keep the current `HWindowControl` display path, preserve existing `MeasurePos` behavior, add interpolation and `MeasurePairs`, and make rectangular ROI geometry adjustable through numeric UI controls with immediate preview.

**Selected approach:** Phase 1 uses numeric ROI control plus live preview. The user still draws an initial ROI with the mouse, but the right-side controls become the source of truth for `Angle`, `ScanLength`, `RoiWidth`, `Interpolation`, and `Measure Mode`. Drag handles, rotation handles, circular ROI, and multi-ROI editing remain out of scope.

## Context

The project already displays images through HALCON, not through a C# `PictureBox` or `System.Drawing.Image` display path:

- `MainWindow.Designer.cs` creates `HalconDotNet.HWindowControl`.
- `MainWindow.LoadAndDisplayImage()` loads images as `HImage`.
- `HWindowControlHelper.Redraw()` calls `HOperatorSet.DispObj(CurrentImage, _window)`.

The existing edge detector already uses the HALCON 1D measure object shown in the HALCON documentation for rectangular ROIs:

```csharp
HOperatorSet.GenMeasureRectangle2(..., "nearest_neighbor", out measureHandle);
HOperatorSet.MeasurePos(...);
```

The current implementation is limited in four ways:

1. `Interpolation` is fixed to `nearest_neighbor`.
2. Only `measure_pos` is exposed; `measure_pairs` is not available.
3. `ScanLength` and `RoiWidth` are UI hints only. They are updated after drawing an ROI but do not drive the actual detect ROI.
4. The UI draws a new axis-aligned ROI on every left-button drag. There is no rotate/edit mode and no dynamic length/width update after the ROI is created.

HALCON 17.12 reference details verified from `halcon_pdf/reference/reference_hdevelop.txt`:

- `gen_measure_rectangle2` accepts `Row, Column, Phi, Length1, Length2, Width, Height, Interpolation` and outputs a measure handle.
- `Interpolation` values include `nearest_neighbor`, `bilinear`, and `bicubic`.
- `measure_pos` extracts straight edges perpendicular to the major axis of a rectangle or annular arc.
- `measure_pairs` extracts edge pairs perpendicular to a rectangle or annular arc and returns `IntraDistance` and `InterDistance`.
- `measure_pos` and `measure_pairs` require a single-channel image.
- `measure_pos.Distance` has one fewer element than `RowEdge` / `ColumnEdge`.

## Architecture

Keep the existing dependency direction:

```text
Domain <- Application <- Halcon <- App.Wpf
```

- **Domain** owns ROI geometry, edge parameters, and result DTOs. No HALCON or UI references.
- **Application** keeps the existing `IEdgeDetector<TImage>` interface.
- **Halcon** expands the adapter from `measure_pos` only to `measure_pos` plus `measure_pairs`.
- **App.Wpf** adds UI controls and preview behavior around the existing `HWindowControlHelper` and `OverlayAnnotator`.

No display-control migration, WPF/XAML migration, circular ROI, or recipe persistence in this phase.

## Files

**Modify:**

- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionParameters.cs`
- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionRoi.cs`
- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeResult.cs`
- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgePoint.cs`
- `src/FlashMeasurementSystem.Halcon/EdgeDetection/HalconEdgeDetector.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`
- `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`

**Create:**

- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgePair.cs`

Add an explicit `<Compile Include="EdgeDetection\EdgePair.cs" />` entry to `FlashMeasurementSystem.Domain.csproj` because this project uses old-style csproj files.

## Domain Layer

### `EdgeDetectionParameters`

Add:

- `Interpolation` (string, default `"nearest_neighbor"`) — allowed: `nearest_neighbor`, `bilinear`, `bicubic`.
- `MeasureMode` (string, default `"single_edge"`) — allowed: `single_edge`, `edge_pair`.

Keep existing fields:

- `Sigma`
- `Threshold`
- `Polarity`
- `EdgeSelector`
- `HighThreshold`

Add allow-list helpers:

- `IsSupportedInterpolation(string value)`
- `IsSupportedMeasureMode(string value)`

`MeasureMode` applies only to the `MeasurePos` radio-button path. `EdgesSubPix` ignores it.

### `EdgeDetectionRoi`

Keep the existing geometry fields:

- `CenterRow`
- `CenterCol`
- `Length1`
- `Length2`
- `AngleRad`

Add a factory for the numeric-control path:

```csharp
public static EdgeDetectionRoi FromCenter(
    double centerRow,
    double centerCol,
    double length1,
    double length2,
    double angleRad)
```

`FromBounds(...)` remains for initial mouse drawing and keeps the current direction heuristic. After the initial draw, the UI can rebuild the final ROI with `FromCenter(...)` using numeric values.

### `EdgePoint` / `EdgeResult` / `EdgePair`

Keep `EdgePoint` for `measure_pos` results.

For `measure_pairs`, prefer a dedicated `EdgePair` DTO:

- `FirstRow`
- `FirstColumn`
- `FirstAmplitude`
- `SecondRow`
- `SecondColumn`
- `SecondAmplitude`
- `IntraDistance`
- `InterDistance`

Add to `EdgeResult`:

- `List<EdgePair> EdgePairs`

`EdgePoints` remains populated for `measure_pos`. For `measure_pairs`, the adapter may also flatten first/second points into `EdgePoints` for overlay compatibility, but pair-specific distances must live in `EdgePairs` so the UI does not misinterpret them as single-edge distances.

## Halcon Layer

`HalconEdgeDetector.DetectEdges(...)` keeps its existing validation, clamping, single-channel conversion, and primary/fallback direction behavior.

### MeasurePos path

Retain `RunMeasurePos(...)`, but pass `p.Interpolation` into `GenMeasureRectangle2` instead of the hard-coded `"nearest_neighbor"`.

### MeasurePairs path

Add `RunMeasurePairs(...)` using the same measure object construction:

```csharp
HOperatorSet.GenMeasureRectangle2(
    centerRow, centerCol, phi, length1, length2,
    width, height, p.Interpolation, out measureHandle);

HOperatorSet.MeasurePairs(
    image, measureHandle,
    new HTuple(p.Sigma), new HTuple(p.Threshold),
    new HTuple(p.Polarity), new HTuple(p.EdgeSelector),
    out rowFirst, out colFirst, out ampFirst,
    out rowSecond, out colSecond, out ampSecond,
    out intraDistance, out interDistance);
```

The same fallback rule applies: if the primary attempt has no exception and returns zero pairs, try `Phi + π/2` with swapped `Length1` / `Length2`.

### Error handling

- Unsupported `Interpolation`: fail with a clear `ErrorMessage` before calling HALCON.
- Unsupported `MeasureMode`: fail with a clear `ErrorMessage` before calling HALCON.
- HALCON exceptions are captured in the result, matching existing `MeasurePos` behavior.
- Single-channel conversion remains mandatory.
- `CloseMeasure` must run in `finally` for both modes.

## Main Window UI

### Controls

In the Edge Detection tab, add:

- `Measure Mode` combo: `Single Edge`, `Edge Pair`.
- `Interpolation` combo: `nearest_neighbor`, `bilinear`, `bicubic`.
- `Angle` numeric: degrees, default derived from current ROI. Range `-180.0` to `180.0`, one decimal place.

Change semantics of existing controls:

- `ScanLength` becomes actual `Length1 * 2` used by detection.
- `RoiWidth` becomes actual `Length2 * 2` used by detection.

The UI should make it clear that these controls apply to `MeasurePos` / `MeasurePairs`; `EdgesSubPix` keeps its existing behavior.

### ROI lifecycle

Initial draw:

1. User enables Draw ROI.
2. User drags an axis-aligned rectangle on the `HWindowControl`.
3. `EdgeDetectionRoi.FromBounds(...)` creates the initial ROI using the existing heuristic.
4. UI fields update from that ROI:
   - `AngleDeg = AngleRad * 180 / π`
   - `ScanLength = Length1 * 2`
   - `RoiWidth = Length2 * 2`
5. Preview redraws as a blue `DispRectangle2` ROI.

Numeric adjustment:

1. User changes `Angle`, `ScanLength`, or `RoiWidth`.
2. `MainWindow` rebuilds the ROI from the current center and numeric values.
3. The preview redraws immediately.
4. Existing edge/fitting state is invalidated because the detect ROI changed.

Detect:

1. `CreateEdgeDetectionRoi()` returns the current numeric ROI, not `FromBounds(...)` directly.
2. `CreateEdgeDetectionParameters()` includes `Interpolation` and `MeasureMode`.
3. `HalconEdgeDetector` dispatches to `measure_pos` or `measure_pairs`.

### Preview behavior

Use the existing overlay mechanism:

```csharp
_imageHelper.SetPersistentOverlayAction(() => DrawFittingLayers(_imageHelper.Annotator));
```

`DrawFittingLayers(...)` already draws the current `_latestEdgeRoi` using `DrawRectangle2(...)`, so the preview should set/update `_latestEdgeRoi` when ROI numeric controls change.

When numeric ROI changes, clear stale edge/fitting results:

- `_latestEdgeResult = null`
- `_latestLineFittingResult = null`
- `_latestCircleFittingResult = null`
- edge result grid cleared
- status reset to `Draw ROI, then Detect`

## Result Display

For `Single Edge` mode:

- Keep current grid columns and behavior.

For `Edge Pair` mode:

- Either add columns dynamically or add fixed columns that can display pair rows:
  - `#`
  - `Row1`
  - `Col1`
  - `Amp1`
  - `Row2`
  - `Col2`
  - `Amp2`
  - `IntraDist`
  - `InterDist`

Preferred phase-1 implementation: reuse the existing grid by clearing/rebuilding columns when binding results. This keeps pair semantics visible and avoids overloading the old `Distance` column.

Overlay:

- Draw both points of each pair as cyan crosses.
- Draw a short cyan or yellow line between first and second edge point for `IntraDistance` visualization.
- Keep `MaxOverlayCrosses` / sampling behavior so dense results do not freeze the UI.

## Approaches Considered

### Option A — Numeric controls only

Draw once, then adjust `Angle`, `ScanLength`, and `RoiWidth` numerically. This is low risk and maps cleanly to HALCON `gen_measure_rectangle2`.

### Option B — Drag handles and rotation handle

Add interactive center dragging, edge resizing, corner resizing, and rotation handles. This has the best operator experience but requires a new hit-testing state machine in `HWindowControlHelper`, careful interaction with zoom/pan, and more manual QA.

### Option C — Hybrid phased approach

Do Option A now, keep the data model compatible with Option B later. This solves the immediate measurement issue while avoiding high-risk UI state-machine work.

**Decision:** Option C, with Option A behavior implemented in this phase.

## Out Of Scope

- Circular / arc ROI (`gen_measure_arc`).
- Drag handles, rotation handles, or CAD-like interactive ROI editing.
- Multi-ROI management.
- Recipe save/load of ROI parameters.
- Measurement workflow state-machine integration.
- WPF/XAML migration.
- Replacing `HWindowControl` or `HWindowControlHelper`.
- HDevelop modal `draw_*` APIs.

## Verification

### Console/domain verification

Update `EdgeDetectionDomainTests` to cover:

- default `Interpolation == "nearest_neighbor"`
- default `MeasureMode == "single_edge"`
- supported interpolation allow-list
- supported measure mode allow-list
- `EdgeDetectionRoi.FromCenter(...)` preserves center, lengths, and angle

### Build

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

### Manual GUI verification

1. Start the app from `src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`.
2. Load a sample image.
3. Enable Draw ROI and drag an initial ROI.
4. Confirm `Angle`, `ScanLength`, and `RoiWidth` update from the initial ROI.
5. Change `Angle`; confirm the blue ROI rotates immediately.
6. Change `ScanLength`; confirm the blue ROI length changes immediately.
7. Change `RoiWidth`; confirm the blue ROI width changes immediately.
8. Run `Single Edge` with each interpolation option and confirm `MeasurePos` still works.
9. Run `Edge Pair` and confirm paired results and distances appear in the grid.
10. Confirm changing ROI geometry clears stale edge/fitting state.
11. Switch to `EdgesSubPix`; confirm existing behavior is not regressed.
12. Confirm zoom/pan still redraws the current ROI and overlays correctly.

## Risks

- **UI layout risk:** `edgeTableLayout` is dense and row-index based. New controls must be inserted carefully so the trailing percent grid row remains the flexible row.
- **Stale state risk:** Numeric ROI changes must invalidate edge, line, circle, distance, and angle overlays that depend on old edge data.
- **Tuple mapping risk:** `measure_pairs` returns several tuples with different semantics. Do not flatten them into `EdgePoint.Distance` without preserving pair identity.
- **Performance risk:** `bicubic` interpolation and dense pair results may be slower. Keep `nearest_neighbor` as default and preserve overlay sampling caps.
- **Direction risk:** The existing primary/fallback direction logic should be reused. Removing it would regress cases where ROI shape alone does not identify the intended scan direction.
- **Compatibility risk:** HALCON calls must be verified under x64.

