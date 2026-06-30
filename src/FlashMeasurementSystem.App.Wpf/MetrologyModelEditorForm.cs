using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using FlashMeasurementSystem.Domain.MetrologyModel;
using FlashMeasurementSystem.Domain.Roi;

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
            _nameBox.TextChanged += (s, e) => { if (!_updating && _selected != null) { _selected.Name = _nameBox.Text; RefreshSelectedItemText(); } };

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
                Tolerance = s.Tolerance
            };
        }
    }
}
