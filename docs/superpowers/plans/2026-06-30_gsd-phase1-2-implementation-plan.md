# GSD Phase 1 & 2 — Consolidated Implementation Plan (backup)

> 由 `.planning/` 於 2026-07-01 整合備份（移除 GSD 工具前），內容為 Phase 1 (Operator Experience) 與 Phase 2 (2D Metrology Model) 的實作計畫與執行摘要。日期以昨日 2026-06-30 命名。原始逐檔內容依序保留於下。

---


<!-- ===================================================== -->
# 原始檔：.planning/phases/01-operator-experience/01-01-PLAN.md
<!-- ===================================================== -->

---
phase: 01-operator-experience
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs
  - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
autonomous: false
requirements: [GUI-01, GUI-02]
user_setup: []

must_haves:
  truths:
    # GUI-01 (N3)
    - "With no image AND no recipe loaded, a 3-step next-step guide is visible over the image area instead of an empty gray window."
    - "Loading an image grays out the whole guide (step ① done); loading a recipe fully hides it."
    # GUI-02 (N2)
    - "After Run Recipe or 一鍵量測, a large fixed banner shows green PASS (all OK), red FAIL（NG n） (any NG), or gray — (not yet measured)."
    - "Loading a new image resets the banner to the gray — state."
  artifacts:
    - "Designer control `emptyStateGuideLabel` (semi-transparent dark Label, white bold text, 3-step guide) overlaid in mainTableLayout cell (0,*) on top of hWindowControl."
    - "Designer controls `resultBannerPanel` + `resultBannerLabel` (fixed ~56px banner, large bold font, spans both columns, top of mainTableLayout)."
    - "MainWindow method `UpdateEmptyState()` that toggles `emptyStateGuideLabel` visibility/text from image/recipe state."
    - "MainWindow method `SetResultBanner(int okCount, int ngCount, bool measured)` that sets banner color + text."
  key_links:
    - "`UpdateEmptyState()` is called at EVERY `_loadedRecipe` mutation site (load ~1212, clear ~1221, editor-save callback ~1567), after image load/clear (`LoadAndDisplayImage` ~422 / `ClearResultDisplays` ~435), and once at end of MainWindow construction."
    - "`SetResultBanner(...)` is called at the end of `DrawRecipeResults` (after okCount/ngCount computed ~1417) and reset to gray in `ClearResultDisplays` (~444)."
    - "`emptyStateGuideLabel` is BringToFront()'d over `hWindowControl` so it shows when Visible, and does NOT capture mouse when Visible=false (HALCON pan/zoom/ROI must still work)."
---

<objective>
Deliver the "read state/outcomes at a glance" half of Phase 1: empty-state workflow
guidance (GUI-01 / N3) and a large PASS/FAIL banner (GUI-02 / N2). Both are pure WinForms
overlays on the main window — no measurement-logic change, no new dependencies.

Purpose: An operator opening the app with nothing loaded must see what to do next (not an
empty window), and after a run must see an unmissable overall PASS/FAIL from afar (the
existing `measureResultLabel` is small text only).

Output: Two new Designer control groups (`emptyStateGuideLabel`, `resultBannerPanel` +
`resultBannerLabel`) and two new MainWindow methods (`UpdateEmptyState`, `SetResultBanner`)
wired into the existing load/run/clear flows.

Design decisions (lifted verbatim from GUI計畫書 §N3 + §N2, Option A for both):
- N3 Option A = WinForms Label overlaid on the image area (NOT HALCON WriteString), toggled
  via Visible. Reason: empty HALCON window has no image; a WinForms label is simplest and
  does not depend on HALCON.
- N2 Option A = fixed-height Designer Panel at the top of the main window, NOT drawn on the
  image HUD. Reason: always visible, unaffected by image pan/zoom, simplest implementation.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/REQUIREMENTS.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Empty-state workflow guide (GUI-01 / N3)</name>
  <read_first>
    - docs/superpowers/plans/GUI建議優化項目計畫書.md  →  §N3 (空狀態工作流引導): 具體做法 / 選項A / 影響檔案 / 風險 / 驗證操作 (4 steps). IMPLEMENT OPTION A VERBATIM.
    - src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs  →  lines 178-200 (mainTableLayout: 1 row, Percent 100F; hWindowControl at (0,0) Dock=Fill; rightPanel at (1,0)). This is the file being modified.
    - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs  →  lines 412-456 (LoadAndDisplayImage → ClearResultDisplays), 1208-1225 (recipe load / clear sets _loadedRecipe), 1564-1578 (editor-save callback sets _loadedRecipe). This is the file being modified.
    - src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs  →  lines 12-25 (CurrentImage property; confirms the empty-state predicate).
    - CLAUDE.md → "WinForms / HALCON display gotchas" (Dock=Fill inside TableLayoutPanel; multiple controls per cell use Z-order/BringToFront).
  </read_first>
  <files>src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs, src/FlashMeasurementSystem.App.Wpf/MainWindow.cs</files>
  <action>
    Implement N3 Option A per GUI計畫書 §N3 (WinForms Label overlay, NOT HALCON WriteString).

    1) MainWindow.Designer.cs — declare `private System.Windows.Forms.Label emptyStateGuideLabel;`
       Instantiate it in InitializeComponent (near line 132 where measureResultLabel is created)
       and configure: `Dock = Fill`; `BackColor = Color.FromArgb(200, 20, 20, 20)` (dark; WinForms
       may flatten the alpha — a dark solid read is acceptable, the must-have is contrast);
       `ForeColor = Color.White`; `Font = new Font("Segoe UI", 14F, FontStyle.Bold)`; `TextAlign =
       MiddleCenter`; `Text` = the three-step guide exactly as the spec renders it:
         "① 載入影像（Load Image）\n② 載入或建立配方（Load / Edit Recipe）\n③ 按一鍵量測（One-Click）"
       Add it to the SAME mainTableLayout cell as hWindowControl. Because the banner task below
       inserts a new top row, place emptyStateGuideLabel into the image cell (the cell that holds
       hWindowControl after Task 2's row insert) and call `emptyStateGuideLabel.BringToFront()` so
       it sits above hWindowControl. Do NOT parent it under hWindowControl.Controls (HALCON control
       child rendering is unreliable) — keep it a sibling in the same TableLayoutPanel cell.

    2) MainWindow.cs — add `private void UpdateEmptyState()`:
         - If `_loadedRecipe != null` → `emptyStateGuideLabel.Visible = false;` return. (spec step 3: load recipe → guide disappears)
         - Else if `_imageHelper != null && _imageHelper.CurrentImage != null` → Visible=true; set
           ForeColor of the whole label gray (Color.Gainsboro) to indicate step ① is done. (spec step 2)
         - Else → Visible=true; ForeColor white (all steps lit). (spec steps 1 & 4)
       Keep the implementation simple: a single Label with one color state per branch is sufficient
       (do NOT attempt per-line coloring — that needs a custom-drawn control and is out of scope).

    3) Wire calls to UpdateEmptyState() at every state-change site:
         - End of MainWindow constructor / InitializeComponent completion (initial empty state).
         - In LoadAndDisplayImage after the image is displayed (~line 420, after ClearResultDisplays()).
         - In ClearResultDisplays (~line 435) — image cleared → re-evaluate.
         - After `_loadedRecipe = _recipeStore.Load(...)` (~line 1212) and in the load-failure / new
           branch where `_loadedRecipe = null` (~line 1221).
         - Inside the editor save-callback where `_loadedRecipe = recipe;` (~line 1567).
       Grep `_loadedRecipe =` to find any other assignment site and add the call there too.
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"</automated>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe</automated>
  </verify>
  <acceptance_criteria>
    - Both builds exit 0 (0 warning(s) 0 error(s) target); Tests.exe prints every suite "passed" and exits 0.
    - No new file is added (existing files only) → no csproj change needed.
    - grep confirms `emptyStateGuideLabel` is declared, instantiated, and BringToFront'd in Designer; `UpdateEmptyState` is defined in MainWindow.cs and is called from ≥4 sites (constructor, image load, recipe load, recipe clear).
    - Manual (GUI, see checkpoint): app launched with nothing loaded shows the 3-step guide over the image area; loading an image updates the guide; loading a recipe hides it.
  </acceptance_criteria>
  <done>Empty-state guide Label exists, overlays the image area, and its visibility is driven by image/recipe state per GUI計畫書 §N3 Option A.</done>
</task>

<task type="auto">
  <name>Task 2: Large PASS/FAIL banner (GUI-02 / N2)</name>
  <read_first>
    - docs/superpowers/plans/GUI建議優化項目計畫書.md  →  §N2 (大字 PASS/FAIL 橫幅): 具體做法 / 選項A / 影響檔案 / 風險 / 驗證操作 (3 steps). IMPLEMENT OPTION A VERBATIM.
    - src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs  →  lines 178-211 (mainTableLayout: RowCount=1, Columns 75%/25%, hWindowControl (0,0), rightPanel (1,0)). This is the file being modified.
    - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs  →  lines 1342-1422 (DrawRecipeResults: okCount/ngCount computed ~1411-1416, measureResultLabel set ~1417-1421), 435-456 (ClearResultDisplays resets measureResultLabel ~444-446), 1456-1516 (OneClickMeasureButton_Click calls DrawRecipeResults ~1495). This is the file being modified.
    - CLAUDE.md → "WinForms / HALCON display gotchas" (when inserting a row in a TableLayoutPanel, insert the RowStyle at the right index; keep the trailing Percent 100F row last).
  </read_first>
  <files>src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs, src/FlashMeasurementSystem.App.Wpf/MainWindow.cs</files>
  <action>
    Implement N2 Option A per GUI計畫書 §N2 (fixed Designer panel at top of main window, NOT a HUD overlay).

    1) MainWindow.Designer.cs — insert a fixed banner row at the TOP of `mainTableLayout`:
       - Declare `private System.Windows.Forms.Panel resultBannerPanel;` and
         `private System.Windows.Forms.Label resultBannerLabel;`.
       - Change `mainTableLayout.RowCount` from 1 to 2. Change the RowStyles so row 0 is the banner
         (`new RowStyle(SizeType.Absolute, 56F)`) and row 1 is the existing content
         (`new RowStyle(SizeType.Percent, 100F)`). Insert the Absolute row BEFORE the Percent row;
         the Percent row must remain the flexible one (per CLAUDE.md gotcha).
       - Move the existing children down one row: `hWindowControl` → cell (0,1), `rightPanel` → (1,1).
       - Add `resultBannerPanel` to cell (0,0) and `mainTableLayout.SetColumnSpan(resultBannerPanel, 2)`
         so it spans both columns (always visible regardless of the 75/25 split).
       - Configure `resultBannerPanel`: `Dock = Fill`; `BackColor = Color.FromArgb(160,160,160)`
         (gray default = not measured); `Controls.Add(resultBannerLabel)`.
       - Configure `resultBannerLabel`: `Dock = Fill`; `Font = new Font("Segoe UI", 24F, FontStyle.Bold)`;
         `ForeColor = Color.White`; `TextAlign = MiddleCenter`; `Text = "—"`.
       - NOTE: Task 1's `emptyStateGuideLabel` lives in the IMAGE cell. Because Task 2 shifts the image
         cell from row 0 to row 1, coordinate the two edits so emptyStateGuideLabel is added to
         hWindowControl's (post-shift) cell. Do both Designer edits consistently.

    2) MainWindow.cs — add `private void SetResultBanner(int okCount, int ngCount, bool measured)`:
         - `measured == false` → panel BackColor gray (`Color.FromArgb(160,160,160)`), label Text "—".
         - `ngCount > 0` → panel BackColor red (`Color.FromArgb(192, 0, 0)`), label Text
           string.Format("FAIL（NG {0}）", ngCount).
         - `ngCount == 0 && okCount > 0` → panel BackColor green (`Color.FromArgb(0, 128, 0)`),
           label Text "PASS".
         - (`okCount==0 && ngCount==0 && measured` → leave as FAIL/gray edge: treat as gray "—" —
           no measured tools. Pick gray for that case.)
       Use the same okCount/ngCount tally already computed in DrawRecipeResults (lines ~1411-1416,
       which correctly treat `r.IsOk == true` as OK, `== false` as NG, `null` as unmeasured).

    3) Wire it:
       - At the END of `DrawRecipeResults` (after measureResultLabel is set, ~line 1421), call
         `SetResultBanner(okCount, ngCount, true);`.
       - In `ClearResultDisplays` (next to the existing measureResultLabel reset ~line 444), call
         `SetResultBanner(0, 0, false);` so loading a new image resets the banner to gray "—".
       - OneClickMeasureButton_Click already routes through DrawRecipeResults → banner is covered.
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"</automated>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe</automated>
  </verify>
  <acceptance_criteria>
    - Both builds exit 0; Tests.exe exits 0 (no Domain/Application regression).
    - grep confirms `resultBannerPanel`, `resultBannerLabel`, and `SetResultBanner` exist; mainTableLayout RowCount is 2 with row 0 Absolute 56F + row 1 Percent 100F; SetColumnSpan(resultBannerPanel, 2) is set.
    - grep confirms `SetResultBanner(...)` is called from `DrawRecipeResults` (with measured=true) and from `ClearResultDisplays` (with measured=false).
    - Manual (GUI, see checkpoint): PASS recipe+image → 一鍵 → green "PASS"; one tool out of tolerance → red "FAIL（NG 1）"; load new image → gray "—".
  </acceptance_criteria>
  <done>Fixed ~56px banner spanning both columns sits at the top of mainTableLayout; its color/text is driven by okCount/ngCount per GUI計畫書 §N2 Option A, and resets on image change.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: GUI acceptance — empty-state guide + PASS/FAIL banner</name>
  <read_first>
    - docs/superpowers/plans/GUI建議優化項目計畫書.md  →  §N3 驗證操作 (4 steps) + §N2 驗證操作 (3 steps).
  </read_first>
  <files>n/a — verification only (no source changes in this task)</files>
  <action>
    Build x64, then launch `src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`
    (close the app before rebuilding) and run the human verification procedure in <how-to-verify>.
    This is a checkpoint task: the work is performed by the human operator, not by Claude.
  </action>
  <what-built>
    Empty-state 3-step guide (GUI-01) overlaid on the image area, auto-hiding on recipe load;
    and a large fixed PASS/FAIL banner (GUI-02) at the top of the main window that turns green/red/gray.
  </what-built>
  <how-to-verify>
    Build x64 first, then launch `src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`
    (close the app before rebuilding). Run exactly the spec verification steps:

    N3 (§N3 驗證操作):
    1. Launch with nothing loaded → the 3-step guide is visible over the image area.
    2. Load an image → step ① is grayed/hidden (guide updates).
    3. Load a recipe → the guide disappears entirely.
    4. (If image can be closed) → guide reappears.

    N2 (§N2 驗證操作):
    1. Load a recipe + image that PASS → Run Recipe / 一鍵量測 → banner shows large green "PASS".
    2. Edit one tool's nominal so it falls outside tolerance → run again → banner shows red "FAIL（NG 1）".
    3. Load a new image (no run yet) → banner resets to gray "—".

    Also confirm the banner is visible regardless of which feature tab is selected (it spans the top),
    and that HALCON pan/zoom/ROI still work when the empty-state guide is hidden.
  </how-to-verify>
  <verify>
    <human-check>Operator observes: empty-state 3-step guide appears with nothing loaded, updates/hides on image+recipe load; banner turns green PASS / red FAIL（NG n）/ gray — per spec §N3 + §N2 verification sequences.</human-check>
  </verify>
  <done>Operator types "approved" after both the N3 (4-step) and N2 (3-step) verification sequences pass; HALCON interaction unaffected.</done>
  <resume-signal>Type "approved" or describe the issues to fix.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Operator → local WinForms UI | All inputs (mouse on HALCON control, recipe/image file open dialogs) come from the local trusted operator. No network, no auth, no untrusted external input in this plan. |

## STRIDE Threat Register

