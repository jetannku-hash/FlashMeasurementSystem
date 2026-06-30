# Constraints (from SPECs)

Synthesized by gsd-doc-synthesizer. Mode: new. 15 SPEC-typed documents.
All SPECs share the same precedence tier (above DOC, below ADR/PRD — none present).
Each entry is traceable via `source:`. Constraint `type` ∈ {api-contract, schema, nfr, protocol}.

Platform baseline (asserted across the SPEC set): .NET Framework 4.8, x64, WinForms
UI (namespace suffix `.App.Wpf` is historical — the UI is WinForms), MVTec HALCON
17.12. Strict layering: Domain → Application (interfaces) → Halcon (adapters) →
App.Wpf (UI). Domain holds no HALCON dependency.

---

## C-01 Image Quality Check
- source: docs/superpowers/specs/2026-06-03-image-quality-check-design.md
- type: api-contract + schema
- Implements the CHECKING_IMAGE stage. Layered design: Domain `ImageQualityResult`,
  `ImageQualityThresholds`; Application `IImageQualityChecker`; Halcon
  `HalconImageQualityChecker`; WinForms test UI in MainWindow.
- Constraint: thresholds schema + error-handling contract defined; scans `data/images`.

## C-02 Template Matching
- source: docs/superpowers/specs/2026-06-03-template-matching-design.md
- type: api-contract + schema
- HALCON shape-model template creation + matching (manual §4.2). Domain DTOs:
  `TemplateMatchResult`, `TemplateCreationParameters`, `TemplateMatchingParameters`;
  Application `ITemplateManager`, `ITemplateMatcher`; Halcon `HalconTemplateManager`,
  `HalconTemplateMatcher`. Persisted artifact: `.shm` template files.

## C-03 Edge Detection (Test UI)
- source: docs/superpowers/specs/2026-06-04-edge-detection-test-ui-design.md
- type: api-contract
- Manual §4.3 — `measure_pos` + `edges_sub_pix`. Domain: `EdgeResult`, `EdgePoint`,
  `EdgeDetectionParameters`, `EdgeDetectionRoi`; Application `IEdgeDetector`; Halcon
  `HalconEdgeDetector`. HWindowControl ROI interaction + edge-profile chart in UI.
- NOTE: DTOs here are the base later extended by C-07 (additive — see INFO-5).

## C-04 Line Fitting
- source: docs/superpowers/specs/2026-06-05-line-fitting-design.md
- type: api-contract
- Adapts HALCON `fit_line_contour_xld`. Domain `LineFittingParameters`/`LineFittingResult`;
  Application `ILineFitter`; Halcon `HalconLineFitter`; consumed on Edge Detection tab,
  rendered via `OverlayAnnotator`. Covered by `LineFittingDomainTests`.

## C-05 Circle Fitting
- source: docs/superpowers/specs/2026-06-09-circle-fitting-design.md
- type: api-contract
- HALCON `fit_circle_contour_xld` from edge points. Domain `CircleFittingParameters`/
  `CircleFittingResult`; Application `ICircleFitter`; Halcon `HalconCircleFitter`;
  OverlayAnnotator. Covered by `CircleFittingDomainTests`.

## C-06 Angle Measurement
- source: docs/superpowers/specs/2026-06-10-angle-measurement-design.md
- type: api-contract
- HALCON `angle_ll` / `angle_lx`. Domain `AngleMeasurementParameters`/
  `AngleMeasurementResult`; Application `IAngleMeasurer`; Halcon `HalconAngleMeasurer`;
  Measurement tab + overlay. References HALCON 17.12 operator docs.

## C-07 Rectangular Measure-Object Enhancement
- source: docs/superpowers/specs/2026-06-12-rectangular-measure-object-enhancement-design.md
- type: api-contract + schema
- Strengthens HALCON 1D measure workflow: `gen_measure_rectangle2`, `measure_pos`,
  `measure_pairs`, interpolation, numeric ROI controls + live preview. Adds `EdgePair`
  DTO; extends `EdgeDetectionParameters`/`EdgeDetectionRoi`/`EdgeResult` from C-03.
- Relationship: additive extension of C-03 (not a contradiction — INFO-5).

