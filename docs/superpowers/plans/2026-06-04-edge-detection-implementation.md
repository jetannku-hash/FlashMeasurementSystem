# Edge Detection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement manual §4.3 Edge Detection as a layered, manually testable Single-ROI Quick Test feature.

**Architecture:** Add pure Domain models, a generic Application interface, a Halcon adapter implementing `MeasurePos` and `EdgesSubPix`, and WinForms UI wiring in the existing transitional `MainWindow`. Keep Halcon-specific types out of Domain and Application.

**Tech Stack:** C# 7.3, .NET Framework 4.8, classic `.csproj`, WinForms, HALCON 17.12 `halcondotnet`.

---

## Baseline

- [x] Read `AGENTS.md`.
- [x] Read manual §4.3 Edge Detection.
- [x] Read current Edge Detection design spec.
- [x] Inspect existing ImageQuality / TemplateMatching patterns.
- [x] Run baseline Any CPU build.

Baseline command:

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: success, 0 warnings, 0 errors.

---

## File Structure

**Create:**

- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgePoint.cs`
- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeResult.cs`
- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionParameters.cs`
- `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionRoi.cs`
- `src/FlashMeasurementSystem.Application/EdgeDetection/IEdgeDetector.cs`
- `src/FlashMeasurementSystem.Halcon/EdgeDetection/HalconEdgeDetector.cs`
- `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`

**Modify:**

- `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`
- `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`
- `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`

Do not commit automatically; user did not request commits.

---

### Task 1: Domain Edge Detection Models

**Files:**
- Create: `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`
- Create: `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgePoint.cs`
- Create: `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeResult.cs`
- Create: `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionParameters.cs`
- Create: `src/FlashMeasurementSystem.Domain/EdgeDetection/EdgeDetectionRoi.cs`
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`

- [ ] **Step 1: Write failing tests**

Create a simple executable test harness because the current test project has no test framework references. Add `EdgeDetectionDomainTests.cs` with assertions for default parameters and ROI definition.

```csharp
using System;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Tests
{
    public static class EdgeDetectionDomainTests
    {
        public static int Main()
        {
            EdgeDetectionParameters defaults = EdgeDetectionParameters.Default();
            AssertEqual("measure_pos", defaults.Algorithm, "Default algorithm");
            AssertEqual(1.2, defaults.Sigma, "Default sigma");
            AssertEqual(25.0, defaults.Threshold, "Default threshold");
            AssertEqual("all", defaults.Polarity, "Default polarity");
            AssertEqual("all", defaults.EdgeSelector, "Default edge selector");
            AssertEqual("parabolic", defaults.SubpixelMethod, "Default subpixel method");
            AssertEqual(40.0, defaults.HighThreshold, "Default high threshold");
            AssertEqual(500.0, defaults.ScanLength, "Default scan length");
            AssertEqual(100.0, defaults.RoiWidth, "Default ROI width");

            var undefinedRoi = new EdgeDetectionRoi();
            AssertEqual(false, undefinedRoi.IsDefined, "Default ROI should be undefined");

            var roi = new EdgeDetectionRoi
            {
                CenterRow = 100.0,
                CenterCol = 200.0,
                Length1 = 250.0,
                Length2 = 50.0,
                AngleRad = 0.25
            };
            AssertEqual(true, roi.IsDefined, "ROI with positive lengths should be defined");

            var result = new EdgeResult();
            AssertEqual(false, result.Success, "New result should not default to success");
            AssertEqual(0, result.EdgePoints.Count, "New result should have empty edge list");
            AssertEqual(string.Empty, result.ErrorMessage, "New result should have empty error message");

            Console.WriteLine("EdgeDetectionDomainTests passed");
            return 0;
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify RED**

Run:

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:Configuration=Debug /p:Platform="AnyCPU"
```

Expected: FAIL because namespace `FlashMeasurementSystem.Domain.EdgeDetection` does not exist.

- [ ] **Step 3: Implement minimal Domain models**

Create models with C# 7.3-compatible syntax and explicit `.csproj` compile entries.

- [ ] **Step 4: Run test to verify GREEN**