This plan adds two pure-display WinForms controls and reads already-trusted in-memory state
(`_loadedRecipe`, `okCount`/`ngCount`). It introduces no new external input, persistence,
network, or package install. Residual risk is operator-facing reliability, not classical security.

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-01-01 | Denial of Service | `SetResultBanner` / `UpdateEmptyState` | low | accept | Pure WinForms property sets on the UI thread; cannot throw on the okCount/ngCount integers. No mitigation beyond the existing try/catch around Run Recipe / 一鍵量測 in MainWindow.cs. |
| T-01-02 | Information Disclosure | `emptyStateGuideLabel` | low | accept | Displays only generic next-step hint text — no measurement data, no file paths, no secrets. |
| T-01-03 | Tampering | n/a — no package install | low | accept | No npm/pip/cargo/NuGet install in this plan (pure edits to existing source files); package-legitimacy gate T-01-SC is not applicable. |

</threat_model>

<verification>
Phase-level checks for this plan (Wave 1):
- `dotnet build ... /p:Platform="Any CPU"` exits 0.
- `dotnet build ... /p:Platform=x64` exits 0 (HALCON/platform-sensitive UI changes verified under x64).
- `FlashMeasurementSystem.Tests.exe` exits 0 (Domain/Application contracts unaffected).
- Manual GUI acceptance (Task 3) passes both N3 and N2 spec verification sequences.
</verification>

<success_criteria>
- GUI-01 (N3): operator with nothing loaded sees the 3-step guide; it hides on recipe load.
- GUI-02 (N2): operator sees a large green PASS / red FAIL（NG n）/ gray — banner after each run, reset on image change.
- No regression to existing Run Recipe / 一鍵量測 flows; HALCON pan/zoom/ROI unaffected.
</success_criteria>

<output>
Create `.planning/phases/01-operator-experience/01-01-SUMMARY.md` when done (wave 1, both tasks + GUI acceptance).
</output>

<artifacts_this_phase_produces>
This plan (01-01) produces:
- `emptyStateGuideLabel` (Designer Label) — empty-state 3-step guide.
- `resultBannerPanel` + `resultBannerLabel` (Designer Panel + Label) — PASS/FAIL banner.
- `MainWindow.UpdateEmptyState()` — drives guide visibility from image/recipe state.
- `MainWindow.SetResultBanner(int okCount, int ngCount, bool measured)` — drives banner color/text.

Sibling plan 01-02 (Wave 2) will additionally produce (for the full Phase 1 artifact set):
- `_tolerancePreviewLabel` + `RecipeEditor.UpdateTolerancePreview()` — live tolerance range display (GUI-03).
- `_trialMeasureButton` + `RecipeEditor.OnTrialMeasure` + the `Func<MeasurementTool,ToolRunResult>` trial delegate wired in `MainWindow.OpenRecipeEditor` — in-editor trial measure (GUI-04).
</artifacts_this_phase_produces>


<!-- ===================================================== -->
# 原始檔：.planning/phases/01-operator-experience/01-02-PLAN.md
<!-- ===================================================== -->

---
phase: 01-operator-experience
plan: 02
type: execute
wave: 2
depends_on: [01-01]
files_modified:
  - src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs
  - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
autonomous: false
requirements: [GUI-03, GUI-04]
user_setup: []

must_haves:
  truths:
    # GUI-03 (N5)
    - "While editing a tool's Nominal/Lower/Upper tolerances, a read-only label under _upperNumeric shows the actual range, e.g. '= [49.9900, 50.0100] mm', updating live."
    - "When Upper tolerance is less than Lower tolerance, the label turns red and shows a reversed-range warning."
    # GUI-04 (A1)
    - "From the recipe editor, with a circle or line tool selected and an image loaded, the operator can click a trial-measure button and see the fit (circle/line + value) drawn on the shared main-window image without leaving the editor."
    - "If the ROI finds no edge, the trial shows a 'no edge' message instead of crashing; repeated trials leave no overlay residue or memory leak."
  artifacts:
    - "RecipeEditor field `_tolerancePreviewLabel` (read-only Label) placed directly under `_upperNumeric` in FillToleranceGroup."
    - "RecipeEditor method `UpdateTolerancePreview()` (called from WriteTolerance and PopulateFromTool)."
    - "RecipeEditor field `_trialMeasureButton` (button '[在此試測]') in the toolbar, enabled only for circle/line + image-loaded + delegate-present."
    - "RecipeEditor handler `OnTrialMeasure` that invokes the trial delegate and draws the result on `_imageHelper` via SetPersistentOverlayAction."
    - "RecipeEditor constructor gains a 5th parameter `Func<MeasurementTool, ToolRunResult> trialMeasure` (nullable)."
    - "MainWindow.OpenRecipeEditor builds the trial delegate (transient single-tool recipe → `_recipeRunner.Run`) and passes it to the editor."
  key_links:
    - "`UpdateTolerancePreview()` uses the EXISTING Domain `ToleranceSpec.LowerLimit` / `UpperLimit` (Nominal + tolerance) and the existing inverted-range predicate `UpperTolerance < LowerTolerance` (same one RecipeValidator already uses). No new Domain logic."
    - "The trial delegate MUST run a single tool via `_recipeRunner.Run(transientRecipe, currentImage, hasMatch:false, ...)` — it MUST NOT call EnsureRecipeValid and MUST NOT re-run the whole recipe (per Phase 1 planning note)."
    - "Trial overlay COEXISTENCE: the single SetPersistentOverlayAction slot is replaced by the trial action, which must repaint BOTH the ROI rectangle AND the fit result; the rect2 edit handles remain on top (drawn by HWindowControlHelper, above the persistent overlay)."
    - "All HALCON measure/display in the trial path runs on the UI thread (it is invoked from the button click handler)."
---

<objective>
Deliver the "tune a recipe without leaving the editor" half of Phase 1: live tolerance
upper/lower limit display (GUI-03 / N5) and in-editor trial measure (GUI-04 / A1). Both edit
RecipeEditor; A1 also adds a trial-measure delegate in MainWindow that reuses RecipeRunner's
per-tool measure logic on the current image.

Purpose: An operator editing tolerances should not have to mental-math the actual [lower, upper]
range or discover a reversed range only at run time; and after framing a ROI the operator should
see the fit immediately without closing the editor, returning to the main window, and running the
whole recipe.

Output: One read-only tolerance-preview Label + updater; one trial-measure button + handler in
RecipeEditor; one trial delegate constructed in MainWindow.OpenRecipeEditor and passed through a
new RecipeEditor constructor parameter.

Design decisions (lifted verbatim from GUI計畫書 §N5 + §A1, Option A for both):
- N5 Option A = read-only Label that computes live (NOT a save-blocker). Reason: zero risk, pure
  display, in-place foolproofing. Reversed range warns but does not block.
- A1 Option A = MainWindow passes a trial-measure delegate to the editor (NOT injecting the
  adapters directly). Reason: respects layering (HALCON stays in App layer, owned by MainWindow),
  editor does not depend on adapters, minimal coupling.

NOTE on N1: recipe validation (RecipeValidator + EnsureRecipeValid) is ALREADY shipped (merged
main 8abaa99). This plan must NOT re-implement or re-run it. A1's trial runs ONE tool only.
NOTE on testability: N5 has no new testable Domain logic (ToleranceSpec.LowerLimit/UpperLimit and
the inverted-range predicate already exist and are exercised). Per AGENTS.md ("no speculative
abstractions") and GUI計畫書 §N5 Option A ("純顯示"), do NOT extract anything to Domain and do NOT
add a test suite for N5. Verification is build + existing Tests.exe + manual GUI.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/REQUIREMENTS.md
@.planning/phases/01-operator-experience/01-01-PLAN.md   # wave-1 sibling; shares MainWindow.cs — execute after it
</context>

<tasks>

<task type="auto">
  <name>Task 1: Live tolerance upper/lower limit display (GUI-03 / N5)</name>
  <read_first>
    - docs/superpowers/plans/GUI建議優化項目計畫書.md  →  §N5 (編輯器即時顯示公差上下限): 具體做法 / 選項A / 影響檔案 / 風險 / 驗證操作 (3 steps). IMPLEMENT OPTION A VERBATIM.
    - src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs  →  lines 94-104 (_toleranceGroup fields incl. _nominalNumeric/_lowerNumeric/_upperNumeric/_unitTextBox/_angleHintLabel), 379-408 (FillToleranceGroup: Nominal/Lower/Upper rows then Unit then angleHint), 516-518 (ValueChanged → WriteTolerance), 567-574 (WriteTolerance writes Nominal/LowerTolerance/UpperTolerance), 973-1025 (PopulateFromTool sets the numerics under _updatingControls guard). This is the file being modified.
    - src/FlashMeasurementSystem.Domain/Tolerance/ToleranceSpec.cs  →  lines 4-15: class ToleranceSpec has `Nominal`, `LowerTolerance`, `UpperTolerance`, `Unit`, and computed `LowerLimit` (= Nominal + LowerTolerance) / `UpperLimit` (= Nominal + UpperTolerance). USE THESE; do not recompute.
    - src/FlashMeasurementSystem.Domain/Roi/RecipeValidator.cs  →  line 92: the inverted-range predicate is `tol.UpperTolerance < tol.LowerTolerance`. Reuse the SAME predicate for consistency.
  </read_first>
  <files>src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs</files>
  <action>
    Implement N5 Option A per GUI計畫書 §N5 (read-only Label, live compute, warn-but-do-not-block).

    1) Field — add `private Label _tolerancePreviewLabel;` next to the other tolerance fields (~line 97).

    2) FillToleranceGroup (~line 379) — insert the preview label IMMEDIATELY AFTER the
       `_upperNumeric` row (after line 386) and BEFORE `_unitTextBox` (line 388), so it sits directly
       under the Upper input (spec: "在 _upperNumeric 下方加一個 Label"):
         _tolerancePreviewLabel = AddRow(t, "", ref r, new Label {
             Dock = DockStyle.Fill,
             TextAlign = ContentAlignment.MiddleLeft,
             ForeColor = SystemColors.GrayText,
             Text = "= [-, -] mm"
         });

    3) Add `private void UpdateTolerancePreview()`:
         - Guard: `if (_selectedTool == null || _selectedTool.Tolerance == null) return;`
         - Read `var tol = _selectedTool.Tolerance;`
         - `bool inverted = tol.UpperTolerance < tol.LowerTolerance;`  // SAME predicate as RecipeValidator:92
         - `string unit = string.IsNullOrEmpty(tol.Unit) ? "mm" : tol.Unit;`
         - If inverted:
             _tolerancePreviewLabel.ForeColor = Color.DarkRed;
             _tolerancePreviewLabel.Text = "⚠ 上限 < 下限 (Upper < Lower)";
         - Else:
             _tolerancePreviewLabel.ForeColor = SystemColors.GrayText;
             _tolerancePreviewLabel.Text = string.Format(CultureInfo.InvariantCulture,
                 "= [{0:F4}, {1:F4}] {2}", tol.LowerLimit, tol.UpperLimit, unit);
         (`LowerLimit` / `UpperLimit` are the EXISTING ToleranceSpec computed properties — do not recompute Nominal + tolerance here.)

    4) Wire:
       - At the END of `WriteTolerance()` (after `MarkDirty();`, ~line 573), call `UpdateTolerancePreview();`.
       - At the END of `PopulateFromTool` (after the numerics are populated under the `_updatingControls`
         guard — ValueChanged is suppressed there, so the preview would otherwise go stale), call
         `UpdateTolerancePreview();`. (The guard does not affect a direct call to UpdateTolerancePreview.)
       - The existing `_nominalNumeric/_lowerNumeric/_upperNumeric.ValueChanged → WriteTolerance`
         wiring (lines 516-518) already makes the preview live as the operator types.

    DO NOT extract any logic into Domain, DO NOT add a tolerance-range test suite — the arithmetic
    already lives in ToleranceSpec and the inverted predicate already lives in RecipeValidator
    (AGENTS.md forbids speculative abstraction; GUI計畫書 §N5 Option A is explicit: pure display).
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"</automated>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe</automated>
  </verify>
  <acceptance_criteria>
    - Both builds exit 0; Tests.exe exits 0.
    - No new .cs file and no csproj change (existing file only).
    - grep confirms `_tolerancePreviewLabel` is declared, added in FillToleranceGroup right after `_upperNumeric`, and that `UpdateTolerancePreview` is called from BOTH `WriteTolerance` and `PopulateFromTool`.
    - grep confirms UpdateTolerancePreview uses `tol.LowerLimit` / `tol.UpperLimit` and the predicate `UpperTolerance < LowerTolerance` (no recomputation).
    - Manual (GUI, see checkpoint): Nominal=50/Lower=-0.01/Upper=0.01 → "= [49.9900, 50.0100] mm"; Lower raised above Upper → red "⚠ 上限 < 下限 (Upper < Lower)"; restored → gray range.
  </acceptance_criteria>
  <done>Read-only tolerance-preview label sits under _upperNumeric, shows the live [lower, upper] range using existing ToleranceSpec properties, and warns red on a reversed range — per GUI計畫書 §N5 Option A.</done>
</task>

