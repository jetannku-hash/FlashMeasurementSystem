# GSD Phase 1 & 2 — Consolidated Spec / Design (backup)

> 由 `.planning/` 於 2026-07-01 整合備份（移除 GSD 工具前）。內容為專案總述、需求、路線圖，以及 Phase 2 的鎖定決策(CONTEXT)、技術研究(RESEARCH)、驗證策略(VALIDATION)。日期以昨日 2026-06-30 命名。原始逐檔內容依序保留於下。

---


<!-- ===================================================== -->
# 原始檔：.planning/PROJECT.md
<!-- ===================================================== -->

# FlashMeasurementSystem

## What This Is

A Windows machine-vision dimensional measurement system ("一鍵式閃測儀") for flash/part
metrology. A single operator builds a measurement recipe and runs pass/fail dimensional
measurements on replay images using MVTec HALCON 17.12. It already does 1D caliper +
XLD line/circle/ellipse/rectangle/arc fitting + template matching + geometry construction
+ GD&T form tolerance, with CSV reporting. Built for one operator and one builder.

## Core Value

An operator can build a measurement recipe and run reliable pass/fail dimensional
measurements on replay images — with results they can trust and report. The north star
is reaching mainstream measurement-instrument capability.

## Requirements

### Validated

<!-- Shipped and confirmed (baseline; see Completed Milestones below). -->

- ✓ M0–M4 core pipeline: image quality → template match → subpixel edge → line/circle fit → distance/angle → tolerance OK/NG → overlay annotation → one-click flow
- ✓ A3 geometry-primitive breadth: ellipse / arc / rectangle / point fitting (incl. interactive arc caliper)
- ✓ A5 geometry construction: line intersection / symmetric midline / point-to-line projection
- ✓ A4 (partial) GD&T form tolerance v1: roundness, straightness, parallelism, perpendicularity, concentricity (single-datum, single-sided)
- ✓ N1 recipe validation: pre-run diagnostics (Error blocks / Warning prompts)
- ✓ Pixel→mm calibration (2-point isotropic), recipe persistence, CSV reporting

### Active

<!-- Forward scope toward mainstream measurement-instrument capability. See ROADMAP.md. -->

- [ ] Operator experience polish — empty-state guidance, PASS/FAIL banner, tolerance-limit display, in-editor trial measure
- [ ] A2 2D Metrology Model — auto-placed measure rectangles on nominal geometry; robust multi-feature one-click measure (the mainstream differentiator)
- [ ] Production robustness — fuzzy/robust edge measurement (B1) + GR&R / repeatability self-test (B2)
- [ ] PDF measurement reporting (B3 PDF)
- [ ] Application-level measurement solutions library — gear count/pitch, PCD, diameter, pin-pitch templates

### Out of Scope

- Full WPF migration / dark mode / full localization — outside measurement-core value, high risk / low return
- BackgroundWorker threading / full MainWindow split — sub-second ops not worth the threading risk; split is low-value / high-risk
- Recipe-creation Wizard — over-designed for a single operator; replaced by empty-state guidance (N3)

## Context

- Stack: .NET Framework 4.8, WinForms, MVTec HALCON 17.12 (HALCON runs 64-bit).
  Strict one-way layering: Domain ← Application ← {Halcon, Mes, Reporting, Infrastructure} ← App.Wpf.
  Domain stays HALCON-free. (`.App.Wpf` is a historical name — the UI is WinForms.)
- Feature-adapter pattern: each feature = Domain DTO + Application interface + Halcon adapter
  + console test + WinForms wiring. Old-style `.csproj` (explicit `<Compile Include>`).
- Tests are console-style (`tests/.../*.exe`), not a framework. HALCON adapters are verified
  manually in the GUI; Domain/Application contracts are unit-tested.
- HALCON operator parameter order must be verified against the bundled offline reference
  (`halcon_pdf/reference/`), never from memory.
- **Hardware reality:** no camera, no calibration board (caltab), no standard artifacts, no
  Z-axis. Any item requiring real metrological accuracy cannot be verified yet — replay-image
  software work only.
- The capability-gap list (A1–A5 / B1–B4 in `docs/superpowers/plans/2026-06-25_現況到主流量測儀_能力差距清單.md`)
  is the roadmap backbone; the canonical dashboard is **`.planning/ROADMAP.md`** (the legacy
  `docs/ROADMAP_待辦與決策.md` was retired on 2026-06-30 when this GSD bootstrap became canonical).

## Constraints

- **Tech stack**: .NET Framework 4.8 / WinForms / HALCON 17.12 — fixed; do not migrate to WPF.
- **Architecture**: strict one-way layering, Domain HALCON-free — enforced by `AGENTS.md` checklist.
- **Hardware**: no camera / caltab / standard parts / Z-axis — blocks A1 full calibration, B4 autofocus, CMM benchmarking, full real-image adapter tests.
- **External dependency**: MES integration blocked on the customer's MES protocol spec.
- **Verification**: replay-image + synthetic-image verifiable only; no real metrological truth available.

## Key Decisions

> Deferred/pending decisions (from the legacy ROADMAP §5) — each deliberately postponed to avoid
> re-litigating. Promote to a LOCKED decision (ADR-class) only when re-tagged via manifest + re-run.

| Decision | Rationale | Date | Outcome |
|----------|-----------|------|---------|
| Defer all A1 calibration (full + anisotropic half) until camera + standard parts arrive | No hardware = no truth to calibrate against; building now is idle motion | 2026-06-27 | Pending |
| GD&T position/symmetry/orientation + full datum frame → v2 | Needs complete datum reference frame + CMM benchmarking; highest error risk without standard parts | 2026-06-27 | Pending |
| Defer tangent-line construction | Not a prerequisite for any GD&T tolerance; no current part needs it; not building just to fill a list | 2026-06-27 | Pending |
| Straightness true value (peak-to-peak perpendicular band) → v2 | v1 uses ResidualRms (RMS approximation, labeled "approx" in UI/report); upgrade = add max−min band in HalconLineFitter. User has no camera/caltab → get algorithm online first | 2026-06-27 | Pending |
| Build measurement-solutions library only after primitives complete | Primitives are the building blocks — they are now done, so the library is unblocked | 2026-06-26 | Pending |
| Recipe-creation Wizard → withdrawn; use empty-state guidance (N3) instead | Over-designed for a single operator | 2026-06-25 | Withdrawn |
| No full WPF migration / dark mode / full localization | Outside measurement-core value, high risk / low return | 2026-06-25 | Rejected |
| No BackgroundWorker threading / full MainWindow split | Sub-second ops not worth the threading risk; split is low-value / high-risk | 2026-06-25 | Rejected |
| Keep 1D + 2D metrology coexisting (not replacing) | Avoid breaking the existing verified pipeline | 2026-06-30 | Pending |

## Completed Milestones

> Detail links live in the cross-referenced memory entries / specs.

- M0–M4 core pipeline (image quality → template → edge → line/circle fit → distance/angle → tolerance → annotation → one-click)
- A3 geometry-primitive breadth (ellipse / rectangle / arc / point, incl. interactive arc caliper)
- A5 geometry construction (line intersection / symmetric midline / point-to-line projection)
- GD&T form-tolerance v1 (roundness / straightness / parallelism / perpendicularity / concentricity)
- Deep-audit P0/P1/P2 fixes
- UI overlay residual fixes (13 items)
- N1 recipe validation (pre-run diagnostics)


---
*Last updated: 2026-06-30 after ingest bootstrap (gsd-roadmapper)*


<!-- ===================================================== -->
# 原始檔：.planning/REQUIREMENTS.md
<!-- ===================================================== -->

# Requirements: FlashMeasurementSystem

**Defined:** 2026-06-30
**Core Value:** An operator can build a recipe and run trustworthy pass/fail dimensional measurements on replay images, driving toward mainstream measurement-instrument capability.

> Scope note: This file tracks the **forward milestone** (current → mainstream measurement
> instrument). The shipped baseline (M0–M4, A3, A5, GD&T v1, N1) is recorded as Validated in
> PROJECT.md and is not re-listed here as v1. v1 below = actionable, hardware-unblocked work
> derived from the capability-gap list (A1–A5 / B1–B4) and the GUI backlog.

## v1 Requirements

### Operator Experience (GUI)

- [ ] **GUI-01**: Operator sees empty-state guidance (next-step hints) when no recipe/image is loaded (N3)
- [ ] **GUI-02**: Operator sees a prominent PASS/FAIL banner after a measurement run (N2)
- [ ] **GUI-03**: Operator sees tolerance upper/lower limits alongside each measured value in real time (N5)
- [ ] **GUI-04**: Operator can run a trial measurement from inside the recipe editor without leaving it (A1)

### 2D Metrology Model (MET2D)

- [ ] **MET2D-01**: Operator can define a metrology model that auto-places measure rectangles along nominal geometry
- [ ] **MET2D-02**: Applying the model to a replay image robustly fits line/circle/ellipse/rectangle and returns parameters + measure points
- [ ] **MET2D-03**: A metrology model is saved in a recipe and coexists with the existing 1D measurement pipeline
- [ ] **MET2D-04**: Operator can measure multiple part features in one click via the metrology model

### Production Robustness (RBST)

- [ ] **RBST-01**: Operator can run fuzzy/robust edge measurement that rejects noise/glare/interfering edges (B1)
- [ ] **RBST-02**: Operator can run a GR&R / repeatability self-test (repeat a recipe N times, get 6σ and repeatability/reproducibility %) (B2)

### Reporting (RPT)

- [ ] **RPT-01**: Operator can export a measurement run as a formatted PDF report (B3 PDF)

### Application Solutions Library (SOL)

- [ ] **SOL-01**: Operator can pick a named measurement solution (select task → frame features → value + PASS/FAIL + report) from a reusable framework
- [ ] **SOL-02**: Operator can run the gear tooth-count / pitch solution (first template)
- [ ] **SOL-03**: Operator can run the PCD / hole-array solution
- [ ] **SOL-04**: Operator can run the shaft/bore-diameter and pin-pitch solution

## v2 Requirements

Deferred — blocked on hardware, external spec, or explicit decision. Not in the current roadmap.

### Calibration (CAL) — blocked: hardware

