# Angle Measurement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add layered Angle Measurement from manual section 4.7 and expose it as a testable WinForms action on the existing Measurement tab, reusing the existing coordinate-input box and `Append Line` button.

**Architecture:** Add pure Angle Measurement models in Domain, an Application interface, and a HALCON adapter that calls official HALCON 17.12 `angle_ll` / `angle_lx`. The existing transitional WinForms `MainWindow` gets an angle-mode combo, a `Measure Angle` button, and an angle overlay on the Measurement tab.

**Tech Stack:** C# .NET Framework 4.8, WinForms, HALCON 17.12 `HalconDotNet`, old-style `.csproj`, console-style tests.

---

## Implementation Rules

- Do not commit during execution unless the user explicitly asks for commits. Suggested commit messages below are checkpoint labels only.
- Keep dependency direction unchanged: `Domain <- Application <- Halcon <- App.Wpf`.
- Do not put Halcon types in Domain or Application.
- Do not fold angle into `DistanceMeasurementType` / `DistanceMeasurementResult`; it is a separate feature.
- Do not add tolerance/OK-NG judgement, `intersection_ll` overlay, recipe integration, or MeasurementWorkflow integration.
- Keep `src/FlashMeasurementSystem.App.Wpf` as WinForms. Do not migrate to WPF/XAML.
- Verify HALCON operator parameter order against `halcon_pdf/reference/reference_hdevelop.txt` (L155253 `angle_ll`, L155322 `angle_lx`) — do not rely on memory.

## File Map

- Create `src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleMeasurementParameters.cs`: supported modes and defaults.
- Create `src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleMeasurementResult.cs`: pure result DTO.
- Modify `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`: include Angle Measurement Domain files.
- Create `src/FlashMeasurementSystem.Application/AngleMeasurement/IAngleMeasurer.cs`: Application contract in plain Domain types.
- Modify `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`: include `IAngleMeasurer.cs`.
- Create `src/FlashMeasurementSystem.Halcon/AngleMeasurement/HalconAngleMeasurer.cs`: HALCON adapter.
- Modify `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`: include adapter file.
- Create `tests/FlashMeasurementSystem.Tests/AngleMeasurementDomainTests.cs`: console-style tests.
- Modify `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`: include test file.
- Modify `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`: call `AngleMeasurementDomainTests.Run()`.
- Modify `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`: instantiate measurer, run handler, parse coordinates, draw overlay.
- Modify `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`: add angle mode combo, Measure Angle button, and rows.

## Task 1: Add Domain Tests First

**Files:**
- Create: `tests/FlashMeasurementSystem.Tests/AngleMeasurementDomainTests.cs`
- Modify: `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`

- [ ] **Step 1: Create failing Angle Measurement domain tests**

Create `tests/FlashMeasurementSystem.Tests/AngleMeasurementDomainTests.cs`:

```csharp
using System;
using FlashMeasurementSystem.Domain.AngleMeasurement;

namespace FlashMeasurementSystem.Tests
{
    public static class AngleMeasurementDomainTests
    {
        public static void Run()
        {
            AngleMeasurementParameters parameters = AngleMeasurementParameters.Default();

            AssertEqual("line_to_line", parameters.Mode, "Default Mode");
            AssertEqual(2.0, parameters.NearParallelWarningDeg, "Default NearParallelWarningDeg");
            AssertEqual(1.0, parameters.MinPointSeparation, "Default MinPointSeparation");

            if (!AngleMeasurementParameters.IsSupportedMode("line_to_line"))
                throw new InvalidOperationException("line_to_line should be supported");
            if (!AngleMeasurementParameters.IsSupportedMode("line_to_horizontal"))
                throw new InvalidOperationException("line_to_horizontal should be supported");
            if (!AngleMeasurementParameters.IsSupportedMode("line_to_vertical"))
                throw new InvalidOperationException("line_to_vertical should be supported");
            if (AngleMeasurementParameters.IsSupportedMode("line_to_diagonal"))
                throw new InvalidOperationException("line_to_diagonal should not be supported");

            AngleMeasurementResult result = new AngleMeasurementResult();
            AssertEqual(false, result.Success, "Default Success");
            AssertEqual(0.0, result.AngleDeg, "Default AngleDeg");
            AssertEqual(0.0, result.AngleRad, "Default AngleRad");
            AssertEqual(0.0, result.AcuteAngleDeg, "Default AcuteAngleDeg");
            AssertEqual(0.0, result.RawAngleDeg, "Default RawAngleDeg");
            AssertEqual(0.0, result.RefAngle1Deg, "Default RefAngle1Deg");
            AssertEqual(0.0, result.RefAngle2Deg, "Default RefAngle2Deg");
            AssertEqual(false, result.IsNearParallel, "Default IsNearParallel");
            AssertEqual(string.Empty, result.ErrorMessage, "Default ErrorMessage");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
            }
        }
    }
}
```

