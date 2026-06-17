# Edge Detection Test UI Design

**Goal:** Add a manually testable Edge Detection feature following the manual's section 4.3 and the project's layer boundaries, with a WinForms test harness in the existing main window.

**Approved approach:** Scheme 1 - Single-ROI Quick Test with right-side parameter panel, profile chart, and edge results table. Uses `HWindowControl` for image display and ROI interaction.

## Context

Manual §4.3 defines two edge detection methods:
- `MeasurePos` — 1D edge detection along a rectangular ROI, returns edge positions with subpixel accuracy.
- `EdgesSubPix("canny")` — 2D Canny edge detection + subpixel refinement, returns `HXLDCont` contours.

The measurement flow places `MEASURING` right after `MATCHING_TEMPLATE`:
```text
MATCHING_TEMPLATE -> MEASURING -> EVALUATING
```

The existing main window (`MainWindow : Form`) already has:
- An image dropdown from `data/images` (from Image Quality Check).
- An image quality test area.
- A template matching test area.

This design adds an Edge Detection test area, reusing the image dropdown and adding an `HWindowControl`.

## Architecture

Same layering pattern as Image Quality Check and Template Matching:

```text
Domain <- Application <- Halcon <- App.Wpf
```

- **Domain** — plain result, parameter, and ROI models. No Halcon, UI, or file-system dependencies.
- **Application** — generic interface that avoids `HalconDotNet` references.
- **Halcon** — manual section 4.3 algorithm implementations (`MeasurePos` + `EdgesSubPix`).
- **App.Wpf** — WinForms test harness for edge detection with ROI interaction.

## Files

**Create:**
- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeResult.cs`
- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgePoint.cs`
- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionParameters.cs`
- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionRoi.cs`
- `src/FlashMeasurementSystem.Application/EdgeDetection/IEdgeDetector.cs`
- `src/FlashMeasurementSystem.Halcon/EdgeDetection/HalconEdgeDetector.cs`

**Modify:**
- `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`

No WPF/XAML migration.

## Domain Layer

`EdgeResult` (from manual section 4.3):
- `Success` (bool)
- `EdgePoints` (List<EdgePoint>)
- `ErrorMessage` (string)

`EdgePoint` (from manual section 4.3):
- `Row` (double)
- `Column` (double)
- `Amplitude` (double)
- `Distance` (double)

`EdgeDetectionParameters`:
- `Algorithm` (string, default "measure_pos") — "measure_pos" | "edges_sub_pix"
- `Sigma` (double, default 1.2, range 0.5-3.0)
- `Threshold` (double, default 25, range 5-80)
- `Polarity` (string, default "all") — "all" | "positive" | "negative"
- `EdgeSelector` (string, default "all") — "all" | "first" | "last"
- `SubpixelMethod` (string, default "parabolic") — "parabolic" | "gaussian" | "none"
- `HighThreshold` (double, default 40) — for EdgesSubPix
- `ScanLength` (double, default 500) — length1 * 2
- `RoiWidth` (double, default 100) — length2 * 2

`EdgeDetectionRoi`:
- `CenterRow` (double)
- `CenterCol` (double)
- `Length1` (double) — semi-length (search direction)
- `Length2` (double) — semi-width (perpendicular)
- `AngleRad` (double) — rotation angle
- `IsDefined` (bool, computed) — Length1 > 0 && Length2 > 0

No Halcon or UI dependencies in these types.

## Application Layer

`IEdgeDetector` with generic type parameters `TImage`:
- `EdgeResult DetectEdges(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters)`

Generic to avoid `HalconDotNet` reference in Application.

## Halcon Layer

`HalconEdgeDetector : IEdgeDetector<HImage>`:
- `DetectEdges` dispatches based on `parameters.Algorithm`:
  - `"measure_pos"` — `GenMeasureRectangle2` + `MeasurePos` + `CloseMeasure`
  - `"edges_sub_pix"` — `ReduceDomain` + `EdgesSubPix("canny", ...)`
- Wraps `HalconException` into `InvalidOperationException`.
- Converts Halcon `HTuple` edge data into `List<EdgePoint>`.
- For EdgesSubPix, ROI rectangle converted to `HRegion` via `GenRectangle2`.

## Main Window Test UI

Added as a new GroupBox section in the existing `MainWindow : Form`.

### Layout

**Left (65%):** `HWindowControl` for image display and ROI interaction.
- Loads the selected image from the shared image dropdown.
- Mouse-driven ROI drawing (click-drag to define a GenMeasureRectangle2 ROI).
- Overlay layers: ROI rectangle (blue dashed) + search direction arrow (blue) + edge points (cyan crosses) + fitted line/contour (green).

**Right (35%):** Parameter panel + results.
- Algorithm selector: RadioButtons (MeasurePos / EdgesSubPix).
- NumericUpDown: Sigma, Threshold, ROI Width, Scan Length.
- ComboBox: Polarity, Edge Selector, Subpixel Method.
- [Detect] and [Clear] buttons.
- Compact profile chart (gray-value profile + gradient + edge markers).
- DataGridView: Row, Column, Amplitude, Distance.

**Bottom:** Status label showing edge count and PASS/FAIL.

### ROI Interaction

- **Draw:** MouseDown → start point. MouseMove → dynamic rectangle preview. MouseUp → ROI fixed.
- Drag direction determines `AngleRad` (search direction along rectangle long axis).
- **Move:** Click inside ROI → drag to translate.
- **Resize:** Drag ROI edge → adjust Length1.
- **Width:** ROI Width NumericUpDown adjusts Length2.
- Visual: blue dashed rectangle + direction arrow. After detect: cyan crosses + green fit.

### Edge Profile Chart

Simple `System.Drawing` chart in a Panel:
- X: pixel distance along ROI search direction.
- Y: gray value (0-255).
- Series: smoothed profile (white), gradient (yellow dashed), detected edges (cyan vertical dashes).
- Data source: Halcon `measure_projection` output.

### Startup Behavior

1. Existing Halcon smoke check.
2. Existing image list load.
3. Existing template list scan.
4. New: initialize HWindowControl with mouse event handlers.
5. Set default parameters from manual defaults table.
Window height increased (~720 to ~900).

## Error Handling

- No image loaded: Detect disabled, tooltip "请先载入影像".
- No ROI defined: Detect disabled, tooltip "请先在影像上绘制 ROI".
- Halcon failure: show error in status label.
- No edges found: FAIL with parameter hints (threshold, sigma).
- HWindowControl properly released on form close.

## Out Of Scope

- Multi-ROI management.
- Step-based pipeline.
- Recipe save/load.
- WPF/XAML migration.
- Test framework setup.
- Integration into MeasurementWorkflow.

## Verification

**Build:**
```powershell
dotnet build FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

**Manual:**
1. Start the app, select an image from dropdown.
2. Draw ROI on HWindowControl by click-dragging.
3. Adjust parameters (Sigma, Threshold, Polarity).
4. Click Detect — confirm cyan edge crosses on image, DataGridView populates, status shows PASS/FAIL.
5. Switch to EdgesSubPix, repeat — confirm green contour XLD displayed.
6. Clear removes overlay.
7. Verify with sample images from data/images.
