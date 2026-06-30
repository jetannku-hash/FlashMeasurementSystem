## Conflict Detection Report

Mode: new (net-new bootstrap). Precedence: ADR > SPEC > PRD > DOC.
Ingest set: 17 docs (15 SPEC, 2 DOC, 0 ADR, 0 PRD). Cross-ref cycle check: PASS (no
cycles; max doc-to-doc depth 3 of 50). No UNKNOWN/low-confidence docs.

Because the set contains no ADRs and no PRDs, the two highest-risk conflict classes do
not apply: there are no LOCKED-vs-LOCKED ADR contradictions and no competing PRD
acceptance variants. All SPECs occupy one precedence tier; DOCs are below them.

### BLOCKERS (0)

(none)

### WARNINGS (0)

(none)

### INFO (5)

[INFO] Recipe schema version progression v4 → v5
  Found: A5 Geometry Construction (C-09/C-10, docs/superpowers/.../2026-06-26-a5-geometry-construction*.md) declares Recipe SchemaVersion v4; GD&T Tolerance v1 (C-12/C-13, docs/superpowers/.../2026-06-27-gdt-tolerance-v1*.md) declares schema v5.
  Note: Sequential evolution of the same schema, not a contradiction. The later/highest version (v5) is the authoritative recipe schema for downstream planning; v4 is a superseded waypoint.

[INFO] Manifest type coercion to SPEC on plan/roadmap-style docs
  Found: GUI建議優化項目計畫書.md (status 待審閱), ROADMAP_待辦與決策.md, and 應用落地量測方案庫_建議文件.md read as plans/proposals/roadmap by content heuristics but were typed SPEC.
  Note: MANIFEST_TYPE is authoritative and was honored. These are catalogued in constraints.md (C-11, C-14, C-15) as proposal/roadmap-class constraints, not hard technical contracts.

[INFO] ROADMAP contains an embedded decision log but no ADR-typed source
  Found: docs/ROADMAP_待辦與決策.md carries a "deferred-decision log" with records resembling ADR §5 entries; no document in the set is typed ADR.
  Note: Those records were NOT promoted to bindable/lockable decisions (decisions.md is empty). They are preserved as context (constraints.md C-15, context.md). To make any govern downstream as hard decisions, re-tag the source as ADR via --manifest and re-run.

[INFO] DOC manual is lowest precedence; defers to current SPECs on recipe/schema
  Found: 開發手冊 (DOC) describes recipe artifact as `.zcp`; current SPECs (C-10, C-13) define a versioned Recipe schema (v4→v5).
  Note: Per precedence ADR>SPEC>PRD>DOC, SPECs win over the manual where they diverge. Synthesized intel treats the manual as background/onboarding context, not as the authority on recipe/schema specifics.

[INFO] EdgeDetection DTOs evolve additively across SPECs
  Found: Rectangular Measure-Object Enhancement (C-07, 2026-06-12...) extends EdgeDetectionParameters / EdgeDetectionRoi / EdgeResult and adds EdgePair, atop the base DTOs defined by Edge Detection Test UI (C-03, 2026-06-04...).
  Note: Additive extension of an earlier design, not a contradiction. Downstream should treat C-07 as the current shape of the EdgeDetection DTOs.