- [ ] **Step 2: Include test file in old-style csproj**

In `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`, add `AngleMeasurementDomainTests.cs` inside the compile item group (beside the existing `*DomainTests.cs` entries).

- [ ] **Step 3: Wire test runner**

In `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`, add the angle suite call after the `DistanceMeasurementDomainTests` block and before the final `EdgeDetectionDomainTests passed` lines:

```csharp
DistanceMeasurementDomainTests.Run();
Console.WriteLine("DistanceMeasurementDomainTests passed");
AngleMeasurementDomainTests.Run();
Console.WriteLine("AngleMeasurementDomainTests passed");
Console.WriteLine("EdgeDetectionDomainTests passed");
```

> Note: as of 2026-06-10 the runner has a duplicated `Console.WriteLine("EdgeDetectionDomainTests passed");` line. Leave it as-is unless told otherwise — do not "tidy" unrelated lines (surgical-change rule).

- [ ] **Step 4: Run tests to verify red state**

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build fails because `FlashMeasurementSystem.Domain.AngleMeasurement` does not exist yet.

- [ ] **Step 5: Checkpoint**

Suggested commit message if the user later asks for commits: `test: add angle measurement domain tests`.

## Task 2: Add Domain Models

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleMeasurementParameters.cs`
- Create: `src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleMeasurementResult.cs`
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`

- [ ] **Step 1: Create `AngleMeasurementParameters`**

Create `src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleMeasurementParameters.cs`:

```csharp
namespace FlashMeasurementSystem.Domain.AngleMeasurement
{
    public class AngleMeasurementParameters
    {
        // 預設值寫在屬性 initializer 上，確保 new 出來的物件天生合法。
        public string Mode { get; set; } = "line_to_line";
        public double NearParallelWarningDeg { get; set; } = 2.0;
        public double MinPointSeparation { get; set; } = 1.0;

        public static AngleMeasurementParameters Default()
        {
            return new AngleMeasurementParameters();
        }

        public static bool IsSupportedMode(string mode)
        {
            return mode == "line_to_line"
                || mode == "line_to_horizontal"
                || mode == "line_to_vertical";
        }
    }
}
```

- [ ] **Step 2: Create `AngleMeasurementResult`**

Create `src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleMeasurementResult.cs`:

```csharp
namespace FlashMeasurementSystem.Domain.AngleMeasurement
{
    public class AngleMeasurementResult
    {
        public bool Success { get; set; }
        public double AngleDeg { get; set; }       // 主要答案：兩線夾角 [0,180]
        public double AngleRad { get; set; }       // 同上，弧度 [0,π]
        public double AcuteAngleDeg { get; set; }  // 折成銳角 [0,90]，與端點順序無關
        public double RawAngleDeg { get; set; }    // angle_ll 原始有號值 (-180,180]
        public double RefAngle1Deg { get; set; }   // 線1 對水平軸 (angle_lx)
        public double RefAngle2Deg { get; set; }   // 線2 對水平軸 (angle_lx)
        public bool IsNearParallel { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 3: Include Domain files in csproj**

In `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`, add:

```xml
<Compile Include="AngleMeasurement\AngleMeasurementParameters.cs" />
<Compile Include="AngleMeasurement\AngleMeasurementResult.cs" />
```

- [ ] **Step 4: Run domain tests**

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
.\tests\FlashMeasurementSystem.Tests\bin\Debug\FlashMeasurementSystem.Tests.exe
```