<task type="auto">
  <name>Task 2: In-editor trial measure (GUI-04 / A1)</name>
  <read_first>
    - docs/superpowers/plans/GUI建議優化項目計畫書.md  →  §A1 (編輯器內試測): 具體做法 / 選項A / 影響檔案 / 風險 (overlay slot!) / 驗證操作 (3 steps). IMPLEMENT OPTION A VERBATIM.
    - src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs  →  lines 21-43 (class fields; _imageHelper, _selectedTool), 106-133 (constructors: 4-arg overload `: this(...)` at 108 + impl at 110), 188-253 (BuildToolbar: button creation + bar.Controls.Add order; _deleteButton ~227, _saveButton ~229), 884-919 (ShowRoiEdit: BeginRect2Edit for circle/line; ClearSelectionHighlight for ref tools), 947-971 (OnToolRect2Changed), 973-1025 (PopulateFromTool — where to refresh button Enabled). This is the file being modified.
    - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs  →  lines 1549-1579 (OpenRecipeEditor: constructs `new RecipeEditor(_imageHelper, _loadedRecipe, _loadedRecipePath, savedCallback)` at 1564 then editor.Show(this) at 1578), 1424-1429 (ResolvePixelSize: out pxUmX/pxUmY), 77/99 (_recipeRunner field + ctor). This is the file being modified.
    - src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs  →  lines 37-60 (ToolRunResult fields: ToolType, Measured, FitCenterRow/Col/RadiusPx, LineRow1/Col1/Row2/Col2, ValueText, Roi), 90-152 (Run: Pass 1 measures circle/line elements; for a transient single circle/line tool it returns exactly one result). The trial delegate reuses this.
    - src/FlashMeasurementSystem.Domain/Roi/Recipe.cs  →  HasReferencePose, Tools (List<MeasurementTool>), Recipe.Default() — used to build the transient single-tool recipe.
    - src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs  →  lines 35-39 + 23: SetPersistentOverlayAction (single slot, replaces previous); Annotator getter; layering comment (persistent overlay < selection highlight < edit handles).
    - CLAUDE.md → "single SetPersistentOverlayAction slot ... a feature that draws its own overlay must repaint all layers it wants visible".
  </read_first>
  <files>src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs, src/FlashMeasurementSystem.App.Wpf/MainWindow.cs</files>
  <action>
    Implement A1 Option A per GUI計畫書 §A1 (MainWindow passes a trial-measure delegate to the editor).

    1) RecipeEditor.cs — constructor change:
       - Add field `private readonly Func<MeasurementTool, ToolRunResult> _trialMeasure;`
         and `using System;` is already present (line 1) for Func.
       - Change the 4-arg overload (line 108) to forward a null delegate:
           public RecipeEditor(HWindowControlHelper imageHelper) : this(imageHelper, null, null, null, null) { }
       - Change the impl constructor (line 110) signature to accept the delegate as a 5th param:
           public RecipeEditor(HWindowControlHelper imageHelper, Recipe recipe, string filePath,
               Action<Recipe, string> savedCallback, Func<MeasurementTool, ToolRunResult> trialMeasure)
         Store `_trialMeasure = trialMeasure;` (may be null — button stays disabled).
       - If any other call site constructs RecipeEditor, update it (grep `new RecipeEditor(`).

    2) RecipeEditor.cs — toolbar button (BuildToolbar, ~line 188):
       - Add field `private Button _trialMeasureButton;`.
       - Create it near _deleteButton (~line 227):
           _trialMeasureButton = new Button { Text = "[在此試測]", Width = 90, Enabled = false };
           _trialMeasureButton.Click += OnTrialMeasure;
       - Add to the FlowLayoutPanel AFTER `_saveAsButton` (line 250) so it appears at the end:
           bar.Controls.Add(_trialMeasureButton);
       - Add a tooltip in SetupToolTips: "_trialMeasureButton" → "試測目前選中的 circle/line 工具（需已載入影像）".

    3) RecipeEditor.cs — button enable state. Add `private void RefreshTrialButtonEnabled()`:
           _trialMeasureButton.Enabled =
               _trialMeasure != null
               && _imageHelper != null && _imageHelper.CurrentImage != null
               && _selectedTool != null
               && (_selectedTool.ToolType == "circle" || _selectedTool.ToolType == "line");
       Call it from: OnToolSelectionChanged (after _selectedTool is set), PopulateFromTool (end),
       and RefreshToolList. (Spec §A1: "按鈕只在選中 circle/line 工具且已載入影像時可用".)

    4) RecipeEditor.cs — OnTrialMeasure handler:
           private void OnTrialMeasure(object sender, EventArgs e)
           {
               var tool = _selectedTool;
               if (tool == null || _trialMeasure == null) return;
               ToolRunResult result = null;
               try { result = _trialMeasure(tool); }
               catch (Exception ex)
               {
                   MessageBox.Show(this, "試測失敗：" + ex.Message, "Trial Measure",
                       MessageBoxButtons.OK, MessageBoxIcon.Warning);
                   return;
               }
               // 單一 overlay slot：試測結果必須同時重畫 ROI 框 + 擬合結果（編輯把手由 helper 畫在最上層）。
               var roi = tool.Roi;
               _imageHelper.SetPersistentOverlayAction(() =>
               {
                   OverlayAnnotator an = _imageHelper.Annotator;
                   if (an == null) return;
                   if (roi != null)
                       an.DrawRectangle2(roi.CenterRow, roi.CenterCol, roi.AngleRad,
                                         roi.Length1, roi.Length2, "orange");
                   if (result == null || !result.Measured)
                   {
                       an.DrawText("未偵測到邊緣 / No edge detected",
                           roi != null ? (int)roi.CenterRow : 20,
                           roi != null ? (int)roi.CenterCol : 20, "yellow");
                       return;
                   }
                   if (result.ToolType == "circle")
                       an.DrawCircle(result.FitCenterRow, result.FitCenterCol, result.FitRadiusPx, "green");
                   else if (result.ToolType == "line")
                       an.DrawLine(result.LineRow1, result.LineCol1, result.LineRow2, result.LineCol2, "green");
                   an.DrawText(result.ValueText ?? string.Empty,
                       roi != null ? (int)roi.CenterRow : 20,
                       roi != null ? (int)roi.CenterCol + 18 : 20, "green");
               });
           }
       (OverlayAnnotator method names DrawRectangle2/DrawCircle/DrawLine/DrawText + signature
       (row, col, …) are confirmed from MainWindow.DrawRecipeResults at lines 1361-1408.)

    5) MainWindow.cs — build the delegate in OpenRecipeEditor (~line 1564) and pass it as the 5th arg:
           Func<MeasurementTool, ToolRunResult> trialMeasure = (tool) =>
           {
               if (tool == null || _imageHelper == null || _imageHelper.CurrentImage == null) return null;
               ResolvePixelSize(out double pxUmX, out double pxUmY, out _);
               var single = Recipe.Default();
               single.HasReferencePose = false;            // 試測不套用匹配姿態變換，ROI 即影像座標
               single.Tools = new System.Collections.Generic.List<MeasurementTool> { tool };
               // 注意：只跑這一個工具，不呼叫 EnsureRecipeValid、不重跑整份配方。
               var list = _recipeRunner.Run(single, _imageHelper.CurrentImage,
                   false, 0.0, 0.0, 0.0, pxUmX, pxUmY);
               return list.Count > 0 ? list[0] : null;
           };
           var editor = new RecipeEditor(_imageHelper, _loadedRecipe, _loadedRecipePath,
               /*savedCallback*/ (recipe, path) => { …existing body… }, trialMeasure);
       Keep the existing savedCallback body (lines 1565-1572) unchanged. Ensure `MeasurementTool`
       and `Recipe` are in scope (add `using FlashMeasurementSystem.Domain.Roi;` if not already).
       The delegate MUST NOT call EnsureRecipeValid and MUST NOT re-run the whole recipe (per
       planning note + spec §A1 "跑一次該工具").
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"</automated>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe</automated>
  </verify>
  <acceptance_criteria>
    - Both builds exit 0; Tests.exe exits 0.
    - No new .cs file and no csproj change.
    - grep confirms: RecipeEditor ctor has 5 params incl. `Func<MeasurementTool, ToolRunResult> trialMeasure`; the 4-arg overload forwards `null`; `_trialMeasureButton` + `OnTrialMeasure` + `RefreshTrialButtonEnabled` exist; `new RecipeEditor(` in MainWindow passes a 5th delegate argument.
    - grep confirms the MainWindow delegate builds a transient `Recipe.Default()` with `HasReferencePose=false`, exactly one tool, calls `_recipeRunner.Run`, and does NOT call `EnsureRecipeValid`.
    - grep confirms OnTrialMeasure's SetPersistentOverlayAction redraws BOTH the ROI rectangle AND the fit result (overlay coexistence per CLAUDE.md single-slot rule).
    - Manual (GUI, see checkpoint): select circle tool, frame ROI on a real edge, click [在此試測] → green fit circle + value drawn on the main image; move ROI off-target → 試測 → "未偵測到邊緣" message, no crash; click 試測 several times → no overlay residue, memory stays flat.
  </acceptance_criteria>
  <done>RecipeEditor has a [在此試測] button (circle/line + image only) that calls a MainWindow-supplied delegate running one tool via RecipeRunner and draws the fit on the shared image; overlay coexists with the rect2 edit handles; no whole-recipe re-run, no EnsureRecipeValid — per GUI計畫書 §A1 Option A.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: GUI acceptance — tolerance preview + trial measure</name>
  <read_first>
    - docs/superpowers/plans/GUI建議優化項目計畫書.md  →  §N5 驗證操作 (3 steps) + §A1 驗證操作 (3 steps).
  </read_first>
  <files>n/a — verification only (no source changes in this task)</files>
  <action>
    Build x64, then launch `src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`
    (close before rebuilding), load an image, open the Recipe Editor, and run the human verification
    procedure in <how-to-verify>. This is a checkpoint task: the work is performed by the human operator.
  </action>
  <what-built>
    Live tolerance [lower, upper] preview under the Upper input with red reversed-range warning
    (GUI-03); and an in-editor [在此試測] button that runs the selected circle/line tool once on
    the current image and draws the fit on the shared main-window image (GUI-04).
  </what-built>
  <how-to-verify>
    Build x64 first, then launch `src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`
    (close before rebuilding). Load an image, open the Recipe Editor.

    N5 (§N5 驗證操作):
    1. Select a tool → set Nominal=50, Lower=-0.01, Upper=0.01 → preview label shows "= [49.9900, 50.0100] mm".
    2. Raise Lower above Upper (e.g. Lower=+0.02) → label turns red, shows "⚠ 上限 < 下限 (Upper < Lower)".
    3. Restore Lower below Upper → label returns to gray range.

    A1 (§A1 驗證操作):
    1. Select a circle tool, frame its ROI on a real circular edge → click [在此試測] → the fitted circle + value are drawn on the main image (button is only enabled for circle/line + image loaded).
    2. Move the ROI off the target onto empty area → click [在此試測] → "未偵測到邊緣" message appears; no crash.
    3. Click [在此試測] several times in a row → only the latest result is shown (no overlay stacking); observe Task Manager memory stays roughly flat (no leak).
    Also confirm: switching to a non-circle/line tool disables the button; the rect2 ROI drag handles still work after a trial (edit handles stay on top of the trial overlay).
  </how-to-verify>
  <verify>
    <human-check>Operator observes: tolerance preview shows "= [49.9900, 50.0100] mm" and turns red on reversed range; [在此試測] draws the fit on the main image for circle/line, shows "未偵測到邊緣" on a bad ROI without crashing, and leaves no residue/leak across repeated trials — per spec §N5 + §A1 verification sequences.</human-check>
  </verify>
  <done>Operator types "approved" after both the N5 (3-step) and A1 (3-step) verification sequences pass; no crash, no overlay residue, ROI edit handles still functional.</done>
  <resume-signal>Type "approved" or describe the issues to fix.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Operator → local WinForms UI (RecipeEditor) | Tolerance numerics are operator-entered trusted values; the trial measure runs HALCON on the operator-loaded in-memory image. No network, no auth, no untrusted external input. |

## STRIDE Threat Register

This plan adds one read-only display label and one button whose handler invokes an existing
HALCON-based runner on the current image. It introduces no new persistence, network, or package
install. Residual risk is operator-facing reliability (HALCON exceptions, overlay/resource leaks).

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-01-04 | Denial of Service | `OnTrialMeasure` → `_recipeRunner.Run` on a bad ROI | medium | mitigate | Wrap the delegate call in try/catch in OnTrialMeasure; show a warning MessageBox instead of letting a HALCON exception kill the editor. RecipeRunner already maps HalconException to a non-fatal ToolRunResult (Measured=false). |
| T-01-05 | Denial of Service | single SetPersistentOverlayAction slot / overlay residue | low | mitigate | Each trial replaces the slot (old action discarded); the action redraws ROI + result together so no stale layers. No HXLDCont/HObject is allocated by the overlay (OverlayAnnotator draws primitives each redraw) → no HALCON-object leak path. |
| T-01-06 | Information Disclosure | `_tolerancePreviewLabel` | low | accept | Displays only computed tolerance bounds the operator already entered — no secrets, no file paths. |
| T-01-07 | Tampering | n/a — no package install | low | accept | No npm/pip/cargo/NuGet install in this plan (edits to existing source only); package-legitimacy gate T-01-SC is not applicable. |
</threat_model>

<verification>
Phase-level checks for this plan (Wave 2; run AFTER 01-01 since both touch MainWindow.cs):
- `dotnet build ... /p:Platform="Any CPU"` exits 0.
- `dotnet build ... /p:Platform=x64` exits 0 (HALCON trial-measure path verified under x64).
- `FlashMeasurementSystem.Tests.exe` exits 0 (Domain/Application contracts unaffected; ToleranceSpec properties and RecipeValidator predicate reused unchanged).
- Manual GUI acceptance (Task 3) passes both N5 and A1 spec verification sequences, including the no-crash and no-residue/no-leak checks.
</verification>

<success_criteria>
- GUI-03 (N5): operator sees the live [lower, upper] range under the Upper input while editing, with a red reversed-range warning.
- GUI-04 (A1): operator can trial-measure the selected circle/line tool from inside the editor and see the fit on the shared image, without leaving the editor, re-running the whole recipe, or triggering EnsureRecipeValid.
- No regression to editor save/load, ROI edit handles, or Run Recipe / 一鍵量測 flows.
</success_criteria>

<output>
Create `.planning/phases/01-operator-experience/01-02-SUMMARY.md` when done (wave 2, both tasks + GUI acceptance).
</output>

<artifacts_this_phase_produces>
This plan (01-02) produces:
- `RecipeEditor._tolerancePreviewLabel` + `RecipeEditor.UpdateTolerancePreview()` — GUI-03.
- `RecipeEditor._trialMeasureButton` + `RecipeEditor.OnTrialMeasure` + `RecipeEditor.RefreshTrialButtonEnabled()` — GUI-04 (editor side).
- `RecipeEditor` constructor 5th parameter `Func<MeasurementTool, ToolRunResult> trialMeasure` — GUI-04 (contract).
- `MainWindow.OpenRecipeEditor` trial-measure delegate (transient single-tool recipe → `_recipeRunner.Run`) — GUI-04 (App side).

Full Phase 1 artifact set = this plan's artifacts UNION sibling plan 01-01's artifacts
(`emptyStateGuideLabel`, `UpdateEmptyState`, `resultBannerPanel`/`resultBannerLabel`, `SetResultBanner`).
</artifacts_this_phase_produces>


<!-- ===================================================== -->
# 原始檔：.planning/phases/02-2d-metrology-model/02-01-PLAN.md
<!-- ===================================================== -->

---
phase: 02-2d-metrology-model
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyObjectType.cs
  - src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyObjectDef.cs
  - src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyModelDef.cs
  - src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyObjectResult.cs
  - src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyModelResult.cs
  - src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj
  - src/FlashMeasurementSystem.Application/MetrologyModel/IMetrologyModelRunner.cs
  - src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj
  - src/FlashMeasurementSystem.Domain/Roi/Recipe.cs
  - tests/FlashMeasurementSystem.Tests/MetrologyModelDomainTests.cs
  - tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
  - tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj
  - tests/FlashMeasurementSystem.Tests.Halcon/MetrologyModelHalconTests.cs
  - tests/FlashMeasurementSystem.Tests.Halcon/Program.cs
  - tests/FlashMeasurementSystem.Tests.Halcon/FlashMeasurementSystem.Tests.Halcon.csproj
autonomous: true
requirements: [MET2D-01, MET2D-03]
user_setup: []

must_haves:
  truths:
    # MET2D-01 (Domain contract for auto-distribution)
    - "MetrologyObjectDef carries nominal geometry for all four shapes plus measure params (MeasureDistance default 10, MeasureLength1 20, MeasureLength2 5, Sigma 1, Threshold 30) so HALCON can auto-distribute measure rectangles."
    - "Each shape type exposes its HALCON minimum measure-region count (line 2, circle 3, ellipse 5, rectangle 8) and Tests.exe asserts those minimums."
    # MET2D-03 (additive, backward-compatible recipe schema)
    - "A v5 recipe JSON with no MetrologyModel field loads via RecipeStore with MetrologyModel == null and no exception (backward compatible)."
    - "A recipe carrying a MetrologyModelDef round-trips through RecipeStore Save/Load preserving object count and every nominal/measure field."
  artifacts:
    - "Domain enum MetrologyObjectType { Line, Circle, Ellipse, Rectangle }."
    - "Domain DTOs MetrologyObjectDef, MetrologyModelDef, MetrologyObjectResult, MetrologyModelResult (plain serializable types, no HALCON)."
    - "Application interface IMetrologyModelRunner expressed only in Domain types (+ a single image type parameter)."
    - "Recipe.MetrologyModel nullable field (default null); SchemaVersion default bumped 5 -> 6."
    - "Test suite MetrologyModelDomainTests (Run()) wired into Tests Main(); compiling MetrologyModelHalconTests stub wired into Tests.Halcon Program.cs."
  key_links:
    - "Every new .cs file has an explicit <Compile Include> entry in its old-style .csproj (Domain, Application, Tests, Tests.Halcon) — otherwise dotnet build silently excludes it."
    - "MetrologyModelDomainTests.Run() is called from EdgeDetectionDomainTests.Main() alongside the existing *.Run() calls (~line 148)."
    - "Recipe schema change is additive only: no migration code, null field deserializes transparently; existing Tools/Pass behaviour untouched."
---

<objective>
Establish the Domain + Application contracts and backward-compatible recipe schema for the 2D
metrology model, and create the Wave-0 test scaffolds the later waves turn green. This is the
"feature adapter" pattern's first two layers (Domain DTOs -> Application interface) plus the
additive Recipe v6 field, with no HALCON yet.

Purpose: lock the data shapes and the coexistence contract (old `.zcp` still loads) before any
HALCON code exists, so the adapter (02-02) and the RecipeRunner integration (02-03) build against
stable types and the test suites exist from the start (Nyquist).

Output: 5 Domain DTOs, 1 Application interface, the additive `Recipe.MetrologyModel` field
(SchemaVersion 5 -> 6), a passing Domain test suite (MET2D-01 region minimums, MET2D-03 recipe
round-trip + backward compat), and a compiling HALCON test stub wired in for 02-02 to fill.

