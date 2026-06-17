# Circle Fitting Design

**Goal:** Adapt manual section 4.5 Circle Fitting into the project layering and add a manually testable WinForms GUI action that fits a circle from the current Edge Detection result.

**Selected approach:** Reuse the existing Edge Detection tab. The user first runs Edge Detection, then clicks Fit Circle to fit the detected edge points and draw the fitted circle overlay.

## Context

Manual section 4.5 defines Circle Fitting as the step after edge detection:

- Input: edge points from the previous edge detection step.
- Output: a fitted circle with center row/column, radius, diameter, residual RMS, roundness, used point count, and error message.
- Typical use: outer diameter, inner holes, screw hole positions, arcs, and circular workpiece features.

The project already has section 4.4 Line Fitting implemented across the expected layers and exposed in the existing GUI. Circle Fitting should follow that pattern instead of introducing a new workflow or a new UI model.

Official HALCON 17.12 documentation (reference L175468-472, verified against the offline
`reference_hdevelop.txt`) confirms that the standard operator for fitting a circle to XLD
contours is `fit_circle_contour_xld` / `HOperatorSet.FitCircleContourXld`. Its parameter order is:

```text
Contours, Algorithm, MaxNumPoints, MaxClosureDist, ClippingEndPoints, Iterations, ClippingFactor,
Row, Column, Radius, StartPhi, EndPhi, PointOrder
```

Note this is **not** the same shape as `fit_line_contour_xld` — circle inserts `MaxClosureDist`
at position 3, between `MaxNumPoints` and `ClippingEndPoints`. The adapter must follow the circle
order exactly; reusing the line operator's order silently produces wrong fits (no exception).

This differs from the simplified manual sample. The manual also mentions `GenMeasureCircle` / `MeasureCircle`, but HALCON 17.12 official documentation uses `gen_measure_arc` with `measure_pos` / `measure_pairs`, or the higher-level 2D Metrology API for known-circle measurement. Those are useful future extensions, but they are not part of this first Circle Fitting GUI action.

Key parameter facts from the reference that the adapter relies on:

- **Algorithm default**: HALCON's own default is `algebraic` (reference L175537). This design instead defaults to `geotukey` — `geometric` is statistically optimal for noise-distorted contours (L175490-492) and the Tukey variant ignores outliers (L175497-498), which matches edge-detection output that frequently contains stray points. Pure least-squares behavior is available via `algebraic`.
- **Minimum point count**: reference L175525-526 requires at least `3 + 2 * ClippingEndPoints` contour points. The adapter must enforce `max(MinPoints, 3 + 2 * ClippingEndPoints)`, not just `MinPoints` — otherwise a non-zero `ClippingEndPoints` passes validation but throws inside HALCON.
- **Iterations**: HALCON default is 3 (L175553), which this design matches. Iterations is **ignored** for the geometric algorithms (`geometric`, `geohuber`, `geotukey`) per L175508-509 — so with the default `geotukey` algorithm it has no effect; it only matters if the user switches to an algebraic algorithm.
- **MaxClosureDist**: with the default `0.0`, a contour is treated as "closed" only when its start and end points coincide (L175520). In practice most fitted contours are therefore returned as **arcs** with meaningful `StartPhi`/`EndPhi`, not closed circles. This does not change the fitted center/radius/diameter — only `StartPhi`/`EndPhi`/`PointOrder` differ — and the overlay draws a full circle regardless.

References:

- MVTec HALCON 17.12 `fit_circle_contour_xld`: https://www.mvtec.com/doc/halcon/1712/en/fit_circle_contour_xld.html
- MVTec HALCON 17.12 `gen_measure_arc`: https://www.mvtec.com/doc/halcon/1712/en/gen_measure_arc.html
- MVTec HALCON 17.12 `measure_pos`: https://www.mvtec.com/doc/halcon/1712/en/measure_pos.html
- MVTec HALCON 17.12 `measure_pairs`: https://www.mvtec.com/doc/halcon/1712/en/measure_pairs.html
- MVTec HALCON 17.12 `add_metrology_object_circle_measure`: https://www.mvtec.com/doc/halcon/1712/en/add_metrology_object_circle_measure.html

## Architecture

Follow the same dependency direction as Edge Detection and Line Fitting:

