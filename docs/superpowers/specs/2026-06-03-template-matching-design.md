# Template Matching Design

**Goal:** Add layered Template Matching (create + find) based on the manual's section 4.2, with a WinForms GUI test harness that covers both template creation and template matching.

**Approved approach:** Scheme 3 - both template creation (`TemplateManager`) and template matching (`TemplateMatcher`) from the manual, layered across Domain/Application/Halcon, with GUI controls for both operations.

## Context

Manual §4.2 defines two classes:
- `TemplateManager.CreateTemplate` — creates an `HShapeModel` from a reference image + ROI and saves a `.shm` file.
- `TemplateMatcher.LoadModel` / `FindMatches` — loads a `.shm` model and searches an image, returning match position/angle/score.

The measurement flow places `MATCHING_TEMPLATE` right after `CHECKING_IMAGE`:

```text
ACQUIRING -> CHECKING_IMAGE -> MATCHING_TEMPLATE -> MEASURING
```

The existing main window (`MainWindow : Form`) already has an image dropdown from `data/images` and an image quality test area. This design reuses that dropdown and adds template-specific controls below the existing quality check area.

Current project state: `src/FlashMeasurementSystem.Domain/ImageQuality/` and `src/FlashMeasurementSystem.Halcon/ImageQuality/` already exist from the previous Image Quality Check implementation.

## Architecture

Same layering pattern as Image Quality Check:

```text
Domain <- Application <- Halcon <- App.Wpf
```

- **Domain** — plain result and parameter models. No Halcon, UI, or file-system dependencies.
- **Application** — generic interfaces that avoid `HalconDotNet` references.
- **Halcon** — manual §4.2 algorithm implementations.
- **App.Wpf** — WinForms test harness for both template creation and matching.

## Files

Create:
- `src/FlashMeasurementSystem.Domain/TemplateMatching/TemplateMatchResult.cs`
- `src/FlashMeasurementSystem.Domain/TemplateMatching/TemplateCreationParameters.cs`
- `src/FlashMeasurementSystem.Domain/TemplateMatching/TemplateMatchingParameters.cs`
- `src/FlashMeasurementSystem.Application/TemplateMatching/ITemplateManager.cs`
- `src/FlashMeasurementSystem.Application/TemplateMatching/ITemplateMatcher.cs`
- `src/FlashMeasurementSystem.Halcon/TemplateMatching/HalconTemplateManager.cs`
- `src/FlashMeasurementSystem.Halcon/TemplateMatching/HalconTemplateMatcher.cs`

Modify:
- `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- `src/FlashMeasurementSystem.Halcon/FlashMeasurementSystem.Halcon.csproj`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`

No WPF/XAML migration.

## Domain Layer

`TemplateMatchResult`:
- `Found` (bool)
- `Row` (double)
- `Column` (double)
- `AngleDeg` (double)
- `Score` (double)
- `ScaleX` (double, default 1.0)
- `Message` (string)

`TemplateCreationParameters`:
- `AngleStart` (double, default -5.0)
- `AngleExtent` (double, default 10.0)
- `PyramidLevel` (int, default 3)
- `Metric` (string, default "use_polarity")
- `MinContrast` (int, default 10)

`TemplateMatchingParameters`:
- `MinScore` (double, default 0.75)
- `NumMatches` (int, default 1)
- `MaxOverlap` (double, default 0.5)
- `AngleStart` (double, default -10.0)
- `AngleExtent` (double, default 20.0)

No Halcon or UI dependencies in these types.

## Application Layer

`ITemplateManager<TImage, TRegion>`:
```csharp
string CreateAndSave(TImage image, TRegion templateRegion, string modelFilePath, TemplateCreationParameters parameters);
```
Returns the saved `.shm` file path on success.

`ITemplateMatcher<TImage, TRegion>`:
```csharp
void LoadModel(string modelFilePath);
TemplateMatchResult FindMatches(TImage image, TRegion searchRegion, TemplateMatchingParameters parameters);
TemplateMatchResult FindMatches(TImage image, TemplateMatchingParameters parameters); // null region = full image
```

Generic to avoid `HalconDotNet` reference in Application.

## Halcon Layer

`HalconTemplateManager : ITemplateManager<HImage, HRegion>`:
- Uses `image.ReduceDomain(templateRegion)` + `HShapeModel.CreateShapeModel()` + `WriteShapeModel()`.
- Wraps `HalconException` into `InvalidOperationException`.

`HalconTemplateMatcher : ITemplateMatcher<HImage, HRegion>`, `IDisposable`:
- `LoadModel(string)` loads an `HShapeModel` from a `.shm` file.
- `FindMatches(...)` calls `HOperatorSet.FindShapeModel`, converts `HTuple` results into `TemplateMatchResult`.
- `Dispose()` disposes `HShapeModel`.

## Main Window Test UI

Two new GroupBox sections added below the existing `resultTextBox`:

**Template Creation GroupBox:**
- Reuses the existing image dropdown (`imageComboBox`) as the reference image source.
- NumericUpDown controls:
  - `angleStartNumeric` (default -5.0, range -30 to 30)
  - `angleExtentNumeric` (default 10.0, range 0 to 360)
  - `pyramidLevelNumeric` (default 3, range 1 to 5)
- `modelFilePathTextBox` — default `data/templates/template.shm`, resolved relative to solution root.
- "建立模板" button → runs creation, shows success/error in a result label.
- Uses full image as ROI for testing (no separate ROI selection).

**Template Matching GroupBox:**
- Reuses the existing image dropdown as the search image source.
- `templateComboBox` — scans `data/templates/*.shm`, populated at startup.
- NumericUpDown:
  - `minScoreNumeric` (default 0.75, range 0 to 1, step 0.05)
- "執行模板匹配" button → loads model and runs `FindMatches` on the selected image.
- Displays results in a TextBox below the button.

Startup behavior:
1. Existing Halcon smoke check.
2. Existing image list load.
3. New: scan `data/templates/*.shm` for the template dropdown.
4. Create `data/templates` directory if it doesn't exist.

Window height increased from 450 to ~720 to fit new sections.

## Error Handling

- No `data/templates` dir: auto-create, template dropdown empty with hint "找不到模板檔 (.shm)".
- No `.shm` files found: dropdown empty with hint message.
- No template file selected: show "請先選擇模板檔".
- Halcon create/match failure: show error message in result label/textbox.
- `HShapeModel` correctly disposed via `IDisposable`.

## Out Of Scope

- ROI region selection UI (uses full image for testing).
- Multiple match results display.
- Integration into `MeasurementWorkflow`.
- WPF/XAML migration.
- Test framework setup.

## Verification

Build:
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

Manual:
1. Start the app.
2. Select an image, click "建立模板" → confirm `.shm` file created.
3. Select a (possibly different) image, select the `.shm` template, click "執行模板匹配" → confirm Found/Row/Column/Angle/Score displayed.
4. Verify with `000.png` and `002.png` from `data/images`.