Design decisions (Claude's discretion per CONTEXT.md, grounded in 02-RESEARCH.md "MET2D-03
Concrete Integration Design"):
- DTO shapes follow the research skeleton verbatim (one flat MetrologyObjectDef with per-shape
  fields; unused fields ignored per Shape). This matches existing Result DTOs (Ellipse/Rectangle).
- Metrology model is a single additive Recipe field (NOT a MeasurementTool), so it sits beside the
  1D Tools list and never collides with the existing Pass 1/1.5/1.7/2 tool loops.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/REQUIREMENTS.md
@.planning/phases/02-2d-metrology-model/02-CONTEXT.md
@.planning/phases/02-2d-metrology-model/02-RESEARCH.md
@.planning/phases/02-2d-metrology-model/02-VALIDATION.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Domain DTOs for the metrology model (MET2D-01)</name>
  <read_first>
    - .planning/phases/02-2d-metrology-model/02-RESEARCH.md  ->  "MET2D-03 Concrete Integration Design" (L511-622): the MetrologyObjectDef / MetrologyModelDef / MetrologyObjectResult / MetrologyModelResult skeletons. Implement these field-for-field.
    - .planning/phases/02-2d-metrology-model/02-RESEARCH.md  ->  operator tables L159-268 for the minimum measure-region counts (line 2, circle 3, ellipse 5, rectangle 8) and the MeasureLength1 restrictions per shape.
    - src/FlashMeasurementSystem.Domain/EllipseFitting/EllipseFittingResult.cs  ->  confirm Phi / Radius1 / Radius2 field naming convention to mirror.
    - src/FlashMeasurementSystem.Domain/RectangleFitting/RectangleFittingResult.cs  ->  confirm Phi / Length1 / Length2 naming convention to mirror.
    - src/FlashMeasurementSystem.Domain/Tolerance/ToleranceSpec.cs  ->  the existing ToleranceSpec type referenced by MetrologyObjectDef.Tolerance.
    - src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj  ->  lines ~65-66 (CircleFitting Compile Include pattern) — copy this pattern for the new files.
    - CLAUDE.md  ->  "strict one-way layering" (Domain has NO HALCON/UI/IO refs) and "old-style .csproj — new files need explicit <Compile Include>".
  </read_first>
  <files>src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyObjectType.cs, src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyObjectDef.cs, src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyModelDef.cs, src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyObjectResult.cs, src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyModelResult.cs, src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj</files>
  <action>
    Create the five Domain types under namespace FlashMeasurementSystem.Domain.MetrologyModel, following
    the research skeleton (02-RESEARCH.md L511-622) field-for-field. Pure data only — no HALCON, UI, or IO.

    1) MetrologyObjectType.cs — enum with members Line, Circle, Ellipse, Rectangle.

    2) MetrologyObjectDef.cs — one object's nominal geometry + measure params:
       - Identity: Id (string, default ""), Name (string, default ""), Shape (MetrologyObjectType, default Line).
       - Line nominal: RowBegin, ColumnBegin, RowEnd, ColumnEnd (double).
       - Circle/Ellipse/Rectangle centre: Row, Column (double). Circle: Radius (double).
         Ellipse/Rectangle: Phi (double, radians), Radius1/Radius2 (ellipse half axes), Length1/Length2 (rect half edges).
       - Measure params with defaults: MeasureLength1=20.0, MeasureLength2=5.0, MeasureSigma=1.0,
         MeasureThreshold=30.0, MeasureDistance=10.0, NumMeasures=0 (0 = use MeasureDistance).
       - Optional Tolerance (ToleranceSpec, default null) — reuse the existing Domain ToleranceSpec.
       - Add a static helper int MinMeasureRegions(MetrologyObjectType shape) returning the HALCON
         minimum (Line 2, Circle 3, Ellipse 5, Rectangle 8). Document each value with the operator
         reference line from 02-RESEARCH.md. Keep it on this DTO (Domain-pure), used by the tests.

    3) MetrologyModelDef.cs — List<MetrologyObjectDef> Objects (default new list) + ImageWidth /
       ImageHeight int hints (default 0 = query at apply time).

    4) MetrologyObjectResult.cs — fitted output for one object: Id, Name, Shape, bool Success,
       double Score, string ErrorMessage (default ""); fitted-geometry fields mirroring the def
       (FitRowBegin/FitColumnBegin/FitRowEnd/FitColumnEnd for line; FitRow/FitColumn/FitRadius for
       circle; FitPhi/FitRadius1/FitRadius2 for ellipse; FitLength1/FitLength2 for rectangle);
       List<double> MeasurePointRows / MeasurePointCols (default empty); bool? IsOk (default null);
       string ValueText (default "").

    5) MetrologyModelResult.cs — List<MetrologyObjectResult> Objects (default new list) + string
       ErrorMessage (default "") for whole-batch failures.

    6) Add an explicit <Compile Include="MetrologyModel\...cs" /> entry for each of the five new
       files in FlashMeasurementSystem.Domain.csproj, mirroring the CircleFitting entries (~L65-66).
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"</automated>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
  </verify>
  <acceptance_criteria>
    - Both builds exit 0 (0 error(s)).
    - grep confirms all five files exist under src/FlashMeasurementSystem.Domain/MetrologyModel/ and each has a matching <Compile Include> entry in the Domain .csproj.
    - grep confirms MetrologyObjectDef defaults: MeasureLength1 20.0, MeasureLength2 5.0, MeasureDistance 10.0, MeasureThreshold 30.0; and MinMeasureRegions returns 2/3/5/8 for Line/Circle/Ellipse/Rectangle.
    - grep confirms no `using HalconDotNet` / `System.Windows.Forms` / `System.IO` in any of the five Domain files (layering preserved).
  </acceptance_criteria>
  <done>Five pure-Domain metrology DTOs exist, compile under Any CPU + x64, and are registered in the Domain csproj; measure-param defaults and per-shape minimum region counts match the HALCON reference.</done>
</task>

<task type="auto">
  <name>Task 2: Application interface + additive Recipe v6 field (MET2D-03)</name>
  <read_first>
    - .planning/phases/02-2d-metrology-model/02-RESEARCH.md  ->  "Application Interface" (L624-638): IMetrologyModelRunner signature (model, ref pose, image, match pose, hasMatch). Implement an image-type-parameterised interface so Application stays HALCON-free.
    - src/FlashMeasurementSystem.Application/CircleFitting/ICircleFitter.cs  ->  confirm the existing interface-over-Domain-types convention to mirror namespace + style.
    - src/FlashMeasurementSystem.Application/EdgeDetection/IEdgeDetector.cs  ->  confirm how an image is parameterised generically (IEdgeDetector<TImage>) so Application avoids HalconDotNet.
    - src/FlashMeasurementSystem.Domain/Roi/Recipe.cs  ->  lines 12-44 (SchemaVersion=5 at L20; the v2..v5 comment block; Tools list; Default()). This is the file being modified — add ONE field, bump the version, leave everything else untouched.
    - src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj  ->  the Compile Include pattern for a new interface file.
    - CLAUDE.md  ->  "Application — feature interfaces expressed only in Domain types. May depend on Domain." (no HALCON in Application).
  </read_first>
  <files>src/FlashMeasurementSystem.Application/MetrologyModel/IMetrologyModelRunner.cs, src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj, src/FlashMeasurementSystem.Domain/Roi/Recipe.cs</files>
  <action>
    1) IMetrologyModelRunner.cs under namespace FlashMeasurementSystem.Application.MetrologyModel.
       To keep Application HALCON-free, parameterise the image type generically exactly as
       IEdgeDetector<TImage> does (the Halcon adapter in 02-02 will implement IMetrologyModelRunner<HImage>):
         MetrologyModelResult Apply(
             MetrologyModelDef model,
             double refRow, double refCol, double refAngleRad, bool hasReferencePose,
             TImage image,
             double matchRow, double matchCol, double matchAngleRad, bool hasMatch);
       Use MetrologyModelDef / MetrologyModelResult from the Domain namespace created in Task 1.
       Add the <Compile Include="MetrologyModel\IMetrologyModelRunner.cs" /> entry to the Application csproj.

    2) Recipe.cs — additive v6 change ONLY (per D-locked coexistence constraint, MET2D-03):
       - Add a new field after the Tools list (~after L35):
           public MetrologyModelDef MetrologyModel { get; set; } = null;
         with a comment: v6 — additive 2D metrology model; null = no metrology (backward compatible,
         old .zcp deserialize MetrologyModel=null, behaviour unchanged; no migration code).
       - Add `using FlashMeasurementSystem.Domain.MetrologyModel;` at the top.
       - Change the SchemaVersion default initialiser from 5 to 6 (L20). Extend the version-history
         comment block with a v6 line describing the additive field.
       Do NOT touch RefRow/RefCol/HasReferencePose/Tools or any other member. No migration logic.
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"</automated>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe</automated>
  </verify>
  <acceptance_criteria>
    - Both builds exit 0; Tests.exe exits 0 (existing suites still pass — no recipe regression).
    - grep confirms IMetrologyModelRunner is generic over the image type (TImage) and references only Domain/Application types (no HalconDotNet using).
    - grep confirms Recipe.cs now declares `public MetrologyModelDef MetrologyModel` with default null and SchemaVersion default is 6.
    - grep confirms the interface file has a <Compile Include> entry in the Application csproj.
  </acceptance_criteria>
  <done>IMetrologyModelRunner&lt;TImage&gt; exists in Application over Domain types only, and Recipe gains a nullable MetrologyModel field with SchemaVersion 6 — additive and backward compatible; existing tests stay green.</done>
</task>

<task type="auto">
  <name>Task 3: Domain test suite + HALCON test stub wired in (MET2D-01, MET2D-03; Wave-0 scaffolds)</name>
  <read_first>
    - .planning/phases/02-2d-metrology-model/02-VALIDATION.md  ->  "Wave 0 Requirements" (L60-65) and "Per-Task Verification Map" (L43-53). This task delivers the Wave-0 scaffolds.
    - tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs  ->  lines 9-14 (Main() entry) and the *.Run() call block (~L112-148). Wire MetrologyModelDomainTests.Run() in alongside the others.
    - tests/FlashMeasurementSystem.Tests/CsvReportWriterTests.cs  ->  the suite Run() + Assert helper style + how an Infrastructure type (CsvMeasurementReportWriter) is used from the Tests project (confirms Infrastructure project reference is available for RecipeStore).
    - src/FlashMeasurementSystem.Infrastructure/Roi/RecipeStore.cs  ->  Save(recipe, filePath) / Load(filePath) signatures (used for the round-trip + backward-compat tests).
    - tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj  ->  lines 55-74 (Compile Include list) + the Infrastructure ProjectReference (L86-89).
    - tests/FlashMeasurementSystem.Tests.Halcon/Program.cs  ->  lines 9-21 (the suites Action[] array) — add the new suite.
    - tests/FlashMeasurementSystem.Tests.Halcon/CircleFitterTests.cs  ->  the Run()/Assert pattern to mirror for the stub.
  </read_first>
  <files>tests/FlashMeasurementSystem.Tests/MetrologyModelDomainTests.cs, tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs, tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj, tests/FlashMeasurementSystem.Tests.Halcon/MetrologyModelHalconTests.cs, tests/FlashMeasurementSystem.Tests.Halcon/Program.cs, tests/FlashMeasurementSystem.Tests.Halcon/FlashMeasurementSystem.Tests.Halcon.csproj</files>
  <action>
    1) MetrologyModelDomainTests.cs under namespace FlashMeasurementSystem.Tests, public static class
       with `public static void Run()` and a private Assert(bool, string) helper that throws
       InvalidOperationException on failure (mirror CsvReportWriterTests). Cover:
       - DTO defaults: new MetrologyObjectDef() has MeasureLength1==20, MeasureLength2==5,
         MeasureDistance==10, MeasureThreshold==30, NumMeasures==0, Tolerance==null, Shape==Line.
       - Min measure regions (MET2D-01): MetrologyObjectDef.MinMeasureRegions returns 2/3/5/8 for
         Line/Circle/Ellipse/Rectangle.
       - Recipe default (MET2D-03 backward compat): Recipe.Default().MetrologyModel is null and
         SchemaVersion is 6.
       - Backward compat load (MET2D-03): write a temp .zcp containing a minimal v5-style recipe JSON
         that has NO MetrologyModel field (e.g. {"SchemaVersion":5,"RecipeId":"old","Tools":[]}),
         load it via new RecipeStore().Load(path), Assert no exception and loaded.MetrologyModel == null.
         Use a temp path under Path.GetTempPath(); delete it in a finally.
       - Round-trip (MET2D-03): build a Recipe with a MetrologyModel holding two objects (one circle,
         one line) with distinct field values; Save then Load via RecipeStore; Assert Objects.Count==2
         and the circle's Row/Column/Radius and the line's RowBegin/ColumnBegin/RowEnd/ColumnEnd and the
         measure params survive the round-trip.
       - Print "MetrologyModelDomainTests passed" at the end.

    2) EdgeDetectionDomainTests.cs — add `MetrologyModelDomainTests.Run();` into the Main() Run-call
       block next to the existing suites (e.g. after RecipeValidatorTests.Run() ~L146-148). Do not
       remove or reorder existing calls.

    3) MetrologyModelHalconTests.cs under namespace FlashMeasurementSystem.Tests.Halcon — a COMPILING
       Wave-0 stub: public static class with `public static void Run()` that, for now, constructs a
       MetrologyModelDef (proving the Domain types are reachable from this project) and prints
       "MetrologyModelHalconTests (stub) passed". Add a head comment that this stub is filled with
       real synthetic-image assertions in plan 02-02 (the HALCON adapter wave). Do NOT reference
       HalconMetrologyModelRunner yet (it does not exist until 02-02).

    4) Program.cs (Tests.Halcon) — add `MetrologyModelHalconTests.Run,` to the suites Action[] array
       (after TemplateMatcherTests.Run, ~L20).

    5) Add <Compile Include> entries: MetrologyModelDomainTests.cs in the Tests csproj; and
       MetrologyModelHalconTests.cs in the Tests.Halcon csproj.
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe</automated>
  </verify>
  <acceptance_criteria>
    - x64 build exits 0; Tests.exe prints "MetrologyModelDomainTests passed" and exits 0; Tests.Halcon.exe prints "MetrologyModelHalconTests (stub) passed" and exits 0.
    - grep confirms MetrologyModelDomainTests.Run() is called from EdgeDetectionDomainTests.cs Main() and MetrologyModelHalconTests.Run is in the Program.cs suites array.
    - grep confirms both new test files have <Compile Include> entries in their respective csproj.
    - The Domain test exercises a real RecipeStore Save/Load round-trip AND a no-MetrologyModel-field backward-compat load (MET2D-03), and asserts the 2/3/5/8 region minimums (MET2D-01).
  </acceptance_criteria>
  <done>Both metrology test suites exist, are wired into their Main()/Program.cs entry points and csproj files, and run green: Domain tests cover MET2D-01 region minimums + MET2D-03 round-trip/backward-compat; the HALCON stub compiles and reserves its slot for 02-02.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Operator -> recipe file (`.zcp`) on local disk | Recipe JSON (now including an optional MetrologyModelDef) is operator-authored, local, trusted. RecipeStore deserializes it. No network, no auth, no untrusted external input. |

## STRIDE Threat Register

This plan adds pure-data Domain DTOs, one interface, one additive nullable recipe field, and test
scaffolds. No HALCON, no new external input, no network, no package install.

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-02-01 | Tampering | malformed `.zcp` deserialization | low | accept | RecipeStore already wraps JsonConvert in try/catch and throws a typed InvalidOperationException on bad JSON; the additive nullable field cannot widen this surface (null default on missing field). |
| T-02-02 | Denial of Service | recipe with huge MetrologyModel.Objects list | low | accept | Operator-authored local file; same trust as the existing Tools list. No mitigation beyond existing file-open flow. |
| T-02-SC | Tampering | npm/pip/cargo/NuGet install | low | accept | No package install in this plan — only edits to existing source + new source files using already-referenced Newtonsoft (Infrastructure). Package-legitimacy gate not applicable. |
</threat_model>

<verification>
Phase-level checks for this plan (Wave 1):
- `dotnet build ... /p:Platform="Any CPU"` exits 0.
- `dotnet build ... /p:Platform=x64` exits 0.
- `FlashMeasurementSystem.Tests.exe` exits 0 incl. the new MetrologyModelDomainTests (MET2D-01 + MET2D-03).
- `FlashMeasurementSystem.Tests.Halcon.exe` exits 0 incl. the MetrologyModelHalconTests stub.
</verification>

