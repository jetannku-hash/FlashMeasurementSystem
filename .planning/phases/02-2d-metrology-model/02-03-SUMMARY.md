---
phase: 02-2d-metrology-model
plan: 03
type: summary
status: complete
requirements: [MET2D-03, MET2D-04]
---

# 02-03 Summary — RecipeRunner Pass 3 + MainWindow wiring + metrology overlay

Wave 3 of Phase 2. Feature-adapter RecipeRunner + WinForms wiring layers.
Executed with Claude. Touches the sensitive MainWindow.cs — change is purely
additive (a new branch + one constructor arg + one field), no existing 1D path
altered.

## Delivered

- **RecipeRunner additive Pass 3** (after Pass 2, before `return results;`):
  runs the injected `IMetrologyModelRunner<HImage>` only when
  `_metrologyRunner != null && recipe.MetrologyModel != null && Objects.Count > 0`.
  Alignment args gated by `recipe.HasReferencePose && hasMatch` (same as 1D). Each
  metrology object appended as a ToolRunResult (never reassigns existing entries).
- **Nullable constructor param** `IMetrologyModelRunner<HImage> metrologyRunner = null`
  (trailing, default null) → every existing construction site + unit tests still
  compile; Pass 3 is a no-op when null (MET2D-03 coexistence).
- **MapToToolRunResult(MetrologyObjectResult)** → ToolType `metrology_<shape>`
  (distinct from 1D types so no pass re-processes them). Carries fitted geometry
  into ToolRunResult (circle: FitCenter*/FitRadiusPx; line: LineRow1..Col2;
  ellipse/rectangle: FitCenter* + new FitPhi/FitRadius1/FitRadius2/FitLength1/
  FitLength2) + measure points (new MetrologyMeasureRows/Cols).
- **MainWindow**: `_metrologyRunner = new HalconMetrologyModelRunner()` field +
  using; passed as the 8th arg to `new RecipeRunner(...)`.
- **Overlay**: a `metrology_*` branch INSIDE the existing single
  SetPersistentOverlayAction (not a second slot — avoids the single-slot
  regression). Draws the fitted shape (green; red/green if IsOk set) via
  DrawCircle/DrawLine/DrawEllipse/DrawRectangle2, measure points as cyan crosses
  sampled against MaxOverlayCrosses (200), and ValueText (anchored at the fit
  centre, or line midpoint). The existing okCount/ngCount tally already includes
  metrology IsOk (true/false counted, null ignored) — banner stays consistent.

## Verification

- `dotnet build … /p:Platform=x64` → 0/0.
- `dotnet build … /p:Platform="Any CPU"` → 0/0.
- `FlashMeasurementSystem.Tests.exe` → all Domain suites pass, exit 0 (null-runner
  construction valid; 1D unchanged).
- `FlashMeasurementSystem.Tests.Halcon.exe` → 11/11, exit 0.
- Visual coexistence of the metrology overlay is exercised at the 02-04 GUI
  acceptance checkpoint — there is no UI path to create a Recipe.MetrologyModel
  until 02-04 adds the editor, so the overlay cannot be screenshotted in isolation
  this wave (expected; plan Task 2 defers visual verification to 02-04).

## Files changed

Edited: `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs` (usings, ToolRunResult
fields, field + nullable ctor param, Pass 3, MapToToolRunResult),
`src/FlashMeasurementSystem.App.Wpf/MainWindow.cs` (using, _metrologyRunner field,
RecipeRunner construction arg, metrology overlay branch).
