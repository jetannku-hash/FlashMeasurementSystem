---
phase: 01-operator-experience
plan: 01
type: summary
status: complete
requirements: [GUI-01, GUI-02]
commit: 935060a
---

# 01-01 Summary — Empty-state guide (N3) + PASS/FAIL banner (N2)

Re-implementation of the originally-rolled-back GUI-01/GUI-02 work
(prior broken attempt reverted in `05b6e0b`). Re-done with Claude,
GUI acceptance approved by operator.

## Delivered

- **GUI-01 / N3 — empty-state guide** (`emptyStateGuideLabel`): a 3-step
  next-step guide Label overlaid on the image area, visible only when no
  image AND no recipe is loaded; hidden once either is present.
- **GUI-02 / N2 — PASS/FAIL banner** (`resultBannerPanel` +
  `resultBannerLabel`): a fixed 56px row spanning both columns at the top
  of `mainTableLayout`; gray "—" (not measured), green "PASS", or red
  "FAIL（NG n）"; reset to gray on image change.
- `MainWindow.UpdateEmptyState()` — drives guide visibility from
  image/recipe state (called from OnLoad, ClearResultDisplays, recipe
  load, editor save-callback).
- `MainWindow.SetResultBanner(int okCount, int ngCount, bool measured)` —
  drives banner color/text (called from OnLoad, ClearResultDisplays,
  end of DrawRecipeResults).

## Deviations from 01-01-PLAN.md (intentional)

1. **N3 overlay host.** The plan put `emptyStateGuideLabel` as a second
   control directly in the `mainTableLayout` image cell. A
   `TableLayoutPanel` cell cannot hold two controls — the second is
   pushed into the next cell (the right column), which was the actual
   on-screen defect. Fix: added a plain `imageHostPanel` (Panel) in the
   image cell and parented BOTH `hWindowControl` and `emptyStateGuideLabel`
   inside it, so Z-order / `BringToFront` overlay works correctly.
2. **No alpha BackColor.** Plan allowed `FromArgb(200,20,20,20)` (alpha is
   flattened by WinForms). Used solid `FromArgb(37,37,38)` instead.
3. **Visibility rule.** Plan kept the guide visible (grayed) after an image
   loads. That re-covers the just-loaded image with an opaque panel. Changed
   to: guide visible only when truly empty (no image AND no recipe).
4. **HALCON control untouched.** Never toggle `hWindowControl.Visible`
   (`HWindowControlHelper` captures `HalconWindow` at construction;
   toggling would invalidate it).

## Verification

- `dotnet build … /p:Platform="Any CPU"` → 0 warnings / 0 errors.
- `dotnet build … /p:Platform=x64` → 0 warnings / 0 errors.
- `FlashMeasurementSystem.Tests.exe` → all suites passed, exit 0.
- GUI acceptance (operator): empty-state guide on the left image area with
  the right control tabs fully visible (no overlay), banner colors correct.

## Files changed

- `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
