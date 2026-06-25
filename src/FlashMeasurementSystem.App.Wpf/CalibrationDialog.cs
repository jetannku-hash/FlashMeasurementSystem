using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using FlashMeasurementSystem.Application.Calibration;
using FlashMeasurementSystem.Domain.Calibration;
using FlashMeasurementSystem.Infrastructure.Calibration;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// 簡易像素尺寸校正對話框（M3b / 4.10b）。座標框輸入兩個校正點 + 已知距離，
    /// 呼叫 M2 的 PixelSizeCalibrator 算出 µm/px，顯示後可存成校正檔（data/calibrations）。
    /// 本階段只「計算 + 顯示 + 存檔」，不立即套用到量測 pixel size（留 M3c）。
    /// </summary>
    public sealed class CalibrationDialog : Form
    {
        private readonly ICalibrator _calibrator = new PixelSizeCalibrator();
        private readonly ICalibrationStore _store = new CalibrationStore();
        private CalibrationProfile _lastProfile;

        private TextBox _profileIdText;
        private NumericUpDown _knownDistance;
        private NumericUpDown _p1Row, _p1Col, _p2Row, _p2Col;
        private NumericUpDown _imgWidth, _imgHeight;
        private TextBox _resultBox;
        private Button _saveButton;

        public CalibrationDialog(int imageWidth, int imageHeight)
        {
            Text = "Pixel Size 校正";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(360, 430);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(10),
                AutoSize = false
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            AddRow(layout, "Profile ID", _profileIdText = new TextBox { Text = "CALIB-DEFAULT", Dock = DockStyle.Fill });
            AddRow(layout, "已知距離 (mm)", _knownDistance = MakeNumeric(0.0001M, 100000M, 4, 10M, 0.1M));
            AddRow(layout, "點1 Row", _p1Row = MakeNumeric(0M, 1000000M, 2, 0M, 1M));
            AddRow(layout, "點1 Col", _p1Col = MakeNumeric(0M, 1000000M, 2, 0M, 1M));
            AddRow(layout, "點2 Row", _p2Row = MakeNumeric(0M, 1000000M, 2, 0M, 1M));
            AddRow(layout, "點2 Col", _p2Col = MakeNumeric(0M, 1000000M, 2, 0M, 1M));
            AddRow(layout, "影像寬 (px)", _imgWidth = MakeNumeric(1M, 1000000M, 0, Math.Max(1, imageWidth), 1M));
            AddRow(layout, "影像高 (px)", _imgHeight = MakeNumeric(1M, 1000000M, 0, Math.Max(1, imageHeight), 1M));

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            var computeButton = new Button { Text = "計算", Width = 90 };
            computeButton.Click += OnCompute;
            _saveButton = new Button { Text = "存檔", Width = 90, Enabled = false };
            _saveButton.Click += OnSave;
            var closeButton = new Button { Text = "關閉", Width = 90 };
            closeButton.Click += (s, e) => Close();
            buttons.Controls.Add(computeButton);
            buttons.Controls.Add(_saveButton);
            buttons.Controls.Add(closeButton);
            AddSpanRow(layout, buttons);

            _resultBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Height = 110,
                BackColor = SystemColors.Window
            };
            AddSpanRow(layout, _resultBox, fill: true);

            Controls.Add(layout);
        }

        private void OnCompute(object sender, EventArgs e)
        {
            try
            {
                CalibrationProfile profile = _calibrator.CalibrateTwoPoint(
                    _profileIdText.Text,
                    (double)_knownDistance.Value,
                    (double)_p1Row.Value, (double)_p1Col.Value,
                    (double)_p2Row.Value, (double)_p2Col.Value,
                    (int)_imgWidth.Value, (int)_imgHeight.Value);

                _lastProfile = profile;
                _resultBox.Text = string.Format(CultureInfo.InvariantCulture,
                    "PixelSize X = {0:F4} um/px\r\nPixelSize Y = {1:F4} um/px (等向)\r\n" +
                    "量得像素 = {2:F2} px\r\nFOV = {3:F3} x {4:F3} mm",
                    profile.PixelSizeUmX, profile.PixelSizeUmY, profile.MeasuredPixels,
                    profile.FieldOfViewMmX, profile.FieldOfViewMmY);
                _saveButton.Enabled = true;
            }
            catch (ArgumentException ex)
            {
                _lastProfile = null;
                _saveButton.Enabled = false;
                _resultBox.Text = "計算失敗: " + ex.Message;
            }
        }

        private void OnSave(object sender, EventArgs e)
        {
            if (_lastProfile == null) return;
            try
            {
                string dir = ResolveCalibrationsDir();
                Directory.CreateDirectory(dir);
                string fileName = SanitizeFileName(
                    string.IsNullOrEmpty(_lastProfile.ProfileId) ? "CALIB-DEFAULT" : _lastProfile.ProfileId) + ".json";
                string path = Path.Combine(dir, fileName);
                _store.Save(_lastProfile, path);
                _resultBox.Text += "\r\n\r\n已存檔:\r\n" + path;
            }
            catch (Exception ex)
            {
                _resultBox.Text += "\r\n\r\n存檔失敗: " + ex.Message;
            }
        }

        private static string ResolveCalibrationsDir() => DataPaths.SubDir("calibrations");

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private static NumericUpDown MakeNumeric(decimal min, decimal max, int decimals, decimal value, decimal increment)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Value = value < min ? min : (value > max ? max : value),
                Increment = increment,
                Dock = DockStyle.Fill
            };
        }

        private static void AddRow(TableLayoutPanel layout, string label, Control input)
        {
            int row = layout.RowCount;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);
            layout.Controls.Add(input, 1, row);
            layout.RowCount = row + 1;
        }

        private static void AddSpanRow(TableLayoutPanel layout, Control content, bool fill = false)
        {
            int row = layout.RowCount;
            layout.RowStyles.Add(new RowStyle(fill ? SizeType.Percent : SizeType.Absolute, fill ? 100F : 40F));
            layout.Controls.Add(content, 0, row);
            layout.SetColumnSpan(content, 2);
            layout.RowCount = row + 1;
        }
    }
}