Expected: build passes and the run prints `AngleMeasurementDomainTests passed` (interface contract test is added in Task 3).

- [ ] **Step 5: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add angle measurement domain models`.

## Task 3: Add Application Interface

**Files:**
- Create: `src/FlashMeasurementSystem.Application/AngleMeasurement/IAngleMeasurer.cs`
- Modify: `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/AngleMeasurementDomainTests.cs`

- [ ] **Step 1: Extend tests with interface contract compile check**

In `tests/FlashMeasurementSystem.Tests/AngleMeasurementDomainTests.cs`, add this `using`:

```csharp
using FlashMeasurementSystem.Application.AngleMeasurement;
```

Add at the end of `Run()`:

```csharp
IAngleMeasurer measurer = new FakeAngleMeasurer();
AngleMeasurementResult fake = measurer.MeasureAngle(0, 0, 0, 10, 0, 0, 10, 0, parameters);
AssertEqual(true, fake.Success, "Fake angle measurer should satisfy interface contract");
```

Add this nested fake class after `AssertEqual<T>()`:

```csharp
private sealed class FakeAngleMeasurer : IAngleMeasurer
{
    public AngleMeasurementResult MeasureAngle(
        double line1Row1, double line1Col1, double line1Row2, double line1Col2,
        double line2Row1, double line2Col1, double line2Row2, double line2Col2,
        AngleMeasurementParameters parameters)
    {
        return new AngleMeasurementResult { Success = true };
    }
}
```

- [ ] **Step 2: Run tests to verify red state**

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build fails because `FlashMeasurementSystem.Application.AngleMeasurement` and `IAngleMeasurer` do not exist yet.

- [ ] **Step 3: Create interface**

Create `src/FlashMeasurementSystem.Application/AngleMeasurement/IAngleMeasurer.cs`:

```csharp
using FlashMeasurementSystem.Domain.AngleMeasurement;

namespace FlashMeasurementSystem.Application.AngleMeasurement
{
    public interface IAngleMeasurer
    {
        AngleMeasurementResult MeasureAngle(
            double line1Row1, double line1Col1, double line1Row2, double line1Col2,
            double line2Row1, double line2Col1, double line2Row2, double line2Col2,
            AngleMeasurementParameters parameters);
    }
}
```

- [ ] **Step 4: Include interface in csproj**

In `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`, add:

```xml
<Compile Include="AngleMeasurement\IAngleMeasurer.cs" />
```

- [ ] **Step 5: Build Application and test projects**

```powershell
dotnet build .\src\FlashMeasurementSystem.Application\FlashMeasurementSystem.Application.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
.\tests\FlashMeasurementSystem.Tests\bin\Debug\FlashMeasurementSystem.Tests.exe
```

Expected: both builds pass; the run prints `AngleMeasurementDomainTests passed`.

- [ ] **Step 6: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add angle measurement application contract`.

## Task 4: Add HALCON Angle Measurer Adapter

**Files:**
- Create: `src/FlashMeasurementSystem.Halcon/AngleMeasurement/HalconAngleMeasurer.cs`
- Modify: `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`

- [ ] **Step 1: Create `HalconAngleMeasurer`**

Create `src/FlashMeasurementSystem.Halcon/AngleMeasurement/HalconAngleMeasurer.cs`:

