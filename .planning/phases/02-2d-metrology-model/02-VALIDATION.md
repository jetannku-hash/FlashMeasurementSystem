---
phase: 2
slug: 2d-metrology-model
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-30
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from 02-RESEARCH.md "## Validation Architecture". Verification is in
> PIXEL space on synthetic images with known ground-truth (no mm/calibration).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Console Exe with Assert helper (same as existing suites) |
| **Config file** | none — old-style console Exe |
| **Quick run command** | `.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe` |
| **Full suite command** | quick + `.\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe` |
| **Estimated runtime** | ~5s (Domain) / ~30s (incl. HALCON integration) |

---

## Sampling Rate

- **After every task commit:** Run quick command (Domain tests, <5s)
- **After every plan wave:** Run full suite (incl. HALCON integration, <30s)
- **Before `/gsd-verify-work`:** Full suite green + GUI human acceptance
- **Max feedback latency:** ~30 seconds

---

## Per-Task Verification Map

> Exact task IDs (02-0X-YY) are assigned by the planner; mapped here by requirement.

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| MET2D-01 | Measure-region count meets minimum after add | unit (Domain) | Tests.exe | ❌ W0 | ⬜ pending |
| MET2D-01 | Measure regions present before apply | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |
| MET2D-02 line | Fitted line params within ±0.5 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |
| MET2D-02 circle | Fitted circle params within ±0.5 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |
| MET2D-02 ellipse | Fitted ellipse params within ±1 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |
| MET2D-02 rect | Fitted rect params within ±1 px on synthetic | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |
| MET2D-03 | Old recipe (no MetrologyModel) loads without exception | unit (Domain) | Tests.exe | ❌ W0 | ⬜ pending |
| MET2D-03 | Recipe round-trip preserves metrology model def | unit (Domain) | Tests.exe | ❌ W0 | ⬜ pending |
| MET2D-04 | 3-object model returns 3 results in one Apply | integration (HALCON) | Tests.Halcon.exe | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/FlashMeasurementSystem.Tests/MetrologyModelDomainTests.cs` — stubs for MET2D-01, MET2D-03 (Domain: region-count, recipe round-trip + backward-compat)
- [ ] `tests/FlashMeasurementSystem.Tests.Halcon/MetrologyModelHalconTests.cs` — stubs for MET2D-02, MET2D-04 (synthetic-image fit within tolerance bands; multi-object one-pass)
- [ ] Wire `MetrologyModelDomainTests.Run()` into the Tests `Main()` (EdgeDetectionDomainTests.cs)
- [ ] Wire `MetrologyModelHalconTests.Run()` into the Tests.Halcon `Main()`
- [ ] Add `<Compile Include="...">` entries for all new files in each old-style .csproj

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Measure-rectangle layout looks right on the image; one-click measures all features; overlays draw correctly and coexist with 1D results | MET2D-01, MET2D-04 | HALCON display + operator visual judgment (project convention: HALCON GUI checked manually) | Launch app x64, load synthetic image, build metrology model, apply, observe fits + measure points drawn |

*Synthetic test images + a ground-truth answer sheet will be generated at phase end for this manual functional testing.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