- **CAL-01**: Full camera calibration + lens-distortion + world-plane metrology (A1 full) — needs caltab + camera
- **CAL-02**: Anisotropic X/Y half-calibration (A1 half) — needs two orthogonal standard parts; no truth without them (deferred per ROADMAP §5)

### Autofocus (AF) — blocked: hardware

- **AF-01**: Z-axis autofocus to acquire sharpest image (B4) — needs Z-axis motor

### MES (MES) — blocked: external spec

- **MES-01**: MES integration skeleton / result upload (B3 MES) — needs customer's MES protocol spec

### Benchmarking (BENCH) — blocked: hardware

- **BENCH-01**: Real metrological-accuracy validation / CMM benchmarking — needs standard parts + camera + CMM

### GD&T v2 (GDT) — deferred decision

- **GDT-02**: Position / symmetry / orientation tolerance + full A+B+C datum frame — needs CMM benchmarking; highest error risk without standard parts
- **GDT-03**: True peak-to-peak straightness band (replace RMS approximation)

### Adapter testing (ADP) — partially blocked

- **ADP-01**: Full real-image HALCON adapter unit tests — needs real imaging (synthetic-image subset possible sooner)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Full WPF migration / dark mode / full localization | Outside measurement-core value; high risk, low return |
| BackgroundWorker threading / full MainWindow split | Sub-second ops not worth threading risk; split low-value / high-risk |
| Recipe-creation Wizard | Over-designed for a single operator; replaced by empty-state guidance (N3) |
| Tangent-line construction | Not a prerequisite for any GD&T tolerance; no concrete part needs it |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| GUI-01 | Phase 1 | Pending |
| GUI-02 | Phase 1 | Pending |
| GUI-03 | Phase 1 | Pending |
| GUI-04 | Phase 1 | Pending |
| MET2D-01 | Phase 2 | Pending |
| MET2D-02 | Phase 2 | Pending |
| MET2D-03 | Phase 2 | Pending |
| MET2D-04 | Phase 2 | Pending |
| RBST-01 | Phase 3 | Pending |
| RBST-02 | Phase 3 | Pending |
| RPT-01 | Phase 4 | Pending |
| SOL-01 | Phase 5 | Pending |
| SOL-02 | Phase 5 | Pending |
| SOL-03 | Phase 5 | Pending |
| SOL-04 | Phase 5 | Pending |

**Coverage:**
- v1 requirements: 15 total
- Mapped to phases: 15
- Unmapped: 0 ✓

---
*Requirements defined: 2026-06-30*
*Last updated: 2026-06-30 after ingest bootstrap*


<!-- ===================================================== -->
# 原始檔：.planning/ROADMAP.md
<!-- ===================================================== -->

# Roadmap: FlashMeasurementSystem

## Overview

The system already runs a complete pass/fail measurement pipeline (1D caliper + XLD fitting +
template matching + geometry construction + GD&T form tolerance, with CSV reporting). This
roadmap covers the forward journey from that baseline toward **mainstream measurement-instrument
capability**. It starts with low-risk operator-experience polish, then delivers the mainstream
differentiator (the 2D Metrology Model), hardens measurement for production (fuzzy edge + GR&R),
adds professional PDF reporting, and finally packages everything into reusable application-level
measurement solutions. All phases are verifiable on replay/synthetic images — no hardware
required. Hardware- and external-spec-blocked work is tracked separately (see Deferred & Blocked).

## Phases

**Phase Numbering:**

- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

- [x] **Phase 1: Operator Experience** - Empty-state guidance, PASS/FAIL banner, tolerance-limit display, in-editor trial measure
- [ ] **Phase 2: 2D Metrology Model** - Auto-placed measure rectangles and robust one-click multi-feature measurement (mainstream differentiator)
- [ ] **Phase 3: Production Robustness** - Fuzzy/robust edge measurement + GR&R/repeatability self-test
- [ ] **Phase 4: PDF Reporting** - Formatted PDF measurement reports beyond CSV
- [ ] **Phase 5: Application Solutions Library** - Named measurement solutions (gear, PCD, diameter, pin-pitch) on existing primitives

## Phase Details

### Phase 1: Operator Experience

**Goal**: An operator can read measurement state and outcomes at a glance, and tune a recipe without leaving the editor.
**Depends on**: Nothing (first phase)
**Requirements**: GUI-01, GUI-02, GUI-03, GUI-04
**Success Criteria** (what must be TRUE):

  1. With no recipe/image loaded, the operator sees clear next-step guidance instead of an empty window
  2. After a run, the operator sees an unmissable PASS or FAIL banner reflecting the overall result
  3. Each measured value is shown next to its tolerance upper/lower limits as the operator works
  4. The operator can trigger a trial measurement from inside the recipe editor and see the result without switching context

**Plans**: 2 plans
Plans:
**Wave 1**

- [ ] 01-01-PLAN.md — Empty-state guidance (GUI-01/N3) + PASS/FAIL banner (GUI-02/N2) — Wave 1

**Wave 2** *(blocked on Wave 1 completion)*

- [ ] 01-02-PLAN.md — Live tolerance-limit display (GUI-03/N5) + in-editor trial measure (GUI-04/A1) — Wave 2 (depends on 01-01)

**UI hint**: yes

### Phase 2: 2D Metrology Model

**Goal**: An operator can define a metrology model on nominal geometry and measure many features of a part in one robust, repeatable pass.
**Depends on**: Phase 1
**Requirements**: MET2D-01, MET2D-02, MET2D-03, MET2D-04
**Success Criteria** (what must be TRUE):

  1. The operator can lay out a metrology model whose measure rectangles auto-distribute along the nominal geometry
  2. Applying the model to a replay image fits line/circle/ellipse/rectangle and returns parameters plus measure points
  3. A metrology model saves into a recipe and runs alongside the existing 1D pipeline without breaking it
  4. The operator can measure multiple features of a part with a single click

**Plans**: 4 plans
Plans:
**Wave 1**

- [ ] 02-01-PLAN.md — Domain DTOs + Application interface + additive Recipe v6 + Wave-0 test scaffolds (MET2D-01, MET2D-03) — Wave 1

**Wave 2** *(blocked on Wave 1)*

- [ ] 02-02-PLAN.md — HalconMetrologyModelRunner adapter + reference_system confirmation + synthetic-image fit tests (MET2D-02, MET2D-04) — Wave 2 (depends on 02-01)

**Wave 3** *(blocked on Wave 2)*

- [ ] 02-03-PLAN.md — RecipeRunner additive Pass 3 + MainWindow injection + metrology overlay (MET2D-03, MET2D-04) — Wave 3 (depends on 02-01, 02-02)

**Wave 4** *(blocked on Wave 3)*

- [ ] 02-04-PLAN.md — Metrology-model editor + synthetic images/ground-truth sheet + GUI acceptance (MET2D-01, MET2D-02, MET2D-04) — Wave 4 (depends on 02-01, 02-02, 02-03)

**UI hint**: yes

### Phase 3: Production Robustness

**Goal**: An operator can get stable measurements on noisy/reflective parts and prove the system's measurement capability with statistics.
**Depends on**: Phase 2
**Requirements**: RBST-01, RBST-02
**Success Criteria** (what must be TRUE):

  1. The operator can switch a measurement to fuzzy/robust mode and get the correct edge despite noise, glare, or interfering edges
  2. Fuzzy mode coexists with existing `measure_pos` measurements without changing their results
  3. The operator can repeat a recipe N times and read a GR&R report with 6σ and repeatability/reproducibility percentages

**Plans**: TBD

### Phase 4: PDF Reporting

**Goal**: An operator can hand off a professional, shareable measurement report, not just a CSV dump.
**Depends on**: Phase 3
**Requirements**: RPT-01
**Success Criteria** (what must be TRUE):

  1. The operator can export a completed measurement run as a formatted PDF report
  2. The PDF includes per-feature measured values, tolerances, and overall PASS/FAIL
  3. Existing CSV export continues to work unchanged

**Plans**: TBD

### Phase 5: Application Solutions Library

**Goal**: An operator can pick a named, end-to-end measurement solution for a real part class and get value + PASS/FAIL + report without assembling primitives by hand.
**Depends on**: Phase 4
**Requirements**: SOL-01, SOL-02, SOL-03, SOL-04
**Success Criteria** (what must be TRUE):

  1. The operator can choose a named solution and follow a "select task → frame features → value + PASS/FAIL + report" flow
  2. The gear tooth-count / pitch solution produces a correct count and pitch on a replay image
  3. The PCD / hole-array solution reports center, diameter, and hole spacing
  4. The shaft/bore-diameter and pin-pitch solution reports diameters and pitch with PASS/FAIL

**Plans**: TBD
**UI hint**: yes

## Deferred & Blocked

Not numbered phases — tracked in REQUIREMENTS.md (v2). Promote to a phase when the unlock condition is met.

| Capability | Req | Blocked by |
|------------|-----|------------|
| A1 full camera calibration + distortion + world-plane | CAL-01 | Hardware: caltab + camera |
| A1 anisotropic X/Y half-calibration | CAL-02 | Hardware: two orthogonal standard parts (no truth without them) |
| B4 Z-axis autofocus | AF-01 | Hardware: Z-axis motor |
| MES integration skeleton | MES-01 | External: customer MES protocol spec |
| CMM benchmarking / metrological-accuracy validation | BENCH-01 | Hardware: standard parts + camera + CMM |
| GD&T position/symmetry/orientation + full datum frame | GDT-02 | Decision: needs CMM benchmarking; deferred to v2 |
| Full real-image HALCON adapter unit tests | ADP-01 | Hardware: real imaging (synthetic subset possible sooner) |

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Operator Experience | 2/2 | Complete | 2026-06-30 |
| 2. 2D Metrology Model | 0/4 | Not started | - |
| 3. Production Robustness | 0/TBD | Not started | - |
| 4. PDF Reporting | 0/TBD | Not started | - |
| 5. Application Solutions Library | 0/TBD | Not started | - |


<!-- ===================================================== -->
# 原始檔：.planning/phases/02-2d-metrology-model/02-CONTEXT.md
<!-- ===================================================== -->

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


<!-- ===================================================== -->
# 原始檔：.planning/phases/02-2d-metrology-model/02-RESEARCH.md
<!-- ===================================================== -->

# Phase 2: 2D Metrology Model — Research