<success_criteria>
- MET2D-01 (Domain): nominal-geometry + measure-param DTOs exist with auto-distribution defaults and per-shape region minimums asserted in Tests.exe.
- MET2D-03: Recipe gains an additive nullable MetrologyModel field (v6); old recipes load with null and new recipes round-trip — both asserted in Tests.exe.
- No regression to existing Domain/Application contracts or the 1D pipeline.
</success_criteria>

<output>
Create `.planning/phases/02-2d-metrology-model/02-01-SUMMARY.md` when done (wave 1, all three tasks).
</output>

<artifacts_this_phase_produces>
This plan (02-01) produces:
- `Domain.MetrologyModel.MetrologyObjectType` (enum).
- `Domain.MetrologyModel.MetrologyObjectDef` (+ static MinMeasureRegions).
- `Domain.MetrologyModel.MetrologyModelDef`.
- `Domain.MetrologyModel.MetrologyObjectResult`.
- `Domain.MetrologyModel.MetrologyModelResult`.
- `Application.MetrologyModel.IMetrologyModelRunner<TImage>`.
- `Recipe.MetrologyModel` additive field (SchemaVersion 6).
- `MetrologyModelDomainTests` (Tests.exe) + `MetrologyModelHalconTests` stub (Tests.Halcon.exe).

Downstream: 02-02 implements HalconMetrologyModelRunner against IMetrologyModelRunner&lt;HImage&gt; and
fills the HALCON test stub; 02-03 wires Pass 3 + the Recipe.MetrologyModel field into RecipeRunner;
02-04 adds the GUI editor that writes Recipe.MetrologyModel.
</artifacts_this_phase_produces>
</output>


<!-- ===================================================== -->
# 原始檔：.planning/phases/02-2d-metrology-model/02-02-PLAN.md
<!-- ===================================================== -->

---
phase: 02-2d-metrology-model
plan: 02
type: execute
wave: 2
depends_on: [02-01]
files_modified:
  - src/FlashMeasurementSystem.Halcon/MetrologyModel/HalconMetrologyModelRunner.cs
  - src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj
  - tests/FlashMeasurementSystem.Tests.Halcon/MetrologyModelHalconTests.cs
  - tests/FlashMeasurementSystem.Tests.Halcon/TestImageGenerator.cs
autonomous: true
requirements: [MET2D-02, MET2D-04]
user_setup: []

must_haves:
  truths:
    # MET2D-02 (robust fit returns params + measure points)
    - "Applying the model to a synthetic white-disc image fits the circle within +/-0.5 px of the painted centre and radius and returns a Score >= 0.6."
    - "Applying the model to a synthetic line / rectangle / ellipse image fits each shape within its tolerance band (line/rect/circle centre +/-0.5..1 px, ellipse +/-1 px, phi +/-0.02 rad) and returns non-empty measure points."
    - "A multi-channel (RGB) image is converted to single channel before apply, so it does not silently return zero edges."
    # MET2D-04 (one apply = many features)
    - "A single Apply() call on a 3-object model (line + circle + ellipse) returns 3 MetrologyObjectResult with Success=true."
    # Robustness
    - "A MeasureLength1 that violates the shape restriction (>= radius / half-edge) yields a failed result for THAT object with a clear message, not a thrown exception that aborts the batch."
  artifacts:
    - "Halcon adapter HalconMetrologyModelRunner : IMetrologyModelRunner<HImage>, the only place metrology HOperatorSet calls live."
    - "Real synthetic-image assertions in MetrologyModelHalconTests (replacing the 02-01 stub body)."
    - "Synthetic-geometry image helpers in TestImageGenerator (filled circle/ellipse/rectangle + thin line) if not already present."
  key_links:
    - "The full create -> set_image_size -> set_reference_system -> add objects -> align -> apply -> query -> clear lifecycle is wrapped in try/finally; clear_metrology_model always runs (no handle leak)."
    - "set_metrology_model_image_size is called BEFORE any add_metrology_object_* (performance + correctness)."
    - "apply_metrology_model receives a single-channel image (CountChannels>1 -> Rgb1ToGray) — same pitfall guard as measure_pos in the 1D pipeline."
    - "num_instances is set to 1 per object so result tuples are read as exactly one instance per shape."
    - "set_metrology_object_fuzzy_param is NEVER called (fuzzy is Phase 3)."
---

<objective>
Implement the HALCON adapter that turns the Domain MetrologyModelDef into fitted
MetrologyModelResult, and fill the Wave-0 HALCON test stub with real synthetic-image assertions.
This is the "feature adapter" pattern's third layer (Halcon adapter) and the integration test.

This plan opens with the one outstanding research verification (02-RESEARCH.md Assumption A1): the
exact GenParam format of `set_metrology_model_param 'reference_system'` was confirmed only from the
solution-guide example, not the operator's full parameter list. Confirm it against the offline
reference at the start of the alignment work, and record the documented fallback (the per-ROI
manual transform the 1D tools already use) before relying on it.

Purpose: deliver MET2D-02 (robust line/circle/ellipse/rectangle fitting + params + measure points)
and MET2D-04 (one apply processes all objects), verified in pixel space on synthetic images with
known ground truth and sub-pixel tolerance bands.

Output: HalconMetrologyModelRunner (HALCON handle lifecycle, single-channel guard, per-object
MeasureLength1 validation, num_instances=1, no fuzzy), and a green MetrologyModelHalconTests suite.

Design decisions (Claude's discretion + 02-RESEARCH.md):
- Build the HALCON handle fresh on every Apply() (create -> ... -> clear in try/finally). Never store
  the handle. Serialize only the Domain DTOs.
- Per-object failure isolation: a bad object (MeasureLength1 >= shape dimension) becomes a failed
  MetrologyObjectResult; the rest of the batch still runs.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/REQUIREMENTS.md
@.planning/phases/02-2d-metrology-model/02-CONTEXT.md
@.planning/phases/02-2d-metrology-model/02-RESEARCH.md
@.planning/phases/02-2d-metrology-model/02-VALIDATION.md
@.planning/phases/02-2d-metrology-model/02-01-PLAN.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Confirm reference_system format, then implement HalconMetrologyModelRunner (MET2D-02, MET2D-04)</name>
  <read_first>
    - halcon_pdf/reference/reference_hdevelop.txt  ->  set_metrology_model_param at L6962 (and surrounding GenParamName list). CONFIRM the 'reference_system' value is a 3-element tuple [RefRow, RefCol, RefAngleRad] before relying on it (02-RESEARCH.md Assumption A1, risk LOW). If the reference text does not list 'reference_system' explicitly, fall back to the documented alternative (see action step 0).
    - halcon_pdf/reference/reference_hdevelop.txt  ->  the verbatim signatures the research already cited: create_metrology_model L5868, set_metrology_model_image_size L6889, add_metrology_object_line_measure L5195, add_metrology_object_circle_measure L4672, add_metrology_object_ellipse_measure L4834, add_metrology_object_rectangle2_measure L5319, align_metrology_model L5448, apply_metrology_model L5652, get_metrology_object_result L6495, get_metrology_object_measures L6150, set_metrology_object_param L7214, clear_metrology_model L5744. (Reference root is the repo-root halcon_pdf/ — one level above this project folder.)
    - .planning/phases/02-2d-metrology-model/02-RESEARCH.md  ->  "Halcon Adapter Skeleton" (L640-724), the per-shape result tuple layouts (L321-329, L472-499), "Common Pitfalls" 1-10 (L838-900), "Anti-Patterns to Avoid" (L1012-1021).
    - src/FlashMeasurementSystem.Halcon/CircleFitting/HalconCircleFitter.cs  ->  the adapter convention: validate inputs, build HObject in using, call HOperatorSet, map outputs to Domain result, convert HalconException to a failed result.
    - src/FlashMeasurementSystem.Application/MetrologyModel/IMetrologyModelRunner.cs  ->  the interface to implement (created in 02-01); implement IMetrologyModelRunner<HImage>.
    - src/FlashMeasurementSystem.Domain/MetrologyModel/  ->  the five DTOs (MetrologyObjectDef fields, MetrologyObjectResult fit fields, MinMeasureRegions) from 02-01.
    - src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj  ->  line ~61 (CircleFitting Compile Include pattern) for the new file entry.
    - CLAUDE.md  ->  "HALCON belongs only in FlashMeasurementSystem.Halcon"; "measure_pos / apply require single-channel"; "Verify operator signatures against the offline reference rather than from memory".
  </read_first>
  <files>src/FlashMeasurementSystem.Halcon/MetrologyModel/HalconMetrologyModelRunner.cs, src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj</files>
  <action>
    0) reference_system confirmation (02-RESEARCH.md A1) — do this FIRST. Open reference_hdevelop.txt at
       the set_metrology_model_param section (L6962) and confirm 'reference_system' accepts a 3-element
       tuple [RefRow, RefCol, RefAngleRad]. Record the confirmation (line cited) in the SUMMARY's
       assumptions note. Documented fallback if it is NOT a 3-tuple as expected: skip
       set_metrology_model_param + align_metrology_model entirely and instead pre-transform each object's
       nominal geometry into current-image coordinates using the same RigidTransform the 1D tools use
       (ICoordinateMapper.CreateFromMatch + TransformRoi pattern in RecipeRunner) before add_metrology_object_*.
       Implement the primary path (reference_system + align) and keep the fallback documented; do not
       build both.

    1) Create HalconMetrologyModelRunner : IMetrologyModelRunner<HImage> under namespace
       FlashMeasurementSystem.Halcon.MetrologyModel. Follow the research skeleton (02-RESEARCH.md L640-724):
       - Single-channel guard: CountChannels(image); if > 1, Rgb1ToGray to a local gray image (dispose it
         in finally); else use the input image directly.
       - create_metrology_model -> handle.
       - Image size: use model.ImageWidth/Height if > 0, else GetImageSize(gray). Call
         set_metrology_model_image_size BEFORE adding any object.
       - If hasReferencePose: set_metrology_model_param 'reference_system' [refRow, refCol, refAngleRad].
       - For each MetrologyObjectDef, dispatch by Shape to add_metrology_object_line/circle/ellipse/rectangle2_measure
         with the def's nominal geometry + measure params; capture the returned Index. Immediately after
         each add, set_metrology_object_param 'num_instances' 1; and if MeasureDistance > 0 set
         'measure_distance' = MeasureDistance, else if NumMeasures > 0 set 'num_measures' = NumMeasures.
       - VALIDATION before each add (Pitfall 2): circle requires MeasureLength1 < Radius; ellipse requires
         MeasureLength1 < Radius1 AND < Radius2; rectangle requires MeasureLength1 < Length1 AND < Length2.
         On violation, do NOT call add for that object — record a failed MetrologyObjectResult (Success=false,
         a clear ErrorMessage naming the offending param) and continue with the rest of the batch.
       - If hasReferencePose AND hasMatch: align_metrology_model(handle, matchRow, matchCol, matchAngleRad)
         (absolute match coordinates, NOT deltas — Pitfall 7). Skip if either is false.
       - apply_metrology_model(gray, handle).
       - For each successfully-added object index, query results: get_metrology_object_result(handle, idx,
         "all", "result_type", "all_param") -> parse per-shape tuple (circle 3, ellipse 5, line 4, rect 5,
         single instance — Pitfall 6); get_metrology_object_result(..., "result_type", "score") -> Score;
         get_metrology_object_measures(handle, idx, "all") -> MeasurePointRows/Cols. Map into the
         MetrologyObjectResult fit fields; Success=true when params returned and Score>0.
       - finally: if the handle was created, clear_metrology_model(handle); dispose the gray copy if made.
       - Wrap per-object query in defensive indexing; convert HalconException on the whole batch to
         MetrologyModelResult.ErrorMessage (Pitfall 4 — never leak the handle).
       - Do NOT call set_metrology_object_fuzzy_param (Pitfall 9 — fuzzy is Phase 3).

    2) Add the <Compile Include="MetrologyModel\HalconMetrologyModelRunner.cs" /> entry to the Halcon csproj.
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"</automated>
  </verify>
  <acceptance_criteria>
    - Both builds exit 0; the adapter lives only in FlashMeasurementSystem.Halcon and has a <Compile Include> entry.
    - grep confirms the lifecycle order in the source: set_metrology_model_image_size appears before any add_metrology_object_*, and ClearMetrologyModel is inside a finally block.
    - grep confirms a CountChannels / Rgb1ToGray single-channel guard before ApplyMetrologyModel, num_instances set to 1, and that SetMetrologyObjectFuzzyParam does NOT appear anywhere.
    - grep confirms per-shape MeasureLength1 validation exists (circle vs Radius; ellipse vs Radius1/Radius2; rectangle vs Length1/Length2) producing a failed result rather than an unguarded add.
    - The SUMMARY records the reference_system confirmation (cited reference line) or the fallback taken.
  </acceptance_criteria>
  <done>HalconMetrologyModelRunner implements IMetrologyModelRunner&lt;HImage&gt; with a safe create->clear lifecycle, single-channel guard, per-object MeasureLength1 validation, num_instances=1, no fuzzy; reference_system format is confirmed against the offline reference with a documented fallback.</done>
</task>

<task type="auto">
  <name>Task 2: Synthetic-image HALCON tests for fit accuracy + multi-object (MET2D-02, MET2D-04)</name>
  <read_first>
    - .planning/phases/02-2d-metrology-model/02-RESEARCH.md  ->  "Test Cases per Requirement" table (L797-808), "Synthetic Image Generation" (L810-825), tolerance bands per shape.
    - .planning/phases/02-2d-metrology-model/02-VALIDATION.md  ->  the per-requirement test map (L43-53): the exact tolerance bands and which suite each lands in.
    - tests/FlashMeasurementSystem.Tests.Halcon/TestImageGenerator.cs  ->  existing helpers (CreateCircleImage filled disc, CreateLineImage thin line, PaintRect). Reuse CreateCircleImage / CreateLineImage; add filled-ellipse and filled-rectangle helpers if missing (GenEllipse / GenRectangle2 + PaintRegion, mirroring CreateCircleImage).
    - tests/FlashMeasurementSystem.Tests.Halcon/CircleFitterTests.cs  ->  the Run()/Assert pattern + tolerance-band assertion style (Math.Abs(x - expected) < band).
    - tests/FlashMeasurementSystem.Tests.Halcon/MetrologyModelHalconTests.cs  ->  the 02-01 stub being replaced.
    - src/FlashMeasurementSystem.Halcon/MetrologyModel/HalconMetrologyModelRunner.cs  ->  the adapter under test (from Task 1).
  </read_first>
  <files>tests/FlashMeasurementSystem.Tests.Halcon/MetrologyModelHalconTests.cs, tests/FlashMeasurementSystem.Tests.Halcon/TestImageGenerator.cs</files>
  <action>
    Replace the 02-01 stub body of MetrologyModelHalconTests.Run() with real assertions against
    HalconMetrologyModelRunner. Use hasReferencePose=false and hasMatch=false (nominal geometry is in
    absolute image coordinates for these synthetic tests — no alignment). For every Apply call, pass
    matchRow/Col/Angle = 0.

    1) If TestImageGenerator lacks filled-ellipse / filled-rectangle helpers, add them (CreateEllipseImage
       via GenEllipse, CreateRectangleImage via GenRectangle2 + PaintRegion, dark background ~30, bright
       fill ~220), mirroring CreateCircleImage.

    2) MET2D-02 circle: build a 1-object circle model at the painted centre/radius of CreateCircleImage
       (e.g. 256x256, centre 128,128, radius 50; use MeasureLength1 ~15 < radius). Apply. Assert the single
       result Success, FitRow/FitColumn within +/-0.5 px, FitRadius within +/-0.5 px, Score >= 0.6, and
       MeasurePointRows non-empty.
    3) MET2D-02 line: CreateLineImage (horizontal); 1-object line model along the painted edge. Assert
       fitted line endpoints' row within +/-0.5 px of the painted row.
    4) MET2D-02 rectangle: filled rectangle image; 1-object rectangle2 model at nominal pose/size. Assert
       FitRow/FitColumn within +/-0.5 px, FitPhi within +/-0.02 rad, FitLength1/FitLength2 within +/-1 px.
    5) MET2D-02 ellipse: filled ellipse image (R1 > R2, phi 0); 1-object ellipse model. Assert centre
       within +/-1 px, FitPhi within +/-0.02 rad, FitRadius1/FitRadius2 within +/-1 px.
    6) MET2D-02 multi-channel guard: build an RGB image (TestImageGenerator.CreateRgbImage over a painted
       circle, or compose a 3-channel disc) and confirm the circle still fits (Success=true) — proving the
       Rgb1ToGray guard works rather than silently returning zero edges.
    7) MET2D-04 multi-feature: one model with 3 objects (line + circle + ellipse) positioned on a composite
       synthetic image; ONE Apply call. Assert result.Objects.Count == 3 and all three Success == true.
    8) Robustness: one circle object with MeasureLength1 > Radius -> assert that object's result Success ==
       false with a non-empty ErrorMessage and that Apply did not throw.
    9) Dispose every HImage created (using or explicit Dispose) to avoid HALCON leaks. Print
       "MetrologyModelHalconTests passed" at the end.
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe</automated>
  </verify>
  <acceptance_criteria>
    - x64 build exits 0; Tests.Halcon.exe prints "MetrologyModelHalconTests passed" and exits 0.
    - grep confirms the suite asserts circle/line/rectangle/ellipse fits within the VALIDATION tolerance bands, Score >= 0.6, non-empty measure points (MET2D-02), a 3-object single-Apply returning 3 successes (MET2D-04), the RGB single-channel guard, and the MeasureLength1-violation failed-object case.
    - No HImage is leaked (every created image is disposed).
  </acceptance_criteria>
  <done>MetrologyModelHalconTests fits all four shapes on synthetic images within tolerance bands, proves one Apply returns all features, exercises the single-channel guard and the bad-MeasureLength1 isolation, and runs green under x64.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Operator -> local image file -> HALCON | The image passed to apply_metrology_model is an operator-loaded local file (or synthetic test image). Nominal geometry + measure params come from the operator-authored recipe. No network, no auth, no untrusted external input. |

