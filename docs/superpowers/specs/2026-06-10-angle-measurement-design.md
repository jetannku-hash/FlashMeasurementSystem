# Angle Measurement Design

**Goal:** Adapt manual section 4.7 Angle Measurement into the project layering and add a manually testable WinForms GUI action that computes the angle between two fitted lines (and, as 4.7 also requires, the angle of one line against the horizontal / vertical axis).

**Selected approach:** Add a standalone Angle Measurement feature following the existing feature-adapter pattern (Domain DTOs + Application interface + HALCON adapter + console tests), and expose it on the **existing Measurement tab**, reusing the same `measurementCoordInput` coordinate-input box and the existing `Append Line` button that the Distance LineToLine measurement already uses.

## Context

Manual section 4.7 defines Angle Measurement as the step **after** line fitting:

- Input: two lines already fitted (each as start point + end point), **or** a single line measured against the horizontal / vertical reference axis.
- Output: the angle between the two lines, plus each line's angle against the horizontal axis.
- Typical use: workpiece edge angular offset, V-groove angle, datum angular deviation.

The manual's own sample provides two implementations of the two-line angle:

1. `MeasureAngle` — pure `Math.Atan2` direction vectors, then folds the difference down to the **acute** angle in `[0°, 90°]`.
2. `MeasureAngleHalcon` — calls HALCON `angle_ll` and takes the absolute value.

The project already has section 4.4 Line Fitting, 4.5 Circle Fitting, and 4.6 Distance Measurement implemented across the expected layers and exposed in the GUI. Angle Measurement should follow that same pattern instead of introducing a new workflow or UI model.

This design uses the HALCON operators rather than hand-rolled `atan2`, mirroring the Circle Fitting decision (use the official operator, verify the signature against the offline reference, do not rely on memory). The relevant HALCON 17.12 operators, verified against `reference_hdevelop.txt`:

- **`angle_ll`** (reference L155253) — `angle_ll(RowA1, ColumnA1, RowA2, ColumnA2, RowB1, ColumnB1, RowB2, ColumnB2, : Angle)`. Interprets each pair of points as a vector (A: pt1→pt2, B: pt1→pt2); the result is the angle obtained by rotating vector A counter-clockwise onto vector B about their intersection. `Angle` is returned **in radians, signed, `-π ≤ Angle ≤ π`**. The reference explicitly states: *"The result depends on the order of the points and on the order of the lines."*
- **`angle_lx`** (reference L155322) — `angle_lx(Row1, Column1, Row2, Column2, : Angle)`. Angle of the line vector (pt1→pt2) against the horizontal axis (column / X axis), in radians, signed, `-π ≤ Angle < π`. Sign follows whether the end point is above/below the horizontal axis.

### The angle-ambiguity decision (important)

Two undirected lines actually define two supplementary angles (e.g. 60° and 120°); a directed vector pair defines an angle in `[0°, 180°]`; folding further gives an acute angle in `[0°, 90°]`. Which one the user wants is genuinely application-dependent:

- A V-groove of 120° wants **120°**, not its supplement 60°.
- An alignment-offset check wants the **acute** angle regardless of which way the edges were traced.
- A tilt check wants the line's angle **against horizontal**.

`angle_ll`'s raw output is order-dependent (swapping a line's two endpoints flips its vector to the supplement). Rather than silently pick one convention and discard information, the result DTO reports **all** of them and lets the caller / operator interpret:

