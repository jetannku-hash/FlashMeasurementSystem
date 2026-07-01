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
using FlashMeasurementSystem.Domain.EllipseFitting;
using FlashMeasurementSystem.Halcon.EllipseFitting;
using FlashMeasurementSystem.Domain.RectangleFitting;
using FlashMeasurementSystem.Halcon.RectangleFitting;
using FlashMeasurementSystem.Domain.DistanceMeasurement;
using FlashMeasurementSystem.Halcon.DistanceMeasurement;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using FlashMeasurementSystem.Halcon.AngleMeasurement;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.Calibration;
using FlashMeasurementSystem.Halcon.CoordinateSystem;
using FlashMeasurementSystem.Halcon.MetrologyModel;
using FlashMeasurementSystem.Infrastructure.Roi;
using FlashMeasurementSystem.Infrastructure.Tolerance;
using FlashMeasurementSystem.Infrastructure.Calibration;
using FlashMeasurementSystem.Reporting.Csv;
using FlashMeasurementSystem.Domain.Workflow;
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
        private readonly HalconEllipseFitter _ellipseFitter = new HalconEllipseFitter();
        private readonly HalconRectangleFitter _rectangleFitter = new HalconRectangleFitter();
        private readonly HalconDistanceMeasurer _distanceMeasurer = new HalconDistanceMeasurer();
        private readonly HalconAngleMeasurer _angleMeasurer = new HalconAngleMeasurer();
        private readonly HalconMetrologyModelRunner _metrologyRunner = new HalconMetrologyModelRunner();
        private EdgeDetectionRoi _latestEdgeRoi;
        private double _editCenterRow, _editCenterCol;
        private EdgeResult _latestEdgeResult;
        private LineFittingResult _latestLineFittingResult;
        private CircleFittingResult _latestCircleFittingResult;
        private EllipseFittingResult _latestEllipseFittingResult;
        private RectangleFittingResult _latestRectangleFittingResult;
        private ArcMeasureRoi _latestArcRoi;
        private bool _updatingEdgeRoiControls;
        private bool _updatingArcControls;

        // M3c-1：配方執行（Stage A：載入 + 設參考姿態 + 轉換並繪製跟隨工件的 ROI）
        private readonly HalconCoordinateMapper _coordinateMapper = new HalconCoordinateMapper();
        private readonly RecipeStore _recipeStore = new RecipeStore();
        private Recipe _loadedRecipe;
        private string _loadedRecipePath;
        private OpenFileDialog _openRecipeDialog;
        private double _lastMatchRow, _lastMatchCol, _lastMatchAngleDeg;
        private bool _hasMatch;
        // 匹配輪廓快取：匹配姿態變更時算一次（transform_shape_model_contours），之後每次
        // pan/zoom/redraw 直接 DispObj，避免每個 redraw 都重算造成卡頓。生命週期由 RefreshMatchContour 管理。
        private HObject _matchContour;
        // B1：配方執行引擎與其相依（量測 + 公差判定 + 校正載入）
        private readonly ToleranceJudger _judger = new ToleranceJudger();
        private readonly CalibrationStore _calibrationStore = new CalibrationStore();
        private readonly CsvMeasurementReportWriter _reportWriter = new CsvMeasurementReportWriter();
        private RecipeRunner _recipeRunner;
        private MeasurementWorkflow _workflow;
        private CheckBox _skipIqcCheckBox;
        private ToolTip _toolTip;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            NormalizeTemplateMatchingLayout();
            SetupToolTips();

            _imageHelper = new HWindowControlHelper(hWindowControl);
            _imageHelper.MouseMoved += OnImageMouseMoved;
            _imageHelper.RoiSelected += OnImageRoiSelected;

            // 配方執行引擎：以既有 adapters 注入（邊緣 + 圓/線擬合 + 公差 + 座標映射）。
            _recipeRunner = new RecipeRunner(_edgeDetector, _circleFitter, _lineFitter, _distanceMeasurer, _angleMeasurer, _judger, _coordinateMapper, _metrologyRunner);
            _workflow = new MeasurementWorkflow(_iqc, _templateMatcher, _recipeRunner, _judger, _reportWriter);
            // 一鍵量測逐階段進度：StateChanged 在 UI 執行緒同步觸發，於 status bar 即時顯示。
            _workflow.StateChanged += OnWorkflowStateChanged;

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

            _arcCenterRowNumeric.ValueChanged += OnArcNumericChanged;
            _arcCenterColNumeric.ValueChanged += OnArcNumericChanged;
            _arcRadiusNumeric.ValueChanged += OnArcNumericChanged;
            _arcAnnulusNumeric.ValueChanged += OnArcNumericChanged;
            _arcAngleStartNumeric.ValueChanged += OnArcNumericChanged;
            _arcAngleExtentNumeric.ValueChanged += OnArcNumericChanged;

            // 結果表空狀態提示（第五組 #11）：無資料列時於表格中央繪製引導文字。
            _edgeResultsGrid.Paint += EdgeResultsGrid_Paint;

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
                AutoSize = true
            };
            var calibButton = new Button { Text = "校正...", Width = 64, Height = 26 };
            calibButton.Click += OpenCalibrationDialog;
            var loadRecipeButton = new Button { Text = "&Load Recipe", Width = 84, Height = 26 };
            loadRecipeButton.Click += LoadRecipeButton_Click;
            var setRefButton = new Button { Text = "&Set Ref", Width = 64, Height = 26 };
            setRefButton.Click += SetRefPoseButton_Click;
            var runRecipeButton = new Button { Text = "&Run Recipe", Width = 84, Height = 26 };
            runRecipeButton.Click += RunRecipeButton_Click;
            // 配方編輯器（M3c-2）：建/編 .zcp。Load Recipe 為執行流程入口，兩者並存。
            var editRecipeButton = new Button { Text = "&Edit Recipe", Width = 84, Height = 26 };
            editRecipeButton.Click += OpenRecipeEditor;
            var metrologyButton = new Button { Text = "Metrology Model", Width = 110, Height = 26 };
            metrologyButton.Click += OpenMetrologyModelEditor;
            var oneClickButton = new Button { Text = "一鍵量測", Width = 84, Height = 26 };
            oneClickButton.Click += OneClickMeasureButton_Click;
            _skipIqcCheckBox = new CheckBox
            {
                Text = "略過IQC",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(4, 6, 4, 0)
            };

            // Toolbar tooltips
            _toolTip.SetToolTip(calibButton, "Open pixel-size calibration dialog");
            _toolTip.SetToolTip(loadRecipeButton, "Load a measurement recipe (.zcp)");
            _toolTip.SetToolTip(setRefButton, "Set the current match pose as the recipe reference pose");
            _toolTip.SetToolTip(runRecipeButton, "Run the loaded recipe on the current image");
            _toolTip.SetToolTip(editRecipeButton, "Open recipe editor to create or modify recipes");
            _toolTip.SetToolTip(metrologyButton, "Define the 2D metrology model for the loaded recipe");
            _toolTip.SetToolTip(oneClickButton, "Run full pipeline: IQC → Match → Measure → Evaluate → Report");
            _toolTip.SetToolTip(_skipIqcCheckBox, "Skip image quality check (for testing with synthetic images)");

            topToolbar.Controls.Add(calibButton);
            topToolbar.Controls.Add(loadRecipeButton);
            topToolbar.Controls.Add(setRefButton);
            topToolbar.Controls.Add(runRecipeButton);
            topToolbar.Controls.Add(editRecipeButton);
            topToolbar.Controls.Add(metrologyButton);
            topToolbar.Controls.Add(oneClickButton);
            topToolbar.Controls.Add(_skipIqcCheckBox);
            measurementTabPage.Controls.Add(topToolbar);
            // WinForms docking 依「反 z-order」處理：最後面(SendToBack)的先 dock 佔邊，
            // 最前面(BringToFront)的後 dock 佔剩餘。故 Top 工具列要在後、Fill 內容要在前。
            topToolbar.SendToBack();
            measurementBox.BringToFront();

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

            // 初始空狀態：尚無影像/配方→顯示三步驟引導；橫幅為灰「—」。
            UpdateEmptyState();
            SetResultBanner(0, 0, false);
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

        private void SetupToolTips()
        {
            _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 600, ReshowDelay = 300, ShowAlways = true };

            // ── Inspection: Image Quality Check ──
            _toolTip.SetToolTip(runIqcButton, "Check image brightness, saturation, blur, and contrast");
            _toolTip.SetToolTip(iqcResultLabel, "Image quality check result — PASS (green) or FAIL (red)");

            // ── Inspection: Template Creation ──
            _toolTip.SetToolTip(loadRefImageButton, "Load a reference image for template creation");
            _toolTip.SetToolTip(angleStartNumeric, "Starting angle offset (degrees) for template search");
            _toolTip.SetToolTip(angleExtentNumeric, "Angle search extent (degrees, ± from start)");
            _toolTip.SetToolTip(pyramidNumeric, "Pyramid level (1–5). Higher = faster but less precise");
            _toolTip.SetToolTip(roiModeCheck, "Enable to draw a rectangular ROI region");
            _toolTip.SetToolTip(roiClearButton, "Clear the current ROI region");
            _toolTip.SetToolTip(createTemplateButton, "Create a shape model (.shm) from the drawn ROI region");

            // ── Inspection: Template Matching ──
            _toolTip.SetToolTip(loadTestImageButton, "Load a test image for template matching");
            _toolTip.SetToolTip(templateFileCombo, "Select a shape model (.shm) file");
            _toolTip.SetToolTip(minScoreNumeric, "Minimum matching score (0.0–1.0, higher = stricter)");
            _toolTip.SetToolTip(runMatchingButton, "Find the loaded template in the current image");

            // ── Edge Detection ──
            _toolTip.SetToolTip(_edgeMeasurePosRadio, "1D edge detection along scan lines (measure_pos)");
            _toolTip.SetToolTip(_edgeSubPixRadio, "Sub-pixel edge detection (edges_sub_pix)");
            _toolTip.SetToolTip(_edgeSigmaNumeric, "Gaussian smoothing sigma (higher = more noise reduction)");
            _toolTip.SetToolTip(_edgeThresholdNumeric, "Minimum edge amplitude threshold (lower = more edges detected)");
            _toolTip.SetToolTip(_edgePolarityCombo, "Edge polarity: all, positive (dark→bright), or negative (bright→dark)");
            _toolTip.SetToolTip(_edgeSelectorCombo, "Which edge(s) to return: all, first, or last");
            _toolTip.SetToolTip(_edgeSubpixelMethodCombo, "Subpixel interpolation method for edges_sub_pix");
            _toolTip.SetToolTip(_edgeRoiWidthNumeric, "ROI half-width perpendicular to scan direction");
            _toolTip.SetToolTip(_edgeScanLengthNumeric, "ROI half-length along scan direction");
            _toolTip.SetToolTip(_edgeAngleNumeric, "ROI rotation angle in degrees");
            _toolTip.SetToolTip(_edgeDrawRoiCheck, "Overlay the ROI rectangle on the image");
            _toolTip.SetToolTip(_edgeMeasureModeCombo, "Single edge or edge pair measurement");
            _toolTip.SetToolTip(_edgeInterpolationCombo, "Interpolation method for measure_pos");
            _toolTip.SetToolTip(_runEdgeDetectionButton, "Run edge detection on the current ROI");
            _toolTip.SetToolTip(_clearEdgeDetectionButton, "Clear edge detection results");
            _toolTip.SetToolTip(fitLineButton, "Fit a straight line to the detected edge points");
            _toolTip.SetToolTip(fitCircleButton, "Fit a circle to the detected edge points");
            _toolTip.SetToolTip(fitEllipseButton, "Fit an ellipse to the detected edge points");
            _toolTip.SetToolTip(fitRectangleButton, "Fit a rectangle to the detected edge points");
            _toolTip.SetToolTip(_edgeResultsGrid, "Detected edge points (Row, Col, Amplitude, Distance)");
            _toolTip.SetToolTip(_edgeStatusLabel, "Edge detection status — PASS (green) or FAIL (red)");
            _toolTip.SetToolTip(lineFittingResultLabel, "Line fitting result");
            _toolTip.SetToolTip(circleFittingResultLabel, "Circle fitting result");
            _toolTip.SetToolTip(ellipseFittingResultLabel, "Ellipse fitting result");
            _toolTip.SetToolTip(rectangleFittingResultLabel, "Rectangle fitting result");

            // ── Measurement ──
            _toolTip.SetToolTip(measurementPixelSizeXNumeric, "Pixel size in X direction (µm/pixel)");
            _toolTip.SetToolTip(measurementPixelSizeYNumeric, "Pixel size in Y direction (µm/pixel)");
            _toolTip.SetToolTip(measurementCoordInput, "Enter coordinates, one pair per line: row,col");
            _toolTip.SetToolTip(appendLineButton, "Append the last fitted line endpoints to the coordinate input");
            _toolTip.SetToolTip(appendCircleButton, "Append the last fitted circle/arc center to the coordinate input");
            _toolTip.SetToolTip(appendEllipseButton, "Append the last fitted ellipse center to the coordinate input");
            _toolTip.SetToolTip(appendRectButton, "Append the last fitted rectangle center to the coordinate input");
            _toolTip.SetToolTip(appendContourButton, "Append detected edge points to the coordinate input");
            _toolTip.SetToolTip(measureDistanceButton, "Measure distance using coordinates above");
            _toolTip.SetToolTip(measureAngleButton, "Measure angle using coordinates above");
            _toolTip.SetToolTip(measureResultLabel, "Measurement result — OK (green) / NG (red). Or run '[Run Recipe]' or '[一鍵量測]' to execute a recipe");
        }

        // ── 進度回饋（第四組）──────────────────────────────────────────
        // 長操作期間 UI thread 阻塞於 HALCON 呼叫，無法處理一般訊息迴圈；
        // 故設定 status 文字後立即 Refresh() 強制處理 WM_PAINT，讓使用者看到
        // 目前進度。Refresh() 只處理繪圖訊息，不會造成按鈕重入。

        private void SetProgress(string text)
        {
            progressLabel.Text = text;
            statusStrip.Refresh();
        }

        private void ClearProgress()
        {
            progressLabel.Text = "Ready";
            statusStrip.Refresh();
        }

        // 一鍵量測逐階段回呼：把 workflow state 映射成可讀文字。
        private void OnWorkflowStateChanged(MeasurementState state)
        {
            string text;
            switch (state)
            {
                case MeasurementState.CheckingImage: text = "一鍵量測：影像品質檢查中…"; break;
                case MeasurementState.MatchingTemplate: text = "一鍵量測：模板匹配中…"; break;
                case MeasurementState.TransformingRois: text = "一鍵量測：座標轉換中…"; break;
                case MeasurementState.Measuring: text = "一鍵量測：量測中…"; break;
                case MeasurementState.Evaluating: text = "一鍵量測：公差判定中…"; break;
                case MeasurementState.Reporting: text = "一鍵量測：產生報表中…"; break;
                case MeasurementState.Completed: text = "一鍵量測：完成"; break;
                case MeasurementState.Failed: text = "一鍵量測：失敗"; break;
                default: text = "一鍵量測：" + state; break;
            }
            SetProgress(text);
        }

        // 結果表空狀態提示（第五組 #11）：Rows.Clear/Add 會觸發重繪，
        // 故無資料列時於此繪製置中引導文字。
        private void EdgeResultsGrid_Paint(object sender, PaintEventArgs e)
        {
            if (_edgeResultsGrid.Rows.Count > 0) return;
            TextRenderer.DrawText(e.Graphics, "尚無邊緣點 — 繪製 ROI 後按 Detect",
                _edgeResultsGrid.Font, _edgeResultsGrid.ClientRectangle,
                System.Drawing.SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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

        private string FindTemplatesDirectory() => DataPaths.TemplatesDirOrNull();

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
            // 重置顏色：否則上一次配方 NG(紅)/OK(綠) 會殘留並染色後續無關文字。
            measureResultLabel.ForeColor = SystemColors.ControlText;

            // 換圖必須清掉匹配姿態，否則 Run Recipe 守門（HasReferencePose && !_hasMatch）
            // 會放行，並用前一張影像的 _lastMatch* 對新影像做 ROI 變換，畫出錯誤的 OK/NG。
            // （DisplayImage 已清 persistent overlay，這裡只需清 C# 狀態。）
            _hasMatch = false;
            _lastMatchRow = 0;
            _lastMatchCol = 0;
            _lastMatchAngleDeg = 0;
            RefreshMatchContour(); // _hasMatch=false → 釋放並清空快取輪廓

            // 換圖重置：橫幅回灰「—」、重新評估空狀態引導（此時已載入影像→引導隱藏）。
            SetResultBanner(0, 0, false);
            UpdateEmptyState();
        }

        // 空狀態工作流引導（GUI-01 / N3）：視窗尚無影像且無配方時，於影像格顯示三步驟指引；
        // 一旦載入影像或配方即隱藏，避免不透明面板蓋住已載入內容。
        // 刻意只覆蓋影像格（column0），且不切換 HALCON 控制項 Visible
        // （HWindowControlHelper 在建構時擷取 HalconWindow，切換控制項可見性會使其失效）。
        private void UpdateEmptyState()
        {
            bool hasImage = _imageHelper != null && _imageHelper.CurrentImage != null;
            bool hasRecipe = _loadedRecipe != null;
            emptyStateGuideLabel.Visible = !hasImage && !hasRecipe;
        }

        // PASS/FAIL 大字橫幅（GUI-02 / N2）：依配方執行結果設定顏色與文字。
        // 未量測 / 無有效工具 → 灰「—」；NG>0 → 紅 FAIL（NG n）；否則 OK>0 → 綠 PASS。
        private void SetResultBanner(int okCount, int ngCount, bool measured)
        {
            if (!measured || (okCount == 0 && ngCount == 0))
            {
                resultBannerPanel.BackColor = System.Drawing.Color.FromArgb(160, 160, 160);
                resultBannerLabel.Text = "—";
            }
            else if (ngCount > 0)
            {
                resultBannerPanel.BackColor = System.Drawing.Color.FromArgb(192, 0, 0);
                resultBannerLabel.Text = string.Format(CultureInfo.InvariantCulture, "FAIL（NG {0}）", ngCount);
            }
            else
            {
                resultBannerPanel.BackColor = System.Drawing.Color.FromArgb(0, 128, 0);
                resultBannerLabel.Text = "PASS";
            }
        }

        // 重算快取匹配輪廓：先釋放舊的，若目前有匹配則依 _lastMatch* 算一次新的。
        // 在匹配成功 / 一鍵量測同步 / 換圖清狀態時呼叫；redraw 不呼叫（直接用快取）。
        private void RefreshMatchContour()
        {
            _matchContour?.Dispose();
            _matchContour = null;
            if (!_hasMatch) return;
            try
            {
                _matchContour = _templateMatcher.GetMatchContour(_lastMatchRow, _lastMatchCol, _lastMatchAngleDeg);
            }
            catch (Exception)
            {
                // 無模板載入（InvalidOperationException）或 HALCON 錯誤 → 略過輪廓，不影響量測。
                _matchContour = null;
            }
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

            Cursor = Cursors.WaitCursor;
            SetProgress("建立模板中…");
            try
            {
                var parameters = new TemplateCreationParameters
                {
                    AngleStart = (double)angleStartNumeric.Value,
                    AngleExtent = (double)angleExtentNumeric.Value,
                    PyramidLevel = (int)pyramidNumeric.Value
                };

                HRegion templateRegion = null;
                try
                {
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
                        // L1：用 persistent overlay 取代直接繪製，這樣 pan/zoom 後文字與框不消失。
                        double mR1 = r1, mC1 = c1, mR2 = r2, mC2 = c2;
                        _imageHelper.EndRect2Edit();
                        _imageHelper.EndArcEdit();
                        _imageHelper.SetPersistentOverlayAction(() =>
                        {
                            _imageHelper.Annotator.DrawRoiRectangle(mR1, mC1, mR2, mC2);
                            _imageHelper.Annotator.DrawText("Model saved", (int)mR1 - 5, (int)mC1);
                        });
                    }

                    LoadTemplateList();
                    matchResultTextBox.Text = $"Template created: {modelPath}";
                }
                finally
                {
                    // CreateAndSave / SmallestRectangle1 / 繪製任一步擲例外，templateRegion
                    // 都須釋放，避免每次失敗建模洩漏一個 HRegion。
                    templateRegion?.Dispose();
                }
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"Halcon error: {ex.Message}", "Error");
            }
            finally
            {
                Cursor = Cursors.Default;
                ClearProgress();
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

            Cursor = Cursors.WaitCursor;
            SetProgress("模板匹配中…");
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
                    RefreshMatchContour(); // 算一次快取輪廓，overlay action 每次 redraw 直接用

                    _imageHelper.EndRect2Edit();  // H2：接管畫面前結束殘留編輯把手
                    _imageHelper.EndArcEdit();
                    _imageHelper.SetPersistentOverlayAction(() =>
                    {
                        if (_matchContour != null)
                            _imageHelper.Annotator.DrawMatchContour(_matchContour, capturedRow, capturedCol, capturedAngle, capturedScore);
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
            finally
            {
                Cursor = Cursors.Default;
                ClearProgress();
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

            if (_latestEdgeRoi == null)
            {
                SetEdgeStatus(false, "Please draw an ROI region first.");
                return;
            }

            Cursor = Cursors.WaitCursor;
            SetProgress("邊緣檢測中…");
            try
            {
                EdgeDetectionRoi roi = _latestEdgeRoi;
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
                _latestEllipseFittingResult = null;
                _latestRectangleFittingResult = null;
                UpdateLineFittingResult(null);
                UpdateCircleFittingResult(null);
                UpdateEllipseFittingResult(null);
                UpdateRectangleFittingResult(null);
                ShowFittingOverlay();
            }
            catch (HalconException ex)
            {
                // 清掉前一次成功的結果/十字/格線，避免「狀態顯示失敗、但畫面仍是舊結果」的矛盾。
                InvalidateEdgeState();
                ShowFittingOverlay();
                SetEdgeStatus(false, "Edge detection failed [Halcon " + ex.GetErrorCode() + "]: " + ex.Message);
            }
            catch (Exception ex)
            {
                // 任何 .NET 未預期例外（UI thread 衝突、null reference、HObject 生命週期等）
                // 都吞進來，避免 leak 到 WinForms 主訊息迴圈導致 unhandled exception dialog。
                InvalidateEdgeState();
                ShowFittingOverlay();
                SetEdgeStatus(false, "Edge detection failed (unexpected " + ex.GetType().Name + "): " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
                ClearProgress();
            }
        }

        // 由六個弧形數值框組出 ArcMeasureRoi（角度轉弧度）。供偵測、即時預覽、互動編輯共用。
        private ArcMeasureRoi BuildArcRoiFromControls()
        {
            return new ArcMeasureRoi
            {
                CenterRow = (double)_arcCenterRowNumeric.Value,
                CenterCol = (double)_arcCenterColNumeric.Value,
                Radius = (double)_arcRadiusNumeric.Value,
                AngleStart = (double)_arcAngleStartNumeric.Value * Math.PI / 180.0,
                AngleExtent = (double)_arcAngleExtentNumeric.Value * Math.PI / 180.0,
                AnnulusRadius = (double)_arcAnnulusNumeric.Value
            };
        }

        private void DetectArcButton_Click(object sender, EventArgs e)
        {
            if (_imageHelper == null || _imageHelper.CurrentImage == null)
            {
                _edgeStatusLabel.Text = "Arc: 請先載入影像";
                _edgeStatusLabel.ForeColor = Color.Red;
                return;
            }

            var arcRoi = BuildArcRoiFromControls();

            string validation = arcRoi.ValidationError;
            if (validation != null)
            {
                _edgeStatusLabel.Text = "Arc ROI 無效: " + validation;
                _edgeStatusLabel.ForeColor = Color.Red;
                return;
            }

            string interp = _edgeInterpolationCombo.SelectedItem?.ToString() ?? "nearest_neighbor";
            string polarity = _edgePolarityCombo.SelectedItem?.ToString() ?? "all";
            string selector = _edgeSelectorCombo.SelectedItem?.ToString() ?? "all";
            double sigma = (double)_edgeSigmaNumeric.Value;
            double threshold = (double)_edgeThresholdNumeric.Value;

            var parameters = new EdgeDetectionParameters
            {
                Sigma = sigma,
                Threshold = threshold,
                Polarity = polarity,
                EdgeSelector = selector,
                Interpolation = interp
            };

            try
            {
                Cursor = Cursors.WaitCursor;
                EdgeResult result = _edgeDetector.DetectEdgesOnArc(_imageHelper.CurrentImage, arcRoi, parameters);
                _latestArcRoi = arcRoi;
                _latestEdgeRoi = null;
                _imageHelper.EndRect2Edit();   // 結束殘留的邊緣 rect2 編輯把手（M5），避免與弧帶並存
                _latestEdgeResult = result;
                _latestLineFittingResult = null;
                _latestCircleFittingResult = null;
                _latestEllipseFittingResult = null;
                _latestRectangleFittingResult = null;
                UpdateLineFittingResult(null);
                UpdateCircleFittingResult(null);
                UpdateEllipseFittingResult(null);
                UpdateRectangleFittingResult(null);

                RestoreDefaultEdgeGridColumns();
                BindEdgeResult(result);
                SetEdgeStatus(result.Success, result.Success
                    ? string.Format("Arc edges: {0} found", result.EdgePoints.Count)
                    : result.ErrorMessage);
                ShowFittingOverlay();
            }
            catch (Exception ex)
            {
                InvalidateEdgeState();
                ShowFittingOverlay();
                SetEdgeStatus(false, "Arc detect failed: " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
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
            // 先清前次結果：失敗（早退/例外）時不殘留舊擬合線於 overlay 與結果表，
            // 避免「label 顯示失敗、畫面卻仍是舊成功結果」的矛盾。結尾一律 ShowFittingOverlay 刷新。
            _latestLineFittingResult = null;
            if (_latestEdgeResult == null || _latestEdgeResult.EdgePoints == null)
            {
                UpdateLineFittingResult(new LineFittingResult { ErrorMessage = "請先執行邊緣檢測" });
                ShowFittingOverlay();
                return;
            }

            try
            {
                LineFittingResult result = _lineFitter.FitLine(_latestEdgeResult.EdgePoints, LineFittingParameters.Default());
                _latestLineFittingResult = result;
                UpdateLineFittingResult(result);
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
            ShowFittingOverlay();
        }

        private void FitCircleButton_Click(object sender, EventArgs e)
        {
            _latestCircleFittingResult = null;
            if (_latestEdgeResult == null || _latestEdgeResult.EdgePoints == null)
            {
                UpdateCircleFittingResult(new CircleFittingResult { ErrorMessage = "請先執行邊緣檢測" });
                ShowFittingOverlay();
                return;
            }

            try
            {
                CircleFittingResult result = _circleFitter.FitCircle(_latestEdgeResult.EdgePoints, CircleFittingParameters.Default());
                _latestCircleFittingResult = result;
                UpdateCircleFittingResult(result);
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
            ShowFittingOverlay();
        }

        private void FitEllipseButton_Click(object sender, EventArgs e)
        {
            _latestEllipseFittingResult = null;
            if (_latestEdgeResult == null || _latestEdgeResult.EdgePoints == null)
            {
                UpdateEllipseFittingResult(new EllipseFittingResult { ErrorMessage = "請先執行邊緣檢測" });
                ShowFittingOverlay();
                return;
            }

            try
            {
                EllipseFittingResult result = _ellipseFitter.FitEllipse(_latestEdgeResult.EdgePoints, EllipseFittingParameters.Default());
                _latestEllipseFittingResult = result;
                UpdateEllipseFittingResult(result);
            }
            catch (HalconException ex)
            {
                UpdateEllipseFittingResult(new EllipseFittingResult
                {
                    ErrorMessage = "橢圓擬合失敗 [Halcon " + ex.GetErrorCode() + "]: " + ex.Message
                });
            }
            catch (Exception ex)
            {
                UpdateEllipseFittingResult(new EllipseFittingResult
                {
                    ErrorMessage = "橢圓擬合失敗 (unexpected " + ex.GetType().Name + "): " + ex.Message
                });
            }
            ShowFittingOverlay();
        }

        private void FitRectangleButton_Click(object sender, EventArgs e)
        {
            _latestRectangleFittingResult = null;
            if (_latestEdgeResult == null || _latestEdgeResult.EdgePoints == null)
            {
                UpdateRectangleFittingResult(new RectangleFittingResult { ErrorMessage = "請先執行邊緣檢測" });
                ShowFittingOverlay();
                return;
            }

            try
            {
                RectangleFittingResult result = _rectangleFitter.FitRectangle(_latestEdgeResult.EdgePoints, RectangleFittingParameters.Default());
                _latestRectangleFittingResult = result;
                UpdateRectangleFittingResult(result);
            }
            catch (HalconException ex)
            {
                UpdateRectangleFittingResult(new RectangleFittingResult
                {
                    ErrorMessage = "矩形擬合失敗 [Halcon " + ex.GetErrorCode() + "]: " + ex.Message
                });
            }
            catch (Exception ex)
            {
                UpdateRectangleFittingResult(new RectangleFittingResult
                {
                    ErrorMessage = "矩形擬合失敗 (unexpected " + ex.GetType().Name + "): " + ex.Message
                });
            }
            ShowFittingOverlay();
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

        // 切換分頁時結束殘留的編輯/繪製模式。影像區跨分頁共用，前一分頁留下的把手或
        // 正在進行的 ROI draw 在換頁後會讓操作困惑（還有 pending RequestRoi callback 風險）。
        private void FeatureTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_imageHelper == null) return;
            _imageHelper.EndRect2Edit();
            _imageHelper.EndArcEdit();
            _imageHelper.ClearRoiCoordinates();  // 消除 fallback 藍框殘留
            _imageHelper.IsRoiMode = false;
            if (_edgeDrawRoiCheck != null && _edgeDrawRoiCheck.Checked) _edgeDrawRoiCheck.Checked = false;
            if (roiModeCheck != null && roiModeCheck.Checked) roiModeCheck.Checked = false;
        }

        // 切換 subpix/measure_pos 演算法後，前一次偵測的格線/十字/狀態已不適用 → 清除並刷新，
        // 提示需重新 Detect（M1）。InitializeComponent 期間 _imageHelper 尚未建立，以 null 守門。
        private void EdgeAlgorithmRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (_imageHelper == null) return;
            InvalidateEdgeState();
            ShowFittingOverlay();
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
            if (_updatingEdgeRoiControls || _imageHelper == null || _imageHelper.CurrentImage == null || !_imageHelper.IsEditingRect2)
            {
                return;
            }

            double phi = (double)_edgeAngleNumeric.Value * Math.PI / 180.0;
            double l1 = (double)_edgeScanLengthNumeric.Value / 2.0;
            double l2 = (double)_edgeRoiWidthNumeric.Value / 2.0;

            _latestEdgeRoi = EdgeDetectionRoi.FromCenter(_editCenterRow, _editCenterCol, l1, l2, phi);
            InvalidateEdgeState();
            ShowFittingOverlay();
            _imageHelper.BeginRect2Edit(_editCenterRow, _editCenterCol, phi, l1, l2, OnEdgeRect2Changed);
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
            _edgeResultsGrid.SuspendLayout();
            try
            {
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
            }
            finally
            {
                _edgeResultsGrid.ResumeLayout();
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
            int total = result.EdgePairs == null ? 0 : result.EdgePairs.Count;
            int displayCount = Math.Min(total, MaxGridRows);

            _edgeResultsGrid.SuspendLayout();
            try
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
            }
            finally
            {
                _edgeResultsGrid.ResumeLayout();
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
            UpdateEmptyState();
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
        // N1：執行前配方驗證。Error → 列出並阻擋（回 false）；只有 Warning → 列出並詢問是否繼續；
        // 無問題 → 直接放行。imageW/H 取目前影像（取不到則 0,0，驗證會略過邊界檢查）。
        private bool EnsureRecipeValid()
        {
            int width = 0, height = 0;
            if (_imageHelper != null && _imageHelper.CurrentImage != null)
            {
                try
                {
                    HOperatorSet.GetImageSize(_imageHelper.CurrentImage, out HTuple w, out HTuple h);
                    width = w.I; height = h.I;
                }
                catch (HalconException) { /* 取不到尺寸 → 略過邊界檢查 */ }
            }

            System.Collections.Generic.List<RecipeIssue> issues =
                RecipeValidator.Validate(_loadedRecipe, width, height);
            if (issues.Count == 0) return true;

            var errors = issues.Where(i => i.Severity == RecipeIssueSeverity.Error).ToList();
            var warnings = issues.Where(i => i.Severity == RecipeIssueSeverity.Warning).ToList();

            if (errors.Count > 0)
            {
                MessageBox.Show(
                    "配方驗證未通過，請先修正以下錯誤：\n\n" + FormatRecipeIssues(errors)
                    + (warnings.Count > 0 ? "\n\n警告：\n" + FormatRecipeIssues(warnings) : ""),
                    "配方驗證", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            DialogResult r = MessageBox.Show(
                "配方有以下警告：\n\n" + FormatRecipeIssues(warnings) + "\n\n是否仍要繼續量測？",
                "配方驗證", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            return r == DialogResult.Yes;
        }

        private static string FormatRecipeIssues(System.Collections.Generic.List<RecipeIssue> issues)
        {
            return string.Join("\n", issues.Select(i =>
                "• " + (string.IsNullOrEmpty(i.ToolName) ? "" : "[" + i.ToolName + "] ") + i.Message));
        }

        private void RunRecipeButton_Click(object sender, EventArgs e)
        {
            if (_loadedRecipe == null) { MessageBox.Show("請先載入配方 (.zcp)。", "Info"); return; }
            if (_imageHelper == null || _imageHelper.CurrentImage == null) { MessageBox.Show("請先載入影像。", "Info"); return; }
            // 參考姿態守門只對「需要姿態變換的 1D 工具」有意義。純 2D 量測模型（無 1D 工具）
            // 不需匹配：未匹配時其標稱幾何以絕對影像座標量測（Pass 3 不套 reference_system/align）。
            if (_loadedRecipe.HasReferencePose && !_hasMatch && _loadedRecipe.Tools.Count > 0)
            {
                MessageBox.Show("此配方含參考姿態且有 1D 量測工具，請先對目前影像執行模板匹配以取得當前工件姿態。", "Info");
                return;
            }
            if (!EnsureRecipeValid()) return;

            // pixel size 來源（決策 A）：配方 CalibrationProfileId 有設且檔案存在 → 用校正檔；
            // 否則退回量測分頁。與一鍵量測共用 ResolvePixelSize（含載入失敗揭露）。
            ResolvePixelSize(out double pixelSizeUmX, out double pixelSizeUmY, out string pixelSizeSource);

            Cursor = Cursors.WaitCursor;
            SetProgress("執行配方量測中…");
            try
            {
                System.Collections.Generic.List<ToolRunResult> results = _recipeRunner.Run(
                    _loadedRecipe, _imageHelper.CurrentImage,
                    _hasMatch, _lastMatchRow, _lastMatchCol, _lastMatchAngleDeg * Math.PI / 180.0,
                    pixelSizeUmX, pixelSizeUmY);

                DrawRecipeResults(results, pixelSizeSource);
            }
            catch (Exception ex)
            {
                // RecipeRunner.Run 內部只接 HalconException；座標變換/繪製的非-Halcon 例外
                // 會逸出處理常式 → WinForms 靜默終止。在此攔截並向操作員顯示失敗原因。
                SetProgress("配方量測：失敗");
                MessageBox.Show("配方量測異常：" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                ClearProgress();
            }
        }

        // 抽出 RunRecipeButton_Click 的 overlay 繪製與結果表更新，供 Run Recipe 與一鍵量測共用。
        private void DrawRecipeResults(System.Collections.Generic.List<ToolRunResult> results, string pixelSizeSource)
        {
            // 產生配方結果前先結束殘留的互動編輯把手（rect2/arc）。否則前一次畫邊緣 ROI 留下的
            // 綠色把手會疊在結果之上，且拖曳它會偷改邊緣量測狀態（H2）。
            _imageHelper.EndRect2Edit();
            _imageHelper.EndArcEdit();
            _imageHelper.SetPersistentOverlayAction(() =>
            {
                OverlayAnnotator an = _imageHelper.Annotator;
                if (_hasMatch && _matchContour != null)
                {
                    an.DrawMatchContour(_matchContour, _lastMatchRow, _lastMatchCol, _lastMatchAngleDeg, 1.0);
                }

                var rows = new System.Collections.Generic.List<OverlayResultRow>();
                foreach (ToolRunResult r in results)
                {
                    if (r.Roi != null)
                    {
                        an.DrawRectangle2(r.Roi.Row, r.Roi.Col, r.Roi.AngleRad, r.Roi.Length1, r.Roi.Length2, "orange");
                        an.DrawText(r.Name ?? string.Empty, (int)r.Roi.Row, (int)r.Roi.Col, "orange");
                    }

                    if (r.Measured && r.ToolType == "circle")
                    {
                        string circleColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                        an.DrawCircle(r.FitCenterRow, r.FitCenterCol, r.FitRadiusPx, circleColor);
                        // 數值畫在影像上（比照 2D 量測模型/distance/angle）：錨在擬合圓心。
                        an.DrawText(r.ValueText ?? string.Empty, (int)r.FitCenterRow, (int)r.FitCenterCol, circleColor);
                    }
                    else if (r.Measured && r.ToolType == "line")
                    {
                        string lineColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                        an.DrawLine(r.LineRow1, r.LineCol1, r.LineRow2, r.LineCol2, lineColor);
                        // 數值畫在影像上（比照 2D 量測模型/distance/angle）：錨在擬合線中點「上方」
                        // 一段距離，避開亮線本體與橘色名稱標籤，落在深色背景上更清楚。
                        an.DrawText(r.ValueText ?? string.Empty,
                            (int)((r.LineRow1 + r.LineRow2) / 2.0) - 22, (int)((r.LineCol1 + r.LineCol2) / 2.0), lineColor);
                    }
                    else if (r.Measured && r.ToolType == "distance")
                    {
                        an.DrawDistance(r.DistRow1, r.DistCol1, r.DistRow2, r.DistCol2, r.ValueText, r.IsOk);
                    }
                    else if (r.Measured && r.ToolType == "angle")
                    {
                        double extent = r.AngleDeg * Math.PI / 180.0;
                        an.DrawAngle(r.AngleCenterRow, r.AngleCenterCol, r.AngleRadiusPx, r.AngleStartRad, extent, r.ValueText, r.IsOk);
                    }
                    else if (r.Measured && r.ToolType == "intersection" && r.OutputPrimitive != null)
                    {
                        an.DrawCross(r.OutputPrimitive.Row, r.OutputPrimitive.Col, 15, "cyan");
                    }
                    else if (r.Measured && r.ToolType == "midline" && r.OutputPrimitive != null)
                    {
                        an.DrawLine(r.OutputPrimitive.Row1, r.OutputPrimitive.Col1,
                            r.OutputPrimitive.Row2, r.OutputPrimitive.Col2, "cyan");
                    }
                    else if (r.Measured && r.ToolType == "projection" && r.OutputPrimitive != null)
                    {
                        an.DrawLine(r.DistRow1, r.DistCol1, r.DistRow2, r.DistCol2, "cyan");      // 圓心→垂足
                        an.DrawCross(r.OutputPrimitive.Row, r.OutputPrimitive.Col, 12, "cyan");   // 垂足
                    }
                    else if (r.Measured && (r.ToolType == "parallelism" || r.ToolType == "perpendicularity"
                        || r.ToolType == "concentricity"))
                    {
                        // 帶基準的形位公差：畫量測↔基準的偏移連線 + 偏差/判定文字（T4 已設好兩端）。
                        // 真圓度/真直度無對應幾何錨點，僅以結果表列呈現（其元素已由各自工具畫出）。
                        an.DrawDistance(r.DistRow1, r.DistCol1, r.DistRow2, r.DistCol2, r.ValueText, r.IsOk);
                    }
                    else if (r.Measured && r.ToolType != null && r.ToolType.StartsWith("metrology_"))
                    {
                        // 2D 量測模型：擬合形狀畫綠色（IsOk 有設時依判定上色），量測區邊點畫青色十字。
                        // 與 1D overlay 同處於這個 persistent-overlay action，故能共存且隨平移/縮放重繪。
                        string mColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "green");
                        if (r.ToolType == "metrology_circle")
                            an.DrawCircle(r.FitCenterRow, r.FitCenterCol, r.FitRadiusPx, mColor);
                        else if (r.ToolType == "metrology_line")
                            an.DrawLine(r.LineRow1, r.LineCol1, r.LineRow2, r.LineCol2, mColor);
                        else if (r.ToolType == "metrology_ellipse")
                            an.DrawEllipse(r.FitCenterRow, r.FitCenterCol, r.FitPhi, r.FitRadius1, r.FitRadius2, mColor);
                        else if (r.ToolType == "metrology_rectangle")
                            an.DrawRectangle2(r.FitCenterRow, r.FitCenterCol, r.FitPhi, r.FitLength1, r.FitLength2, mColor);

                        // 量測區邊點（青色十字），等間距抽樣至多 MaxOverlayCrosses 個（同 1D 邊緣十字慣例）。
                        int mTotal = Math.Min(r.MetrologyMeasureRows.Count, r.MetrologyMeasureCols.Count);
                        if (mTotal > 0)
                        {
                            int mStep = mTotal <= MaxOverlayCrosses ? 1 : (int)Math.Ceiling((double)mTotal / MaxOverlayCrosses);
                            for (int mi = 0; mi < mTotal; mi += mStep)
                                an.DrawCross(r.MetrologyMeasureRows[mi], r.MetrologyMeasureCols[mi], 6, "cyan");
                        }

                        // 文字錨點：線用兩端中點，其餘用擬合中心。
                        double mTextRow = r.ToolType == "metrology_line" ? (r.LineRow1 + r.LineRow2) / 2.0 : r.FitCenterRow;
                        double mTextCol = r.ToolType == "metrology_line" ? (r.LineCol1 + r.LineCol2) / 2.0 : r.FitCenterCol;
                        an.DrawText(r.ValueText ?? string.Empty, (int)mTextRow, (int)mTextCol, mColor);
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
                _loadedRecipe != null ? _loadedRecipe.Name : "", results.Count, okCount, ngCount, pixelSizeSource);
            measureResultLabel.ForeColor = ngCount > 0 ? System.Drawing.Color.DarkRed
                : (okCount > 0 ? System.Drawing.Color.DarkGreen : System.Drawing.SystemColors.ControlText);
            SetResultBanner(okCount, ngCount, true);
        }

        // Pixel size 來源：配方 CalibrationProfileId 有設且檔案存在 → 用校正檔；否則退回量測分頁。
        private void ResolvePixelSize(out double pxUmX, out double pxUmY, out string source)
        {
            pxUmX = (double)measurementPixelSizeXNumeric.Value;
            pxUmY = (double)measurementPixelSizeYNumeric.Value;
            source = "量測分頁";
            if (!string.IsNullOrEmpty(_loadedRecipe.CalibrationProfileId))
            {
                try
                {
                    string calPath = Path.Combine(ResolveCalibrationsDir(), _loadedRecipe.CalibrationProfileId + ".json");
                    if (File.Exists(calPath))
                    {
                        CalibrationProfile prof = _calibrationStore.Load(calPath);
                        pxUmX = prof.PixelSizeUmX;
                        pxUmY = prof.PixelSizeUmY;
                        source = "校正檔 " + _loadedRecipe.CalibrationProfileId;
                    }
                    else
                    {
                        // 配方指定了校正檔但檔案不存在 → 揭露，不可靜默退回
                        source = "量測分頁（⚠️ 找不到校正檔 " + _loadedRecipe.CalibrationProfileId + "）";
                    }
                }
                catch (Exception ex)
                {
                    // 載入校正檔失敗（損毀/讀取錯誤）→ 沿用量測分頁數值，但須讓操作員知道，不可靜默吞。
                    source = "量測分頁（⚠️ 校正檔 " + _loadedRecipe.CalibrationProfileId + " 載入失敗：" + ex.Message + "）";
                }
            }
        }

        private void OneClickMeasureButton_Click(object sender, EventArgs e)
        {
            if (_loadedRecipe == null) { MessageBox.Show("請先載入配方 (.zcp)。", "Info"); return; }
            if (_imageHelper == null || _imageHelper.CurrentImage == null) { MessageBox.Show("請先載入影像。", "Info"); return; }
            if (!EnsureRecipeValid()) return;

            Cursor = Cursors.WaitCursor;
            try
            {

                ResolvePixelSize(out double pxUmX, out double pxUmY, out string pixelSizeSource);

                // 模板模型路徑：從下拉選單取目前選取檔案
                string templatePath = null;
                var templateFile = templateFileCombo.SelectedItem as FileItemWrapper;
                if (templateFile != null && templateFile.IsRealFile)
                    templatePath = templateFile.FullPath;

                string reportDir = Path.Combine(ResolveDataDir(), "reports");

                WorkflowResult wfResult = _workflow.RunOnce(
                    _loadedRecipe, _imageHelper.CurrentImage,
                    pxUmX, pxUmY,
                    reportDir,
                    templatePath, null, null,
                    _skipIqcCheckBox != null && _skipIqcCheckBox.Checked,
                    out System.Collections.Generic.List<ToolRunResult> results);

                // 同步本次匹配狀態到 MainWindow 欄位，避免 DrawRecipeResults 畫出上一次匹配的殘留綠框。
                // 無參考姿態的配方 HasMatch=false → 不畫匹配輪廓。
                _hasMatch = wfResult.HasMatch;
                if (wfResult.HasMatch)
                {
                    _lastMatchRow = wfResult.MatchRow;
                    _lastMatchCol = wfResult.MatchCol;
                    _lastMatchAngleDeg = wfResult.MatchAngleDeg;
                }
                RefreshMatchContour(); // 依本次匹配姿態更新快取輪廓（無匹配則清空）

                DrawRecipeResults(results, pixelSizeSource);

                string csvInfo = !string.IsNullOrEmpty(wfResult.ReportPath)
                    ? " | CSV: " + wfResult.ReportPath
                    : "";
                measureResultLabel.Text += string.Format(CultureInfo.InvariantCulture,
                    " | 一鍵：{0} OK {1}/NG {2}{3}{4}",
                    wfResult.AllOk ? "PASS" : "FAIL", wfResult.OkCount, wfResult.NgCount,
                    csvInfo,
                    !wfResult.Success ? " (" + wfResult.Message + ")" : "");
            }
            catch (Exception ex)
            {
                SetProgress("一鍵量測：失敗");
                MessageBox.Show("一鍵量測異常：" + ex.ToString(), "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // 由 app base directory 往上找 .sln，定位 data/ (root of data subdirs)。
        private static string ResolveDataDir() => DataPaths.DataDir();

        private static string ResolveCalibrationsDir() => DataPaths.SubDir("calibrations");

        private static string ResolveRecipesDir() => DataPaths.SubDir("recipes");

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

        // 開啟 RecipeEditor（獨立非 modal Form，方便同時操作主畫面取 ROI）。
        // 需先有影像（ROI 擷取需要）。編輯器存檔後以 callback 回寫 MainWindow，
        // Run Recipe 立即可用編輯結果，不需重新 Load。
        private void OpenRecipeEditor(object sender, EventArgs e)
        {
            if (_imageHelper == null || _imageHelper.CurrentImage == null)
            {
                MessageBox.Show(this, "Please load an image first.", "Recipe Editor",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // A1：提供「單一工具試測」委派給編輯器。只跑這一個工具（暫態單工具配方），
            // 不套匹配姿態（ROI 即影像座標）、不呼叫 EnsureRecipeValid、不重跑整份配方。
            Func<MeasurementTool, ToolRunResult> trialMeasure = (tool) =>
            {
                if (tool == null || _imageHelper == null || _imageHelper.CurrentImage == null) return null;
                ResolvePixelSize(out double pxUmX, out double pxUmY, out _);
                var single = Recipe.Default();
                single.HasReferencePose = false;
                single.Tools = new System.Collections.Generic.List<MeasurementTool> { tool };
                var list = _recipeRunner.Run(single, _imageHelper.CurrentImage,
                    false, 0.0, 0.0, 0.0, pxUmX, pxUmY);
                return list.Count > 0 ? list[0] : null;
            };

            // If a recipe is already loaded in MainWindow, pass it to the editor
            // so the user can inspect and edit without re-loading. On save, write the
            // edited recipe back so Run Recipe uses it immediately (no re-load needed).
            var editor = new RecipeEditor(_imageHelper, _loadedRecipe, _loadedRecipePath,
                (recipe, path) =>
                {
                    _loadedRecipe = recipe;
                    _loadedRecipePath = path;
                    measureResultLabel.Text = string.Format(CultureInfo.InvariantCulture,
                        "已從編輯器更新配方 '{0}'（{1} 工具）。可執行 Run Recipe。",
                        recipe.Name, recipe.Tools.Count);
                    UpdateEmptyState();
                },
                trialMeasure);
            // 編輯器接管共用影像視窗做 ROI 編輯：先清掉主視窗殘留的偵測/擬合 overlay
            // （Edge Detection 藍框、邊緣十字、Run Recipe 結果等），讓編輯器從乾淨影像開始。
            _imageHelper.EndRect2Edit();
            _imageHelper.EndArcEdit();
            _imageHelper.ClearOverlay();
            editor.Show(this);
        }

        // 開啟 2D 量測模型編輯器（modal）。編輯的是目前載入的配方；存檔後回寫 _loadedRecipe
        // 並（若有路徑）以 RecipeStore 持久化，Run Recipe 立即經 Pass 3 套用此模型。
        private void OpenMetrologyModelEditor(object sender, EventArgs e)
        {
            if (_loadedRecipe == null)
            {
                // 純 2D 量測模型配方無需先備妥一份 1D 配方：就地建立空白配方
                // （無 1D 工具、無參考姿態），讓操作員直接定義量測模型並 Run Recipe。
                _loadedRecipe = Recipe.Default();
                _loadedRecipe.HasReferencePose = false;
            }

            int imgW = 0, imgH = 0;
            if (_imageHelper != null && _imageHelper.CurrentImage != null)
            {
                try
                {
                    HOperatorSet.GetImageSize(_imageHelper.CurrentImage, out HTuple w, out HTuple h);
                    imgW = w.I; imgH = h.I;
                }
                catch (HalconException) { /* 取不到尺寸用 0：apply 時 adapter 會即時查詢 */ }
            }

            using (var editor = new MetrologyModelEditorForm(_loadedRecipe, imgW, imgH,
                (recipe) =>
                {
                    _loadedRecipe = recipe;

                    // 尚無檔案路徑（例如全新的純量測模型配方）→ 跳「另存新檔」讓操作員取檔名，
                    // 量測模型編輯器即可自給自足存檔，不必再繞 Edit Recipe。已有路徑則直接覆寫。
                    if (string.IsNullOrEmpty(_loadedRecipePath))
                    {
                        using (var save = new SaveFileDialog
                        {
                            Filter = "Recipe (*.zcp)|*.zcp|All Files|*.*",
                            DefaultExt = ".zcp",
                            Title = "Save Metrology Recipe As"
                        })
                        {
                            string dir = ResolveRecipesDir();
                            if (Directory.Exists(dir)) save.InitialDirectory = dir;
                            if (save.ShowDialog(this) == DialogResult.OK)
                                _loadedRecipePath = save.FileName;
                        }
                    }

                    if (!string.IsNullOrEmpty(_loadedRecipePath))
                    {
                        try { _recipeStore.Save(_loadedRecipe, _loadedRecipePath); }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, "量測模型存檔失敗：" + ex.Message, "Metrology Model",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }

                    int count = _loadedRecipe.MetrologyModel != null && _loadedRecipe.MetrologyModel.Objects != null
                        ? _loadedRecipe.MetrologyModel.Objects.Count : 0;
                    string savedNote = string.IsNullOrEmpty(_loadedRecipePath)
                        ? "（未存檔，僅暫存記憶體）" : "（已存至 " + Path.GetFileName(_loadedRecipePath) + "）";
                    measureResultLabel.Text = string.Format(CultureInfo.InvariantCulture,
                        "已更新量測模型（{0} 物件）{1}。可執行 Run Recipe。", count, savedNote);
                }))
            {
                editor.ShowDialog(this);
            }
        }

        // 單一 overlay slot 的共用底層：把目前有效的偵測/擬合狀態（ROI 框 + 邊緣十字 +
        // 直線/圓擬合）全部重畫。直接讀 _latest* 欄位，確保任何功能設定 overlay 時都不會
        // 抹掉其他仍然有效的圖層（先 Fit Circle 再 Fit Line 時圓消失、label 卻還顯示
        // "Circle OK" 這類畫面與狀態矛盾）。
        private void DrawFittingLayers(OverlayAnnotator an)
        {
            EdgeDetectionRoi roi = _latestEdgeRoi;
            ArcMeasureRoi arcRoi = _latestArcRoi;
            if (arcRoi != null && arcRoi.IsDefined)
            {
                // 弧形卡尺量測帶：畫內弧(R-Annulus)、中弧(R)、外弧(R+Annulus)，
                // 讓使用者清楚看到實際掃描的環帶範圍是否壓在特徵邊緣上。
                // 內外弧用橘色(邊界)、中弧用黃色，並在中心畫十字標出弧心。
                double cr = arcRoi.CenterRow, cc = arcRoi.CenterCol;
                double a0 = arcRoi.AngleStart;
                double a1 = arcRoi.AngleStart + arcRoi.AngleExtent;
                string order = arcRoi.AngleExtent > 0 ? "positive" : "negative";
                double rIn = Math.Max(1.0, arcRoi.Radius - arcRoi.AnnulusRadius);
                double rOut = arcRoi.Radius + arcRoi.AnnulusRadius;

                an.DrawArc(cr, cc, rIn, a0, a1, order, "orange");
                an.DrawArc(cr, cc, rOut, a0, a1, order, "orange");
                an.DrawArc(cr, cc, arcRoi.Radius, a0, a1, order, "yellow");
                an.DrawCross(cr, cc, 15, "orange");
            }

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
                if (circle.IsClosed)
                {
                    an.DrawCircle(circle.CenterRow, circle.CenterColumn, circle.RadiusPx, "green");
                }
                else
                {
                    an.DrawArc(circle.CenterRow, circle.CenterColumn, circle.RadiusPx,
                        circle.StartPhi, circle.EndPhi, circle.PointOrder, "green");
                }
            }

            EllipseFittingResult ellipse = _latestEllipseFittingResult;
            if (ellipse != null && ellipse.Success)
            {
                an.DrawEllipse(ellipse.CenterRow, ellipse.CenterColumn, ellipse.Phi,
                    ellipse.Radius1Px, ellipse.Radius2Px, "green");
            }

            RectangleFittingResult rect = _latestRectangleFittingResult;
            if (rect != null && rect.Success)
            {
                an.DrawRectangle2(rect.CenterRow, rect.CenterColumn, rect.Phi,
                    rect.Length1Px, rect.Length2Px, "green");
            }

            // 結果表（4.13b/B）：顯示目前量測值與公差判定（OK/NG），由 M3c 配方流程接上。
            System.Collections.Generic.List<OverlayResultRow> resultRows = BuildResultRows();
            if (resultRows.Count > 0)
            {
                an.DrawResultTable(resultRows);
            }
        }

        // 從目前的 _latest* 量測狀態建出結果表的列，含公差判定 (M3c 已接上 ToleranceJudger)。
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
                        "Len={0:F1}px Ang={1:F2}deg", line.Length,
                        Domain.AngleMeasurement.AngleNormalizer.ToHalfCircle(line.AngleDeg)),
                    IsOk = null
                });
            }

            CircleFittingResult circleResult = _latestCircleFittingResult;
            if (circleResult != null && circleResult.Success)
            {
                string prefix = circleResult.IsClosed ? "Circle" : "Arc";
                rows.Add(new OverlayResultRow
                {
                    Name = prefix,
                    ValueText = circleResult.IsClosed
                        ? string.Format(CultureInfo.InvariantCulture,
                            "D={0:F1}px R={1:F1}px", circleResult.DiameterPx, circleResult.RadiusPx)
                        : string.Format(CultureInfo.InvariantCulture,
                            "R={0:F1}px {1:F1}°→{2:F1}°",
                            circleResult.RadiusPx,
                            circleResult.StartPhi * 180.0 / Math.PI,
                            circleResult.EndPhi * 180.0 / Math.PI),
                    IsOk = null
                });
            }

            EllipseFittingResult ellipseResult = _latestEllipseFittingResult;
            if (ellipseResult != null && ellipseResult.Success)
            {
                rows.Add(new OverlayResultRow
                {
                    Name = "Ellipse",
                    ValueText = string.Format(CultureInfo.InvariantCulture,
                        "R1={0:F1}px R2={1:F1}px", ellipseResult.Radius1Px, ellipseResult.Radius2Px),
                    IsOk = null
                });
            }

            RectangleFittingResult rectResult = _latestRectangleFittingResult;
            if (rectResult != null && rectResult.Success)
            {
                rows.Add(new OverlayResultRow
                {
                    Name = "Rectangle",
                    ValueText = string.Format(CultureInfo.InvariantCulture,
                        "L1={0:F1}px L2={1:F1}px", rectResult.Length1Px, rectResult.Length2Px),
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
                Domain.AngleMeasurement.AngleNormalizer.ToHalfCircle(result.AngleDeg),
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

            string typeLabel = result.IsClosed ? "Circle" : "Arc";
            circleFittingResultLabel.Text = string.Format(
                CultureInfo.InvariantCulture,
                typeLabel + " OK | C=({0:F2},{1:F2}) R={2:F2}px D={3:F2}px\n"
                + (result.IsClosed ? "" : "Arc {7:F1}°→{8:F1}° | ")
                + "RMS={4:F4}px Round={5:F4}px Pts={6}",
                result.CenterRow,
                result.CenterColumn,
                result.RadiusPx,
                result.DiameterPx,
                result.ResidualRms,
                result.Roundness,
                result.UsedPoints,
                result.StartPhi * 180.0 / Math.PI,
                result.EndPhi * 180.0 / Math.PI);
            circleFittingResultLabel.ForeColor = Color.Green;
        }

        private void UpdateEllipseFittingResult(EllipseFittingResult result)
        {
            if (result == null)
            {
                ellipseFittingResultLabel.Text = "橢圓擬合: 尚未執行";
                ellipseFittingResultLabel.ForeColor = Color.Black;
                return;
            }

            if (!result.Success)
            {
                ellipseFittingResultLabel.Text = "橢圓擬合失敗: " + result.ErrorMessage;
                ellipseFittingResultLabel.ForeColor = Color.Red;
                return;
            }

            ellipseFittingResultLabel.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Ellipse OK | C=({0:F2},{1:F2}) R1={2:F2}px R2={3:F2}px\nPhi={4:F2}° RMS={5:F4}px Pts={6}",
                result.CenterRow,
                result.CenterColumn,
                result.Radius1Px,
                result.Radius2Px,
                result.Phi * 180.0 / Math.PI,
                result.ResidualRms,
                result.UsedPoints);
            ellipseFittingResultLabel.ForeColor = Color.Green;
        }

        private void UpdateRectangleFittingResult(RectangleFittingResult result)
        {
            if (result == null)
            {
                rectangleFittingResultLabel.Text = "矩形擬合: 尚未執行";
                rectangleFittingResultLabel.ForeColor = Color.Black;
                return;
            }

            if (!result.Success)
            {
                rectangleFittingResultLabel.Text = "矩形擬合失敗: " + result.ErrorMessage;
                rectangleFittingResultLabel.ForeColor = Color.Red;
                return;
            }

            rectangleFittingResultLabel.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Rectangle OK | C=({0:F2},{1:F2}) L1={2:F2}px L2={3:F2}px\nPhi={4:F2}° RMS={5:F4}px Pts={6}",
                result.CenterRow,
                result.CenterColumn,
                result.Length1Px,
                result.Length2Px,
                result.Phi * 180.0 / Math.PI,
                result.ResidualRms,
                result.UsedPoints);
            rectangleFittingResultLabel.ForeColor = Color.Green;
        }

        private void ClearFittingState()
        {
            // 換圖/清除時若弧形互動編輯仍勾選，同步取消（helper 端已結束弧編輯，這裡讓
            // checkbox 與實際狀態一致，避免「勾著但無弧帶」的不同步）。
            if (_arcEditCheck != null && _arcEditCheck.Checked)
            {
                _updatingArcControls = true;
                try { _arcEditCheck.Checked = false; } finally { _updatingArcControls = false; }
            }
            _latestEdgeRoi = null;
            _latestArcRoi = null;
            _latestEdgeResult = null;
            _latestLineFittingResult = null;
            _latestCircleFittingResult = null;
            _latestEllipseFittingResult = null;
            _latestRectangleFittingResult = null;
            UpdateLineFittingResult(null);
            UpdateCircleFittingResult(null);
            UpdateEllipseFittingResult(null);
            UpdateRectangleFittingResult(null);
        }

        // 邊緣量測結果失效：清結果/擬合狀態與結果表，回到「等待 Detect」。
        // 不動 _latestEdgeRoi（各呼叫端自行設定），也不動 overlay/編輯模式（呼叫端視情況處理）。
        private void InvalidateEdgeState()
        {
            _latestEdgeResult = null;
            _latestArcRoi = null;
            _latestLineFittingResult = null;
            _latestCircleFittingResult = null;
            _latestEllipseFittingResult = null;
            _latestRectangleFittingResult = null;
            UpdateLineFittingResult(null);
            UpdateCircleFittingResult(null);
            UpdateEllipseFittingResult(null);
            UpdateRectangleFittingResult(null);
            RestoreDefaultEdgeGridColumns();
            _edgeResultsGrid.Rows.Clear();
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
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
            InvalidateEdgeState();
            // 畫新的邊緣 rect ROI = 改用邊緣 ROI，對稱於 ArcEditCheck：清掉殘留的圓弧帶並
            // 同步取消「互動編輯」勾選（BeginRect2Edit 已關掉 arc 編輯把手，這裡只補狀態/UI）。
            _latestArcRoi = null;
            if (_arcEditCheck.Checked) _arcEditCheck.Checked = false;
            _imageHelper.IsRoiMode = false;
            _edgeDrawRoiCheck.Checked = false;
            roiModeCheck.Checked = false;   // M2：template ROI 繪製完成後同步取消，避免洩漏到 edge 狀態
            ShowFittingOverlay();
            _imageHelper.BeginRect2Edit(_latestEdgeRoi.CenterRow, _latestEdgeRoi.CenterCol,
                _latestEdgeRoi.AngleRad, _latestEdgeRoi.Length1, _latestEdgeRoi.Length2, OnEdgeRect2Changed);
        }

        // 滑鼠互動編輯 rect2 的回呼：回寫數值框（度）與 _latestEdgeRoi，並使結果失效。
        private void OnEdgeRect2Changed(double cr, double cc, double phi, double l1, double l2)
        {
            _editCenterRow = cr;
            _editCenterCol = cc;
            _updatingEdgeRoiControls = true;
            try
            {
                _edgeScanLengthNumeric.Value = ClampNumericValue(_edgeScanLengthNumeric, (decimal)(l1 * 2.0));
                _edgeRoiWidthNumeric.Value = ClampNumericValue(_edgeRoiWidthNumeric, (decimal)(l2 * 2.0));
                _edgeAngleNumeric.Value = ClampNumericValue(_edgeAngleNumeric, (decimal)(phi * 180.0 / Math.PI));
            }
            finally
            {
                _updatingEdgeRoiControls = false;
            }

            _latestEdgeRoi = EdgeDetectionRoi.FromCenter(cr, cc, l1, l2, phi);
            InvalidateEdgeState();
        }

        // 弧形數值框變更：更新 _latestArcRoi 並即時重畫環帶預覽；若正在互動編輯，刷新把手位置。
        // 由 OnArcRoiChanged 回寫數值時以 _updatingArcControls 抑制，避免回授迴圈。
        private void OnArcNumericChanged(object sender, EventArgs e)
        {
            if (_updatingArcControls || _imageHelper == null || _imageHelper.CurrentImage == null)
                return;

            _latestArcRoi = BuildArcRoiFromControls();
            if (!_latestArcRoi.IsDefined)
                return;

            ShowFittingOverlay();
            if (_imageHelper.IsEditingArc)
            {
                _imageHelper.BeginArcEdit(_latestArcRoi.CenterRow, _latestArcRoi.CenterCol,
                    _latestArcRoi.Radius, _latestArcRoi.AngleStart, _latestArcRoi.AngleExtent,
                    _latestArcRoi.AnnulusRadius, OnArcRoiChanged);
            }
        }

        // 滑鼠互動編輯弧形的回呼：回寫六個數值框（角度轉度，起角正規化到 0..360）與 _latestArcRoi。
        private void OnArcRoiChanged(double cr, double cc, double radius, double a0, double extent, double annulus)
        {
            double a0Deg = a0 * 180.0 / Math.PI;
            a0Deg -= 360.0 * Math.Floor(a0Deg / 360.0); // 正規化到 [0, 360)，配合數值框 Minimum=0
            double extentDeg = extent * 180.0 / Math.PI;

            _updatingArcControls = true;
            try
            {
                _arcCenterRowNumeric.Value = ClampNumericValue(_arcCenterRowNumeric, (decimal)cr);
                _arcCenterColNumeric.Value = ClampNumericValue(_arcCenterColNumeric, (decimal)cc);
                _arcRadiusNumeric.Value = ClampNumericValue(_arcRadiusNumeric, (decimal)radius);
                _arcAnnulusNumeric.Value = ClampNumericValue(_arcAnnulusNumeric, (decimal)annulus);
                _arcAngleStartNumeric.Value = ClampNumericValue(_arcAngleStartNumeric, (decimal)a0Deg);
                _arcAngleExtentNumeric.Value = ClampNumericValue(_arcAngleExtentNumeric, (decimal)extentDeg);
            }
            finally
            {
                _updatingArcControls = false;
            }

            _latestArcRoi = new ArcMeasureRoi
            {
                CenterRow = cr,
                CenterCol = cc,
                Radius = radius,
                AngleStart = a0,
                AngleExtent = extent,
                AnnulusRadius = annulus
            };
        }

        // 互動編輯弧形開關：勾選 -> 以目前數值框內容進入拖曳編輯；取消 -> 離開編輯（保留數值與環帶）。
        private void ArcEditCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (_imageHelper == null) return;

            if (_arcEditCheck.Checked)
            {
                if (_imageHelper.CurrentImage == null)
                {
                    _updatingArcControls = true;
                    try { _arcEditCheck.Checked = false; } finally { _updatingArcControls = false; }
                    _edgeStatusLabel.Text = "Arc: 請先載入影像";
                    _edgeStatusLabel.ForeColor = Color.Red;
                    return;
                }

                _latestArcRoi = BuildArcRoiFromControls();
                if (!_latestArcRoi.IsDefined)
                {
                    _updatingArcControls = true;
                    try { _arcEditCheck.Checked = false; } finally { _updatingArcControls = false; }
                    _edgeStatusLabel.Text = "Arc ROI 無效: " + _latestArcRoi.ValidationError;
                    _edgeStatusLabel.ForeColor = Color.Red;
                    return;
                }

                // 進入互動編輯 = 改用圓弧 ROI，比照 Detect Arc 清掉殘留的邊緣 rect ROI，
                // 否則 DrawFittingLayers 仍會畫出舊的藍色 Rectangle2，與圓弧帶同時殘留在畫面上。
                _latestEdgeRoi = null;

                // 關閉 ROI 繪製模式，否則弧把手 hit-test 被 IsRoiMode 擋住，滑鼠按下會畫新框
                // 而非拖把手（M4）。取消勾選會經各自 handler 把 IsRoiMode 設為 false。
                if (_edgeDrawRoiCheck.Checked) _edgeDrawRoiCheck.Checked = false;
                if (roiModeCheck.Checked) roiModeCheck.Checked = false;
                _imageHelper.IsRoiMode = false;

                ShowFittingOverlay();
                _imageHelper.BeginArcEdit(_latestArcRoi.CenterRow, _latestArcRoi.CenterCol,
                    _latestArcRoi.Radius, _latestArcRoi.AngleStart, _latestArcRoi.AngleExtent,
                    _latestArcRoi.AnnulusRadius, OnArcRoiChanged);
            }
            else
            {
                _imageHelper.EndArcEdit();
            }
        }

        private static decimal ClampNumericValue(NumericUpDown numeric, decimal value)
        {
            if (value < numeric.Minimum) return numeric.Minimum;
            if (value > numeric.Maximum) return numeric.Maximum;
            return value;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _matchContour?.Dispose();
            _matchContour = null;
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

        // 把最近一次成功的 Ellipse 擬合結果（中心）附加到座標框。橢圓中心是「點」，
        // 故走既有 PointToPoint/PointToLine 量測（A3-D：EllipseCenterToX）。
        private void AppendEllipseButton_Click(object sender, EventArgs e)
        {
            EllipseFittingResult ellipse = _latestEllipseFittingResult;
            if (ellipse == null || !ellipse.Success)
            {
                measureResultLabel.Text = "尚無成功的 Ellipse 擬合結果可帶入（請先在 Edge Detection 分頁按 Fit Ellipse）。";
                return;
            }
            AppendCoordLine(ellipse.CenterRow, ellipse.CenterColumn);
        }

        // 把最近一次成功的 Rectangle 擬合結果（中心）附加到座標框（A3-D：RectCenterToX）。
        private void AppendRectButton_Click(object sender, EventArgs e)
        {
            RectangleFittingResult rect = _latestRectangleFittingResult;
            if (rect == null || !rect.Success)
            {
                measureResultLabel.Text = "尚無成功的 Rectangle 擬合結果可帶入（請先在 Edge Detection 分頁按 Fit Rectangle）。";
                return;
            }
            AppendCoordLine(rect.CenterRow, rect.CenterColumn);
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

            _imageHelper.EndRect2Edit();  // H2：接管畫面前結束殘留編輯把手
            _imageHelper.EndArcEdit();
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

            _imageHelper.EndRect2Edit();  // H2：接管畫面前結束殘留編輯把手
            _imageHelper.EndArcEdit();
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

            _imageHelper.EndRect2Edit();  // H2：接管畫面前結束殘留編輯把手
            _imageHelper.EndArcEdit();
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

            _imageHelper.EndRect2Edit();  // H2：接管畫面前結束殘留編輯把手
            _imageHelper.EndArcEdit();
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

            HXLDCont cont1 = null, cont2 = null;
            try
            {
                cont1 = EdgePointsToContour(contour1Points);
                cont2 = EdgePointsToContour(contour2Points);
                return _distanceMeasurer.MeasureContourMaxMin(cont1, cont2, parameters);
            }
            catch (HalconException ex)
            {
                return new DistanceMeasurementResult { ErrorMessage = "Halcon error: " + ex.Message };
            }
            finally
            {
                // 每次 ContourMaxMin 量測都 new 兩個 HXLDCont，須釋放避免非託管記憶體累積。
                cont1?.Dispose();
                cont2?.Dispose();
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
