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

- [ ] **Phase 1: Operator Experience** - Empty-state guidance, PASS/FAIL banner, tolerance-limit display, in-editor trial measure
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

**Plans**: TBD
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
| 1. Operator Experience | 0/2 | Not started | - |
| 2. 2D Metrology Model | 0/TBD | Not started | - |
| 3. Production Robustness | 0/TBD | Not started | - |
| 4. PDF Reporting | 0/TBD | Not started | - |
| 5. Application Solutions Library | 0/TBD | Not started | - |