- `RawAngleDeg` — the raw signed `angle_ll` output in `(-180°, 180°]`. Order-dependent; for advanced users / debugging.
- `AngleDeg` / `AngleRad` — the **directed-vector** angle `|angle_ll|`, folded to `[0°, 180°]`. This is the primary answer and preserves the V-groove case. Still depends on each line's endpoint order (a line and its reverse give supplementary results); for line-fitting input the endpoint order is stable for a given ROI.
- `AcuteAngleDeg` — folded further to `[0°, 90°]`. This is the only **fully order-independent** value (matches the manual's `MeasureAngle`).
- `RefAngle1Deg` / `RefAngle2Deg` — each line's angle against the horizontal axis via `angle_lx`, in `(-180°, 180°]`.
- `IsNearParallel` — true when `AcuteAngleDeg < NearParallelWarningDeg`. The manual notes that for near-parallel lines (< 2°) angle is unstable and distance measurement should be used instead; this surfaces that as a flag rather than failing.

References:

- MVTec HALCON 17.12 `angle_ll`: https://www.mvtec.com/doc/halcon/1712/en/angle_ll.html
- MVTec HALCON 17.12 `angle_lx`: https://www.mvtec.com/doc/halcon/1712/en/angle_lx.html
- Offline: `halcon_pdf/reference/reference_hdevelop.txt` L155253 (`angle_ll`), L155322 (`angle_lx`).

## Architecture

Follow the same dependency direction as Line Fitting, Circle Fitting, and Distance Measurement:

```text
Domain <- Application <- Halcon <- App.Wpf
```

- **Domain** — plain parameter/result models. No Halcon, UI, file-system, or hardware dependencies.
- **Application** — angle measurement interface expressed only in Domain types (here, plain `double` coordinates + the Angle DTOs).
- **Halcon** — concrete adapter that calls `angle_ll` / `angle_lx` and maps outputs to the Domain result.
- **App.Wpf** — transitional WinForms test harness, using the existing Measurement tab, `measurementCoordInput`, and the `HWindowControl` overlay.

No WPF/XAML migration.

## Files

**Create:**

- `src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleMeasurementParameters.cs`
- `src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleMeasurementResult.cs`
- `src/FlashMeasurementSystem.Application/AngleMeasurement/IAngleMeasurer.cs`
- `src/FlashMeasurementSystem.Halcon/AngleMeasurement/HalconAngleMeasurer.cs`
- `tests/FlashMeasurementSystem.Tests/AngleMeasurementDomainTests.cs`

**Modify:**

- `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`
- `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`
- `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs` (wire `AngleMeasurementDomainTests.Run()`)
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`

No change to `OverlayAnnotator.cs` is required — the existing `DrawLine` / `DrawCross` / `DrawText` helpers cover the angle overlay.

## Domain Layer

`AngleMeasurementParameters`:

- `Mode` (string, default `"line_to_line"`) — allow-list: `line_to_line`, `line_to_horizontal`, `line_to_vertical`. Covers the manual's full 4.7 scope ("between two lines, or against horizontal/vertical").
- `NearParallelWarningDeg` (double, default `2.0`) — below this acute angle, `IsNearParallel` is set (manual's < 2° guidance).
- `MinPointSeparation` (double, default `1.0`) — minimum pixel distance between a line's two endpoints; below it the line direction is undefined and the measurement fails fast with a clear message (manual notes lines should be ≥ ~200 px for stability; 1.0 px is only the hard degeneracy floor).
- `Default()` factory returning the above.
- `IsSupportedMode(string)` allow-list check.

Defaults are written as property initializers (same convention as the post-Batch-1 `LineFittingParameters` / `CircleFittingParameters`) so `new AngleMeasurementParameters()` is born legal, and `Default()` just returns `new AngleMeasurementParameters()`.

`AngleMeasurementResult`:

- `Success` (bool)
- `AngleDeg` / `AngleRad` (double) — primary directed angle, `[0°, 180°]` / `[0, π]`.
- `AcuteAngleDeg` (double) — `[0°, 90°]`, order-independent.
- `RawAngleDeg` (double) — signed `angle_ll`, `(-180°, 180°]`.
- `RefAngle1Deg` (double) — line 1 vs horizontal (`angle_lx`).
- `RefAngle2Deg` (double) — line 2 vs horizontal (`angle_lx`). For horizontal/vertical modes this is the synthesized reference axis (0° or 90°).
- `IsNearParallel` (bool)
- `ErrorMessage` (string, default `string.Empty`)

No Halcon types should appear in Domain models.

## Application Layer

`IAngleMeasurer`:

```csharp
AngleMeasurementResult MeasureAngle(
    double line1Row1, double line1Col1, double line1Row2, double line1Col2,
    double line2Row1, double line2Col1, double line2Row2, double line2Col2,
    AngleMeasurementParameters parameters);
```

One uniform signature for all three modes. For `line_to_horizontal` / `line_to_vertical` the `line2*` arguments are ignored — the adapter synthesizes the reference vector internally. This keeps the interface in plain Domain types (`double` + the Angle DTOs) with no Halcon dependency, matching `IDistanceMeasurer`'s coordinate-based style.

## Halcon Layer

`HalconAngleMeasurer : IAngleMeasurer`:

1. Use `AngleMeasurementParameters.Default()` when `parameters` is null.
2. Validate `Mode` against `IsSupportedMode`; fail fast with a clear message otherwise.
3. Validate line 1's endpoint separation ≥ `MinPointSeparation`; fail fast otherwise.
4. Resolve line 2 per mode:
   - `line_to_line` — use the supplied `line2*`; also validate its separation ≥ `MinPointSeparation`.
   - `line_to_horizontal` — synthesize line 2 as a horizontal vector through line 1's first point: `(l1r1, l1c1) → (l1r1, l1c1 + 100)`.
   - `line_to_vertical` — synthesize line 2 as a vertical vector through line 1's first point: `(l1r1, l1c1) → (l1r1 + 100, l1c1)`.
   - The synthesized reference shares line 1's first point, so `angle_ll`'s intersection (center of rotation) is well-defined.
5. Call `HOperatorSet.AngleLl(...)`; `raw = Angle.D` (radians, signed).
6. Map results:
   - `RawAngleDeg = raw * 180 / π`
   - `directed = |raw|`; `AngleRad = directed`; `AngleDeg = directed * 180 / π`
   - `AcuteAngleDeg = AngleDeg > 90 ? 180 - AngleDeg : AngleDeg`
   - `RefAngle1Deg` via `HOperatorSet.AngleLx` on line 1; `RefAngle2Deg` via `AngleLx` on the (possibly synthesized) line 2.
   - `IsNearParallel = AcuteAngleDeg < NearParallelWarningDeg`
7. `Success = true` on the happy path.
8. Convert `HalconException` into a failed result with a useful `ErrorMessage`.

The adapter adds no `atan2` math of its own beyond the trivial endpoint-separation check (`sqrt(dRow² + dCol²)`); the angle itself comes entirely from HALCON operators.

## Main Window Test UI

Add the test action to the **existing Measurement tab**, reusing `measurementCoordInput` and the existing `Append Line` button.

### Coordinate input format

Identical parsing to the existing Distance measurement (`ParseCoordinateLine`, one `row,col` per line):

- `line_to_line` — **4 lines**: line1-pt1, line1-pt2, line2-pt1, line2-pt2. (Press `Append Line` twice to load two fitted lines.)
- `line_to_horizontal` / `line_to_vertical` — **2 lines**: the single line's two endpoints. (Press `Append Line` once.)

This reuses the same format the Distance `LineToLine` type already expects for its first two lines, so the existing `Append Line` plumbing needs no change.

### Layout

In `measurementTableLayout`, add below the existing distance controls:

- `angleModeLabel` + `angleModeCombo` (ComboBox, DropDownList, items `line_to_line` / `line_to_horizontal` / `line_to_vertical`, default `line_to_line`).
- `measureAngleButton` ("Measure Angle"), spanning both columns.

The angle result is shown by **reusing the existing `measureResultLabel`** (the flexible bottom row) — distance and angle share the result display, last action wins. This avoids adding a second result label and keeps designer churn minimal.

> ⚠️ `measurementTableLayout` maps `RowStyles` **by index**, and its current last row is `Percent 100F` (the result label). When adding the two new rows, **insert** their `RowStyle`s before the trailing percent row (or rebuild the collection so order is: existing absolute rows → the two new absolute rows → `Percent 100F` last). Appending naively makes the new rows inherit the percent stretch and squashes the result label — the same trap documented in the Circle Fitting plan for `edgeTableLayout`.

### Behavior

1. User fits two lines (Edge Detection tab → Fit Line, ×2) and appends them with `Append Line`, or types coordinates manually.
2. User selects the angle mode and clicks `Measure Angle`.
3. GUI parses the required number of coordinate lines for the mode, calls `HalconAngleMeasurer.MeasureAngle`, and on success draws the overlay and shows the numeric result.
4. On failure, shows `ErrorMessage`.

### Overlay

Reuse the `SetPersistentOverlayAction` pattern and call `DrawFittingLayers(an)` first (so ROI / edge crosses / fitted line+circle evidence stays visible underneath, consistent with the Batch-2 distance overlays), then draw:

- both lines in green with `L1` / `L2` text labels (for horizontal/vertical mode: line 1 green + the synthesized reference axis in gray),
- a magenta vertex cross at the average of the involved points (robust against the parallel-line case where a true intersection does not exist),
- the angle value as yellow text near the vertex.

## Error Handling

- Unsupported mode value: fail fast in the adapter result with a clear message.
- Degenerate line (endpoints closer than `MinPointSeparation`): fail with which line and the required separation.
- Too few coordinate lines for the selected mode: UI shows the required count and the `Append Line` hint; the adapter is not called.
- Invalid coordinate format: UI shows a format error; the adapter is not called.
- HALCON failure: show the HALCON message in `AngleMeasurementResult.ErrorMessage`.
- `IsNearParallel`: not an error — the result is still returned and displayed, with an appended "near-parallel, consider distance measurement" note.

## Known Limitations / Caveats

- `AngleDeg` depends on each line's endpoint order (a line vs its reverse gives supplementary angles). For line-fitting input the order is stable per ROI; `AcuteAngleDeg` is the order-independent fallback.
- Angle stability degrades for short lines; the manual recommends ROI length ≥ ~200 px. `MinPointSeparation` only guards true degeneracy, not stability — short-but-valid lines still measure (possibly noisily).
- The overlay vertex is the centroid of the involved points, not the true geometric intersection, so for clearly intersecting lines the magenta cross may sit slightly off the crossing. This is cosmetic and intentional (robust to parallel lines); the numeric angle is unaffected.

## Out Of Scope

- Folding angle measurement into `DistanceMeasurementType` / `DistanceMeasurementResult` (kept as a separate feature per the manual's separate 4.7 section and the feature-adapter pattern).
- Multi-line / polygon interior-angle measurement.
- `intersection_ll`-based true-vertex overlay.
- Tolerance / OK-NG judgement on the angle (that is section 4.8).
- Recipe (`.zcp`) save/load of angle measurement setups.
- MeasurementWorkflow state-machine integration.
- WPF/XAML migration.
- New test framework setup.

## Verification

**Build:**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

**Tests:**

- Add `AngleMeasurementDomainTests.cs` (defaults, mode allow-list, interface compile contract) and wire its `Run()` into `Main()`.
- Keep tests focused on Domain defaults and interface compile contracts; HALCON adapters remain manually verified in the GUI.

**Manual:**

1. Start the app; load an image from `data/images`.
2. Edge Detection tab: draw an ROI on one straight edge, Detect, Fit Line; repeat on a second edge that meets it at a known angle.
3. Measurement tab: `Append Line` once after each fit (4 coordinate lines total); select `line_to_line`; click `Measure Angle`.
4. Confirm `AngleDeg`, `AcuteAngleDeg`, `RefAngle1`, `RefAngle2`, and `raw` are displayed, and the green lines + magenta vertex + yellow angle text appear over the image with the ROI/edge/fit evidence still visible underneath.
5. Switch mode to `line_to_horizontal`, leave only one line's 2 points (or append one line), and confirm the line-vs-horizontal angle matches `RefAngle1`.
6. Feed two nearly-parallel lines and confirm `IsNearParallel` note appears, no crash.
7. Feed a degenerate line (two identical points) and confirm the clear failure message, no exception.
8. Stale state: measure an angle, load a different image, confirm the result label / overlay reset (no angle drawn at old coordinates on the new image).