**Researched:** 2026-06-30
**Domain:** HALCON 17.12 metrology-model API + .NET feature-adapter integration
**Confidence:** HIGH (all operator signatures verified against halcon_pdf/reference/reference_hdevelop.txt)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **Master = nominal geometry parameters (Option A).** Nominal geometry stored in recipe IS the master.
- **CAD / DXF master import (Option B) is DEFERRED.** Do not implement DXF/CAD in Phase 2.
- **No hardware calibration.** Phase 2 outputs/acceptance are in pixel units. Absolute mm accuracy is NOT a success criterion.
- **mm conversion** if shown at all reuses the existing manual pixel-size path (display-only, not validated here).
- **Coexistence (MET2D-03):** Metrology model saves into the existing recipe (`.zcp`) and runs alongside the existing 1D pipeline. Loading/running an old recipe with no metrology model must behave exactly as before (backward compatible).
- **HALCON usage:** Use HALCON 17.12 metrology-model operators. Confirm every operator name, parameter order, and value list against `halcon_pdf/reference`. HALCON belongs only in `FlashMeasurementSystem.Halcon`.
- **Architecture:** Follow the existing "feature adapter" pattern. Old-style `.csproj` — new files need explicit `<Compile Include>`.
- **Verify under x64** in addition to Any CPU.
- **Execution must be done with Claude, not the GLM executor.**
- **Verification:** Verified on synthetic images with known pixel ground-truth. Acceptance uses tolerance bands, not exact equality.
- **Build gates:** `dotnet build … x64` 0/0, `Tests.exe` all pass, GUI human acceptance.

### Claude's Discretion
- Domain DTO shapes
- Exact UI placement of the metrology-model editor controls
- Measure-rectangle auto-distribution spacing defaults
- How the metrology model serializes inside the recipe schema (additive, backward compatible)

### Deferred Ideas (OUT OF SCOPE)
- Option B — CAD / DXF master import
- mm absolute metrological accuracy / calibration (CAL-01/02)
- CMM benchmarking (BENCH-01)
- Phase 3 fuzzy/robust edge mode and GR&R
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MET2D-01 | Operator can define a metrology model that auto-places measure rectangles along nominal geometry | HALCON auto-distributes via `num_measures`/`measure_distance` param; all 4 shape types supported |
| MET2D-02 | Applying the model to a replay image robustly fits line/circle/ellipse/rectangle and returns parameters + measure points | `apply_metrology_model` uses RANSAC internally; `get_metrology_object_result` returns fitted params; `get_metrology_object_measures` returns edge positions |
| MET2D-03 | A metrology model is saved in a recipe and coexists with the existing 1D measurement pipeline | Recipe v6: additive `MetrologyModel` field (null = backward compat); RecipeRunner Pass 3 (additive) |
| MET2D-04 | Operator can measure multiple part features in one click via the metrology model | Single `apply_metrology_model` call processes all objects; `get_metrology_object_result` with Index='all' returns all results |
</phase_requirements>

---

## Summary

HALCON 17.12 ships a complete 2D metrology model API (Ch.2, 27 operators, L4511–7451 in
reference_hdevelop.txt). The workflow is: create model → set image size → add nominal-geometry objects
(line/circle/ellipse/rectangle) → optionally align to current match → apply to image → query fitted parameters
and located edge points. HALCON internally auto-distributes measure rectangles along the nominal contour and
uses a RANSAC algorithm to fit shapes robustly, so the planner does not need to implement any of this logic
from scratch.

The integration follows the existing feature-adapter pattern exactly: new Domain DTOs
(`MetrologyObjectDef`, `MetrologyObjectResult`, `MetrologyModelDef`, `MetrologyModelResult`),
a new Application interface (`IMetrologyModelRunner`), a new Halcon adapter
(`HalconMetrologyModelRunner`), and console tests in `Tests.Halcon`. The Recipe schema gains one
additive nullable field (`MetrologyModel`) bumping `SchemaVersion` to 6. RecipeRunner gains a new
"Pass 3" that runs after all 1D passes if `recipe.MetrologyModel != null`. All existing 1D behaviour is
entirely unchanged.

The alignment bridge between template matching and the metrology model uses HALCON's built-in
`align_metrology_model` + `set_metrology_model_param('reference_system', ...)` — which is
cleaner than the per-ROI manual transform used for 1D tools.

**Primary recommendation:** Implement the metrology model as a single feature adapter. Build the
HALCON handle fresh on every `Apply()` call (create → add objects → align → apply → query → clear in
a try/finally), storing only serializable DTOs in the recipe. No persistent HALCON handle in the recipe.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Nominal geometry definition (MET2D-01) | Domain | — | Pure data (MetrologyObjectDef), no HALCON |
| Measure-rectangle auto-distribution | HALCON adapter | — | HALCON owns this internally via num_measures/measure_distance |
| RANSAC robust fitting (MET2D-02) | HALCON adapter | — | apply_metrology_model uses RANSAC internally |
| Recipe persistence (MET2D-03) | Infrastructure (RecipeStore) | Domain | JSON serialization of MetrologyModelDef alongside existing tools |
| One-click multi-feature run (MET2D-04) | App.Wpf (RecipeRunner) | Application | RecipeRunner orchestrates; IMetrologyModelRunner executes |
| Result display / overlay | App.Wpf (MainWindow/OverlayAnnotator) | — | Draws fitted contours and edge points |
| Alignment to current match pose | HALCON adapter | — | align_metrology_model called with matchRow/Col/Angle |

---

## HALCON 17.12 Metrology Model Workflow

### Step Sequence (verified against reference_hdevelop.txt and solution_guide_iii_b_2d_measuring.md)

```
1. create_metrology_model            → MetrologyHandle
2. set_metrology_model_image_size    (call BEFORE adding objects for efficiency)
3. set_metrology_model_param         'reference_system' [RefRow, RefCol, RefAngleRad]
                                     (only if HasReferencePose = true)
4. add_metrology_object_*_measure    × N  (one per feature)
5. align_metrology_model             matchRow, matchCol, matchAngleRad
                                     (only if HasReferencePose && hasMatch)
6. apply_metrology_model             (Image must be single-channel)
7. get_metrology_object_result       Index, 'all', 'result_type', 'all_param'
   get_metrology_object_result       Index, 'all', 'result_type', 'score'
   get_metrology_object_result       Index, 'all', 'used_edges', 'row' / 'column'
   get_metrology_object_measures     Index, 'all'  → measure-region contours + edge coords
8. clear_metrology_model             (in finally block — always runs)
```

---

## Verbatim Operator Signatures (HALCON 17.12)

All line numbers are in `halcon_pdf/reference/reference_hdevelop.txt`.

### 1. `create_metrology_model` (L5868)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L5868]

```
create_metrology_model ( : : : MetrologyHandle )
```

- **MetrologyHandle** (output_control): metrology_model ; integer — handle for all subsequent ops.
- Note: call `set_metrology_model_image_size` immediately after for efficiency.

---

### 2. `set_metrology_model_image_size` (L6889)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L6889]

```
set_metrology_model_image_size ( : : MetrologyHandle, Width, Height : )
```

- **Width** (integer, default 640), **Height** (integer, default 480).
- **Must be called BEFORE `add_metrology_object_*`**. If called after or if image size changes
  at apply time, HALCON recomputes all measure regions (performance hit).
- Typical call: `HOperatorSet.GetImageSize(image, out HTuple w, out HTuple h)` then pass `w.I, h.I`.

---

### 3. `set_metrology_model_param` — reference_system (L6962)
[VERIFIED: halcon_pdf/solution_guide/output/solution_guide_iii_b_2d_measuring/solution_guide_iii_b_2d_measuring.md L508]

```
set_metrology_model_param ( : : MetrologyHandle, GenParamName, GenParamValue : )
  GenParamName = 'reference_system'
  GenParamValue = [RefRow, RefCol, RefAngleRad]   // 3-element tuple
```

- Sets the coordinate system in which nominal geometry objects are defined.
- At run time, `align_metrology_model` maps this reference frame to the current match position.
- If `HasReferencePose = false`, skip this call; nominal geometry is in absolute image coordinates.

---

### 4. `add_metrology_object_line_measure` (L5195)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L5195-5317]

```
add_metrology_object_line_measure ( : : MetrologyHandle,
    RowBegin, ColumnBegin, RowEnd, ColumnEnd,
    MeasureLength1, MeasureLength2, MeasureSigma, MeasureThreshold,
    GenParamName, GenParamValue : Index )
```

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| RowBegin, ColumnBegin | real/int | — | Start point (Y, X) of nominal line |
| RowEnd, ColumnEnd | real/int | — | End point (Y, X) of nominal line |
| MeasureLength1 | real | 20.0 | Half-length **perpendicular** to boundary (search depth). Min 1.0. |
| MeasureLength2 | real | 5.0 | Half-length **tangential** to boundary (box width). Min 1.0. |
| MeasureSigma | real | 1.0 | Gaussian smoothing sigma. Range [0.4, 100]. |
| MeasureThreshold | real | 30.0 | Minimum edge amplitude. Range [1, 255]. |
| GenParamName / GenParamValue | string[] / value[] | [], [] | Optional; see set_metrology_object_param |
| **Index** (output) | integer | — | Index of the added metrology object (0-based, increments per add call) |

- Minimum measure regions: 2 (one at each endpoint).
- Measure direction: left→right seen from RowBegin→RowEnd direction.
- No restriction on MeasureLength1 vs. line length.

---

### 5. `add_metrology_object_circle_measure` (L4672)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L4672-4832]

```
add_metrology_object_circle_measure ( : : MetrologyHandle,
    Row, Column, Radius,
    MeasureLength1, MeasureLength2, MeasureSigma, MeasureThreshold,
    GenParamName, GenParamValue : Index )
```

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| Row, Column | real/int | — | Center (Y, X) of nominal circle |
| Radius | real/int | — | Nominal radius in pixels |
| MeasureLength1 | real | 20.0 | Half-length perpendicular. **Restriction: MeasureLength1 < Radius** |
| MeasureLength2 | real | 5.0 | Half-length tangential. |
| MeasureSigma | real | 1.0 | Gaussian sigma. [0.4, 100] |
| MeasureThreshold | real | 30.0 | Min edge amplitude. |
| GenParamName 'start_phi' | real | 0.0 | Arc start angle (0 = full circle default) |
| GenParamName 'end_phi' | real | 6.28318 | Arc end angle (2π = full circle) |
| GenParamName 'point_order' | string | 'positive' | 'positive' (CCW) or 'negative' (CW) |
| **Index** (output) | integer | — | Object index |

