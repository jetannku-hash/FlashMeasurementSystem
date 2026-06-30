# Synthesis Summary

Entry point for `gsd-roadmapper`. Produced by gsd-doc-synthesizer.
Mode: new (net-new bootstrap). Precedence: ADR > SPEC > PRD > DOC.
Project: .NET Framework 4.8 / WinForms / MVTec HALCON 17.12 machine-vision measurement system.

## Doc counts by type
- Total ingested: 17
- SPEC: 15
- DOC: 2
- ADR: 0
- PRD: 0
- UNKNOWN / low-confidence: 0

## Cross-ref graph
- Cycle detection: PASS (no cycles)
- Max doc-to-doc depth: 3 (limit 50)

## Decisions (ADRs)
- Locked decisions: 0
- Total decisions: 0 (no ADR-typed docs)
- See: decisions.md (notes the ROADMAP embedded decision log that was NOT promoted)

## Requirements (PRDs)
- Requirements extracted: 0 (no PRD-typed docs)
- Feature intent lives in SPEC constraints; see requirements.md note.
- See: requirements.md

## Constraints (SPECs) — 15 entries (C-01 … C-15)
- api-contract: C-01..C-07, C-09, C-10, C-12, C-13 (feature design specs)
- schema: C-01, C-02, C-07, C-10, C-12, C-13 (thresholds / DTOs / Recipe schema)
- nfr / ux: C-08 (capability gaps), C-14 (GUI optimization)
- protocol / proposal / roadmap: C-09, C-11, C-15
- Authoritative Recipe schema: v5 (C-13) supersedes v4 (C-09/C-10)
- Current EdgeDetection DTO shape: C-07 (extends C-03)
- See: constraints.md

## Context (DOCs) — 4 topics
- System purpose & domain; core measurement pipeline; architecture & layering rules;
  project conventions & data locations. Plus cross-cutting project-state context
  (hardware/external-spec blockers, completed milestones, open directions).
- See: context.md

## Conflicts
- BLOCKERS: 0
- WARNINGS (competing-variants): 0
- INFO (auto-resolved / transparency): 5
  1. Recipe schema v4 → v5 progression (v5 authoritative)
  2. Manifest type coercion to SPEC on plan/roadmap docs (honored)
  3. ROADMAP embedded decision log not promoted to ADRs
  4. DOC manual lowest precedence; defers to SPECs on recipe/schema
  5. EdgeDetection DTOs evolve additively (C-07 current)
- Full detail: ../INGEST-CONFLICTS.md

## Status
READY — no blockers, no competing variants. Safe to route to gsd-roadmapper.

## Intel files
- decisions.md
- requirements.md
- constraints.md
- context.md
- ../INGEST-CONFLICTS.md
