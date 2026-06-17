# Image Quality Check Design

**Goal:** Add a manually testable Image Quality Check feature that follows the manual's `CHECKING_IMAGE` stage and the project's layer boundaries.

**Approved approach:** Scheme 1 - layered implementation with a WinForms test UI in the current main window. The image source is fixed to `data/images`.

## Context

The project manual defines the measurement flow as:

```text
ACQUIRING -> CHECKING_IMAGE -> MATCHING_TEMPLATE
```

The manual's Image Quality Check sample uses Halcon APIs (`HImage`, `HOperatorSet`, `HalconException`) to compute:

- Mean brightness
- Saturation ratio
- Blur score
- Contrast

The current application project is named `FlashMeasurementSystem.App.Wpf`, but the active UI is Windows Forms:

- `src/FlashMeasurementSystem.App.Wpf/Program.cs` runs `Application.Run(new MainWindow())`.
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs` defines `MainWindow : Form`.
- `MainWindow.Designer.cs` currently contains an empty form.

The Domain, Application, and Halcon projects are currently skeletons. This feature will introduce the first vertical slice through those layers.

## Architecture

The implementation will keep Halcon-specific code out of Domain and Application. Domain will hold plain result and threshold models. Application will define the checker interface. Halcon will implement the interface using the manual's algorithm. The WinForms main window will act as a thin manual test harness.

Dependency direction remains:

```text
Domain <- Application <- Halcon <- App.Wpf
```

`App.Wpf` already references `Halcon`, so it can instantiate the Halcon adapter for this transitional UI test harness.

## Files

Create:

- `src/FlashMeasurementSystem.Domain/ImageQuality/ImageQualityResult.cs`
- `src/FlashMeasurementSystem.Domain/ImageQuality/ImageQualityThresholds.cs`
- `src/FlashMeasurementSystem.Application/ImageQuality/IImageQualityChecker.cs`
- `src/FlashMeasurementSystem.Halcon/ImageQuality/HalconImageQualityChecker.cs`

Modify:

- `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`

No full WPF/XAML migration is part of this change.

## Domain Layer

`ImageQualityResult` stores the measured values and final pass/fail message:

- `Pass`
- `MeanBrightness`
- `SaturationRatio`
- `BlurScore`
- `Contrast`
- `Message`

`ImageQualityThresholds` stores configurable thresholds with defaults matching the manual:

- `MinBrightness = 80`
- `MaxBrightness = 180`
- `MaxSaturationRatio = 1.0`
- `MinBlurScore = 100.0`
- `MinContrast = 20.0`

These classes must not reference Halcon, UI, file system, or hardware APIs.

## Application Layer

`IImageQualityChecker` defines the application-facing contract:

```csharp
public interface IImageQualityChecker<TImage>
{
    ImageQualityResult Check(TImage image, ImageQualityThresholds thresholds);
}
```

The interface is generic so Application does not reference `HalconDotNet`, while the Halcon adapter can implement `IImageQualityChecker<HImage>` with compile-time type safety.

## Halcon Layer

`HalconImageQualityChecker` implements `IImageQualityChecker<HImage>` using the manual's algorithm:

1. Compute mean brightness with `HOperatorSet.Intensity`.
2. Compute saturation ratio by thresholding gray values `254..255`.
3. Compute blur score using `image.Laplace("absolute", 3, "n_4_self_opt")` and image deviation.
4. Compute contrast using gray-level deviation.
5. Compare against thresholds and return `ImageQualityResult`.

`HalconException` is converted into `ImageQualityResult` with `Pass = false` and a readable message.

## Main Window Test UI

The existing WinForms `MainWindow` will gain:

- A combo box listing images from `data/images`.
- A button to run Image Quality Check.
- Labels or readonly text fields showing pass/fail and metric values.
- A multiline result message area.

Startup behavior:

1. Keep the current Halcon version smoke check.
2. Scan `data/images` relative to the solution root.
3. Populate supported files with extensions `.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`.

Manual test behavior:

1. User selects an image from the dropdown.
2. User clicks the check button.
3. The form loads the selected file into an `HImage`.
4. The form calls `HalconImageQualityChecker.Check` with default thresholds.
5. The form displays all metrics and the message.

If `data/images` is missing or contains no supported images, the UI displays a clear message and disables the check button.

## Error Handling

- Missing image folder: show `找不到 data/images 測試圖片資料夾`.
- Empty image folder: show `找不到支援的測試圖片`.
- No selected image: show `請先選擇圖片`.
- Halcon load or check failure: return/display a failed result message.

No broad fallback path or alternate image source is included because the approved scope is fixed `data/images` scanning.

## Verification

Build verification:

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
```

Halcon/platform-sensitive verification:

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Manual verification:

1. Start the app.
2. Confirm `000.png` and `002.png` from `data/images` appear in the dropdown.
3. Select each image and run the check.
4. Confirm the UI shows pass/fail, all four metric values, and a message.

## Out Of Scope

- WPF/XAML migration.
- Folder picker or arbitrary image source selection.
- Integration into a full `MeasurementWorkflow` class, because no production workflow implementation currently exists in code.
- Test framework installation unless explicitly requested later.
- Threshold editing UI.
