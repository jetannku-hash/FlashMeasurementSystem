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

<!-- Shipped and confirmed (baseline; see docs/ROADMAP_待辦與決策.md §6). -->

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
  is the roadmap backbone; the single dashboard is `docs/ROADMAP_待辦與決策.md`.

## Constraints

- **Tech stack**: .NET Framework 4.8 / WinForms / HALCON 17.12 — fixed; do not migrate to WPF.
- **Architecture**: strict one-way layering, Domain HALCON-free — enforced by `AGENTS.md` checklist.
- **Hardware**: no camera / caltab / standard parts / Z-axis — blocks A1 full calibration, B4 autofocus, CMM benchmarking, full real-image adapter tests.
- **External dependency**: MES integration blocked on the customer's MES protocol spec.
- **Verification**: replay-image + synthetic-image verifiable only; no real metrological truth available.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Defer all A1 calibration (full + anisotropic half) until camera + standard parts arrive | No hardware = no truth to calibrate against; building now is idle motion | — Pending |
| GD&T position/symmetry/orientation + full datum frame → v2 | Needs complete datum reference frame + CMM benchmarking; highest error risk without standard parts | — Pending |
| Build measurement-solutions library only after primitives complete | Primitives are the building blocks; they are now done, so the library is unblocked | — Pending |
| Keep 1D + 2D metrology coexisting (not replacing) | Avoid breaking the existing verified pipeline | — Pending |

---
*Last updated: 2026-06-30 after ingest bootstrap (gsd-roadmapper)*