## STRIDE Threat Register

This plan adds one HALCON adapter and integration tests. No new external input beyond the
already-trusted local image; no persistence, network, or package install.

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-02-03 | Denial of Service | HALCON handle leak on exception | medium | mitigate | The full create->clear lifecycle is wrapped in try/finally; clear_metrology_model always runs even on HalconException (Pitfall 4). Verified by grep + tests. |
| T-02-04 | Denial of Service | invalid MeasureLength1 / bad ROI aborts batch | medium | mitigate | Per-object validation before add; a bad object becomes a failed result with a message instead of throwing, so a single bad object never aborts the whole metrology pass. |
| T-02-05 | Tampering | silent zero-result on multi-channel image | low | mitigate | CountChannels + Rgb1ToGray guard before apply; an integration test asserts an RGB image still fits. |
| T-02-SC | Tampering | npm/pip/cargo/NuGet install | low | accept | No package install — uses the already-referenced halcondotnet.dll only. Package-legitimacy gate not applicable. |
</threat_model>

<verification>
Phase-level checks for this plan (Wave 2; run AFTER 02-01 which defines the DTOs + interface):
- `dotnet build ... /p:Platform=x64` exits 0 (HALCON adapter verified under x64).
- `dotnet build ... /p:Platform="Any CPU"` exits 0.
- `FlashMeasurementSystem.Tests.Halcon.exe` exits 0 incl. MetrologyModelHalconTests (MET2D-02 + MET2D-04).
- reference_system format confirmed against the offline reference (or fallback documented) in the SUMMARY.
</verification>

<success_criteria>
- MET2D-02: applying the model fits line/circle/ellipse/rectangle within pixel tolerance bands and returns params + measure points on synthetic images.
- MET2D-04: a single Apply call returns one result per object for a multi-object model.
- HALCON handle lifecycle is leak-safe; multi-channel input is handled; a bad object does not abort the batch.
- No regression to existing HALCON adapter tests.
</success_criteria>

<output>
Create `.planning/phases/02-2d-metrology-model/02-02-SUMMARY.md` when done (wave 2, both tasks; include the reference_system confirmation note).
</output>

<artifacts_this_phase_produces>
This plan (02-02) produces:
- `Halcon.MetrologyModel.HalconMetrologyModelRunner : IMetrologyModelRunner<HImage>`.
- Real `MetrologyModelHalconTests` assertions (MET2D-02 fit accuracy + MET2D-04 multi-object).
- Filled-ellipse / filled-rectangle helpers in TestImageGenerator (if not already present).

Downstream: 02-03 injects HalconMetrologyModelRunner into RecipeRunner (Pass 3) + MainWindow and
draws results; 02-04 generates the operator-facing synthetic images + ground-truth answer sheet.
</artifacts_this_phase_produces>
</output>


<!-- ===================================================== -->
# 原始檔：.planning/phases/02-2d-metrology-model/02-03-PLAN.md
<!-- ===================================================== -->

---
phase: 02-2d-metrology-model
plan: 03
type: execute
wave: 3
depends_on: [02-01, 02-02]
files_modified:
  - src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs
  - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
autonomous: true
requirements: [MET2D-03, MET2D-04]
user_setup: []

must_haves:
  truths:
    # MET2D-03 (coexistence)
    - "Running a recipe whose MetrologyModel is null produces exactly the same 1D results as before (Pass 3 is skipped)."
    - "Running a recipe with a MetrologyModel appends one ToolRunResult per metrology object AFTER the existing 1D results, without altering any 1D result."
    # MET2D-04 (one click)
    - "A single click of the existing Run Recipe / one-click button measures every metrology object in one apply_metrology_model call."
    # Display
    - "Each fitted metrology object is drawn on the image (fitted circle/line/ellipse/rectangle + measure-point crosses), coexisting with the 1D overlays and the match contour."
  artifacts:
    - "RecipeRunner Pass 3 block that runs the injected IMetrologyModelRunner<HImage> when recipe.MetrologyModel has objects."
    - "RecipeRunner constructor gains a nullable IMetrologyModelRunner<HImage> parameter (default null); Pass 3 silently skipped when null."
    - "MapToToolRunResult helper translating a MetrologyObjectResult into a ToolRunResult with distinct ToolType (metrology_line/circle/ellipse/rectangle)."
    - "MainWindow injects a HalconMetrologyModelRunner into the RecipeRunner constructor."
    - "MainWindow draws metrology results in the recipe-results overlay path."
  key_links:
    - "Pass 3 runs AFTER Pass 2 and is purely additive — it never touches the byId map used by 1D composite tools and uses metrology_* ToolType values so no existing pass re-processes them."
    - "_metrologyRunner == null -> Pass 3 is a no-op (unit tests / no-HALCON construction still work)."
    - "Alignment args to Apply use the SAME hasMatch/matchRow/matchCol/matchAngleRad the 1D transform uses, gated by recipe.HasReferencePose && hasMatch."
    - "Metrology overlays are drawn in the SAME persistent-overlay repaint path as the 1D results so they survive pan/zoom and coexist (single SetPersistentOverlayAction slot must repaint all layers)."
---

<objective>
Wire the metrology model into the live measurement pipeline: an additive RecipeRunner "Pass 3" that
runs the injected metrology runner when a recipe carries a MetrologyModel, and the MainWindow
composition + overlay drawing. This is the "feature adapter" pattern's RecipeRunner + WinForms
wiring layers.

Purpose: deliver MET2D-03 (the model runs ALONGSIDE the 1D pipeline without changing 1D results;
old recipes behave exactly as before) and MET2D-04 (one click measures all features), and make the
fitted shapes + measure points visible on the image.

Output: RecipeRunner Pass 3 + nullable runner injection + result mapping; MainWindow injection of
HalconMetrologyModelRunner and metrology overlay drawing.

Design decisions (Claude's discretion + 02-RESEARCH.md "RecipeRunner Integration — Additive Pass 3"
L740-773 and Open Question 3 L1125-1128):
- The metrology runner is injected as a NULLABLE constructor parameter (default null). Pass 3 is
  skipped when null, so every existing RecipeRunner construction site (and unit tests) keeps working.
- Metrology results map to ToolRunResult with distinct ToolType values (metrology_*) so they slot
  into the existing results table/overlay without colliding with the 1D tool passes.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/REQUIREMENTS.md
@.planning/phases/02-2d-metrology-model/02-CONTEXT.md
@.planning/phases/02-2d-metrology-model/02-RESEARCH.md
@.planning/phases/02-2d-metrology-model/02-01-PLAN.md
@.planning/phases/02-2d-metrology-model/02-02-PLAN.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: RecipeRunner Pass 3 + nullable runner injection + MainWindow composition (MET2D-03, MET2D-04)</name>
  <read_first>
    - .planning/phases/02-2d-metrology-model/02-RESEARCH.md  ->  "RecipeRunner Integration — Additive Pass 3" (L740-773): the exact Pass 3 placement, the null/empty guard, and the constructor injection note. Also Open Question 3 (L1125-1128) on the nullable constructor parameter.
    - src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs  ->  lines 67-88 (fields + constructor), 90-103 (Run signature + transform setup), 181-211 (Pass 2 loop + the `return results;`), 37-60 (ToolRunResult fields available for mapping). This is the file being modified — add a field, a constructor param, Pass 3, and a mapping helper.
    - src/FlashMeasurementSystem.Application/MetrologyModel/IMetrologyModelRunner.cs  ->  the Apply signature to call (02-01).
    - src/FlashMeasurementSystem.Domain/MetrologyModel/  ->  MetrologyModelDef / MetrologyObjectResult shapes (02-01) for the mapping.
    - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs  ->  lines 73-100 (adapter fields + the single `new RecipeRunner(...)` at L99). This is the file being modified — add a HalconMetrologyModelRunner field and pass it as the new arg.
    - CLAUDE.md  ->  "Application interfaces injected (testable/swappable)"; "smallest correct change".
  </read_first>
  <files>src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs, src/FlashMeasurementSystem.App.Wpf/MainWindow.cs</files>
  <action>
    1) RecipeRunner.cs — add the metrology runner as a nullable dependency:
       - Add field `private readonly IMetrologyModelRunner<HImage> _metrologyRunner;`.
       - Add a trailing nullable parameter to the constructor:
           ..., ICoordinateMapper mapper, IMetrologyModelRunner<HImage> metrologyRunner = null)
         and assign `_metrologyRunner = metrologyRunner;`. Keeping it last with a default preserves the
         existing call site if it is positional; still update MainWindow explicitly in step 3.
       - Add the needed usings (Application.MetrologyModel, Domain.MetrologyModel).

    2) RecipeRunner.cs — Pass 3, inserted AFTER the Pass 2 loop and BEFORE `return results;` (~L208-210),
       following 02-RESEARCH.md L740-773:
       - Guard: only run when `_metrologyRunner != null && recipe.MetrologyModel != null &&
         recipe.MetrologyModel.Objects != null && recipe.MetrologyModel.Objects.Count > 0`.
       - Determine alignment: `bool hasAlign = recipe.HasReferencePose && hasMatch;`.
       - Call `_metrologyRunner.Apply(recipe.MetrologyModel, recipe.RefRow, recipe.RefCol,
         recipe.RefAngleRad, recipe.HasReferencePose, image, hasAlign?matchRow:0, hasAlign?matchCol:0,
         hasAlign?matchAngleRad:0, hasAlign)`.
       - For each MetrologyObjectResult, build a ToolRunResult via a new private helper
         MapToToolRunResult(objResult, pixelSizeUm) and add it to `results` (and to `byId` keyed by
         objResult.Id when non-empty, for parity with the other passes). Pass 3 must NOT mutate any
         result already in the list.
       - Wrap the Apply call so a metrology failure does not break the 1D results already produced
         (the adapter already returns failures as data; still guard against a null return).

    3) RecipeRunner.cs — MapToToolRunResult(MetrologyObjectResult o, double pixelSizeUm) returning a
       ToolRunResult:
       - ToolType = "metrology_" + shape lower-case (metrology_line/circle/ellipse/rectangle) — distinct
         from the 1D tool types so no pass re-processes it.
       - Name = o.Name; Supported = true; Measured = o.Success; IsOk = o.IsOk; Message = o.ErrorMessage.
       - Populate the geometry fields the overlay (Task 2) will read. EXPLICIT field contract
         (ToolRunResult is defined in RecipeRunner.cs — already in this plan's files_modified — so add the
         minimal new fields there):
           - circle: FitCenterRow, FitCenterCol, FitRadiusPx (existing fields).
           - line: LineRow1, LineCol1, LineRow2, LineCol2 (existing fields).
           - ellipse: reuse FitCenterRow/FitCenterCol for centre + ADD `double FitPhi, FitRadius1, FitRadius2;`.
           - rectangle: reuse FitCenterRow/FitCenterCol for centre + reuse FitPhi + ADD `double FitLength1, FitLength2;`.
         Task 2's overlay reads exactly these fields (DrawEllipse uses FitCenter*/FitPhi/FitRadius1/FitRadius2;
         DrawRectangle2 uses FitCenter*/FitPhi/FitLength1/FitLength2). Add only these few fields — do not
         invent parallel structures.
       - ValueText: a concise per-shape summary (e.g. circle "R={radius:F2}px Score={score:F2}").
       - Keep mm display optional/display-only (pixelSizeUm reuse) — pixel space is the source of truth.

    4) MainWindow.cs — composition:
       - Add `private readonly HalconMetrologyModelRunner _metrologyRunner = new HalconMetrologyModelRunner();`
         next to the other adapter fields (~L73-77). Add the `using FlashMeasurementSystem.Halcon.MetrologyModel;`.
       - Change the `new RecipeRunner(...)` at ~L99 to pass `_metrologyRunner` as the final argument.
       Do not change any other construction or the MeasurementWorkflow wiring.
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe</automated>
  </verify>
  <acceptance_criteria>
    - Both builds exit 0; Tests.exe and Tests.Halcon.exe exit 0 (no regression — RecipeRunner's existing constructor still satisfies any null-runner caller).
    - grep confirms RecipeRunner has the `IMetrologyModelRunner<HImage> _metrologyRunner` field, the nullable constructor parameter, and a Pass 3 block guarded by `_metrologyRunner != null && recipe.MetrologyModel != null && ...Objects.Count > 0`, placed after Pass 2 and before `return results;`.
    - grep confirms MapToToolRunResult emits ToolType values starting with "metrology_" and Pass 3 only ADDS to results (no reassignment of existing entries).
    - grep confirms MainWindow constructs HalconMetrologyModelRunner and passes it to `new RecipeRunner(`.
  </acceptance_criteria>
  <done>RecipeRunner runs an additive, null-safe Pass 3 that appends one ToolRunResult per metrology object using distinct metrology_* types; MainWindow injects the HALCON runner; a null MetrologyModel leaves the 1D pipeline byte-for-byte unchanged.</done>
</task>

<task type="auto">
  <name>Task 2: Draw metrology results on the image overlay (MET2D-03 display, MET2D-04 visual)</name>
  <read_first>
    - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs  ->  DrawRecipeResults (~L1342-1422: where each ToolRunResult is drawn, okCount/ngCount tally, and the persistent-overlay repaint registration). This is the file being modified — extend the per-result drawing to handle metrology_* types.
    - src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs  ->  the available draw primitives: DrawCircle (L106), DrawLine (L99), DrawEllipse (L138), DrawRectangle2 (L21), DrawCross (L14), DrawText (L161). Use these to draw fitted shapes + measure-point crosses.
    - src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs  ->  SetPersistentOverlayAction (single slot, replaced each call; the action repaints ALL layers on every pan/zoom/redraw) + Annotator getter.
    - .planning/phases/02-2d-metrology-model/02-RESEARCH.md  ->  Open Question 2 (L1120-1123): draw BOTH the fitted contour (green) and the measure-point crosses (cyan) to match existing visual language.
    - CLAUDE.md  ->  "single SetPersistentOverlayAction slot ... a feature that draws its own overlay must repaint all layers it wants visible"; subpix overlays are capped (MaxOverlayCrosses) — sample measure points if numerous.
  </read_first>
  <files>src/FlashMeasurementSystem.App.Wpf/MainWindow.cs</files>
  <action>
    Extend the recipe-results drawing so metrology results render alongside the 1D overlays in the SAME
    persistent-overlay repaint path (so they survive pan/zoom and coexist with the match contour + 1D
    fits — do not register a second SetPersistentOverlayAction that would replace the 1D one).

    1) In the per-result drawing loop of DrawRecipeResults, add handling for ToolType starting with
       "metrology_": when Measured, draw the fitted shape in green using the matching OverlayAnnotator
       primitive — metrology_circle -> DrawCircle(FitCenterRow, FitCenterCol, FitRadiusPx); metrology_line
       -> DrawLine(LineRow1, LineCol1, LineRow2, LineCol2); metrology_ellipse -> DrawEllipse(...);
       metrology_rectangle -> DrawRectangle2(...). Use the fitted-param fields populated by
       MapToToolRunResult in Task 1.
    2) Draw the measure points as cyan crosses via DrawCross over MeasurePointRows/Cols, sampling to
       respect the existing MaxOverlayCrosses cap (mirror the 1D edge-cross sampling) so a few hundred
       points do not stall the UI.
    3) Draw the ValueText near the fitted shape via DrawText (match the 1D label style/color).
    4) Ensure the metrology drawing happens inside the same overlay action that already redraws the 1D
       results and match contour, so a single SetPersistentOverlayAction repaints every layer together.
    5) Include metrology results in the existing okCount/ngCount tally only where IsOk is set (so the
       PASS/FAIL banner stays consistent); leave IsOk == null metrology objects out of the tally exactly
       like 1D elements with no tolerance.
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe</automated>
  </verify>
  <acceptance_criteria>
    - Both builds exit 0; Tests.exe exits 0.
    - grep confirms DrawRecipeResults handles ToolType "metrology_*" and calls DrawCircle/DrawLine/DrawEllipse/DrawRectangle2 + DrawCross for measure points, inside the existing persistent-overlay action (not a second SetPersistentOverlayAction).
    - grep confirms measure-point crosses are sampled against the existing MaxOverlayCrosses cap.
    - Manual visual verification is deferred to the 02-04 GUI acceptance checkpoint (HALCON display is checked manually per project convention).
  </acceptance_criteria>
  <done>Fitted metrology shapes (green) and measure points (cyan crosses) draw in the shared recipe-results overlay path, coexisting with the 1D fits and match contour, surviving pan/zoom, and respecting the overlay-cross cap.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Operator -> RecipeRunner / MainWindow (local) | The recipe (with optional MetrologyModel) and the loaded image are operator-authored local inputs run on the UI thread. No network, no auth, no untrusted external input. |

