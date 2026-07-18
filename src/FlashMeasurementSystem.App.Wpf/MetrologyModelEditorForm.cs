using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using FlashMeasurementSystem.Domain.MetrologyModel;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.Tolerance;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// 2D 量測模型編輯器（獨立 modal Form，純程式建構，無 Designer）。
    /// 操作員輸入各物件的標稱幾何 + 量測參數；量測矩形的自動佈放由 HALCON 在 apply 時負責，
    /// 此處不計算任何佈點。Save 時寫入 recipe.MetrologyModel 並回呼；Cancel 不動原配方
    /// （編輯的是 clone，未存檔前原 recipe.MetrologyModel 不變）。
    /// </summary>
    public sealed class MetrologyModelEditorForm : Form
    {
        private readonly Recipe _recipe;
        private readonly Action<Recipe> _savedCallback;
        private readonly int _imgW;
        private readonly int _imgH;

        private readonly List<MetrologyObjectDef> _objects = new List<MetrologyObjectDef>();
        private MetrologyObjectDef _selected;
        private bool _updating;

        private ListBox _list;
        private Button _addButton, _removeButton, _saveButton, _cancelButton;
        private TextBox _nameBox;
        private ComboBox _shapeCombo;
        private Label _warnLabel;

        private NumericUpDown _rowBegin, _colBegin, _rowEnd, _colEnd;   // line
        private NumericUpDown _row, _col, _radius;                       // circle / centre
        private NumericUpDown _phi, _radius1, _radius2, _length1, _length2; // ellipse / rectangle
        private NumericUpDown _ml1, _ml2, _sigma, _threshold, _measureDist, _numMeasures; // measure params

        // 公差（px）— 固定兩個 slot，依 Shape 對應 MetrologyJudger.JudgedQuantityKeys 綁定。
        private Label _slotALabel, _slotBLabel;
        private CheckBox _slotAEnable, _slotBEnable;
        private NumericUpDown _slotANominal, _slotALower, _slotAUpper;
        private NumericUpDown _slotBNominal, _slotBLower, _slotBUpper;
        private FlowLayoutPanel _slotAFlow, _slotBFlow;
        private RowStyle _slotARowStyle, _slotBRowStyle;
        private string _slotAKey, _slotBKey;

        public MetrologyModelEditorForm(Recipe recipe, int imageWidth, int imageHeight, Action<Recipe> savedCallback)
        {
            _recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            _savedCallback = savedCallback;
            _imgW = imageWidth;
            _imgH = imageHeight;

            if (_recipe.MetrologyModel != null && _recipe.MetrologyModel.Objects != null)
            {
                foreach (MetrologyObjectDef o in _recipe.MetrologyModel.Objects)
                    _objects.Add(Clone(o));
            }

            Text = "Metrology Model Editor";
            MinimumSize = new Size(660, 520);
            Size = new Size(660, 520);
            StartPosition = FormStartPosition.CenterParent;

            BuildLayout();
            RefreshList();
            if (_objects.Count > 0) _list.SelectedIndex = 0;
            else SetPropertyEnabled(false);
        }

        private void BuildLayout()
        {
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(6) };
            _cancelButton = new Button { Text = "Cancel", Width = 80 };
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton = new Button { Text = "Save", Width = 80 };
            _saveButton.Click += OnSave;
            bottom.Controls.Add(_cancelButton);
            bottom.Controls.Add(_saveButton);

            var left = new Panel { Dock = DockStyle.Left, Width = 200, Padding = new Padding(6) };
            _list = new ListBox { Dock = DockStyle.Fill };
            _list.SelectedIndexChanged += OnSelectionChanged;
            var listButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36 };
            _addButton = new Button { Text = "+ Add", Width = 80 };
            _addButton.Click += OnAdd;
            _removeButton = new Button { Text = "Remove", Width = 80 };
            _removeButton.Click += OnRemove;
            listButtons.Controls.Add(_addButton);
            listButtons.Controls.Add(_removeButton);
            left.Controls.Add(_list);
            left.Controls.Add(listButtons);

            var right = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };
            var t = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            int r = 0;

            _nameBox = (TextBox)AddRow(t, "Name", ref r, new TextBox { Dock = DockStyle.Fill });
            // 只更新名稱欄位值；清單標籤改在離開欄位時刷新。直接在 TextChanged 重設 ListBox.Items[idx]
            // 會把焦點搶回清單，導致每打一字就跳開。
            _nameBox.TextChanged += (s, e) => { if (!_updating && _selected != null) _selected.Name = _nameBox.Text; };
            _nameBox.Leave += (s, e) => { if (!_updating) RefreshSelectedItemText(); };

            _shapeCombo = (ComboBox)AddRow(t, "Shape", ref r, new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList });
            _shapeCombo.Items.AddRange(new object[] { "Line", "Circle", "Ellipse", "Rectangle" });
            _shapeCombo.SelectedIndexChanged += OnShapeChanged;

            _rowBegin = AddNumeric(t, "RowBegin (line)", ref r, 0, 100000, 2, 1);
            _colBegin = AddNumeric(t, "ColumnBegin (line)", ref r, 0, 100000, 2, 1);
            _rowEnd = AddNumeric(t, "RowEnd (line)", ref r, 0, 100000, 2, 1);
            _colEnd = AddNumeric(t, "ColumnEnd (line)", ref r, 0, 100000, 2, 1);
            _row = AddNumeric(t, "Row (centre)", ref r, 0, 100000, 2, 1);
            _col = AddNumeric(t, "Column (centre)", ref r, 0, 100000, 2, 1);
            _radius = AddNumeric(t, "Radius (circle)", ref r, 0, 100000, 2, 1);
            _phi = AddNumeric(t, "Phi rad (ellipse/rect)", ref r, -4, 4, 4, 0.01M);
            _radius1 = AddNumeric(t, "Radius1 (ellipse)", ref r, 0, 100000, 2, 1);
            _radius2 = AddNumeric(t, "Radius2 (ellipse)", ref r, 0, 100000, 2, 1);
            _length1 = AddNumeric(t, "Length1 (rect)", ref r, 0, 100000, 2, 1);
            _length2 = AddNumeric(t, "Length2 (rect)", ref r, 0, 100000, 2, 1);

            _ml1 = AddNumeric(t, "MeasureLength1", ref r, 1, 100000, 2, 1, 20);
            _ml2 = AddNumeric(t, "MeasureLength2", ref r, 1, 100000, 2, 1, 5);
            _sigma = AddNumeric(t, "MeasureSigma", ref r, 0.4M, 100, 2, 0.1M, 1);
            _threshold = AddNumeric(t, "MeasureThreshold", ref r, 1, 255, 2, 1, 30);
            _measureDist = AddNumeric(t, "MeasureDistance (0=use NumMeasures)", ref r, 0, 100000, 2, 1, 10);
            _numMeasures = AddNumeric(t, "NumMeasures (0=use Distance)", ref r, 0, 100000, 0, 1, 0);

            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            t.Controls.Add(new Label { Text = "公差 (px)", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, r);
            t.RowCount = ++r;
            BuildToleranceSlot(t, ref r, out _slotALabel, out _slotAEnable, out _slotANominal, out _slotALower, out _slotAUpper, out _slotAFlow, out _slotARowStyle);
            BuildToleranceSlot(t, ref r, out _slotBLabel, out _slotBEnable, out _slotBNominal, out _slotBLower, out _slotBUpper, out _slotBFlow, out _slotBRowStyle);

            _warnLabel = (Label)AddRow(t, "", ref r, new Label { Dock = DockStyle.Fill, ForeColor = Color.DarkRed, AutoSize = true });

            right.Controls.Add(t);

            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(bottom);
        }

        private void OnAdd(object sender, EventArgs e)
        {
            var def = new MetrologyObjectDef { Name = "obj" + (_objects.Count + 1), Shape = MetrologyObjectType.Circle };
            _objects.Add(def);
            RefreshList();
            _list.SelectedIndex = _objects.Count - 1;
        }

        private void OnRemove(object sender, EventArgs e)
        {
            int idx = _list.SelectedIndex;
            if (idx < 0 || idx >= _objects.Count) return;
            _objects.RemoveAt(idx);
            RefreshList();
            if (_objects.Count > 0) _list.SelectedIndex = Math.Min(idx, _objects.Count - 1);
            else { _selected = null; SetPropertyEnabled(false); }
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            int idx = _list.SelectedIndex;
            if (idx < 0 || idx >= _objects.Count) { _selected = null; SetPropertyEnabled(false); return; }
            _selected = _objects[idx];
            SetPropertyEnabled(true);
            Populate(_selected);
        }

        private void OnShapeChanged(object sender, EventArgs e)
        {
            if (_updating || _selected == null) return;
            _selected.Shape = ParseShape(_shapeCombo.SelectedItem as string);
            SetGeometryEnabledForShape(_selected.Shape);
            _updating = true;
            try
            {
                RebindToleranceSlots(_selected.Shape);
                LoadToleranceSlot(_slotAKey, _slotAEnable, _slotANominal, _slotALower, _slotAUpper);
                LoadToleranceSlot(_slotBKey, _slotBEnable, _slotBNominal, _slotBLower, _slotBUpper);
            }
            finally { _updating = false; }
            RefreshSelectedItemText();
            UpdateWarning();
        }

        private void Populate(MetrologyObjectDef d)
        {
            _updating = true;
            try
            {
                _nameBox.Text = d.Name ?? "";
                _shapeCombo.SelectedItem = d.Shape.ToString();
                _rowBegin.Value = Clamp(d.RowBegin, _rowBegin); _colBegin.Value = Clamp(d.ColumnBegin, _colBegin);
                _rowEnd.Value = Clamp(d.RowEnd, _rowEnd); _colEnd.Value = Clamp(d.ColumnEnd, _colEnd);
                _row.Value = Clamp(d.Row, _row); _col.Value = Clamp(d.Column, _col); _radius.Value = Clamp(d.Radius, _radius);
                _phi.Value = Clamp(d.Phi, _phi); _radius1.Value = Clamp(d.Radius1, _radius1); _radius2.Value = Clamp(d.Radius2, _radius2);
                _length1.Value = Clamp(d.Length1, _length1); _length2.Value = Clamp(d.Length2, _length2);
                _ml1.Value = Clamp(d.MeasureLength1, _ml1); _ml2.Value = Clamp(d.MeasureLength2, _ml2);
                _sigma.Value = Clamp(d.MeasureSigma, _sigma); _threshold.Value = Clamp(d.MeasureThreshold, _threshold);
                _measureDist.Value = Clamp(d.MeasureDistance, _measureDist); _numMeasures.Value = Clamp(d.NumMeasures, _numMeasures);
                SetGeometryEnabledForShape(d.Shape);
                RebindToleranceSlots(d.Shape);
                LoadToleranceSlot(_slotAKey, _slotAEnable, _slotANominal, _slotALower, _slotAUpper);
                LoadToleranceSlot(_slotBKey, _slotBEnable, _slotBNominal, _slotBLower, _slotBUpper);
            }
            finally { _updating = false; }
            UpdateWarning();
        }

        // 任一數值變更 → 寫回選中物件（量測矩形佈放仍由 HALCON 負責，這裡只存參數）。
        private void WriteBack()
        {
            if (_updating || _selected == null) return;
            _selected.RowBegin = (double)_rowBegin.Value; _selected.ColumnBegin = (double)_colBegin.Value;
            _selected.RowEnd = (double)_rowEnd.Value; _selected.ColumnEnd = (double)_colEnd.Value;
            _selected.Row = (double)_row.Value; _selected.Column = (double)_col.Value; _selected.Radius = (double)_radius.Value;
            _selected.Phi = (double)_phi.Value; _selected.Radius1 = (double)_radius1.Value; _selected.Radius2 = (double)_radius2.Value;
            _selected.Length1 = (double)_length1.Value; _selected.Length2 = (double)_length2.Value;
            _selected.MeasureLength1 = (double)_ml1.Value; _selected.MeasureLength2 = (double)_ml2.Value;
            _selected.MeasureSigma = (double)_sigma.Value; _selected.MeasureThreshold = (double)_threshold.Value;
            _selected.MeasureDistance = (double)_measureDist.Value; _selected.NumMeasures = (int)_numMeasures.Value;
            UpdateWarning();
        }

        // 依 Shape 決定 slot A/B 對應哪個判定量 key（順序即 MetrologyJudger.JudgedQuantityKeys 順序）。
        // Slot A 對 4 種 Shape 皆存在；Slot B 只在 ellipse/rectangle 存在，否則隱藏。
        private void RebindToleranceSlots(MetrologyObjectType shape)
        {
            List<KeyValuePair<string, string>> keys = MetrologyJudger.JudgedQuantityKeys(shape);

            _slotAKey = keys.Count > 0 ? keys[0].Key : null;
            _slotALabel.Text = keys.Count > 0 ? keys[0].Value : "";
            SetSlotRowVisible(_slotALabel, _slotAFlow, _slotARowStyle, keys.Count > 0);

            _slotBKey = keys.Count > 1 ? keys[1].Key : null;
            _slotBLabel.Text = keys.Count > 1 ? keys[1].Value : "";
            SetSlotRowVisible(_slotBLabel, _slotBFlow, _slotBRowStyle, keys.Count > 1);
        }

        private static void SetSlotRowVisible(Label labelCtl, Control flow, RowStyle rowStyle, bool visible)
        {
            labelCtl.Visible = visible;
            flow.Visible = visible;
            rowStyle.Height = visible ? 28F : 0F;
        }

        // 依 key 從 _selected.Tolerances 找出既有公差載入 slot；找不到則取消啟用、欄位歸零。呼叫端須在 _updating guard 內。
        private void LoadToleranceSlot(string key, CheckBox enableCtl, NumericUpDown nominal, NumericUpDown lower, NumericUpDown upper)
        {
            if (key == null) return;
            MetrologyItemTolerance found = null;
            if (_selected.Tolerances != null)
            {
                foreach (MetrologyItemTolerance it in _selected.Tolerances)
                    if (it != null && it.Quantity == key) { found = it; break; }
            }
            if (found != null && found.Spec != null)
            {
                enableCtl.Checked = true;
                nominal.Value = Clamp(found.Spec.Nominal, nominal);
                lower.Value = Clamp(found.Spec.LowerTolerance, lower);
                upper.Value = Clamp(found.Spec.UpperTolerance, upper);
            }
            else
            {
                enableCtl.Checked = false;
                nominal.Value = 0; lower.Value = 0; upper.Value = 0;
            }
        }

        // 任一公差欄位變更 → 依可見且啟用的 slot 重建 _selected.Tolerances（未啟用/隱藏的 slot 不寫入 → 該量不判定）。
        private void WriteTolerances()
        {
            if (_updating || _selected == null) return;
            var list = new List<MetrologyItemTolerance>();
            if (_slotAKey != null && _slotAEnable.Checked)
                list.Add(new MetrologyItemTolerance
                {
                    Quantity = _slotAKey,
                    Spec = new ToleranceSpec { Nominal = (double)_slotANominal.Value, LowerTolerance = (double)_slotALower.Value, UpperTolerance = (double)_slotAUpper.Value, Unit = "px" }
                });
            if (_slotBKey != null && _slotBEnable.Checked)
                list.Add(new MetrologyItemTolerance
                {
                    Quantity = _slotBKey,
                    Spec = new ToleranceSpec { Nominal = (double)_slotBNominal.Value, LowerTolerance = (double)_slotBLower.Value, UpperTolerance = (double)_slotBUpper.Value, Unit = "px" }
                });
            _selected.Tolerances = list;
            UpdateWarning();
        }

        private void BuildToleranceSlot(TableLayoutPanel t, ref int row, out Label labelCtl, out CheckBox enableCtl,
            out NumericUpDown nominal, out NumericUpDown lower, out NumericUpDown upper, out FlowLayoutPanel flow, out RowStyle rowStyle)
        {
            rowStyle = new RowStyle(SizeType.Absolute, 28F);
            t.RowStyles.Add(rowStyle);

            labelCtl = new Label { Text = "", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            t.Controls.Add(labelCtl, 0, row);

            flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = false };

            enableCtl = new CheckBox { Text = "啟用", AutoSize = true, Margin = new Padding(0, 5, 8, 0) };
            enableCtl.CheckedChanged += (s, e) => WriteTolerances();
            flow.Controls.Add(enableCtl);

            flow.Controls.Add(new Label { Text = "Nominal", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(4, 7, 2, 0) });
            nominal = new NumericUpDown { Width = 70, Minimum = 0, Maximum = 100000, DecimalPlaces = 2, Increment = 1, Margin = new Padding(0, 3, 0, 0) };
            nominal.ValueChanged += (s, e) => WriteTolerances();
            flow.Controls.Add(nominal);

            flow.Controls.Add(new Label { Text = "下限", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(4, 7, 2, 0) });
            lower = new NumericUpDown { Width = 70, Minimum = -100000, Maximum = 100000, DecimalPlaces = 2, Increment = 0.1M, Margin = new Padding(0, 3, 0, 0) };
            lower.ValueChanged += (s, e) => WriteTolerances();
            flow.Controls.Add(lower);

            flow.Controls.Add(new Label { Text = "上限", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(4, 7, 2, 0) });
            upper = new NumericUpDown { Width = 70, Minimum = -100000, Maximum = 100000, DecimalPlaces = 2, Increment = 0.1M, Margin = new Padding(0, 3, 0, 0) };
            upper.ValueChanged += (s, e) => WriteTolerances();
            flow.Controls.Add(upper);

            t.Controls.Add(flow, 1, row);
            t.RowCount = ++row;
        }

        private void UpdateWarning()
        {
            if (_selected == null) { _warnLabel.Text = ""; return; }
            string msg = null;
            switch (_selected.Shape)
            {
                case MetrologyObjectType.Circle:
                    if (_selected.MeasureLength1 >= _selected.Radius) msg = "MeasureLength1 必須 < Radius，否則此物件量測會失敗。";
                    break;
                case MetrologyObjectType.Ellipse:
                    if (_selected.MeasureLength1 >= _selected.Radius1 || _selected.MeasureLength1 >= _selected.Radius2)
                        msg = "MeasureLength1 必須 < Radius1 且 < Radius2。";
                    break;
                case MetrologyObjectType.Rectangle:
                    if (_selected.MeasureLength1 >= _selected.Length1 || _selected.MeasureLength1 >= _selected.Length2)
                        msg = "MeasureLength1 必須 < Length1 且 < Length2。";
                    break;
            }
            _warnLabel.Text = msg ?? "";
        }

        private void OnSave(object sender, EventArgs e)
        {
            _recipe.MetrologyModel = new MetrologyModelDef
            {
                Objects = _objects,
                ImageWidth = _imgW,
                ImageHeight = _imgH
            };
            _savedCallback?.Invoke(_recipe);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void SetGeometryEnabledForShape(MetrologyObjectType shape)
        {
            bool line = shape == MetrologyObjectType.Line;
            bool circle = shape == MetrologyObjectType.Circle;
            bool ellipse = shape == MetrologyObjectType.Ellipse;
            bool rect = shape == MetrologyObjectType.Rectangle;
            _rowBegin.Enabled = _colBegin.Enabled = _rowEnd.Enabled = _colEnd.Enabled = line;
            _row.Enabled = _col.Enabled = circle || ellipse || rect;
            _radius.Enabled = circle;
            _phi.Enabled = ellipse || rect;
            _radius1.Enabled = _radius2.Enabled = ellipse;
            _length1.Enabled = _length2.Enabled = rect;
        }

        private void SetPropertyEnabled(bool on)
        {
            _nameBox.Enabled = _shapeCombo.Enabled = on;
            _ml1.Enabled = _ml2.Enabled = _sigma.Enabled = _threshold.Enabled = _measureDist.Enabled = _numMeasures.Enabled = on;
            _slotAEnable.Enabled = _slotBEnable.Enabled = on;
            _slotANominal.Enabled = _slotALower.Enabled = _slotAUpper.Enabled = on;
            _slotBNominal.Enabled = _slotBLower.Enabled = _slotBUpper.Enabled = on;
            if (!on)
            {
                _rowBegin.Enabled = _colBegin.Enabled = _rowEnd.Enabled = _colEnd.Enabled = false;
                _row.Enabled = _col.Enabled = _radius.Enabled = _phi.Enabled = false;
                _radius1.Enabled = _radius2.Enabled = _length1.Enabled = _length2.Enabled = false;
                _warnLabel.Text = "";
            }
        }

        private void RefreshList()
        {
            int prev = _list.SelectedIndex;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (MetrologyObjectDef o in _objects)
                _list.Items.Add(ItemText(o));
            _list.EndUpdate();
            if (prev >= 0 && prev < _objects.Count) _list.SelectedIndex = prev;
        }

        private void RefreshSelectedItemText()
        {
            int idx = _list.SelectedIndex;
            if (idx >= 0 && idx < _objects.Count) _list.Items[idx] = ItemText(_objects[idx]);
        }

        private static string ItemText(MetrologyObjectDef o)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}  [{1}]", o.Name, o.Shape);
        }

        private static MetrologyObjectType ParseShape(string s)
        {
            switch (s)
            {
                case "Circle": return MetrologyObjectType.Circle;
                case "Ellipse": return MetrologyObjectType.Ellipse;
                case "Rectangle": return MetrologyObjectType.Rectangle;
                default: return MetrologyObjectType.Line;
            }
        }

        private NumericUpDown AddNumeric(TableLayoutPanel t, string label, ref int row,
            decimal min, decimal max, int decimals, decimal increment, decimal value = 0)
        {
            var nud = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Increment = increment,
                Value = value < min ? min : (value > max ? max : value)
            };
            nud.ValueChanged += (s, e) => WriteBack();
            return (NumericUpDown)AddRow(t, label, ref row, nud);
        }

        private static Control AddRow(TableLayoutPanel t, string label, ref int row, Control control)
        {
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            if (!string.IsNullOrEmpty(label))
                t.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            t.Controls.Add(control, 1, row);
            t.RowCount = ++row;
            return control;
        }

        private static decimal Clamp(double value, NumericUpDown nud)
        {
            decimal d = (decimal)value;
            if (d < nud.Minimum) return nud.Minimum;
            if (d > nud.Maximum) return nud.Maximum;
            return d;
        }

        private static MetrologyObjectDef Clone(MetrologyObjectDef s)
        {
            return new MetrologyObjectDef
            {
                Id = s.Id, Name = s.Name, Shape = s.Shape,
                RowBegin = s.RowBegin, ColumnBegin = s.ColumnBegin, RowEnd = s.RowEnd, ColumnEnd = s.ColumnEnd,
                Row = s.Row, Column = s.Column, Radius = s.Radius,
                Phi = s.Phi, Radius1 = s.Radius1, Radius2 = s.Radius2, Length1 = s.Length1, Length2 = s.Length2,
                MeasureLength1 = s.MeasureLength1, MeasureLength2 = s.MeasureLength2,
                MeasureSigma = s.MeasureSigma, MeasureThreshold = s.MeasureThreshold,
                MeasureDistance = s.MeasureDistance, NumMeasures = s.NumMeasures,
                Tolerance = s.Tolerance,
                Tolerances = CloneTolerances(s.Tolerances)
            };
        }

        private static List<MetrologyItemTolerance> CloneTolerances(List<MetrologyItemTolerance> src)
        {
            var list = new List<MetrologyItemTolerance>();
            if (src == null) return list;
            foreach (MetrologyItemTolerance it in src)
            {
                if (it == null) continue;
                ToleranceSpec spec = it.Spec == null ? null : new ToleranceSpec
                {
                    Nominal = it.Spec.Nominal, LowerTolerance = it.Spec.LowerTolerance,
                    UpperTolerance = it.Spec.UpperTolerance, Unit = it.Spec.Unit
                };
                list.Add(new MetrologyItemTolerance { Quantity = it.Quantity, Spec = spec });
            }
            return list;
        }
    }
}