```csharp
using System;
using FlashMeasurementSystem.Application.AngleMeasurement;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.AngleMeasurement
{
    public class HalconAngleMeasurer : IAngleMeasurer
    {
        // 合成參考向量長度（像素）。只要非零即可定義方向；與線1第一點共用起點，
        // 確保 angle_ll 的旋轉中心（兩線交點）有定義。
        private const double ReferenceLength = 100.0;

        public AngleMeasurementResult MeasureAngle(
            double line1Row1, double line1Col1, double line1Row2, double line1Col2,
            double line2Row1, double line2Col1, double line2Row2, double line2Col2,
            AngleMeasurementParameters parameters)
        {
            AngleMeasurementResult result = new AngleMeasurementResult();
            AngleMeasurementParameters p = parameters ?? AngleMeasurementParameters.Default();

            if (!AngleMeasurementParameters.IsSupportedMode(p.Mode))
            {
                result.ErrorMessage = "不支援的角度量測模式: " + p.Mode;
                return result;
            }

            if (Separation(line1Row1, line1Col1, line1Row2, line1Col2) < p.MinPointSeparation)
            {
                result.ErrorMessage = string.Format(
                    "線 1 兩端點過近，方向不可靠 (需 >= {0} px)", p.MinPointSeparation);
                return result;
            }

            // 依模式決定線 2（水平/垂直模式合成參考向量，忽略傳入的 line2*）。
            if (p.Mode == "line_to_horizontal")
            {
                line2Row1 = line1Row1; line2Col1 = line1Col1;
                line2Row2 = line1Row1; line2Col2 = line1Col1 + ReferenceLength; // +Column = 水平
            }
            else if (p.Mode == "line_to_vertical")
            {
                line2Row1 = line1Row1; line2Col1 = line1Col1;
                line2Row2 = line1Row1 + ReferenceLength; line2Col2 = line1Col1; // +Row = 垂直
            }
            else
            {
                if (Separation(line2Row1, line2Col1, line2Row2, line2Col2) < p.MinPointSeparation)
                {
                    result.ErrorMessage = string.Format(
                        "線 2 兩端點過近，方向不可靠 (需 >= {0} px)", p.MinPointSeparation);
                    return result;
                }
            }

            try
            {
                // angle_ll：旋轉向量A逆時針到向量B，回傳有號弧度 -π..π（reference L155253）。
                HOperatorSet.AngleLl(
                    line1Row1, line1Col1, line1Row2, line1Col2,
                    line2Row1, line2Col1, line2Row2, line2Col2,
                    out HTuple angle);

                double raw = angle.D;
                result.RawAngleDeg = raw * 180.0 / Math.PI;

                double directed = Math.Abs(raw);             // 0..π，方向向量夾角
                result.AngleRad = directed;
                result.AngleDeg = directed * 180.0 / Math.PI;

                result.AcuteAngleDeg = result.AngleDeg > 90.0
                    ? 180.0 - result.AngleDeg
                    : result.AngleDeg;

                // angle_lx：線對水平軸夾角（reference L155322）。
                HOperatorSet.AngleLx(line1Row1, line1Col1, line1Row2, line1Col2, out HTuple ref1);
                result.RefAngle1Deg = ref1.D * 180.0 / Math.PI;
                HOperatorSet.AngleLx(line2Row1, line2Col1, line2Row2, line2Col2, out HTuple ref2);
                result.RefAngle2Deg = ref2.D * 180.0 / Math.PI;

                result.IsNearParallel = result.AcuteAngleDeg < p.NearParallelWarningDeg;
                result.Success = true;
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "角度量測失敗: " + ex.Message;
            }

            return result;
        }

        private static double Separation(double r1, double c1, double r2, double c2)
        {
            double dr = r2 - r1;
            double dc = c2 - c1;
            return Math.Sqrt(dr * dr + dc * dc);
        }
    }
}
```

- [ ] **Step 2: Include adapter in csproj**

In `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`, add:

```xml
<Compile Include="AngleMeasurement\HalconAngleMeasurer.cs" />
```

- [ ] **Step 3: Build Halcon project in Any CPU and x64**

```powershell
dotnet build .\src\FlashMeasurementSystem.Halcon\FlashMeasurementSystem.Halcon.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\src\FlashMeasurementSystem.Halcon\FlashMeasurementSystem.Halcon.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: both builds pass. If HALCON reference resolution fails, report the exact path/SDK blocker before changing code.

- [ ] **Step 4: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add halcon angle measurer`.

## Task 5: Wire Angle Measurement Into MainWindow Code

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

> Before editing, grep `MainWindow.cs` for `_distanceMeasurer`, `MeasureDistanceButton_Click`, `ParseCoordinateLine`, `DrawFittingLayers`, and `ClearResultDisplays` to confirm current line numbers — the snippets below are anchored to method names, not line numbers.

