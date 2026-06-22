using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.ImageQuality;
using FlashMeasurementSystem.Domain.LineFitting;
using FlashMeasurementSystem.Domain.TemplateMatching;
using FlashMeasurementSystem.Halcon.EdgeDetection;
using FlashMeasurementSystem.Halcon.ImageQuality;
using FlashMeasurementSystem.Halcon.LineFitting;
using FlashMeasurementSystem.Halcon.TemplateMatching;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Halcon.CircleFitting;
using FlashMeasurementSystem.Domain.DistanceMeasurement;
using FlashMeasurementSystem.Halcon.DistanceMeasurement;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using FlashMeasurementSystem.Halcon.AngleMeasurement;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.Calibration;
using FlashMeasurementSystem.Halcon.CoordinateSystem;
using FlashMeasurementSystem.Infrastructure.Roi;
using FlashMeasurementSystem.Infrastructure.Tolerance;
using FlashMeasurementSystem.Infrastructure.Calibration;
using HalconDotNet;
namespace FlashMeasurementSystem
{
    public partial class MainWindow : Form
    {
        private HWindowControlHelper _imageHelper;
        private OpenFileDialog _openImageDialog;

        private readonly HalconTemplateManager _templateManager = new HalconTemplateManager();
        private readonly HalconTemplateMatcher _templateMatcher = new HalconTemplateMatcher();
        private readonly HalconImageQualityChecker _iqc = new HalconImageQualityChecker();
        private readonly HalconEdgeDetector _edgeDetector = new HalconEdgeDetector();
        private readonly HalconLineFitter _lineFitter = new HalconLineFitter();
        private readonly HalconCircleFitter _circleFitter = new HalconCircleFitter();
        private readonly HalconDistanceMeasurer _distanceMeasurer = new HalconDistanceMeasurer();
        private readonly HalconAngleMeasurer _angleMeasurer = new HalconAngleMeasurer();
        private EdgeDetectionRoi _latestEdgeRoi;
        private EdgeResult _latestEdgeResult;
        private LineFittingResult _latestLineFittingResult;
        private CircleFittingResult _latestCircleFittingResult;
        private bool _updatingEdgeRoiControls;

        // M3c-1：配方執行（Stage A：載入 + 設參考姿態 + 轉換並繪製跟隨工件的 ROI）
        private readonly HalconCoordinateMapper _coordinateMapper = new HalconCoordinateMapper();
        private readonly RecipeStore _recipeStore = new RecipeStore();
        private Recipe _loadedRecipe;
        private string _loadedRecipePath;
        private OpenFileDialog _openRecipeDialog;
        private double _lastMatchRow, _lastMatchCol, _lastMatchAngleDeg;
        private bool _hasMatch;
        // B1：配方執行引擎與其相依（量測 + 公差判定 + 校正載入）
        private readonly ToleranceJudger _judger = new ToleranceJudger();
        private readonly CalibrationStore _calibrationStore = new CalibrationStore();
        private RecipeRunner _recipeRunner;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            NormalizeTemplateMatchingLayout();

            _imageHelper = new HWindowControlHelper(hWindowControl);
            _imageHelper.MouseMoved += OnImageMouseMoved;
            _imageHelper.RoiSelected += OnImageRoiSelected;

            // 配方執行引擎：以既有 adapters 注入（邊緣 + 圓/線擬合 + 公差 + 座標映射）。
            _recipeRunner = new RecipeRunner(_edgeDetector, _circleFitter, _lineFitter, _distanceMeasurer, _angleMeasurer, _judger, _coordinateMapper);

            // 三個下拉的選項由 designer 以 Items.AddRange 填入，但沒有設預設選取，
            // 導致畫面顯示空白、且 RunEdgeDetectionButton_Click 讀 SelectedItem.ToString()
            // 會 NullReferenceException。這裡在程式啟動時補上預設選取（抗 designer 重生成）。
            EnsureComboDefault(_edgePolarityCombo);        // 預設 "all"
            EnsureComboDefault(_edgeSelectorCombo);        // 預設 "all"
            EnsureComboDefault(_edgeSubpixelMethodCombo);  // 預設 "parabolic"
            EnsureComboDefault(_edgeMeasureModeCombo);     // 預設 "single_edge"
            EnsureComboDefault(_edgeInterpolationCombo);   // 預設 "nearest_neighbor"

            _edgeAngleNumeric.ValueChanged += OnEdgeRoiNumericChanged;
            _edgeScanLengthNumeric.ValueChanged += OnEdgeRoiNumericChanged;
            _edgeRoiWidthNumeric.ValueChanged += OnEdgeRoiNumericChanged;

            // Distance Measurement 的兩個下拉同樣沒設預設選取。contourModeCombo 尤其關鍵：
            // MeasureDistanceButton_Click 會無條件讀 contourModeCombo.SelectedItem.ToString()，
            // 若為空白則任何 Measure 都會 NullReferenceException。
            EnsureComboDefault(measurementTypeCombo);      // 預設 "PointToPoint"
            EnsureComboDefault(contourModeCombo);          // 預設 "point_to_point"
            EnsureComboDefault(angleModeCombo);            // 預設 "line_to_line"

            // 校正 + 配方按鈕（M3b/M3c）：程式碼動態加在 Measurement 分頁頂端的工具列，不動 Designer.cs。
            // measurementBox 為 Dock=Fill 且先加入，故此 Top 工具列置前後可佔頂端、GroupBox 填其餘。
            var topToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Height = 34,
                AutoSize = false
            };
            var calibButton = new Button { Text = "校正...", Width = 64, Height = 26 };
            calibButton.Click += OpenCalibrationDialog;
            var loadRecipeButton = new Button { Text = "Load Recipe", Width = 84, Height = 26 };
            loadRecipeButton.Click += LoadRecipeButton_Click;
            var setRefButton = new Button { Text = "Set Ref", Width = 64, Height = 26 };
            setRefButton.Click += SetRefPoseButton_Click;
            var runRecipeButton = new Button { Text = "Run Recipe", Width = 84, Height = 26 };
            runRecipeButton.Click += RunRecipeButton_Click;
            topToolbar.Controls.Add(calibButton);
            topToolbar.Controls.Add(loadRecipeButton);
            topToolbar.Controls.Add(setRefButton);
            topToolbar.Controls.Add(runRecipeButton);
            measurementTabPage.Controls.Add(topToolbar);
            topToolbar.BringToFront();

