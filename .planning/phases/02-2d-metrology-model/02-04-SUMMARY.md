---
phase: 02-2d-metrology-model
plan: 04
type: summary
status: awaiting-gui-acceptance
requirements: [MET2D-01, MET2D-02, MET2D-04]
---

# 02-04 Summary — Metrology editor + synthetic images + answer sheet

Wave 4 of Phase 2. Operator-facing layer. Tasks 1 & 2 (autonomous) done with
Claude; Task 3 is the blocking GUI human-verify (operator).

## Delivered

- **MetrologyModelEditorForm** (dedicated modal WinForms Form, code-built, no
  Designer — lowest-risk, self-contained, easy rollback per CONTEXT decision):
  object list + Add/Remove, shape combo (Line/Circle/Ellipse/Rectangle) gating
  the enabled nominal-geometry fields, measure-param numerics (prefilled with DTO
  defaults), per-object Name, a non-blocking MeasureLength1 warning. Edits clones;
  Save commits to `recipe.MetrologyModel` (+ ImageWidth/Height hint) and invokes
  the saved callback; Cancel discards. **No measure-rectangle placement math** —
  only sets MeasureDistance/NumMeasures (HALCON auto-distributes).
- **MainWindow "Metrology Model" button** + `OpenMetrologyModelEditor`: opens the
  editor for `_loadedRecipe`, persists via `_recipeStore.Save` when a path exists,
  so the saved model flows straight into RecipeRunner Pass 3.
- **SyntheticMetrologyImageGenerator** (Tests.Halcon): writes 5 PNGs to
  `data/images` (line=vertical step edge, circle, ellipse, rectangle, composite),
  geometry identical to the answer sheet + the Wave-2 tests. Wired into Program.cs
  (runs with the suite) + csproj.
- **data/images/SYNTHETIC_METROLOGY_GROUNDTRUTH.md**: per-image nominal geometry,
  expected fitted params, tolerance bands, and an operator how-to.

## Verification (automated)

- `dotnet build … /p:Platform=x64` → 0/0; `… "Any CPU"` → 0/0.
- `FlashMeasurementSystem.Tests.Halcon.exe` → 12/12 suites incl. the generator
  ("wrote 5 images"), exit 0; all 5 PNGs + the answer sheet present on disk.
- `FlashMeasurementSystem.Tests.exe` → all Domain suites pass, exit 0.

## Task 3 — GUI acceptance (PENDING operator)

Blocking human-verify: define a metrology model in the editor, load a synthetic
image, Run Recipe once → fitted shapes + cyan measure points draw (overlay from
Wave 3 first visible here), compare against the answer sheet within tolerance;
confirm an old/no-model recipe runs unchanged (coexistence) and overlays survive
pan/zoom. Operator types "approved" to close the phase.

## Files changed

New: `src/FlashMeasurementSystem.App.Wpf/MetrologyModelEditorForm.cs`,
`tests/FlashMeasurementSystem.Tests.Halcon/SyntheticMetrologyImageGenerator.cs`,
`data/images/SYNTHETIC_METROLOGY_GROUNDTRUTH.md`, 5× `data/images/synthetic_metrology_*.png`.
Edited: `MainWindow.cs` (button + handler), App.Wpf csproj, Tests.Halcon Program.cs + csproj.
