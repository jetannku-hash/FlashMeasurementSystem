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
