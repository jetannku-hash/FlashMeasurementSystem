# Image Quality Check Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a layered Halcon Image Quality Check feature and expose it through the current WinForms main window for manual testing with images from `data/images`.

**Architecture:** Domain contains plain result and threshold models. Application contains a generic checker contract that avoids a Halcon dependency. Halcon implements the checker using the manual's algorithm, and the existing WinForms `MainWindow` provides a thin test harness.

**Tech Stack:** .NET Framework 4.8, C# 7.3, Windows Forms, HalconDotNet 17.12, old-style `.csproj` compile includes.

---

## Task 1: Domain Image Quality Models

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/ImageQuality/ImageQualityResult.cs`
- Create: `src/FlashMeasurementSystem.Domain/ImageQuality/ImageQualityThresholds.cs`
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`

- [ ] **Step 1: Create `ImageQualityResult`**

```csharp
namespace FlashMeasurementSystem.Domain.ImageQuality
{
    public class ImageQualityResult
    {
        public bool Pass { get; set; }
        public double MeanBrightness { get; set; }
        public double SaturationRatio { get; set; }
        public double BlurScore { get; set; }
        public double Contrast { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 2: Create `ImageQualityThresholds`**

```csharp
namespace FlashMeasurementSystem.Domain.ImageQuality
{
    public class ImageQualityThresholds
    {
        public double MinBrightness { get; set; } = 80.0;
        public double MaxBrightness { get; set; } = 180.0;
        public double MaxSaturationRatio { get; set; } = 1.0;
        public double MinBlurScore { get; set; } = 100.0;
        public double MinContrast { get; set; } = 20.0;

        public static ImageQualityThresholds Default()
        {
            return new ImageQualityThresholds();
        }
    }
}
```

- [ ] **Step 3: Add compile includes to Domain `.csproj`**

Add these inside the existing `<ItemGroup>` that contains `Properties\AssemblyInfo.cs`:

```xml
<Compile Include="ImageQuality\ImageQualityResult.cs" />
<Compile Include="ImageQuality\ImageQualityThresholds.cs" />
```

- [ ] **Step 4: Verify Domain builds through solution**

Run:

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build reaches at least the same baseline result as before this task. If it fails, fix only errors introduced by the new Domain files.

## Task 2: Application Checker Contract

**Files:**
- Create: `src/FlashMeasurementSystem.Application/ImageQuality/IImageQualityChecker.cs`
- Modify: `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`

- [ ] **Step 1: Create generic checker interface**

```csharp
using FlashMeasurementSystem.Domain.ImageQuality;

