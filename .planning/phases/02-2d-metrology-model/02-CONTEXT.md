# Phase 2: 2D Metrology Model - Context

**Gathered:** 2026-06-30
**Status:** Ready for planning
**Source:** Direct discussion (operator + Claude), 2026-06-30

<domain>
## Phase Boundary

Add a **2D metrology model** measurement layer on top of the existing system:
HALCON's metrology-model workflow (define nominal geometry → auto-distribute
measure rectangles → apply to image → robustly fit line/circle/ellipse/rectangle
→ return parameters + measure points). It sits ON TOP of the existing template
matching (locating master, `.shm`) and COEXISTS with the existing 1D
`measure_pos` / fit pipeline (RecipeRunner) — it does not replace it.

Requirements in scope: **MET2D-01, MET2D-02, MET2D-03, MET2D-04**.

This phase delivers and is verified entirely in **pixel space on synthetic /
replay images** — the same trust level as the already-shipped 1D pipeline
(M1–M4), which was also built and verified without hardware calibration.
</domain>

<decisions>
## Implementation Decisions (LOCKED)

### Master / reference geometry
- **Master = nominal geometry parameters (Option A).** The metrology model's
  nominal geometry (line endpoints, circle center/radius, ellipse axes, rect
  pose/size), defined when building the model and stored in the recipe, IS the
  master. Deviations are computed as fitted-vs-nominal.
- **CAD / DXF master import (Option B) is DEFERRED** to a later, separately
  evaluated phase. Do NOT implement DXF/CAD contour import in Phase 2.

### Units / calibration constraint
- **No hardware calibration.** Pixel↔mm calibration (CAL-01/02) remains
  deferred (no camera, no caltab, no standard parts → no truth). Therefore:
  - Phase 2 outputs and acceptance are in **pixel units** (plus parameters /
    measure points). Absolute mm metrological accuracy is **NOT** a success
    criterion of this phase.
  - mm conversion, if shown at all, reuses the existing manual pixel-size path
    (same as the 1D pipeline) — it is display-only, not validated here.

### Coexistence (MET2D-03)
- A metrology model **saves into the existing recipe (`.zcp`)** and runs
  **alongside** the existing 1D pipeline without changing existing 1D results.
  Loading/running an old recipe with no metrology model must behave exactly as
  before (backward compatible).

### HALCON usage
- Use HALCON 17.12 metrology-model operators (`create_metrology_model`,
  `add_metrology_object_*_measure`, `apply_metrology_model`,
  `get_metrology_object_result` / `_measures`, etc.). **Confirm every operator
  name, parameter order, and value list against `halcon_pdf/reference`** — never
  from memory (project rule).
- HALCON belongs only in the `FlashMeasurementSystem.Halcon` adapter project.

### Architecture / process
- Follow the existing **"feature adapter" pattern** (CLAUDE.md): Domain DTOs
  (Parameters/Result, no HALCON) → Application interface over Domain types →
  Halcon adapter (validate, build HObject in `using`, call operator, map back,
  convert HalconException to failed result) → console test suite wired into
  `Main()` → WinForms wiring. Old-style `.csproj`: new files need explicit
  `<Compile Include>`.
- Verify under **x64** (HALCON) in addition to Any CPU.
- **Execution must be done with Claude, not the GLM executor** (prior GLM
  execute-phase run corrupted the UI; see project history).

### Verification
- Verified on **synthetic images with known pixel ground-truth**. Acceptance
  uses **tolerance bands** (sub-pixel fitting residual), not exact equality.
- Standard gates: `dotnet build … x64` 0/0, `Tests.exe` all pass,
  GUI human acceptance.
- Claude will **generate the synthetic test images + a ground-truth answer
  sheet** at the end of the phase for the operator's functional testing.

### Claude's Discretion
- Domain DTO shapes, exact UI placement of the metrology-model editor controls,
  measure-rectangle auto-distribution spacing defaults, how the metrology model
  serializes inside the recipe schema (additive, backward compatible).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` — Phase 2 goal + success criteria
- `.planning/REQUIREMENTS.md` — MET2D-01..04 definitions; deferred CAL/BENCH

### Project rules & patterns
- `CLAUDE.md` — feature-adapter pattern, one-way layering, WinForms/HALCON
  display gotchas, build/test commands, HALCON measurement semantics
- `AGENTS.md` — mandatory operating checklist (no speculative abstraction)
- `docs/本手冊/FlashMeasurementSystem_開發手冊.md` — domain reference

### HALCON offline reference (confirm operator signatures here)
- `halcon_pdf/reference/reference_hdevelop.txt` — full 17.12 operator text
- `halcon_pdf/reference/halcon_operator_index.md` — operator → line lookup
- `halcon_pdf/reference/halcon_chapter_intros.md` — per-chapter concepts

### Integration points (existing code to extend, not rewrite)
- `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs` — 1D pipeline; coexist
- `src/FlashMeasurementSystem.Domain/Roi/Recipe.cs` + `MeasurementTool.cs` —
  recipe/tool model the metrology model must save alongside
- `src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs` — recipe editor UI
- `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs` — drawing primitives
</canonical_refs>

<specifics>
## Specific Ideas

- MET2D-01: operator lays out a metrology model whose measure rectangles
  auto-distribute along nominal geometry.
- MET2D-02: applying the model to a replay image fits line/circle/ellipse/
  rectangle and returns parameters + measure points.
- MET2D-03: metrology model saves into a recipe; runs alongside the 1D pipeline
  without breaking it (old recipes still work).
- MET2D-04: one click measures multiple features of a part.
</specifics>

<deferred>
## Deferred Ideas

- **Option B — CAD / DXF master import** as nominal geometry (separate later
  decision; absolute-mm comparison would also require calibration).
- mm absolute metrological accuracy / calibration (CAL-01/02) — blocked on
  hardware, no truth.
- CMM benchmarking (BENCH-01) — blocked on hardware.
- Phase 3 fuzzy/robust edge mode and GR&R — later phases.
</deferred>

---

*Phase: 02-2d-metrology-model*
*Context gathered: 2026-06-30 via direct discussion*