Run:

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:Configuration=Debug /p:Platform="AnyCPU"
.\tests\FlashMeasurementSystem.Tests\bin\Debug\FlashMeasurementSystem.Tests.exe
```

Expected: build succeeds and executable prints `EdgeDetectionDomainTests passed`.

---

### Task 2: Application Interface

**Files:**
- Create: `src/FlashMeasurementSystem.Application/EdgeDetection/IEdgeDetector.cs`
- Modify: `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`

- [ ] **Step 1: Write failing compile usage**

Extend `EdgeDetectionDomainTests.cs` with a compile-time fake implementing `IEdgeDetector<object>`.

- [ ] **Step 2: Run test to verify RED**

Expected: FAIL because `IEdgeDetector<TImage>` does not exist.

- [ ] **Step 3: Implement interface**

```csharp
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Application.EdgeDetection
{
    public interface IEdgeDetector<TImage>
    {
        EdgeResult DetectEdges(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters);
        EdgeResult DetectEdgesSubPix(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters);
    }
}
```

- [ ] **Step 4: Run test to verify GREEN**

Run test project build and executable again.

---

### Task 3: Halcon Edge Detector

**Files:**
- Create: `src/FlashMeasurementSystem.Halcon/EdgeDetection/HalconEdgeDetector.cs`
- Modify: `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`

- [ ] **Step 1: Write failing integration compile target**

Add the Halcon class to `.csproj` before implementation is present so the Halcon project build fails on missing file.

- [ ] **Step 2: Run build to verify RED**

Run:

```powershell
dotnet build .\src\FlashMeasurementSystem.Halcon\FlashMeasurementSystem.Halcon.csproj /p:Configuration=Debug /p:Platform=x64
```

Expected: FAIL because `EdgeDetection\HalconEdgeDetector.cs` is missing.

- [ ] **Step 3: Implement minimal Halcon adapter**

Implement:
- null image returns failed `EdgeResult`
- undefined ROI returns failed `EdgeResult`
- `MeasurePos` path uses `GenMeasureRectangle2`, `MeasurePos`, and `CloseMeasure`
- `EdgesSubPix` path uses `GenRectangle2`, `ReduceDomain`, `EdgesSubPix`, and returns success when at least one contour object exists
- `HalconException` is converted to failed `EdgeResult` with message

- [ ] **Step 4: Run Halcon project build to verify GREEN**

Expected: build succeeds.

---

### Task 4: WinForms Single-ROI UI Wiring

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`
- Modify: `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`

- [ ] **Step 1: Add UI compile references first**

Add fields and event handler references in Designer to force compile failure until handlers exist.

- [ ] **Step 2: Run App build to verify RED**

Run:

```powershell
dotnet build .\src\FlashMeasurementSystem.App.Wpf\FlashMeasurementSystem.App.Wpf.csproj /p:Configuration=Debug /p:Platform=x64
```

Expected: FAIL on missing `RunEdgeDetectionButton_Click` or related fields.

- [ ] **Step 3: Implement minimal UI wiring**

Add:
- `HalconEdgeDetector _edgeDetector`
- Edge Detection group box below existing controls
- Algorithm radio buttons
- Numeric inputs: sigma, threshold, ROI width, scan length
- Combo boxes: polarity, selector, subpixel method
- Detect/Clear buttons
- results DataGridView
- status label

- [ ] **Step 4: Implement Detect/Clear behavior**

Use current rectangle ROI from `HWindowControlHelper.GetCurrentRoi()` and convert to `EdgeDetectionRoi` with center, half lengths, and `AngleRad = 0.0` for v1. Draw crosses for returned edge points.

- [ ] **Step 5: Run App build to verify GREEN**

Expected: App builds for x64.

---

### Task 5: Final Verification

- [ ] **Step 1: Run test harness**

```powershell
.\tests\FlashMeasurementSystem.Tests\bin\Debug\FlashMeasurementSystem.Tests.exe
```

Expected: `EdgeDetectionDomainTests passed`.

- [ ] **Step 2: Run Any CPU solution build**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: success.

- [ ] **Step 3: Run x64 solution build**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: success.

- [ ] **Step 4: Manual smoke test**

Open app, load image, draw ROI, run MeasurePos, verify edge crosses and table output. Switch EdgesSubPix and verify contour-compatible result status.
