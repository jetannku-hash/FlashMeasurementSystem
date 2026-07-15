using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.Gdt;
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
        private bool _editorOwnsEdit;  // L2：追蹤編輯把手是否為編輯器持有，關閉時只拆自己的
        private bool _editorInstalledOverlay;  // #3：試測是否在共用 helper 裝過 persistent overlay，關閉時清掉
        private string _savePath;

        // ── Toolbar controls ──
        private Label _filePathLabel;
        private Button _newButton;
        private Button _loadButton;
        private Button _addCircleButton;
        private Button _addLineButton;
        private Button _addArcButton;
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

        // ── Left: tool list ──
        private ListBox _toolListBox;

        // ── Right: property groups ──
        private GroupBox _commonGroup;
        private TextBox _nameTextBox;
        private TextBox _idTextBox;
        private Label _typeLabel;

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
            _savePath = filePath;
            _savedCallback = savedCallback;
            _trialMeasure = trialMeasure;

            MinimumSize = new Size(580, 440);

            BuildLayout();
            SetupToolTips();
            SetPropertyPanelEnabled(false);

            if (recipe != null)
                LoadFromRecipe(recipe);

            FormClosing += OnFormClosing;
            this.FormClosed += (s, e) =>
            {
                // L2：只拆除編輯器自己持有的 edit/highlight，不誤殺主視窗的。
                if (_editorOwnsEdit)
                {
                    _imageHelper.EndRect2Edit();
                    _imageHelper.EndArcEdit();   // 弧形工具的把手同樣由編輯器持有，一併拆除
                    _editorOwnsEdit = false;
                }
                _imageHelper.ClearSelectionHighlight();
                // #3：試測會把 persistent overlay 裝在共用主視窗 helper 上；只有裝過才清除，
                // 避免殘留橘色試測 ROI + 綠擬合，也避免無試測時誤清主視窗自身的 overlay。
                if (_editorInstalledOverlay) { _imageHelper.ClearOverlay(); _editorInstalledOverlay = false; }
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

            bar.Controls.Add(_newButton);
            bar.Controls.Add(_loadButton);
            bar.Controls.Add(_addCircleButton);
            bar.Controls.Add(_addLineButton);
            bar.Controls.Add(_addArcButton);
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
            // 7 列 × 28px（6 個數值 + 擷取按鈕）+ 標題與內距。roi 群組同構但少一列。
            _arcGroup = CreateGroupBox("弧形 ROI (Arc)", parent, 240);
            _roiGroup = CreateGroupBox("ROI Geometry", parent, 210);
            _commonGroup = CreateGroupBox("Common", parent, 130);

            FillCommonGroup(_commonGroup);
            FillRoiGroup(_roiGroup);
            FillArcGroup(_arcGroup);
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

        // 弧形 ROI（gen_measure_arc 語意）：角度一律弧度，與 ROI 群組的 Angle (rad) 同單位。
        private void FillArcGroup(GroupBox gb)
        {
            var t = NewTable();
            int r = 0;

            _arcCenterRowNumeric = AddNumericRow(t, "弧心 Row", ref r, 0M, 100000M, 2, 200M, 1M);
            _arcCenterColNumeric = AddNumericRow(t, "弧心 Col", ref r, 0M, 100000M, 2, 200M, 1M);
            _arcRadiusNumeric = AddNumericRow(t, "半徑 (px)", ref r, 0M, 100000M, 2, 100M, 1M);
            _arcAngleStartNumeric = AddNumericRow(t, "起始角 (rad)", ref r, -7M, 7M, 4, 0M, 0.1M);
            _arcAngleExtentNumeric = AddNumericRow(t, "角度範圍 (rad)", ref r, -7M, 7M, 4, 6.2832M, 0.1M);
            _arcAnnulusNumeric = AddNumericRow(t, "環寬一半 (px)", ref r, 0M, 100000M, 2, 5M, 1M);

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
            if (_imageHelper.IsEditingRect2)
            {
                _imageHelper.BeginRect2Edit(_selectedTool.Roi.CenterRow, _selectedTool.Roi.CenterCol,
                    _selectedTool.Roi.AngleRad, _selectedTool.Roi.Length1, _selectedTool.Roi.Length2,
                    OnToolRect2Changed);
            }
            MarkDirty();
        }

        // 數值框 → ArcRoi。比照 WriteRoi：若弧形把手正在編輯中，同步重下把手座標（即時預覽）。
        private void WriteArc()
        {
            if (_updatingControls || _selectedTool == null || _selectedTool.ArcRoi == null) return;
            ArcMeasureRoi a = _selectedTool.ArcRoi;
            a.CenterRow = (double)_arcCenterRowNumeric.Value;
            a.CenterCol = (double)_arcCenterColNumeric.Value;
            a.Radius = (double)_arcRadiusNumeric.Value;
            a.AngleStart = (double)_arcAngleStartNumeric.Value;
            a.AngleExtent = (double)_arcAngleExtentNumeric.Value;
            a.AnnulusRadius = (double)_arcAnnulusNumeric.Value;
            if (_imageHelper.IsEditingArc)
            {
                _imageHelper.BeginArcEdit(a.CenterRow, a.CenterCol, a.Radius,
                    a.AngleStart, a.AngleExtent, a.AnnulusRadius, OnToolArcChanged);
            }
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
                && (_selectedTool.ToolType == "circle" || _selectedTool.ToolType == "line");
        }

        // A1：在編輯器內就地試測選中的 circle/line 工具，把擬合結果畫在共用主視窗影像上。
        // 只跑這一個工具（MainWindow 委派內以暫態單工具配方呼叫 RecipeRunner.Run），
        // 不重跑整份配方、不做配方驗證。單一 overlay slot：重畫 ROI 框 + 擬合結果。
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
            _imageHelper.SetPersistentOverlayAction(() =>
            {
                OverlayAnnotator an = _imageHelper.Annotator;
                if (an == null) return;
                if (roi != null)
                    an.DrawRectangle2(roi.CenterRow, roi.CenterCol, roi.AngleRad,
                                      roi.Length1, roi.Length2, "orange");
                if (result == null || !result.Measured)
                {
                    an.DrawText("未偵測到邊緣 / No edge detected",
                        roi != null ? (int)roi.CenterRow : 20,
                        roi != null ? (int)roi.CenterCol : 20, "yellow");
                    return;
                }
                if (result.ToolType == "circle")
                    an.DrawCircle(result.FitCenterRow, result.FitCenterCol, result.FitRadiusPx, "green");
                else if (result.ToolType == "line")
                    an.DrawLine(result.LineRow1, result.LineCol1, result.LineRow2, result.LineCol2, "green");
                an.DrawText(result.ValueText ?? string.Empty,
                    roi != null ? (int)roi.CenterRow : 20,
                    roi != null ? (int)roi.CenterCol + 18 : 20, "green");
            });
            _editorInstalledOverlay = true;  // #3：記錄已佔用共用 overlay slot，關閉編輯器時清除
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
            return new Recipe
            {
                SchemaVersion = src.SchemaVersion,
                RecipeId = src.RecipeId,
                Name = src.Name,
                CalibrationProfileId = src.CalibrationProfileId,
                RefRow = src.RefRow,
                RefCol = src.RefCol,
                RefAngleRad = src.RefAngleRad,
                HasReferencePose = src.HasReferencePose,
                MetrologyModel = src.MetrologyModel,
                CreatedAt = src.CreatedAt,
                ModifiedAt = src.ModifiedAt
            };
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
                _imageHelper.EndRect2Edit();
                _imageHelper.EndArcEdit();
                _imageHelper.ClearSelectionHighlight();
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
                _imageHelper.EndRect2Edit();
                _imageHelper.EndArcEdit();
                _editorOwnsEdit = false;
                _imageHelper.ClearSelectionHighlight();
                return;
            }

            // 弧形工具：進入弧形互動編輯（BeginArcEdit 內部會自行關閉 rect2 編輯，故不重複呼叫
            // EndRect2Edit 以免多一次 Redraw）。無 ArcRoi 時只收把手，不進編輯。
            if (_selectedTool.ToolType == "arc")
            {
                _imageHelper.ClearSelectionHighlight();
                ArcMeasureRoi a = _selectedTool.ArcRoi;
                if (a == null)
                {
                    _imageHelper.EndRect2Edit();
                    _imageHelper.EndArcEdit();
                    _editorOwnsEdit = false;
                    return;
                }
                _imageHelper.BeginArcEdit(a.CenterRow, a.CenterCol, a.Radius,
                    a.AngleStart, a.AngleExtent, a.AnnulusRadius, OnToolArcChanged);
                _editorOwnsEdit = true;
                return;
            }

            bool isElement = _selectedTool.ToolType == "circle" || _selectedTool.ToolType == "line";
            if (!isElement)
            {
                // 參照型工具（GD&T/距離/角度/構造）：高亮其參照的元素 ROI（青色，疊在量測
                // 結果之上），讓使用者一眼看出此工具作用在哪些特徵上。
                _imageHelper.EndRect2Edit();
                _imageHelper.EndArcEdit();
                _editorOwnsEdit = false;
                var refs = GetReferencedElements(_selectedTool);
                if (refs.Count > 0)
                    _imageHelper.SetSelectionHighlight(() => DrawReferencedElements(_imageHelper.Annotator, refs));
                else
                    _imageHelper.ClearSelectionHighlight();
                return;
            }

            // 元素：以編輯把手指示選取，不需 ref 高亮。
            _imageHelper.ClearSelectionHighlight();
            var roi = _selectedTool.Roi;
            _imageHelper.BeginRect2Edit(roi.CenterRow, roi.CenterCol, roi.AngleRad,
                roi.Length1, roi.Length2, OnToolRect2Changed);
            _editorOwnsEdit = true;
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
        private void OnToolArcChanged(double cr, double cc, double radius,
            double angleStart, double angleExtent, double annulus)
        {
            if (_selectedTool == null || _selectedTool.ArcRoi == null) return;
            ArcMeasureRoi a = _selectedTool.ArcRoi;
            a.CenterRow = cr;
            a.CenterCol = cc;
            a.Radius = radius;
            a.AngleStart = angleStart;
            a.AngleExtent = angleExtent;
            a.AnnulusRadius = annulus;

            LoadArcFieldsFromSelectedTool();
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
                _arcAngleStartNumeric.Value = ClampDecimal(a.AngleStart, _arcAngleStartNumeric.Minimum, _arcAngleStartNumeric.Maximum);
                _arcAngleExtentNumeric.Value = ClampDecimal(a.AngleExtent, _arcAngleExtentNumeric.Minimum, _arcAngleExtentNumeric.Maximum);
                _arcAnnulusNumeric.Value = ClampDecimal(a.AnnulusRadius, _arcAnnulusNumeric.Minimum, _arcAnnulusNumeric.Maximum);
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
                bool isConstruction = tool.ToolType == "intersection" || tool.ToolType == "midline" || tool.ToolType == "projection";
                bool isComposite = tool.ToolType == "distance" || tool.ToolType == "angle";
                bool isGdt = IsGdtType(tool.ToolType);
                bool usesRefs = isComposite || isConstruction || isGdt;

                // Element tools (circle/line): ROI + Edge. Construction/composite/GD&T: RefTools.
                // GD&T 走單邊 Gdt 群組並隱藏雙邊 Tolerance；其餘維持雙邊。
                // 弧形工具走 ArcRoi：顯示弧形群組、隱藏 rect2 ROI 群組（其 Roi 未被使用，
                // RecipeValidator 的 RoiElementTypes 也不含 "arc"）；但邊緣參數仍有用——
                // RecipeRunner Pass 1.2 會把 tool.EdgeParameters 傳給 DetectEdgesOnArc。
                _roiGroup.Visible = isElement;
                _arcGroup.Visible = isArc;
                _edgeGroup.Visible = isElement || isArc;
                _refGroup.Visible = usesRefs;
                _toleranceGroup.Visible = !isGdt;
                _gdtGroup.Visible = isGdt;
                _angleHintLabel.Visible = tool.ToolType == "line";

                if (isGdt && tool.Gdt != null)
                {
                    _gdtCharLabel.Text = GdtCharLabelText(tool.ToolType);
                    _gdtZoneNumeric.Value = ClampDecimal(tool.Gdt.ToleranceZoneMm, _gdtZoneNumeric.Minimum, _gdtZoneNumeric.Maximum);
                }

                if (isElement)
                {
                    _centerRowNumeric.Value = ClampDecimal(tool.Roi.CenterRow, _centerRowNumeric.Minimum, _centerRowNumeric.Maximum);
                    _centerColNumeric.Value = ClampDecimal(tool.Roi.CenterCol, _centerColNumeric.Minimum, _centerColNumeric.Maximum);
                    _length1Numeric.Value = ClampDecimal(tool.Roi.Length1, _length1Numeric.Minimum, _length1Numeric.Maximum);
                    _length2Numeric.Value = ClampDecimal(tool.Roi.Length2, _length2Numeric.Minimum, _length2Numeric.Maximum);
                    _angleRadNumeric.Value = ClampDecimal(tool.Roi.AngleRad, _angleRadNumeric.Minimum, _angleRadNumeric.Maximum);
                }
                else if (isArc)
                {
                    LoadArcFieldsFromSelectedTool();
                }
                else if (usesRefs)
                {
                    PopulateRefCombos(tool);
                }

                // 邊緣參數：circle/line（rect2 卡尺）與 arc（弧形卡尺）都會用到。
                if (isElement || isArc)
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
                _imageHelper.RequestRoi((startRow, startCol, endRow, endCol) =>
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
            _imageHelper.ClearSelectionHighlight();
            _imageHelper.BeginArcEdit(a.CenterRow, a.CenterCol, a.Radius,
                a.AngleStart, a.AngleExtent, a.AnnulusRadius, OnToolArcChanged);
            _editorOwnsEdit = true;
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
            _toolTip.SetToolTip(_arcAngleStartNumeric, "起始角（弧度）");
            _toolTip.SetToolTip(_arcAngleExtentNumeric, "角度範圍（弧度，2π≈6.2832 為整圈；負值為順時針）");
            _toolTip.SetToolTip(_arcAnnulusNumeric, "環寬一半（像素）");
            _toolTip.SetToolTip(_captureArcButton, "在主影像上拖曳把手調整弧形 ROI");
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