- [ ] **Step 1: Add using statements**

At the top of `MainWindow.cs`, beside the Distance Measurement namespaces, add:

```csharp
using FlashMeasurementSystem.Domain.AngleMeasurement;
using FlashMeasurementSystem.Halcon.AngleMeasurement;
```

- [ ] **Step 2: Add measurer field**

Beside `private readonly HalconDistanceMeasurer _distanceMeasurer = new HalconDistanceMeasurer();` add:

```csharp
private readonly HalconAngleMeasurer _angleMeasurer = new HalconAngleMeasurer();
```

- [ ] **Step 3: Default the angle mode combo**

In the constructor, beside the existing `EnsureComboDefault(measurementTypeCombo);` call, add:

```csharp
EnsureComboDefault(angleModeCombo);            // 預設 "line_to_line"
```

- [ ] **Step 4: Add `MeasureAngleButton_Click`**

Add this method after `MeasureDistanceButton_Click`:

```csharp
private void MeasureAngleButton_Click(object sender, EventArgs e)
{
    try
    {
        AngleMeasurementParameters parameters = new AngleMeasurementParameters
        {
            Mode = angleModeCombo.SelectedItem == null ? "line_to_line" : angleModeCombo.SelectedItem.ToString()
        };

        string[] lines = measurementCoordInput.Text.Split(
            new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        AngleMeasurementResult result;

        if (parameters.Mode == "line_to_line")
        {
            if (lines.Length < 4)
            {
                measureResultLabel.Text = "角度量測需 4 行座標（線1兩端點、線2兩端點）。可按兩次 Append Line。";
                return;
            }
            double[] a1 = ParseCoordinateLine(lines[0]);
            double[] a2 = ParseCoordinateLine(lines[1]);
            double[] b1 = ParseCoordinateLine(lines[2]);
            double[] b2 = ParseCoordinateLine(lines[3]);
            if (a1 == null || a2 == null || b1 == null || b2 == null)
            {
                measureResultLabel.Text = "座標格式錯誤（每行 row,col）。";
                return;
            }
            result = _angleMeasurer.MeasureAngle(
                a1[0], a1[1], a2[0], a2[1], b1[0], b1[1], b2[0], b2[1], parameters);
            if (result.Success) DrawAngleOverlay(a1, a2, b1, b2, result);
        }
        else
        {
            if (lines.Length < 2)
            {
                measureResultLabel.Text = "角度量測（對水平/垂直）需 2 行座標（單一條線的兩端點）。可按一次 Append Line。";
                return;
            }
            double[] a1 = ParseCoordinateLine(lines[0]);
            double[] a2 = ParseCoordinateLine(lines[1]);
            if (a1 == null || a2 == null)
            {
                measureResultLabel.Text = "座標格式錯誤（每行 row,col）。";
                return;
            }
            result = _angleMeasurer.MeasureAngle(
                a1[0], a1[1], a2[0], a2[1], 0, 0, 0, 0, parameters);
            if (result.Success) DrawAngleRefOverlay(a1, a2, parameters.Mode, result);
        }

        if (result.Success)
        {
            string warn = result.IsNearParallel ? "  (近平行，建議改用距離量測)" : "";
            measureResultLabel.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Angle: {0:F3}°  (acute {1:F3}°){2}\r\nL1∠x={3:F2}°  L2∠x={4:F2}°  raw={5:F2}°",
                result.AngleDeg, result.AcuteAngleDeg, warn,
                result.RefAngle1Deg, result.RefAngle2Deg, result.RawAngleDeg);
        }
        else
        {
            measureResultLabel.Text = "Failed: " + result.ErrorMessage;
        }
    }
    catch (Exception ex)
    {
        measureResultLabel.Text = "Error: " + ex.Message;
    }
}
```

- [ ] **Step 5: Add `DrawAngleOverlay` (two-line mode)**

Add after `DrawMeasurementOverlay`:

```csharp
private void DrawAngleOverlay(double[] a1, double[] a2, double[] b1, double[] b2, AngleMeasurementResult result)
{
    if (_imageHelper == null || _imageHelper.CurrentImage == null) return;
    string angleText = string.Format(CultureInfo.InvariantCulture, "{0:F2}°", result.AngleDeg);

    _imageHelper.SetPersistentOverlayAction(() =>
    {
        OverlayAnnotator an = _imageHelper.Annotator;
        // 先重畫偵測/擬合底層再疊角度標註（與距離 overlay 一致）。
        DrawFittingLayers(an);
        an.DrawLine(a1[0], a1[1], a2[0], a2[1], "green");
        an.DrawText("L1", (int)((a1[0] + a2[0]) / 2), (int)((a1[1] + a2[1]) / 2), "green");
        an.DrawLine(b1[0], b1[1], b2[0], b2[1], "green");
        an.DrawText("L2", (int)((b1[0] + b2[0]) / 2), (int)((b1[1] + b2[1]) / 2), "green");
        // 頂點 = 四點重心（對平行線也安全，非真正交點）。
        double vr = (a1[0] + a2[0] + b1[0] + b2[0]) / 4.0;
        double vc = (a1[1] + a2[1] + b1[1] + b2[1]) / 4.0;
        an.DrawCross(vr, vc, 18, "magenta");
        an.DrawText(angleText, (int)vr - 16, (int)vc + 8, "yellow");
    });
}
```

- [ ] **Step 6: Add `DrawAngleRefOverlay` (horizontal/vertical mode)**

Add after `DrawAngleOverlay`:

```csharp
private void DrawAngleRefOverlay(double[] a1, double[] a2, string mode, AngleMeasurementResult result)
{
    if (_imageHelper == null || _imageHelper.CurrentImage == null) return;
    string angleText = string.Format(CultureInfo.InvariantCulture, "{0:F2}°", result.AngleDeg);
    const double refLen = 100.0;

    _imageHelper.SetPersistentOverlayAction(() =>
    {
        OverlayAnnotator an = _imageHelper.Annotator;
        DrawFittingLayers(an);
        an.DrawLine(a1[0], a1[1], a2[0], a2[1], "green");
        an.DrawText("L1", (int)((a1[0] + a2[0]) / 2), (int)((a1[1] + a2[1]) / 2), "green");
        // 參考軸（灰）通過線1第一點
        if (mode == "line_to_vertical")
            an.DrawLine(a1[0], a1[1], a1[0] + refLen, a1[1], "gray");
        else
            an.DrawLine(a1[0], a1[1], a1[0], a1[1] + refLen, "gray");
        an.DrawCross(a1[0], a1[1], 18, "magenta");
        an.DrawText(angleText, (int)a1[0] - 16, (int)a1[1] + 8, "yellow");
    });
}
```

- [ ] **Step 7: Reset angle result on new image**

In `ClearResultDisplays()` (called from `LoadAndDisplayImage`), the existing `measureResultLabel`/`matchResultTextBox` resets already cover the shared result label. Confirm `measureResultLabel` is reset there; if it is not currently reset, add:

```csharp
measureResultLabel.Text = string.Empty;
```

so a stale angle string does not persist across image loads. (The overlay itself is already cleared by `DisplayImage` resetting the persistent overlay action.)

- [ ] **Step 8: Build to expose designer dependency**

```powershell
dotnet build .\src\FlashMeasurementSystem.App.Wpf\FlashMeasurementSystem.App.Wpf.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build fails until Task 6 declares `angleModeCombo` and `measureAngleButton` and wires the click handler in the Designer.

- [ ] **Step 9: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: wire angle measurement window logic`.