```text
Domain <- Application <- Halcon <- App.Wpf
```

- **Domain** - plain parameter/result models. No Halcon, UI, file-system, or hardware dependencies.
- **Application** - circle fitting interface expressed in terms of Domain types.
- **Halcon** - concrete adapter that converts edge points into an XLD contour and calls HALCON.
- **App.Wpf** - transitional WinForms test harness, using the existing Edge Detection tab and `HWindowControl` overlay.

No WPF/XAML migration.

## Files

**Create:**

- `src/FlashMeasurementSystem.Domain/CircleFitting/CircleFittingParameters.cs`
- `src/FlashMeasurementSystem.Domain/CircleFitting/CircleFittingResult.cs`
- `src/FlashMeasurementSystem.Application/CircleFitting/ICircleFitter.cs`
- `src/FlashMeasurementSystem.Halcon/CircleFitting/HalconCircleFitter.cs`
- `tests/FlashMeasurementSystem.Tests/CircleFittingDomainTests.cs`

**Modify:**

- `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`
- `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`
- `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`
- `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`

## Domain Layer

`CircleFittingParameters`:

- `Algorithm` (string, default `"geotukey"`) - valid HALCON 17.12 values: `algebraic`, `ahuber`, `atukey`, `geometric`, `geohuber`, `geotukey`.
- `MaxNumPoints` (int, default `-1`) - `-1` means use all points.
- `MaxClosureDist` (double, default `0.0`) - HALCON closure distance threshold.
- `ClippingEndPoints` (int, default `0`) - number of contour points ignored at each end for fitting.
- `Iterations` (int, default `3`) - used by algebraic robust methods; ignored by geometric methods per HALCON docs (so it has no effect with the default `geotukey` algorithm).
- `ClippingFactor` (double, default `2.0`) - robust clipping factor for Tukey-style algorithms.
- `MinPoints` (int, default `3`) - **user-defined floor** for point count before calling HALCON. The adapter enforces the effective minimum as `max(MinPoints, 3 + 2 * ClippingEndPoints)` per reference L175526 — increasing `ClippingEndPoints` raises the real requirement.

`CircleFittingResult`:

- `Success` (bool)
- `CenterRow` / `CenterColumn` (double)
- `RadiusPx` (double)
- `DiameterPx` (double)
- `StartPhi` / `EndPhi` (double)
- `PointOrder` (string)
- `ResidualRms` (double)
- `Roundness` (double)
- `UsedPoints` (int)
- `ErrorMessage` (string)

No Halcon types should appear in Domain models.

## Application Layer

`ICircleFitter`:

```csharp
CircleFittingResult FitCircle(IList<EdgePoint> edgePoints, CircleFittingParameters parameters);
```

The interface depends only on `FlashMeasurementSystem.Domain.EdgeDetection` and `FlashMeasurementSystem.Domain.CircleFitting`.

## Halcon Layer

`HalconCircleFitter : ICircleFitter`:

1. Use `CircleFittingParameters.Default()` when parameters are null.
2. Validate `Algorithm` against official HALCON values.
3. Validate edge point count against `MinPoints`.
4. Convert `EdgePoint.Row` and `EdgePoint.Column` into row/column arrays in that exact order.
5. Create an XLD contour via `GenContourPolygonXld`.
6. Call `HOperatorSet.FitCircleContourXld` with the documented HALCON 17.12 parameter order.
7. Map fitted center, radius, start/end phi, and point order into `CircleFittingResult`.
8. Compute `DiameterPx = RadiusPx * 2.0`.
9. Compute residuals from radial error: `abs(distance(edgePoint, center) - radius)`.
10. Compute `ResidualRms = sqrt(mean(radialError^2))`.
11. Compute `Roundness = max(pointRadius) - min(pointRadius)` where `pointRadius` is each edge point's distance to the fitted center. This is a **radial peak-to-valley (runout)** metric in pixels — it is *not* ISO 1101 roundness/circularity, and a single stray point inflates it. Always read it together with `ResidualRms`.
12. Convert `HalconException` into a failed `CircleFittingResult` with a useful `ErrorMessage`.

Both `ResidualRms` and `Roundness` are computed over **all** input edge points, including the
outliers that `geotukey`/`atukey` exclude internally. They therefore measure "how well the fit
matches the full input set", not "the HALCON algorithm's internal inlier error". This mirrors the
Line Fitting RMS convention and is acceptable for quality reporting and visual inspection.