- Minimum measure regions: 3.
- Measure direction: inside → outside of circle boundary.
- **CRITICAL:** MeasureLength1 < Radius — if violated, HalconException is thrown.

---

### 6. `add_metrology_object_ellipse_measure` (L4834)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L4834-4993]

```
add_metrology_object_ellipse_measure ( : : MetrologyHandle,
    Row, Column, Phi, Radius1, Radius2,
    MeasureLength1, MeasureLength2, MeasureSigma, MeasureThreshold,
    GenParamName, GenParamValue : Index )
```

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| Row, Column | real/int | — | Center (Y, X) of nominal ellipse |
| Phi | real | — | Orientation of main axis [rad], mapped to (-π, π] |
| Radius1 | real/int | — | Larger half axis (pixels) |
| Radius2 | real/int | — | Smaller half axis (pixels) |
| MeasureLength1 | real | 20.0 | Half-length perpendicular. **Restriction: < Radius1 AND < Radius2** |
| MeasureLength2 | real | 5.0 | Half-length tangential. |
| MeasureSigma | real | 1.0 | Gaussian sigma. [0.4, 100] |
| MeasureThreshold | real | 30.0 | Min edge amplitude. |
| GenParamName 'start_phi' / 'end_phi' / 'point_order' | — | 0 / 2π / 'positive' | Arc subset of ellipse |
| **Index** (output) | integer | — | Object index |

- Minimum measure regions: 5.
- **CRITICAL:** MeasureLength1 must be strictly less than BOTH Radius1 and Radius2.
- Phi angle convention is the same as the existing `EllipseFittingResult.Phi`.

---

### 7. `add_metrology_object_rectangle2_measure` (L5319)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L5319-5446]

```
add_metrology_object_rectangle2_measure ( : : MetrologyHandle,
    Row, Column, Phi, Length1, Length2,
    MeasureLength1, MeasureLength2, MeasureSigma, MeasureThreshold,
    GenParamName, GenParamValue : Index )
```

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| Row, Column | real/int | — | Center (Y, X) of nominal rectangle |
| Phi | real | — | Orientation of main axis [rad], mapped to (-π, π] |
| Length1 | real/int | — | Larger half edge (pixels) |
| Length2 | real/int | — | Smaller half edge (pixels) |
| MeasureLength1 | real | 20.0 | Half-length perpendicular. **Restriction: < Length1 AND < Length2** |
| MeasureLength2 | real | 5.0 | Half-length tangential. |
| MeasureSigma | real | 1.0 | Gaussian sigma. [0.4, 100] |
| MeasureThreshold | real | 30.0 | Min edge amplitude. |
| **Index** (output) | integer | — | Object index |

- Minimum measure regions: 8 (2 per side × 4 sides).
- Measure direction: inside → outside of rectangle boundary.
- **CRITICAL:** MeasureLength1 < Length1 AND MeasureLength1 < Length2.
- `rect2` convention = oriented rectangle; same as existing system's `RoiGeometry`.

---

### 8. `align_metrology_model` (L5448)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L5448-5651]

```
align_metrology_model ( : : MetrologyHandle, Row, Column, Angle : )
```

- **Row, Column**: absolute image coordinates of the current match position (from find_shape_model).
- **Angle**: rotation angle [rad] of the current match.
- Semantics: "first rotate by Angle, then translate by Row, Column" relative to the reference_system.
- Alignment is **temporary** — overwritten by the next call; the model definition itself is unchanged.
- Used with template matching: pass matchRow, matchCol, matchAngleRad directly.
- Skip this call entirely if `HasReferencePose = false` or `hasMatch = false`.

---

### 9. `apply_metrology_model` (L5652)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L5652-5742]

```
apply_metrology_model ( Image : : MetrologyHandle : )
```

- **Image**: must be `singlechannelimage ; object : byte / uint2 / real`. Multi-channel input
  silently produces zero edges — same pitfall as `measure_pos`.
- Internally uses `measure_pos` (or `fuzzy_measure_pos` if fuzzy params were set).
- RANSAC fits the geometric shape to located edge positions.
- Raises HalconException if no valid fitting can be found and other conditions (see Pitfalls).
- Results stored in MetrologyHandle until the next `apply_metrology_model` call or `clear_metrology_model`.

---

### 10. `get_metrology_object_result` (L6495)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L6495-6633]

```
get_metrology_object_result ( : : MetrologyHandle, Index, Instance,
    GenParamName, GenParamValue : Parameter )
```

**To get all fitted parameters for a single object:**
```csharp
HOperatorSet.GetMetrologyObjectResult(
    handle, new HTuple(objIndex), "all",
    "result_type", "all_param",
    out HTuple parameter);
```

**Result tuple layout (no calibration → pixel coordinates):**

| Shape | Tuple length | Order |
|-------|-------------|-------|
| circle | 3 × NumInstances | [row, column, radius] per instance |
| ellipse | 5 × NumInstances | [row, column, phi, radius1, radius2] per instance |
| line | 4 × NumInstances | [row_begin, column_begin, row_end, column_end] per instance |
| rectangle | 5 × NumInstances | [row, column, phi, length1, length2] per instance |

**Multi-object query returns instances interleaved:**
index order = (inst0_obj0, inst1_obj0, …, inst0_obj1, inst1_obj1, …)

**To get fitting score:**
```csharp
HOperatorSet.GetMetrologyObjectResult(
    handle, new HTuple(objIndex), "all",
    "result_type", "score",
    out HTuple score);
// score.D is in [0, 1]: fraction of measure regions that contributed an edge
```

**To get used edge points:**
```csharp
HOperatorSet.GetMetrologyObjectResult(
    handle, new HTuple(objIndex), "all",
    "used_edges", "row", out HTuple usedRow);
HOperatorSet.GetMetrologyObjectResult(
    handle, new HTuple(objIndex), "all",
    "used_edges", "column", out HTuple usedCol);
```

---

### 11. `get_metrology_object_measures` (L6150)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L6150-6225]

```
get_metrology_object_measures ( : Contours : MetrologyHandle,
    Index, Transition : Row, Column )
```

- **Contours** (output_object): XLD contours of the rectangular measure regions (for visualising where HALCON searched).
- **Row, Column** (output_control): real arrays of ALL located edge positions across all measure regions for the specified object.
- **Transition**: 'all', 'positive', 'negative'.
- Note: order of (Row, Column) points is **not defined** — cannot be mapped to a specific measure region.
- Note: if called before `apply_metrology_model`, Row/Column are empty.

---

### 12. `get_metrology_object_result_contour` (L6635)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L6635-6698]

```
get_metrology_object_result_contour ( : Contour : MetrologyHandle,
    Index, Instance, Resolution : )
```

- Returns the **fitted** geometric shape as an XLD contour (for overlay display).
- **Resolution**: Euclidean distance between neighboring contour points (default 1.5 px).
- Useful for OverlayAnnotator — display the fitted circle/ellipse/line/rectangle as a contour.

---

### 13. `set_metrology_object_param` (L7214) — key generic parameters
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L7214-7405]

```
set_metrology_object_param ( : : MetrologyHandle, Index,
    GenParamName, GenParamValue : )
```

| GenParamName | Default | Effect |
|-------------|---------|--------|
| `'num_measures'` | — | Desired number of measure regions (auto-spaced). Min: circle=3, ellipse=5, line=2, rect=8. |
| `'measure_distance'` | 10.0 px | Distance between measure region centers. If set, `num_measures` has no effect. |
| `'measure_length1'` | 20.0 | Half-length perpendicular (search depth). |
| `'measure_length2'` | 5.0 | Half-length tangential (box width). |
| `'measure_sigma'` | 1.0 | Gaussian smoothing sigma [0.4, 100]. |
| `'measure_threshold'` | 30.0 | Min edge amplitude. |
| `'measure_transition'` | 'all' | 'all', 'positive', 'negative', 'uniform'. |
| `'min_score'` | 0.7 | Min fraction of measure regions that must have a valid edge. Range: [0, 1]. |
| `'num_instances'` | 1 | Max number of shape instances returned. |
| `'distance_threshold'` | 3.5 | RANSAC inlier radius (px). |
| `'max_num_iterations'` | -1 | RANSAC max iterations (-1 = unlimited). |
| `'rand_seed'` | 42 | RANSAC random seed (42 = reproducible; 0 = time-based). |
| `'instances_outside_measure_regions'` | 'false' | 'false' = reject instances outside measure region major axes. |

**How auto-distribution works (MET2D-01):**
HALCON spaces measure rectangles evenly along the nominal geometry contour using
`measure_distance` (default 10 px) or `num_measures` (fixed count). The developer never
computes or places individual measure rectangles — HALCON owns this entirely.

---

### 14. `clear_metrology_model` (L5744)
[VERIFIED: halcon_pdf/reference/reference_hdevelop.txt L5744-5867]

```
clear_metrology_model ( : : MetrologyHandle : )
```

- Frees all memory. Must be called in a `finally` block to avoid handle leaks.
- After this call, MetrologyHandle is invalid.

---

## MET2D-01: Auto-Distribution of Measure Rectangles

**How it works:** HALCON internally distributes measure rectangles along the nominal geometry.
The developer sets `measure_distance` (default 10.0 px) or `num_measures`; HALCON places
the boxes. There is no manual placement code.

