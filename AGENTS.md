# FlashMeasurementSystem Agent Rules

## Purpose
- This file is the project-level instruction source for AI-assisted development in this repository.
- The full development reference is `docs/本手冊/FlashMeasurementSystem_開發手冊.md`.
- Treat the manual as the source of detailed domain knowledge, and this file as the mandatory operating checklist.

## Required Pre-Work
- Before any non-trivial implementation, read the relevant sections of `docs/本手冊/FlashMeasurementSystem_開發手冊.md`.
- Also inspect the files directly affected by the task. Do not infer APIs, project structure, or Halcon behavior from memory.
- If the manual conflicts with existing code, report the conflict and choose the smallest safe change. Do not silently rewrite architecture.

## Architecture Rules
- Keep the solution layout aligned with the manual:
  - `src/FlashMeasurementSystem.App.Wpf`
  - `src/FlashMeasurementSystem.Domain`
  - `src/FlashMeasurementSystem.Application`
  - `src/FlashMeasurementSystem.Infrastructure`
  - `src/FlashMeasurementSystem.Halcon`
  - `src/FlashMeasurementSystem.Mes`
  - `src/FlashMeasurementSystem.Reporting`
  - `tests/FlashMeasurementSystem.Tests`
  - `data/images`, `data/recipes`, `data/calibrations`, `data/reports`
  - `docs/本手冊`
- Dependency direction must remain one-way:
  - `Domain` depends on no project in this solution.
  - `Application` may depend on `Domain`.
  - `Infrastructure`, `Halcon`, `Mes`, and `Reporting` may depend on `Application` and `Domain`.
  - `App.Wpf` composes the application and adapter projects.
  - `Tests` may reference `Domain` and `Application` unless a task explicitly requires integration testing another adapter.
- Do not place Halcon, MES, reporting, file-system, UI, or hardware-specific logic in `Domain`.
- `src/FlashMeasurementSystem.App.Wpf` is currently transitional. Preserve existing WinForms behavior unless the task explicitly authorizes WPF migration.

## Platform And Halcon Rules
- Target .NET Framework 4.8 unless the user explicitly approves a platform migration.
- Halcon work must account for 64-bit execution. Prefer x64 verification for Halcon-related changes.
- Keep Halcon-specific code behind `FlashMeasurementSystem.Halcon` or existing transitional app code until a dedicated extraction task moves it.
- Halcon exceptions must be handled intentionally at application/service boundaries; do not swallow exceptions silently.
- UI display operations involving Halcon controls must run on the UI thread.

## Domain Workflow Rules
- Preserve the measurement flow described in the manual unless explicitly changing it:
  `IDLE -> LOADING_PROGRAM -> WAITING_PART -> PREPARING -> ACQUIRING -> CHECKING_IMAGE -> MATCHING_TEMPLATE -> MEASURING -> EVALUATING -> REPORTING -> OUTPUTTING -> IDLE`.
- Recipes use `.zcp` files and belong under `data/recipes` for sample/replay data.
- Calibration data belongs under `data/calibrations`.
- Replay images belong under `data/images`.
- CSV/report outputs belong under `data/reports`.

## Implementation Discipline
- Make the smallest correct change for the requested task.
- Do not add speculative abstractions, sample implementations, or new dependencies just because the manual mentions future functionality.
- Keep feature work, bug fixes, refactors, and documentation changes separate unless the user explicitly asks for a combined task.
- Do not commit build outputs (`bin/`, `obj/`, `.vs/`) or machine-specific files.

## Verification
- Before claiming completion, run the narrowest relevant verification and then the solution build when applicable.
- Standard build command for this environment:
  `dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"`
- For Halcon/platform-sensitive changes, also run:
  `dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64`
- If verification cannot run, report the exact blocker and what was verified instead.

## Graphify Usage
- Use graphify as an optional architecture-navigation aid for broad dependency questions, large refactors, or manual-to-code relationship analysis.
- Do not require graphify for small localized bug fixes or single-file changes.
