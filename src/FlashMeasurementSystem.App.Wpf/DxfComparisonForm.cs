using System;
using System.Windows.Forms;
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
        private readonly FlashMeasurementSystem.Halcon.DxfComparison.HalconDxfContourComparer _comparer;

        private TextBox _dxfPathBox;
        private NumericUpDown _toleranceNumeric;
        private NumericUpDown _minScoreNumeric;
        private NumericUpDown _scaleSeedNumeric;
        private Button _runButton;
        private Label _resultLabel;

        // 三個 iconic 各自的唯一 owner：本表單負責釋放（DisposeIconics），即使 CompareWithOverlay
        // 回傳 FAILED 結果，alignedNominal 仍可能非 null（見 Task 5 註解），故不可只在成功分支釋放。
        private HObject _previewContour;
        private HObject _alignedNominal;
        private HObject _actualEdges;

        // 本表單對共用影像視窗的 overlay 所有權。畫在自己的圖層上，關閉時 Dispose 即收回，
        // 下層（主視窗）的量測結果 overlay 會自動重新顯示——不再需要 _installedOverlay 記帳。
        private readonly IOverlayLease _lease;

        public DxfComparisonForm(HWindowControlHelper imageHelper, FlashMeasurementSystem.Halcon.DxfComparison.HalconDxfContourComparer comparer)
        {
            _imageHelper = imageHelper ?? throw new ArgumentNullException(nameof(imageHelper));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            _lease = _imageHelper.AcquireOverlay("DxfComparisonForm");

            // 字型須在建立子控制項前設定，子控制項才會繼承。本表單子控制項是絕對定位
            // （Left/Top），故只用與原字型同行高（13px）、寬度僅多 3~7% 的 8.25pt；
            // 9pt 會把絕對座標的版面撐破。
            Font = new System.Drawing.Font("Segoe UI", 8.25F);

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

        private DxfComparisonParameters BuildParams() => new DxfComparisonParameters
        {
            TolerancePx = (double)_toleranceNumeric.Value,
            MinScore = (double)_minScoreNumeric.Value,
            ScaleSeedPxPerMm = (double)_scaleSeedNumeric.Value
        };

        private void DisposeIconics()
        {
            _previewContour?.Dispose(); _previewContour = null;
            _alignedNominal?.Dispose(); _alignedNominal = null;
            _actualEdges?.Dispose(); _actualEdges = null;
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "DXF (*.dxf)|*.dxf", Title = "選擇 DXF 標稱輪廓" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _dxfPathBox.Text = dlg.FileName;
            }

            // 幽靈預覽：讀取標稱輪廓並置中縮放顯示，讓使用者在執行比對前先確認 DXF 讀對了。
            DisposeIconics();
            _previewContour = _comparer.LoadNominalContour(_dxfPathBox.Text, BuildParams());
            if (_previewContour == null)
            {
                MessageBox.Show(this, "無法讀取 DXF 標稱輪廓（可能非 AC1009/R12 或實體不支援）。", "DXF 比對");
                return;
            }
            if (_imageHelper.CurrentImage != null)
            {
                HOperatorSet.GetImageSize(_imageHelper.CurrentImage, out HTuple iw, out HTuple ih);
                var prev = _previewContour; double w = iw.D, h = ih.D;
                _lease.SetPersistentOverlay(() => _imageHelper.Annotator.DrawContourFitted(prev, w, h, "gray"));
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

            Cursor = Cursors.WaitCursor;
            try
            {
                // 釋放上一輪 iconic：即使上一輪 CompareWithOverlay 回傳 FAILED，
                // _alignedNominal/_actualEdges 仍可能持有非 null 句柄（Task 5 行為），此處一併清掉。
                DisposeIconics();
                DxfComparisonResult r = _comparer.CompareWithOverlay(_imageHelper.CurrentImage, _dxfPathBox.Text, BuildParams(),
                    out _alignedNominal, out _actualEdges, out double[] overRows, out double[] overCols);
                _resultLabel.Text = r.Message;
                _resultLabel.ForeColor = !r.Success ? System.Drawing.SystemColors.ControlText
                    : (r.IsPass ? System.Drawing.Color.DarkGreen : System.Drawing.Color.DarkRed);
                if (r.Success)
                {
                    var aligned = _alignedNominal; var actual = _actualEdges;
                    const int maxMarks = 200;
                    int step = overRows.Length > maxMarks ? (int)Math.Ceiling(overRows.Length / (double)maxMarks) : 1;
                    _lease.SetPersistentOverlay(() =>
                    {
                        var an = _imageHelper.Annotator;
                        an.DrawContour(aligned, "blue");
                        an.DrawContour(actual, "green");
                        for (int i = 0; i < overRows.Length; i += step)
                            an.DrawCross(overRows[i], overCols[i], 12, "red");
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "DXF 比對異常：" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // 釋放本表單的圖層所有權：只收自己畫的，主視窗當前的量測結果 overlay 自動回來。
            _lease.Dispose();
            DisposeIconics();
            base.OnFormClosed(e);
        }
    }
}