This implementation should not add custom RANSAC, segmentation, or metrology wrappers. The official HALCON operator already provides robust algorithms such as `geotukey`.

## Main Window Test UI

Add the test action to the existing Edge Detection tab.

### Layout

- Change the existing button panel from three buttons to four buttons:
  - `Detect`
  - `Clear`
  - `Fit Line`
  - `Fit Circle`
- Add a read-only result label below the existing Line Fitting result label for:
  - success/failure
  - center row/column
  - radius and diameter in pixels
  - residual RMS
  - roundness
  - used point count
- Keep the UI compact to avoid designer churn.

### Behavior

1. User loads an image.
2. User draws or uses the existing Edge Detection ROI.
3. User clicks Detect to populate edge points.
4. User clicks Fit Circle.
5. GUI calls `HalconCircleFitter.FitCircle` with the latest edge points and default parameters.
6. On success, draw the fitted circle overlay on `HWindowControl` and display numeric results.
7. On failure, show `ErrorMessage` in the Circle Fitting result area.

The circle fitting action should not silently rerun Edge Detection. Keeping Detect and Fit Circle separate makes failures easier to diagnose.

## Overlay

Add `OverlayAnnotator.DrawCircle(double row, double col, double radius, string color)` and implement it through HALCON `DispCircle`.

The Circle Fitting overlay should redraw:

- the latest Edge Detection ROI in blue,
- sampled edge points in cyan,
- the fitted circle in green.

It should reuse the existing `SetPersistentOverlayAction` pattern so pan/zoom and window resize redraw the overlay.

## Error Handling

- No Edge Detection result: show `請先執行邊緣檢測`.
- Insufficient edge points: show point count and required minimum.
- Unsupported algorithm value: fail fast in the adapter result with a clear message.
- HALCON failure: show the HALCON message in `CircleFittingResult.ErrorMessage`.
- Overlay drawing failure should not corrupt the fitted numerical result; report drawing failure separately in the Circle Fitting result label.

## Known Limitations / Caveats

The first version assumes all input edge points belong to one physical circle or arc. This follows the same philosophy as Line Fitting, where fitting quality is judged by RMS and visual inspection.

Known cases where results may be mathematically valid but semantically wrong:

- ROI contains multiple circular features.
- ROI contains both inner and outer edges of a ring.
- Edge points cover only a tiny arc segment, making center/radius unstable.
- Edge Detection returns points from non-circular nearby geometry.
- Subpix mode returns many points; overlay must remain capped to avoid UI lag.

`MeasurePos` / `MeasurePairs` / 2D Metrology support is intentionally out of scope for this first version. It should be added later only when the GUI has a clear way to provide an initial circle center/radius.

## Stale State

The GUI stores `_latestEdgeRoi`, `_latestEdgeResult`, `_latestLineFittingResult`, and should add `_latestCircleFittingResult`. Circle fitting state must be invalidated when the user changes the underlying image or ROI:

- Loading a new image
- Clearing the ROI
- Drawing/selecting a new ROI
- Running a new Edge Detection result

## Out Of Scope

- 2D Metrology API integration.
- `GenMeasureArc` / `MeasurePos` / `MeasurePairs` workflow.
- Automatic Edge Detection rerun inside Fit Circle.
- Multiple circle fitting.
- Automatic circle/arc segmentation.
- Recipe save/load.
- MeasurementWorkflow integration.
- WPF/XAML migration.
- Custom RANSAC implementation.
- New test framework setup.

## Verification

**Build:**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

**Tests:**

- Run the existing console-style test project after adding `CircleFittingDomainTests.cs`.
- Keep tests focused on Domain defaults and interface compile contracts unless HALCON integration testing is explicitly added.

**Manual:**

1. Start the app.
2. Select an image from `data/images`.
3. Draw an ROI in the Edge Detection tab.
4. Click Detect and verify edge points appear.
5. Click Fit Circle.
6. Confirm a fitted circle overlay appears.
7. Confirm center, radius, diameter, RMS, roundness, and point count are displayed.
8. Clear overlays and repeat with different edge detection parameters.
9. Verify insufficient-points and no-edge-result messages.

