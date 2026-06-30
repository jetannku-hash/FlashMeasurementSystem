---
phase: 02-2d-metrology-model
plan: 01
type: summary
status: complete
requirements: [MET2D-01, MET2D-03]
---

# 02-01 Summary — Domain + Application contracts + additive Recipe v6

Wave 1 of Phase 2. Established the data shapes and coexistence contract before
any HALCON code. Executed with Claude.

## Delivered

- **5 Domain DTOs** (`FlashMeasurementSystem.Domain.MetrologyModel`, pure data,
  no HALCON/UI/IO): `MetrologyObjectType` (enum), `MetrologyObjectDef`
  (nominal geometry + measure params + static `MinMeasureRegions` 2/3/5/8),
  `MetrologyModelDef`, `MetrologyObjectResult`, `MetrologyModelResult`.
- **Application interface** `IMetrologyModelRunner<TImage>` — generic over the
  image type (mirrors `IEdgeDetector<TImage>`) so Application stays HALCON-free.
- **Recipe v6**: additive nullable `MetrologyModel` field (default null);
  `SchemaVersion` default 5→6. No migration code; old `.zcp` deserialize with
  `MetrologyModel == null`, behaviour unchanged.
- **Tests**: `MetrologyModelDomainTests` (DTO defaults; MinMeasureRegions
  2/3/5/8 for MET2D-01; Recipe default null + SchemaVersion 6; a real
  RecipeStore backward-compat load of a v5 JSON with no MetrologyModel field;
  a Save/Load round-trip preserving object count + nominal/measure fields for
  MET2D-03) wired into Tests `Main()`. Compiling `MetrologyModelHalconTests`
  stub wired into Tests.Halcon `Program.cs` (filled in 02-02).
- All new files registered with explicit `<Compile Include>` in their old-style
  csprojs (Domain, Application, Tests, Tests.Halcon).

## Deviation from plan (necessary)

The plan's files_modified did not list `RoiDomainTests.cs`, but two pre-existing
assertions there hard-coded `Recipe.Default().SchemaVersion == 5` (line 31) and
the round-trip `SchemaVersion == 5` (line 96). The mandated v5→v6 bump correctly
broke them; both were updated to 6 (fixing breakage this change caused). No
behavioural change beyond the version number.

## Verification

- `dotnet build … /p:Platform="Any CPU"` → 0/0.
- `dotnet build … /p:Platform=x64` → 0/0.
- `FlashMeasurementSystem.Tests.exe` → all suites pass incl. MetrologyModelDomainTests, exit 0.
- `FlashMeasurementSystem.Tests.Halcon.exe` → 11/11 suites incl. the metrology stub, exit 0.

## Files changed

New: 5 Domain DTOs, `Application/MetrologyModel/IMetrologyModelRunner.cs`,
`tests/.../MetrologyModelDomainTests.cs`, `tests/.../MetrologyModelHalconTests.cs`.
Edited: `Domain/Roi/Recipe.cs`, Domain/Application/Tests/Tests.Halcon csprojs,
`tests/.../EdgeDetectionDomainTests.cs`, `tests/.../Program.cs`,
`tests/.../RoiDomainTests.cs` (schema-bump assertion fix).