## Task 6: Add WinForms Designer Controls

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`

> The Measurement tab uses `measurementTableLayout` (2 columns 42%/58%). As of 2026-06-10 it has `RowCount = 9`; row indices 0-4 are `Absolute 26F`, index 5 `Absolute 130F` (coord input), index 6 `Absolute 28F` (append button panel), index 7 `Absolute 36F` (`measureDistanceButton`), index 8 `Percent 100F` (`measureResultLabel`). Confirm by grepping for `measurementTableLayout.RowStyles` and `measurementTableLayout.Controls.Add` before editing.

- [ ] **Step 1: Add field declarations and initialization**

Declare three new controls beside the existing measurement controls. In the initialization region:

```csharp
this.angleModeLabel = new System.Windows.Forms.Label();
this.angleModeCombo = new System.Windows.Forms.ComboBox();
this.measureAngleButton = new System.Windows.Forms.Button();
```

In the class field declarations at the bottom of the file:

```csharp
private System.Windows.Forms.Label angleModeLabel;
private System.Windows.Forms.ComboBox angleModeCombo;
private System.Windows.Forms.Button measureAngleButton;
```

- [ ] **Step 2: Re-home `measureResultLabel` and add two rows**

Change `measurementTableLayout.RowCount` from `9` to `11`. The new layout (by index) must be:

- `0-4` : `Absolute 26F` (unchanged)
- `5` : `Absolute 130F` (coord input, unchanged)
- `6` : `Absolute 28F` (append button panel, unchanged)
- `7` : `Absolute 36F` (`measureDistanceButton`, unchanged)
- `8` : `Absolute 26F` — **new** angle mode row (`angleModeLabel` col 0 + `angleModeCombo` col 1)
- `9` : `Absolute 36F` — **new** `measureAngleButton` row (col-span 2)
- `10` : `Percent 100F` (`measureResultLabel`, moved from index 8)

> ⚠️ `TableLayoutPanel` maps `RowStyles` by index. The current collection ends with `RowStyle(Percent, 100F)` for `measureResultLabel`. **Insert** the two new `Absolute` styles **before** that trailing percent entry (or rebuild the collection in the order above). If you append them after the percent row, the new rows inherit the stretch and the result label collapses — the same trap documented in the Circle Fitting plan.

Update the `Controls.Add` calls. The existing block ends with:

```csharp
this.measurementTableLayout.Controls.Add(this.measureDistanceButton, 0, 7);
this.measurementTableLayout.Controls.Add(this.measureResultLabel, 0, 8);
```

Replace with:

```csharp
this.measurementTableLayout.Controls.Add(this.measureDistanceButton, 0, 7);
this.measurementTableLayout.Controls.Add(this.angleModeLabel, 0, 8);
this.measurementTableLayout.Controls.Add(this.angleModeCombo, 1, 8);
this.measurementTableLayout.Controls.Add(this.measureAngleButton, 0, 9);
this.measurementTableLayout.SetColumnSpan(this.measureAngleButton, 2);
this.measurementTableLayout.Controls.Add(this.measureResultLabel, 0, 10);
```

(`measureResultLabel` already has `SetColumnSpan(..., 2)` set elsewhere; keep it.)

- [ ] **Step 3: Add the angle mode label block**

```csharp
// 
// angleModeLabel
// 
this.angleModeLabel.AutoSize = true;
this.angleModeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
this.angleModeLabel.Name = "angleModeLabel";
this.angleModeLabel.Text = "Angle mode";
this.angleModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
```

- [ ] **Step 4: Add the angle mode combo block**

```csharp
// 
// angleModeCombo
// 
this.angleModeCombo.Dock = System.Windows.Forms.DockStyle.Fill;
this.angleModeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
this.angleModeCombo.FormattingEnabled = true;
this.angleModeCombo.Items.AddRange(new object[] {
    "line_to_line",
    "line_to_horizontal",
    "line_to_vertical"});
this.angleModeCombo.Name = "angleModeCombo";
```

- [ ] **Step 5: Add the Measure Angle button block**

```csharp
// 
// measureAngleButton
// 
this.measureAngleButton.Dock = System.Windows.Forms.DockStyle.Fill;
this.measureAngleButton.Name = "measureAngleButton";
this.measureAngleButton.Text = "Measure Angle";
this.measureAngleButton.UseVisualStyleBackColor = true;
this.measureAngleButton.Click += new System.EventHandler(this.MeasureAngleButton_Click);
```

- [ ] **Step 6: Build App project**

```powershell
dotnet build .\src\FlashMeasurementSystem.App.Wpf\FlashMeasurementSystem.App.Wpf.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build passes. If designer line numbers differ, preserve the same control hierarchy, row indices, and event wiring.

