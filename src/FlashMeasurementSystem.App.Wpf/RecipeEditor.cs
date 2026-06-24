using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using FlashMeasurementSystem.Domain.EdgeDetection;
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
        // Working recipe metadata (pose/calibration/schema), isolated from caller's object.
        private Recipe _recipe = Recipe.Default();
        private MeasurementTool _selectedTool;
        private int _toolIdCounter;
        private bool _updatingControls;
        private bool _dirty;
        private string _savePath;

        // ── Toolbar controls ──
        private Label _filePathLabel;

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
        private TextBox _unitTextBox;
        private Label _angleHintLabel;

        // ── Constructors ──────────────────────────────────────────────

        public RecipeEditor(HWindowControlHelper imageHelper) : this(imageHelper, null, null, null) { }

        public RecipeEditor(HWindowControlHelper imageHelper, Recipe recipe, string filePath,
            Action<Recipe, string> savedCallback)
        {
            _imageHelper = imageHelper ?? throw new ArgumentNullException(nameof(imageHelper));
            _savePath = filePath;
            _savedCallback = savedCallback;

            MinimumSize = new Size(580, 440);

            BuildLayout();
            SetPropertyPanelEnabled(false);

            if (recipe != null)
                LoadFromRecipe(recipe);

            FormClosing += OnFormClosing;
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
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
                AutoSize = true
            };

            var newButton = new Button { Text = "New", Width = 50 };
            newButton.Click += OnNewRecipe;
            var loadButton = new Button { Text = "Load", Width = 50 };
            loadButton.Click += OnLoadRecipe;
            var addCircleButton = new Button { Text = "+ Circle", Width = 70 };
            addCircleButton.Click += (s, e) => AddTool("circle");
            var addLineButton = new Button { Text = "+ Line", Width = 65 };
            addLineButton.Click += (s, e) => AddTool("line");
            var addDistanceButton = new Button { Text = "+ Distance", Width = 80 };
            addDistanceButton.Click += (s, e) => AddTool("distance");
            var addAngleButton = new Button { Text = "+ Angle", Width = 70 };
            addAngleButton.Click += (s, e) => AddTool("angle");
            var deleteButton = new Button { Text = "Delete", Width = 60 };
            deleteButton.Click += OnDeleteTool;
            var saveButton = new Button { Text = "Save", Width = 50 };
            saveButton.Click += OnSave;
            var saveAsButton = new Button { Text = "Save As", Width = 65 };
            saveAsButton.Click += OnSaveAs;

            bar.Controls.Add(newButton);
            bar.Controls.Add(loadButton);
            bar.Controls.Add(addCircleButton);
            bar.Controls.Add(addLineButton);
            bar.Controls.Add(addDistanceButton);
            bar.Controls.Add(addAngleButton);
            bar.Controls.Add(deleteButton);
            bar.Controls.Add(saveButton);
            bar.Controls.Add(saveAsButton);

            return bar;
        }

        private void BuildPropertyGroups(Panel parent)
        {
            // Docked Top groups stack bottom-up in reverse insertion order; add in
            // visual top-to-bottom order then it shows reversed, so insert reversed:
            // we want Common, ROI, Edge, RefTool, Tolerance from top down.
            // With Dock=Top, the LAST added sits at top. Add in reverse.
            _toleranceGroup = CreateGroupBox("Tolerance", parent, 185);
            _refGroup = CreateGroupBox("Reference Tools", parent, 95);
            _edgeGroup = CreateGroupBox("Edge Detection", parent, 210);
            _roiGroup = CreateGroupBox("ROI Geometry", parent, 210);
            _commonGroup = CreateGroupBox("Common", parent, 130);

            FillCommonGroup(_commonGroup);
            FillRoiGroup(_roiGroup);
            FillEdgeGroup(_edgeGroup);
            FillRefGroup(_refGroup);
            FillToleranceGroup(_toleranceGroup);

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
        }

        private void WriteRoi()
        {
            if (_updatingControls || _selectedTool == null) return;
            _selectedTool.Roi.CenterRow = (double)_centerRowNumeric.Value;
            _selectedTool.Roi.CenterCol = (double)_centerColNumeric.Value;
            _selectedTool.Roi.Length1 = (double)_length1Numeric.Value;
            _selectedTool.Roi.Length2 = (double)_length2Numeric.Value;
            _selectedTool.Roi.AngleRad = (double)_angleRadNumeric.Value;
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

        private void OnSave(object sender, EventArgs e)
        {
            if (_tools.Count == 0)
            {
                MessageBox.Show(this, "No tools in recipe.", "Save",
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
            if (_tools.Count == 0)
            {
                MessageBox.Show(this, "No tools in recipe.", "Save As",
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
                RefToolIds = new List<string>(src.RefToolIds ?? new List<string>())
            };
        }

        // ─── Selection & population ────────────────────────────────────

        private void OnToolSelectionChanged(object sender, EventArgs e)
        {
            int idx = _toolListBox.SelectedIndex;
            if (idx < 0 || idx >= _tools.Count)
            {
                _selectedTool = null;
                SetPropertyPanelEnabled(false);
                return;
            }

            _selectedTool = _tools[idx];
            SetPropertyPanelEnabled(true);
            PopulateFromTool(_selectedTool);
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
                bool isComposite = tool.ToolType == "distance" || tool.ToolType == "angle";

                // Element tools (circle/line): ROI + Edge. Composite (distance/angle): RefTools.
                _roiGroup.Visible = isElement;
                _edgeGroup.Visible = isElement;
                _refGroup.Visible = isComposite;
                _angleHintLabel.Visible = tool.ToolType == "line";

                if (isElement)
                {
                    _centerRowNumeric.Value = ClampDecimal(tool.Roi.CenterRow, _centerRowNumeric.Minimum, _centerRowNumeric.Maximum);
                    _centerColNumeric.Value = ClampDecimal(tool.Roi.CenterCol, _centerColNumeric.Minimum, _centerColNumeric.Maximum);
                    _length1Numeric.Value = ClampDecimal(tool.Roi.Length1, _length1Numeric.Minimum, _length1Numeric.Maximum);
                    _length2Numeric.Value = ClampDecimal(tool.Roi.Length2, _length2Numeric.Minimum, _length2Numeric.Maximum);
                    _angleRadNumeric.Value = ClampDecimal(tool.Roi.AngleRad, _angleRadNumeric.Minimum, _angleRadNumeric.Maximum);

                    _sigmaNumeric.Value = ClampDecimal(tool.EdgeParameters.Sigma, _sigmaNumeric.Minimum, _sigmaNumeric.Maximum);
                    _thresholdNumeric.Value = ClampDecimal(tool.EdgeParameters.Threshold, _thresholdNumeric.Minimum, _thresholdNumeric.Maximum);
                    SelectCombo(_polarityCombo, tool.EdgeParameters.Polarity);
                    SelectCombo(_selectorCombo, tool.EdgeParameters.EdgeSelector);
                    SelectCombo(_interpolationCombo, tool.EdgeParameters.Interpolation);
                    SelectCombo(_measureModeCombo, tool.EdgeParameters.MeasureMode);
                }
                else if (isComposite)
                {
                    PopulateRefCombos(tool);
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
        }

        // distance supports line↔line, circle↔circle, line↔circle; angle only line↔line.
        private void PopulateRefCombos(MeasurementTool tool)
        {
            _ref1Combo.Items.Clear();
            _ref2Combo.Items.Clear();

            bool allowLine = true;
            bool allowCircle = tool.ToolType == "distance";

            foreach (var t in _tools)
            {
                if (t.ToolType == "line" && !allowLine) continue;
                if (t.ToolType == "circle" && !allowCircle) continue;
                if (t.ToolType != "line" && t.ToolType != "circle") continue;
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
            _commonGroup.Enabled = enabled;
            _roiGroup.Enabled = enabled;
            _edgeGroup.Enabled = enabled;
            _refGroup.Enabled = enabled;
            _toleranceGroup.Enabled = enabled;

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
            Text = "Recipe Editor - " + fileName + dirtyMark;
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
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tools.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