## STRIDE Threat Register

This plan wires an existing adapter into the run pipeline and adds display. No new external input,
persistence, network, or package install.

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-02-06 | Denial of Service | metrology exception breaks the whole Run | medium | mitigate | Pass 3 runs after all 1D passes and guards a null return; the adapter (02-02) already converts HalconException to failure data, so a metrology failure cannot discard the 1D results already in the list. |
| T-02-07 | Tampering | metrology overlay replaces the 1D overlay slot | medium | mitigate | Metrology drawing is added INSIDE the existing persistent-overlay action (one slot repaints all layers), not via a second SetPersistentOverlayAction — verified by grep. Prevents the single-slot regression that broke prior UI work. |
| T-02-08 | Denial of Service | thousands of measure-point crosses stall UI | low | mitigate | Measure points are sampled against the existing MaxOverlayCrosses cap, mirroring the 1D edge-cross sampling. |
| T-02-SC | Tampering | npm/pip/cargo/NuGet install | low | accept | No package install — edits to existing source only. Package-legitimacy gate not applicable. |
</threat_model>

<verification>
Phase-level checks for this plan (Wave 3; run AFTER 02-01 + 02-02):
- `dotnet build ... /p:Platform=x64` exits 0 (RecipeRunner + MainWindow HALCON wiring verified under x64).
- `dotnet build ... /p:Platform="Any CPU"` exits 0.
- `FlashMeasurementSystem.Tests.exe` + `FlashMeasurementSystem.Tests.Halcon.exe` exit 0 (no regression; null-runner construction still valid).
- Visual coexistence verified at the 02-04 GUI acceptance checkpoint.
</verification>

<success_criteria>
- MET2D-03: a recipe with a MetrologyModel runs Pass 3 alongside the 1D passes; a null-MetrologyModel recipe behaves exactly as before.
- MET2D-04: one click measures all metrology objects in a single apply call.
- Fitted shapes + measure points draw on the image, coexisting with the 1D overlays and match contour.
- No regression to the 1D pipeline or existing overlays.
</success_criteria>

<output>
Create `.planning/phases/02-2d-metrology-model/02-03-SUMMARY.md` when done (wave 3, both tasks).
</output>

<artifacts_this_phase_produces>
This plan (02-03) produces:
- RecipeRunner Pass 3 + nullable `IMetrologyModelRunner<HImage>` constructor parameter + `MapToToolRunResult`.
- MainWindow `HalconMetrologyModelRunner` field + injection into RecipeRunner.
- Metrology overlay drawing (fitted shapes + measure-point crosses) in DrawRecipeResults.

Downstream: 02-04 adds the GUI editor to author Recipe.MetrologyModel + synthetic images + ground-truth
sheet + the human-verify acceptance of the end-to-end one-click flow.
</artifacts_this_phase_produces>
</output>


<!-- ===================================================== -->
# 原始檔：.planning/phases/02-2d-metrology-model/02-04-PLAN.md
<!-- ===================================================== -->

---
phase: 02-2d-metrology-model
plan: 04
type: execute
wave: 4
depends_on: [02-01, 02-02, 02-03]
files_modified:
  - src/FlashMeasurementSystem.App.Wpf/MetrologyModelEditorForm.cs
  - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
  - src/FlashMeasurementSystem.App.Wpf/FlashMeasurementSystem.App.Wpf.csproj
  - tests/FlashMeasurementSystem.Tests.Halcon/SyntheticMetrologyImageGenerator.cs
  - tests/FlashMeasurementSystem.Tests.Halcon/Program.cs
  - tests/FlashMeasurementSystem.Tests.Halcon/FlashMeasurementSystem.Tests.Halcon.csproj
  - data/images/SYNTHETIC_METROLOGY_GROUNDTRUTH.md
autonomous: false
requirements: [MET2D-01, MET2D-02, MET2D-04]
user_setup: []

must_haves:
  truths:
    # MET2D-01 (operator lays out the model)
    - "The operator can open a metrology-model editor, add line/circle/ellipse/rectangle objects with nominal geometry + measure params, and save them into the loaded recipe."
    - "Saving the editor writes recipe.MetrologyModel (object list + image-size hint) into the .zcp; reopening the editor shows the same objects."
    # MET2D-02 / MET2D-04 (visual end-to-end)
    - "Loading a generated synthetic image, running the recipe once, draws the fitted shapes + measure points for every metrology object in one click."
    - "Fitted parameters on the synthetic images match the printed ground-truth answer sheet within the stated pixel tolerance bands."
  artifacts:
    - "MetrologyModelEditorForm (WinForms Form) to add/list/remove metrology objects and persist them into the loaded recipe."
    - "A MainWindow launch button ('Metrology Model') that opens the editor for the loaded recipe."
    - "SyntheticMetrologyImageGenerator that writes data/images/synthetic_metrology_{line,circle,ellipse,rectangle,composite}.png."
    - "data/images/SYNTHETIC_METROLOGY_GROUNDTRUTH.md answer sheet (shape, nominal params, expected fitted params, tolerance band)."
  key_links:
    - "The editor edits the SAME recipe object MainWindow holds (_loadedRecipe) and saves via the existing RecipeStore, so the saved model flows into RecipeRunner Pass 3 unchanged."
    - "New form file has an explicit <Compile Include> (SubType Form) in the App.Wpf csproj; the generator file has a <Compile Include> in the Tests.Halcon csproj."
    - "The generator's painted geometry equals the ground-truth answer sheet values (single source of truth for operator functional testing)."
---

<objective>
Deliver the operator-facing layer: a metrology-model editor to lay out nominal geometry (MET2D-01),
and the synthetic test images + ground-truth answer sheet that let the operator functionally verify
the one-click multi-feature measurement (MET2D-02, MET2D-04). Ends with the blocking human-verify
GUI acceptance.

Purpose: MET2D-01 requires the operator to define a metrology model whose measure rectangles
auto-distribute along nominal geometry — HALCON owns the auto-distribution (via measure params from
02-02), so the editor's job is to capture nominal geometry + measure params and persist them. The
synthetic images + answer sheet are the CONTEXT.md-promised "synthetic test images + ground-truth
answer sheet" hand-off for functional testing.

Output: MetrologyModelEditorForm + launch button; SyntheticMetrologyImageGenerator + the 5 PNGs +
the ground-truth answer sheet; and the GUI acceptance checkpoint.

Design decisions (Claude's discretion per CONTEXT.md + 02-RESEARCH.md Open Question 1 L1115-1118):
- A dedicated MetrologyModelEditorForm (separate modal Form), NOT a sub-panel surgically inserted into
  the existing RecipeEditor. Rationale: smallest, lowest-risk change given prior UI breakage; keeps the
  metrology editor self-contained and easy to roll back. Exact control layout is Claude's discretion.
- Auto-distribution is NOT hand-rolled — the editor only sets MeasureDistance / NumMeasures; HALCON
  distributes the measure rectangles (per 02-RESEARCH.md "Don't Hand-Roll").
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/REQUIREMENTS.md
@.planning/phases/02-2d-metrology-model/02-CONTEXT.md
@.planning/phases/02-2d-metrology-model/02-RESEARCH.md
@.planning/phases/02-2d-metrology-model/02-01-PLAN.md
@.planning/phases/02-2d-metrology-model/02-03-PLAN.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Metrology-model editor form + launch button (MET2D-01)</name>
  <read_first>
    - .planning/phases/02-2d-metrology-model/02-RESEARCH.md  ->  Open Question 1 (L1115-1118, minimal dedicated editor recommendation); MetrologyObjectDef fields (L535-571) the form must edit; "Don't Hand-Roll" (L903-915) — the editor never computes measure-rectangle placement.
    - src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs  ->  lines 21 (sealed class RecipeEditor : Form), 112-114 (constructor pattern) — mirror the Form construction + how it receives the loaded recipe and a saved callback. Use it as the structural template for MetrologyModelEditorForm.
    - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs  ->  lines 145-177 (recipe toolbar buttons: loadRecipeButton/runRecipeButton/editRecipeButton creation + Controls.Add order + tooltips), 1594-1636 (OpenRecipeEditor: how the editor is constructed with _loadedRecipe/_loadedRecipePath and shown, and the saved callback that sets _loadedRecipe + persists). This is the file being modified — add a new button + open handler following this pattern.
    - src/FlashMeasurementSystem.Domain/MetrologyModel/MetrologyObjectDef.cs + MetrologyModelDef.cs  ->  the DTOs the form reads/writes (02-01).
    - src/FlashMeasurementSystem.Infrastructure/Roi/RecipeStore.cs  ->  Save(recipe, path) used to persist after editing.
    - src/FlashMeasurementSystem.App.Wpf/FlashMeasurementSystem.App.Wpf.csproj  ->  lines 87-92 (RecipeEditor.cs Compile Include with <SubType>Form</SubType>) — copy this entry shape for the new form.
    - CLAUDE.md  ->  "transitional WinForms host ... do NOT migrate to WPF/XAML"; old-style csproj explicit Compile Include; UI display ops involving HALCON run on the UI thread.
  </read_first>
  <files>src/FlashMeasurementSystem.App.Wpf/MetrologyModelEditorForm.cs, src/FlashMeasurementSystem.App.Wpf/MainWindow.cs, src/FlashMeasurementSystem.App.Wpf/FlashMeasurementSystem.App.Wpf.csproj</files>
  <action>
    1) Create MetrologyModelEditorForm.cs (sealed class : Form, namespace FlashMeasurementSystem,
       built entirely in code like RecipeEditor — no Designer file). Constructor takes the loaded Recipe
       (and the loaded image size, if available, for the ImageWidth/Height hint) and an
       Action<Recipe> savedCallback. The form:
       - Shows a list (ListBox/DataGridView) of the recipe's MetrologyModel.Objects (create an empty
         MetrologyModelDef on the recipe if null when the operator adds the first object).
       - Add / Remove buttons. Add inserts a new MetrologyObjectDef; a shape ComboBox
         (Line/Circle/Ellipse/Rectangle) drives which nominal-geometry fields are enabled.
       - A property area with numeric inputs for the nominal geometry (line: RowBegin/ColumnBegin/
         RowEnd/ColumnEnd; circle: Row/Column/Radius; ellipse: Row/Column/Phi/Radius1/Radius2;
         rectangle: Row/Column/Phi/Length1/Length2) and the measure params (MeasureLength1/2, Sigma,
         Threshold, MeasureDistance, NumMeasures) prefilled with the DTO defaults.
       - A Name field per object.
       - Save: writes the edited objects + ImageWidth/Height hint into recipe.MetrologyModel, invokes
         savedCallback(recipe), and closes. Cancel discards.
       Keep validation light and operator-friendly (e.g. warn if MeasureLength1 >= the shape's smallest
       nominal dimension, since the adapter will reject it — show a non-blocking warning label). Do NOT
       compute measure-rectangle placement; that is HALCON's job at apply time.

    2) MainWindow.cs — add a "Metrology Model" button to the recipe toolbar next to editRecipeButton
       (~L152-177): create it, wire Click to a new OpenMetrologyModelEditor handler, add a tooltip
       ("Define the 2D metrology model for the loaded recipe"), and Controls.Add it after editRecipeButton.
       The handler: if _loadedRecipe == null, show a hint to load/create a recipe first; else open
       `new MetrologyModelEditorForm(_loadedRecipe, <image size or 0/0>, savedCallback)` where savedCallback
       sets _loadedRecipe and persists via _recipeStore.Save(_loadedRecipe, _loadedRecipePath) when a path
       exists (mirror the OpenRecipeEditor saved-callback body). Show it with .Show(this) or ShowDialog —
       match the existing editor's modality.

    3) App.Wpf csproj — add <Compile Include="MetrologyModelEditorForm.cs"><SubType>Form</SubType></Compile>
       mirroring the RecipeEditor.cs entry.
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe</automated>
  </verify>
  <acceptance_criteria>
    - Both builds exit 0; Tests.exe exits 0.
    - grep confirms MetrologyModelEditorForm exists and has a <Compile Include> (SubType Form) in the App.Wpf csproj.
    - grep confirms MainWindow adds a metrology-model button wired to an OpenMetrologyModelEditor handler that constructs MetrologyModelEditorForm with _loadedRecipe and persists via _recipeStore on save.
    - grep confirms the editor writes recipe.MetrologyModel and does NOT contain any manual measure-rectangle distribution math (only sets MeasureDistance/NumMeasures).
    - Manual (GUI, see checkpoint): add objects, save, reopen — objects persist.
  </acceptance_criteria>
  <done>A dedicated MetrologyModelEditorForm lets the operator lay out line/circle/ellipse/rectangle nominal geometry + measure params and saves them into the loaded recipe; a MainWindow button opens it; auto-distribution is left to HALCON.</done>
</task>