- [ ] **Step 7: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add angle measurement gui action`.

## Task 7: Full Verification

**Files:**
- Verify all files changed by Tasks 1-6.

- [ ] **Step 1: Run LSP diagnostics on changed source and test files**

```text
src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleMeasurementParameters.cs
src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleMeasurementResult.cs
src/FlashMeasurementSystem.Application/AngleMeasurement/IAngleMeasurer.cs
src/FlashMeasurementSystem.Halcon/AngleMeasurement/HalconAngleMeasurer.cs
src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs
tests/FlashMeasurementSystem.Tests/AngleMeasurementDomainTests.cs
tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
```

Expected: no new diagnostics caused by this change.

- [ ] **Step 2: Build and run tests**

```powershell
dotnet build .\tests\FlashMeasurementSystem.Tests\FlashMeasurementSystem.Tests.csproj /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
.\tests\FlashMeasurementSystem.Tests\bin\Debug\FlashMeasurementSystem.Tests.exe
```

Expected output includes:

```text
DistanceMeasurementDomainTests passed
AngleMeasurementDomainTests passed
EdgeDetectionDomainTests passed
```

- [ ] **Step 3: Build full solution Any CPU and x64**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: both builds pass.

- [ ] **Step 4: Manual GUI verification**

Run the app (close any running instance first — a live app locks the dll files and `dotnet build` then fails with MSB3026/MSB3027):

1. Load an image from `data/images` with two visibly non-parallel straight edges.
2. Edge Detection tab: ROI on edge 1 → Detect → Fit Line. ROI on edge 2 → Detect → Fit Line.
3. Measurement tab: `Append Line` (loads the latest fitted line's 2 endpoints). Re-fit edge 1 first if needed — `Append Line` always appends the *latest* fit, so the intended workflow is: Fit Line on edge 1 → Append Line → Fit Line on edge 2 → Append Line. Confirm 4 coordinate lines appear in the box.
4. Select `line_to_line`, click `Measure Angle`. Confirm the label shows `Angle`, `acute`, `L1∠x`, `L2∠x`, `raw`, and the green lines + magenta vertex + yellow angle text appear with ROI/edge/fit evidence still visible underneath.
5. Cross-check: the displayed `AngleDeg` should roughly equal `|L1∠x − L2∠x|` folded into `[0,180]`.
6. Switch to `line_to_horizontal`: clear the box, `Append Line` once (2 lines), `Measure Angle`. Confirm `AngleDeg` ≈ `|RefAngle1Deg|` folded to `[0,180]`, the gray horizontal reference axis is drawn, and a known-horizontal edge reads ≈ 0°, a known-vertical edge ≈ 90°.
7. Switch to `line_to_vertical` and confirm a vertical edge reads ≈ 0° against the vertical axis.
8. Near-parallel: feed two edges < 2° apart; confirm the `(近平行，建議改用距離量測)` note appears and no crash.
9. Degenerate line: put two identical `row,col` lines for the first line; confirm `線 1 兩端點過近...` failure message, no exception.
10. Too few lines: select `line_to_line` with only 2 coordinate lines; confirm the "需 4 行座標" hint, adapter not called.
11. Stale state: measure an angle, load a different image; confirm the result label resets and no angle overlay is drawn at old coordinates on the new image.

- [ ] **Step 5: Final diff review**

Confirm:

- Domain files contain no Halcon types.
- Application interface references only Domain types.
- HALCON usage matches the reference: `AngleLl(RowA1, ColA1, RowA2, ColA2, RowB1, ColB1, RowB2, ColB2, out Angle)` and `AngleLx(Row1, Col1, Row2, Col2, out Angle)`.
- Angle is a standalone feature; no changes to `DistanceMeasurementType` / `DistanceMeasurementResult`.
- No tolerance/OK-NG, `intersection_ll`, recipe, or workflow code was added.
- No unrelated formatting or refactors were included (including leaving the pre-existing duplicated test print line alone).

- [ ] **Step 6: Checkpoint**

Suggested commit message if the user later asks for commits: `feat: add angle measurement from two lines`.
