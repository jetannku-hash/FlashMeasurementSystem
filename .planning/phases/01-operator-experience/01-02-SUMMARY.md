---
phase: 01-operator-experience
plan: 02
type: summary
status: complete
requirements: [GUI-03, GUI-04]
commits: [58eea73, aec4fa7]
---

# 01-02 Summary — Live tolerance preview (N5) + in-editor trial measure (A1)

Implemented with Claude; GUI acceptance approved by operator (including a
re-verify round after three reported display issues were fixed).

## Delivered

- **GUI-03 / N5 — live tolerance range preview** (`_tolerancePreviewLabel`):
  read-only Label under the Upper input in the Tolerance group; shows
  `= [LowerLimit, UpperLimit] <unit>` live, turns red `⚠ 上限 < 下限` on a
  reversed range. `UpdateTolerancePreview()` called from `WriteTolerance`
  and `PopulateFromTool`. Reuses `ToleranceSpec.LowerLimit/UpperLimit` and
  the RecipeValidator inverted-range predicate — no Domain change, no test
  suite (no new testable logic), no save-block.
- **GUI-04 / A1 — in-editor trial measure** (`_trialMeasureButton`
  `[在此試測]` + `OnTrialMeasure` + `RefreshTrialButtonEnabled`): runs the
  selected circle/line tool once and draws the fit on the shared main image.
  Button enabled only for circle/line + image loaded + delegate present.
  `RecipeEditor` ctor gained a 5th param
  `Func<MeasurementTool, ToolRunResult> trialMeasure` (4-arg overload
  forwards null). `MainWindow.OpenRecipeEditor` builds the delegate: a
  transient single-tool `Recipe.Default()` with `HasReferencePose=false`,
  one `_recipeRunner.Run` call, NO `EnsureRecipeValid`, NO whole-recipe
  re-run. Trial overlay repaints ROI rect + fit in the single persistent
  slot; HalconException → warning MessageBox, not a crash.

## Verification API audit (done before coding)

Confirmed against live code, not the plan's claims: `OverlayAnnotator`
`DrawRectangle2/DrawCircle/DrawLine/DrawText` signatures; `ToolRunResult`
fields (`ToolType/Measured/FitCenterRow/Col/RadiusPx/LineRow1..Col2/ValueText`);
`tool.Roi` is `RoiGeometry` (CenterRow/CenterCol/AngleRad/Length1/Length2);
`RecipeRunner.Run` 8-arg signature; `ToleranceSpec.LowerLimit/UpperLimit`;
`Recipe.Default/Tools/HasReferencePose`; `HWindowControlHelper.SetPersistentOverlayAction(Action)`
+ `Annotator`.

## Post-acceptance fixes (commit aec4fa7 — pre-existing display bugs)

Surfaced during verification, fixed on operator request, kept in a separate
commit (single responsibility):
1. `RecipeRunner.MeasureLine` showed angle only; now `Len=..px Ang=..deg`
   in both branches, matching the manual Fit Line display.
2. `OverlayAnnotator.DrawResultTable` value column too narrow → text ran
   into the verdict column; widened col2=160 / col3=370 / box 470.

The N5 preview row also forced the Tolerance GroupBox height 185->215
(part of commit 58eea73) so the trailing angle-hint row is no longer
clipped — this was a regression from adding the preview row.

## Verification

- `dotnet build … /p:Platform="Any CPU"` → 0/0.
- `dotnet build … /p:Platform=x64` → 0/0.
- `FlashMeasurementSystem.Tests.exe` → all suites pass, exit 0.
- GUI acceptance (operator): N5 range + red reversed warning + full
  angle-hint visible; A1 fit draw / no-edge message / no residue / button
  enable rules; line length shown in Run Recipe; no result-table overflow.

## Files changed

- `src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs` (post-acceptance fix)
- `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs` (post-acceptance fix)
