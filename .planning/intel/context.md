# Context (from DOCs)

Synthesized by gsd-doc-synthesizer. Mode: new. 2 DOC-typed documents.
DOC is the lowest precedence tier — where it diverges from current SPECs, the SPECs
win (see INFO-4). Notes are appended by topic with source attribution.

---

## Topic: System purpose & domain
- source: docs/本手冊/FlashMeasurementSystem_開發手冊.md
- 一鍵式閃測儀 (one-click flash optical measurement instrument) developer reference
  manual. Builds a C# UI app with HALCON 17.12 machine vision. Covers measurement
  principles, environment setup, architecture, and per-feature implementation with
  code examples. Prose-heavy reference; no decision/requirement contract.

## Topic: Core measurement pipeline (manual)
- source: docs/本手冊/FlashMeasurementSystem_開發手冊.md
- Subpixel edge detection; calibration (µm/px); template matching / shape model; ROI +
  geometry fitting; tolerance evaluation (OK/NG); measurement state machine.
- Data-model artifacts named in the manual: recipe `.zcp`, template `.shm`,
  `appsettings.json`. (NOTE: current SPECs evolve the recipe model to a versioned
  schema — v4 in C-10, v5 in C-13. Manual's `.zcp` description is older context; defer
  to SPECs on recipe/schema specifics — INFO-1, INFO-4.)
- Includes a debugging and tuning guide.

## Topic: Architecture & layering rules
- source: docs/本手冊/開發檢查清單.md
- Pre-/post-implementation developer checklist enforcing layered architecture:
  Domain, Application, Infrastructure, Halcon adapter, Mes, Reporting, App.Wpf (WinForms).
  Build target: .NET Framework 4.8 / x64. Domain layer must remain HALCON-free.

## Topic: Project conventions & data locations
- source: docs/本手冊/開發檢查清單.md
- Measurement state machine present. Canonical data folders: images, recipes,
  calibrations, reports. Mandates tests + verification and a completion-reporting step.
  Cross-references the main 開發手冊 and `AGENTS.md`.

---

## Cross-cutting context (sourced from SPEC-typed roadmap/proposal docs)

These items are typed SPEC (manifest-authoritative) but carry project-state context
relevant to downstream planning. Captured here for the roadmapper's situational view;
authoritative detail lives in `constraints.md`.

- Blocked work (hardware): camera, calibration target/standard part, autofocus — cannot
  be fully verified without hardware; only fully software-verifiable algorithm items are
  actionable now. (source: docs/ROADMAP_待辦與決策.md)
- Blocked work (external spec): MES integration awaits an external interface spec.
  (source: docs/ROADMAP_待辦與決策.md)
- Completed milestones per roadmap: M0–M4, A3 (arc caliper), A5 (geometry construction),
  GD&T v1, N1 (recipe validation). (source: docs/ROADMAP_待辦與決策.md)
- Open direction: 2D metrology model (A2) and the Measurement Solutions library remain
  proposed/not-yet-built. (sources: ROADMAP; 應用落地量測方案庫 proposal)
