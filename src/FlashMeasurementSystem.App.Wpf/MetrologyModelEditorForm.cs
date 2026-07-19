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
    /// 2D 量測模型編輯器（獨立 modeless Form，純程式建構，無 Designer；Phase 4 起與主視窗
    /// 共存，讓稍後加入的 on-image 繪製能操作主視窗共用影像）。
    /// 操作員輸入各物件的標稱幾何 + 量測參數；量測矩形的自動佈放由 HALCON 在 apply 時負責，
    /// 此處不計算任何佈點。Save 時寫入 recipe.MetrologyModel 並回呼；Cancel 不動原配方
    /// （編輯的是 clone，未存檔前原 recipe.MetrologyModel 不變）。
    /// </summary>
    public sealed class MetrologyModelEditorForm : Form
    {
        private readonly Recipe _recipe;
        private readonly Action<Recipe> _savedCallback;
        private readonly Action<MetrologyModelDef> _trialCallback;
        private readonly int _imgW;
        private readonly int _imgH;
        // Phase 4：編輯器改 modeless 後接管主視窗共用影像視窗，用來在影像上拉矩形擷取標稱幾何。
        private readonly HWindowControlHelper _imageHelper;

        private readonly List<MetrologyObjectDef> _objects = new List<MetrologyObjectDef>();
        private MetrologyObjectDef _selected;
        private bool _updating;
        private bool _dirty;
        // Phase 4 groundwork：比照 RecipeEditor 的 _editorInstalledOverlay——目前編輯器尚未持有
        // _imageHelper（仍全經由 callback 與 MainWindow 溝通），故此旗標尚未被設為 true；
        // on-image 繪製 commit 注入 _imageHelper 後才會在裝 overlay 處設定它，並於 FormClosed 清除。
        private bool _editorInstalledOverlay;

        private ListBox _list;
        private Button _addButton, _removeButton, _saveButton, _cancelButton, _trialButton, _drawButton;
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

        public MetrologyModelEditorForm(Recipe recipe, int imageWidth, int imageHeight, Action<Recipe> savedCallback,
            HWindowControlHelper imageHelper, Action<MetrologyModelDef> trialCallback = null)
        {
            _recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            _savedCallback = savedCallback;
            _trialCallback = trialCallback;
            _imageHelper = imageHelper;
            _imgW = imageWidth;
            _imgH = imageHeight;

            if (_recipe.MetrologyModel != null && _recipe.MetrologyModel.Objects != null)
            {
                foreach (MetrologyObjectDef o in _recipe.MetrologyModel.Objects)
                    _objects.Add(Clone(o));
            }

            Text = "Metrology Model Editor";
            // 800 寬：右側屬性欄 col1（Percent 100%）需容納公差列的 啟用/Nominal/下限/上限 四組控制
            // （約 365px），660 寬時 col1 只剩 ~320px 會把「上限」切掉。
            MinimumSize = new Size(800, 520);
            Size = new Size(800, 520);
            StartPosition = FormStartPosition.CenterParent;

            BuildLayout();
            RefreshList();
            if (_objects.Count > 0) _list.SelectedIndex = 0;
            else SetPropertyEnabled(false);

            // Phase 4：改 modeless 後，關閉前需比照 RecipeEditor 檔未存變更確認，
            // 並在關閉時做編輯器自己持有資源的 teardown（目前尚無，見下方註解）。
            FormClosing += OnFormClosing;
            FormClosed += (s, e) =>
            {
                // 本 commit 只用一次性 RequestRoi（不裝 persistent overlay），但比照 RecipeEditor
                // 保險地結束任何 rect2/弧形編輯把手（兩者皆 idempotent），並僅在本編輯器曾裝過
                // overlay 時 ClearOverlay()——不動別的元件裝的 overlay。
                if (_imageHelper != null)
                {
                    _imageHelper.EndRect2Edit();
                    _imageHelper.EndArcEdit();
                    if (_editorInstalledOverlay) _imageHelper.ClearOverlay();
                }
                _editorInstalledOverlay = false;
            };
        }

        private void BuildLayout()
        {
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(6) };
            _cancelButton = new Button { Text = "Cancel", Width = 80 };
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton = new Button { Text = "Save", Width = 80 };
            _saveButton.Click += OnSave;
            // Phase 3：在此以「目前（未存檔）模型」跑一次量測，把擬合結果畫在主視窗共用影像上，
            // 免去 Save → 關閉 → Run Recipe → 重開編輯器的來回。無委派（trialCallback==null）時停用。
            _trialButton = new Button { Text = "試測", Width = 80, Enabled = _trialCallback != null };
            _trialButton.Click += OnTrial;
            // Phase 4：在主視窗共用影像上拉一個矩形，把角點轉成選中物件的標稱幾何（矩形/橢圓/圓）。
            // 僅在有影像且選中物件為 Rectangle/Circle/Ellipse 時啟用（Line 由後續 commit 處理）。
            _drawButton = new Button { Text = "在影像上繪製", Width = 110, Enabled = false };
            _drawButton.Click += OnDrawOnImage;
            bottom.Controls.Add(_cancelButton);
            bottom.Controls.Add(_saveButton);
            bottom.Controls.Add(_trialButton);
            bottom.Controls.Add(_drawButton);

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
            _nameBox.TextChanged += (s, e) => { if (!_updating && _selected != null) { _selected.Name = _nameBox.Text; _dirty = true; } };
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
            _dirty = true;
            RefreshList();
            _list.SelectedIndex = _objects.Count - 1;
        }

        private void OnRemove(object sender, EventArgs e)
        {
            int idx = _list.SelectedIndex;
            if (idx < 0 || idx >= _objects.Count) return;
            _objects.RemoveAt(idx);
            _dirty = true;
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
            _dirty = true;
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
            UpdateDrawButtonEnabled();
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
            UpdateDrawButtonEnabled();
        }

        // 任一數值變更 → 寫回選中物件（量測矩形佈放仍由 HALCON 負責，這裡只存參數）。
        private void WriteBack()
        {
            if (_updating || _selected == null) return;
            _dirty = true;
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
            _dirty = true;
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
            var msgs = new List<string>();

            switch (_selected.Shape)
            {
                case MetrologyObjectType.Circle:
                    if (_selected.MeasureLength1 >= _selected.Radius) msgs.Add("MeasureLength1 必須 < Radius，否則此物件量測會失敗。");
                    break;
                case MetrologyObjectType.Ellipse:
                    if (_selected.MeasureLength1 >= _selected.Radius1 || _selected.MeasureLength1 >= _selected.Radius2)
                        msgs.Add("MeasureLength1 必須 < Radius1 且 < Radius2。");
                    break;
                case MetrologyObjectType.Rectangle:
                    if (_selected.MeasureLength1 >= _selected.Length1 || _selected.MeasureLength1 >= _selected.Length2)
                        msgs.Add("MeasureLength1 必須 < Length1 且 < Length2。");
                    break;
            }

            // Phase 2a：量測區數是否足夠讓 HALCON 佈點成形（MinMeasureRegions：Line 2/Circle 3/Ellipse 5/Rectangle 8）。
            int min = MetrologyObjectDef.MinMeasureRegions(_selected.Shape);
            if (_selected.NumMeasures > 0 && _selected.NumMeasures < min)
            {
                msgs.Add(string.Format(CultureInfo.InvariantCulture,
                    "量測區數 {0} 少於此形狀最少 {1}", _selected.NumMeasures, min));
            }
            else if (_selected.MeasureDistance > 0)
            {
                double perimeter = EstimateNominalPerimeterPx(_selected);
                if (perimeter > 0)
                {
                    double regions = perimeter / _selected.MeasureDistance;
                    if (regions < min)
                    {
                        msgs.Add(string.Format(CultureInfo.InvariantCulture,
                            "以間距估計約 {0:0} 區，少於最少 {1}（縮小 MeasureDistance 或改用 NumMeasures）", regions, min));
                    }
                }
            }

            // Phase 2b：MeasureDistance 與 NumMeasures 同時設定時，執行期以 Distance 優先，NumMeasures 靜默被忽略。
            if (_selected.MeasureDistance > 0 && _selected.NumMeasures > 0)
            {
                msgs.Add("MeasureDistance 與 NumMeasures 同時設定：以 MeasureDistance 優先，NumMeasures 被忽略");
            }

            _warnLabel.Text = string.Join(" ; ", msgs);
        }

        // Phase 2a 輔助：以標稱幾何估計形狀周長（px），供「距離模式」估算量測區數。
        private static double EstimateNominalPerimeterPx(MetrologyObjectDef o)
        {
            switch (o.Shape)
            {
                case MetrologyObjectType.Circle:
                    return 2.0 * Math.PI * o.Radius;
                case MetrologyObjectType.Ellipse:
                    // Ramanujan 近似。
                    return Math.PI * (3.0 * (o.Radius1 + o.Radius2)
                        - Math.Sqrt((3.0 * o.Radius1 + o.Radius2) * (o.Radius1 + 3.0 * o.Radius2)));
                case MetrologyObjectType.Rectangle:
                    // Length1/Length2 為半邊 → 全周長 = 2*(2*Length1 + 2*Length2) = 4*(Length1+Length2)。
                    return 4.0 * (o.Length1 + o.Length2);
                case MetrologyObjectType.Line:
                    {
                        double dr = o.RowEnd - o.RowBegin, dc = o.ColumnEnd - o.ColumnBegin;
                        return Math.Sqrt(dr * dr + dc * dc);
                    }
                default:
                    return 0;
            }
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
            _dirty = false;
            DialogResult = DialogResult.OK;
            Close();
        }

        // ─── Dirty tracking / close safety（比照 RecipeEditor.ConfirmDiscardIfDirty/OnFormClosing）───

        private bool ConfirmDiscardIfDirty()
        {
            if (!_dirty) return true;
            DialogResult r = MessageBox.Show(this,
                "有未儲存的變更，是否於關閉前儲存？", "未儲存的變更",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes)
            {
                OnSave(this, EventArgs.Empty);
                return !_dirty; // OnSave 一定會清 _dirty；若日後改為可能失敗，這裡仍是正確的守門
            }
            return true; // No → 放棄變更
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscardIfDirty())
                e.Cancel = true;
        }

        // Phase 3：不寫回 _recipe、不關閉編輯器 —— 只把目前 _objects 建成暫態模型交給呼叫端跑一次量測。
        private void OnTrial(object sender, EventArgs e)
        {
            if (_trialCallback == null) return;
            var model = new MetrologyModelDef
            {
                Objects = _objects,
                ImageWidth = _imgW,
                ImageHeight = _imgH
            };
            _trialCallback(model);
        }

        // ─── Phase 4：在影像上繪製標稱幾何（重用 RequestRoi 的拉矩形手勢）───

        // 依選中物件與影像狀態決定「在影像上繪製」是否可用（Line 不支援此手勢，由後續 commit 處理）。
        private void UpdateDrawButtonEnabled()
        {
            if (_drawButton == null) return;
            _drawButton.Enabled = _selected != null
                && _imageHelper != null && _imageHelper.CurrentImage != null
                && (_selected.Shape == MetrologyObjectType.Rectangle
                    || _selected.Shape == MetrologyObjectType.Circle
                    || _selected.Shape == MetrologyObjectType.Ellipse
                    || _selected.Shape == MetrologyObjectType.Line);
        }

        private void OnDrawOnImage(object sender, EventArgs e)
        {
            if (_selected == null || _imageHelper == null || _imageHelper.CurrentImage == null) return;
            try
            {
                // RequestRoi/RequestLine 內部已會 DisarmInteractiveModes，這裡再呼叫一次確保無殘留 pending 手勢。
                _imageHelper.DisarmInteractiveModes();
                if (_selected.Shape == MetrologyObjectType.Line)
                    _imageHelper.RequestLine(OnDrawnLine);   // 直線：兩點拖曳手勢
                else
                    _imageHelper.RequestRoi(OnDrawnRoi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "在影像上繪製失敗：" + ex.Message, "繪製",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // RequestRoi 於 MouseUp 傳回原始角點（>5px 才觸發）。轉 rect2 的數學比照 RecipeEditor.OnCaptureRoi：
        // center=中點；半邊=abs(delta)/2；長軸自動偵測（較長邊為 Length1，phi 取 0 或 π/2）。
        private void OnDrawnRoi(double startRow, double startCol, double endRow, double endCol)
        {
            double centerRow = (startRow + endRow) / 2.0;
            double centerCol = (startCol + endCol) / 2.0;
            double rowExt = Math.Abs(endRow - startRow) / 2.0;
            double colExt = Math.Abs(endCol - startCol) / 2.0;

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
                BeginInvoke(new Action(() => ApplyDrawnShape(centerRow, centerCol, length1, length2, angleRad)));
            else
                ApplyDrawnShape(centerRow, centerCol, length1, length2, angleRad);
        }

        // RequestLine 於 MouseUp 傳回兩端點（>5px 才觸發）。直接寫進直線標稱幾何欄位。
        private void OnDrawnLine(double r1, double c1, double r2, double c2)
        {
            if (InvokeRequired)
                BeginInvoke(new Action(() => ApplyDrawnLine(r1, c1, r2, c2)));
            else
                ApplyDrawnLine(r1, c1, r2, c2);
        }

        private void ApplyDrawnLine(double r1, double c1, double r2, double c2)
        {
            if (_selected == null || _selected.Shape != MetrologyObjectType.Line) return;
            _selected.RowBegin = r1; _selected.ColumnBegin = c1;
            _selected.RowEnd = r2; _selected.ColumnEnd = c2;
            Populate(_selected);   // 於 _updating guard 下刷新 NUD
            _dirty = true;
            UpdateWarning();
        }

        // 依 Shape 把繪製結果寫進標稱幾何欄位（只動當前形狀相關欄位），再刷 NUD、標記 dirty、更新警示。
        private void ApplyDrawnShape(double cr, double cc, double len1, double len2, double phi)
        {
            if (_selected == null) return;
            switch (_selected.Shape)
            {
                case MetrologyObjectType.Rectangle:
                    _selected.Row = cr; _selected.Column = cc; _selected.Phi = phi;
                    _selected.Length1 = len1; _selected.Length2 = len2;
                    break;
                case MetrologyObjectType.Ellipse:
                    _selected.Row = cr; _selected.Column = cc; _selected.Phi = phi;
                    _selected.Radius1 = len1; _selected.Radius2 = len2;
                    break;
                case MetrologyObjectType.Circle:
                    _selected.Row = cr; _selected.Column = cc;
                    _selected.Radius = Math.Min(len1, len2);
                    break;
                default:
                    return; // Line 不處理
            }
            // 手拉框為外接框，標稱邊會落在真實邊外一段距離；量測區只在垂直 ±MeasureLength1 內搜尋。
            // 依框較短半邊自動放大搜尋帶（0.6 倍），讓粗略拉框也能往內吃到真邊；保持 < 幾何尺寸以通過驗證。
            double refDim = _selected.Shape == MetrologyObjectType.Circle ? _selected.Radius : Math.Min(len1, len2);
            if (refDim > 0)
                _selected.MeasureLength1 = Math.Max(5.0, Math.Round(0.6 * refDim));
            Populate(_selected);   // 於 _updating guard 下刷新 NUD
            _dirty = true;
            UpdateWarning();
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
            UpdateDrawButtonEnabled();
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