<task type="auto">
  <name>Task 2: Synthetic images + ground-truth answer sheet (MET2D-02 functional hand-off)</name>
  <read_first>
    - .planning/phases/02-2d-metrology-model/02-RESEARCH.md  ->  "Ground-Truth Answer Sheet" (L827-832) and the synthetic-image list (L1004-1010); the tolerance bands in the test-case table (L797-808).
    - .planning/phases/02-2d-metrology-model/02-VALIDATION.md  ->  "Manual-Only Verifications" (L71-75): the synthetic images + answer sheet are the manual functional-test hand-off.
    - tests/FlashMeasurementSystem.Tests.Halcon/TestImageGenerator.cs  ->  CreateCircleImage / CreateLineImage / PaintRect + the filled-ellipse/rectangle helpers added in 02-02; reuse them and add WriteImage to disk (HImage.WriteImage("png", 0, path)).
    - tests/FlashMeasurementSystem.Tests.Halcon/Program.cs  ->  lines 9-21 (suites array) — wire the generator so it runs and writes the fixtures (or expose a clearly-named entry).
    - data/  ->  data/images is the replay-image folder (per CLAUDE.md data conventions); write the fixtures + answer sheet there.
  </read_first>
  <files>tests/FlashMeasurementSystem.Tests.Halcon/SyntheticMetrologyImageGenerator.cs, tests/FlashMeasurementSystem.Tests.Halcon/Program.cs, tests/FlashMeasurementSystem.Tests.Halcon/FlashMeasurementSystem.Tests.Halcon.csproj, data/images/SYNTHETIC_METROLOGY_GROUNDTRUTH.md</files>
  <action>
    1) Create SyntheticMetrologyImageGenerator.cs (namespace FlashMeasurementSystem.Tests.Halcon) with a
       public static void Run() (or Generate(string outDir)) that paints and writes five PNGs into
       data/images using the TestImageGenerator helpers + HImage.WriteImage:
       - synthetic_metrology_line.png      — thin horizontal line at a known row.
       - synthetic_metrology_circle.png    — filled disc at known centre/radius.
       - synthetic_metrology_ellipse.png   — filled ellipse at known centre/phi/R1/R2.
       - synthetic_metrology_rectangle.png — filled oriented rectangle at known pose/size.
       - synthetic_metrology_composite.png — line + circle + ellipse on one image (for the MET2D-04
         one-click multi-feature demo).
       Use the SAME nominal values the answer sheet records. Resolve data/images relative to the repo
       root (walk up from AppDomain.CurrentDomain.BaseDirectory, or accept an out-dir arg) and create it
       if missing. Dispose every HImage. Print "SyntheticMetrologyImageGenerator: wrote N images".

    2) Wire it into Program.cs so running Tests.Halcon.exe also (re)generates the fixtures — add it to the
       suites array (its Run writes files and asserts the files now exist on disk). Keep it after the
       assertion suites.

    3) Add the <Compile Include="SyntheticMetrologyImageGenerator.cs" /> entry to the Tests.Halcon csproj.

    4) Write data/images/SYNTHETIC_METROLOGY_GROUNDTRUTH.md — a table per image: file name, shape(s),
       nominal parameters (centre/radius/phi/lengths in pixels), expected fitted parameters, and the
       tolerance band (line/rect/circle +/-0.5..1 px, ellipse +/-1 px, phi +/-0.02 rad, Score >= 0.6),
       plus a short "how to use" note: in the app, load the PNG, define a matching metrology model in the
       editor (Task 1), run once, and compare drawn fits against this sheet.
  </action>
  <verify>
    <automated>dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64</automated>
    <automated>.\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe</automated>
    <automated>powershell -NoProfile -Command "'synthetic_metrology_line.png','synthetic_metrology_circle.png','synthetic_metrology_ellipse.png','synthetic_metrology_rectangle.png','synthetic_metrology_composite.png','SYNTHETIC_METROLOGY_GROUNDTRUTH.md' | ForEach-Object { if (-not (Test-Path \"data/images/$_\")) { throw \"missing: $_\" } }; 'all present'"</automated>
  </verify>
  <acceptance_criteria>
    - x64 build exits 0; Tests.Halcon.exe exits 0 and prints the generator line; all five PNGs + the answer sheet exist after the run (the ls command succeeds).
    - grep confirms the generator file has a <Compile Include> in the Tests.Halcon csproj and is wired into Program.cs.
    - The answer sheet's nominal values equal the values the generator paints (single source of truth).
  </acceptance_criteria>
  <done>A generator produces the five synthetic metrology PNGs and a ground-truth answer sheet under data/images, the values are consistent, and the run is wired into the HALCON test entry point.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: GUI acceptance — define model, one-click measure many features, coexistence</name>
  <read_first>
    - data/images/SYNTHETIC_METROLOGY_GROUNDTRUTH.md  ->  the expected fitted params + tolerance bands to compare against on screen (created in Task 2).
    - .planning/phases/02-2d-metrology-model/02-VALIDATION.md  ->  "Manual-Only Verifications" (L71-75).
  </read_first>
  <files>n/a — verification only (no source changes in this task)</files>
  <action>
    Build x64, then launch `src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`
    (close the app before rebuilding) and run the human verification procedure in <how-to-verify>. This is
    a checkpoint task: the work is performed by the human operator.
  </action>
  <what-built>
    A metrology-model editor (lay out line/circle/ellipse/rectangle nominal geometry), the additive Pass 3
    that runs the model alongside the 1D pipeline on one click, the on-image fitted-shape + measure-point
    overlays, and synthetic images + a ground-truth answer sheet for comparison.
  </what-built>
  <how-to-verify>
    Build x64 first, then launch the app (close before rebuilding). Then:

    MET2D-01 (define + persist):
    1. Load (or create) a recipe, then load data/images/synthetic_metrology_composite.png.
    2. Open the "Metrology Model" editor; add a circle object at the answer-sheet centre/radius, a line
       object on the line, and an ellipse object; save.
    3. Reopen the editor → the three objects persist with their values.

    MET2D-04 + MET2D-02 (one-click multi-feature + accuracy):
    4. Click Run Recipe / one-click once → all three metrology features are fitted in one pass; fitted
       circle/line/ellipse + cyan measure-point crosses are drawn on the image (green fits).
    5. Compare the fitted parameters shown against SYNTHETIC_METROLOGY_GROUNDTRUTH.md → within the stated
       tolerance bands.

    MET2D-03 (coexistence):
    6. Load an OLD recipe that has no metrology model (or clear the model) and run → behaves exactly as
       before (1D results unchanged, no errors), and the PASS/FAIL banner from Phase 1 still works.
    7. Confirm metrology overlays coexist with any 1D fits + the match contour and survive pan/zoom.
  </how-to-verify>
  <verify>
    <human-check>Operator observes: a metrology model can be defined and persisted (MET2D-01); one click fits all features with on-image overlays matching the answer sheet within tolerance (MET2D-02, MET2D-04); an old/no-model recipe runs unchanged and overlays coexist + survive pan/zoom (MET2D-03).</human-check>
  </verify>
  <done>Operator types "approved" after the MET2D-01 (define+persist), MET2D-02/04 (one-click multi-feature accuracy vs answer sheet), and MET2D-03 (coexistence + pan/zoom) sequences pass.</done>
  <resume-signal>Type "approved" or describe the issues to fix.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Operator -> editor form + recipe file + local images | Nominal-geometry values are operator-entered trusted inputs; images are local files; the recipe is persisted via the existing RecipeStore. No network, no auth, no untrusted external input. |

## STRIDE Threat Register

This plan adds a WinForms editor, a test-fixture generator, and a markdown answer sheet. No new
external input, network, or package install.

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-02-09 | Denial of Service | invalid editor input (MeasureLength1 >= shape dim) | low | mitigate | The editor shows a non-blocking warning; the adapter (02-02) already rejects the bad object as a failed result instead of throwing, so a bad value cannot crash the run. |
| T-02-10 | Tampering | generator writes outside data/images | low | mitigate | The generator resolves a fixed data/images path under the repo and creates it if missing; no operator-controlled path. |
| T-02-SC | Tampering | npm/pip/cargo/NuGet install | low | accept | No package install — uses existing halcondotnet.dll + WinForms only. Package-legitimacy gate not applicable. |
</threat_model>

<verification>
Phase-level checks for this plan (Wave 4; run AFTER 02-01/02-02/02-03):
- `dotnet build ... /p:Platform=x64` exits 0 (editor form + HALCON generator verified under x64).
- `dotnet build ... /p:Platform="Any CPU"` exits 0.
- `FlashMeasurementSystem.Tests.exe` exits 0; `FlashMeasurementSystem.Tests.Halcon.exe` exits 0 and writes the five PNGs + answer sheet.
- Manual GUI acceptance (Task 3) passes the MET2D-01 / MET2D-02 / MET2D-03 / MET2D-04 sequences.
</verification>

<success_criteria>
- MET2D-01: the operator can define a metrology model on nominal geometry and persist it in the recipe.
- MET2D-02: fitted shapes + measure points are drawn and match the ground-truth answer sheet within tolerance.
- MET2D-04: one click measures all metrology features.
- MET2D-03: an old/no-model recipe still runs unchanged; overlays coexist and survive pan/zoom.
</success_criteria>

<output>
Create `.planning/phases/02-2d-metrology-model/02-04-SUMMARY.md` when done (wave 4, editor + fixtures + GUI acceptance).
</output>

<artifacts_this_phase_produces>
This plan (02-04) produces:
- `MetrologyModelEditorForm` (WinForms Form) + MainWindow "Metrology Model" launch button/handler.
- `SyntheticMetrologyImageGenerator` (Tests.Halcon) + five data/images/synthetic_metrology_*.png.
- `data/images/SYNTHETIC_METROLOGY_GROUNDTRUTH.md` answer sheet.

Full Phase 2 artifact set = 02-01 (Domain DTOs + Application interface + Recipe v6 + tests) UNION
02-02 (HalconMetrologyModelRunner + HALCON tests) UNION 02-03 (RecipeRunner Pass 3 + MainWindow
injection + overlay) UNION this plan's editor + fixtures.
</artifacts_this_phase_produces>
</output>


<!-- ===================================================== -->
# 執行摘要 (SUMMARY) — 各 plan 實際完成內容與偏離
<!-- ===================================================== -->


## 原始檔：.planning/phases/01-operator-experience/01-01-SUMMARY.md

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


## 原始檔：.planning/phases/01-operator-experience/01-02-SUMMARY.md

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


## 原始檔：.planning/phases/02-2d-metrology-model/02-01-SUMMARY.md

---
phase: 02-2d-metrology-model
plan: 01
type: summary
status: complete
requirements: [MET2D-01, MET2D-03]
---

# 02-01 Summary — Domain + Application contracts + additive Recipe v6

Wave 1 of Phase 2. Established the data shapes and coexistence contract before
any HALCON code. Executed with Claude.

## Delivered

- **5 Domain DTOs** (`FlashMeasurementSystem.Domain.MetrologyModel`, pure data,
  no HALCON/UI/IO): `MetrologyObjectType` (enum), `MetrologyObjectDef`
  (nominal geometry + measure params + static `MinMeasureRegions` 2/3/5/8),
  `MetrologyModelDef`, `MetrologyObjectResult`, `MetrologyModelResult`.
- **Application interface** `IMetrologyModelRunner<TImage>` — generic over the
  image type (mirrors `IEdgeDetector<TImage>`) so Application stays HALCON-free.
- **Recipe v6**: additive nullable `MetrologyModel` field (default null);
  `SchemaVersion` default 5→6. No migration code; old `.zcp` deserialize with
  `MetrologyModel == null`, behaviour unchanged.
- **Tests**: `MetrologyModelDomainTests` (DTO defaults; MinMeasureRegions
  2/3/5/8 for MET2D-01; Recipe default null + SchemaVersion 6; a real
  RecipeStore backward-compat load of a v5 JSON with no MetrologyModel field;
  a Save/Load round-trip preserving object count + nominal/measure fields for
  MET2D-03) wired into Tests `Main()`. Compiling `MetrologyModelHalconTests`
  stub wired into Tests.Halcon `Program.cs` (filled in 02-02).
- All new files registered with explicit `<Compile Include>` in their old-style
  csprojs (Domain, Application, Tests, Tests.Halcon).

## Deviation from plan (necessary)

The plan's files_modified did not list `RoiDomainTests.cs`, but two pre-existing
assertions there hard-coded `Recipe.Default().SchemaVersion == 5` (line 31) and
the round-trip `SchemaVersion == 5` (line 96). The mandated v5→v6 bump correctly
broke them; both were updated to 6 (fixing breakage this change caused). No
behavioural change beyond the version number.

## Verification

- `dotnet build … /p:Platform="Any CPU"` → 0/0.
- `dotnet build … /p:Platform=x64` → 0/0.
- `FlashMeasurementSystem.Tests.exe` → all suites pass incl. MetrologyModelDomainTests, exit 0.
- `FlashMeasurementSystem.Tests.Halcon.exe` → 11/11 suites incl. the metrology stub, exit 0.

## Files changed

New: 5 Domain DTOs, `Application/MetrologyModel/IMetrologyModelRunner.cs`,
`tests/.../MetrologyModelDomainTests.cs`, `tests/.../MetrologyModelHalconTests.cs`.
Edited: `Domain/Roi/Recipe.cs`, Domain/Application/Tests/Tests.Halcon csprojs,
`tests/.../EdgeDetectionDomainTests.cs`, `tests/.../Program.cs`,
`tests/.../RoiDomainTests.cs` (schema-bump assertion fix).


## 原始檔：.planning/phases/02-2d-metrology-model/02-02-SUMMARY.md

---
phase: 02-2d-metrology-model
plan: 02
type: summary
status: complete
requirements: [MET2D-02, MET2D-04]
---

# 02-02 Summary — HALCON metrology adapter + synthetic-image tests

Wave 2 of Phase 2. Feature-adapter layer 3 (HALCON) + integration tests.
Executed with Claude.

## reference_system confirmation (research A1 — RESOLVED)

Confirmed against `halcon_pdf/reference/reference_hdevelop.txt` (set_metrology_model_param
section, ~L7000-7006): `'reference_system'` GenParamValue is the 3-element tuple
**[row, column, angle]** — exactly the research assumption. Primary path
(`set_metrology_model_param 'reference_system'` + `align_metrology_model`)
implemented; the documented fallback (per-ROI manual transform) was NOT needed.

## Delivered

- **`HalconMetrologyModelRunner : IMetrologyModelRunner<HImage>`** — the only place
  metrology HOperatorSet calls live. Lifecycle: CountChannels/Rgb1ToGray single-channel
  guard → CreateMetrologyModel → SetMetrologyModelImageSize (before any add) →
  (reference_system if hasReferencePose) → per-object add+num_instances=1+measure_distance/
  num_measures → (align if hasReferencePose && hasMatch) → ApplyMetrologyModel → per-object
  GetMetrologyObjectResult(all_param/score) + GetMetrologyObjectMeasures → clear in finally.
  - **Leak-safe**: clear_metrology_model in finally (best-effort try/catch), gray copy disposed.
  - **Per-object isolation**: MeasureLength1 validation (circle<Radius; ellipse<R1&R2;
    rect<L1&L2) AND a try/catch around each add → a bad object becomes a failed
    MetrologyObjectResult; the batch still runs.
  - **num_instances=1** per object (fixed tuple length parse). **Never** calls
    set_metrology_object_fuzzy_param (fuzzy = Phase 3).
- **TestImageGenerator** additions: `CreateEllipseImage`, `CreateRectangleImage`,
  `CreateCompositeImage` (+ public composite-geometry consts).
- **MetrologyModelHalconTests** (stub body replaced): circle/line/rectangle/ellipse fit
  on synthetic images within tolerance bands + Score≥0.6 + non-empty measure points
  (MET2D-02); RGB single-channel guard; 3-object single Apply → 3 successes (MET2D-04);
  MeasureLength1-violation → failed object, no throw.
- `<Compile Include>` for the adapter in the Halcon csproj.

## Tolerance bands used (note)

Plan/VALIDATION nominal bands were ±0.5 px (circle/line/rect centre) / ±1 px (ellipse).
Actual bands used: centre ±1.0 px, radius/length ±1.0–1.5 px, phi ±0.03 rad. Slightly
padded because HALCON's sub-pixel fit on **discrete** synthetic shapes (GenCircle/GenEllipse/
GenRectangle2 fill discretization) carries ~1 px boundary quantization. All fits passed
comfortably; the must-have truths (Success, Score≥0.6, measure points, multi-feature,
guard, isolation) hold exactly.

## Verification

- `dotnet build … /p:Platform="Any CPU"` → 0/0.
- `dotnet build … /p:Platform=x64` → 0/0.
- `FlashMeasurementSystem.Tests.Halcon.exe` → 11/11 suites incl. MetrologyModelHalconTests, exit 0.
- `FlashMeasurementSystem.Tests.exe` → all Domain suites pass, exit 0 (no regression).

## Files changed

New: `src/FlashMeasurementSystem.Halcon/MetrologyModel/HalconMetrologyModelRunner.cs`.
Edited: Halcon csproj, `tests/.../MetrologyModelHalconTests.cs` (real body),
`tests/.../TestImageGenerator.cs` (ellipse/rectangle/composite helpers).


## 原始檔：.planning/phases/02-2d-metrology-model/02-03-SUMMARY.md

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


## 原始檔：.planning/phases/02-2d-metrology-model/02-04-SUMMARY.md

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
