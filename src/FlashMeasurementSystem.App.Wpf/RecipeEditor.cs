using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.ImageQuality;
using FlashMeasurementSystem.Domain.GearAnalysis;
using FlashMeasurementSystem.Domain.Gdt;
using FlashMeasurementSystem.Domain.HoleArrayAnalysis;
using FlashMeasurementSystem.Domain.PcdAnalysis;
using FlashMeasurementSystem.Domain.PinPitchAnalysis;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// Recipe editor (M3c-2 E1+E2): standalone Form with tool list + per-type property panel +
    /// ROI capture from main image. Supports Load/Save via RecipeStore.
    /// E2 adds distance/angle panels (RefToolIds ComboBox listing line tools), dirty tracking
    /// with a close prompt, and a save-callback so MainWindow can pick up the edited recipe.
    /// </summary>
    public sealed class RecipeEditor : Form
    {
        // ComboBox item that displays "Name (Id)" but carries the tool Id.
        private sealed class ToolRef
        {
            public string Id;
            public string Display;
            public override string ToString() => Display;
        }

        private readonly HWindowControlHelper _imageHelper;
        private readonly RecipeStore _recipeStore = new RecipeStore();
        private readonly List<MeasurementTool> _tools = new List<MeasurementTool>();
        private readonly Action<Recipe, string> _savedCallback;
        // A1：MainWindow 提供的單一工具試測委派（可為 null → 試測按鈕停用）。
        private readonly Func<MeasurementTool, ToolRunResult> _trialMeasure;
        private ToolTip _toolTip;
        // Working recipe metadata (pose/calibration/schema), isolated from caller's object.
        private Recipe _recipe = Recipe.Default();
        private MeasurementTool _selectedTool;
        private int _toolIdCounter;
        private bool _updatingControls;
        private bool _dirty;
        // 本編輯器對共用影像視窗的 overlay/編輯/手勢所有權。取代先前的 _editorOwnsEdit /
        // _editorInstalledOverlay 兩個手動記帳旗標：租約本身就知道哪一層、哪個把手是自己的。
        private readonly IOverlayLease _lease;
        private string _savePath;

        // ── Toolbar controls ──
        private Label _filePathLabel;
        private Button _newButton;
        private Button _loadButton;
        private Button _addCircleButton;
        private Button _addLineButton;
        private Button _addArcButton;
        private Button _addGearButton;
        private Button _addPcdButton;
        private Button _addPinButton;
        private Button _addHoleArrayButton;
        private Button _addDistanceButton;
        private Button _addAngleButton;
        private Button _addIntersectionButton;
        private Button _addMidlineButton;
        private Button _addProjectionButton;
        private Button _addRoundnessButton;
        private Button _addStraightnessButton;
        private Button _addParallelismButton;
        private Button _addPerpendicularityButton;
        private Button _addConcentricityButton;
        private Button _deleteButton;
        private Button _saveButton;
        private Button _saveAsButton;
        private Button _trialMeasureButton;
        private Button _iqcThresholdsButton;

        // ── Left: tool list ──
        private ListBox _toolListBox;

        // ── Right: property groups ──
        private GroupBox _commonGroup;
        private TextBox _nameTextBox;
        private TextBox _idTextBox;
        private Label _typeLabel;
        // v10：circle 工具的 ROI 類型選擇（矩形 rect2 / 扇形 ArcRoi，重用 _arcGroup）。
        // 只在 ToolType=="circle" 時顯示；其餘工具型別隱藏（不影響 line/arc/gear/pcd 既有行為）。
        private ComboBox _roiTypeCombo;

        private GroupBox _roiGroup;
        private NumericUpDown _centerRowNumeric;
        private NumericUpDown _centerColNumeric;
        private NumericUpDown _length1Numeric;
        private NumericUpDown _length2Numeric;
        private NumericUpDown _angleRadNumeric;
        private Button _captureRoiButton;

        // 弧形工具（ToolType == "arc"）專屬 ROI：用 ArcRoi 而非 rect2 Roi，故自成一組。
        private GroupBox _arcGroup;
        private NumericUpDown _arcCenterRowNumeric;
        private NumericUpDown _arcCenterColNumeric;
        private NumericUpDown _arcRadiusNumeric;
        private NumericUpDown _arcAngleStartNumeric;
        private NumericUpDown _arcAngleExtentNumeric;
        private NumericUpDown _arcAnnulusNumeric;
        private Button _captureArcButton;

        // 齒輪工具（ToolType == "gear"）專屬參數群組：量測環（ArcRoi）沿用弧形群組，此處只放齒輪判定參數。
        private GroupBox _gearGroup;
        private NumericUpDown _gearCountNumeric;
        private CheckBox _gearDarkCheck;
        private NumericUpDown _gearPitchTolNumeric;
        private NumericUpDown _gearWidthTolNumeric;

        // PCD 螺栓孔圈工具（ToolType == "pcd"）專屬參數群組：量測環（ArcRoi）沿用弧形群組，
        // 此處只放孔圈判定參數（比照 _gearGroup）。
        private GroupBox _pcdGroup;
        private NumericUpDown _pcdCountNumeric;
        private NumericUpDown _pcdNominalNumeric;
        private NumericUpDown _pcdTolNumeric;
        private NumericUpDown _pcdAngTolNumeric;
        private NumericUpDown _pcdRadTolNumeric;
        private CheckBox _pcdDarkCheck;
        private NumericUpDown _pcdMinAreaNumeric;

        // 引腳間距工具（ToolType == "pin_pitch"）專屬參數群組。量測區用 rect2 Roi（沿用 _roiGroup），
        // 此處只放引腳判定參數（比照 _pcdGroup）。引腳偵測走 blob 而非邊緣掃描，故不顯示 _edgeGroup。
        private GroupBox _pinGroup;
        private NumericUpDown _pinCountNumeric;
        private NumericUpDown _pinPitchNumeric;
        private NumericUpDown _pinPitchTolNumeric;
        private NumericUpDown _pinUniformTolNumeric;
        private CheckBox _pinDarkCheck;
        private NumericUpDown _pinMinAreaNumeric;

        // 孔陣列工具（ToolType == "hole_array"）專屬參數群組。量測區用 rect2 Roi（沿用 _roiGroup，
        // 比照 _pinGroup），此處只放網格判定參數。孔偵測走 blob 而非邊緣掃描，故不顯示 _edgeGroup。
        private GroupBox _holeArrayGroup;
        private NumericUpDown _holeRowsNumeric;
        private NumericUpDown _holeColsNumeric;
        private NumericUpDown _holeDiameterNumeric;
        private NumericUpDown _holeDiameterTolNumeric;
        private NumericUpDown _holePitchXNumeric;
        private NumericUpDown _holePitchYNumeric;
        private NumericUpDown _holePitchTolNumeric;
        private NumericUpDown _holePositionTolNumeric;
        private CheckBox _holeDarkCheck;
        private NumericUpDown _holeMinAreaNumeric;
        private NumericUpDown _holeMinCircularityNumeric;

        private GroupBox _edgeGroup;
        private NumericUpDown _sigmaNumeric;
        private NumericUpDown _thresholdNumeric;
        private ComboBox _polarityCombo;
        private ComboBox _selectorCombo;
        private ComboBox _interpolationCombo;
        private ComboBox _measureModeCombo;

        private GroupBox _refGroup;
        private ComboBox _ref1Combo;
        private ComboBox _ref2Combo;

        private GroupBox _toleranceGroup;
        private NumericUpDown _nominalNumeric;
        private NumericUpDown _lowerNumeric;
        private NumericUpDown _upperNumeric;
        private Label _tolerancePreviewLabel;
        private TextBox _unitTextBox;
        private Label _angleHintLabel;

        private GroupBox _gdtGroup;
        private Label _gdtCharLabel;
        private NumericUpDown _gdtZoneNumeric;
        private Label _gdtHintLabel;

        // ── Constructors ──────────────────────────────────────────────

        public RecipeEditor(HWindowControlHelper imageHelper) : this(imageHelper, null, null, null, null) { }

        public RecipeEditor(HWindowControlHelper imageHelper, Recipe recipe, string filePath,
            Action<Recipe, string> savedCallback, Func<MeasurementTool, ToolRunResult> trialMeasure)
        {
            _imageHelper = imageHelper ?? throw new ArgumentNullException(nameof(imageHelper));
            _lease = _imageHelper.AcquireOverlay("RecipeEditor");
            _savePath = filePath;
            _savedCallback = savedCallback;
            _trialMeasure = trialMeasure;

            // 字型須在 BuildLayout() 前設定，子控制項才會繼承。8.25pt 與原本的
            // Microsoft Sans Serif 8.25pt 行高相同（13px）、寬度僅多 3~7%，可安全替換；
            // 9pt 會在既有版面造成截字。
            Font = new Font("Segoe UI", 8.25F);

            MinimumSize = new Size(580, 440);

            BuildLayout();
            SetupToolTips();
            SetPropertyPanelEnabled(false);

            if (recipe != null)
                LoadFromRecipe(recipe);

            FormClosing += OnFormClosing;
            this.FormClosed += (s, e) =>
            {
                // L2/#3：釋放租約即拆掉「編輯器自己」的編輯把手、選取高亮與試測/弧帶 overlay，
                // 主視窗那一層完全不受影響（且會自動重新顯示）。關閉後的延遲 callback 亦成 no-op。
                _lease.Dispose();
            };
        }

        // ─── Layout builders ───────────────────────────────────────────

        private void BuildLayout()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(8)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            // 工具列列高 AutoSize：按鈕多時 FlowLayoutPanel 會折行，列高隨之增長，
            // 避免按鈕數量超過單列寬度時被裁掉（原固定 36F 會藏住折行的按鈕）。
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Row 0: Toolbar (spans both columns)
            var toolbar = BuildToolbar();
            mainLayout.Controls.Add(toolbar, 0, 0);
            mainLayout.SetColumnSpan(toolbar, 2);

            // Row 1: File path label (spans both columns)
            _filePathLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.GrayText,
                AutoEllipsis = true
            };
            mainLayout.Controls.Add(_filePathLabel, 0, 1);
            mainLayout.SetColumnSpan(_filePathLabel, 2);

            // Row 2: Left tool list + right property panel
            _toolListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false
            };
            _toolListBox.SelectedIndexChanged += OnToolSelectionChanged;
            mainLayout.Controls.Add(_toolListBox, 0, 2);

            var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            BuildPropertyGroups(scrollPanel);
            mainLayout.Controls.Add(scrollPanel, 1, 2);

            Controls.Add(mainLayout);
            UpdateTitle();
        }

        private FlowLayoutPanel BuildToolbar()
        {
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,          // 寬度不足時按鈕折到下一列，不被裁掉
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            _newButton = new Button { Text = "New", Width = 50 };
            _newButton.Click += OnNewRecipe;
            _loadButton = new Button { Text = "Load", Width = 50 };
            _loadButton.Click += OnLoadRecipe;
            _addCircleButton = new Button { Text = "+ Circle", Width = 70 };
            _addCircleButton.Click += (s, e) => AddTool("circle");
            _addLineButton = new Button { Text = "+ Line", Width = 65 };
            _addLineButton.Click += (s, e) => AddTool("line");
            _addArcButton = new Button { Text = "+ 弧形", Width = 70 };
            _addArcButton.Click += (s, e) => AddTool("arc");
            _addGearButton = new Button { Text = "+ 齒輪", Width = 70 };
            _addGearButton.Click += (s, e) => AddTool("gear");
            _addPcdButton = new Button { Text = "+ 螺栓孔圈", Width = 80 };
            _addPcdButton.Click += (s, e) => AddTool("pcd");
            _addPinButton = new Button { Text = "+ 引腳間距", Width = 80 };
            _addPinButton.Click += (s, e) => AddTool("pin_pitch");
            _addHoleArrayButton = new Button { Text = "+ 孔陣列", Width = 80 };
            _addHoleArrayButton.Click += (s, e) => AddTool("hole_array");
            _addDistanceButton = new Button { Text = "+ Distance", Width = 80 };
            _addDistanceButton.Click += (s, e) => AddTool("distance");
            _addAngleButton = new Button { Text = "+ Angle", Width = 70 };
            _addAngleButton.Click += (s, e) => AddTool("angle");
            _addIntersectionButton = new Button { Text = "+ 交點", Width = 70 };
            _addIntersectionButton.Click += (s, e) => AddTool("intersection");
            _addMidlineButton = new Button { Text = "+ 中線", Width = 70 };
            _addMidlineButton.Click += (s, e) => AddTool("midline");
            _addProjectionButton = new Button { Text = "+ 投影", Width = 70 };
            _addProjectionButton.Click += (s, e) => AddTool("projection");
            _addRoundnessButton = new Button { Text = "+ 真圓度", Width = 80 };
            _addRoundnessButton.Click += (s, e) => AddTool("roundness");
            _addStraightnessButton = new Button { Text = "+ 真直度", Width = 80 };
            _addStraightnessButton.Click += (s, e) => AddTool("straightness");
            _addParallelismButton = new Button { Text = "+ 平行度", Width = 80 };
            _addParallelismButton.Click += (s, e) => AddTool("parallelism");
            _addPerpendicularityButton = new Button { Text = "+ 垂直度", Width = 80 };
            _addPerpendicularityButton.Click += (s, e) => AddTool("perpendicularity");
            _addConcentricityButton = new Button { Text = "+ 同心度", Width = 80 };
            _addConcentricityButton.Click += (s, e) => AddTool("concentricity");
            _deleteButton = new Button { Text = "Delete", Width = 60 };
            _deleteButton.Click += OnDeleteTool;
            _saveButton = new Button { Text = "Save", Width = 50 };
            _saveButton.Click += OnSave;
            _saveAsButton = new Button { Text = "Save As", Width = 65 };
            _saveAsButton.Click += OnSaveAs;
            _trialMeasureButton = new Button { Text = "[在此試測]", Width = 90, Enabled = false };
            _trialMeasureButton.Click += OnTrialMeasure;
            // v15：本配方的影像品質門檻。屬配方層級設定，右側屬性面板是工具專屬的，故走獨立對話框。
            _iqcThresholdsButton = new Button { Text = "影像品質門檻…", Width = 100 };
            _iqcThresholdsButton.Click += OnEditIqcThresholds;

            bar.Controls.Add(_newButton);
            bar.Controls.Add(_loadButton);
            bar.Controls.Add(_addCircleButton);
            bar.Controls.Add(_addLineButton);
            bar.Controls.Add(_addArcButton);
            bar.Controls.Add(_addGearButton);
            bar.Controls.Add(_addPcdButton);
            bar.Controls.Add(_addPinButton);
            bar.Controls.Add(_addHoleArrayButton);
            bar.Controls.Add(_addDistanceButton);
            bar.Controls.Add(_addAngleButton);
            bar.Controls.Add(_addIntersectionButton);
            bar.Controls.Add(_addMidlineButton);
            bar.Controls.Add(_addProjectionButton);
            bar.Controls.Add(_addRoundnessButton);
            bar.Controls.Add(_addStraightnessButton);
            bar.Controls.Add(_addParallelismButton);
            bar.Controls.Add(_addPerpendicularityButton);
            bar.Controls.Add(_addConcentricityButton);
            bar.Controls.Add(_deleteButton);
            bar.Controls.Add(_saveButton);
            bar.Controls.Add(_saveAsButton);
            bar.Controls.Add(_trialMeasureButton);
            bar.Controls.Add(_iqcThresholdsButton);

            return bar;
        }

        private void BuildPropertyGroups(Panel parent)
        {
            // Docked Top groups stack bottom-up in reverse insertion order; add in
            // visual top-to-bottom order then it shows reversed, so insert reversed:
            // we want Common, ROI, Edge, RefTool, Tolerance from top down.
            // With Dock=Top, the LAST added sits at top. Add in reverse.
            _gdtGroup = CreateGroupBox("GD&T 形位公差", parent, 120);
            // 高度含 6 列 × 28px（Nominal/Lower/Upper/公差預覽/Unit/角度提示）+ 標題與內距；
            // N5 新增的公差預覽列使原 185 高度容不下最後的角度提示列，提高到 215。
            _toleranceGroup = CreateGroupBox("Tolerance", parent, 215);
            _refGroup = CreateGroupBox("Reference Tools", parent, 95);
            _edgeGroup = CreateGroupBox("Edge Detection", parent, 210);
            // 齒輪參數群組：4 列（齒數 / 極性核取 / 齒距公差 / 齒寬公差）× 28px + 標題與內距。
            _gearGroup = CreateGroupBox("齒輪參數", parent, 150);
            // PCD 孔圈參數群組：7 列（孔數/標稱PCD/PCD公差/角度公差/徑向公差/暗孔核取/最小孔面積）× 28px + 標題與內距。
            _pcdGroup = CreateGroupBox("PCD 螺栓孔圈參數", parent, 240);
            // 引腳間距參數群組：6 列（標稱腳數/標稱間距/間距公差/均勻度公差/暗腳核取/最小腳面積）× 28px + 標題與內距。
            _pinGroup = CreateGroupBox("引腳間距參數", parent, 210);
            // 孔陣列參數群組：10 列（列數/行數/標稱孔徑/孔徑公差/X 孔距/Y 孔距/孔距公差/位置公差/暗孔核取/最小孔面積）
            // × 28px + 標題與內距。
            _holeArrayGroup = CreateGroupBox("孔陣列參數", parent, 320);
            // 7 列 × 28px（6 個數值 + 擷取按鈕）+ 標題與內距。roi 群組同構但少一列。
            _arcGroup = CreateGroupBox("Arc ROI", parent, 240);
            _roiGroup = CreateGroupBox("ROI Geometry", parent, 210);
            // v10：新增第 4 列「ROI 類型」下拉（circle 專用），130 已不夠放 4 列 × 28px，比照 Tolerance 群組的加法（+30）。
            _commonGroup = CreateGroupBox("Common", parent, 160);

            FillCommonGroup(_commonGroup);
            FillRoiGroup(_roiGroup);
            FillArcGroup(_arcGroup);
            FillGearGroup(_gearGroup);
            FillPcdGroup(_pcdGroup);
            FillPinGroup(_pinGroup);
            FillHoleArrayGroup(_holeArrayGroup);
            FillEdgeGroup(_edgeGroup);
            FillRefGroup(_refGroup);
            FillToleranceGroup(_toleranceGroup);
            FillGdtGroup(_gdtGroup);

            WireChangeEvents();
        }

        private static GroupBox CreateGroupBox(string title, Panel parent, int height)
        {
            var gb = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = height,
                Padding = new Padding(6, 14, 6, 6)
            };
            parent.Controls.Add(gb);
            return gb;
        }

        // ─── Fill each GroupBox with a 2-column TableLayoutPanel ───────

        private void FillCommonGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _nameTextBox = AddRow(t, "Name", ref r, new TextBox { Dock = DockStyle.Fill });
            _nameTextBox.TextChanged += (s, e) =>
            {
                if (!_updatingControls && _selectedTool != null)
                {
                    _selectedTool.Name = _nameTextBox.Text;
                    RefreshToolList();
                    MarkDirty();
                }
            };

            _idTextBox = AddRow(t, "Id", ref r, new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = SystemColors.Control
            });

            _typeLabel = AddRow(t, "Type", ref r, new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "-"
            });

            // v10：circle 專用 ROI 類型下拉（矩形／扇形）；PopulateFromTool 依 ToolType 控制 Visible，
            // 其餘工具型別一律隱藏。SelectedIndex：0=矩形("rect")，1=扇形("sector")。
            _roiTypeCombo = AddComboRow(t, "ROI 類型", ref r, new[] { "矩形", "扇形" });

            gb.Controls.Add(t);
        }

        private void FillRoiGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _centerRowNumeric = AddNumericRow(t, "Center Row", ref r, 0M, 1000000M, 2, 0M, 1M);
            _centerColNumeric = AddNumericRow(t, "Center Col", ref r, 0M, 1000000M, 2, 0M, 1M);
            _length1Numeric = AddNumericRow(t, "Length1 (half)", ref r, 0M, 1000000M, 2, 100M, 1M);
            _length2Numeric = AddNumericRow(t, "Length2 (half)", ref r, 0M, 1000000M, 2, 50M, 1M);
            _angleRadNumeric = AddNumericRow(t, "Angle (rad)", ref r, -6.2832M, 6.2832M, 4, 0M, 0.1M);

            _captureRoiButton = new Button
            {
                Text = "Take ROI from Image",
                Dock = DockStyle.Fill,
                Height = 28
            };
            _captureRoiButton.Click += OnCaptureRoi;
            AddRow(t, "", ref r, _captureRoiButton);

            gb.Controls.Add(t);
        }

        // 弧形 ROI（gen_measure_arc 語意）：ArcRoi 內部存弧度；此處數值框以「度」顯示（與 MainWindow 一致），
        // 在 WriteArc/LoadArcFieldsFromSelectedTool 的邊界做 deg↔rad 轉換。半徑/環寬 Minimum=1，避免退化成 !IsDefined。
        private void FillArcGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _arcCenterRowNumeric = AddNumericRow(t, "Center Row", ref r, 0M, 100000M, 2, 200M, 1M);
            _arcCenterColNumeric = AddNumericRow(t, "Center Col", ref r, 0M, 100000M, 2, 200M, 1M);
            _arcRadiusNumeric = AddNumericRow(t, "Radius (px)", ref r, 1M, 100000M, 2, 100M, 1M);
            _arcAngleStartNumeric = AddNumericRow(t, "Angle Start (deg)", ref r, 0M, 360M, 1, 0M, 5M);
            _arcAngleExtentNumeric = AddNumericRow(t, "Angle Extent (deg)", ref r, -360M, 360M, 1, 360M, 5M);
            _arcAnnulusNumeric = AddNumericRow(t, "Annulus (half, px)", ref r, 1M, 100000M, 2, 5M, 1M);

            _captureArcButton = new Button
            {
                Text = "在影像上調整弧形 ROI",
                Dock = DockStyle.Fill,
                Height = 28
            };
            _captureArcButton.Click += OnCaptureArc;
            AddRow(t, "", ref r, _captureArcButton);

            gb.Controls.Add(t);
        }

        // 齒輪判定參數（背光剪影）：齒數為整數計數；齒暗核取決定邊配對極性；齒距/齒寬公差為角度（度）上限。
        // 量測環（弧心/半徑/起訖角/環寬）沿用弧形 ROI 群組，故此處不含幾何欄位。
        private void FillGearGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _gearCountNumeric = AddNumericRow(t, "齒數", ref r, 1M, 10000M, 0, 20M, 1M);
            _gearDarkCheck = AddRow(t, "", ref r, new CheckBox
            {
                Text = "齒為暗（背光）",
                Checked = true,
                Dock = DockStyle.Fill
            });
            _gearPitchTolNumeric = AddNumericRow(t, "齒距公差(度)", ref r, 0.01M, 360M, 2, 1M, 0.1M);
            _gearWidthTolNumeric = AddNumericRow(t, "齒寬公差(度)", ref r, 0.01M, 360M, 2, 2M, 0.1M);

            gb.Controls.Add(t);
        }

        // PCD 螺栓孔圈判定參數（背光穿孔）：孔數為整數計數；標稱 PCD/公差/徑向公差以 mm；角度公差以度；
        // 孔暗核取決定偵測層極性；最小孔面積濾雜訊（偵測層用，分析器忽略）。
        // 量測環（弧心/半徑/起訖角/環寬）沿用弧形 ROI 群組，故此處不含幾何欄位（比照 FillGearGroup）。
        private void FillPcdGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _pcdCountNumeric = AddNumericRow(t, "標稱孔數", ref r, 1M, 10000M, 0, 6M, 1M);
            _pcdNominalNumeric = AddNumericRow(t, "標稱 PCD (mm)", ref r, 0M, 100000M, 3, 0M, 0.1M);
            _pcdTolNumeric = AddNumericRow(t, "PCD 公差(mm)", ref r, 0.001M, 1000000M, 3, 0.1M, 0.01M);
            _pcdAngTolNumeric = AddNumericRow(t, "角度公差(度)", ref r, 0.01M, 360M, 2, 1M, 0.1M);
            _pcdRadTolNumeric = AddNumericRow(t, "徑向公差(mm)", ref r, 0.001M, 1000000M, 3, 0.05M, 0.01M);
            _pcdDarkCheck = AddRow(t, "", ref r, new CheckBox
            {
                Text = "孔為暗（背光）",
                Checked = true,
                Dock = DockStyle.Fill
            });
            _pcdMinAreaNumeric = AddNumericRow(t, "最小孔面積(px)", ref r, 1M, 10000000M, 0, 20M, 1M);

            gb.Controls.Add(t);
        }

        private void FillPinGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            // 標稱腳數 0＝不判定腳數（同 PCD 對 nominal 的處理慣例）。
            _pinCountNumeric = AddNumericRow(t, "標稱腳數", ref r, 0M, 100000M, 0, 0M, 1M);
            _pinPitchNumeric = AddNumericRow(t, "標稱間距(mm)", ref r, 0M, 100000M, 3, 0M, 0.1M);
            _pinPitchTolNumeric = AddNumericRow(t, "間距公差(mm)", ref r, 0M, 1000000M, 3, 0.1M, 0.01M);
            _pinUniformTolNumeric = AddNumericRow(t, "均勻度公差(mm)", ref r, 0M, 1000000M, 3, 0.05M, 0.01M);
            _pinDarkCheck = AddRow(t, "", ref r, new CheckBox
            {
                Text = "引腳為暗（背光）",
                Checked = true,
                Dock = DockStyle.Fill
            });
            _pinMinAreaNumeric = AddNumericRow(t, "最小腳面積(px)", ref r, 1M, 10000000M, 0, 20M, 1M);

            gb.Controls.Add(t);
        }

        // 孔陣列判定參數（背光穿孔）：列數/行數為整數網格；標稱孔徑/孔距/各公差以 mm；
        // 暗孔核取決定偵測層極性；最小孔面積濾雜訊（偵測層用，分析器忽略）。
        // 量測區（中心/半長/半寬/角度）沿用 rect2 ROI 群組，故此處不含幾何欄位（比照 FillPinGroup）。
        private void FillHoleArrayGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _holeRowsNumeric = AddNumericRow(t, "列數 (Rows)", ref r, 1M, 10000M, 0, 1M, 1M);
            _holeColsNumeric = AddNumericRow(t, "行數 (Cols)", ref r, 1M, 10000M, 0, 1M, 1M);
            // 標稱孔徑 0＝待使用者設定（同 PCD/pin 對 nominal 的處理慣例）。
            _holeDiameterNumeric = AddNumericRow(t, "標稱孔徑(mm)", ref r, 0M, 100000M, 3, 0M, 0.1M);
            _holeDiameterTolNumeric = AddNumericRow(t, "孔徑公差(mm)", ref r, 0M, 1000000M, 3, 0.05M, 0.01M);
            _holePitchXNumeric = AddNumericRow(t, "X 孔距(mm)", ref r, 0M, 100000M, 3, 0M, 0.1M);
            _holePitchYNumeric = AddNumericRow(t, "Y 孔距(mm)", ref r, 0M, 100000M, 3, 0M, 0.1M);
            _holePitchTolNumeric = AddNumericRow(t, "孔距公差(mm)", ref r, 0M, 1000000M, 3, 0.1M, 0.01M);
            _holePositionTolNumeric = AddNumericRow(t, "位置公差(mm)", ref r, 0M, 1000000M, 3, 0.1M, 0.01M);
            _holeDarkCheck = AddRow(t, "", ref r, new CheckBox
            {
                Text = "孔為暗（背光）",
                Checked = true,
                Dock = DockStyle.Fill
            });
            _holeMinAreaNumeric = AddNumericRow(t, "最小孔面積(px)", ref r, 1M, 10000000M, 0, 20M, 1M);
            // 圓度下限：擋掉兩孔沾黏被 connection 併成一顆的情形（併起來的雙孔圓度明顯偏低，
            // 實測乾淨圓=1.00、沾黏對≈0.63，預設 0.80 落在中間）。
            // ⚠️ 長孔/橢圓孔本來圓度就低（2:1 橢圓≈0.5）會被誤拒 → 設 0 可完全停用此過濾。
            _holeMinCircularityNumeric = AddNumericRow(t, "圓度下限(0=停用)", ref r, 0M, 1M, 2, 0.80M, 0.05M);

            gb.Controls.Add(t);
        }

        private void FillEdgeGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _sigmaNumeric = AddNumericRow(t, "Sigma", ref r, 0.1M, 100M, 1, 1.2M, 0.1M);
            _thresholdNumeric = AddNumericRow(t, "Threshold", ref r, 0M, 255M, 0, 25M, 1M);
            _polarityCombo = AddComboRow(t, "Polarity", ref r, new[] { "all", "positive", "negative" });
            _selectorCombo = AddComboRow(t, "Selector", ref r, new[] { "all", "first", "last" });
            _interpolationCombo = AddComboRow(t, "Interpolation", ref r,
                new[] { "nearest_neighbor", "bilinear", "bicubic" });
            _measureModeCombo = AddComboRow(t, "Measure Mode", ref r,
                new[] { "single_edge", "edge_pair" });

            gb.Controls.Add(t);
        }

        private void FillRefGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _ref1Combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _ref2Combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            AddRow(t, "Ref 1", ref r, _ref1Combo);
            AddRow(t, "Ref 2", ref r, _ref2Combo);

            gb.Controls.Add(t);
        }

        private void FillToleranceGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _nominalNumeric = AddNumericRow(t, "Nominal", ref r, -1000000M, 1000000M, 4, 0M, 0.1M);
            _lowerNumeric = AddNumericRow(t, "Lower", ref r, -1000000M, 1000000M, 4, -0.005M, 0.001M);
            _upperNumeric = AddNumericRow(t, "Upper", ref r, -1000000M, 1000000M, 4, 0.005M, 0.001M);

            // N5：即時顯示實際允許範圍 [LowerLimit, UpperLimit]，上限<下限時轉紅警示（不擋存檔）。
            _tolerancePreviewLabel = AddRow(t, "", ref r, new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.GrayText,
                Text = "= [-, -] mm"
            });

            _unitTextBox = AddRow(t, "Unit", ref r, new TextBox { Dock = DockStyle.Fill, Text = "mm" });
            _unitTextBox.TextChanged += (s, e) =>
            {
                if (!_updatingControls && _selectedTool != null)
                {
                    _selectedTool.Tolerance.Unit = _unitTextBox.Text;
                    MarkDirty();
                }
            };

            _angleHintLabel = AddRow(t, "", ref r, new Label
            {
                Dock = DockStyle.Fill,
                Text = "Unit='deg': judges the line's angle, Unit='mm': judges distance",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.GrayText,
                Visible = false
            });

            gb.Controls.Add(t);
        }

        // GD&T 形位公差輸入（單邊）：特性唯讀（由工具型別決定）+ 公差帶 T。
        private void FillGdtGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _gdtCharLabel = AddRow(t, "特性", ref r, new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "-"
            });
            _gdtZoneNumeric = AddNumericRow(t, "公差帶 T (mm)", ref r, 0M, 1000000M, 4, 0.05M, 0.001M);
            _gdtHintLabel = AddRow(t, "", ref r, new Label
            {
                Dock = DockStyle.Fill,
                Text = "單邊：0 ≤ 偏差 ≤ T。Ref1=量測元素；平行/垂直/同心需 Ref2=基準。",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.GrayText
            });

            gb.Controls.Add(t);
        }

        // ─── Layout helpers ────────────────────────────────────────────

        private static TableLayoutPanel NewTable()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return t;
        }

        private static T AddRow<T>(TableLayoutPanel table, string label, ref int row, T control)
            where T : Control
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            if (!string.IsNullOrEmpty(label))
            {
                table.Controls.Add(new Label
                {
                    Text = label,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, row);
            }
            table.Controls.Add(control, 1, row);
            table.RowCount = ++row;
            return control;
        }

        private static NumericUpDown AddNumericRow(TableLayoutPanel table, string label, ref int row,
            decimal min, decimal max, int decimals, decimal value, decimal increment)
        {
            var nud = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Value = Clamp(value, min, max),
                Increment = increment,
                Dock = DockStyle.Fill
            };
            return AddRow(table, label, ref row, nud);
        }

        private static ComboBox AddComboRow(TableLayoutPanel table, string label, ref int row, string[] items)
        {
            var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(items);
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            return AddRow(table, label, ref row, combo);
        }

        private static decimal Clamp(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // ─── Change event wiring ───────────────────────────────────────

        private void WireChangeEvents()
        {
            _roiTypeCombo.SelectedIndexChanged += RoiTypeCombo_SelectedIndexChanged;

            _centerRowNumeric.ValueChanged += (s, e) => WriteRoi();
            _centerColNumeric.ValueChanged += (s, e) => WriteRoi();
            _length1Numeric.ValueChanged += (s, e) => WriteRoi();
            _length2Numeric.ValueChanged += (s, e) => WriteRoi();
            _angleRadNumeric.ValueChanged += (s, e) => WriteRoi();

            _arcCenterRowNumeric.ValueChanged += (s, e) => WriteArc();
            _arcCenterColNumeric.ValueChanged += (s, e) => WriteArc();
            _arcRadiusNumeric.ValueChanged += (s, e) => WriteArc();
            _arcAngleStartNumeric.ValueChanged += (s, e) => WriteArc();
            _arcAngleExtentNumeric.ValueChanged += (s, e) => WriteArc();
            _arcAnnulusNumeric.ValueChanged += (s, e) => WriteArc();

            _gearCountNumeric.ValueChanged += (s, e) => WriteGear();
            _gearDarkCheck.CheckedChanged += (s, e) => WriteGear();
            _gearPitchTolNumeric.ValueChanged += (s, e) => WriteGear();
            _gearWidthTolNumeric.ValueChanged += (s, e) => WriteGear();

            _pcdCountNumeric.ValueChanged += (s, e) => WritePcd();
            _pcdNominalNumeric.ValueChanged += (s, e) => WritePcd();
            _pcdTolNumeric.ValueChanged += (s, e) => WritePcd();
            _pcdAngTolNumeric.ValueChanged += (s, e) => WritePcd();
            _pcdRadTolNumeric.ValueChanged += (s, e) => WritePcd();
            _pcdDarkCheck.CheckedChanged += (s, e) => WritePcd();
            _pcdMinAreaNumeric.ValueChanged += (s, e) => WritePcd();

            _pinCountNumeric.ValueChanged += (s, e) => WritePinPitch();
            _pinPitchNumeric.ValueChanged += (s, e) => WritePinPitch();
            _pinPitchTolNumeric.ValueChanged += (s, e) => WritePinPitch();
            _pinUniformTolNumeric.ValueChanged += (s, e) => WritePinPitch();
            _pinDarkCheck.CheckedChanged += (s, e) => WritePinPitch();
            _pinMinAreaNumeric.ValueChanged += (s, e) => WritePinPitch();

            _holeRowsNumeric.ValueChanged += (s, e) => WriteHoleArray();
            _holeColsNumeric.ValueChanged += (s, e) => WriteHoleArray();
            _holeDiameterNumeric.ValueChanged += (s, e) => WriteHoleArray();
            _holeDiameterTolNumeric.ValueChanged += (s, e) => WriteHoleArray();
            _holePitchXNumeric.ValueChanged += (s, e) => WriteHoleArray();
            _holePitchYNumeric.ValueChanged += (s, e) => WriteHoleArray();
            _holePitchTolNumeric.ValueChanged += (s, e) => WriteHoleArray();
            _holePositionTolNumeric.ValueChanged += (s, e) => WriteHoleArray();
            _holeDarkCheck.CheckedChanged += (s, e) => WriteHoleArray();
            _holeMinAreaNumeric.ValueChanged += (s, e) => WriteHoleArray();
            _holeMinCircularityNumeric.ValueChanged += (s, e) => WriteHoleArray();

            _sigmaNumeric.ValueChanged += (s, e) => WriteEdgeParams();
            _thresholdNumeric.ValueChanged += (s, e) => WriteEdgeParams();
            _polarityCombo.SelectedIndexChanged += (s, e) => WriteEdgeParams();
            _selectorCombo.SelectedIndexChanged += (s, e) => WriteEdgeParams();
            _interpolationCombo.SelectedIndexChanged += (s, e) => WriteEdgeParams();
            _measureModeCombo.SelectedIndexChanged += (s, e) => WriteEdgeParams();

            _ref1Combo.SelectedIndexChanged += (s, e) => WriteRefToolIds();
            _ref2Combo.SelectedIndexChanged += (s, e) => WriteRefToolIds();

            _nominalNumeric.ValueChanged += (s, e) => WriteTolerance();
            _lowerNumeric.ValueChanged += (s, e) => WriteTolerance();
            _upperNumeric.ValueChanged += (s, e) => WriteTolerance();

            _gdtZoneNumeric.ValueChanged += (s, e) => WriteGdt();
        }

        private void WriteRoi()
        {
            if (_updatingControls || _selectedTool == null) return;
            _selectedTool.Roi.CenterRow = (double)_centerRowNumeric.Value;
            _selectedTool.Roi.CenterCol = (double)_centerColNumeric.Value;
            _selectedTool.Roi.Length1 = (double)_length1Numeric.Value;
            _selectedTool.Roi.Length2 = (double)_length2Numeric.Value;
            _selectedTool.Roi.AngleRad = (double)_angleRadNumeric.Value;
            if (_lease.IsEditingRect2)
            {
                _lease.BeginRect2Edit(_selectedTool.Roi.CenterRow, _selectedTool.Roi.CenterCol,
                    _selectedTool.Roi.AngleRad, _selectedTool.Roi.Length1, _selectedTool.Roi.Length2,
                    OnToolRect2Changed);
            }
            MarkDirty();
        }

        // v10：circle 專用 ROI 類型切換（矩形/扇形）。比照 WriteRoi/WriteArc 以 _updatingControls 守衛，
        // 且僅在選中工具為 circle 時生效（其餘工具型別即使 combo 值變動也不寫回，因為 combo 對它們是隱藏的）。
        // 切到扇形且尚無 ArcRoi 時，比照 AddTool("arc") 種一個預設弧（此處用四分之一弧，而非整圈，
        // 避免使用者誤以為「扇形」預設是整圓，需自己拖把手縮小）。
        private void RoiTypeCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updatingControls || _selectedTool == null || _selectedTool.ToolType != "circle") return;

            string newShape = _roiTypeCombo.SelectedIndex == 1 ? "sector" : "rect";
            _selectedTool.RoiShape = newShape;
            if (newShape == "sector" && _selectedTool.ArcRoi == null)
            {
                _selectedTool.ArcRoi = new ArcMeasureRoi
                {
                    CenterRow = 200,
                    CenterCol = 200,
                    Radius = 100,
                    AngleStart = 0.0,
                    AngleExtent = Math.PI / 2.0,
                    AnnulusRadius = 15.0
                };
            }
            MarkDirty();
            // 重新套用面板可見性（矩形 ROI 群組 ↔ Arc ROI 群組）並切換影像編輯把手，
            // 直接沿用選工具時的既有路徑（PopulateFromTool + ShowRoiEdit），不另建一套。
            PopulateFromTool(_selectedTool);
            ShowRoiEdit();
        }

        // 數值框 → ArcRoi。比照 WriteRoi：若弧形把手正在編輯中，同步重下把手座標（即時預覽）。
        private void WriteArc()
        {
            if (_updatingControls || _selectedTool == null || _selectedTool.ArcRoi == null) return;
            ArcMeasureRoi a = _selectedTool.ArcRoi;
            a.CenterRow = (double)_arcCenterRowNumeric.Value;
            a.CenterCol = (double)_arcCenterColNumeric.Value;
            a.Radius = (double)_arcRadiusNumeric.Value;
            // 數值框顯示度 → ArcRoi 存弧度（deg→rad 轉換點）。
            a.AngleStart = (double)_arcAngleStartNumeric.Value * Math.PI / 180.0;
            a.AngleExtent = (double)_arcAngleExtentNumeric.Value * Math.PI / 180.0;
            a.AnnulusRadius = (double)_arcAnnulusNumeric.Value;
            // 改數值即回到「即時弧帶/扇形」overlay（若剛試測過，靜態結果 overlay 會蓋住即時框）。
            InstallArcBandOverlay();
            if (_lease.IsEditingArc)
            {
                // 重下把手座標（即時預覽）；BeginArcEdit 內部會 Redraw，弧帶 persistent overlay 一併重畫。
                _lease.BeginArcEdit(a.CenterRow, a.CenterCol, a.Radius,
                    a.AngleStart, a.AngleExtent, a.AnnulusRadius, OnToolArcChanged);
            }
            else
            {
                // 未進互動編輯時，仍要讓弧帶 persistent overlay 依更新後的 ArcRoi 重畫。
                _imageHelper.Redraw();
            }
            MarkDirty();
        }

        // 數值框/核取 → GearAnalysisParameters。比照 WriteArc：以 _updatingControls 守衛避免載入時誤標 dirty。
        private void WriteGear()
        {
            if (_updatingControls || _selectedTool == null || _selectedTool.Gear == null) return;
            GearAnalysisParameters g = _selectedTool.Gear;
            g.NominalToothCount = (int)_gearCountNumeric.Value;
            g.ToothIsDark = _gearDarkCheck.Checked;
            g.PitchToleranceDeg = (double)_gearPitchTolNumeric.Value;
            g.WidthToleranceDeg = (double)_gearWidthTolNumeric.Value;
            MarkDirty();
        }

        // 數值框/核取 → PcdAnalysisParameters。比照 WriteGear：以 _updatingControls 守衛避免載入時誤標 dirty。
        private void WritePcd()
        {
            if (_updatingControls || _selectedTool == null || _selectedTool.Pcd == null) return;
            PcdAnalysisParameters p = _selectedTool.Pcd;
            p.NominalHoleCount = (int)_pcdCountNumeric.Value;
            p.NominalPcdMm = (double)_pcdNominalNumeric.Value;
            p.PcdToleranceMm = (double)_pcdTolNumeric.Value;
            p.AngularToleranceDeg = (double)_pcdAngTolNumeric.Value;
            p.RadialToleranceMm = (double)_pcdRadTolNumeric.Value;
            p.HoleIsDark = _pcdDarkCheck.Checked;
            p.MinHoleAreaPx = (double)_pcdMinAreaNumeric.Value;
            MarkDirty();
        }

        private void WritePinPitch()
        {
            if (_updatingControls || _selectedTool == null || _selectedTool.PinPitch == null) return;
            PinPitchAnalysisParameters p = _selectedTool.PinPitch;
            p.NominalPinCount = (int)_pinCountNumeric.Value;
            p.NominalPitchMm = (double)_pinPitchNumeric.Value;
            p.PitchToleranceMm = (double)_pinPitchTolNumeric.Value;
            p.UniformityToleranceMm = (double)_pinUniformTolNumeric.Value;
            p.PinIsDark = _pinDarkCheck.Checked;
            p.MinPinAreaPx = (double)_pinMinAreaNumeric.Value;
            MarkDirty();
        }

        // 數值框/核取 → HoleArrayAnalysisParameters。比照 WritePinPitch：以 _updatingControls 守衛避免載入時誤標 dirty。
        private void WriteHoleArray()
        {
            if (_updatingControls || _selectedTool == null || _selectedTool.HoleArray == null) return;
            HoleArrayAnalysisParameters h = _selectedTool.HoleArray;
            h.Rows = (int)_holeRowsNumeric.Value;
            h.Cols = (int)_holeColsNumeric.Value;
            h.NominalDiameterMm = (double)_holeDiameterNumeric.Value;
            h.DiameterToleranceMm = (double)_holeDiameterTolNumeric.Value;
            h.NominalPitchXMm = (double)_holePitchXNumeric.Value;
            h.NominalPitchYMm = (double)_holePitchYNumeric.Value;
            h.PitchToleranceMm = (double)_holePitchTolNumeric.Value;
            h.PositionToleranceMm = (double)_holePositionTolNumeric.Value;
            h.HoleIsDark = _holeDarkCheck.Checked;
            h.MinHoleAreaPx = (double)_holeMinAreaNumeric.Value;
            h.MinCircularity = (double)_holeMinCircularityNumeric.Value;
            MarkDirty();
        }

        private void WriteEdgeParams()
        {
            if (_updatingControls || _selectedTool == null) return;
            _selectedTool.EdgeParameters.Sigma = (double)_sigmaNumeric.Value;
            _selectedTool.EdgeParameters.Threshold = (double)_thresholdNumeric.Value;
            if (_polarityCombo.SelectedItem != null)
                _selectedTool.EdgeParameters.Polarity = _polarityCombo.SelectedItem.ToString();
            if (_selectorCombo.SelectedItem != null)
                _selectedTool.EdgeParameters.EdgeSelector = _selectorCombo.SelectedItem.ToString();
            if (_interpolationCombo.SelectedItem != null)
                _selectedTool.EdgeParameters.Interpolation = _interpolationCombo.SelectedItem.ToString();
            if (_measureModeCombo.SelectedItem != null)
                _selectedTool.EdgeParameters.MeasureMode = _measureModeCombo.SelectedItem.ToString();
            MarkDirty();
        }

        private void WriteRefToolIds()
        {
            if (_updatingControls || _selectedTool == null) return;
            _selectedTool.RefToolIds.Clear();
            var r1 = _ref1Combo.SelectedItem as ToolRef;
            var r2 = _ref2Combo.SelectedItem as ToolRef;
            if (r1 != null) _selectedTool.RefToolIds.Add(r1.Id);
            if (r2 != null) _selectedTool.RefToolIds.Add(r2.Id);
            MarkDirty();
        }

        private void WriteTolerance()
        {
            if (_updatingControls || _selectedTool == null) return;
            _selectedTool.Tolerance.Nominal = (double)_nominalNumeric.Value;
            _selectedTool.Tolerance.LowerTolerance = (double)_lowerNumeric.Value;
            _selectedTool.Tolerance.UpperTolerance = (double)_upperNumeric.Value;
            MarkDirty();
            UpdateTolerancePreview();
        }

        // N5：依目前 Tolerance 即時顯示 [LowerLimit, UpperLimit]；上限<下限時轉紅警示（純顯示、不擋存檔）。
        // 算術沿用 ToleranceSpec.LowerLimit/UpperLimit，反轉判定沿用 RecipeValidator 同一述詞。
        private void UpdateTolerancePreview()
        {
            if (_tolerancePreviewLabel == null) return;
            if (_selectedTool == null || _selectedTool.Tolerance == null) return;
            var tol = _selectedTool.Tolerance;
            bool inverted = tol.UpperTolerance < tol.LowerTolerance;
            string unit = string.IsNullOrEmpty(tol.Unit) ? "mm" : tol.Unit;
            if (inverted)
            {
                _tolerancePreviewLabel.ForeColor = Color.DarkRed;
                _tolerancePreviewLabel.Text = "⚠ 上限 < 下限 (Upper < Lower)";
            }
            else
            {
                _tolerancePreviewLabel.ForeColor = SystemColors.GrayText;
                _tolerancePreviewLabel.Text = string.Format(CultureInfo.InvariantCulture,
                    "= [{0:F4}, {1:F4}] {2}", tol.LowerLimit, tol.UpperLimit, unit);
            }
        }

        // A1：試測按鈕只在「有委派 + 已載入影像 + 選中 circle/line 工具」時可用。
        private void RefreshTrialButtonEnabled()
        {
            if (_trialMeasureButton == null) return;
            _trialMeasureButton.Enabled =
                _trialMeasure != null
                && _imageHelper != null && _imageHelper.CurrentImage != null
                && _selectedTool != null
                && (_selectedTool.ToolType == "circle" || _selectedTool.ToolType == "line"
                    || _selectedTool.ToolType == "arc" || _selectedTool.ToolType == "gear"
                    || _selectedTool.ToolType == "pcd" || _selectedTool.ToolType == "pin_pitch"
                    || _selectedTool.ToolType == "hole_array");
        }

        // A1：在編輯器內就地試測選中的 circle/line/arc 工具，把擬合結果畫在共用主視窗影像上。
        // 只跑這一個工具（MainWindow 委派內以暫態單工具配方呼叫 RecipeRunner.Run），
        // 不重跑整份配方、不做配方驗證。單一 overlay slot：重畫 ROI 框/弧帶 + 擬合結果。
        private void OnTrialMeasure(object sender, EventArgs e)
        {
            var tool = _selectedTool;
            if (tool == null || _trialMeasure == null) return;
            ToolRunResult result;
            try { result = _trialMeasure(tool); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "試測失敗：" + ex.Message, "Trial Measure",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var roi = tool.Roi;
            bool isArc = tool.ToolType == "arc";
            bool isGear = tool.ToolType == "gear";
            bool isPcd = tool.ToolType == "pcd";
            // v10：circle 選扇形 ROI 時比照弧形工具（借 ArcRoi/PlacedArc、不畫 rect 框）。
            bool isSectorCircle = tool.ToolType == "circle" && tool.RoiShape == "sector";
            ArcMeasureRoi arc = tool.ArcRoi;
            _lease.SetPersistentOverlay(() =>
            {
                OverlayAnnotator an = _imageHelper.Annotator;
                if (an == null) return;
                // 弧形/齒輪/PCD/扇形 circle 的 Roi 未使用（畫出來會是退化橘框）——只有這些以外才畫矩形 ROI 框。
                if (roi != null && !isArc && !isGear && !isPcd && !isSectorCircle)
                    an.DrawRectangle2(roi.CenterRow, roi.CenterCol, roi.AngleRad,
                                      roi.Length1, roi.Length2, "orange");

                // 文字錨點：弧形/齒輪/PCD/扇形 circle 錨在弧心，其餘錨在 rect2 ROI 中心。
                bool usesArcRoi = isArc || isGear || isPcd || isSectorCircle;
                double txtR = usesArcRoi && arc != null ? arc.CenterRow : (roi != null ? roi.CenterRow : 20);
                double txtC = usesArcRoi && arc != null ? arc.CenterCol : (roi != null ? roi.CenterCol : 20);
                if (result == null || !result.Measured)
                {
                    an.DrawText("未偵測到邊緣 / No edge detected", (int)txtR, (int)txtC, "yellow");
                    return;
                }

                // 弧形/齒輪/PCD 結果：畫量測帶 + 抽樣邊點十字 + 數值，比照 MainWindow.DrawRecipeResults 的弧形分支。
                // 齒輪重用弧卡尺量測環（PlacedArc/ArcEdge*）+ 以 ValueText 顯示齒數/齒距/齒寬判定訊息；
                // PCD 同樣重用量測環（PlacedArc），但不填 ArcEdgeRows/Cols（孔偵測非邊緣掃描），故十字迴圈自然空跑。
                // 【刻意分工，勿當 parity bug 統一】此處（編輯器試測，調機視圖）對齒輪刻意畫「原始邊點」，
                // 讓操作者在調 ROI/Sigma/Threshold 時確認每對進/出齒被乾淨抓到；主頁一鍵量測則畫「齒中心」結果
                // （見 MainWindow.DrawRecipeResults 齒輪分支：每齒一十字＝齒數、缺齒洋紅）。兩處視圖不同是設計，不是缺陷。
                if ((result.ToolType == "arc" || result.ToolType == "gear" || result.ToolType == "pcd") && result.PlacedArc != null)
                {
                    ArcMeasureRoi a = result.PlacedArc;
                    string c = result.IsOk == true ? "green" : (result.IsOk == false ? "red" : "yellow");
                    an.DrawArcBand(a.CenterRow, a.CenterCol, a.Radius, a.AngleStart, a.AngleExtent, a.AnnulusRadius);
                    int n = Math.Min(result.ArcEdgeRows.Count, result.ArcEdgeCols.Count);
                    int step = n > 200 ? (int)Math.Ceiling(n / 200.0) : 1;
                    for (int i = 0; i < n; i += step)
                        an.DrawCross(result.ArcEdgeRows[i], result.ArcEdgeCols[i], 10, c);
                    // 試測值標在環帶外緣上方，避免長字串（gear/pcd 三～四項）疊在環帶/邊點上；調機時仍看得到數值。
                    double labelRow = a.CenterRow - (a.Radius + a.AnnulusRadius) - 20;
                    an.DrawText(result.ValueText ?? string.Empty, (int)labelRow, (int)a.CenterCol, c);
                    return;
                }

                // v10：扇形 ROI 的 circle：先畫量測扇形帶（PlacedArc），再由下方 circle 分支畫擬合圓（不 return）。
                if (isSectorCircle && result.PlacedArc != null)
                {
                    ArcMeasureRoi sa = result.PlacedArc;
                    an.DrawSectorRoi(sa.CenterRow, sa.CenterCol, sa.Radius, sa.AnnulusRadius, sa.AngleStart, sa.AngleExtent);
                }

                if (result.ToolType == "circle")
                    an.DrawCircle(result.FitCenterRow, result.FitCenterCol, result.FitRadiusPx, "green");
                else if (result.ToolType == "line")
                    an.DrawLine(result.LineRow1, result.LineCol1, result.LineRow2, result.LineCol2, "green");
                an.DrawText(result.ValueText ?? string.Empty, (int)txtR, (int)txtC + 18, "green");
            });
        }

        private void WriteGdt()
        {
            if (_updatingControls || _selectedTool == null || _selectedTool.Gdt == null) return;
            _selectedTool.Gdt.ToleranceZoneMm = (double)_gdtZoneNumeric.Value;
            MarkDirty();
        }

        // ─── Tool CRUD ─────────────────────────────────────────────────

        private void AddTool(string toolType)
        {
            var tolerance = ToleranceSpec.Default();
            // Angle is measured in degrees; distance/diameter in mm.
            if (toolType == "angle") tolerance.Unit = "deg";
            // 弧形卡尺量的是邊數（無因次計數），不是長度，故單位不能沿用預設 "mm"。
            if (toolType == "arc") tolerance.Unit = "count";

            var tool = new MeasurementTool
            {
                Id = "t" + (++_toolIdCounter),
                Name = toolType + "_" + _toolIdCounter,
                ToolType = toolType,
                Roi = new RoiGeometry(),
                EdgeParameters = EdgeDetectionParameters.Default(),
                Tolerance = tolerance,
                RefToolIds = new List<string>()
            };
            // 弧形工具：必須帶一個「已定義」的 ArcRoi，否則 RecipeValidator 會立刻擋下一鍵流程。
            // 預設為整圈（AngleExtent = 2π），使用者再以數值框或影像把手調整。
            if (toolType == "arc")
            {
                tool.ArcRoi = new ArcMeasureRoi
                {
                    CenterRow = 200,
                    CenterCol = 200,
                    Radius = 100,
                    AngleStart = 0.0,
                    AngleExtent = 2.0 * Math.PI,
                    AnnulusRadius = 5.0
                };
            }
            // 齒輪工具：量測環沿用弧形 ArcRoi（整圈掃描），另帶一組齒輪判定參數（齒數/極性/齒距/齒寬公差）。
            // ArcRoi 必須「已定義」，否則 RecipeValidator 會擋下一鍵流程（比照弧形工具）。
            if (toolType == "gear")
            {
                tool.ArcRoi = new ArcMeasureRoi
                {
                    CenterRow = 200,
                    CenterCol = 200,
                    Radius = 100,
                    AngleStart = 0.0,
                    AngleExtent = 2.0 * Math.PI,
                    AnnulusRadius = 5.0
                };
                tool.Gear = new GearAnalysisParameters();  // NominalToothCount=20, ToothIsDark=true, tols 1/2
            }
            // PCD 螺栓孔圈工具：量測環沿用弧形 ArcRoi（整圈掃描環帶偵測孔），另帶一組孔圈判定參數
            // （孔數/標稱PCD/PCD公差/角度公差/徑向公差/暗孔/最小孔面積）。ArcRoi 必須「已定義」，
            // 否則 RecipeValidator 會擋下一鍵流程（比照弧形/齒輪工具）。不設定 tool.Tolerance——
            // PCD 走四條件判定（PcdAnalysisParameters），不用雙邊 Tolerance 群組。
            if (toolType == "pcd")
            {
                tool.ArcRoi = new ArcMeasureRoi
                {
                    CenterRow = 200,
                    CenterCol = 200,
                    Radius = 100,
                    AngleStart = 0.0,
                    AngleExtent = 2.0 * Math.PI,
                    AnnulusRadius = 15.0
                };
                tool.Pcd = new PcdAnalysisParameters();
            }
            // 引腳間距工具：量測區用 rect2 Roi（與 circle/line 同座標變換，非 ArcRoi），另帶一組引腳判定參數。
            // 種一個「寬而短」的預設 rect2（一排引腳沿主軸展開），使用者再以數值框或影像把手調整。
            // Validator 要求 Roi != null（已由建構區塊的 new RoiGeometry() 保證），此處只覆寫成合用尺寸。
            // 不設定 tool.Tolerance——pin_pitch 走引腳判定（PinPitchAnalysisParameters），自判定，不用雙邊 Tolerance 群組。
            if (toolType == "pin_pitch")
            {
                tool.Roi = new RoiGeometry
                {
                    CenterRow = 200,
                    CenterCol = 200,
                    Length1 = 150,  // 半長：沿主軸（引腳排列方向）
                    Length2 = 30,   // 半寬：垂直主軸
                    AngleRad = 0.0
                };
                tool.PinPitch = PinPitchAnalysisParameters.Default();
            }
            // 孔陣列工具：量測區用 rect2 Roi（與 pin_pitch 同構，非 ArcRoi），另帶一組網格判定參數。
            // 種一個涵蓋整片孔網格的預設 rect2（兩軸都要夠寬，不像引腳那樣「寬而短」），
            // 使用者再以數值框或影像把手調整。Validator 要求 Roi != null（建構區塊的 new RoiGeometry() 已保證）。
            // 不設定 tool.Tolerance——hole_array 走五條件判定（HoleArrayAnalysisParameters），自判定，不用雙邊 Tolerance 群組。
            if (toolType == "hole_array")
            {
                tool.Roi = new RoiGeometry
                {
                    CenterRow = 200,
                    CenterCol = 200,
                    Length1 = 200,  // 半長：沿主軸（網格 X/行方向）
                    Length2 = 150,  // 半寬：沿次軸（網格 Y/列方向）
                    AngleRad = 0.0
                };
                tool.HoleArray = HoleArrayAnalysisParameters.Default();
            }
            // GD&T 工具：單邊形位公差，預設帶寬待使用者設定（0.05mm 佔位，非 0 以免一律 NG）。
            if (IsGdtType(toolType))
            {
                tool.Gdt = new GdtToleranceSpec
                {
                    Characteristic = GdtCharacteristicFor(toolType),
                    ToleranceZoneMm = 0.05
                };
            }
            _tools.Add(tool);
            RefreshToolList();
            _toolListBox.SelectedIndex = _tools.Count - 1;
            MarkDirty();
        }

        private void OnDeleteTool(object sender, EventArgs e)
        {
            if (_selectedTool == null) return;
            _tools.Remove(_selectedTool);
            _selectedTool = null;
            RefreshToolList();
            SetPropertyPanelEnabled(false);
            MarkDirty();
        }

        private void OnNewRecipe(object sender, EventArgs e)
        {
            if (!ConfirmDiscardIfDirty()) return;
            _tools.Clear();
            _recipe = Recipe.Default();
            _selectedTool = null;
            _toolIdCounter = 0;
            _savePath = null;
            _toolListBox.Items.Clear();
            SetPropertyPanelEnabled(false);
            MarkClean();
            UpdateTitle();
        }

        // ─── Load ──────────────────────────────────────────────────────

        private void OnLoadRecipe(object sender, EventArgs e)
        {
            if (!ConfirmDiscardIfDirty()) return;

            using (var dialog = new OpenFileDialog
            {
                Filter = "Recipe Files (*.zcp)|*.zcp|All Files (*.*)|*.*",
                Title = "Load Recipe"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var recipe = _recipeStore.Load(dialog.FileName);
                    _savePath = dialog.FileName;
                    LoadFromRecipe(recipe);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Load failed: " + ex.Message, "Load",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadFromRecipe(Recipe recipe)
        {
            _tools.Clear();
            _selectedTool = null;
            _toolIdCounter = 0;

            // Deep-copy into an isolated working copy so edits never leak into the
            // caller's recipe object. Without this, "discard" (No on close) would have
            // no effect because the editor would be mutating the shared object in place.
            _recipe = CopyRecipeMetadata(recipe);

            foreach (var tool in recipe.Tools)
            {
                MeasurementTool copy = DeepCopyTool(tool);
                _tools.Add(copy);
                // Track max tool id for new-tool counter
                if (copy.Id != null && copy.Id.StartsWith("t") &&
                    int.TryParse(copy.Id.Substring(1), out int num) && num > _toolIdCounter)
                {
                    _toolIdCounter = num;
                }
            }

            RefreshToolList();
            SetPropertyPanelEnabled(false);
            MarkClean();
            UpdateTitle();

            if (_tools.Count > 0)
            {
                _toolListBox.SelectedIndex = 0;
            }
        }

        // ─── Save ──────────────────────────────────────────────────────

        // 一份配方只要有 1D 工具「或」量測模型物件即為有效內容。純 2D 量測模型
        // 配方（0 個 1D 工具）不該被擋存——量測模型隨 CopyRecipeMetadata 一併保存。
        private bool HasSavableContent()
            => _tools.Count > 0
               || (_recipe != null && _recipe.MetrologyModel != null
                   && _recipe.MetrologyModel.Objects != null
                   && _recipe.MetrologyModel.Objects.Count > 0);

        private void OnSave(object sender, EventArgs e)
        {
            if (!HasSavableContent())
            {
                MessageBox.Show(this, "Recipe has no 1D tools and no metrology model.", "Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_savePath))
            {
                OnSaveAs(sender, e);
                return;
            }

            try
            {
                var recipe = BuildRecipe();
                _recipeStore.Save(recipe, _savePath);
                MarkClean();
                UpdateTitle();
                _savedCallback?.Invoke(recipe, _savePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed: " + ex.Message, "Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnSaveAs(object sender, EventArgs e)
        {
            if (!HasSavableContent())
            {
                MessageBox.Show(this, "Recipe has no 1D tools and no metrology model.", "Save As",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new SaveFileDialog
            {
                Filter = "Recipe Files (*.zcp)|*.zcp|All Files (*.*)|*.*",
                DefaultExt = ".zcp",
                Title = "Save Recipe As"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var recipe = BuildRecipe();
                    _recipeStore.Save(recipe, dialog.FileName);
                    _savePath = dialog.FileName;
                    MarkClean();
                    UpdateTitle();
                    _savedCallback?.Invoke(recipe, _savePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Save failed: " + ex.Message, "Save As",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Produce a fresh deep copy each time so neither the file nor the MainWindow
        // callback shares mutable objects with the editor's working state.
        private Recipe BuildRecipe()
        {
            var recipe = CopyRecipeMetadata(_recipe);
            recipe.Name = !string.IsNullOrEmpty(_savePath)
                ? Path.GetFileNameWithoutExtension(_savePath)
                : "Untitled";
            foreach (var t in _tools)
                recipe.Tools.Add(DeepCopyTool(t));
            return recipe;
        }

        // Copies all recipe fields EXCEPT Tools (caller fills Tools). Preserves
        // reference pose / calibration / schema set elsewhere (e.g. Set Ref).
        // MetrologyModel is carried through unchanged so editing/saving a recipe in
        // the 1D editor does NOT wipe a metrology model defined in the metrology editor
        // (this method runs both on load → _recipe and on save → BuildRecipe).
        private static Recipe CopyRecipeMetadata(Recipe src)
        {
            // 委派給 Domain 的 CloneWithoutTools()：它以 MemberwiseClone 複製所有欄位，
            // 新增欄位會自動帶過去。原本這裡逐欄手寫，v16 的 TemplateModelId 就是這樣漏掉的
            // ——存檔會把 Set Ref 記錄的模板靜默清空，而配方檔看起來一切正常。
            return src.CloneWithoutTools();
        }

        private static MeasurementTool DeepCopyTool(MeasurementTool src)
        {
            var ep = src.EdgeParameters ?? EdgeDetectionParameters.Default();
            var tol = src.Tolerance ?? ToleranceSpec.Default();
            var roi = src.Roi ?? new RoiGeometry();
            return new MeasurementTool
            {
                Id = src.Id,
                Name = src.Name,
                ToolType = src.ToolType,
                // v10：circle 的 ROI 類型選擇（rect/sector）——單一 scalar，漏掉會在載入/存檔時
                // 遺失使用者選的扇形 ROI，變回矩形（RoiShape 預設 "rect"）。
                RoiShape = src.RoiShape,
                Roi = new RoiGeometry
                {
                    CenterRow = roi.CenterRow,
                    CenterCol = roi.CenterCol,
                    Length1 = roi.Length1,
                    Length2 = roi.Length2,
                    AngleRad = roi.AngleRad
                },
                EdgeParameters = new EdgeDetectionParameters
                {
                    Sigma = ep.Sigma,
                    Threshold = ep.Threshold,
                    Polarity = ep.Polarity,
                    EdgeSelector = ep.EdgeSelector,
                    HighThreshold = ep.HighThreshold,
                    Interpolation = ep.Interpolation,
                    MeasureMode = ep.MeasureMode
                },
                Tolerance = new ToleranceSpec
                {
                    Nominal = tol.Nominal,
                    LowerTolerance = tol.LowerTolerance,
                    UpperTolerance = tol.UpperTolerance,
                    Unit = tol.Unit
                },
                // 深複製弧形 ROI——漏掉會在載入/存檔時遺失弧形設定（ArcRoi 變 null →
                // RecipeValidator 直接判 Error），比照下方 Gdt 的處理。
                ArcRoi = src.ArcRoi == null ? null : new ArcMeasureRoi
                {
                    CenterRow = src.ArcRoi.CenterRow,
                    CenterCol = src.ArcRoi.CenterCol,
                    Radius = src.ArcRoi.Radius,
                    AngleStart = src.ArcRoi.AngleStart,
                    AngleExtent = src.ArcRoi.AngleExtent,
                    AnnulusRadius = src.ArcRoi.AnnulusRadius
                },
                // 深複製 GD&T 規格——漏掉會在存檔/重載時遺失形位公差設定。
                Gdt = src.Gdt == null ? null : new GdtToleranceSpec
                {
                    Characteristic = src.Gdt.Characteristic,
                    ToleranceZoneMm = src.Gdt.ToleranceZoneMm
                },
                // 深複製齒輪參數——漏掉會在載入/存檔時遺失齒輪判定設定（比照 ArcRoi/Gdt 的處理）。
                Gear = src.Gear == null ? null : new GearAnalysisParameters
                {
                    NominalToothCount = src.Gear.NominalToothCount,
                    ToothIsDark = src.Gear.ToothIsDark,
                    PitchToleranceDeg = src.Gear.PitchToleranceDeg,
                    WidthToleranceDeg = src.Gear.WidthToleranceDeg
                },
                // 深複製 PCD 參數——漏掉會在載入/存檔時遺失孔圈判定設定（比照 ArcRoi/Gdt/Gear 的處理）。
                Pcd = src.Pcd == null ? null : new PcdAnalysisParameters
                {
                    NominalHoleCount = src.Pcd.NominalHoleCount,
                    NominalPcdMm = src.Pcd.NominalPcdMm,
                    PcdToleranceMm = src.Pcd.PcdToleranceMm,
                    AngularToleranceDeg = src.Pcd.AngularToleranceDeg,
                    RadialToleranceMm = src.Pcd.RadialToleranceMm,
                    HoleIsDark = src.Pcd.HoleIsDark,
                    MinHoleAreaPx = src.Pcd.MinHoleAreaPx
                },
                // 深複製引腳間距參數——漏掉會在載入/存檔時遺失引腳判定設定（比照 ArcRoi/Gdt/Gear/Pcd 的處理）。
                // 必須逐欄複製 PinPitchAnalysisParameters 全部欄位，否則存後重載會悄悄還原成預設值。
                PinPitch = src.PinPitch == null ? null : new PinPitchAnalysisParameters
                {
                    NominalPinCount = src.PinPitch.NominalPinCount,
                    NominalPitchMm = src.PinPitch.NominalPitchMm,
                    PitchToleranceMm = src.PinPitch.PitchToleranceMm,
                    UniformityToleranceMm = src.PinPitch.UniformityToleranceMm,
                    PinIsDark = src.PinPitch.PinIsDark,
                    MinPinAreaPx = src.PinPitch.MinPinAreaPx
                },
                // 深複製孔陣列參數——漏掉會在載入/存檔時遺失網格判定設定（比照 ArcRoi/Gdt/Gear/Pcd/PinPitch 的處理）。
                // 必須逐欄複製 HoleArrayAnalysisParameters 全部 11 個欄位，否則存後重載會悄悄還原成預設值。
                HoleArray = src.HoleArray == null ? null : new HoleArrayAnalysisParameters
                {
                    Rows = src.HoleArray.Rows,
                    Cols = src.HoleArray.Cols,
                    NominalDiameterMm = src.HoleArray.NominalDiameterMm,
                    DiameterToleranceMm = src.HoleArray.DiameterToleranceMm,
                    NominalPitchXMm = src.HoleArray.NominalPitchXMm,
                    NominalPitchYMm = src.HoleArray.NominalPitchYMm,
                    PitchToleranceMm = src.HoleArray.PitchToleranceMm,
                    PositionToleranceMm = src.HoleArray.PositionToleranceMm,
                    HoleIsDark = src.HoleArray.HoleIsDark,
                    MinHoleAreaPx = src.HoleArray.MinHoleAreaPx,
                    MinCircularity = src.HoleArray.MinCircularity
                },
                RefToolIds = new List<string>(src.RefToolIds ?? new List<string>())
            };
        }

        private static bool IsGdtType(string t)
        {
            return t == "roundness" || t == "straightness" || t == "parallelism"
                || t == "perpendicularity" || t == "concentricity";
        }

        private static GdtCharacteristic GdtCharacteristicFor(string toolType)
        {
            switch (toolType)
            {
                case "straightness": return GdtCharacteristic.Straightness;
                case "parallelism": return GdtCharacteristic.Parallelism;
                case "perpendicularity": return GdtCharacteristic.Perpendicularity;
                case "concentricity": return GdtCharacteristic.Concentricity;
                default: return GdtCharacteristic.Roundness;
            }
        }

        // ─── Selection & population ────────────────────────────────────

        private void OnToolSelectionChanged(object sender, EventArgs e)
        {
            int idx = _toolListBox.SelectedIndex;
            if (idx < 0 || idx >= _tools.Count)
            {
                _selectedTool = null;
                SetPropertyPanelEnabled(false);
                _lease.EndRect2Edit();
                _lease.EndArcEdit();
                ClearEditorOverlayIfAny();
                _lease.ClearSelectionHighlight();
                RefreshTrialButtonEnabled();
                return;
            }

            _selectedTool = _tools[idx];
            SetPropertyPanelEnabled(true);
            PopulateFromTool(_selectedTool);
            ShowRoiEdit();
        }

        // 選工具時更新主視窗 overlay：circle/line 元素進入 rect2 互動編輯（拖曳把手畫在
        // persistent overlay 之上），GD&T/構造/複合工具只退出編輯。
        // 不再 ClearOverlay——保留量測結果 overlay（Run Recipe 的結果不會因選工具而消失）；
        // 編輯把手是疊加層，毋須清掉底圖，清掉反而會造成結果消失與 fallback 藍框。
        private void ShowRoiEdit()
        {
            if (_selectedTool == null)
            {
                _lease.EndRect2Edit();
                _lease.EndArcEdit();
                ClearEditorOverlayIfAny();
                _lease.ClearSelectionHighlight();
                return;
            }

            // 弧形/齒輪/PCD 工具：進入弧形互動編輯（BeginArcEdit 內部會自行關閉 rect2 編輯，故不重複呼叫
            // EndRect2Edit 以免多一次 Redraw）。齒輪/PCD 重用相同的量測環 ArcRoi。
            // v10：circle 選扇形 ROI（isSectorCircle）比照弧形工具，同樣走弧形互動編輯（借用 _arcGroup/ArcRoi）；
            // circle 選矩形 ROI 則不進此分支，落到下面 isElement 分支走既有 rect2 編輯。
            // 無 ArcRoi 或尚未載入影像時只收把手，不進編輯。
            bool isSectorCircle = _selectedTool.ToolType == "circle" && _selectedTool.RoiShape == "sector";
            if (_selectedTool.ToolType == "arc" || _selectedTool.ToolType == "gear" || _selectedTool.ToolType == "pcd" || isSectorCircle)
            {
                _lease.ClearSelectionHighlight();
                ArcMeasureRoi a = _selectedTool.ArcRoi;
                // Fix 6：未載入影像時不進入弧形編輯（比照 OnCaptureArc 的守衛），
                // 避免在沒有影像時悄悄進入 edit 狀態卻什麼都沒顯示。
                if (a == null || _imageHelper.CurrentImage == null)
                {
                    _lease.EndRect2Edit();
                    _lease.EndArcEdit();
                    ClearEditorOverlayIfAny();
                    return;
                }
                _lease.BeginArcEdit(a.CenterRow, a.CenterCol, a.Radius,
                    a.AngleStart, a.AngleExtent, a.AnnulusRadius, OnToolArcChanged);
                InstallArcBandOverlay();  // Fix 1c：弧帶 persistent overlay（把手畫在其上），與 MainWindow 一致
                return;
            }

            // 離開弧形工具 → 清掉編輯器自己裝的弧帶/試測 overlay，避免殘留到 circle/line/參照工具。
            // 只清編輯器自己那一層，不誤清主視窗的 Run Recipe 結果 overlay（由租約保證）。
            ClearEditorOverlayIfAny();

            // rect2 互動編輯：circle/line（元素）與 pin_pitch（引腳間距量測區）、hole_array（孔陣列量測區）都用 rect2 Roi。
            bool isElement = _selectedTool.ToolType == "circle" || _selectedTool.ToolType == "line"
                || _selectedTool.ToolType == "pin_pitch" || _selectedTool.ToolType == "hole_array";
            if (!isElement)
            {
                // 參照型工具（GD&T/距離/角度/構造）：高亮其參照的元素 ROI（青色，疊在量測
                // 結果之上），讓使用者一眼看出此工具作用在哪些特徵上。
                _lease.EndRect2Edit();
                _lease.EndArcEdit();
                var refs = GetReferencedElements(_selectedTool);
                if (refs.Count > 0)
                    _lease.SetSelectionHighlight(() => DrawReferencedElements(_imageHelper.Annotator, refs));
                else
                    _lease.ClearSelectionHighlight();
                return;
            }

            // 元素：以編輯把手指示選取，不需 ref 高亮。
            _lease.ClearSelectionHighlight();
            var roi = _selectedTool.Roi;
            _lease.BeginRect2Edit(roi.CenterRow, roi.CenterCol, roi.AngleRad,
                roi.Length1, roi.Length2, OnToolRect2Changed);
        }

        // Fix 1c：弧帶 persistent overlay。裝在共用主視窗 helper 上（單一 overlay slot），
        // 讀選中工具的 ArcRoi 畫量測帶；BeginArcEdit 的綠色把手於 Redraw 時疊在其上。
        // 數值變更（WriteArc）或把手拖曳（BeginArcEdit）觸發 Redraw 時，band 依最新 ArcRoi 重畫。
        private void InstallArcBandOverlay()
        {
            _lease.SetPersistentOverlay(() =>
            {
                var a = _selectedTool?.ArcRoi;
                if (a == null || !a.IsDefined) return;
                // 扇形 circle：畫封閉扇形（DrawSectorRoi，含起訖兩條徑向邊），與主頁一致，並和「弧形檢測開口環帶」
                // 明確區分，避免使用者誤以為是弧形抓邊工具。弧形/齒輪/PCD 維持開口環帶（DrawArcBand）。
                bool secCircle = _selectedTool != null && _selectedTool.ToolType == "circle" && _selectedTool.RoiShape == "sector";
                if (secCircle)
                    _imageHelper.Annotator.DrawSectorRoi(a.CenterRow, a.CenterCol, a.Radius,
                        a.AnnulusRadius, a.AngleStart, a.AngleExtent);
                else
                    _imageHelper.Annotator.DrawArcBand(a.CenterRow, a.CenterCol, a.Radius,
                        a.AngleStart, a.AngleExtent, a.AnnulusRadius);
            });
        }

        // 只清除「編輯器自己那一層」的 persistent overlay（弧帶或試測）。租約保證不會誤清
        // 主視窗的 Run Recipe 結果 overlay——清掉本層後，下層的結果會自動重新顯示。
        private void ClearEditorOverlayIfAny()
        {
            _lease.ClearPersistentOverlay();
        }

        // 取得某工具參照到的「元素」(circle/line，具 ROI)。構造工具等無 ROI 者不納入高亮。
        private List<MeasurementTool> GetReferencedElements(MeasurementTool tool)
        {
            var list = new List<MeasurementTool>();
            if (tool.RefToolIds == null) return list;
            foreach (string id in tool.RefToolIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                MeasurementTool t = _tools.Find(x => x.Id == id);
                if (t != null && t.Roi != null && (t.ToolType == "circle" || t.ToolType == "line"))
                    list.Add(t);
            }
            return list;
        }

        private static void DrawReferencedElements(OverlayAnnotator an, List<MeasurementTool> elements)
        {
            if (an == null) return;
            foreach (MeasurementTool t in elements)
            {
                RoiGeometry roi = t.Roi;
                an.DrawRectangle2(roi.CenterRow, roi.CenterCol, roi.AngleRad, roi.Length1, roi.Length2, "cyan");
                an.DrawText(t.Name ?? string.Empty, (int)roi.CenterRow, (int)roi.CenterCol, "cyan");
            }
        }

        // 滑鼠互動編輯回呼：回寫 RoiGeometry（弧度）與數值框，標記 dirty。
        private void OnToolRect2Changed(double cr, double cc, double phi, double l1, double l2)
        {
            if (_selectedTool == null) return;
            _selectedTool.Roi.CenterRow = cr;
            _selectedTool.Roi.CenterCol = cc;
            _selectedTool.Roi.AngleRad = phi;
            _selectedTool.Roi.Length1 = l1;
            _selectedTool.Roi.Length2 = l2;

            _updatingControls = true;
            try
            {
                _centerRowNumeric.Value = ClampDecimal(cr, _centerRowNumeric.Minimum, _centerRowNumeric.Maximum);
                _centerColNumeric.Value = ClampDecimal(cc, _centerColNumeric.Minimum, _centerColNumeric.Maximum);
                _length1Numeric.Value = ClampDecimal(l1, _length1Numeric.Minimum, _length1Numeric.Maximum);
                _length2Numeric.Value = ClampDecimal(l2, _length2Numeric.Minimum, _length2Numeric.Maximum);
                _angleRadNumeric.Value = ClampDecimal(phi, _angleRadNumeric.Minimum, _angleRadNumeric.Maximum);
            }
            finally
            {
                _updatingControls = false;
            }
            MarkDirty();
        }

        // 弧形把手拖曳回呼：回寫 ArcRoi（角度為弧度）與數值框，標記 dirty。
        // BeginArcEdit 交來的角度為弧度；起角先正規化到 [0, 2π)（比照 MainWindow.OnArcRoiChanged 的
        // [0,360) 正規化），避免環繞拖曳被數值框的 Minimum=0 夾掉。
        private void OnToolArcChanged(double cr, double cc, double radius,
            double angleStart, double angleExtent, double annulus)
        {
            if (_selectedTool == null || _selectedTool.ArcRoi == null) return;
            double twoPi = 2.0 * Math.PI;
            angleStart -= twoPi * Math.Floor(angleStart / twoPi); // 正規化到 [0, 2π)
            ArcMeasureRoi a = _selectedTool.ArcRoi;
            a.CenterRow = cr;
            a.CenterCol = cc;
            a.Radius = radius;
            a.AngleStart = angleStart;
            a.AngleExtent = angleExtent;
            a.AnnulusRadius = annulus;

            LoadArcFieldsFromSelectedTool();
            // 拖把手即回到「即時弧帶/扇形」overlay：若剛按過「在此試測」，其靜態結果 overlay 會蓋住即時框，
            // 這裡重裝即時 overlay，讓框隨把手動態更新（拖曳後由 HWindowControlHelper 的 Redraw 呈現）。
            InstallArcBandOverlay();
            MarkDirty();
        }

        // ArcRoi → 數值框。_updatingControls 用「存後還原」而非硬設 false，因為 PopulateFromTool
        // 會在自己的 guard 內呼叫本方法；若直接還原成 false 會提前解除外層 guard，
        // 使後續公差數值的 ValueChanged 真的觸發 WriteTolerance → 只是選個工具就被標記 dirty。
        private void LoadArcFieldsFromSelectedTool()
        {
            if (_selectedTool == null || _selectedTool.ArcRoi == null) return;
            ArcMeasureRoi a = _selectedTool.ArcRoi;
            bool prev = _updatingControls;
            _updatingControls = true;
            try
            {
                _arcCenterRowNumeric.Value = ClampDecimal(a.CenterRow, _arcCenterRowNumeric.Minimum, _arcCenterRowNumeric.Maximum);
                _arcCenterColNumeric.Value = ClampDecimal(a.CenterCol, _arcCenterColNumeric.Minimum, _arcCenterColNumeric.Maximum);
                _arcRadiusNumeric.Value = ClampDecimal(a.Radius, _arcRadiusNumeric.Minimum, _arcRadiusNumeric.Maximum);
                // ArcRoi 存弧度 → 數值框顯示度（rad→deg 轉換點）。
                _arcAngleStartNumeric.Value = ClampDecimal(a.AngleStart * 180.0 / Math.PI, _arcAngleStartNumeric.Minimum, _arcAngleStartNumeric.Maximum);
                _arcAngleExtentNumeric.Value = ClampDecimal(a.AngleExtent * 180.0 / Math.PI, _arcAngleExtentNumeric.Minimum, _arcAngleExtentNumeric.Maximum);
                _arcAnnulusNumeric.Value = ClampDecimal(a.AnnulusRadius, _arcAnnulusNumeric.Minimum, _arcAnnulusNumeric.Maximum);
            }
            finally
            {
                _updatingControls = prev;
            }
        }

        // GearAnalysisParameters → 控制項。比照 LoadArcFieldsFromSelectedTool：以「存後還原」而非硬設 false，
        // 因為 PopulateFromTool 會在自己的 guard 內呼叫本方法；提前還原成 false 會使後續齒輪參數的
        // ValueChanged 真的觸發 WriteGear → 只是選個工具就被標記 dirty。
        private void LoadGearFieldsFromSelectedTool()
        {
            if (_selectedTool == null || _selectedTool.Gear == null) return;
            GearAnalysisParameters g = _selectedTool.Gear;
            bool prev = _updatingControls;
            _updatingControls = true;
            try
            {
                _gearCountNumeric.Value = ClampDecimal(g.NominalToothCount, _gearCountNumeric.Minimum, _gearCountNumeric.Maximum);
                _gearDarkCheck.Checked = g.ToothIsDark;
                _gearPitchTolNumeric.Value = ClampDecimal(g.PitchToleranceDeg, _gearPitchTolNumeric.Minimum, _gearPitchTolNumeric.Maximum);
                _gearWidthTolNumeric.Value = ClampDecimal(g.WidthToleranceDeg, _gearWidthTolNumeric.Minimum, _gearWidthTolNumeric.Maximum);
            }
            finally
            {
                _updatingControls = prev;
            }
        }

        // PcdAnalysisParameters → 控制項。比照 LoadGearFieldsFromSelectedTool：以「存後還原」而非硬設 false，
        // 因為 PopulateFromTool 會在自己的 guard 內呼叫本方法；提前還原成 false 會使後續 PCD 參數的
        // ValueChanged 真的觸發 WritePcd → 只是選個工具就被標記 dirty。
        private void LoadPcdFieldsFromSelectedTool()
        {
            if (_selectedTool == null || _selectedTool.Pcd == null) return;
            PcdAnalysisParameters p = _selectedTool.Pcd;
            bool prev = _updatingControls;
            _updatingControls = true;
            try
            {
                _pcdCountNumeric.Value = ClampDecimal(p.NominalHoleCount, _pcdCountNumeric.Minimum, _pcdCountNumeric.Maximum);
                _pcdNominalNumeric.Value = ClampDecimal(p.NominalPcdMm, _pcdNominalNumeric.Minimum, _pcdNominalNumeric.Maximum);
                _pcdTolNumeric.Value = ClampDecimal(p.PcdToleranceMm, _pcdTolNumeric.Minimum, _pcdTolNumeric.Maximum);
                _pcdAngTolNumeric.Value = ClampDecimal(p.AngularToleranceDeg, _pcdAngTolNumeric.Minimum, _pcdAngTolNumeric.Maximum);
                _pcdRadTolNumeric.Value = ClampDecimal(p.RadialToleranceMm, _pcdRadTolNumeric.Minimum, _pcdRadTolNumeric.Maximum);
                _pcdDarkCheck.Checked = p.HoleIsDark;
                _pcdMinAreaNumeric.Value = ClampDecimal(p.MinHoleAreaPx, _pcdMinAreaNumeric.Minimum, _pcdMinAreaNumeric.Maximum);
            }
            finally
            {
                _updatingControls = prev;
            }
        }

        // PinPitchAnalysisParameters → 控制項。比照 LoadPcdFieldsFromSelectedTool：以「存後還原」而非硬設 false，
        // 因為 PopulateFromTool 會在自己的 guard 內呼叫本方法；提前還原成 false 會使後續 pin 參數的
        // ValueChanged 真的觸發 WritePinPitch → 只是選個工具就被標記 dirty。
        private void LoadPinPitchFieldsFromSelectedTool()
        {
            if (_selectedTool == null || _selectedTool.PinPitch == null) return;
            PinPitchAnalysisParameters p = _selectedTool.PinPitch;
            bool prev = _updatingControls;
            _updatingControls = true;
            try
            {
                _pinCountNumeric.Value = ClampDecimal(p.NominalPinCount, _pinCountNumeric.Minimum, _pinCountNumeric.Maximum);
                _pinPitchNumeric.Value = ClampDecimal(p.NominalPitchMm, _pinPitchNumeric.Minimum, _pinPitchNumeric.Maximum);
                _pinPitchTolNumeric.Value = ClampDecimal(p.PitchToleranceMm, _pinPitchTolNumeric.Minimum, _pinPitchTolNumeric.Maximum);
                _pinUniformTolNumeric.Value = ClampDecimal(p.UniformityToleranceMm, _pinUniformTolNumeric.Minimum, _pinUniformTolNumeric.Maximum);
                _pinDarkCheck.Checked = p.PinIsDark;
                _pinMinAreaNumeric.Value = ClampDecimal(p.MinPinAreaPx, _pinMinAreaNumeric.Minimum, _pinMinAreaNumeric.Maximum);
            }
            finally
            {
                _updatingControls = prev;
            }
        }

        // HoleArrayAnalysisParameters → 控制項。比照 LoadPinPitchFieldsFromSelectedTool：以「存後還原」而非硬設 false，
        // 因為 PopulateFromTool 會在自己的 guard 內呼叫本方法；提前還原成 false 會使後續孔陣列參數的
        // ValueChanged 真的觸發 WriteHoleArray → 只是選個工具就被標記 dirty。
        private void LoadHoleArrayFieldsFromSelectedTool()
        {
            if (_selectedTool == null || _selectedTool.HoleArray == null) return;
            HoleArrayAnalysisParameters h = _selectedTool.HoleArray;
            bool prev = _updatingControls;
            _updatingControls = true;
            try
            {
                _holeRowsNumeric.Value = ClampDecimal(h.Rows, _holeRowsNumeric.Minimum, _holeRowsNumeric.Maximum);
                _holeColsNumeric.Value = ClampDecimal(h.Cols, _holeColsNumeric.Minimum, _holeColsNumeric.Maximum);
                _holeDiameterNumeric.Value = ClampDecimal(h.NominalDiameterMm, _holeDiameterNumeric.Minimum, _holeDiameterNumeric.Maximum);
                _holeDiameterTolNumeric.Value = ClampDecimal(h.DiameterToleranceMm, _holeDiameterTolNumeric.Minimum, _holeDiameterTolNumeric.Maximum);
                _holePitchXNumeric.Value = ClampDecimal(h.NominalPitchXMm, _holePitchXNumeric.Minimum, _holePitchXNumeric.Maximum);
                _holePitchYNumeric.Value = ClampDecimal(h.NominalPitchYMm, _holePitchYNumeric.Minimum, _holePitchYNumeric.Maximum);
                _holePitchTolNumeric.Value = ClampDecimal(h.PitchToleranceMm, _holePitchTolNumeric.Minimum, _holePitchTolNumeric.Maximum);
                _holePositionTolNumeric.Value = ClampDecimal(h.PositionToleranceMm, _holePositionTolNumeric.Minimum, _holePositionTolNumeric.Maximum);
                _holeDarkCheck.Checked = h.HoleIsDark;
                _holeMinAreaNumeric.Value = ClampDecimal(h.MinHoleAreaPx, _holeMinAreaNumeric.Minimum, _holeMinAreaNumeric.Maximum);
                _holeMinCircularityNumeric.Value = ClampDecimal(h.MinCircularity, _holeMinCircularityNumeric.Minimum, _holeMinCircularityNumeric.Maximum);
            }
            finally
            {
                _updatingControls = prev;
            }
        }

        private void PopulateFromTool(MeasurementTool tool)
        {
            _updatingControls = true;
            try
            {
                _nameTextBox.Text = tool.Name ?? "";
                _idTextBox.Text = tool.Id ?? "";
                _typeLabel.Text = tool.ToolType ?? "-";

                bool isElement = tool.ToolType == "circle" || tool.ToolType == "line";
                bool isArc = tool.ToolType == "arc";
                bool isGear = tool.ToolType == "gear";
                bool isPcd = tool.ToolType == "pcd";
                // 引腳間距工具：量測區用 rect2 Roi（沿用 _roiGroup，比照 circle/line），另顯示引腳判定參數群組；
                // 判定走引腳三條件（腳數/間距/均勻度），自判定，故隱藏雙邊 Tolerance；偵測走 blob 非邊緣掃描，故不顯示 _edgeGroup。
                bool isPinPitch = tool.ToolType == "pin_pitch";
                // 孔陣列工具：量測區用 rect2 Roi（沿用 _roiGroup，比照 pin_pitch），另顯示孔陣列判定參數群組；
                // 判定走五條件（孔數/孔徑/X 孔距/Y 孔距/位置度），自判定，故隱藏雙邊 Tolerance；偵測走 blob 非邊緣掃描，故不顯示 _edgeGroup。
                bool isHoleArray = tool.ToolType == "hole_array";
                // v10：circle 選扇形 ROI → 借用弧形群組（_arcGroup/ArcRoi），矩形 ROI 群組改隱藏。
                // 僅 circle 工具讀 RoiShape；其餘工具型別忽略（isSectorCircle 恆為 false）。
                bool isSectorCircle = tool.ToolType == "circle" && tool.RoiShape == "sector";
                bool isConstruction = tool.ToolType == "intersection" || tool.ToolType == "midline" || tool.ToolType == "projection";
                bool isComposite = tool.ToolType == "distance" || tool.ToolType == "angle";
                bool isGdt = IsGdtType(tool.ToolType);
                bool usesRefs = isComposite || isConstruction || isGdt;

                // Element tools (circle/line): ROI + Edge. Construction/composite/GD&T: RefTools.
                // GD&T 走單邊 Gdt 群組並隱藏雙邊 Tolerance；其餘維持雙邊。
                // 弧形工具走 ArcRoi：顯示弧形群組、隱藏 rect2 ROI 群組（其 Roi 未被使用，
                // RecipeValidator 的 RoiElementTypes 也不含 "arc"）；但邊緣參數仍有用——
                // RecipeRunner Pass 1.2 會把 tool.EdgeParameters 傳給 DetectEdgesOnArc。
                // 齒輪工具重用弧形 ROI 群組（量測環）+ 邊緣參數（弧卡尺量邊），並顯示齒輪參數群組；
                // 齒輪判定走三條件（齒數/齒距/齒寬），不用雙邊 Tolerance 群組，故一併隱藏。
                // PCD 工具同樣重用弧形 ROI 群組（環帶偵測孔），但不用邊緣參數（孔偵測非邊緣掃描，故
                // 不加進 _edgeGroup 的可見條件），並顯示 PCD 參數群組；PCD 判定走四條件，同樣隱藏雙邊 Tolerance。
                // 扇形 circle（isSectorCircle）比照弧形工具：借用 _arcGroup，矩形 _roiGroup 隱藏；
                // 邊緣參數/雙邊 Tolerance 維持（isElement 已含 circle，兩者條件本就成立，不必額外加項）。
                _roiGroup.Visible = (isElement || isPinPitch || isHoleArray) && !isSectorCircle;
                _arcGroup.Visible = isArc || isGear || isPcd || isSectorCircle;
                _gearGroup.Visible = isGear;
                _pcdGroup.Visible = isPcd;
                _pinGroup.Visible = isPinPitch;
                _holeArrayGroup.Visible = isHoleArray;
                _edgeGroup.Visible = isElement || isArc || isGear;
                _refGroup.Visible = usesRefs;
                _toleranceGroup.Visible = !isGdt && !isGear && !isPcd && !isPinPitch && !isHoleArray;
                _gdtGroup.Visible = isGdt;
                _angleHintLabel.Visible = tool.ToolType == "line";

                // v10：ROI 類型下拉只在 circle 工具顯示；其餘工具型別隱藏（combo 對它們無意義）。
                _roiTypeCombo.Visible = tool.ToolType == "circle";
                if (tool.ToolType == "circle")
                    _roiTypeCombo.SelectedIndex = isSectorCircle ? 1 : 0;

                if (isGdt && tool.Gdt != null)
                {
                    _gdtCharLabel.Text = GdtCharLabelText(tool.ToolType);
                    _gdtZoneNumeric.Value = ClampDecimal(tool.Gdt.ToleranceZoneMm, _gdtZoneNumeric.Minimum, _gdtZoneNumeric.Maximum);
                }

                // 引腳間距/孔陣列工具亦用 rect2 Roi（比照 circle/line），故一同填 rect2 幾何欄位。
                if ((isElement || isPinPitch || isHoleArray) && !isSectorCircle)
                {
                    _centerRowNumeric.Value = ClampDecimal(tool.Roi.CenterRow, _centerRowNumeric.Minimum, _centerRowNumeric.Maximum);
                    _centerColNumeric.Value = ClampDecimal(tool.Roi.CenterCol, _centerColNumeric.Minimum, _centerColNumeric.Maximum);
                    _length1Numeric.Value = ClampDecimal(tool.Roi.Length1, _length1Numeric.Minimum, _length1Numeric.Maximum);
                    _length2Numeric.Value = ClampDecimal(tool.Roi.Length2, _length2Numeric.Minimum, _length2Numeric.Maximum);
                    _angleRadNumeric.Value = ClampDecimal(tool.Roi.AngleRad, _angleRadNumeric.Minimum, _angleRadNumeric.Maximum);
                }
                else if (isArc || isSectorCircle)
                {
                    LoadArcFieldsFromSelectedTool();
                }
                else if (isGear)
                {
                    // 齒輪工具同時擁有量測環（ArcRoi）與齒輪判定參數，兩者都在 guard 內載入。
                    LoadArcFieldsFromSelectedTool();
                    LoadGearFieldsFromSelectedTool();
                }
                else if (isPcd)
                {
                    // PCD 工具同時擁有量測環（ArcRoi）與孔圈判定參數，兩者都在 guard 內載入（比照齒輪）。
                    LoadArcFieldsFromSelectedTool();
                    LoadPcdFieldsFromSelectedTool();
                }
                else if (usesRefs)
                {
                    PopulateRefCombos(tool);
                }

                // 引腳間距工具的 rect2 幾何已在上方 (isElement || isPinPitch) 分支填好；此處再填引腳判定參數
                // （比照齒輪/PCD 同時載入量測 ROI 與判定參數）。放在 if/else 鏈外，因 pin_pitch 已被首個 if 消耗。
                if (isPinPitch)
                {
                    LoadPinPitchFieldsFromSelectedTool();
                }

                // 孔陣列工具同理：rect2 幾何已在上方分支填好，此處補填網格判定參數（比照 pin_pitch）。
                if (isHoleArray)
                {
                    LoadHoleArrayFieldsFromSelectedTool();
                }

                // 邊緣參數：circle/line（rect2 卡尺）、arc（弧形卡尺）與 gear（齒輪弧卡尺量邊）都會用到。
                if (isElement || isArc || isGear)
                {
                    _sigmaNumeric.Value = ClampDecimal(tool.EdgeParameters.Sigma, _sigmaNumeric.Minimum, _sigmaNumeric.Maximum);
                    _thresholdNumeric.Value = ClampDecimal(tool.EdgeParameters.Threshold, _thresholdNumeric.Minimum, _thresholdNumeric.Maximum);
                    SelectCombo(_polarityCombo, tool.EdgeParameters.Polarity);
                    SelectCombo(_selectorCombo, tool.EdgeParameters.EdgeSelector);
                    SelectCombo(_interpolationCombo, tool.EdgeParameters.Interpolation);
                    SelectCombo(_measureModeCombo, tool.EdgeParameters.MeasureMode);
                }

                _nominalNumeric.Value = ClampDecimal(tool.Tolerance.Nominal, _nominalNumeric.Minimum, _nominalNumeric.Maximum);
                _lowerNumeric.Value = ClampDecimal(tool.Tolerance.LowerTolerance, _lowerNumeric.Minimum, _lowerNumeric.Maximum);
                _upperNumeric.Value = ClampDecimal(tool.Tolerance.UpperTolerance, _upperNumeric.Minimum, _upperNumeric.Maximum);
                _unitTextBox.Text = tool.Tolerance.Unit ?? "mm";
            }
            finally
            {
                _updatingControls = false;
            }

            // 數值已填好（guard 已解除）→ 刷新公差預覽與試測按鈕狀態。
            UpdateTolerancePreview();
            RefreshTrialButtonEnabled();
        }

        // distance: line/circle/intersection/midline/projection; angle: line/midline;
        // intersection/midline: line; projection: circle + line。
        private void PopulateRefCombos(MeasurementTool tool)
        {
            _ref1Combo.Items.Clear();
            _ref2Combo.Items.Clear();

            foreach (var t in _tools)
            {
                if (!IsAllowedRef(tool.ToolType, t.ToolType)) continue;
                var item = new ToolRef
                {
                    Id = t.Id,
                    Display = string.Format(CultureInfo.InvariantCulture, "{0} ({1})", t.Name, t.Id)
                };
                _ref1Combo.Items.Add(item);
                _ref2Combo.Items.Add(item);
            }

            string id1 = tool.RefToolIds.Count > 0 ? tool.RefToolIds[0] : null;
            string id2 = tool.RefToolIds.Count > 1 ? tool.RefToolIds[1] : null;
            SelectRefCombo(_ref1Combo, id1);
            SelectRefCombo(_ref2Combo, id2);
        }

        // 依「目前工具型別」決定可作為其參考的「候選工具型別」。
        private static bool IsAllowedRef(string ownerType, string candidateType)
        {
            switch (ownerType)
            {
                case "distance":
                    return candidateType == "line" || candidateType == "circle"
                        || candidateType == "intersection" || candidateType == "midline"
                        || candidateType == "projection";
                case "angle":
                    return candidateType == "line" || candidateType == "midline";
                case "intersection":
                case "midline":
                    return candidateType == "line";
                case "projection":
                    return candidateType == "line" || candidateType == "circle";
                case "roundness":
                case "concentricity":
                    return candidateType == "circle";
                case "straightness":
                case "parallelism":
                case "perpendicularity":
                    return candidateType == "line";
                default:
                    return false;
            }
        }

        private static string GdtCharLabelText(string toolType)
        {
            switch (toolType)
            {
                case "roundness": return "真圓度 Roundness";
                case "straightness": return "真直度 Straightness";
                case "parallelism": return "平行度 Parallelism";
                case "perpendicularity": return "垂直度 Perpendicularity";
                case "concentricity": return "同心度 Concentricity";
                default: return toolType;
            }
        }

        private static void SelectRefCombo(ComboBox combo, string id)
        {
            if (string.IsNullOrEmpty(id)) { combo.SelectedIndex = -1; return; }
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ToolRef tr && tr.Id == id)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            combo.SelectedIndex = -1;
        }

        private void SetPropertyPanelEnabled(bool enabled)
        {
            // Keep all controls enabled so ToolTips always work.
            // Individual handlers already guard on _selectedTool == null.
            if (!enabled)
            {
                _updatingControls = true;
                try
                {
                    _nameTextBox.Text = "";
                    _idTextBox.Text = "";
                    _typeLabel.Text = "-";
                }
                finally
                {
                    _updatingControls = false;
                }
            }
        }

        // ─── ROI capture callback ──────────────────────────────────────

        private void OnCaptureRoi(object sender, EventArgs e)
        {
            if (_selectedTool == null) return;
            if (_selectedTool.ToolType != "circle" && _selectedTool.ToolType != "line") return;

            try
            {
                _lease.RequestRoi((startRow, startCol, endRow, endCol) =>
                {
                    double centerRow = (startRow + endRow) / 2.0;
                    double centerCol = (startCol + endCol) / 2.0;
                    double rowExt = Math.Abs(endRow - startRow) / 2.0;
                    double colExt = Math.Abs(endCol - startCol) / 2.0;

                    // rect2 convention: Length1 along major axis (AngleRad), Length2 perpendicular.
                    // Auto-detect major axis orientation from drawn rectangle aspect ratio.
                    double length1, length2, angleRad;
                    if (colExt >= rowExt)
                    {
                        length1 = colExt;
                        length2 = rowExt;
                        angleRad = 0.0;
                    }
                    else
                    {
                        length1 = rowExt;
                        length2 = colExt;
                        angleRad = Math.PI / 2.0;
                    }

                    if (InvokeRequired)
                    {
                        BeginInvoke(new Action(() =>
                            ApplyRoiCapture(centerRow, centerCol, length1, length2, angleRad)));
                    }
                    else
                    {
                        ApplyRoiCapture(centerRow, centerCol, length1, length2, angleRad);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "ROI capture failed: " + ex.Message, "ROI Capture",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // 弧形 ROI 沒有「拉一個矩形」的擷取語意（RequestRoi 是 rect2 專用），改為直接進入
        // 弧形互動編輯：在主影像上拖曳把手調整弧心/半徑/起訖角/環寬，即時回寫。
        private void OnCaptureArc(object sender, EventArgs e)
        {
            if (_selectedTool == null || _selectedTool.ArcRoi == null) return;
            if (_imageHelper.CurrentImage == null)
            {
                MessageBox.Show(this, "請先在主視窗載入影像。", "弧形 ROI",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ArcMeasureRoi a = _selectedTool.ArcRoi;
            _lease.ClearSelectionHighlight();
            _lease.BeginArcEdit(a.CenterRow, a.CenterCol, a.Radius,
                a.AngleStart, a.AngleExtent, a.AnnulusRadius, OnToolArcChanged);
            InstallArcBandOverlay();  // Fix 1c：明確進入弧形編輯時（也）確保弧帶 overlay 已裝上
        }

        private void ApplyRoiCapture(double centerRow, double centerCol, double length1, double length2, double angleRad)
        {
            if (_selectedTool == null) return;

            _selectedTool.Roi.CenterRow = centerRow;
            _selectedTool.Roi.CenterCol = centerCol;
            _selectedTool.Roi.Length1 = length1;
            _selectedTool.Roi.Length2 = length2;
            _selectedTool.Roi.AngleRad = angleRad;

            _updatingControls = true;
            try
            {
                _centerRowNumeric.Value = ClampDecimal(centerRow, _centerRowNumeric.Minimum, _centerRowNumeric.Maximum);
                _centerColNumeric.Value = ClampDecimal(centerCol, _centerColNumeric.Minimum, _centerColNumeric.Maximum);
                _length1Numeric.Value = ClampDecimal(length1, _length1Numeric.Minimum, _length1Numeric.Maximum);
                _length2Numeric.Value = ClampDecimal(length2, _length2Numeric.Minimum, _length2Numeric.Maximum);
                _angleRadNumeric.Value = ClampDecimal(angleRad, _angleRadNumeric.Minimum, _angleRadNumeric.Maximum);
            }
            finally
            {
                _updatingControls = false;
            }
            MarkDirty();
            ShowRoiEdit();
        }

        // v15：編輯本配方的影像品質門檻。門檻存在 _recipe 上（非工具層級），
        // 存檔時由 CopyRecipeMetadata 帶進輸出的 Recipe。
        private void OnEditIqcThresholds(object sender, EventArgs e)
        {
            using (var dlg = new IqcThresholdsDialog(_recipe.IqcThresholds))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                // 只有真的變了才標記為未存檔，避免「開啟後直接按確定」也讓配方變 dirty。
                if (!SameThresholds(_recipe.IqcThresholds, dlg.Result))
                {
                    _recipe.IqcThresholds = dlg.Result;
                    MarkDirty();
                }
            }
        }

        private static bool SameThresholds(ImageQualityThresholds a, ImageQualityThresholds b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.MinBrightness == b.MinBrightness
                && a.MaxBrightness == b.MaxBrightness
                && a.MaxSaturationRatio == b.MaxSaturationRatio
                && a.MinBlurScore == b.MinBlurScore
                && a.MinContrast == b.MinContrast;
        }

        // ─── Dirty tracking ────────────────────────────────────────────

        private void MarkDirty()
        {
            if (_dirty) return;
            _dirty = true;
            UpdateTitle();
        }

        private void MarkClean()
        {
            _dirty = false;
            UpdateTitle();
        }

        private bool ConfirmDiscardIfDirty()
        {
            if (!_dirty) return true;
            DialogResult r = MessageBox.Show(this,
                "There are unsaved changes. Save before continuing?",
                "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes)
            {
                OnSave(this, EventArgs.Empty);
                return !_dirty; // if save was cancelled (no path chosen), stay
            }
            return true; // No → discard
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscardIfDirty())
                e.Cancel = true;
        }

        // ─── Helpers ───────────────────────────────────────────────────

        private void UpdateTitle()
        {
            string fileName = !string.IsNullOrEmpty(_savePath)
                ? Path.GetFileName(_savePath)
                : "Untitled";
            string dirtyMark = _dirty ? " *" : "";
            string toolCount = _tools.Count == 1 ? " (1 tool)" : " (" + _tools.Count + " tools)";
            Text = "Recipe Editor - " + fileName + dirtyMark + toolCount;
            _filePathLabel.Text = !string.IsNullOrEmpty(_savePath)
                ? _savePath + dirtyMark
                : "New recipe (unsaved)";
        }

        private static void SelectCombo(ComboBox combo, string value)
        {
            int idx = combo.FindStringExact(value);
            if (idx >= 0) combo.SelectedIndex = idx;
        }

        private static decimal ClampDecimal(double value, decimal min, decimal max)
        {
            decimal d = (decimal)value;
            if (d < min) return min;
            if (d > max) return max;
            return d;
        }

        private void RefreshToolList()
        {
            int prev = _toolListBox.SelectedIndex;
            _toolListBox.BeginUpdate();
            _toolListBox.Items.Clear();
            foreach (var t in _tools)
                _toolListBox.Items.Add(string.Format(CultureInfo.InvariantCulture, "{0}  [{1}]", t.Name, t.ToolType));
            _toolListBox.EndUpdate();
            if (prev >= 0 && prev < _tools.Count)
                _toolListBox.SelectedIndex = prev;
            UpdateTitle();
            RefreshTrialButtonEnabled();
        }

        private void SetupToolTips()
        {
            _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 600, ReshowDelay = 300, ShowAlways = true };

            _toolTip.SetToolTip(_newButton, "Start a new empty recipe (prompts to save current changes)");
            _toolTip.SetToolTip(_loadButton, "Load a recipe from a .zcp file");
            _toolTip.SetToolTip(_addCircleButton, "Add a circle measurement tool");
            _toolTip.SetToolTip(_addLineButton, "Add a line measurement tool");
            _toolTip.SetToolTip(_addArcButton, "弧形卡尺：量圓周等分特徵邊數（孔數/齒數/引腳數）");
            _toolTip.SetToolTip(_addGearButton, "齒輪：量齒數/齒距/齒寬（背光剪影）");
            _toolTip.SetToolTip(_addPcdButton, "PCD 螺栓孔圈：量孔數/節圓直徑/角均勻/真圓度（背光）");
            _toolTip.SetToolTip(_addPinButton, "新增引腳間距工具（一排引腳的腳數/間距/均勻度/缺腳）");
            _toolTip.SetToolTip(_addHoleArrayButton, "新增孔陣列工具（rows×cols 網格的孔數/孔徑/X-Y 孔距/位置度）");
            _toolTip.SetToolTip(_addDistanceButton, "Add a distance tool (between two line/circle tools)");
            _toolTip.SetToolTip(_addAngleButton, "Add an angle tool (between two line tools)");
            _toolTip.SetToolTip(_addRoundnessButton, "真圓度：Ref1=一個 circle，偏差=圓擬合 max-min 徑向");
            _toolTip.SetToolTip(_addStraightnessButton, "真直度：Ref1=一個 line，偏差=線擬合殘差 RMS（v1 近似）");
            _toolTip.SetToolTip(_addParallelismButton, "平行度：Ref1=量測 line，Ref2=基準 line");
            _toolTip.SetToolTip(_addPerpendicularityButton, "垂直度：Ref1=量測 line，Ref2=基準 line");
            _toolTip.SetToolTip(_addConcentricityButton, "同心度：Ref1=量測 circle，Ref2=基準 circle");
            _toolTip.SetToolTip(_deleteButton, "Delete the currently selected tool");
            _toolTip.SetToolTip(_saveButton, "Save the recipe to the current file");
            _toolTip.SetToolTip(_saveAsButton, "Save the recipe to a new .zcp file");
            _toolTip.SetToolTip(_trialMeasureButton, "試測目前選中的 circle/line 工具（需已載入影像）");
            _toolTip.SetToolTip(_nameTextBox, "Tool display name (appears in result table)");
            _toolTip.SetToolTip(_idTextBox, "Unique tool ID (auto-generated, read-only)");
            _toolTip.SetToolTip(_typeLabel, "Tool type: circle, line, distance, or angle");
            _toolTip.SetToolTip(_centerRowNumeric, "ROI center row (pixels)");
            _toolTip.SetToolTip(_centerColNumeric, "ROI center column (pixels)");
            _toolTip.SetToolTip(_length1Numeric, "ROI half-length along major axis (pixels)");
            _toolTip.SetToolTip(_length2Numeric, "ROI half-length perpendicular to major axis (pixels)");
            _toolTip.SetToolTip(_angleRadNumeric, "ROI major axis angle in radians");
            _toolTip.SetToolTip(_captureRoiButton, "Draw a rectangle on the main image to capture the ROI");
            _toolTip.SetToolTip(_arcCenterRowNumeric, "弧心 row（像素）");
            _toolTip.SetToolTip(_arcCenterColNumeric, "弧心 column（像素）");
            _toolTip.SetToolTip(_arcRadiusNumeric, "掃描半徑（像素）");
            _toolTip.SetToolTip(_arcAngleStartNumeric, "起始角（度，0..360）");
            _toolTip.SetToolTip(_arcAngleExtentNumeric, "角度範圍（度，360 為整圈；負值為順時針）");
            _toolTip.SetToolTip(_arcAnnulusNumeric, "環寬一半（像素）");
            _toolTip.SetToolTip(_captureArcButton, "在主影像上拖曳把手調整弧形 ROI");
            _toolTip.SetToolTip(_gearCountNumeric, "標稱齒數（整數）：實測齒數需等於此值才判 OK");
            _toolTip.SetToolTip(_gearDarkCheck, "背光剪影下齒為暗、齒隙為亮時勾選；決定齒邊配對極性");
            _toolTip.SetToolTip(_gearPitchTolNumeric, "齒距最大偏差上限（度）：實測齒距最大偏差 ≤ 此值才判 OK");
            _toolTip.SetToolTip(_gearWidthTolNumeric, "齒寬最大偏差上限（度）：實測齒寬最大偏差 ≤ 此值才判 OK");
            _toolTip.SetToolTip(_pcdCountNumeric, "標稱孔數（整數）：實測孔數需等於此值才判 OK");
            _toolTip.SetToolTip(_pcdNominalNumeric, "標稱節圓直徑 PCD（mm）");
            _toolTip.SetToolTip(_pcdTolNumeric, "PCD 公差（mm）：|實測PCD − 標稱PCD| ≤ 此值才判 OK");
            _toolTip.SetToolTip(_pcdAngTolNumeric, "角度公差（度）：相鄰孔角距對均值的最大偏差 ≤ 此值才判 OK");
            _toolTip.SetToolTip(_pcdRadTolNumeric, "徑向公差（mm）：孔心徑向偏差上限，判定真圓度");
            _toolTip.SetToolTip(_pcdDarkCheck, "背光下孔為暗、板材為亮時勾選；決定孔偵測極性");
            _toolTip.SetToolTip(_pcdMinAreaNumeric, "孔 blob 最小面積（像素）：濾除雜訊，小於此值不視為孔");
            _toolTip.SetToolTip(_sigmaNumeric, "Gaussian smoothing sigma for edge detection");
            _toolTip.SetToolTip(_thresholdNumeric, "Minimum edge amplitude threshold");
            _toolTip.SetToolTip(_polarityCombo, "Edge polarity: all, positive (dark→bright), or negative (bright→dark)");
            _toolTip.SetToolTip(_selectorCombo, "Which edge(s) to return: all, first, or last");
            _toolTip.SetToolTip(_interpolationCombo, "Interpolation method for edge detection");
            _toolTip.SetToolTip(_measureModeCombo, "Single edge or edge pair measurement mode");
            _toolTip.SetToolTip(_ref1Combo, "First reference tool (for distance or angle)");
            _toolTip.SetToolTip(_ref2Combo, "Second reference tool (for distance or angle)");
            _toolTip.SetToolTip(_nominalNumeric, "Nominal (expected) measurement value");
            _toolTip.SetToolTip(_lowerNumeric, "Lower tolerance (negative deviation from nominal)");
            _toolTip.SetToolTip(_upperNumeric, "Upper tolerance (positive deviation from nominal)");
            _toolTip.SetToolTip(_unitTextBox, "Tolerance unit: 'mm' for distance/diameter, 'deg' for angle");
            _toolTip.SetToolTip(_angleHintLabel, "Line angle tolerance judgment: use Unit='deg' to judge the line's angle instead of length");
            _toolTip.SetToolTip(_gdtZoneNumeric, "形位公差帶寬 T（mm，單邊）：OK 條件為 0 ≤ 偏差 ≤ T");
            _toolTip.SetToolTip(_gdtCharLabel, "形位公差特性（由工具型別決定）");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tools.Clear();
                _toolTip?.Dispose();  // ToolTip 未加入 components 容器；編輯器可重複開關，手動釋放原生 handle
            }
            base.Dispose(disposing);
        }
    }
}