**Recommended defaults for Phase 2:**
- `MeasureLength1` = 20.0 px (search depth — must be less than the shape's smallest dimension)
- `MeasureLength2` = 5.0 px (tangential width)
- `MeasureSigma` = 1.0 (Gaussian smoothing; increase to 1.5–2.0 for blurry images)
- `MeasureThreshold` = 30.0 (edge amplitude; lower to 15–20 for low-contrast images)
- `measure_distance` = 10.0 px (default; operator can tune per object)

**Operator-visible result (UI):** after adding an object, call
`get_metrology_object_model_contour` to get the nominal geometry contour and
`get_metrology_object_measures` (before apply) to display the grey measure-region
rectangles — this gives the operator visual feedback confirming placement.

---

## MET2D-02: Apply and Read Back Results

**Single-call multi-feature measurement:**
```csharp
// In HalconMetrologyModelRunner.Apply():
HOperatorSet.ApplyMetrologyModel(grayImage, handle);

foreach (int idx in objectIndices)
{
    // Fitted parameters
    HOperatorSet.GetMetrologyObjectResult(
        handle, idx, "all", "result_type", "all_param", out HTuple param);

    // Fitting score
    HOperatorSet.GetMetrologyObjectResult(
        handle, idx, "all", "result_type", "score", out HTuple score);

    // Used edge points (for display)
    HOperatorSet.GetMetrologyObjectResult(
        handle, idx, "all", "used_edges", "row", out HTuple edgeRow);
    HOperatorSet.GetMetrologyObjectResult(
        handle, idx, "all", "used_edges", "column", out HTuple edgeCol);
}
```

**Result extraction per shape (single instance, all_param):**

```csharp
// Circle: param = [row, col, radius]
double centerRow   = param[0].D;
double centerCol   = param[1].D;
double radius      = param[2].D;

// Ellipse: param = [row, col, phi, radius1, radius2]
double centerRow   = param[0].D;
double centerCol   = param[1].D;
double phi         = param[2].D;  // [rad], in (-π, π]
double radius1     = param[3].D;  // larger half axis
double radius2     = param[4].D;  // smaller half axis

// Line: param = [row_begin, col_begin, row_end, col_end]
double rowBegin    = param[0].D;
double colBegin    = param[1].D;
double rowEnd      = param[2].D;
double colEnd      = param[3].D;

// Rectangle: param = [row, col, phi, length1, length2]
double centerRow   = param[0].D;
double centerCol   = param[1].D;
double phi         = param[2].D;  // [rad]
double length1     = param[3].D;  // larger half edge
double length2     = param[4].D;  // smaller half edge
```

**Measure points (per-object edge positions):**
```csharp
HOperatorSet.GetMetrologyObjectMeasures(
    out HObject contours, handle, new HTuple(idx), "all",
    out HTuple edgeRow, out HTuple edgeCol);
// edgeRow, edgeCol: real arrays, order not defined
```

---

## MET2D-03: Concrete Integration Design (Feature Adapter Pattern)

### New Files (4-layer feature adapter)

```
src/FlashMeasurementSystem.Domain/MetrologyModel/
├── MetrologyObjectType.cs          // enum { Line, Circle, Ellipse, Rectangle }
├── MetrologyObjectDef.cs           // nominal geometry + measure params (one object)
├── MetrologyModelDef.cs            // List<MetrologyObjectDef> + image-size hint
├── MetrologyObjectResult.cs        // fitted params + score + measure points (one object)
└── MetrologyModelResult.cs         // List<MetrologyObjectResult>

src/FlashMeasurementSystem.Application/MetrologyModel/
└── IMetrologyModelRunner.cs        // interface over Domain types only

src/FlashMeasurementSystem.Halcon/MetrologyModel/
└── HalconMetrologyModelRunner.cs   // adapter: create → add → align → apply → query → clear

tests/FlashMeasurementSystem.Tests/MetrologyModelDomainTests.cs     // Domain DTO defaults
tests/FlashMeasurementSystem.Tests.Halcon/MetrologyModelHalconTests.cs  // synthetic-image tests
```

### Domain DTOs (Claude's discretion)

**`MetrologyObjectDef.cs`:**
```csharp
public class MetrologyObjectDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public MetrologyObjectType Shape { get; set; } = MetrologyObjectType.Line;

    // Nominal geometry — one set per shape type; others ignored.
    // Line:
    public double RowBegin { get; set; }
    public double ColumnBegin { get; set; }
    public double RowEnd { get; set; }
    public double ColumnEnd { get; set; }
    // Circle / Ellipse / Rectangle — centre:
    public double Row { get; set; }
    public double Column { get; set; }
    // Circle:
    public double Radius { get; set; }
    // Ellipse / Rectangle:
    public double Phi { get; set; }
    public double Radius1 { get; set; }   // ellipse larger half axis
    public double Radius2 { get; set; }   // ellipse smaller half axis
    public double Length1 { get; set; }   // rect larger half edge
    public double Length2 { get; set; }   // rect smaller half edge

    // Measure params (same for all shape types)
    public double MeasureLength1 { get; set; } = 20.0;
    public double MeasureLength2 { get; set; } = 5.0;
    public double MeasureSigma { get; set; } = 1.0;
    public double MeasureThreshold { get; set; } = 30.0;
    public double MeasureDistance { get; set; } = 10.0;  // 0 = use num_measures instead
    public int NumMeasures { get; set; } = 0;             // 0 = use MeasureDistance

    // Optional tolerance on the primary fitted parameter (e.g. radius for circle)
    public ToleranceSpec Tolerance { get; set; } = null;
}
```

**`MetrologyModelDef.cs`:**
```csharp
public class MetrologyModelDef
{
    public List<MetrologyObjectDef> Objects { get; set; } = new List<MetrologyObjectDef>();
    // Stored image size hint; set when model is built so Apply can avoid recomputing regions.
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
}
```

**`MetrologyObjectResult.cs`:**
```csharp
public class MetrologyObjectResult
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public MetrologyObjectType Shape { get; set; }
    public bool Success { get; set; }
    public double Score { get; set; }  // [0, 1] fraction of measure regions with edge
    public string ErrorMessage { get; set; } = "";

    // Fitted geometry (only the fields for this shape type are populated)
    // Line:
    public double FitRowBegin { get; set; }
    public double FitColumnBegin { get; set; }
    public double FitRowEnd { get; set; }
    public double FitColumnEnd { get; set; }
    // Circle:
    public double FitRow { get; set; }
    public double FitColumn { get; set; }
    public double FitRadius { get; set; }
    // Ellipse:
    public double FitPhi { get; set; }
    public double FitRadius1 { get; set; }
    public double FitRadius2 { get; set; }
    // Rectangle:
    public double FitLength1 { get; set; }
    public double FitLength2 { get; set; }

    // All edge points located in measure regions (from get_metrology_object_measures)
    public List<double> MeasurePointRows { get; set; } = new List<double>();
    public List<double> MeasurePointCols { get; set; } = new List<double>();

    // Optional tolerance judgement (same judger as 1D pipeline)
    public bool? IsOk { get; set; }
    public string ValueText { get; set; } = "";
}
```

### Application Interface

```csharp
// IMetrologyModelRunner.cs
public interface IMetrologyModelRunner
{
    MetrologyModelResult Apply(
        MetrologyModelDef model,
        double refRow, double refCol, double refAngleRad,
        bool hasReferencePose,
        HImage image,
        double matchRow, double matchCol, double matchAngleRad,
        bool hasMatch);
}
```

### Halcon Adapter Skeleton

```csharp
// HalconMetrologyModelRunner.cs
public class HalconMetrologyModelRunner : IMetrologyModelRunner
{
    public MetrologyModelResult Apply(MetrologyModelDef model,
        double refRow, double refCol, double refAngleRad,
        bool hasReferencePose,
        HImage image,
        double matchRow, double matchCol, double matchAngleRad,
        bool hasMatch)
    {
        var result = new MetrologyModelResult();
        HTuple handle = new HTuple();
        HImage grayImage = null;
        bool disposGray = false;
        try
        {
            // 1. Single-channel enforcement
            HOperatorSet.CountChannels(image, out HTuple channels);
            if (channels.I > 1)
            {
                HOperatorSet.Rgb1ToGray(image, out HObject gray);
                grayImage = (HImage)gray;
                disposGray = true;
            }
            else { grayImage = image; }

            // 2. Create model
            HOperatorSet.CreateMetrologyModel(out handle);

            // 3. Image size (from stored hint or live query)
            int w = model.ImageWidth > 0 ? model.ImageWidth : 0;
            int h = model.ImageHeight > 0 ? model.ImageHeight : 0;
            if (w == 0 || h == 0)
                HOperatorSet.GetImageSize(grayImage, out HTuple tw, out HTuple th)
                    .Then(() => { w = tw.I; h = th.I; });
            HOperatorSet.SetMetrologyModelImageSize(handle, w, h);

            // 4. Reference system
            if (hasReferencePose)
                HOperatorSet.SetMetrologyModelParam(handle, "reference_system",
                    new HTuple(new double[] { refRow, refCol, refAngleRad }));

            // 5. Add objects
            var indices = new int[model.Objects.Count];
            for (int i = 0; i < model.Objects.Count; i++)
            {
                MetrologyObjectDef obj = model.Objects[i];
                HTuple idx;
                AddMetrologyObject(handle, obj, out idx);
                indices[i] = idx.I;
            }

            // 6. Align
            if (hasReferencePose && hasMatch)
                HOperatorSet.AlignMetrologyModel(handle, matchRow, matchCol, matchAngleRad);

            // 7. Apply
            HOperatorSet.ApplyMetrologyModel(grayImage, handle);

            // 8. Query results
            for (int i = 0; i < model.Objects.Count; i++)
            {
                MetrologyObjectDef def = model.Objects[i];
                MetrologyObjectResult objResult = QueryResult(handle, indices[i], def);
                result.Objects.Add(objResult);
            }
        }
        catch (HalconException ex)
        {
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            if (handle.Length > 0)
                HOperatorSet.ClearMetrologyModel(handle);
            if (disposGray && grayImage != null)
                grayImage.Dispose();
        }
        return result;
    }
    // ... AddMetrologyObject and QueryResult helpers ...
}
```

### Recipe Schema — Additive v6 Change

In `src/FlashMeasurementSystem.Domain/Roi/Recipe.cs`, add one field:

```csharp
// v6: 2D metrology model (additive; null = no metrology, backward compatible).
// Old recipes load with MetrologyModel = null (Newtonsoft JSON default) — behaviour unchanged.
public MetrologyModelDef MetrologyModel { get; set; } = null;
```

Bump `SchemaVersion` default from 5 to 6. No migration code needed — null field is
transparently deserialized as null by Newtonsoft.

### RecipeRunner Integration — Additive Pass 3

In `RecipeRunner.Run()`, after Pass 2 (distance/angle tools), add:

```csharp
// ── Pass 3: 2D Metrology Model (MET2D) ──
if (_metrologyRunner != null
    && recipe.MetrologyModel != null
    && recipe.MetrologyModel.Objects != null
    && recipe.MetrologyModel.Objects.Count > 0)
{
    bool hasAlign = recipe.HasReferencePose && hasMatch;
    MetrologyModelResult mResult = _metrologyRunner.Apply(
        recipe.MetrologyModel,
        recipe.RefRow, recipe.RefCol, recipe.RefAngleRad,
        recipe.HasReferencePose,
        image,
        hasAlign ? matchRow : 0.0,
        hasAlign ? matchCol : 0.0,
        hasAlign ? matchAngleRad : 0.0,
        hasAlign);

    foreach (MetrologyObjectResult objResult in mResult.Objects)
    {
        var res = MapToToolRunResult(objResult, pixelSizeUm);
        results.Add(res);
        if (!string.IsNullOrEmpty(objResult.Id))
            byId[objResult.Id] = res;
    }
}
```

`_metrologyRunner` injected via RecipeRunner constructor (same injection pattern as `_circleFitter`).
When `_metrologyRunner` is null (unit tests, no HALCON), Pass 3 is silently skipped.

---

## MET2D-04: One-Click Multi-Feature Run

`apply_metrology_model` processes all objects in the model in one call. The operator
clicks "Run" (existing button) → RecipeRunner.Run() → Pass 3 iterates all
`MetrologyModelDef.Objects` and returns one `MetrologyObjectResult` per object → results
displayed in the existing results table. No new button or UI mechanism needed beyond
what the existing "Run" flow already provides.

---

## Pixel-Space Synthetic Image Verification

### Approach

Tests live in `tests/FlashMeasurementSystem.Tests.Halcon/MetrologyModelHalconTests.cs`,
following the `CircleFitterTests` pattern (generate known-geometry point cloud or paint
a region on a synthetic image, assert fitted parameters within tolerance bands).

### Test Cases per Requirement

| Req | Test | Synthetic Image | Tolerance Band |
|-----|------|-----------------|----------------|
| MET2D-01 | Auto-distributed measure regions visible before apply | Blank image; call get_metrology_object_measures before apply — Contours count >= min | Count >= 3 (circle), >= 2 (line) |
| MET2D-02 (line) | Horizontal line at Row=100, Col=50→350 | Paint 1-px-wide white line on black background | FitRowBegin/End within ±0.5 px of 100; FitColBegin within ±0.5 of 50 |
| MET2D-02 (circle) | Circle at Row=200, Col=200, Radius=80 | paint_region(gen_circle) on black background | FitRow, FitColumn within ±0.5 px; FitRadius within ±0.5 px |
| MET2D-02 (ellipse) | Ellipse Row=200, Col=200, Phi=0, R1=100, R2=60 | paint_region(gen_ellipse) | Center ±1.0 px; Phi ±0.02 rad; R1/R2 ±1.0 px |
| MET2D-02 (rect) | Rectangle Row=150, Col=150, Phi=0, L1=80, L2=40 | paint_region(gen_rectangle2) | Center ±0.5 px; Phi ±0.02 rad; L1/L2 ±1.0 px |
| MET2D-02 (score) | Any shape | Above | Score >= 0.6 |
| MET2D-02 (measure pts) | Circle | Above | MeasurePointRows/Cols not empty after apply |
| MET2D-03 | Old recipe (no MetrologyModel field) loads and runs without exception | Deserialize recipe with SchemaVersion=5 | No exception; MetrologyModel = null; 1D results unchanged |
| MET2D-03 | New recipe with MetrologyModel loads correctly | Serialize/deserialize MetrologyModelDef | Object count preserved; all fields round-trip |
| MET2D-04 | Model with 3 objects (line + circle + ellipse) in one Apply call | Composite synthetic image | 3 results returned; all Success = true |

### Synthetic Image Generation (C# HALCON)

```csharp
// Example: synthetic circle test image
HOperatorSet.GenImageConst(out HObject image, "byte", 640, 480);
HOperatorSet.GenCircle(out HObject circle, 200.0, 200.0, 80.0);
HOperatorSet.PaintRegion(circle, image, out HObject paintedImage, 255, "fill");
// paintedImage: white-filled disc on black background
```

For a line (1-px boundary rather than filled region, use `gen_region_line` or paint thin region):
```csharp
HOperatorSet.GenRegionLine(out HObject lineReg, 100, 50, 100, 350);
HOperatorSet.Dilation1(lineReg, out HObject dilLine, "rectangle", 2);
HOperatorSet.PaintRegion(dilLine, image, out HObject paintedImage, 255, "fill");
```

### Ground-Truth Answer Sheet (operator testing)

The planner should schedule a task that generates `data/images/synthetic_metrology_*.png`
with a printed ground-truth table (shape, nominal params, ground-truth fitted params) as
the functional test hand-off artefact. This is the "synthetic test images + ground-truth
answer sheet" referenced in CONTEXT.md.

---

## Common Pitfalls

### Pitfall 1: Multi-channel image passed to apply_metrology_model
**What goes wrong:** `apply_metrology_model` accepts only `singlechannelimage`. Multi-channel
input (RGB) silently returns zero edges and zero results — no exception thrown, but
all MetrologyObjectResult.Success = false.
**Why it happens:** Same root cause as `measure_pos` in the 1D pipeline.
**How to avoid:** Call `HOperatorSet.CountChannels` before apply; if > 1, call `Rgb1ToGray`.
**Warning signs:** Score = 0 or HalconException about "no valid instance found" on first test.

### Pitfall 2: MeasureLength1 too large for small shapes
**What goes wrong:** HalconException at `add_metrology_object_*_measure` time.
- Circle: MeasureLength1 must be < Radius.
- Ellipse: MeasureLength1 must be < Radius1 AND < Radius2.
- Rectangle: MeasureLength1 must be < Length1 AND < Length2.
**How to avoid:** Validate in `HalconMetrologyModelRunner.AddMetrologyObject()` before the call;
return a failed MetrologyObjectResult with clear message instead of propagating the exception
to the whole batch.

### Pitfall 3: set_metrology_model_image_size called after adding objects
**What goes wrong:** Performance degradation — all measure regions recomputed.
**How to avoid:** Always call `set_metrology_model_image_size` before any `add_metrology_object_*`.
**Ordering rule:** create → set_image_size → set_reference_system → add objects → align → apply.

### Pitfall 4: MetrologyHandle not cleared on exception
**What goes wrong:** Handle leak if HalconException is thrown during add/apply.
**How to avoid:** Wrap the entire create→clear lifecycle in try/finally. `clear_metrology_model`
in the finally block. Never store the handle in a field across calls.

### Pitfall 5: Coordinate system confusion (Row, Col = Y, X)
**What goes wrong:** Nominal geometry swapped (X and Y reversed) — circle appears off-centre.
**How to avoid:** HALCON always uses (Row, Column) = (Y, X). When the UI stores a pixel position
as (x_screen, y_screen), swap to (y_screen, x_screen) = (Row, Column).

### Pitfall 6: get_metrology_object_result tuple indexing off by shape
**What goes wrong:** Reading radius as circle[2] works, but reading phi as ellipse[2] — correct;
reading radius1 as ellipse[3] — correct. BUT if multiple instances exist (num_instances > 1),
the tuple is [row0, col0, phi0, r10, r20, row1, col1, phi1, r11, r21, ...], not a single 5-tuple.
**How to avoid:** Default num_instances = 1; for Phase 2, always set num_instances = 1 explicitly
unless the use-case requires multi-instance. Read exactly N values per shape type per instance.

### Pitfall 7: align_metrology_model with absolute vs. delta coordinates
**What goes wrong:** Passing the delta from reference to match instead of the absolute match position.
**How to avoid:** Pass `matchRow, matchCol, matchAngleRad` directly from find_shape_model
(or the existing template match result). These are absolute image coordinates, not deltas.
The HALCON documentation confirms: "alignment relative to the image coordinate system which
has its origin in the top left corner".

### Pitfall 8: Old-style csproj — new files silently excluded
**What goes wrong:** New `.cs` files compile fine locally (VS includes all files) but are excluded
from `dotnet build` because old-style csproj requires explicit `<Compile Include="..." />` entries.
**How to avoid:** Add `<Compile Include="MetrologyModel\MetrologyObjectDef.cs" />` etc. to each
`.csproj` file as each new file is created. Follow the same pattern as existing CircleFitting entries.

### Pitfall 9: Fuzzy params interaction
**What goes wrong:** If `set_metrology_object_fuzzy_param` is called on any object, the whole
model switches to `fuzzy_measure_pos` internally — affecting all objects.
**How to avoid:** Phase 2 must NOT call `set_metrology_object_fuzzy_param`. Fuzzy mode is Phase 3.

### Pitfall 10: apply_metrology_model ignores image domain
**What goes wrong:** Expecting domain/ROI masking to work. The documentation states
"apply_metrology_model ignores the domain of Image for efficiency reasons (see also measure_pos)".
**How to avoid:** The nominal geometry must be positioned so that measure regions only cover
the part of interest. No region masking can limit where HALCON searches.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Distributing measure rectangles along a contour | Custom geometry math | `add_metrology_object_*_measure` + `measure_distance` param | HALCON handles arc/ellipse tangent math, edge cases, minimum count enforcement |
| RANSAC fitting for line/circle/ellipse/rect | Custom RANSAC | `apply_metrology_model` | HALCON's RANSAC is optimised, handles partial coverage, multi-instance |
| Sub-pixel edge location within measure regions | Custom edge interpolation | `apply_metrology_model` (uses `measure_pos` internally) | Same sub-pixel precision as the existing 1D pipeline |
| Aligned coordinate transforms for metrology | Manual per-object transform | `set_metrology_model_param('reference_system')` + `align_metrology_model` | HALCON applies rigid body transform to all objects in one call |
| Parsing fitted parameter tuples per shape | Custom enum dispatch | Document the fixed tuple layouts from the reference | Layouts are fixed by HALCON; just index correctly |

**Key insight:** HALCON's metrology model is a complete measurement pipeline. The adapter's job
is to translate Domain DTOs in → extract Domain DTOs out, and to handle the HALCON handle
lifecycle safely. Do not re-implement any of the measurement logic.

---

## Standard Stack

### Core (no new external dependencies)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `halcondotnet.dll` | 17.12 | HALCON .NET binding | Already in project; all metrology operators available in this version |
| `Newtonsoft.Json` | (existing) | Recipe JSON serialization | Already in project; MetrologyModelDef serializes transparently |

No new NuGet packages required. The HALCON 17.12 Ch.2 2D Metrology API covers all 4 shape types
in scope and includes RANSAC, edge detection, and result access.

## Package Legitimacy Audit

No new external packages are introduced in this phase. Existing `halcondotnet.dll` and
`Newtonsoft.Json` remain unchanged.

---

## Architecture Patterns

### System Architecture Diagram

```
Operator (UI click "Run")
        │
        ▼
RecipeRunner.Run()
  ├── Pass 1: line/circle tools   ──────────► IEdgeDetector + ILineFitter/ICircleFitter
  ├── Pass 1.5: construction tools
  ├── Pass 1.7: GD&T tools
  ├── Pass 2: distance/angle tools
  └── Pass 3: MetrologyModel (new)
              │
              ▼
        IMetrologyModelRunner.Apply()
              │
              ▼
        HalconMetrologyModelRunner
          ├── rgb1_to_gray (if multi-channel)
          ├── create_metrology_model
          ├── set_metrology_model_image_size
          ├── set_metrology_model_param 'reference_system' [RefRow,RefCol,RefPhi]
          ├── add_metrology_object_line_measure     ┐
          ├── add_metrology_object_circle_measure   ├─ per object
          ├── add_metrology_object_ellipse_measure  │
          ├── add_metrology_object_rect2_measure    ┘
          ├── align_metrology_model (matchRow, matchCol, matchAngle)
          ├── apply_metrology_model ──── RANSAC fit (internal)
          ├── get_metrology_object_result (all_param, score, used_edges)
          ├── get_metrology_object_measures (edge point cloud)
          └── clear_metrology_model (finally)
              │
              ▼
        MetrologyModelResult (Domain DTOs)
              │
              ▼
        List<ToolRunResult> (mapped, added to results list)
              │
              ▼
        MainWindow / OverlayAnnotator (display fitted contours + edge crosses)
```

### Recommended Project Structure Additions

```
src/FlashMeasurementSystem.Domain/MetrologyModel/
├── MetrologyObjectType.cs
├── MetrologyObjectDef.cs
├── MetrologyModelDef.cs
├── MetrologyObjectResult.cs
└── MetrologyModelResult.cs

src/FlashMeasurementSystem.Application/MetrologyModel/
└── IMetrologyModelRunner.cs

src/FlashMeasurementSystem.Halcon/MetrologyModel/
└── HalconMetrologyModelRunner.cs

tests/FlashMeasurementSystem.Tests/
└── MetrologyModelDomainTests.cs

tests/FlashMeasurementSystem.Tests.Halcon/
└── MetrologyModelHalconTests.cs

data/images/
├── synthetic_metrology_line.png
├── synthetic_metrology_circle.png
├── synthetic_metrology_ellipse.png
├── synthetic_metrology_rectangle.png
└── synthetic_metrology_composite.png
```

### Anti-Patterns to Avoid

- **Storing MetrologyHandle in a field or recipe:** The handle is a live HALCON resource.
  Serialize only the Domain DTOs (`MetrologyModelDef`); rebuild the handle on every `Apply()` call.
- **Calling apply_metrology_model on a colour image:** Silently fails (zero results, no exception).
- **Skipping clear_metrology_model:** Handle leak. Always use try/finally.
- **Setting MeasureLength1 >= shape dimension without guard:** HalconException at add time.
- **Implementing measure-rectangle distribution manually:** Unnecessary; HALCON owns this.
- **Mixing metrology results into the 1D tool list by ToolType collisions:** Use distinct ToolType
  values like `"metrology_circle"`, `"metrology_line"` etc. so RecipeRunner passes don't re-process them.

---

## Validation Architecture

No config.json with nyquist_validation = false found; treating as enabled.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | Console Exe with Assert helper — same as existing test suite |
| Config file | None (old-style console Exe) |
| Quick run command | `.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe` |
| Full suite command | `.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe` + `.\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MET2D-01 | Measure-region count meets minimum after add | unit (Domain) | Tests.exe | ❌ Wave 0 |
| MET2D-01 | Measure regions visible before apply | integration (HALCON) | Tests.Halcon.exe | ❌ Wave 0 |
| MET2D-02 (line) | Fitted line params within ±0.5 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ Wave 0 |
| MET2D-02 (circle) | Fitted circle params within ±0.5 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ Wave 0 |
| MET2D-02 (ellipse) | Fitted ellipse params within ±1 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ Wave 0 |
| MET2D-02 (rect) | Fitted rect params within ±1 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ Wave 0 |
| MET2D-03 | Old recipe (no MetrologyModel) loads without exception | unit (Domain) | Tests.exe | ❌ Wave 0 |
| MET2D-03 | Recipe round-trip preserves MetrologyModelDef | unit (Domain) | Tests.exe | ❌ Wave 0 |
| MET2D-04 | 3-object model returns 3 results in one Apply call | integration (HALCON) | Tests.Halcon.exe | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `Tests.exe` (Domain-only tests, < 5s)
- **Per wave merge:** `Tests.Halcon.exe` (HALCON integration, < 30s)
- **Phase gate:** Full suite green before human GUI acceptance

### Wave 0 Gaps

- [ ] `tests/FlashMeasurementSystem.Tests/MetrologyModelDomainTests.cs` — covers MET2D-01, MET2D-03
- [ ] `tests/FlashMeasurementSystem.Tests.Halcon/MetrologyModelHalconTests.cs` — covers MET2D-02, MET2D-04
- [ ] Wire `MetrologyModelDomainTests.Run()` into `EdgeDetectionDomainTests.cs Main()` (existing entry point)
- [ ] Wire `MetrologyModelHalconTests.Run()` into Tests.Halcon `Main()` (or equivalent)
- [ ] Add `<Compile Include="...">` entries for all new files in each old-style .csproj

---

## Security Domain

No new external inputs, network access, or privilege escalation in this phase. All inputs are
operator-defined recipe parameters (nominal geometry values) and local image files. Standard
input-validation rules apply: validate MeasureLength1 < shape dimensions before passing to HALCON
to avoid unexpected exceptions. No ASVS categories newly applicable beyond existing project baseline.

---

## Environment Availability

| Dependency | Required By | Available | Notes |
|------------|------------|-----------|-------|
| HALCON 17.12 (`halcondotnet.dll`) | HalconMetrologyModelRunner | ✓ | All Ch.2 metrology operators confirmed in 17.12 reference |
| .NET Framework 4.8 | All layers | ✓ | Existing project platform |
| Newtonsoft.Json | RecipeStore serialization | ✓ | Already in project |
| x64 build | HALCON adapter | ✓ | Existing build target |

No missing dependencies.

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| Manual per-ROI coordinate transform (1D tools) | `align_metrology_model` with `reference_system` (2D tools) | Cleaner; single HALCON call aligns the entire model |
| Separate `fit_circle_contour_xld` / `fit_ellipse_contour_xld` after manual edge detection | `apply_metrology_model` (unified edge + RANSAC fit) | One call handles all 4 shape types; more robust to partial edge coverage |
| Custom measure-rectangle placement | `measure_distance` / `num_measures` param in add_* | No geometry math needed; HALCON handles arc tangents and distribution |

**No deprecated operators in scope.** All 14 operators used above are current in HALCON 17.12.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `set_metrology_model_param 'reference_system'` takes a 3-element tuple [Row, Col, AngleRad] | Integration Design | Wrong format → HalconException at run time; confirmed from solution guide code but not from the parameter list in the reference text (which was cut off at camera_param/plane_pose) |
| A2 | When `align_metrology_model` is called with absolute match coordinates and `reference_system` is set, the model correctly follows part movement | Integration Design | Wrong alignment model → all metrology objects displaced; testable on first synthetic image with known offset |

**Note on A1:** The solution guide (L508) shows `set_metrology_model_param(MetrologyHandle, 'reference_system', [RowModel, ColumnModel, 0])` which strongly confirms 3-element tuple. The risk is LOW. The planner should add a verification note to confirm this at the start of implementation.

---

## Open Questions

1. **How to expose the metrology model editor in the GUI?**
   - What we know: The existing RecipeEditor.cs has per-tool editing UI. Metrology objects are a different kind of tool (multiple nominal geometry fields).
   - What's unclear: Whether to add a sub-panel inside RecipeEditor, a separate modal editor, or a list-based editor for multiple objects.
   - Recommendation: Minimal approach — a dedicated `MetrologyModelEditorPanel` with a list of objects and a property panel, embedded in RecipeEditor. UI shape left to Claude's discretion per CONTEXT.md.

2. **OverlayAnnotator drawing for metrology results**
   - What we know: `get_metrology_object_result_contour` returns fitted shapes as XLD contours, drawable via existing HALCON display.
   - What's unclear: Whether to draw edge-point crosses (like existing 1D pipeline) or just the fitted contour.
   - Recommendation: Draw both: fitted contour (green) and measure-point crosses (cyan) to match the existing visual language.

3. **RecipeRunner constructor signature change**
   - What we know: Adding `IMetrologyModelRunner` parameter follows existing injection pattern.
   - What's unclear: Whether `MainWindow.cs` (which constructs RecipeRunner) needs to be updated to inject `HalconMetrologyModelRunner`.
   - Recommendation: Add `IMetrologyModelRunner` as a nullable constructor parameter with null default. When null, Pass 3 is silently skipped. This preserves existing construction sites.

---

## Sources

### Primary (HIGH confidence — verified against local HALCON 17.12 reference)
- `halcon_pdf/reference/reference_hdevelop.txt` Ch.2 — verbatim operator signatures at L4672, L4834, L5195, L5319, L5448, L5652, L5744, L5868, L6150, L6227, L6495, L6635, L6889, L6962, L7214
- `halcon_pdf/reference/halcon_operator_index.md` — operator index Ch.2 (27 ops, L4511-7451)
- `halcon_pdf/reference/halcon_chapter_intros.md` — Ch.2 2D Metrology concept (L113-240)
- `halcon_pdf/solution_guide/output/solution_guide_iii_b_2d_measuring/solution_guide_iii_b_2d_measuring.md` — section 2.3 workflow + alignment patterns (L413-532)

### Codebase (verified by direct read)
- `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs` — 1D pipeline structure; Pass 1/1.5/1.7/2 pattern; ToolRunResult fields
- `src/FlashMeasurementSystem.Domain/Roi/Recipe.cs` — SchemaVersion=5; MetrologyModel field placement
- `src/FlashMeasurementSystem.Domain/Roi/MeasurementTool.cs` — existing tool type conventions
- `src/FlashMeasurementSystem.Infrastructure/Roi/RecipeStore.cs` — JSON serialization strategy
- `src/FlashMeasurementSystem.Halcon/CircleFitting/HalconCircleFitter.cs` — feature adapter skeleton
- `src/FlashMeasurementSystem.Domain/EllipseFitting/EllipseFittingResult.cs` — Phi, Radius1/2 field names
- `src/FlashMeasurementSystem.Domain/RectangleFitting/RectangleFittingResult.cs` — Phi, Length1/2 field names

---

## Metadata

**Confidence breakdown:**
- HALCON operator signatures: HIGH — read verbatim from local reference_hdevelop.txt
- Auto-distribution behaviour: HIGH — documented in set_metrology_object_param reference
- Integration design: HIGH — feature adapter pattern is well-established in codebase
- Alignment strategy (reference_system + align): MEDIUM (A1) — confirmed from solution guide code; one detail (parameter format) should be verified in Wave 0 implementation
- Pixel-space tolerance bands: MEDIUM — based on typical sub-pixel accuracy for clean synthetic images; adjust if synthetic images have aliasing

**Research date:** 2026-06-30
**Valid until:** 2026-09-30 (HALCON 17.12 is a pinned version; no API churn expected)


<!-- ===================================================== -->
# 原始檔：.planning/phases/02-2d-metrology-model/02-VALIDATION.md
<!-- ===================================================== -->

---
phase: 2
slug: 2d-metrology-model
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-30
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from 02-RESEARCH.md "## Validation Architecture". Verification is in
> PIXEL space on synthetic images with known ground-truth (no mm/calibration).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Console Exe with Assert helper (same as existing suites) |
| **Config file** | none — old-style console Exe |
| **Quick run command** | `.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe` |
| **Full suite command** | quick + `.\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe` |
| **Estimated runtime** | ~5s (Domain) / ~30s (incl. HALCON integration) |

---

## Sampling Rate

- **After every task commit:** Run quick command (Domain tests, <5s)
- **After every plan wave:** Run full suite (incl. HALCON integration, <30s)
- **Before `/gsd-verify-work`:** Full suite green + GUI human acceptance
- **Max feedback latency:** ~30 seconds

---

## Per-Task Verification Map

> Exact task IDs (02-0X-YY) are assigned by the planner; mapped here by requirement.

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| MET2D-01 | Measure-region count meets minimum after add | unit (Domain) | Tests.exe | ❌ W0 | ⬜ pending |
| MET2D-01 | Measure regions present before apply | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |
| MET2D-02 line | Fitted line params within ±0.5 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |
| MET2D-02 circle | Fitted circle params within ±0.5 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |
| MET2D-02 ellipse | Fitted ellipse params within ±1 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |
| MET2D-02 rect | Fitted rect params within ±1 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |
| MET2D-03 | Old recipe (no MetrologyModel) loads without exception | unit (Domain) | Tests.exe | ❌ W0 | ⬜ pending |
| MET2D-03 | Recipe round-trip preserves metrology model def | unit (Domain) | Tests.exe | ❌ W0 | ⬜ pending |
| MET2D-04 | 3-object model returns 3 results in one Apply | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/FlashMeasurementSystem.Tests/MetrologyModelDomainTests.cs` — stubs for MET2D-01, MET2D-03 (Domain: region-count, recipe round-trip + backward-compat)
- [ ] `tests/FlashMeasurementSystem.Tests.Halcon/MetrologyModelHalconTests.cs` — stubs for MET2D-02, MET2D-04 (synthetic-image fit within tolerance bands; multi-object one-pass)
- [ ] Wire `MetrologyModelDomainTests.Run()` into the Tests `Main()` (EdgeDetectionDomainTests.cs)
- [ ] Wire `MetrologyModelHalconTests.Run()` into the Tests.Halcon `Main()`
- [ ] Add `<Compile Include="...">` entries for all new files in each old-style .csproj

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Measure-rectangle layout looks right on the image; one-click measures all features; overlays draw correctly and coexist with 1D results | MET2D-01, MET2D-04 | HALCON display + operator visual judgment (project convention: HALCON GUI checked manually) | Launch app x64, load synthetic image, build metrology model, apply, observe fits + measure points drawn |

*Synthetic test images + a ground-truth answer sheet will be generated at phase end for this manual functional testing.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

<!-- ===================================================== -->
# 附錄：Phase 2 執行結果彙整（由 .planning/phases/02-*/*-SUMMARY.md 濃縮，2026-07-01 移除 GSD 前保存）
<!-- ===================================================== -->

> 上面各節是「計畫/研究/驗證」；本附錄記錄 **實際寫進程式的內容與 commit**。狀態：waves 1–4 全用 Claude 主迴圈實作完成，每 wave `dotnet build` x64 + Any CPU 皆 0/0、測試綠、逐 wave commit。**GUI 人工驗收（02-04 Task 3）進行中**，尚未 sign-off。逐日細節見 `docs/每日進度/2026-07-01_工作彙整_Phase1收尾與Phase2.md`。

## Wave 1 — Domain + Application 契約 + 加性 Recipe v6（commit e983ef1，MET2D-01/03）
- 5 個 Domain DTO（`FlashMeasurementSystem.Domain.MetrologyModel`，純資料）：`MetrologyObjectType`(enum)、`MetrologyObjectDef`（標稱幾何 + 量測參數 + `MinMeasureRegions` 2/3/5/8）、`MetrologyModelDef`、`MetrologyObjectResult`、`MetrologyModelResult`。
- Application `IMetrologyModelRunner<TImage>`（generic over image type，Application 維持 HALCON-free，鏡射 `IEdgeDetector<TImage>`）。
- **Recipe v6**：加性 nullable `MetrologyModel` 欄（預設 null）、`SchemaVersion` 5→6；無 migration，舊 `.zcp` 反序列化為 null、行為不變。
- 連帶修：`RoiDomainTests.cs` 兩處硬編 `SchemaVersion == 5` → 6（v5→v6 bump 正確打破，僅修本次造成的破壞）。

## Wave 2 — HALCON 適配器 + 合成圖擬合測試（commit 8dd8154，MET2D-02/04）
- `HalconMetrologyModelRunner`：每次 `Apply()` 現建 handle（create → add objects → align → apply → query → clear，try/finally），recipe 只存可序列化 DTO，不存 HALCON handle。
- `apply_metrology_model` 需**單通道影像**（同 `measure_pos` 陷阱，先 `rgb1_to_gray`）。
- 開放問題 A1 確認：`set_metrology_model_param 'reference_system'` 格式 = `[row, column, angle]`。

## Wave 3 — RecipeRunner Pass 3 + 注入 + overlay（commit 4cdbbbc，MET2D-03/04）
- RecipeRunner 新增 **Pass 3**：`recipe.MetrologyModel != null` 時，在既有 1D passes 之後執行 nullable runner；1D 行為完全不變（共存，非取代）。
- `ToolRunResult` 擴充：ellipse 用 `FitPhi/FitRadius1/FitRadius2`、rectangle 用 `FitPhi/FitLength1/FitLength2`。
- MainWindow 注入 + on-image overlay（cyan 量測點 + 擬合輪廓）。

## Wave 4 — 編輯器 + 合成圖 + 答案表（commit 8044100，MET2D-01/02/04）
- `MetrologyModelEditorForm`（純程式碼建構、無 Designer，最低風險/易 rollback）：物件清單 + Add/Remove、shape combo 閘門對應可用的標稱幾何欄、量測參數 numerics（預填 DTO 預設）、per-object Name、MeasureLength1 非阻斷警告。Save 提交至 `recipe.MetrologyModel`（+ ImageWidth/Height hint）→ 直接流入 Pass 3。**不做量測矩形佈點數學**，只設 MeasureDistance/NumMeasures，交 HALCON auto-distribute。
- MainWindow「Metrology Model」按鈕 + `OpenMetrologyModelEditor`，有路徑時 `_recipeStore.Save`。
- `SyntheticMetrologyImageGenerator`（Tests.Halcon）：寫 5 張 PNG 到 `data/images`（line/circle/ellipse/rectangle/composite）＋ `SYNTHETIC_METROLOGY_GROUNDTRUTH.md` 答案表。註：`data/images` 為 gitignore，PNG 跑 `Tests.Halcon.exe` 重生。

## GUI 驗收期間的 bug 修正（commits）
- **6ab879e**：1D「Edit Recipe」存檔會洗掉 MetrologyModel → `CopyRecipeMetadata` 補帶。
- **f879689**：純量測模型配方被參考姿態守門擋住 + 失敗物件結果表空白 → 守門只在有 1D 工具時擋、補 `ValueText`。
- **5ce575b（改壞）→ 6713fcc（正解）**：擅自把量測模型改「一律絕對座標」改壞對齊與一鍵量測；正解＝對齊改用與 1D 相同的剛體變換（`CreateFromMatch → TransformRoi` 預轉標稱幾何再以絕對座標套用），取代不穩的 HALCON `reference_system`/`align_metrology_model`。
- **906e003**：編輯器 Name 欄跳焦點（TextChanged 只寫值、Leave 才刷清單）。

## ⚠️ 未解 landmine 與待辦（交接）
- **`MainWindow.Designer.cs` 有未提交的 VS WinForms Designer 重新生成（~444 行，控制項重排）**，已確認掉了 `emptyStateGuideLabel.BringToFront()`（HEAD 有、工作區無）→ N3 空狀態引導恐被 HALCON 控制項蓋住。建議 `git checkout HEAD -- MainWindow.Designer.cs` 還原已提交良好版再 rebuild（待使用者決定，勿擅自還原）。
- 對齊修正（6713fcc）需在**旋轉工件**的 GUI 場景複驗；**補對齊路徑自動測試**（Wave 2 測試全 `hasReferencePose=false`，從未測對齊＝驗證洞，正是害這次的根因）。
- 通過後：STATE → phase_complete、ROADMAP Phase 2 打勾。
- 教訓：**GSD 綠燈 ≠ 驗證到位；未提交的 Designer 亂改是地雷；風險/視覺路徑動手前先自造情境驗證。**