            _openImageDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All Files|*.*",
                Title = "Select Image"
            };

            LoadTemplateList();

            try
            {
                HOperatorSet.GetSystem("version", out HTuple version);
                Text = $"Flash Measurement System - Template Matching (Halcon {version.S})";
            }
            catch (HalconException)
            {
                Text = "Flash Measurement System - Template Matching (Halcon unavailable)";
            }
        }

        private void NormalizeTemplateMatchingLayout()
        {
            templateMatchingBox.SuspendLayout();
            try
            {
                templateMatchingBox.Controls.Clear();

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 6,
                    Padding = new Padding(8, 18, 8, 8)
                };

                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

                loadTestImageButton.Dock = DockStyle.Fill;
                loadTestImageButton.Margin = new Padding(0, 0, 0, 4);

                templateLabel.Dock = DockStyle.Fill;
                templateLabel.TextAlign = ContentAlignment.MiddleLeft;
                templateLabel.Margin = new Padding(0, 0, 0, 0);

                templateFileCombo.Dock = DockStyle.Fill;
                templateFileCombo.Margin = new Padding(0, 2, 0, 4);

                var scorePanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4) };
                minScoreLabel.AutoSize = true;
                minScoreLabel.Location = new Point(0, 6);
                minScoreNumeric.Location = new Point(70, 2);
                minScoreNumeric.Width = 80;
                scorePanel.Controls.Add(minScoreNumeric);
                scorePanel.Controls.Add(minScoreLabel);

                runMatchingButton.Dock = DockStyle.Fill;
                runMatchingButton.Margin = new Padding(0, 0, 0, 6);

                matchResultTextBox.Dock = DockStyle.Fill;
                matchResultTextBox.Margin = new Padding(0, 0, 0, 0);
                matchResultTextBox.ScrollBars = ScrollBars.Vertical;

                layout.Controls.Add(loadTestImageButton, 0, 0);
                layout.Controls.Add(templateLabel, 0, 1);
                layout.Controls.Add(templateFileCombo, 0, 2);
                layout.Controls.Add(scorePanel, 0, 3);
                layout.Controls.Add(runMatchingButton, 0, 4);
                layout.Controls.Add(matchResultTextBox, 0, 5);

                templateMatchingBox.Controls.Add(layout);
            }
            finally
            {
                templateMatchingBox.ResumeLayout(true);
            }
        }

        private void LoadTemplateList()
        {
            templateFileCombo.Items.Clear();

            string templatesDir = FindTemplatesDirectory();
            if (templatesDir == null || !Directory.Exists(templatesDir))
            {
                templateFileCombo.Items.Add(new FileItemWrapper("(no templates directory)"));
                return;
            }

            var shmFiles = Directory.GetFiles(templatesDir, "*.shm").OrderBy(Path.GetFileName).ToArray();
            if (shmFiles.Length == 0)
            {
                templateFileCombo.Items.Add(new FileItemWrapper("(no .shm files)"));
                return;
            }

            foreach (var f in shmFiles) templateFileCombo.Items.Add(new FileItemWrapper(f));
            templateFileCombo.SelectedIndex = 0;
        }

        private string FindTemplatesDirectory()
        {
            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "FlashMeasurementSystem.sln")))
                    return Path.Combine(current.FullName, "data", "templates");
                current = current.Parent;
            }
            return null;
        }

        private void LoadRefImageButton_Click(object sender, EventArgs e)
        {
            if (_openImageDialog.ShowDialog() != DialogResult.OK) return;
            LoadAndDisplayImage(_openImageDialog.FileName);
        }

        private void LoadTestImageButton_Click(object sender, EventArgs e)
        {
            if (_openImageDialog.ShowDialog() != DialogResult.OK) return;
            LoadAndDisplayImage(_openImageDialog.FileName);
        }

        private void LoadAndDisplayImage(string path)
        {
            try
            {
                var image = new HImage(path);
                _imageHelper.DisplayImage(image);
                ClearFittingState();
                ClearResultDisplays();
                HOperatorSet.GetImageSize(image, out HTuple w, out HTuple h);
                imageSizeLabel.Text = $"Size: {w.I} x {h.I}";
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"Failed to load image: {ex.Message}", "Error");
            }
        }

        // 換圖後清掉所有結果顯示。ClearFittingState 只清擬合狀態欄位與兩個 fitting label，
        // grid / 邊緣狀態列 / 匹配結果 / IQC 結果若不清，會殘留上一張影像的結果
        // （例如狀態列仍顯示 "PASS | 1234 edge(s)"），操作者極易誤判新圖已檢測過。
        private void ClearResultDisplays()
        {
            RestoreDefaultEdgeGridColumns();
            _edgeResultsGrid.Rows.Clear();
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            matchResultTextBox.Text = string.Empty;
            iqcResultLabel.Text = "Not tested";
            iqcResultLabel.ForeColor = Color.Black;
            measureResultLabel.Text = string.Empty;
        }

        private void RoiModeCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (roiModeCheck.Checked && _edgeDrawRoiCheck != null)
            {
                _edgeDrawRoiCheck.Checked = false;
            }

            _imageHelper.IsRoiMode = roiModeCheck.Checked;
        }

        private void ClearRoiButton_Click(object sender, EventArgs e)
        {
            // 必須用 ClearRoi()（清 ROI 座標 + overlay）而非 ClearOverlay()（只清視覺框）。
            // 否則藍框消失但 HasRoi 仍為 true，之後 Create Template 會用隱形的舊 ROI 建模。
            _imageHelper.ClearRoi();
            roiModeCheck.Checked = false;
            _edgeDrawRoiCheck.Checked = false;
        }

        private void CreateTemplateButton_Click(object sender, EventArgs e)
        {
            if (_imageHelper.CurrentImage == null)
            {
                MessageBox.Show("Please load a reference image first.", "Info");
                return;
            }

            try
            {
                var parameters = new TemplateCreationParameters
                {
                    AngleStart = (double)angleStartNumeric.Value,
                    AngleExtent = (double)angleExtentNumeric.Value,
                    PyramidLevel = (int)pyramidNumeric.Value
                };

                HRegion templateRegion;
                if (_imageHelper.HasRoi)
                {
                    var roi = _imageHelper.GetCurrentRoi();
                    templateRegion = new HRegion(roi.Row1, roi.Col1, roi.Row2, roi.Col2);
                }
                else
                {
                    templateRegion = _imageHelper.CurrentImage.GetDomain();
                }

                string modelPath = Path.Combine(
                    FindTemplatesDirectory() ?? Path.GetTempPath(),
                    $"template_{DateTime.Now:yyyyMMdd_HHmmss}.shm");

                _templateManager.CreateAndSave(_imageHelper.CurrentImage, templateRegion, modelPath, parameters);

                if (templateRegion != null)
                {
                    HOperatorSet.SmallestRectangle1(templateRegion,
                        out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
                    _imageHelper.Redraw();
                    _imageHelper.Annotator.DrawRoiRectangle(r1, c1, r2, c2);
                    _imageHelper.Annotator.DrawText("Model saved", (int)r1 - 5, (int)c1);
                }

                templateRegion?.Dispose();
                LoadTemplateList();
                matchResultTextBox.Text = $"Template created: {modelPath}";
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"Halcon error: {ex.Message}", "Error");
            }
        }

        private void RunMatchingButton_Click(object sender, EventArgs e)
        {
            if (_imageHelper.CurrentImage == null)
            {
                MessageBox.Show("Please load a test image first.", "Info");
                return;
            }

            var templateFile = templateFileCombo.SelectedItem as FileItemWrapper;
            if (templateFile == null || !templateFile.IsRealFile)
            {
                MessageBox.Show("Please select a valid template file.", "Info");
                return;
            }

            try
            {
                var parameters = new TemplateMatchingParameters
                {
                    MinScore = (double)minScoreNumeric.Value
                };

                _templateMatcher.LoadModel(templateFile.FullPath);
                var result = _templateMatcher.FindMatches(_imageHelper.CurrentImage, null, parameters);

                if (result.Found)
                {
                    var capturedRow = result.Row;
                    var capturedCol = result.Column;
                    var capturedAngle = result.AngleDeg;
                    var capturedScore = result.Score;

                    // M3c-1：保存當前匹配姿態，供配方執行的 ROI 座標轉換使用。
                    _lastMatchRow = result.Row;
                    _lastMatchCol = result.Column;
                    _lastMatchAngleDeg = result.AngleDeg;
                    _hasMatch = true;

                    _imageHelper.SetPersistentOverlayAction(() =>
                    {
                        using (var contour = _templateMatcher.GetMatchContour(capturedRow, capturedCol, capturedAngle))
                        {
                            _imageHelper.Annotator.DrawMatchContour(contour, capturedRow, capturedCol, capturedAngle, capturedScore);
                        }
                    });
                    matchResultTextBox.Text = string.Join(Environment.NewLine, new[]
                    {
                        $"Found: {result.Found}",
                        $"Row: {result.Row:F4}",
                        $"Col: {result.Column:F4}",
                        $"Angle: {result.AngleDeg:F2}°",
                        $"Score: {result.Score:F4}",
                        result.Message
                    });
                }
                else
                {
                    _imageHelper.ClearOverlay();
                    matchResultTextBox.Text = result.Message;
                }
            }
            catch (HalconException ex)
            {
                _imageHelper.ClearOverlay();
                matchResultTextBox.Text = $"Matching failed: {ex.Message}";
            }
        }

        private void RunIqcButton_Click(object sender, EventArgs e)
        {
            if (_imageHelper.CurrentImage == null)
            {
                MessageBox.Show("Please load an image first.", "Info");
                return;
            }

            try
            {
                var result = _iqc.Check(_imageHelper.CurrentImage, ImageQualityThresholds.Default());
                iqcResultLabel.Text = result.Pass
                    ? $"PASS | Mean:{result.MeanBrightness:F1} Sat:{result.SaturationRatio:F2}% Blur:{result.BlurScore:F1} Contrast:{result.Contrast:F1}"
                    : $"FAIL | {result.Message}";
                iqcResultLabel.ForeColor = result.Pass ? System.Drawing.Color.Green : System.Drawing.Color.Red;
            }
            catch (HalconException ex)
            {
                iqcResultLabel.Text = $"IQC error: {ex.Message}";
                iqcResultLabel.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void RunEdgeDetectionButton_Click(object sender, EventArgs e)
        {
            if (_imageHelper.CurrentImage == null)
            {
                SetEdgeStatus(false, "Please load an image first.");
                return;
            }

            if (!_imageHelper.HasRoi)
            {
                SetEdgeStatus(false, "Please draw an ROI region first.");
                return;
            }

            try
            {
                EdgeDetectionRoi roi = CreateEdgeDetectionRoi(_imageHelper.GetCurrentRoi());
                EdgeDetectionParameters parameters = CreateEdgeDetectionParameters();
                EdgeResult result = _edgeSubPixRadio.Checked
                    ? _edgeDetector.DetectEdgesSubPix(_imageHelper.CurrentImage, roi, parameters)
                    : _edgeDetector.DetectEdges(_imageHelper.CurrentImage, roi, parameters);

                if (!_edgeSubPixRadio.Checked && parameters.MeasureMode == "edge_pair")
                {
                    BindEdgePairResult(result);
                }
                else
                {
                    RestoreDefaultEdgeGridColumns();
                    BindEdgeResult(result);
                }
                _latestEdgeRoi = roi;
                _latestEdgeResult = result;
                _latestLineFittingResult = null;
                _latestCircleFittingResult = null;
                UpdateLineFittingResult(null);
                UpdateCircleFittingResult(null);
                ShowFittingOverlay();
            }
            catch (HalconException ex)
            {
                SetEdgeStatus(false, "Edge detection failed [Halcon " + ex.GetErrorCode() + "]: " + ex.Message);
            }
            catch (Exception ex)
            {
                // 任何 .NET 未預期例外（UI thread 衝突、null reference、HObject 生命週期等）
                // 都吞進來，避免 leak 到 WinForms 主訊息迴圈導致 unhandled exception dialog。
                SetEdgeStatus(false, "Edge detection failed (unexpected " + ex.GetType().Name + "): " + ex.Message);
            }
        }

        private void ClearEdgeDetectionButton_Click(object sender, EventArgs e)
        {
            RestoreDefaultEdgeGridColumns();
            _edgeResultsGrid.Rows.Clear();
            _imageHelper.ClearRoi();
            // ClearRoi() 內部把 IsRoiMode 設為 false，兩個 ROI checkbox 都要同步，
            // 否則 Inspection 分頁的 roiModeCheck 仍顯示勾選、實際卻已不能畫 ROI。
            _edgeDrawRoiCheck.Checked = false;
            roiModeCheck.Checked = false;
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            ClearFittingState();
        }

        private void FitLineButton_Click(object sender, EventArgs e)
        {
            if (_latestEdgeResult == null || _latestEdgeResult.EdgePoints == null)
            {
                UpdateLineFittingResult(new LineFittingResult { ErrorMessage = "請先執行邊緣檢測" });
                return;
            }

            try
            {
                LineFittingResult result = _lineFitter.FitLine(_latestEdgeResult.EdgePoints, LineFittingParameters.Default());
                _latestLineFittingResult = result;
                UpdateLineFittingResult(result);

                if (result.Success)
                {
                    ShowFittingOverlay();
                }
            }
            catch (HalconException ex)
            {
                UpdateLineFittingResult(new LineFittingResult
                {
                    ErrorMessage = "直線擬合失敗 [Halcon " + ex.GetErrorCode() + "]: " + ex.Message
                });
            }
            catch (Exception ex)
            {
                UpdateLineFittingResult(new LineFittingResult
                {
                    ErrorMessage = "直線擬合失敗 (unexpected " + ex.GetType().Name + "): " + ex.Message
                });
            }

        }

        private void FitCircleButton_Click(object sender, EventArgs e)
        {
            if (_latestEdgeResult == null || _latestEdgeResult.EdgePoints == null)
            {
                UpdateCircleFittingResult(new CircleFittingResult { ErrorMessage = "請先執行邊緣檢測" });
                return;
            }

            try
            {
                CircleFittingResult result = _circleFitter.FitCircle(_latestEdgeResult.EdgePoints, CircleFittingParameters.Default());
                _latestCircleFittingResult = result;
                UpdateCircleFittingResult(result);

                if (result.Success)
                {
                    ShowFittingOverlay();
                }
            }
            catch (HalconException ex)
            {
                UpdateCircleFittingResult(new CircleFittingResult
                {
                    ErrorMessage = "圓擬合失敗 [Halcon " + ex.GetErrorCode() + "]: " + ex.Message
                });
            }
            catch (Exception ex)
            {
                UpdateCircleFittingResult(new CircleFittingResult
                {
                    ErrorMessage = "圓擬合失敗 (unexpected " + ex.GetType().Name + "): " + ex.Message
                });
            }
        }

        private void EdgeDrawRoiCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (_imageHelper != null)
            {
                if (_edgeDrawRoiCheck.Checked)
                {
                    roiModeCheck.Checked = false;
                }

                _imageHelper.IsRoiMode = _edgeDrawRoiCheck.Checked;
            }
        }

        private EdgeDetectionParameters CreateEdgeDetectionParameters()
        {
            // MeasureMode applies to the MeasurePos path; EdgesSubPix keeps using SubpixelMethod.
            return new EdgeDetectionParameters
            {
                Sigma = (double)_edgeSigmaNumeric.Value,
                Threshold = (double)_edgeThresholdNumeric.Value,
                Polarity = _edgePolarityCombo.SelectedItem.ToString(),
                EdgeSelector = _edgeSelectorCombo.SelectedItem.ToString(),
                Interpolation = _edgeInterpolationCombo.SelectedItem.ToString(),
                MeasureMode = _edgeMeasureModeCombo.SelectedItem.ToString()
            };
        }

        private EdgeDetectionRoi CreateEdgeDetectionRoi(RegionInfo region)
        {
            return CreateEdgeDetectionRoiFromNumeric(region);
        }

        private EdgeDetectionRoi CreateEdgeDetectionRoiFromNumeric(RegionInfo region)
        {
            double centerRow = (region.Row1 + region.Row2) / 2.0;
            double centerCol = (region.Col1 + region.Col2) / 2.0;
            double angleRad = (double)_edgeAngleNumeric.Value * Math.PI / 180.0;
            double length1 = (double)_edgeScanLengthNumeric.Value / 2.0;
            double length2 = (double)_edgeRoiWidthNumeric.Value / 2.0;
            return EdgeDetectionRoi.FromCenter(centerRow, centerCol, length1, length2, angleRad);
        }

        private void OnEdgeRoiNumericChanged(object sender, EventArgs e)
        {
            if (_updatingEdgeRoiControls || _imageHelper == null || _imageHelper.CurrentImage == null || !_imageHelper.HasRoi)
            {
                return;
            }

            RegionInfo currentRoi = _imageHelper.GetCurrentRoi();
            if (currentRoi == null)
            {
                return;
            }

            _latestEdgeRoi = CreateEdgeDetectionRoiFromNumeric(currentRoi);
            _latestEdgeResult = null;
            _latestLineFittingResult = null;
            _latestCircleFittingResult = null;
            UpdateLineFittingResult(null);
            UpdateCircleFittingResult(null);
            RestoreDefaultEdgeGridColumns();
            _edgeResultsGrid.Rows.Clear();
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            ShowFittingOverlay();
        }

        // 若下拉有選項但尚未選取，預設選第一項。避免空白顯示與 SelectedItem 為 null 的崩潰。
        private static void EnsureComboDefault(ComboBox combo)
        {
            if (combo != null && combo.Items.Count > 0 && combo.SelectedIndex < 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        // Subpix 模式單次可能回上千個 EdgePoint。DataGridView 無 virtualization、HALCON
        // DrawCross 每點一次同步繪製 — 兩者超過幾百筆都會明顯卡。設上限保護 UI。
        private const int MaxGridRows = 500;
        private const int MaxOverlayCrosses = 200;

        private void BindEdgeResult(EdgeResult result)
        {
            _edgeResultsGrid.Rows.Clear();
            int total = result.EdgePoints.Count;
            int displayCount = Math.Min(total, MaxGridRows);
            for (int i = 0; i < displayCount; i++)
            {
                EdgePoint edge = result.EdgePoints[i];
                _edgeResultsGrid.Rows.Add(
                    i + 1,
                    edge.Row.ToString("F2", CultureInfo.InvariantCulture),
                    edge.Column.ToString("F2", CultureInfo.InvariantCulture),
                    edge.Amplitude.ToString("F1", CultureInfo.InvariantCulture),
                    edge.Distance.ToString("F2", CultureInfo.InvariantCulture));
            }

            string mode = _edgeSubPixRadio.Checked ? "EdgesSubPix" : "MeasurePos";
            string message;
            if (result.Success)
            {
                message = total > displayCount
                    ? string.Format("{0}: {1} edge(s) (showing first {2})", mode, total, displayCount)
                    : string.Format("{0}: {1} edge(s)", mode, total);
            }
            else
            {
                // 紅字標籤只放短摘要（避免長訊息擠掉 grid 與其他資訊）。
                // 完整診斷（measure_pos 限制、ROI 建議、參數描述）已寫到 data/logs/edge_detection.log。
                message = "未偵測到邊緣（完整原因見 log）";
            }
            SetEdgeStatus(result.Success, message);
        }

        private void BindEdgePairResult(EdgeResult result)
        {
            _edgeResultsGrid.Columns.Clear();
            AddEdgeGridColumn("#");
            AddEdgeGridColumn("Row1");
            AddEdgeGridColumn("Col1");
            AddEdgeGridColumn("Amp1");
            AddEdgeGridColumn("Row2");
            AddEdgeGridColumn("Col2");
            AddEdgeGridColumn("Amp2");
            AddEdgeGridColumn("IntraDist");
            AddEdgeGridColumn("InterDist");

            _edgeResultsGrid.Rows.Clear();
            int total = result.EdgePairs == null ? 0 : result.EdgePairs.Count;
            int displayCount = Math.Min(total, MaxGridRows);
            for (int i = 0; i < displayCount; i++)
            {
                EdgePair pair = result.EdgePairs[i];
                _edgeResultsGrid.Rows.Add(
                    i + 1,
                    pair.FirstRow.ToString("F2", CultureInfo.InvariantCulture),
                    pair.FirstColumn.ToString("F2", CultureInfo.InvariantCulture),
                    pair.FirstAmplitude.ToString("F1", CultureInfo.InvariantCulture),
                    pair.SecondRow.ToString("F2", CultureInfo.InvariantCulture),
                    pair.SecondColumn.ToString("F2", CultureInfo.InvariantCulture),
                    pair.SecondAmplitude.ToString("F1", CultureInfo.InvariantCulture),
                    pair.IntraDistance.ToString("F2", CultureInfo.InvariantCulture),
                    pair.InterDistance.ToString("F2", CultureInfo.InvariantCulture));
            }

            string message;
            if (result.Success)
            {
                message = total > displayCount
                    ? string.Format("MeasurePairs: {0} pair(s) (showing first {1})", total, displayCount)
                    : string.Format("MeasurePairs: {0} pair(s)", total);
            }
            else
            {
                message = result.ErrorMessage;
            }
            SetEdgeStatus(result.Success, message);
        }

        private void RestoreDefaultEdgeGridColumns()
        {
            if (_edgeResultsGrid.Columns.Count == 5
                && _edgeResultsGrid.Columns[0].HeaderText == "#"
                && _edgeResultsGrid.Columns[1].HeaderText == "Row"
                && _edgeResultsGrid.Columns[2].HeaderText == "Column"
                && _edgeResultsGrid.Columns[3].HeaderText == "Amp"
                && _edgeResultsGrid.Columns[4].HeaderText == "Dist")
            {
                return;
            }

            _edgeResultsGrid.Columns.Clear();
            AddEdgeGridColumn("#");
            AddEdgeGridColumn("Row");
            AddEdgeGridColumn("Column");
            AddEdgeGridColumn("Amp");
            AddEdgeGridColumn("Dist");
        }

        private void AddEdgeGridColumn(string headerText)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                Name = "edge" + headerText.Replace("#", "Index").Replace(" ", string.Empty) + "Column",
                ReadOnly = true
            };
            _edgeResultsGrid.Columns.Add(column);
        }

        // ─── M3c-1：配方執行（載入 + 設參考姿態 + 量測 + 公差判定）────────────
        // PlacedRoi / ToolRunResult / RecipeRunner 定義於 RecipeRunner.cs。

        private void LoadRecipeButton_Click(object sender, EventArgs e)
        {
            if (_openRecipeDialog == null)
            {
                _openRecipeDialog = new OpenFileDialog
                {
                    Filter = "Recipe (*.zcp)|*.zcp|All Files|*.*",
                    Title = "Load Recipe"
                };
                string dir = ResolveRecipesDir();
                if (Directory.Exists(dir)) _openRecipeDialog.InitialDirectory = dir;
            }

            if (_openRecipeDialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _loadedRecipe = _recipeStore.Load(_openRecipeDialog.FileName);
                _loadedRecipePath = _openRecipeDialog.FileName;
                measureResultLabel.Text = string.Format(CultureInfo.InvariantCulture,
                    "已載入配方 '{0}'（{1} 工具，SchemaVer {2}{3}）",
                    _loadedRecipe.Name, _loadedRecipe.Tools.Count, _loadedRecipe.SchemaVersion,
                    _loadedRecipe.HasReferencePose ? "，含參考姿態" : "，無參考姿態（需 Set Ref）");
            }
            catch (Exception ex)
            {
                _loadedRecipe = null;
                _loadedRecipePath = null;
                measureResultLabel.Text = "載入配方失敗: " + ex.Message;
            }
        }

        // 以「目前的模板匹配姿態」設為配方的參考姿態，並存回原檔。
        // 這定義了 ROI 的 reference frame：之後在其他影像匹配時，ROI 依當前姿態相對此參考轉換。
        private void SetRefPoseButton_Click(object sender, EventArgs e)
        {
            if (_loadedRecipe == null) { MessageBox.Show("請先載入配方 (.zcp)。", "Info"); return; }
            if (!_hasMatch) { MessageBox.Show("請先在參考影像上執行模板匹配。", "Info"); return; }

            _loadedRecipe.RefRow = _lastMatchRow;
            _loadedRecipe.RefCol = _lastMatchCol;
            _loadedRecipe.RefAngleRad = _lastMatchAngleDeg * Math.PI / 180.0;
            _loadedRecipe.HasReferencePose = true;

            try
            {
                if (!string.IsNullOrEmpty(_loadedRecipePath))
                {
                    _recipeStore.Save(_loadedRecipe, _loadedRecipePath);
                }
                measureResultLabel.Text = string.Format(CultureInfo.InvariantCulture,
                    "參考姿態已設定並存檔：Row={0:F2} Col={1:F2} Angle={2:F2}°",
                    _loadedRecipe.RefRow, _loadedRecipe.RefCol, _lastMatchAngleDeg);
            }
            catch (Exception ex)
            {
                measureResultLabel.Text = "參考姿態存檔失敗: " + ex.Message;
            }
        }

        // 執行配方（B1）：每個工具 ROI 轉到當前匹配姿態 → circle 量直徑(mm) → 公差判定 →
        // 結果表 OK/NG 上色 + 畫擬合圓。pixel size 優先用配方參考的校正檔，否則退回量測分頁數值。
        private void RunRecipeButton_Click(object sender, EventArgs e)
        {
            if (_loadedRecipe == null) { MessageBox.Show("請先載入配方 (.zcp)。", "Info"); return; }
            if (_imageHelper == null || _imageHelper.CurrentImage == null) { MessageBox.Show("請先載入影像。", "Info"); return; }
            if (_loadedRecipe.HasReferencePose && !_hasMatch)
            {
                MessageBox.Show("此配方含參考姿態，請先對目前影像執行模板匹配以取得當前工件姿態。", "Info");
                return;
            }

            // pixel size 來源（決策 A）：配方 CalibrationProfileId 有設且檔案存在 → 用校正檔；否則退回量測分頁。
            double pixelSizeUmX = (double)measurementPixelSizeXNumeric.Value;
            double pixelSizeUmY = (double)measurementPixelSizeYNumeric.Value;
            string pixelSizeSource = "量測分頁";
            if (!string.IsNullOrEmpty(_loadedRecipe.CalibrationProfileId))
            {
                try
                {
                    string calPath = System.IO.Path.Combine(ResolveCalibrationsDir(), _loadedRecipe.CalibrationProfileId + ".json");
                    if (System.IO.File.Exists(calPath))
                    {
                        CalibrationProfile prof = _calibrationStore.Load(calPath);
                        pixelSizeUmX = prof.PixelSizeUmX;
                        pixelSizeUmY = prof.PixelSizeUmY;
                        pixelSizeSource = "校正檔 " + _loadedRecipe.CalibrationProfileId;
                    }
                }
                catch
                {
                    // 載入校正檔失敗 → 沿用量測分頁數值
                }
            }

            System.Collections.Generic.List<ToolRunResult> results = _recipeRunner.Run(
                _loadedRecipe, _imageHelper.CurrentImage,
                _hasMatch, _lastMatchRow, _lastMatchCol, _lastMatchAngleDeg * Math.PI / 180.0,
                pixelSizeUmX, pixelSizeUmY);

            _imageHelper.SetPersistentOverlayAction(() =>
            {
                OverlayAnnotator an = _imageHelper.Annotator;
                // 匹配輪廓（綠），讓使用者看出 ROI 與工件的關係。
                if (_hasMatch)
                {
                    try
                    {
                        using (HObject contour = _templateMatcher.GetMatchContour(_lastMatchRow, _lastMatchCol, _lastMatchAngleDeg))
                        {
                            an.DrawMatchContour(contour, _lastMatchRow, _lastMatchCol, _lastMatchAngleDeg, 1.0);
                        }
                    }
                    catch (HalconException) { /* 無模板載入則略過輪廓 */ }
                }

                var rows = new System.Collections.Generic.List<OverlayResultRow>();
                foreach (ToolRunResult r in results)
                {
                    // 元素工具（line/circle）有 ROI → 畫框；複合工具（distance）r.Roi 為 null。
                    if (r.Roi != null)
                    {
                        an.DrawRectangle2(r.Roi.Row, r.Roi.Col, r.Roi.AngleRad, r.Roi.Length1, r.Roi.Length2, "orange");
                        an.DrawText(r.Name ?? string.Empty, (int)r.Roi.Row, (int)r.Roi.Col, "orange");
                    }

                    if (r.Measured && r.ToolType == "circle")
                    {
                        string circleColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                        an.DrawCircle(r.FitCenterRow, r.FitCenterCol, r.FitRadiusPx, circleColor);
                    }
                    else if (r.Measured && r.ToolType == "line")
                    {
                        string lineColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                        an.DrawLine(r.LineRow1, r.LineCol1, r.LineRow2, r.LineCol2, lineColor);
                    }
                    else if (r.Measured && r.ToolType == "distance")
                    {
                        an.DrawDistance(r.DistRow1, r.DistCol1, r.DistRow2, r.DistCol2, r.ValueText, r.IsOk);
                    }
                    else if (r.Measured && r.ToolType == "angle")
                    {
                        // 弧由線A端點方向(起點)起，延伸角度=AngleDeg(銳角)。
                        double extent = r.AngleDeg * Math.PI / 180.0;
                        an.DrawAngle(r.AngleCenterRow, r.AngleCenterCol, r.AngleRadiusPx, r.AngleStartRad, extent, r.ValueText, r.IsOk);
                    }

                    rows.Add(new OverlayResultRow { Name = r.Name, ValueText = r.ValueText, IsOk = r.IsOk });
                }
                an.DrawResultTable(rows);
            });

            int okCount = 0, ngCount = 0;
            foreach (ToolRunResult r in results)
            {
                if (r.IsOk == true) okCount++;
                else if (r.IsOk == false) ngCount++;
            }
            measureResultLabel.Text = string.Format(CultureInfo.InvariantCulture,
                "配方 '{0}'：{1} 工具，OK {2} / NG {3}（pixel size：{4}）",
                _loadedRecipe.Name, results.Count, okCount, ngCount, pixelSizeSource);
        }

        // 由 app base directory 往上找 .sln，定位 data/calibrations（與 CalibrationDialog 同邏輯）。
        private static string ResolveCalibrationsDir()
        {
            try
            {
                var current = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (current != null)
                {
                    if (System.IO.File.Exists(System.IO.Path.Combine(current.FullName, "FlashMeasurementSystem.sln")))
                    {
                        return System.IO.Path.Combine(current.FullName, "data", "calibrations");
                    }
                    current = current.Parent;
                }
            }
            catch
            {
                // 退回 base directory
            }
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "calibrations");
        }

        private static string ResolveRecipesDir()
        {
            try
            {
                var current = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (current != null)
                {
                    if (System.IO.File.Exists(System.IO.Path.Combine(current.FullName, "FlashMeasurementSystem.sln")))
                    {
                        return System.IO.Path.Combine(current.FullName, "data", "recipes");
                    }
                    current = current.Parent;
                }
            }
            catch
            {
                // 退回 base directory
            }
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "recipes");
        }

        // 開啟簡易 pixel size 校正對話框（M3b / 4.10b）。帶入目前影像尺寸供 FOV 計算。
        private void OpenCalibrationDialog(object sender, EventArgs e)
        {
            int width = 0, height = 0;
            if (_imageHelper != null && _imageHelper.CurrentImage != null)
            {
                try
                {
                    HOperatorSet.GetImageSize(_imageHelper.CurrentImage, out HTuple w, out HTuple h);
                    width = w.I;
                    height = h.I;
                }
                catch (HalconException)
                {
                    // 取不到尺寸就用 0（對話框會以 Min=1 處理），不阻擋校正
                }
            }

            using (var dialog = new CalibrationDialog(width, height))
            {
                dialog.ShowDialog(this);
            }
        }

        // 單一 overlay slot 的共用底層：把目前有效的偵測/擬合狀態（ROI 框 + 邊緣十字 +
        // 直線/圓擬合）全部重畫。直接讀 _latest* 欄位，確保任何功能設定 overlay 時都不會
        // 抹掉其他仍然有效的圖層（先 Fit Circle 再 Fit Line 時圓消失、label 卻還顯示
        // "Circle OK" 這類畫面與狀態矛盾）。
        private void DrawFittingLayers(OverlayAnnotator an)
        {
            EdgeDetectionRoi roi = _latestEdgeRoi;
            if (roi != null && _latestEdgeResult != null)
            {
                // 邊緣檢測後：畫旋轉的量測 ROI (Rectangle2)
                an.DrawRectangle2(roi.CenterRow, roi.CenterCol, roi.AngleRad, roi.Length1, roi.Length2, "blue");
            }
            else if (roi != null)
            {
                // 尚未執行邊緣檢測：畫出測量用 Rectangle2（與按下 Detect 後一致），
                // 避免因 NumericUpDown 欄位範圍限制 (Min/Max clamping) 造成
                // Detect 前後藍框尺寸不同。DrawRoiRectangle 畫的是 raw pixel 座標
                // (未受欄位 clamp)，DrawRectangle2 畫的是 _latestEdgeRoi（已受 clamp），
                // 兩者只在 Phi∈{0,±π/2} 且無 clamp 時才重合。
                an.DrawRectangle2(roi.CenterRow, roi.CenterCol, roi.AngleRad, roi.Length1, roi.Length2, "blue");
            }

            EdgeResult edges = _latestEdgeResult;
            if (edges != null && edges.EdgePoints != null && edges.EdgePoints.Count > 0)
            {
                // 等間距抽樣，最多畫 MaxOverlayCrosses 個十字。subpix 模式輪廓密集 → 用較小的
                // 十字（size=3）避免重疊糊掉；measure_pos 通常只有少數邊 → 維持 size=8 醒目。
                int total = edges.EdgePoints.Count;
                int step = total <= MaxOverlayCrosses ? 1 : (int)Math.Ceiling((double)total / MaxOverlayCrosses);
                int crossSize = _edgeSubPixRadio.Checked ? 3 : 8;
                for (int i = 0; i < total; i += step)
                {
                    EdgePoint edge = edges.EdgePoints[i];
                    an.DrawCross(edge.Row, edge.Column, crossSize, "cyan");
                }
            }

            EdgeResult edgesWithPairs = _latestEdgeResult;
            if (edgesWithPairs != null && edgesWithPairs.EdgePairs != null && edgesWithPairs.EdgePairs.Count > 0)
            {
                int totalPairs = edgesWithPairs.EdgePairs.Count;
                int pairStep = totalPairs <= MaxOverlayCrosses ? 1 : (int)Math.Ceiling((double)totalPairs / MaxOverlayCrosses);
                for (int i = 0; i < totalPairs; i += pairStep)
                {
                    EdgePair pair = edgesWithPairs.EdgePairs[i];
                    an.DrawCross(pair.FirstRow, pair.FirstColumn, 8, "cyan");
                    an.DrawCross(pair.SecondRow, pair.SecondColumn, 8, "cyan");
                    an.DrawLine(pair.FirstRow, pair.FirstColumn, pair.SecondRow, pair.SecondColumn, "yellow");
                }
            }

            LineFittingResult line = _latestLineFittingResult;
            if (line != null && line.Success)
            {
                an.DrawLine(line.Row1, line.Column1, line.Row2, line.Column2, "green");
            }

            CircleFittingResult circle = _latestCircleFittingResult;
            if (circle != null && circle.Success)
            {
                an.DrawCircle(circle.CenterRow, circle.CenterColumn, circle.RadiusPx, "green");
            }

            // 結果表（4.13b/B）：顯示目前量測值；公差判定 (IsOk) 留白，待配方流程 (M3c) 接上 OK/NG。
            System.Collections.Generic.List<OverlayResultRow> resultRows = BuildResultRows();
            if (resultRows.Count > 0)
            {
                an.DrawResultTable(resultRows);
            }
        }

        // 從目前的 _latest* 量測狀態建出結果表的列。IsOk 一律 null（此階段只顯示數值，
        // 公差 OK/NG 待 M3c 配方載入 nominal/公差後再接 ToleranceJudger）。
        private System.Collections.Generic.List<OverlayResultRow> BuildResultRows()
        {
            var rows = new System.Collections.Generic.List<OverlayResultRow>();

            EdgeResult edges = _latestEdgeResult;
            if (edges != null && edges.Success && edges.EdgePoints != null && edges.EdgePoints.Count > 0)
            {
                rows.Add(new OverlayResultRow
                {
                    Name = "Edges",
                    ValueText = edges.EdgePoints.Count + " pts",
                    IsOk = null
                });
            }

            LineFittingResult line = _latestLineFittingResult;
            if (line != null && line.Success)
            {
                rows.Add(new OverlayResultRow
                {
                    Name = "Line",
                    ValueText = string.Format(CultureInfo.InvariantCulture,
                        "Len={0:F1}px Ang={1:F2}deg", line.Length, line.AngleDeg),
                    IsOk = null
                });
            }

            CircleFittingResult circleResult = _latestCircleFittingResult;
            if (circleResult != null && circleResult.Success)
            {
                rows.Add(new OverlayResultRow
                {
                    Name = "Circle",
                    ValueText = string.Format(CultureInfo.InvariantCulture,
                        "D={0:F1}px R={1:F1}px", circleResult.DiameterPx, circleResult.RadiusPx),
                    IsOk = null
                });
            }

            return rows;
        }

        // Detect / Fit Line / Fit Circle 共用：以 DrawFittingLayers 為 persistent overlay。
        private void ShowFittingOverlay()
        {
            _imageHelper.SetPersistentOverlayAction(() => DrawFittingLayers(_imageHelper.Annotator));
        }

        private void UpdateLineFittingResult(LineFittingResult result)
        {
            if (result == null)
            {
                lineFittingResultLabel.Text = "直線擬合: 尚未執行";
                lineFittingResultLabel.ForeColor = Color.Black;
                return;
            }

            if (!result.Success)
            {
                lineFittingResultLabel.Text = "直線擬合失敗: " + result.ErrorMessage;
                lineFittingResultLabel.ForeColor = Color.Red;
                return;
            }

            lineFittingResultLabel.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Line OK | P1=({0:F2},{1:F2}) P2=({2:F2},{3:F2})\nAngle={4:F2}° Len={5:F2}px RMS={6:F4}px Pts={7}",
                result.Row1,
                result.Column1,
                result.Row2,
                result.Column2,
                result.AngleDeg,
                result.Length,
                result.ResidualRms,
                result.UsedPoints);
            lineFittingResultLabel.ForeColor = Color.Green;
        }

        private void UpdateCircleFittingResult(CircleFittingResult result)
        {
            if (result == null)
            {
                circleFittingResultLabel.Text = "圓擬合: 尚未執行";
                circleFittingResultLabel.ForeColor = Color.Black;
                return;
            }

            if (!result.Success)
            {
                circleFittingResultLabel.Text = "圓擬合失敗: " + result.ErrorMessage;
                circleFittingResultLabel.ForeColor = Color.Red;
                return;
            }

            circleFittingResultLabel.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Circle OK | C=({0:F2},{1:F2}) R={2:F2}px D={3:F2}px\nRMS={4:F4}px Round={5:F4}px Pts={6}",
                result.CenterRow,
                result.CenterColumn,
                result.RadiusPx,
                result.DiameterPx,
                result.ResidualRms,
                result.Roundness,
                result.UsedPoints);
            circleFittingResultLabel.ForeColor = Color.Green;
        }

        private void ClearFittingState()
        {
            _latestEdgeRoi = null;
            _latestEdgeResult = null;
            _latestLineFittingResult = null;
            _latestCircleFittingResult = null;
            UpdateLineFittingResult(null);
            UpdateCircleFittingResult(null);
        }

        private void SetEdgeStatus(bool success, string message)
        {
            _edgeStatusLabel.Text = success ? "PASS | " + message : "FAIL | " + message;
            _edgeStatusLabel.ForeColor = success ? Color.Green : Color.Red;
        }

        private void OnImageMouseMoved(double row, double col)
        {
            coordLabel.Text = $"Row: {row:F1}  Col: {col:F1}";
        }

        private void OnImageRoiSelected(double row1, double col1, double row2, double col2)
        {
            EdgeDetectionRoi roi = EdgeDetectionRoi.FromBounds(row1, col1, row2, col2);
            _updatingEdgeRoiControls = true;
            try
            {
                _edgeScanLengthNumeric.Value = ClampNumericValue(_edgeScanLengthNumeric, (decimal)(roi.Length1 * 2.0));
                _edgeRoiWidthNumeric.Value = ClampNumericValue(_edgeRoiWidthNumeric, (decimal)(roi.Length2 * 2.0));
                _edgeAngleNumeric.Value = ClampNumericValue(_edgeAngleNumeric, (decimal)(roi.AngleRad * 180.0 / Math.PI));
            }
            finally
            {
                _updatingEdgeRoiControls = false;
            }

            _latestEdgeRoi = EdgeDetectionRoi.FromCenter(
                roi.CenterRow,
                roi.CenterCol,
                (double)_edgeScanLengthNumeric.Value / 2.0,
                (double)_edgeRoiWidthNumeric.Value / 2.0,
                (double)_edgeAngleNumeric.Value * Math.PI / 180.0);
            _latestEdgeResult = null;
            _latestLineFittingResult = null;
            _latestCircleFittingResult = null;
            UpdateLineFittingResult(null);
            UpdateCircleFittingResult(null);
            RestoreDefaultEdgeGridColumns();
            _edgeResultsGrid.Rows.Clear();
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            ShowFittingOverlay();
        }

        private static decimal ClampNumericValue(NumericUpDown numeric, decimal value)
        {
            if (value < numeric.Minimum) return numeric.Minimum;
            if (value > numeric.Maximum) return numeric.Maximum;
            return value;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _templateMatcher.Dispose();
            _imageHelper?.Dispose();
            base.OnFormClosed(e);
        }

        private sealed class FileItemWrapper
        {
            private readonly string _fullPath;
            public FileItemWrapper(string fullPath)
            {
                _fullPath = fullPath;
                IsRealFile = File.Exists(fullPath);
            }
            public string FullPath => _fullPath;
            public bool IsRealFile { get; }
            public override string ToString() => IsRealFile ? Path.GetFileName(_fullPath) : _fullPath;
        }

        private void MeasurementTypeCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (measurementTypeCombo.SelectedItem == null) return;
            string type = measurementTypeCombo.SelectedItem.ToString();
            contourModeCombo.Enabled = type == "ContourMaxMin";
            // 量測前在結果區顯示該 type 的座標輸入格式，避免餵錯行數/語意。
            measureResultLabel.Text = GetMeasurementFormatHint(type);
        }

        // 各 measurement type 需要的座標輸入格式（每行 row,col）。
        private static string GetMeasurementFormatHint(string type)
        {
            switch (type)
            {
                case "PointToPoint":
                    return "格式（每行 row,col）：\r\n  行1：點 1\r\n  行2：點 2";
                case "PointToLine":
                    return "格式（每行 row,col）：\r\n  行1：點\r\n  行2：線端點 1\r\n  行3：線端點 2";
                case "LineToLine":
                    return "格式（每行 row,col）：\r\n  行1-2：線 1 的兩端點\r\n  行3-4：線 2 的兩端點";
                case "CircleToCircle":
                    return "格式（每行 row,col）：\r\n  行1：圓 1 圓心\r\n  行2：圓 2 圓心";
                case "ContourMaxMin":
                    return "格式（每行 row,col）：\r\n  contour 1 各點…\r\n  (空一行分隔)\r\n  contour 2 各點…";
                default:
                    return string.Empty;
            }
        }

        // 把最近一次成功的 Line 擬合結果（兩端點）附加到座標輸入框，免去手動抄座標。
        private void AppendLineButton_Click(object sender, EventArgs e)
        {
            LineFittingResult line = _latestLineFittingResult;
            if (line == null || !line.Success)
            {
                measureResultLabel.Text = "尚無成功的 Line 擬合結果可帶入（請先在 Edge Detection 分頁按 Fit Line）。";
                return;
            }
            AppendCoordLine(line.Row1, line.Column1);
            AppendCoordLine(line.Row2, line.Column2);
        }

        // 把最近一次成功的 Circle 擬合結果（圓心）附加到座標輸入框。
        private void AppendCircleButton_Click(object sender, EventArgs e)
        {
            CircleFittingResult circle = _latestCircleFittingResult;
            if (circle == null || !circle.Success)
            {
                measureResultLabel.Text = "尚無成功的 Circle 擬合結果可帶入（請先在 Edge Detection 分頁按 Fit Circle）。";
                return;
            }
            AppendCoordLine(circle.CenterRow, circle.CenterColumn);
        }

        // 把最近一次 Edge Detection 的所有 EdgePoints 當成一條 contour 帶入座標框。
        // 第二次按時自動以空行分隔，形成 ContourMaxMin 需要的「contour1 (空行) contour2」格式。
        // 建議搭配 EdgesSubPix 取得密集輪廓點。
        private void AppendContourButton_Click(object sender, EventArgs e)
        {
            EdgeResult edge = _latestEdgeResult;
            if (edge == null || edge.EdgePoints == null || edge.EdgePoints.Count == 0)
            {
                measureResultLabel.Text = "尚無邊緣檢測結果可帶入（請先在 Edge Detection 分頁 Detect，建議用 EdgesSubPix 取得密集輪廓點）。";
                return;
            }

            // subpix 可能上千點，用 StringBuilder 一次附加，避免逐點 AppendText 造成 UI 卡頓。
            var sb = new System.Text.StringBuilder();
            if (measurementCoordInput.TextLength > 0)
            {
                if (!measurementCoordInput.Text.EndsWith("\n", StringComparison.Ordinal))
                {
                    sb.AppendLine();
                }
                sb.AppendLine(); // 空行 = 兩條 contour 的分隔
            }
            foreach (EdgePoint p in edge.EdgePoints)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F2},{1:F2}", p.Row, p.Column));
            }
            measurementCoordInput.AppendText(sb.ToString());

            // 帶入 contour 後自動切到 ContourMaxMin 量測類型，省一步操作。
            int idx = measurementTypeCombo.Items.IndexOf("ContourMaxMin");
            if (idx >= 0)
            {
                measurementTypeCombo.SelectedIndex = idx;
            }
        }

        // 以 ParseCoordinateLine 能解析的格式 (row,col) 附加一行；自動補換行。
        private void AppendCoordLine(double row, double col)
        {
            string text = string.Format(CultureInfo.InvariantCulture, "{0:F2},{1:F2}", row, col);
            if (measurementCoordInput.TextLength > 0 &&
                !measurementCoordInput.Text.EndsWith("\n", StringComparison.Ordinal))
            {
                measurementCoordInput.AppendText(Environment.NewLine);
            }
            measurementCoordInput.AppendText(text + Environment.NewLine);
        }

        // 量測成功後，把量測元素畫在目前載入的影像上（需 Edge Detection 分頁已載入影像）。
        // 座標即 measurementCoordInput 的內容，沿用 ParseCoordinateLine 解析。
        // 把量測元素畫在影像上，並清楚標示：
        //   綠 = 參考幾何（線）、洋紅 = 點/圓心（含 label）、紅 = 真正被量測的那段距離、
        //   黃 = 距離數值文字。讓使用者一眼看出量的是哪兩個東西、距離是哪一段、值多少。
        private void DrawMeasurementOverlay(string typeName, DistanceMeasurementResult result)
        {
            if (_imageHelper == null || _imageHelper.CurrentImage == null) return;

            // ContourMaxMin 用空行分隔成兩條 contour，並標出 Min/Max 距離線，單獨處理。
            if (typeName == "ContourMaxMin")
            {
                DrawContourMeasurementOverlay(result);
                return;
            }

            var pts = new System.Collections.Generic.List<double[]>();
            string[] lines = measurementCoordInput.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string l in lines)
            {
                double[] c = ParseCoordinateLine(l);
                if (c != null) pts.Add(c);
            }

            string distText = string.Format(CultureInfo.InvariantCulture,
                "{0:F1} px / {1:F3} mm", result.DistancePx, result.DistanceMm);

            _imageHelper.SetPersistentOverlayAction(() =>
            {
                var an = _imageHelper.Annotator;
                // 先重畫偵測/擬合底層（ROI + 邊緣 + 擬合線/圓），量測標註疊在其上。
                // 否則按 Measure 的瞬間所有視覺證據被換掉，無法目視驗證量測線是否貼合擬合結果。
                DrawFittingLayers(an);
                switch (typeName)
                {
                    case "PointToPoint":
                        if (pts.Count >= 2)
                        {
                            DrawMeasurePoint(an, pts[0], "P1");
                            DrawMeasurePoint(an, pts[1], "P2");
                            an.DrawLine(pts[0][0], pts[0][1], pts[1][0], pts[1][1], "red");
                            DrawDistanceLabel(an, pts[0], pts[1], distText);
                        }
                        break;
                    case "CircleToCircle":
                        if (pts.Count >= 2)
                        {
                            DrawMeasurePoint(an, pts[0], "C1");
                            DrawMeasurePoint(an, pts[1], "C2");
                            an.DrawLine(pts[0][0], pts[0][1], pts[1][0], pts[1][1], "red");
                            DrawDistanceLabel(an, pts[0], pts[1], distText);
                        }
                        break;
                    case "PointToLine":
                        if (pts.Count >= 3)
                        {
                            // 參考線 L（綠）
                            an.DrawLine(pts[1][0], pts[1][1], pts[2][0], pts[2][1], "green");
                            an.DrawText("L", (int)((pts[1][0] + pts[2][0]) / 2), (int)((pts[1][1] + pts[2][1]) / 2), "green");
                            // 待量測的點 P（洋紅）
                            DrawMeasurePoint(an, pts[0], "P");
                            // 真正的距離 = P 到線的垂足 F（紅）
                            double[] f = PerpFoot(pts[0], pts[1], pts[2]);
                            an.DrawCross(f[0], f[1], 16, "red");
                            an.DrawLine(pts[0][0], pts[0][1], f[0], f[1], "red");
                            DrawDistanceLabel(an, pts[0], f, distText);
                        }
                        break;
                    case "LineToLine":
                        if (pts.Count >= 4)
                        {
                            an.DrawLine(pts[0][0], pts[0][1], pts[1][0], pts[1][1], "green");
                            an.DrawText("L1", (int)((pts[0][0] + pts[1][0]) / 2), (int)((pts[0][1] + pts[1][1]) / 2), "green");
                            an.DrawLine(pts[2][0], pts[2][1], pts[3][0], pts[3][1], "green");
                            an.DrawText("L2", (int)((pts[2][0] + pts[3][0]) / 2), (int)((pts[2][1] + pts[3][1]) / 2), "green");
                            // 距離 = line1 中點到 line2 的垂足（紅）—— 對平行邊即垂直間距
                            double[] m1 = { (pts[0][0] + pts[1][0]) / 2.0, (pts[0][1] + pts[1][1]) / 2.0 };
                            double[] f2 = PerpFoot(m1, pts[2], pts[3]);
                            an.DrawLine(m1[0], m1[1], f2[0], f2[1], "red");
                            DrawDistanceLabel(an, m1, f2, distText);
                        }
                        break;
                }
            });
        }

        // 角度量測 overlay（line_to_line）：兩條線（綠）+ 頂點（洋紅）+ 角度值（黃）。
        // 頂點用四點重心而非真正交點——對平行線也安全（交點可能不存在或在影像外）。
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
                double vr = (a1[0] + a2[0] + b1[0] + b2[0]) / 4.0;
                double vc = (a1[1] + a2[1] + b1[1] + b2[1]) / 4.0;
                an.DrawCross(vr, vc, 18, "magenta");
                an.DrawText(angleText, (int)vr - 16, (int)vc + 8, "yellow");
            });
        }

        // 角度量測 overlay（line_to_horizontal / line_to_vertical）：
        // 線（綠）+ 通過線1第一點的參考軸（灰）+ 角度值（黃）。
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
                if (mode == "line_to_vertical")
                    an.DrawLine(a1[0], a1[1], a1[0] + refLen, a1[1], "gray");
                else
                    an.DrawLine(a1[0], a1[1], a1[0], a1[1] + refLen, "gray");
                an.DrawCross(a1[0], a1[1], 18, "magenta");
                an.DrawText(angleText, (int)a1[0] - 16, (int)a1[1] + 8, "yellow");
            });
        }

        // ContourMaxMin 專屬 overlay：把兩條 contour 畫出來，並標出最近(Min,紅)與最遠(Max,橘)的距離線。
        private void DrawContourMeasurementOverlay(DistanceMeasurementResult result)
        {
            var c1 = new System.Collections.Generic.List<double[]>();
            var c2 = new System.Collections.Generic.List<double[]>();
            bool second = false;
            string[] lines = measurementCoordInput.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string l in lines)
            {
                if (string.IsNullOrWhiteSpace(l)) { second = true; continue; }
                double[] c = ParseCoordinateLine(l);
                if (c == null) continue;
                if (second) c2.Add(c); else c1.Add(c);
            }
            if (c1.Count == 0 || c2.Count == 0) return;

            FindNearestFarthestPair(c1, c2, out double[] na, out double[] nb, out double[] fa, out double[] fb);

            string minText = string.Format(CultureInfo.InvariantCulture,
                "Min {0:F1}px / {1:F3}mm", result.DistanceMinPx, result.DistanceMinMm);
            string maxText = string.Format(CultureInfo.InvariantCulture,
                "Max {0:F1}px / {1:F3}mm", result.DistanceMaxPx, result.DistanceMaxMm);

            _imageHelper.SetPersistentOverlayAction(() =>
            {
                var an = _imageHelper.Annotator;
                // 同 DrawMeasurementOverlay：先重畫偵測/擬合底層再疊量測標註。
                DrawFittingLayers(an);
                DrawContourPoints(an, c1, "cyan");
                DrawContourPoints(an, c2, "cyan");
                // Max（橘線）
                an.DrawLine(fa[0], fa[1], fb[0], fb[1], "orange");
                DrawDistanceLabel(an, fa, fb, maxText, "orange");
                // Min（紅線，醒目，兩端標 cross）
                an.DrawCross(na[0], na[1], 18, "red");
                an.DrawCross(nb[0], nb[1], 18, "red");
                an.DrawLine(na[0], na[1], nb[0], nb[1], "red");
                DrawDistanceLabel(an, na, nb, minText, "yellow");
            });
        }

        // 抽樣畫 contour 控制點（避免上千點全畫造成卡頓）。
        private static void DrawContourPoints(OverlayAnnotator an, System.Collections.Generic.List<double[]> c, string color)
        {
            int step = Math.Max(1, c.Count / 200);
            for (int i = 0; i < c.Count; i += step)
            {
                an.DrawCross(c[i][0], c[i][1], 6, color);
            }
        }

        // 在兩組點之間找最近與最遠的點對（像素距離；抽樣以控制計算量）。
        private static void FindNearestFarthestPair(
            System.Collections.Generic.List<double[]> c1, System.Collections.Generic.List<double[]> c2,
            out double[] nearA, out double[] nearB, out double[] farA, out double[] farB)
        {
            nearA = c1[0]; nearB = c2[0]; farA = c1[0]; farB = c2[0];
            double minD = double.MaxValue, maxD = double.MinValue;
            int s1 = Math.Max(1, c1.Count / 300);
            int s2 = Math.Max(1, c2.Count / 300);
            for (int i = 0; i < c1.Count; i += s1)
            {
                for (int j = 0; j < c2.Count; j += s2)
                {
                    double dr = c1[i][0] - c2[j][0];
                    double dc = c1[i][1] - c2[j][1];
                    double d = dr * dr + dc * dc;
                    if (d < minD) { minD = d; nearA = c1[i]; nearB = c2[j]; }
                    if (d > maxD) { maxD = d; farA = c1[i]; farB = c2[j]; }
                }
            }
        }

        private static void DrawMeasurePoint(OverlayAnnotator an, double[] p, string label)
        {
            an.DrawCross(p[0], p[1], 24, "magenta");
            an.DrawText(label, (int)p[0] - 18, (int)p[1] + 12, "magenta");
        }

        private static void DrawDistanceLabel(OverlayAnnotator an, double[] a, double[] b, string distText, string color = "yellow")
        {
            int midRow = (int)((a[0] + b[0]) / 2.0);
            int midCol = (int)((a[1] + b[1]) / 2.0);
            an.DrawText(distText, midRow - 16, midCol + 8, color);
        }

        // 點 P 到「線 A-B（視為無限延伸）」的垂足座標 [row, col]。
        private static double[] PerpFoot(double[] p, double[] a, double[] b)
        {
            double abr = b[0] - a[0];
            double abc = b[1] - a[1];
            double denom = abr * abr + abc * abc;
            if (denom < 1e-9) return new[] { a[0], a[1] };
            double t = ((p[0] - a[0]) * abr + (p[1] - a[1]) * abc) / denom;
            return new[] { a[0] + t * abr, a[1] + t * abc };
        }
        private void MeasureDistanceButton_Click(object sender, EventArgs e)
        {
            try
            {
                var parameters = new DistanceMeasurementParameters
                {
                    PixelSizeUmX = (double)measurementPixelSizeXNumeric.Value,
                    PixelSizeUmY = (double)measurementPixelSizeYNumeric.Value,
                    ContourMode = contourModeCombo.SelectedItem.ToString()
                };

                string typeName = measurementTypeCombo.SelectedItem.ToString();
                DistanceMeasurementResult result;

                switch (typeName)
                {
                    case "PointToPoint":
                        result = MeasurePointToPoint(parameters);
                        break;
                    case "PointToLine":
                        result = MeasurePointToLine(parameters);
                        break;
                    case "LineToLine":
                        result = MeasureLineToLine(parameters);
                        break;
                    case "CircleToCircle":
                        result = MeasureCircleToCircle(parameters);
                        break;
                    case "ContourMaxMin":
                        result = MeasureContourMaxMin(parameters);
                        break;
                    default:
                        measureResultLabel.Text = "Unknown measurement type.";
                        return;
                }

                if (result.Success)
                {
                    string text = string.Format(
                        CultureInfo.InvariantCulture,
                        "Distance: {0:F4} px / {1:F4} mm",
                        result.DistancePx, result.DistanceMm);
                    if (typeName == "LineToLine" || typeName == "ContourMaxMin")
                    {
                        text += string.Format(
                            CultureInfo.InvariantCulture,
                            "\r\nMin: {0:F4} mm  Max: {1:F4} mm",
                            result.DistanceMinMm, result.DistanceMaxMm);
                    }
                    measureResultLabel.Text = text;
                    DrawMeasurementOverlay(typeName, result);
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

        // 角度量測（手冊 4.7）：line_to_line 需 4 行座標（兩條線），
        // line_to_horizontal / line_to_vertical 需 2 行座標（單一條線）。
        // 座標來源與距離量測共用 measurementCoordInput（可用 Append Line 帶入擬合線）。
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

        private double[] ParseCoordinateLine(string line)
        {
            string[] parts = line.Split(',');
            if (parts.Length != 2) return null;
            if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double row)) return null;
            if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double col)) return null;
            return new double[] { row, col };
        }

        private DistanceMeasurementResult MeasurePointToPoint(DistanceMeasurementParameters parameters)
        {
            string[] lines = measurementCoordInput.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return new DistanceMeasurementResult { ErrorMessage = "Need 2 coordinate lines (row,col each)." };
            double[] p1 = ParseCoordinateLine(lines[0]);
            double[] p2 = ParseCoordinateLine(lines[1]);
            if (p1 == null || p2 == null) return new DistanceMeasurementResult { ErrorMessage = "Invalid coordinate format." };
            return _distanceMeasurer.MeasurePointToPoint(p1[0], p1[1], p2[0], p2[1], parameters);
        }

        private DistanceMeasurementResult MeasurePointToLine(DistanceMeasurementParameters parameters)
        {
            string[] lines = measurementCoordInput.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3) return new DistanceMeasurementResult { ErrorMessage = "Need 3 coordinate lines (point, line-pt1, line-pt2)." };
            double[] pt = ParseCoordinateLine(lines[0]);
            double[] l1 = ParseCoordinateLine(lines[1]);
            double[] l2 = ParseCoordinateLine(lines[2]);
            if (pt == null || l1 == null || l2 == null) return new DistanceMeasurementResult { ErrorMessage = "Invalid coordinate format." };
            return _distanceMeasurer.MeasurePointToLine(pt[0], pt[1], l1[0], l1[1], l2[0], l2[1], parameters);
        }

        private DistanceMeasurementResult MeasureLineToLine(DistanceMeasurementParameters parameters)
        {
            string[] lines = measurementCoordInput.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 4) return new DistanceMeasurementResult { ErrorMessage = "Need 4 coordinate lines (line1-pt1, line1-pt2, line2-pt1, line2-pt2)." };
            double[] a1 = ParseCoordinateLine(lines[0]);
            double[] a2 = ParseCoordinateLine(lines[1]);
            double[] b1 = ParseCoordinateLine(lines[2]);
            double[] b2 = ParseCoordinateLine(lines[3]);
            if (a1 == null || a2 == null || b1 == null || b2 == null) return new DistanceMeasurementResult { ErrorMessage = "Invalid coordinate format." };
            return _distanceMeasurer.MeasureLineToLine(a1[0], a1[1], a2[0], a2[1], b1[0], b1[1], b2[0], b2[1], parameters);
        }

        private DistanceMeasurementResult MeasureCircleToCircle(DistanceMeasurementParameters parameters)
        {
            string[] lines = measurementCoordInput.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return new DistanceMeasurementResult { ErrorMessage = "Need 2 coordinate lines (circle1 center, circle2 center)." };
            double[] c1 = ParseCoordinateLine(lines[0]);
            double[] c2 = ParseCoordinateLine(lines[1]);
            if (c1 == null || c2 == null) return new DistanceMeasurementResult { ErrorMessage = "Invalid coordinate format." };
            return _distanceMeasurer.MeasureCircleToCircle(c1[0], c1[1], c2[0], c2[1], parameters);
        }

        private DistanceMeasurementResult MeasureContourMaxMin(DistanceMeasurementParameters parameters)
        {
            // 注意：這裡必須用 None 而非 RemoveEmptyEntries —— ContourMaxMin 靠「空行」分隔
            // 兩條 contour，RemoveEmptyEntries 會把空行整個移除，導致下面的分隔偵測永遠失效、
            // 所有點被塞進 contour1。其他 Measure 方法按行數取座標才用 RemoveEmptyEntries。
            string[] lines = measurementCoordInput.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length < 2) return new DistanceMeasurementResult { ErrorMessage = "Need at least 2 points for contour. Use 2+ lines per contour separated by a blank line." };

            var contour1Points = new System.Collections.Generic.List<EdgePoint>();
            var contour2Points = new System.Collections.Generic.List<EdgePoint>();
            bool secondContour = false;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    secondContour = true;
                    continue;
                }
                double[] coord = ParseCoordinateLine(line);
                if (coord == null) return new DistanceMeasurementResult { ErrorMessage = "Invalid coordinate: " + line };
                if (!secondContour)
                    contour1Points.Add(new EdgePoint { Row = coord[0], Column = coord[1] });
                else
                    contour2Points.Add(new EdgePoint { Row = coord[0], Column = coord[1] });
            }

            if (contour1Points.Count < 2 || contour2Points.Count < 2)
                return new DistanceMeasurementResult { ErrorMessage = "Each contour needs at least 2 points." };

            try
            {
                HXLDCont cont1 = EdgePointsToContour(contour1Points);
                HXLDCont cont2 = EdgePointsToContour(contour2Points);
                return _distanceMeasurer.MeasureContourMaxMin(cont1, cont2, parameters);
            }
            catch (HalconException ex)
            {
                return new DistanceMeasurementResult { ErrorMessage = "Halcon error: " + ex.Message };
            }
        }

        private static HXLDCont EdgePointsToContour(System.Collections.Generic.IList<EdgePoint> points)
        {
            int n = points.Count;
            double[] rows = new double[n];
            double[] cols = new double[n];
            for (int i = 0; i < n; i++)
            {
                rows[i] = points[i].Row;
                cols[i] = points[i].Column;
            }
            var contour = new HXLDCont();
            contour.GenContourPolygonXld(rows, cols);
            return contour;
        }

    }
}