namespace FlashMeasurementSystem.Application.ImageQuality
{
    public interface IImageQualityChecker<TImage>
    {
        ImageQualityResult Check(TImage image, ImageQualityThresholds thresholds);
    }
}
```

- [ ] **Step 2: Add compile include to Application `.csproj`**

Add this inside the existing `<ItemGroup>` that contains `Properties\AssemblyInfo.cs`:

```xml
<Compile Include="ImageQuality\IImageQualityChecker.cs" />
```

- [ ] **Step 3: Build solution**

Run:

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Expected: build succeeds or only pre-existing unrelated issues remain.

## Task 3: Halcon Image Quality Checker

**Files:**
- Create: `src/FlashMeasurementSystem.Halcon/ImageQuality/HalconImageQualityChecker.cs`
- Modify: `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`

- [ ] **Step 1: Implement Halcon checker**

```csharp
using System.Collections.Generic;
using FlashMeasurementSystem.Application.ImageQuality;
using FlashMeasurementSystem.Domain.ImageQuality;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.ImageQuality
{
    public class HalconImageQualityChecker : IImageQualityChecker<HImage>
    {
        public ImageQualityResult Check(HImage image, ImageQualityThresholds thresholds)
        {
            var result = new ImageQualityResult();
            var effectiveThresholds = thresholds ?? ImageQualityThresholds.Default();

            try
            {
                HOperatorSet.Intensity(new HImage(), image, out HTuple meanBrightness, out HTuple _);
                result.MeanBrightness = meanBrightness.D;

                HRegion saturatedRegion = image.Threshold(254.0, 255.0);
                HOperatorSet.AreaCenter(saturatedRegion, out HTuple satArea, out HTuple _, out HTuple _);
                HOperatorSet.GetImagePointer1(image, out HTuple _, out HTuple _, out HTuple width, out HTuple height);

                double totalPixels = width.D * height.D;
                result.SaturationRatio = totalPixels <= 0.0 ? 0.0 : (satArea.D / totalPixels) * 100.0;

                HImage laplace = image.Laplace("absolute", 3, "n_4_self_opt");
                HOperatorSet.Intensity(new HImage(), laplace, out HTuple _, out HTuple blurDeviation);
                result.BlurScore = blurDeviation.D;

                HOperatorSet.Intensity(new HImage(), image, out HTuple _, out HTuple deviation);
                result.Contrast = deviation.D;

                var failures = new List<string>();

                if (result.MeanBrightness < effectiveThresholds.MinBrightness)
                    failures.Add($"過暗 (mean={result.MeanBrightness:F1} < {effectiveThresholds.MinBrightness})");
                else if (result.MeanBrightness > effectiveThresholds.MaxBrightness)
                    failures.Add($"過亮 (mean={result.MeanBrightness:F1} > {effectiveThresholds.MaxBrightness})");

                if (result.SaturationRatio > effectiveThresholds.MaxSaturationRatio)
                    failures.Add($"飽和過高 ({result.SaturationRatio:F2}% > {effectiveThresholds.MaxSaturationRatio}%)");

                if (result.BlurScore < effectiveThresholds.MinBlurScore)
                    failures.Add($"模糊 (blur score={result.BlurScore:F1} < {effectiveThresholds.MinBlurScore})");

                if (result.Contrast < effectiveThresholds.MinContrast)
                    failures.Add($"對比不足 (contrast={result.Contrast:F1} < {effectiveThresholds.MinContrast})");

                result.Pass = failures.Count == 0;
                result.Message = result.Pass ? "影像品質合格" : string.Join("; ", failures);
            }
            catch (HalconException ex)
            {
                result.Pass = false;
                result.Message = $"影像品質檢查異常：{ex.Message}";
            }

            return result;
        }
    }
}
```

- [ ] **Step 2: Add compile include to Halcon `.csproj`**

Add this inside the existing `<ItemGroup>` that contains `Properties\AssemblyInfo.cs`:

```xml
<Compile Include="ImageQuality\HalconImageQualityChecker.cs" />
```

- [ ] **Step 3: Build x64**

Run:

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: Halcon project compiles against the configured HalconDotNet reference.

## Task 4: Main Window Manual Test UI

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

- [ ] **Step 1: Add WinForms controls in Designer**

Add fields for:

```csharp
private System.Windows.Forms.ComboBox imageComboBox;
private System.Windows.Forms.Button runImageQualityCheckButton;
private System.Windows.Forms.Label selectedImageLabel;
private System.Windows.Forms.TextBox resultTextBox;
```

Initialize them in `InitializeComponent()` with readable positions and attach:

```csharp
this.runImageQualityCheckButton.Click += new System.EventHandler(this.RunImageQualityCheckButton_Click);
```

- [ ] **Step 2: Add MainWindow logic**

Add helper methods to `MainWindow.cs`:

```csharp
private void LoadImageList()
private string FindImagesDirectory()
private void RunImageQualityCheckButton_Click(object sender, EventArgs e)
private void DisplayImageQualityResult(ImageQualityResult result)
```

Behavior:

- `FindImagesDirectory()` locates `data/images` by walking upward from `AppDomain.CurrentDomain.BaseDirectory` until it finds `FlashMeasurementSystem.sln`, then combines `data/images`.
- `LoadImageList()` lists supported extensions and stores full paths as combo box item values.
- `RunImageQualityCheckButton_Click()` reads the selected image into `HImage`, calls `HalconImageQualityChecker`, and displays metrics.

- [ ] **Step 3: Preserve current Halcon version smoke check**

Keep `HOperatorSet.GetSystem("version", out HTuple version)` in `MainWindow_Loaded`, then call `LoadImageList()` after the smoke check attempt.

- [ ] **Step 4: Build Any CPU and x64**

Run:

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Expected: app compiles with new references and UI event handler.

## Task 5: Manual Verification

**Files:**
- No code files unless verification reveals an issue caused by this feature.

- [ ] **Step 1: Launch application**

Run the app from Visual Studio or the built executable for the x64 configuration if Halcon requires x64.

- [ ] **Step 2: Confirm image dropdown**

Expected: dropdown contains `000.png` and `002.png` from `data/images`.

- [ ] **Step 3: Run checks**

Select each image and click the check button.

Expected: result text shows:

- Pass/Fail
- Mean Brightness
- Saturation Ratio
- Blur Score
- Contrast
- Message

- [ ] **Step 4: Document verification result**

Record build commands, pass/fail status, and any environment blocker such as missing Halcon runtime/license.