## C-08 Capability Gap List (current → mainstream metrology)
- source: docs/superpowers/plans/2026-06-25_現況到主流量測儀_能力差距清單.md
- type: nfr (capability/roadmap)
- Gap analysis mapping current features to mainstream metrology with per-gap HALCON
  operators + effort/risk: camera calibration / lens distortion / world-plane metrology;
  2D metrology model; geometric primitives (ellipse/arc/rectangle/point); GD&T;
  geometric construction; fuzzy robust edge; GR&R repeatability; PDF report / MES;
  autofocus (Z). This is the upstream driver referenced by C-09..C-13.

## C-09 A5 Geometry Construction (Implementation Plan)
- source: docs/superpowers/plans/2026-06-26-a5-geometry-construction.md
- type: protocol + api-contract
- Plan for line-intersection, symmetric midline, point-to-line projection from fitted
  lines/circles, consumable by distance/angle tools. Introduces `GeometricPrimitive`,
  `GeometryConstruction`, `ToolRunResult.OutputPrimitive`; Recipe SchemaVersion v4.

## C-10 A5 Geometry Construction (Design Spec)
- source: docs/superpowers/specs/2026-06-26-a5-geometry-construction-design.md
- type: api-contract + schema
- Composes fitted line/circle elements into new geometry (intersection / midline /
  bisector / projection). Adds `GeometricPrimitiveResolver`, extends `MeasurementTool`/
  `ToolRunResult`, `RecipeRunner`, `RecipeEditor`. Recipe SchemaVersion = v4.

## C-11 Measurement Solutions Library (Proposal)
- source: docs/superpowers/plans/2026-06-26_應用落地量測方案庫_建議文件.md
- type: protocol (proposal)
- Library of named `MeasurementSolution`s layered on existing primitives; each defines
  detection target, primitives, outputs, judgment, output format. Targets: gear tooth
  count/pitch, PCD/hole arrays, shaft/bore diameter, pin pitch, GD&T / contour compare,
  template-align + batch measure, pixel-to-mm calibration. Status: proposal (not built).

## C-12 GD&T Form Tolerance v1 (Implementation Plan)
- source: docs/superpowers/plans/2026-06-27-gdt-tolerance-v1.md
- type: api-contract + schema
- 10-task plan for roundness, straightness, parallelism, perpendicularity,
  concentricity. Domain `GdtCharacteristic`, `GdtToleranceSpec`, `GdtCalculator`,
  `GdtEvaluation`; RecipeRunner + overlay + RecipeEditor; Recipe schema v5; CSV report.
  Test images via `scripts/gen_gdt_test_images.py`.

## C-13 GD&T Form Tolerance v1 (Design Spec)
- source: docs/superpowers/specs/2026-06-27-gdt-tolerance-v1-design.md
- type: api-contract + schema
- Design for the 5 GD&T characteristics: data model, deviation math, RecipeRunner
  integration, UI, tests. Recipe schema v5 (supersedes C-09/C-10 v4 — see INFO-1).

## C-14 GUI Optimization Plan
- source: docs/superpowers/plans/GUI建議優化項目計畫書.md
- type: nfr / ux (proposal, status 待審閱)
- 12 prioritized WinForms GUI/UX improvements with options + affected files +
  verification: recipe validation (N1, done), empty-state (N3), PASS/FAIL banner (N2),
  tolerance-limit display (N5), in-editor trial measure (A1), hotkeys + drag-drop (N6),
  live edge preview (A3), Undo/Redo (A5), recent recipes/images (N7), tool TreeView (A4),
  edge-param tooltips (A6), SplitContainer layout (A7). Touches `RecipeValidator`,
  `RecipeIssue`, MainWindow, RecipeEditor.
- NOTE: content is plan-like; typed SPEC by authoritative manifest (INFO-2).

## C-15 ROADMAP — Backlog, Decisions & Worklog (single dashboard)
- source: docs/ROADMAP_待辦與決策.md
- type: protocol (roadmap + deferred-decision log)
- Single dashboard of backlog/next-steps; hardware-blocked items (camera, calibration,
  autofocus); external-spec-blocked items (MES integration); deferred-decision log;
  completed milestones (M0–M4, A3, A5, GD&T v1, N1); 2D metrology model (A2); GUI backlog;
  solutions library. Embedded decision records present but NOT promoted to ADR-level
  decisions (INFO-3). Typed SPEC by authoritative manifest (INFO-2).
