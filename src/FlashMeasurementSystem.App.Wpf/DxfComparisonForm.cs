using System;
using System.Windows.Forms;
using FlashMeasurementSystem.Application.DxfComparison;
using FlashMeasurementSystem.Domain.DxfComparison;
using HalconDotNet;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// DXF/CAD 輪廓度比對獨立面板：選 DXF、設公差/最低分/scale 種子、執行 → PASS/FAIL + 統計 + overlay。
    /// 用共用主視窗 HWindowControlHelper 畫 overlay（比照 RecipeEditor 接管共用影像）。
    /// </summary>
    public sealed class DxfComparisonForm : Form
    {
        private readonly HWindowControlHelper _imageHelper;
        private readonly IDxfContourComparer<HImage> _comparer;

        private TextBox _dxfPathBox;
        private NumericUpDown _toleranceNumeric;
        private NumericUpDown _minScoreNumeric;
        private NumericUpDown _scaleSeedNumeric;
        private Button _runButton;
        private Label _resultLabel;

        public DxfComparisonForm(HWindowControlHelper imageHelper, IDxfContourComparer<HImage> comparer)
        {
            _imageHelper = imageHelper ?? throw new ArgumentNullException(nameof(imageHelper));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));

            Text = "DXF/CAD 輪廓度比對";
            Width = 460; Height = 260;

            var browse = new Button { Text = "選 DXF…", Left = 12, Top = 12, Width = 90 };
            browse.Click += OnBrowse;
            _dxfPathBox = new TextBox { Left = 110, Top = 14, Width = 320, ReadOnly = true };

            _toleranceNumeric = LabeledNumeric("公差 T (px)", 12, 50, 2.0m, 0.01m, 0.1m, 100m, out Label t1);
            _minScoreNumeric = LabeledNumeric("最低分", 12, 84, 0.5m, 0.01m, 0.1m, 1.0m, out Label t2);
            _scaleSeedNumeric = LabeledNumeric("scale 種子 (px/mm，0=自動)", 12, 118, 0m, 0.1m, 0m, 1000m, out Label t3);

            _runButton = new Button { Text = "執行比對", Left = 12, Top = 156, Width = 120 };
            _runButton.Click += OnRun;

            _resultLabel = new Label { Left = 150, Top = 156, Width = 280, Height = 50, Text = "" };

            Controls.AddRange(new Control[] { browse, _dxfPathBox, t1, _toleranceNumeric,
                t2, _minScoreNumeric, t3, _scaleSeedNumeric, _runButton, _resultLabel });
        }

        private NumericUpDown LabeledNumeric(string caption, int left, int top, decimal val,
            decimal inc, decimal min, decimal max, out Label label)
        {
            label = new Label { Text = caption, Left = left, Top = top + 2, Width = 170 };
            return new NumericUpDown
            {
                Left = left + 180, Top = top, Width = 90,
                DecimalPlaces = 2, Increment = inc, Minimum = min, Maximum = max, Value = val
            };
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "DXF (*.dxf)|*.dxf", Title = "選擇 DXF 標稱輪廓" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _dxfPathBox.Text = dlg.FileName;
            }
        }

        private void OnRun(object sender, EventArgs e)
        {
            if (_imageHelper.CurrentImage == null)
            {
                MessageBox.Show(this, "請先在主視窗載入影像。", "DXF 比對"); return;
            }
            if (string.IsNullOrEmpty(_dxfPathBox.Text))
            {
                MessageBox.Show(this, "請先選 DXF 檔。", "DXF 比對"); return;
            }

            var pars = new DxfComparisonParameters
            {
                TolerancePx = (double)_toleranceNumeric.Value,
                MinScore = (double)_minScoreNumeric.Value,
                ScaleSeedPxPerMm = (double)_scaleSeedNumeric.Value
            };

            Cursor = Cursors.WaitCursor;
            try
            {
                DxfComparisonResult r = _comparer.Compare(_imageHelper.CurrentImage, _dxfPathBox.Text, pars);
                _resultLabel.Text = r.Message;
                _resultLabel.ForeColor = !r.Success ? System.Drawing.SystemColors.ControlText
                    : (r.IsPass ? System.Drawing.Color.DarkGreen : System.Drawing.Color.DarkRed);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "DXF 比對異常：" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }
    }
}
