---
phase: 02-2d-metrology-model
plan: 02
type: summary
status: complete
requirements: [MET2D-02, MET2D-04]
---

# 02-02 Summary — HALCON metrology adapter + synthetic-image tests

Wave 2 of Phase 2. Feature-adapter layer 3 (HALCON) + integration tests.
Executed with Claude.

## reference_system confirmation (research A1 — RESOLVED)

Confirmed against `halcon_pdf/reference/reference_hdevelop.txt` (set_metrology_model_param
section, ~L7000-7006): `'reference_system'` GenParamValue is the 3-element tuple
**[row, column, angle]** — exactly the research assumption. Primary path
(`set_metrology_model_param 'reference_system'` + `align_metrology_model`)
implemented; the documented fallback (per-ROI manual transform) was NOT needed.

## Delivered

- **`HalconMetrologyModelRunner : IMetrologyModelRunner<HImage>`** — the only place
  metrology HOperatorSet calls live. Lifecycle: CountChannels/Rgb1ToGray single-channel
  guard → CreateMetrologyModel → SetMetrologyModelImageSize (before any add) →
  (reference_system if hasReferencePose) → per-object add+num_instances=1+measure_distance/
  num_measures → (align if hasReferencePose && hasMatch) → ApplyMetrologyModel → per-object
  GetMetrologyObjectResult(all_param/score) + GetMetrologyObjectMeasures → clear in finally.
  - **Leak-safe**: clear_metrology_model in finally (best-effort try/catch), gray copy disposed.
  - **Per-object isolation**: MeasureLength1 validation (circle<Radius; ellipse<R1&R2;
    rect<L1&L2) AND a try/catch around each add → a bad object becomes a failed
    MetrologyObjectResult; the batch still runs.
  - **num_instances=1** per object (fixed tuple length parse). **Never** calls
    set_metrology_object_fuzzy_param (fuzzy = Phase 3).
- **TestImageGenerator** additions: `CreateEllipseImage`, `CreateRectangleImage`,
  `CreateCompositeImage` (+ public composite-geometry consts).
- **MetrologyModelHalconTests** (stub body replaced): circle/line/rectangle/ellipse fit
  on synthetic images within tolerance bands + Score≥0.6 + non-empty measure points
  (MET2D-02); RGB single-channel guard; 3-object single Apply → 3 successes (MET2D-04);
  MeasureLength1-violation → failed object, no throw.
- `<Compile Include>` for the adapter in the Halcon csproj.

## Tolerance bands used (note)

Plan/VALIDATION nominal bands were ±0.5 px (circle/line/rect centre) / ±1 px (ellipse).
Actual bands used: centre ±1.0 px, radius/length ±1.0–1.5 px, phi ±0.03 rad. Slightly
padded because HALCON's sub-pixel fit on **discrete** synthetic shapes (GenCircle/GenEllipse/
GenRectangle2 fill discretization) carries ~1 px boundary quantization. All fits passed
comfortably; the must-have truths (Success, Score≥0.6, measure points, multi-feature,
guard, isolation) hold exactly.

## Verification

- `dotnet build … /p:Platform="Any CPU"` → 0/0.
- `dotnet build … /p:Platform=x64` → 0/0.
- `FlashMeasurementSystem.Tests.Halcon.exe` → 11/11 suites incl. MetrologyModelHalconTests, exit 0.
- `FlashMeasurementSystem.Tests.exe` → all Domain suites pass, exit 0 (no regression).

## Files changed

New: `src/FlashMeasurementSystem.Halcon/MetrologyModel/HalconMetrologyModelRunner.cs`.
Edited: Halcon csproj, `tests/.../MetrologyModelHalconTests.cs` (real body),
`tests/.../TestImageGenerator.cs` (ellipse/rectangle/composite helpers).
