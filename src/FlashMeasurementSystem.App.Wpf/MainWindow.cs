using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.ImageQuality;
using FlashMeasurementSystem.Domain.TemplateMatching;
using FlashMeasurementSystem.Halcon.EdgeDetection;
using FlashMeasurementSystem.Halcon.ImageQuality;
using FlashMeasurementSystem.Halcon.LineFitting;
using FlashMeasurementSystem.Halcon.TemplateMatching;
using FlashMeasurementSystem.Halcon.CircleFitting;
using FlashMeasurementSystem.Halcon.DistanceMeasurement;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using FlashMeasurementSystem.Halcon.AngleMeasurement;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.Calibration;
using FlashMeasurementSystem.Domain.MetrologyModel;
using FlashMeasurementSystem.Halcon.CoordinateSystem;
using FlashMeasurementSystem.Halcon.MetrologyModel;
using FlashMeasurementSystem.Application.HoleDetection;
using FlashMeasurementSystem.Application.HoleArrayDetection;
using FlashMeasurementSystem.Application.PinDetection;
using FlashMeasurementSystem.Halcon.HoleArrayDetection;
using FlashMeasurementSystem.Halcon.HoleDetection;
using FlashMeasurementSystem.Halcon.PinDetection;
using FlashMeasurementSystem.Infrastructure.Roi;
using FlashMeasurementSystem.Infrastructure.Tolerance;
using FlashMeasurementSystem.Infrastructure.Calibration;
using FlashMeasurementSystem.Reporting.Csv;
using FlashMeasurementSystem.Reporting.Pdf;
using FlashMeasurementSystem.Application.Reporting;
using FlashMeasurementSystem.Domain.Reporting;
using FlashMeasurementSystem.Domain.Tolerance;
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
        private readonly HalconDistanceMeasurer _distanceMeasurer = new HalconDistanceMeasurer();
        private readonly HalconAngleMeasurer _angleMeasurer = new HalconAngleMeasurer();
        private readonly HalconMetrologyModelRunner _metrologyRunner = new HalconMetrologyModelRunner();
        private readonly IHoleDetector<HImage> _holeDetector = new HalconHoleDetector();
        private readonly IPinDetector<HImage> _pinDetector = new HalconPinDetector();
        private readonly IHoleArrayDetector<HImage> _holeArrayDetector = new HalconHoleArrayDetector();
        private EdgeDetectionRoi _latestEdgeRoi;
        private double _editCenterRow, _editCenterCol;
        private EdgeResult _latestEdgeResult;
        private ArcMeasureRoi _latestArcRoi;
        private bool _updatingEdgeRoiControls;
        private bool _updatingArcControls;
        // 主 Detect 按鈕分流用：true=目前有效 ROI 是扇形(_latestArcRoi)，Detect 走
        // DetectEdgesInAnnularSector；false（預設）=矩形路徑，維持既有行為不變。
        // 由 OnSectorRoiCreated 設 true；畫新矩形 ROI（OnImageRoiSelected）設回 false。
        private bool _sectorRoiActive;

        // M3c-1：配方執行（Stage A：載入 + 設參考姿態 + 轉換並繪製跟隨工件的 ROI）
        private readonly HalconCoordinateMapper _coordinateMapper = new HalconCoordinateMapper();
        private readonly RecipeStore _recipeStore = new RecipeStore();
        private Recipe _loadedRecipe;
        private string _loadedRecipePath;
        private OpenFileDialog _openRecipeDialog;
        private double _lastMatchRow, _lastMatchCol, _lastMatchAngleDeg;
        private bool _hasMatch;
        // v16：產生 _lastMatch* 的模板檔名。姿態只有在同一個 .shm 下才可與配方的參考姿態
        // 相比較，故必須隨姿態一起記住並在 Run Recipe 前比對（見 EnsureMatchTemplateMatchesRecipe）。
        private string _lastMatchTemplateId;
        // 匹配輪廓快取：匹配姿態變更時算一次（transform_shape_model_contours），之後每次
        // pan/zoom/redraw 直接 DispObj，避免每個 redraw 都重算造成卡頓。生命週期由 RefreshMatchContour 管理。
        private HObject _matchContour;
        // B1：配方執行引擎與其相依（量測 + 公差判定 + 校正載入）
        private readonly ToleranceJudger _judger = new ToleranceJudger();
        private readonly CalibrationStore _calibrationStore = new CalibrationStore();
        private readonly CsvMeasurementReportWriter _reportWriter = new CsvMeasurementReportWriter();
        // 一鍵量測的附加輸出：PDF 報表（含視窗截圖）。CSV 行為不受影響。
        private readonly IMeasurementPdfReportWriter _pdfReportWriter = new PdfMeasurementReportWriter();
        private RecipeRunner _recipeRunner;
        private MeasurementWorkflow _workflow;
        private CheckBox _skipIqcCheckBox;
        private ToolTip _toolTip;

        private readonly FlashMeasurementSystem.Halcon.DxfComparison.HalconDxfContourComparer _dxfComparer
            = new FlashMeasurementSystem.Halcon.DxfComparison.HalconDxfContourComparer();

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            NormalizeTemplateMatchingLayout();
            SetupToolTips();

            // 操作員／工程師模式切換選單與操作員面板（見 MainWindow.ViewMode.cs）。
            BuildViewModeMenu();
            BuildOperatorPanel();
            ApplyViewMode();

            _imageHelper = new HWindowControlHelper(hWindowControl);
            _imageHelper.MouseMoved += OnImageMouseMoved;
            _imageHelper.RoiSelected += OnImageRoiSelected;

            // 配方執行引擎：以既有 adapters 注入（邊緣 + 圓/線擬合 + 公差 + 座標映射）。
            _recipeRunner = new RecipeRunner(_edgeDetector, _circleFitter, _lineFitter, _distanceMeasurer, _angleMeasurer, _judger, _coordinateMapper, _metrologyRunner, _holeDetector, _pinDetector, _holeArrayDetector);
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

            // 三個分頁各自的動作工具列（比照原 topToolbar 以程式碼建構，不動 Designer.cs）。
            // 分頁順序即工作流程順序，故按鈕依「屬於哪一步」分配，而非全部堆在同一列：
            //   ② 配方 = 定義要量什麼；③ 量測 = 執行與驗證；④ 診斷 = 出問題時才用。
            // 校正不在任何分頁——它是整台機器的設定、一台機器只做一次，放在分頁裡會讓人
            // 以為每次建模板都得重做，故移到選單列（見 MainWindow.ViewMode.cs 的「設定」選單）。

            // ── ② 配方 ──
            var recipeToolbar = MakeTabToolbar();
            var loadRecipeButton = new Button { Text = "載入配方…", Width = 90, Height = 26 };
            loadRecipeButton.Click += LoadRecipeButton_Click;
            var setRefButton = new Button { Text = "設定參考姿態", Width = 100, Height = 26 };
            setRefButton.Click += SetRefPoseButton_Click;
            var editRecipeButton = new Button { Text = "編輯配方…", Width = 90, Height = 26 };
            editRecipeButton.Click += OpenRecipeEditor;
            var metrologyButton = new Button { Text = "2D 量測模型…", Width = 100, Height = 26 };
            metrologyButton.Click += OpenMetrologyModelEditor;
            recipeToolbar.Controls.Add(loadRecipeButton);
            recipeToolbar.Controls.Add(setRefButton);
            recipeToolbar.Controls.Add(editRecipeButton);
            recipeToolbar.Controls.Add(metrologyButton);
            recipeTabPage.Controls.Add(recipeToolbar);

            // ── ③ 量測 ──
            var runToolbar = MakeTabToolbar();
            // 載入影像在「① 建立模板」分頁也有一顆（供 Run Matching 用）。共用同一個 handler，
            // 兩個入口不會漂移；量測分頁需要它，否則得跳回第一個分頁才能換圖。
            var loadImageButton = new Button { Text = "載入影像…", Width = 90, Height = 26 };
            loadImageButton.Click += LoadTestImageButton_Click;
            var runRecipeButton = new Button { Text = "執行配方", Width = 84, Height = 26 };
            runRecipeButton.Click += RunRecipeButton_Click;
            var oneClickButton = new Button { Text = "一鍵量測", Width = 84, Height = 26 };
            oneClickButton.Click += OneClickMeasureButton_Click;
            _skipIqcCheckBox = new CheckBox
            {
                Text = "略過IQC",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(4, 6, 4, 0)
            };
            runToolbar.Controls.Add(loadImageButton);
            runToolbar.Controls.Add(runRecipeButton);
            runToolbar.Controls.Add(oneClickButton);
            runToolbar.Controls.Add(_skipIqcCheckBox);
            measurementTabPage.Controls.Add(runToolbar);

            // ── ④ 診斷 ──
            var diagToolbar = MakeTabToolbar();
            var dxfButton = new Button { Text = "DXF 比對…", Width = 90, Height = 26 };
            dxfButton.Click += OpenDxfComparisonForm;
            diagToolbar.Controls.Add(dxfButton);
            edgeDetectionTabPage.Controls.Add(diagToolbar);

            _toolTip.SetToolTip(loadRecipeButton, "載入量測配方 (.zcp)");
            _toolTip.SetToolTip(setRefButton, "把目前的匹配姿態設為本配方的參考姿態，並記住所用模板");
            _toolTip.SetToolTip(editRecipeButton, "開啟配方編輯器，新增或修改量測工具");
            _toolTip.SetToolTip(metrologyButton, "定義本配方的 2D 量測模型");
            _toolTip.SetToolTip(loadImageButton, "載入待測影像");
            _toolTip.SetToolTip(runRecipeButton, "以目前影像執行配方量測（不產報表；需要姿態時會自行匹配）");
            _toolTip.SetToolTip(oneClickButton, "完整流程：影像品質 → 模板匹配 → 量測 → 判定 → CSV/PDF 報表");
            _toolTip.SetToolTip(_skipIqcCheckBox,
                "略過影像品質檢查（僅供合成影像測試）。只在工程模式生效，切到操作員模式會自動取消勾選。");
            _toolTip.SetToolTip(dxfButton, "開啟 DXF/CAD 輪廓度比對面板");

            // WinForms docking 依「反 z-order」處理：最後面(SendToBack)的先 dock 佔邊，
            // 最前面的後 dock 佔剩餘。故 Top 工具列要在後、Fill 內容要在前。
            recipeToolbar.SendToBack();
            runToolbar.SendToBack();
            diagToolbar.SendToBack();
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
                // 經 SetBaseWindowTitle 而非直接寫 Text：標題列尾端還要接模式後綴，
                // 直接指派會把 [工程模式] 抹掉（見 MainWindow.ViewMode.cs）。
                SetBaseWindowTitle($"Flash Measurement System - Template Matching (Halcon {version.S})");
            }
            catch (HalconException)
            {
                SetBaseWindowTitle("Flash Measurement System - Template Matching (Halcon unavailable)");
            }

            // 還原上次使用的配方（見 MainWindow.ViewMode.cs）：操作員開機後即可直接載圖量測。
            // 須在 UpdateEmptyState 之前，空狀態引導才會反映「配方已載入」。
            TryRestoreLastRecipe();

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
            _toolTip.SetToolTip(_sectorDrawCheck, "勾選後從圓心往外拖拉出扇形量測區，放開後自動進入把手微調");
            _toolTip.SetToolTip(_edgeResultsGrid, "Detected edge points (Row, Col, Amplitude, Distance)");
            _toolTip.SetToolTip(_edgeStatusLabel, "Edge detection status — PASS (green) or FAIL (red)");

            // ── Measurement ──
            _toolTip.SetToolTip(measurementPixelSizeXNumeric, "Pixel size in X direction (µm/pixel)");
            _toolTip.SetToolTip(measurementPixelSizeYNumeric, "Pixel size in Y direction (µm/pixel)");
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
            // 重置顏色：否則上一次配方 NG(紅)/OK(綠) 會殘留並染色後續無關文字。
            // 走 SetMeasurementResult 一併清掉操作員結果面，否則換圖後操作員畫面
            // 仍留著上一張影像的 OK/NG 文字（正是本方法要防的誤判）。
            SetMeasurementResult(string.Empty, SystemColors.ControlText);

            // 換圖必須清掉匹配姿態，否則 Run Recipe 守門（HasReferencePose && !_hasMatch）
            // 會放行，並用前一張影像的 _lastMatch* 對新影像做 ROI 變換，畫出錯誤的 OK/NG。
            // （DisplayImage 已清 persistent overlay，這裡只需清 C# 狀態。）
            _hasMatch = false;
            _lastMatchRow = 0;
            _lastMatchCol = 0;
            _lastMatchAngleDeg = 0;
            _lastMatchTemplateId = null;
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

            // 只要還沒有影像就顯示引導。原本的條件是「無影像且無配方」，在配方只能手動載入時
            // 沒問題；但開機自動還原配方後，沿用該條件會讓操作員一開機就看到全黑畫面、
            // 沒有任何提示。影像格此時是空的，引導不會蓋住任何已載入內容。
            emptyStateGuideLabel.Visible = !hasImage;

            if (!hasImage)
            {
                emptyStateGuideLabel.Text = hasRecipe
                    ? "配方已就緒\n① 載入影像（Load Image）\n② 按一鍵量測（One-Click）"
                    : "① 載入影像（Load Image）\n② 載入或建立配方（Load / Edit Recipe）\n③ 按一鍵量測（One-Click）";
            }
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

            // _imageHelper 於 OnLoad 才建立；比照 EdgeDrawRoiCheck_CheckedChanged 防呆，
            // 避免 Designer 若在 InitializeComponent 期間觸發此事件時 NRE。
            if (_imageHelper == null) return;
            // 開啟矩形繪製即取消扇形繪製 checkbox（模式互斥）；其 CheckedChanged 會 disarm
            // IsSectorMode 並清 pending callback，同時讓 checkbox 視覺與模式一致。
            if (roiModeCheck.Checked && _sectorDrawCheck.Checked) _sectorDrawCheck.Checked = false;
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
            catch (Exception ex)
            {
                // backstop：.shm 寫入/讀取的 IO/權限例外（唯讀樣板夾、檔案鎖定等）非上述型別，
                // 否則會冒出未處理例外崩潰對話框而非可讀訊息。
                MessageBox.Show(ex.Message, "Error");
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
                    // v16：一併記住是哪個模板量出這個姿態。姿態只有在同一個 .shm 下才可與
                    // 配方的參考姿態相比較，Run Recipe 會用這個值把關（見 EnsureMatchTemplateMatchesRecipe）。
                    _lastMatchTemplateId = Path.GetFileName(templateFile.FullPath);
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
                    // 匹配失敗必須清掉殘留的成功姿態，否則 Run Recipe 守門（HasReferencePose && !_hasMatch）
                    // 會放行，並用前一次成功的 _lastMatch* 對現影像做 ROI 變換，畫出錯誤的 OK/NG。
                    ResetMatchPose();
                    _imageHelper.ClearOverlay();
                    // 這裡是匹配失敗最先被看到的地方，直接點出最常見的原因（模板選錯），
                    // 否則使用者往下走會在 Run Recipe/Set Ref 得到「請先執行模板匹配」而更困惑。
                    matchResultTextBox.Text = result.Message
                        + "\r\n找不到工件：請確認所選模板與目前影像的工件相符。"
                        + (_loadedRecipe != null && !string.IsNullOrEmpty(_loadedRecipe.TemplateModelId)
                            ? "\r\n本配方指定的模板：" + _loadedRecipe.TemplateModelId
                            : "");
                }
            }
            catch (Exception ex)
            {
                // 涵蓋 HalconException 及 IO/權限等非 Halcon 例外（否則會冒出未處理例外崩潰對話框）。
                ResetMatchPose();
                _imageHelper.ClearOverlay();
                matchResultTextBox.Text = $"Matching failed: {ex.Message}";
            }
            finally
            {
                Cursor = Cursors.Default;
                ClearProgress();
            }
        }

        // 清掉已保存的匹配姿態（匹配失敗/例外時呼叫），避免 Run Recipe 守門用舊姿態變換 ROI。
        // 換圖路徑（DisplayImage）另有等價重置。
        private void ResetMatchPose()
        {
            _hasMatch = false;
            _lastMatchRow = 0;
            _lastMatchCol = 0;
            _lastMatchAngleDeg = 0;
            _lastMatchTemplateId = null;
            RefreshMatchContour();
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
                // 與一鍵量測用同一組門檻（配方未載入時才退回全域預設），否則會出現
                // 「單獨檢查說 PASS、一鍵量測卻 FAIL」這種難以追查的不一致。
                ImageQualityThresholds thresholds = _loadedRecipe != null
                    ? _loadedRecipe.EffectiveIqcThresholds()
                    : ImageQualityThresholds.Default();
                var result = _iqc.Check(_imageHelper.CurrentImage, thresholds);
                SetIqcResult(result.Pass
                    ? $"PASS | Mean:{result.MeanBrightness:F1} Sat:{result.SaturationRatio:F2}% Blur:{result.BlurScore:F1} Contrast:{result.Contrast:F1}"
                    : $"FAIL | {result.Message}",
                    result.Pass ? System.Drawing.Color.Green : System.Drawing.Color.Red);
            }
            catch (HalconException ex)
            {
                SetIqcResult($"IQC error: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        private void RunEdgeDetectionButton_Click(object sender, EventArgs e)
        {
            if (_imageHelper.CurrentImage == null)
            {
                SetEdgeStatus(false, "Please load an image first.");
                return;
            }

            // 扇形 ROI 分流：目前有效 ROI 是扇形（Task 3 拖曳建立/編輯）時，主 Detect 按鈕改走
            // 扇形環帶量測（DetectEdgesInAnnularSector），結果一樣寫回 _latestEdgeResult，
            // 讓既有 Fit Line/Circle/Ellipse/Rectangle 按鈕不需任何修改即可使用。
            if (_sectorRoiActive && _latestArcRoi != null && _latestArcRoi.IsDefined)
            {
                Cursor = Cursors.WaitCursor;
                SetProgress("扇形邊緣檢測中…");
                try
                {
                    EdgeDetectionParameters sectorParameters = CreateEdgeDetectionParameters();
                    EdgeResult sectorResult = _edgeDetector.DetectEdgesInAnnularSector(
                        _imageHelper.CurrentImage, _latestArcRoi, sectorParameters);

                    // 比照 DetectArcButton_Click：確保沒有殘留的矩形 ROI 藍框跟扇形帶同時畫出。
                    _latestEdgeRoi = null;
                    _latestEdgeResult = sectorResult;

                    RestoreDefaultEdgeGridColumns();
                    BindEdgeResult(sectorResult);
                    SetEdgeStatus(sectorResult.Success, sectorResult.Success
                        ? string.Format("Sector edges: {0} found", sectorResult.EdgePoints.Count)
                        : sectorResult.ErrorMessage);
                    ShowFittingOverlay();
                }
                catch (HalconException ex)
                {
                    InvalidateEdgeState();
                    ShowFittingOverlay();
                    SetEdgeStatus(false, "Edge detection failed [Halcon " + ex.GetErrorCode() + "]: " + ex.Message);
                }
                catch (Exception ex)
                {
                    InvalidateEdgeState();
                    ShowFittingOverlay();
                    SetEdgeStatus(false, "Edge detection failed (unexpected " + ex.GetType().Name + "): " + ex.Message);
                }
                finally
                {
                    Cursor = Cursors.Default;
                    ClearProgress();
                }
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
                // 切線弧偵測 (DetectEdgesOnArc) 是與扇形不同的演算法：取得弧 ROI 擁有權，
                // 使 overlay 回到 DrawArcBand、主 Detect 回到矩形路徑（比照 OnImageRoiSelected
                // 切換 ROI 型別時的重置）。要再跑扇形量測，重新拖曳扇形即可。
                _sectorRoiActive = false;
                _latestEdgeRoi = null;
                _imageHelper.EndRect2Edit();   // 結束殘留的邊緣 rect2 編輯把手（M5），避免與弧帶並存
                _latestEdgeResult = result;

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
            // ClearRoi() 內部把 IsRoiMode/IsSectorMode 設為 false，三個繪製 checkbox 都要同步，
            // 否則 checkbox 仍顯示勾選、實際卻已不能畫 ROI。
            _edgeDrawRoiCheck.Checked = false;
            roiModeCheck.Checked = false;
            if (_sectorDrawCheck.Checked) _sectorDrawCheck.Checked = false;
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            ClearFittingState();
        }

        private void EdgeDrawRoiCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (_imageHelper != null)
            {
                if (_edgeDrawRoiCheck.Checked)
                {
                    roiModeCheck.Checked = false;
                    // 開啟矩形繪製即取消扇形繪製 checkbox（模式互斥）；其 CheckedChanged 會
                    // disarm IsSectorMode 並清 callback，同時讓 checkbox 視覺與模式一致。
                    if (_sectorDrawCheck.Checked) _sectorDrawCheck.Checked = false;
                }

                _imageHelper.IsRoiMode = _edgeDrawRoiCheck.Checked;
            }
        }

        // 切換分頁時結束殘留的編輯/繪製模式。影像區跨分頁共用，前一分頁留下的把手或
        // 正在進行的 ROI draw 在換頁後會讓操作困惑（還有 pending RequestRoi callback 風險）。
        private void FeatureTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_imageHelper == null) return;
            // 一次解除所有互動模式（含扇形繪製的 pending callback），關閉先前已知的
            // tab-switch stale-callback 缺口。
            _imageHelper.DisarmInteractiveModes();
            if (_edgeDrawRoiCheck != null && _edgeDrawRoiCheck.Checked) _edgeDrawRoiCheck.Checked = false;
            if (roiModeCheck != null && roiModeCheck.Checked) roiModeCheck.Checked = false;
            if (_sectorDrawCheck != null && _sectorDrawCheck.Checked) _sectorDrawCheck.Checked = false;
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

            LoadRecipeFromPath(_openRecipeDialog.FileName);
            UpdateEmptyState();
        }

        // 以「目前的模板匹配姿態」設為配方的參考姿態，並存回原檔。
        // 這定義了 ROI 的 reference frame：之後在其他影像匹配時，ROI 依當前姿態相對此參考轉換。
        private void SetRefPoseButton_Click(object sender, EventArgs e)
        {
            if (_loadedRecipe == null) { MessageBox.Show("請先載入配方 (.zcp)。", "Info"); return; }
            if (!_hasMatch)
            {
                // 同 Run Recipe：匹配失敗後這裡看到的也是「沒有姿態」，訊息須點出這個可能。
                MessageBox.Show(
                    "請先在參考影像上執行模板匹配（Inspection 分頁的 Run Matching）。\r\n\r\n" +
                    "若剛才已執行過，代表匹配失敗——最常見的原因是所選模板與目前工件不符。",
                    "需要模板匹配", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _loadedRecipe.RefRow = _lastMatchRow;
            _loadedRecipe.RefCol = _lastMatchCol;
            _loadedRecipe.RefAngleRad = _lastMatchAngleDeg * Math.PI / 180.0;
            _loadedRecipe.HasReferencePose = true;
            // v16：參考姿態是「某個模板」量出來的，模板必須跟姿態一起存。這裡是唯一正確的
            // 寫入點——按下 Set Ref 的當下，剛剛才用某個模板匹配成功。
            _loadedRecipe.TemplateModelId = _lastMatchTemplateId ?? "";

            try
            {
                if (!string.IsNullOrEmpty(_loadedRecipePath))
                {
                    _recipeStore.Save(_loadedRecipe, _loadedRecipePath);
                }
                SetMeasurementResult(string.Format(CultureInfo.InvariantCulture,
                    "參考姿態已設定並存檔：Row={0:F2} Col={1:F2} Angle={2:F2}°（模板：{3}）",
                    _loadedRecipe.RefRow, _loadedRecipe.RefCol, _lastMatchAngleDeg,
                    string.IsNullOrEmpty(_loadedRecipe.TemplateModelId) ? "未記錄" : _loadedRecipe.TemplateModelId),
                    SystemColors.ControlText);
                UpdateOperatorRecipeInfo();
            }
            catch (Exception ex)
            {
                SetMeasurementResult("參考姿態存檔失敗: " + ex.Message, SystemColors.ControlText);
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
            // 姿態只對「需要姿態變換的 1D 工具」有意義。純 2D 量測模型（無 1D 工具）
            // 不需匹配：未匹配時其標稱幾何以絕對影像座標量測（Pass 3 不套 reference_system/align）。
            if (_loadedRecipe.HasReferencePose && _loadedRecipe.Tools.Count > 0)
            {
                if (!EnsureMatchPoseForRecipe()) return;
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

                    // v10：扇形 ROI 的 circle 工具畫量測扇形帶（PlacedArc；成功/失敗都畫，顯示環帶位置）。
                    // 矩形 circle 的 r.Roi 非 null 走上面橘框；扇形 circle 的 r.Roi 為 null，改在此畫扇形帶。
                    if (r.ToolType == "circle" && r.PlacedArc != null)
                    {
                        an.DrawSectorRoi(r.PlacedArc.CenterRow, r.PlacedArc.CenterCol, r.PlacedArc.Radius,
                            r.PlacedArc.AnnulusRadius, r.PlacedArc.AngleStart, r.PlacedArc.AngleExtent);
                        an.DrawText(r.Name ?? string.Empty, (int)r.PlacedArc.CenterRow, (int)r.PlacedArc.CenterCol, "orange");
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
                    else if (r.ToolType == "pin_pitch" && r.PinPitch != null)
                    {
                        // 引腳間距：rect2 ROI 橘框 + 橘名稱已由上方通用 r.Roi 分支畫（同 circle/line 慣例）。
                        // 此處補畫各引腳質心十字（依判定上色）、首→末質心連線（引腳排列主軸）、
                        // 與判定/數值文字。Pins 為影像座標，引腳數少故全畫（不抽樣）。
                        string pinColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                        var pins = r.PinPitch.Pins;
                        if (pins != null && pins.Count > 0)
                        {
                            foreach (var p in pins)
                                an.DrawCross(p.Row, p.Col, 12, pinColor);
                            if (pins.Count >= 2)
                                an.DrawLine(pins[0].Row, pins[0].Col,
                                            pins[pins.Count - 1].Row, pins[pins.Count - 1].Col, pinColor);
                        }
                        // 判定/數值文字錨在 ROI 中心上方一段（避開橘名稱標籤與引腳本體），依判定上色。
                        if (r.Roi != null)
                            an.DrawText(r.ValueText ?? string.Empty,
                                (int)r.Roi.Row - 24, (int)r.Roi.Col, pinColor);
                    }
                    else if (r.ToolType == "hole_array" && r.HoleArray != null)
                    {
                        // 孔陣列：rect2 ROI 橘框 + 橘名稱已由上方通用 r.Roi 分支畫（同 pin_pitch 慣例）。
                        // 此處補畫每孔「量測到的孔徑圓」（半徑 = DiameterPx/2）與孔心十字，讓孔徑大小
                        // 直接可視化（此工具重點），再加判定/數值文字。Holes 為影像座標。
                        // 孔數少（網格）故原則上全畫，仍守 MaxOverlayCrosses 上限做防呆抽樣。
                        string holeColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                        var holes = r.HoleArray.Holes;
                        if (holes != null && holes.Count > 0)
                        {
                            int hStep = holes.Count <= MaxOverlayCrosses
                                ? 1 : (int)Math.Ceiling((double)holes.Count / MaxOverlayCrosses);
                            for (int hi = 0; hi < holes.Count; hi += hStep)
                            {
                                var h = holes[hi];
                                if (h.DiameterPx > 0)
                                    an.DrawCircle(h.Row, h.Col, h.DiameterPx / 2.0, holeColor);
                                an.DrawCross(h.Row, h.Col, 8, holeColor);
                            }
                        }
                        // 缺孔位置畫洋紅大十字（比照 gear 缺齒 / PCD 缺孔的提示慣例）；
                        // 否則缺孔處只是一片留白，操作員得自己用眼睛找是哪一格少了。
                        if (r.HoleArray.MissingNodes != null)
                        {
                            foreach (var m in r.HoleArray.MissingNodes)
                                an.DrawCross(m.Row, m.Col, 18, "magenta");
                        }
                        // 判定/數值文字錨在 ROI 中心上方一段（避開橘名稱標籤與孔本體），依判定上色。
                        if (r.Roi != null)
                            an.DrawText(r.ValueText ?? string.Empty,
                                (int)r.Roi.Row - 24, (int)r.Roi.Col, holeColor);
                    }

                    // 結果表值欄由 DrawResultTable 統一裁到欄寬（過長截斷加「…」），任何工具皆不溢到判定欄。
                    rows.Add(new OverlayResultRow { Name = r.Name, ValueText = r.ValueText, IsOk = r.IsOk });
                }

                // Arc 卡尺結果：Roi 刻意留 null（見上方 Pass 1.2 註解），畫框那段不會經過，
                // 故弧本體/邊點十字/名稱標籤需在此自行補畫。
                foreach (ToolRunResult r in results)
                {
                    if (r == null || r.ToolType != "arc" || r.PlacedArc == null) continue;
                    string arcColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                    ArcMeasureRoi a = r.PlacedArc;
                    string pointOrder = a.AngleExtent > 0 ? "positive" : "negative";
                    an.DrawArc(a.CenterRow, a.CenterCol, a.Radius,
                        a.AngleStart, a.AngleStart + a.AngleExtent, pointOrder, arcColor);

                    // 邊點十字：等間距抽樣至多 MaxOverlayCrosses 個（同 metrology/1D 慣例）。
                    int aTotal = Math.Min(r.ArcEdgeRows.Count, r.ArcEdgeCols.Count);
                    if (aTotal > 0)
                    {
                        int aStep = aTotal <= MaxOverlayCrosses ? 1 : (int)Math.Ceiling((double)aTotal / MaxOverlayCrosses);
                        for (int ai = 0; ai < aTotal; ai += aStep)
                            an.DrawCross(r.ArcEdgeRows[ai], r.ArcEdgeCols[ai], 10, arcColor);
                    }

                    // 名稱標籤：錨在弧心（比照 Roi 分支把名稱標在 Roi.Row/Col 的慣例）。
                    an.DrawText(r.Name ?? string.Empty, (int)a.CenterRow, (int)a.CenterCol, arcColor);
                }

                // 齒輪工具結果：與 arc 同樣 Roi 刻意留 null（見 RecipeRunner Pass 1.3），畫框那段不會經過。
                // 幾何在 PlacedArc（已 pose 轉換）。畫量測環帶 + 每齒中心十字 + 缺齒提示（洋紅大十字）+ 名稱/數值。
                // 【刻意分工，勿當 parity bug 統一】主頁（生產視圖）畫「齒中心」結果：每齒一個十字＝齒數結果，
                // 缺齒以洋紅標出。編輯器試測（RecipeEditor.OnTrialMeasure 齒輪分支）則刻意改畫「原始邊點」，
                // 供操作者調 ROI/Sigma/Threshold 時確認每對進/出齒被乾淨抓到。兩處視圖不同是設計，不是缺陷。
                // 角度→(row,col) 採全專案慣例 row = cr + R·sinθ、col = cc + R·cosθ（同 GearToothAnalyzer 的 atan2(row-cr, col-cc)
                // 與 DrawAngle/DrawArcBand），確保標記落在環帶上。齒角度為「度」，先轉弧度。
                foreach (ToolRunResult r in results)
                {
                    if (r == null || r.ToolType != "gear" || r.PlacedArc == null) continue;
                    string gearColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                    ArcMeasureRoi a = r.PlacedArc;
                    an.DrawArcBand(a.CenterRow, a.CenterCol, a.Radius, a.AngleStart, a.AngleExtent, a.AnnulusRadius);
                    if (r.Gear != null && r.Gear.Success)
                    {
                        foreach (var tooth in r.Gear.Teeth)
                        {
                            double th = tooth.CenterAngleDeg * Math.PI / 180.0;
                            an.DrawCross(a.CenterRow + a.Radius * Math.Sin(th), a.CenterCol + a.Radius * Math.Cos(th), 12, gearColor);
                        }
                        foreach (double hintDeg in r.Gear.MissingToothHintsDeg)
                        {
                            double th = hintDeg * Math.PI / 180.0;
                            an.DrawCross(a.CenterRow + a.Radius * Math.Sin(th), a.CenterCol + a.Radius * Math.Cos(th), 18, "magenta");
                        }
                    }
                    // 影像上只標工具名（比照 arc/pcd），避免三項長字串疊在環帶/齒中心上；數值看左上結果表 HUD。
                    an.DrawText(r.Name ?? string.Empty, (int)a.CenterRow, (int)a.CenterCol, gearColor);
                }

                // PCD 工具結果：Roi 刻意留 null（同 arc/gear），畫框那段不會經過。畫量測環帶 + 擬合節圓
                // + 各孔中心十字 + 缺孔提示（洋紅）+ 名稱/數值。偵測到的孔用原始質心（hole.Row/Col）畫十字；
                // 僅缺孔提示需由角度→(row,col)（row=cr+R·sinθ、col=cc+R·cosθ）換算落到節圓上。
                foreach (ToolRunResult r in results)
                {
                    if (r == null || r.ToolType != "pcd" || r.PlacedArc == null) continue;
                    string pcdColor = r.IsOk == true ? "green" : (r.IsOk == false ? "red" : "yellow");
                    ArcMeasureRoi a = r.PlacedArc;
                    an.DrawArcBand(a.CenterRow, a.CenterCol, a.Radius, a.AngleStart, a.AngleExtent, a.AnnulusRadius);
                    if (r.Pcd != null && r.Pcd.Success)
                    {
                        var pcd = r.Pcd;
                        double pcdRadiusPx = pcd.PcdPx / 2.0;
                        an.DrawCircle(pcd.CenterRow, pcd.CenterCol, pcdRadiusPx, pcdColor);   // 擬合節圓
                        foreach (var hole in pcd.Holes)
                            an.DrawCross(hole.Row, hole.Col, 12, pcdColor);
                        foreach (double hintDeg in pcd.MissingHoleHintsDeg)
                        {
                            double th = hintDeg * Math.PI / 180.0;
                            an.DrawCross(pcd.CenterRow + pcdRadiusPx * Math.Sin(th),
                                         pcd.CenterCol + pcdRadiusPx * Math.Cos(th), 18, "magenta");
                        }
                    }
                    // 影像上只標工具名（比照 arc 分支），避免四項長字串疊在環帶/節圓上；
                    // 數值看左上結果表 HUD。名稱錨在弧心（節圓中心為空，不會蓋到孔/缺孔標記）。
                    an.DrawText(r.Name ?? string.Empty, (int)a.CenterRow, (int)a.CenterCol, pcdColor);
                }
                an.DrawResultTable(rows);
            });

            int okCount = 0, ngCount = 0;
            foreach (ToolRunResult r in results)
            {
                if (r.IsOk == true) okCount++;
                else if (r.IsOk == false) ngCount++;
            }
            SetMeasurementResult(string.Format(CultureInfo.InvariantCulture,
                "配方 '{0}'：{1} 工具，OK {2} / NG {3}（pixel size：{4}）",
                _loadedRecipe != null ? _loadedRecipe.Name : "", results.Count, okCount, ngCount, pixelSizeSource),
                ngCount > 0 ? System.Drawing.Color.DarkRed
                    : (okCount > 0 ? System.Drawing.Color.DarkGreen : System.Drawing.SystemColors.ControlText));
            SetResultBanner(okCount, ngCount, true);
        }

        // Pixel size 來源：配方 CalibrationProfileId 有設且檔案存在 → 用校正檔；否則退回量測分頁。
        private void ResolvePixelSize(out double pxUmX, out double pxUmY, out string source)
        {
            pxUmX = (double)measurementPixelSizeXNumeric.Value;
            pxUmY = (double)measurementPixelSizeYNumeric.Value;
            source = "量測分頁";
            // 試測路徑允許尚未載入配方（暫態單工具配方），此時 _loadedRecipe 為 null，
            // 直接沿用量測分頁數值，不查校正檔。
            if (_loadedRecipe != null && !string.IsNullOrEmpty(_loadedRecipe.CalibrationProfileId))
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

                // v16：模板優先取自配方（參考姿態就是它量出來的）；舊配方才退回下拉選單。
                string templatePath = ResolveTemplatePath(_loadedRecipe, out string templateError);
                if (templateError != null)
                {
                    MessageBox.Show(this, templateError, "模板", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 覆蓋了畫面上的選取就要說出來。默默改用另一個模板即使結果是對的，
                // 使用者仍會以為跑的是他選的那個——選錯模板卻一路 PASS，最容易讓人誤以為系統壞了。
                string usedTemplateId = string.IsNullOrEmpty(templatePath) ? null : Path.GetFileName(templatePath);
                string selectedTemplateId = SelectedTemplateIdOrNull();
                string templateNote = "";
                if (usedTemplateId != null && selectedTemplateId != null
                    && !string.Equals(usedTemplateId, selectedTemplateId, StringComparison.OrdinalIgnoreCase))
                {
                    templateNote = string.Format(CultureInfo.InvariantCulture,
                        " | 模板：已用配方指定的 {0}（畫面選取為 {1}，已忽略）",
                        usedTemplateId, selectedTemplateId);
                }

                string reportDir = Path.Combine(ResolveDataDir(), "reports");

                WorkflowResult wfResult = _workflow.RunOnce(
                    _loadedRecipe, _imageHelper.CurrentImage,
                    pxUmX, pxUmY,
                    reportDir,
                    templatePath, null, null,
                    // 略過 IQC 只在工程模式生效。此旗標用途是拿合成影像測試，
                    // 若讓它在操作員模式也生效，工程師忘了取消勾選就會讓產線靜默跳過
                    // 影像品質把關——那正是最不該被跳過的一關。
                    _viewMode == ViewMode.Engineering && _skipIqcCheckBox != null && _skipIqcCheckBox.Checked,
                    out System.Collections.Generic.List<ToolRunResult> results,
                    out System.Collections.Generic.List<ItemJudgment> judgments);

                // 同步本次匹配狀態到 MainWindow 欄位，避免 DrawRecipeResults 畫出上一次匹配的殘留綠框。
                // 無參考姿態的配方 HasMatch=false → 不畫匹配輪廓。
                _hasMatch = wfResult.HasMatch;
                if (wfResult.HasMatch)
                {
                    _lastMatchRow = wfResult.MatchRow;
                    _lastMatchCol = wfResult.MatchCol;
                    _lastMatchAngleDeg = wfResult.MatchAngleDeg;
                    // v16：一鍵是用上面解析出的 templatePath 去匹配的，記錄它才能與 _lastMatch* 一致。
                    // 漏掉會留下前一次 Run Matching 的舊值，讓後續 Run Recipe 的模板比對誤報不一致。
                    _lastMatchTemplateId = string.IsNullOrEmpty(templatePath)
                        ? null : Path.GetFileName(templatePath);
                }
                else
                {
                    _lastMatchTemplateId = null;
                }
                RefreshMatchContour(); // 依本次匹配姿態更新快取輪廓（無匹配則清空）

                DrawRecipeResults(results, pixelSizeSource);

                // overlay 已於 DrawRecipeResults 內同步畫完（SetPersistentOverlayAction 會呼叫 Redraw），
                // 此時視窗內容才是完整的標註影像 → 截圖 → 產 PDF。
                string pdfInfo = WritePdfReport(wfResult, judgments, reportDir,
                    pxUmX, pxUmY, pixelSizeSource);

                string csvInfo = !string.IsNullOrEmpty(wfResult.ReportPath)
                    ? " | CSV: " + wfResult.ReportPath
                    : "";
                AppendMeasurementResult(string.Format(CultureInfo.InvariantCulture,
                    " | 一鍵：{0} OK {1}/NG {2}{3}{4}{5}{6}",
                    wfResult.AllOk ? "PASS" : "FAIL", wfResult.OkCount, wfResult.NgCount,
                    csvInfo,
                    pdfInfo,
                    !wfResult.Success ? " (" + wfResult.Message + ")" : "",
                    templateNote));
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
                ClearProgress();  // 比照 RunRecipeButton：狀態列還原 Ready，不卡在「一鍵量測：…」
            }
        }

        /// <summary>
        /// 一鍵量測的附加輸出：截下含 overlay 的視窗影像，並產生 PDF 報表。
        /// 全程不丟例外——PDF 只是附加品，量測結果本身才是主要產出，任何失敗都只回傳
        /// 一段附加在結果標籤上的簡短訊息，不中斷流程、不彈對話框。
        /// </summary>
        /// <returns>要接在結果標籤後面的字串（成功為 PDF 路徑，失敗為簡短原因）。</returns>
        private string WritePdfReport(
            WorkflowResult wfResult,
            System.Collections.Generic.List<ItemJudgment> judgments,
            string reportDir,
            double pxUmX, double pxUmY, string pixelSizeSource)
        {
            try
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string recipeName = SanitizeFileName(
                    _loadedRecipe != null && !string.IsNullOrEmpty(_loadedRecipe.Name)
                        ? _loadedRecipe.Name : "recipe");

                if (!Directory.Exists(reportDir)) Directory.CreateDirectory(reportDir);

                string pngPath = Path.Combine(reportDir, recipeName + "_" + stamp + "_overlay.png");
                string pdfPath = Path.Combine(reportDir, recipeName + "_" + stamp + ".pdf");

                // 截圖失敗回 null → 報表照樣產出，只是沒有嵌圖（Build/Write 都容許空路徑）。
                string savedPng = _imageHelper.DumpWindowToPng(pngPath) ?? "";

                // pixel size 兩軸相同時只寫一個值，避免報表出現冗長的 "10.00/10.00"。
                string pixelSizeText = Math.Abs(pxUmX - pxUmY) < 1e-9
                    ? string.Format(CultureInfo.InvariantCulture, "{0:F2} µm ({1})", pxUmX, pixelSizeSource)
                    : string.Format(CultureInfo.InvariantCulture, "X {0:F2} / Y {1:F2} µm ({2})",
                        pxUmX, pxUmY, pixelSizeSource);

                MeasurementReportModel model =
                    MeasurementReportBuilder.Build(wfResult, judgments, pixelSizeText, savedPng);
                _pdfReportWriter.Write(model, pdfPath);

                // 報表寫成功後才清理，只保留最近 N 組；失敗只會多一段訊息，不影響本次報表。
                return " | PDF: " + pdfPath + (string.IsNullOrEmpty(savedPng) ? "（無截圖）" : "")
                    + ReportRetentionSweep.Sweep(reportDir);
            }
            catch (Exception ex)
            {
                // 揭露但不中斷：操作員看得到 PDF 沒產出的原因，量測結果與 CSV 不受影響。
                return " | PDF 失敗：" + ex.Message;
            }
        }

        // 配方名稱可能含使用者輸入的非法字元，直接拼進路徑會丟例外；替換成 '_'。
        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
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
            if (!CanOpenSharedImageEditor()) return;

            // 編輯器接管共用影像視窗做 ROI 編輯：先解除所有互動模式（含扇形繪製 pending callback），
            // 再清掉主視窗殘留的偵測/擬合 overlay（Edge Detection 藍框、邊緣十字、Run Recipe 結果等），
            // 讓編輯器從乾淨影像開始。必須在 new RecipeEditor(...) 之前——編輯器建構時就取得自己的
            // overlay 圖層，之後這裡的 ClearOverlay 會清到編輯器那層而非主視窗那層。
            _imageHelper.DisarmInteractiveModes();
            if (_sectorDrawCheck != null && _sectorDrawCheck.Checked) _sectorDrawCheck.Checked = false;
            _imageHelper.ClearOverlay();

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
            ClaimSharedImageEditor(editor);
            editor.Show(this);
        }

        // ── 共用影像視窗的多開閘門 ────────────────────────────────────────────
        // RecipeEditor / MetrologyModelEditorForm / DxfComparisonForm 都是 modeless 且都在共用
        // 影像視窗上疊自己的 overlay 圖層與互動手勢。政策為【封鎖多開】：同一時間只允許一個
        // 這類編輯器存在（滑鼠只有一個，兩個編輯器同時武裝手勢對操作員也無從分辨）。
        private Form _sharedImageEditor;

        private bool CanOpenSharedImageEditor()
        {
            if (_sharedImageEditor == null || _sharedImageEditor.IsDisposed) return true;
            MessageBox.Show(this,
                "「" + _sharedImageEditor.Text + "」正在使用共用影像視窗，請先關閉它再開啟其他編輯器。",
                "共用影像視窗", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _sharedImageEditor.Activate();
            return false;
        }

        private void ClaimSharedImageEditor(Form editor)
        {
            _sharedImageEditor = editor;
            editor.FormClosed += (s, e) =>
            {
                if (ReferenceEquals(_sharedImageEditor, editor)) _sharedImageEditor = null;
            };
        }

        // 開啟 2D 量測模型編輯器（modeless，Phase 4：與 RecipeEditor 一致，讓稍後加入的
        // on-image 繪製能操作主視窗共用影像）。編輯的是目前載入的配方；存檔後回寫 _loadedRecipe
        // 並（若有路徑）以 RecipeStore 持久化，Run Recipe 立即經 Pass 3 套用此模型。
        private void OpenMetrologyModelEditor(object sender, EventArgs e)
        {
            if (!CanOpenSharedImageEditor()) return;
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

            // Phase 3：編輯器內「試測」委派——用目前（未存檔）模型建一份暫態純量測配方（無 1D 工具、
            // 無參考姿態），跑法與 overlay 繪製比照 RunRecipeButton_Click，直接重用 DrawRecipeResults，
            // 讓量測模型 overlay（含判定上色）直接畫在主視窗共用影像上。不做 IQC/CSV，僅擬合預覽。
            Action<MetrologyModelDef> metrologyTrial = (model) =>
            {
                if (_imageHelper == null || _imageHelper.CurrentImage == null) return;
                var tempRecipe = new Recipe { MetrologyModel = model };
                ResolvePixelSize(out double pxUmX, out double pxUmY, out string pxSource);
                try
                {
                    System.Collections.Generic.List<ToolRunResult> results = _recipeRunner.Run(
                        tempRecipe, _imageHelper.CurrentImage,
                        _hasMatch, _lastMatchRow, _lastMatchCol, _lastMatchAngleDeg * Math.PI / 180.0,
                        pxUmX, pxUmY);
                    DrawRecipeResults(results, pxSource);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "試測失敗：" + ex.Message, "Metrology Trial",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            // 編輯器改 modeless 後同樣接管共用影像視窗（比照 OpenRecipeEditor）：先解除所有互動模式，
            // 避免主視窗殘留的扇形繪製等 pending callback 洩漏進編輯器共用的 helper。
            _imageHelper.DisarmInteractiveModes();
            var editor = new MetrologyModelEditorForm(_loadedRecipe, imgW, imgH,
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
                        // 以檔名補上 RecipeId/Name（全新或既有但無名的量測配方，否則報表 Recipe 欄空白）。
                        string baseName = Path.GetFileNameWithoutExtension(_loadedRecipePath);
                        if (string.IsNullOrEmpty(_loadedRecipe.RecipeId)) _loadedRecipe.RecipeId = baseName;
                        if (string.IsNullOrEmpty(_loadedRecipe.Name)) _loadedRecipe.Name = baseName;
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
                },
                _imageHelper,
                metrologyTrial);
            ClaimSharedImageEditor(editor);
            editor.Show(this);
        }

        // 獨立動作（Task 4）：DXF/CAD 輪廓度比對面板。非配方/一鍵量測流程的一環，
        // 僅開一個獨立面板操作目前共用影像（比照 Edit Recipe / Metrology Model）。
        private void OpenDxfComparisonForm(object sender, EventArgs e)
        {
            if (!CanOpenSharedImageEditor()) return;
            var form = new DxfComparisonForm(_imageHelper, _dxfComparer);
            ClaimSharedImageEditor(form);
            form.Show(this);
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
                // 內外弧用橘色(邊界)、中弧用黃色，並在中心畫十字標出弧心。共用 DrawArcBand（與編輯器一致）。
                // 扇形 ROI（_sectorRoiActive）改用 DrawSectorRoi：在 DrawArcBand 基礎上多補起訖角
                // 兩條徑向邊，使畫面呈現封閉扇形（與 DetectEdgesInAnnularSector 的量測區域外形一致）。
                if (_sectorRoiActive)
                {
                    an.DrawSectorRoi(arcRoi.CenterRow, arcRoi.CenterCol, arcRoi.Radius,
                        arcRoi.AnnulusRadius, arcRoi.AngleStart, arcRoi.AngleExtent);
                }
                else
                {
                    an.DrawArcBand(arcRoi.CenterRow, arcRoi.CenterCol, arcRoi.Radius,
                        arcRoi.AngleStart, arcRoi.AngleExtent, arcRoi.AnnulusRadius);
                }
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
                // 扇形 ROI（DetectEdgesInAnnularSector）內部一律用 edges_sub_pix（與演算法 radio 無關），
                // 故 _sectorRoiActive 時也視為 subpix 用 size=3，避免密集弧邊被畫成粗帶。
                int total = edges.EdgePoints.Count;
                int step = total <= MaxOverlayCrosses ? 1 : (int)Math.Ceiling((double)total / MaxOverlayCrosses);
                int crossSize = (_edgeSubPixRadio.Checked || _sectorRoiActive) ? 3 : 8;
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

            return rows;
        }

        // 邊緣檢測結果的 persistent overlay（ROI 框、弧帶、邊緣十字、邊對）。
        private void ShowFittingOverlay()
        {
            _imageHelper.SetPersistentOverlayAction(() => DrawFittingLayers(_imageHelper.Annotator));
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
            _sectorRoiActive = false;
            _latestEdgeResult = null;
        }

        // 邊緣量測結果失效：清結果/擬合狀態與結果表，回到「等待 Detect」。
        // 不動 _latestEdgeRoi（各呼叫端自行設定），也不動 overlay/編輯模式（呼叫端視情況處理）。
        private void InvalidateEdgeState()
        {
            _latestEdgeResult = null;
            _latestArcRoi = null;
            _sectorRoiActive = false;
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
            // 種下編輯中心：OnEdgeRoiNumericChanged 會用 _editCenterRow/Col 重建 ROI。
            // 若不在此 seed，畫完 ROI 後「先改數值框、還沒拖曳把手」會用預設 0 → ROI 跳到 (0,0)。
            _editCenterRow = _latestEdgeRoi.CenterRow;
            _editCenterCol = _latestEdgeRoi.CenterCol;
            InvalidateEdgeState();
            // 畫新的邊緣 rect ROI = 改用邊緣 ROI，對稱於 ArcEditCheck：清掉殘留的圓弧帶並
            // 同步取消「互動編輯」勾選（BeginRect2Edit 已關掉 arc 編輯把手，這裡只補狀態/UI）。
            _latestArcRoi = null;
            _sectorRoiActive = false;
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
                // 進入互動編輯亦取消扇形繪製 checkbox（BeginArcEdit 會 disarm IsSectorMode，
                // 這裡讓 checkbox 視覺同步；手勢 handoff 時 OnSectorRoiCreated 已先取消，冪等無害）。
                if (_sectorDrawCheck.Checked) _sectorDrawCheck.Checked = false;
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

        // 「扇形 ROI（拖曳繪製）」checkbox：鏡射矩形「Draw ROI」(_edgeDrawRoiCheck) 的生命週期。
        // 勾選 = 進入扇形繪製模式（可見勾選代表已武裝）；取消 = 離開繪製模式。
        // 勾選時先關掉其他繪製/編輯的可見勾選（互斥），再 RequestSector 武裝手勢+回呼；
        // 取消時把 IsSectorMode 設 false（setter 清 pending callback）。畫完一個扇形後
        // OnSectorRoiCreated 會自動取消勾選（比照 OnImageRoiSelected 畫完矩形取消 _edgeDrawRoiCheck）。
        private void SectorDrawCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (_imageHelper == null) return;

            if (_sectorDrawCheck.Checked)
            {
                if (_imageHelper.CurrentImage == null)
                {
                    // 無影像不能繪製：還原勾選並提示（比照 ArcEditCheck 防呆）。再入本 handler
                    // 走 else 分支只是 disarm，無副作用。
                    _sectorDrawCheck.Checked = false;
                    _edgeStatusLabel.Text = "扇形 ROI: 請先載入影像";
                    _edgeStatusLabel.ForeColor = Color.Red;
                    return;
                }

                // 互斥：關掉其他繪製/編輯的可見勾選（各自 handler 會清 IsRoiMode / 結束 arc 編輯）。
                if (_edgeDrawRoiCheck.Checked) _edgeDrawRoiCheck.Checked = false;
                if (roiModeCheck.Checked) roiModeCheck.Checked = false;
                if (_arcEditCheck.Checked) _arcEditCheck.Checked = false;

                _edgeStatusLabel.Text = "扇形 ROI：從圓心往外拖曳繪製";
                _edgeStatusLabel.ForeColor = Color.Black;
                _imageHelper.RequestSector(OnSectorRoiCreated);   // 武裝 IsSectorMode + 回呼
            }
            else
            {
                _imageHelper.IsSectorMode = false;   // 離開繪製模式；setter 清 pending callback
            }
        }

        // RequestSector 手勢完成（放開滑鼠、拖曳距離 > 5px）的回呼：沿用 OnArcRoiChanged
        // 把建立好的 ArcMeasureRoi 回寫六個弧形數值框（含角度轉度與 [0,360) 正規化）並設定
        // _latestArcRoi，再勾選「互動編輯」進入既有五把手編輯路徑，不重複實作同步邏輯。
        private void OnSectorRoiCreated(ArcMeasureRoi roi)
        {
            // 建立新扇形前先清掉上一次偵測/擬合結果（邊點、擬合、結果表），比照畫新矩形的
            // OnImageRoiSelected 呼叫 InvalidateEdgeState，避免舊結果殘留在新扇形上（按 Detect 前）。
            // InvalidateEdgeState 會清 _latestArcRoi/_sectorRoiActive，下面立即由 OnArcRoiChanged
            // 與 _sectorRoiActive=true 重新設定，故無副作用。
            InvalidateEdgeState();
            OnArcRoiChanged(roi.CenterRow, roi.CenterCol, roi.Radius,
                roi.AngleStart, roi.AngleExtent, roi.AnnulusRadius);
            // 標記目前有效 ROI 是扇形，主 Detect 按鈕改走 DetectEdgesInAnnularSector。
            _sectorRoiActive = true;

            // 畫完一個扇形即離開繪製模式：取消繪製 checkbox 的勾選（比照 OnImageRoiSelected
            // 畫完矩形後取消 _edgeDrawRoiCheck）。此 uncheck 觸發 else 分支 disarm（IsSectorMode
            // 早在 mouseup 已為 false，冪等無害）。
            _sectorDrawCheck.Checked = false;

            // 勾選互動編輯進入五把手編輯：_arcEditCheck 目前為 false（進入繪製時已取消），
            // 設 true 必觸發 ArcEditCheck_CheckedChanged 的 true 分支 → 以剛寫入的數值框
            // 呼叫 BeginArcEdit(...,OnArcRoiChanged)，交給既有五把手編輯微調。
            _arcEditCheck.Checked = true;
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
            _toolTip?.Dispose();  // ToolTip 未加入 components 容器，手動釋放原生 handle
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
