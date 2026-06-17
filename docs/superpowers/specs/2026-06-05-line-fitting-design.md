# Line Fitting Design

**Goal:** Adapt manual section 4.4 Line Fitting into the project layering and add a manually testable WinForms GUI action that fits a line from the current Edge Detection result.

**Selected approach:** Scheme A - reuse the existing Edge Detection tab. The user first runs Edge Detection, then clicks Fit Line to fit the detected edge points and draw the fitted line overlay.

## Context

Manual section 4.4 defines Line Fitting as the step after edge detection:

- Input: edge points from the previous edge detection step.
- Output: a fitted line segment with start/end row/column, angle, length, residual RMS, used point count, and error message.
- Typical use: workpiece straight edges, slot edges, step edges.

The project already has section 4.3 Edge Detection implemented across the expected layers and exposed in the existing GUI. This design intentionally reuses that result instead of creating a separate image/ROI pipeline.

Official HALCON 17.12 documentation (reference L175794-797, L175851-875) confirms one detail from the manual sample: `fit_line_contour_xld` supports `regression`, `gauss`, `huber`, `tukey`, and `drop`. It does not document `least_squares` or `ransac` as valid values for this operator. The default algorithm should therefore be `tukey` (matches HALCON's own default, robust against outliers — important because edge detection results often contain outliers from neighbouring edges or noise), with `regression` available when pure least-squares behavior is desired.

The operator's positional parameter order is **`Algorithm, MaxNumPoints, ClippingEndPoints, Iterations, ClippingFactor`** — `Iterations` comes **before** `ClippingFactor`. Swapping them does not raise an exception (both are numeric) but silently produces wrong fit results. The HALCON 17.12 default for `Iterations` is 5; this design chooses 3 to favor faster convergence on small edge counts, with the trade-off that very noisy edge sets may need the user to raise it.

Reference L175845 specifies a hard minimum of `2 + 2 * ClippingEndPoints` contour points. The adapter must enforce `max(MinPoints, 2 + 2 * ClippingEndPoints)`, not just `MinPoints`.

References:

- MVTec HALCON 17.12 `fit_line_contour_xld`: https://www.mvtec.com/doc/halcon/1712/en/fit_line_contour_xld.html
- MVTec HALCON 23.11 `fit_line_contour_xld`: https://www.mvtec.com/doc/halcon/2311/en/fit_line_contour_xld.html
- MVTec HALCON `gen_contour_polygon_xld`: https://www.mvtec.com/doc/halcon/2511/en/gen_contour_polygon_xld.html
- MVTec HALCON `distance_pl`: https://www.mvtec.com/doc/halcon/2311/en/distance_pl.html

## Architecture

Follow the same dependency direction as Edge Detection, Image Quality, and Template Matching:

```text
Domain <- Application <- Halcon <- App.Wpf
```

- **Domain** - plain parameter/result models. No Halcon, UI, file-system, or hardware dependencies.
- **Application** - line fitting interface expressed in terms of Domain types.
- **Halcon** - concrete adapter that converts edge points into an XLD contour and calls HALCON.
- **App.Wpf** - transitional WinForms test harness, using the existing Edge Detection tab and `HWindowControl` overlay.

## Files

**Create:**

- `src/FlashMeasurementSystem.Domain/LineFitting/LineFittingParameters.cs`
- `src/FlashMeasurementSystem.Domain/LineFitting/LineFittingResult.cs`
- `src/FlashMeasurementSystem.Application/LineFitting/ILineFitter.cs`
- `src/FlashMeasurementSystem.Halcon/LineFitting/HalconLineFitter.cs`
- `tests/FlashMeasurementSystem.Tests/LineFittingDomainTests.cs`

**Modify:**

- `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`
- `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`
- `src/FlashMeasurementSystem.App.Wpf/FlashMeasurementSystem.App.Wpf.csproj` only if new helper files are added
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`
- `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs` — add a `DrawLine(row1, col1, row2, col2, color)` helper (existing draw helpers cover Cross / Rectangle2 / RoiRectangle / Text / MatchResult / MatchContour but not a plain line segment)

No WPF/XAML migration.

## Domain Layer

`LineFittingParameters`:

- `Algorithm` (string, default `"tukey"`) - valid values: `regression`, `gauss`, `huber`, `tukey`, `drop`.
- `MaxNumPoints` (int, default `-1`) - `-1` means use all points.
- `ClippingEndPoints` (int, default `0`) - number of contour points ignored at each end for fitting.
- `ClippingFactor` (double, default `2.0`) - robust clipping factor, especially for `tukey`.
- `Iterations` (int, default `3`) - ignored by `regression`, used by robust methods. HALCON's own default is 5; 3 is chosen here as a faster convergence default for typical edge sets — raise it explicitly if the fit looks unstable on very noisy data.
- `MinPoints` (int, default `2`) - **user-defined floor** for point count before calling HALCON. The adapter enforces the effective minimum as `max(MinPoints, 2 + 2 * ClippingEndPoints)` per reference L175845 — increasing `ClippingEndPoints` raises the real requirement.

`LineFittingResult`:

- `Success` (bool)
- `Row1` / `Column1` (double)
- `Row2` / `Column2` (double)
- `AngleDeg` (double)
- `Length` (double)
- `ResidualRms` (double)
- `UsedPoints` (int)
- `ErrorMessage` (string)

No Halcon types should appear in Domain models.

## Application Layer

`ILineFitter`:

```csharp
LineFittingResult FitLine(IList<EdgePoint> edgePoints, LineFittingParameters parameters);
```

The interface depends only on `FlashMeasurementSystem.Domain.EdgeDetection` and `FlashMeasurementSystem.Domain.LineFitting`.

## Halcon Layer

`HalconLineFitter : ILineFitter`:

1. Validate edge point count against `MinPoints`.
2. Convert `EdgePoint.Row` and `EdgePoint.Column` into row/column arrays in that exact order.
3. Create an XLD contour via `GenContourPolygonXld`.
4. Call `HOperatorSet.FitLineContourXld` with the selected algorithm and parameters.
5. Map fitted start/end points into `LineFittingResult`.
6. Compute `AngleDeg` using `Math.Atan2(deltaRow, deltaColumn)`.
7. Compute `Length` from the fitted segment endpoints.
8. Compute `ResidualRms` from point-to-line distances. Prefer `DistancePl` or equivalent point-to-line distance over `DistancePc`, because `DistancePc` measures distance to contour segments, not the fitted infinite line.
9. Convert `HalconException` into a failed `LineFittingResult` with a useful `ErrorMessage`.

The implementation should not add unsupported algorithms such as `ransac` unless a later task uses a different HALCON operator or custom algorithm.

## Main Window Test UI

Add the test action to the existing Edge Detection tab.

### Layout

- Add a small Line Fitting section near the current edge detection result controls.
- Add a `Fit Line` button.
- Add read-only result labels/text for:
  - success/failure
  - start/end row/column
  - angle in degrees
  - length in pixels
  - residual RMS
  - used point count
- Keep the UI compact to avoid large designer refactors.

### Behavior

1. User loads an image.
2. User draws or uses the existing Edge Detection ROI.
3. User clicks Detect to populate edge points.
4. User clicks Fit Line.
5. GUI calls `HalconLineFitter.FitLine` with the latest edge points and default parameters.
6. On success, draw the fitted line overlay on `HWindowControl` and display numeric results.
7. On failure, show `ErrorMessage` in the Line Fitting result area.

The line fitting action should not silently rerun Edge Detection. Keeping the two actions separate makes failures easier to diagnose.

## Error Handling

- No image loaded: show `請先載入影像`.
- No Edge Detection result: show `請先執行邊緣檢測`.
- Insufficient edge points: show point count and the required minimum.
- Unsupported algorithm value: fail fast in the adapter result with a clear message.
- HALCON failure: show the HALCON message in `LineFittingResult.ErrorMessage`.
- Overlay drawing failure should not corrupt the fitted numerical result; report drawing failure separately in the GUI status text.

## Known Limitations / Caveats

The 2026-06-05 EdgeDetection work surfaced several scenarios where `fit_line_contour_xld` will produce technically correct but semantically meaningless results. These are not bugs — they follow from `fit_line_contour_xld` assuming **all input points belong to one physical line**:

- **ROI 跨越多條物理邊**：if Edge Detection returned points from both the top and bottom of a rectangle, fit_line will produce a single "average" line between them. The RMS will be large; the UI should treat large RMS as a hint that the input set is mixed.
- **斜邊上的 multi-sample**：measure_pos against an edge that is not perpendicular to its major axis produces multiple closely-spaced edge samples for the same physical line (we observed 5 samples on a 15° rotated square). fit_line will fit them correctly, but the input set looks suspiciously "noisy" relative to a single transition.
- **Subpix 模式可能回上百~上千控制點**：fitting performance scales linearly with point count and HALCON's `gen_contour_polygon_xld` will accept all of them; the resulting line is fine. The GUI grid/overlay is capped (see EdgeDetection fixes A2/A3), but the underlying `EdgeResult.EdgePoints` is the full list.

The Fit Line action does not attempt to detect or reject these scenarios automatically. Quality assessment is left to the user via the RMS/UsedPoints fields and overlay visual inspection.

## Stale State

The GUI stores `_latestEdgeRoi` / `_latestEdgeResult` between Detect and Fit Line. These must be invalidated when the user does anything that changes the underlying image or ROI — otherwise Fit Line will draw on the wrong image:

- Loading a new image
- Clearing the ROI
- (For future) switching tabs that change the displayed image

The plan covers `LoadAndDisplayImage` and `ClearEdgeDetectionButton_Click` invalidation explicitly.

## Out Of Scope

- Multi-line fitting.
- Circle fitting or other geometry fitting sections.
- Automatic Edge Detection rerun inside Fit Line.
- Recipe save/load.
- MeasurementWorkflow integration.
- WPF/XAML migration.
- New test framework setup.
- Custom RANSAC implementation.

## Verification

**Build:**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

**Tests:**

- Run the existing console-style test project after adding `LineFittingDomainTests.cs`.
- Keep tests focused on Domain defaults and interface compile contracts unless HALCON integration testing is explicitly added.

**Manual:**

1. Start the app.
2. Select an image from `data/images`.
3. Draw an ROI in the Edge Detection tab.
4. Click Detect and verify edge points appear.
5. Click Fit Line.
6. Confirm a fitted line overlay appears.
7. Confirm angle, length, RMS, and point count are displayed.
8. Clear overlays and repeat with different edge detection parameters.
9. Verify insufficient-points and no-edge-result messages.
