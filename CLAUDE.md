# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project authority & required pre-work

`AGENTS.md` (repo root) is the mandatory operating checklist — read it before any non-trivial change; the rules below summarize but do not replace it. The detailed domain reference is `docs/本手冊/FlashMeasurementSystem_開發手冊.md`. When the manual conflicts with existing code, report the conflict and make the smallest safe change rather than silently rewriting architecture.

This is a Windows machine-vision measurement system: .NET Framework 4.8, WinForms, MVTec **HALCON 17.12** via `halcondotnet.dll` (referenced from `C:\Program Files\MVTec\HALCON-17.12-Progress\bin\dotnet35\`). HALCON runs 64-bit.

## Build, test, run

```powershell
# Standard build (Any CPU)
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"

# HALCON / platform-sensitive changes ALSO require x64
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

- The `/p:GenerateResourceMSBuildArchitecture=CurrentArchitecture` flag is required — omitting it breaks the resource build on this machine.
- The `FlashMeasurementSystem.Halcon` project is `PlatformTarget=x64`; the app is `AnyCPU` but anything touching HALCON must be verified under x64.

**Tests are console-style, not a test framework.** `tests/FlashMeasurementSystem.Tests` is an `Exe` whose `Main()` lives in `EdgeDetectionDomainTests.cs` and calls each suite's `Run()` sequentially (`LineFittingDomainTests.Run()`, `CircleFittingDomainTests.Run()`, …). Assertions throw `InvalidOperationException`; the process returns 0 on success and prints `<Suite> passed` per suite.

```powershell
# Run all tests (after building)
.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
```

There is no per-test runner. To run a "single test", temporarily comment out the other `*.Run()` calls in `Main()`, or add a new suite class and wire its `Run()` into `Main()`. Tests cover Domain defaults and Application interface compile-contracts only; HALCON adapters are **not** unit-tested (verified manually in the GUI).

**Running the app:** launch `src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`. Note the assembly was renamed at some point — if a stale `FlashMeasurementSystem.exe` reappears in a `bin/`/`obj/` folder it is a pre-rename zombie build; do not run it (it has frozen old code). A running app locks the output DLLs, so `dotnet build` then fails with MSB3026/MSB3027 — close the app before rebuilding.

## Architecture: strict one-way layering

```
Domain  ←  Application  ←  { Halcon, Mes, Reporting, Infrastructure }  ←  App.Wpf
```

- **`FlashMeasurementSystem.Domain`** — pure parameter/result models and value objects. **No** HALCON, UI, file-system, or hardware references. Depends on nothing else in the solution.
- **`FlashMeasurementSystem.Application`** — feature interfaces (`IEdgeDetector<TImage>`, `ILineFitter`, `ICircleFitter`, …) expressed only in Domain types. May depend on Domain.
- **`FlashMeasurementSystem.Halcon`** — concrete adapters implementing the Application interfaces against HALCON. This is the **only** place `HalconDotNet` / `HOperatorSet` / `HImage` belong (plus existing transitional app code).
- **`FlashMeasurementSystem.App.Wpf`** — transitional **WinForms** host (despite the `.Wpf` name and the `.App.Wpf` assembly name, the root namespace is `FlashMeasurementSystem` and the UI is WinForms). Composes the adapters. Do **not** migrate to WPF/XAML unless a task explicitly authorizes it.
- `Mes`, `Reporting`, `Infrastructure` are adapter projects at the same layer as `Halcon`.

Keep HALCON, MES, reporting, file-system, UI, and hardware logic out of `Domain`. Handle HALCON exceptions intentionally at application/service boundaries (never swallow silently). UI display operations involving HALCON controls must run on the UI thread.

### The "feature adapter" pattern (how Edge Detection / Line Fitting / Circle Fitting are built)

Each measurement feature is added as four coordinated pieces — follow this when adding a new one (e.g. ellipse fitting):

1. `Domain/<Feature>/<Feature>Parameters.cs` + `<Feature>Result.cs` — plain DTOs; `Parameters` has a `Default()` and (where relevant) an `IsSupportedAlgorithm()` allow-list.
2. `Application/<Feature>/I<Feature>er.cs` — interface over Domain types.
3. `Halcon/<Feature>/Halcon<Feature>er.cs` — adapter: validate inputs, build an `HXLDCont` (wrap in `using`), call the HALCON operator, map outputs back to the Domain result, convert `HalconException` to a failed result.
4. `tests/.../​<Feature>DomainTests.cs` — console suite; wire its `Run()` into `Main()`.
5. WinForms wiring in `MainWindow.cs` + `MainWindow.Designer.cs` (button, result label, overlay).

The projects use **old-style `.csproj`** — new files must be added with explicit `<Compile Include="..." />` entries; they are not globbed.

### WinForms / HALCON display gotchas (MainWindow + HWindowControlHelper + OverlayAnnotator)

- ROI mouse interaction is hand-written on standard WinForms `MouseDown/Move/Up` in `HWindowControlHelper` (HALCON 17.12's own mouse-event and modal `draw_*` APIs were found unstable/blocking and are deliberately avoided). All zoom/pan/ROI/rotation must be implemented there, not via HALCON's interactive operators.
- Overlays use a single **`SetPersistentOverlayAction(Action)`** slot that the helper re-invokes on every redraw (pan/zoom/resize). It **replaces** the previous action — so a feature that draws its own overlay (e.g. fitted circle) must repaint *all* layers it wants visible (ROI rectangle + edge crosses + its own shape), or the earlier overlay disappears.
- Subpix edge detection can return hundreds–thousands of points. The grid and overlay are intentionally capped (`MaxGridRows`, `MaxOverlayCrosses` with even sampling) to keep the UI responsive — preserve these caps when touching result binding.
- Designer controls are `Dock=Fill` inside `TableLayoutPanel`s, so hard-coded `Location`/`Size` values are cosmetic; layout is governed by `RowStyles`/`ColumnStyles` and cell assignment. When inserting a row, insert the `RowStyle` at the right index (don't blindly append) and keep the trailing `Percent 100F` row last (it's the flexible grid row).

### HALCON measurement semantics worth knowing

- `measure_pos` / `edges_sub_pix` require a **single-channel** image — multi-channel input silently returns zero results (no exception). Adapters convert with `rgb1_to_gray` / `access_channel` first.
- `measure_pos` returns one point per physical edge on the rectangle's major axis, and its `Distance` tuple has **one fewer element** than the edge tuples — index defensively.
- A rectangular ROI's shape alone cannot determine whether the user wants horizontal vs vertical edges; the edge detector tries the primary orientation then falls back by rotating 90°.
- `fit_line_contour_xld` and `fit_circle_contour_xld` have **different positional parameter orders** (circle inserts `MaxClosureDist`); the minimum point count is `2 + 2*ClippingEndPoints` (line) / `3 + 2*ClippingEndPoints` (circle). Verify operator signatures against the offline reference rather than from memory.

## HALCON offline reference

The full HALCON 17.12 HDevelop operator reference is bundled for offline lookup under `halcon_pdf/reference/`:
- `reference_hdevelop.txt` — extracted plaintext (≈183k lines).
- `halcon_operator_index.md` — all ~2113 operators with line numbers + one-line descriptions, grouped by chapter. Grep this for an operator name to jump to its exact lines in the `.txt`.
- `halcon_chapter_intros.md` — per-chapter concept/workflow/glossary.

Always confirm HALCON operator parameter order and value lists against this reference; do not rely on memory.

## Domain workflow & data conventions

- Measurement state flow (preserve unless explicitly changing):
  `IDLE → LOADING_PROGRAM → WAITING_PART → PREPARING → ACQUIRING → CHECKING_IMAGE → MATCHING_TEMPLATE → MEASURING → EVALUATING → REPORTING → OUTPUTTING → IDLE`.
- HALCON uses `(Row, Column)` = `(Y, X)`; angles in radians, `Phi ∈ (-π, π]`.
- Data folders: recipes `.zcp` under `data/recipes`, calibrations under `data/calibrations`, replay images under `data/images`, CSV/reports under `data/reports`, shape-model templates `.shm` under `data/templates`.

## Implementation discipline

- Make the smallest correct change; keep features, bug fixes, refactors, and docs as separate tasks unless the user asks to combine them.
- Don't add speculative abstractions or new dependencies just because the manual mentions future functionality.
- Don't commit build outputs (`bin/`, `obj/`, `.vs/`) or machine-specific files. The `_obsolete_build_20260605/` folder is a quarantined backup of an old renamed build — leave it alone.
- Feature design/implementation docs live in `docs/superpowers/specs/` and `docs/superpowers/plans/`; daily work summaries in `docs/每日進度/`.
