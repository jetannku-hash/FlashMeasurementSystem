using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using FlashMeasurementSystem.Domain.ImageQuality;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// 編輯「本配方」的影像品質門檻（schema v15）。
    ///
    /// 存在理由：正確的亮度/銳利度合格範圍取決於工件、鏡頭與打光，單一全域值不可能對所有料號
    /// 都正確。門檻寫死時，一旦不適用當前設定，操作員除了請工程師勾「略過IQC」之外毫無出路——
    /// 那是把品質把關整個關掉，而不是把它調對。
    ///
    /// 「使用全域預設」勾選時，配方存的是 null（不是把預設值複製一份進去）。差別在於：
    /// 存 null 的配方會跟著日後預設值的調整走，複製一份則會凍結在今天的值。
    /// </summary>
    public sealed class IqcThresholdsDialog : Form
    {
        private readonly CheckBox _useDefaultCheck;
        private readonly NumericUpDown _minBrightness;
        private readonly NumericUpDown _maxBrightness;
        private readonly NumericUpDown _maxSaturation;
        private readonly NumericUpDown _minBlur;
        private readonly NumericUpDown _minContrast;
        private readonly Label _hintLabel;

        /// <summary>編輯結果；null 表示「使用全域預設」。</summary>
        public ImageQualityThresholds Result { get; private set; }

        public IqcThresholdsDialog(ImageQualityThresholds current)
        {
            Font = new Font("Segoe UI", 8.25F);

            Text = "影像品質門檻（本配方）";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(430, 300);

            ImageQualityThresholds seed = current ?? ImageQualityThresholds.Default();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(10)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _useDefaultCheck = new CheckBox
            {
                Text = "使用全域預設門檻",
                Dock = DockStyle.Fill,
                Checked = current == null
            };
            _useDefaultCheck.CheckedChanged += (s, e) => ApplyEnabledState();
            AddSpanRow(layout, _useDefaultCheck, 26F);

            _minBrightness = AddRow(layout, "亮度下限 (0–255)", MakeNumeric(0M, 255M, 1, (decimal)seed.MinBrightness, 1M));
            _maxBrightness = AddRow(layout, "亮度上限 (0–255)", MakeNumeric(0M, 255M, 1, (decimal)seed.MaxBrightness, 1M));
            _maxSaturation = AddRow(layout, "飽和像素比例上限 (%)", MakeNumeric(0M, 100M, 2, (decimal)seed.MaxSaturationRatio, 0.1M));
            _minBlur = AddRow(layout, "銳利度下限", MakeNumeric(0M, 100000M, 1, (decimal)seed.MinBlurScore, 1M));
            _minContrast = AddRow(layout, "對比度下限", MakeNumeric(0M, 100000M, 1, (decimal)seed.MinContrast, 1M));

            ImageQualityThresholds def = ImageQualityThresholds.Default();
            _hintLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.GrayText,
                Text = string.Format(CultureInfo.InvariantCulture,
                    "全域預設：亮度 {0:F0}–{1:F0}、飽和 ≤{2:F1}%、銳利度 ≥{3:F0}、對比 ≥{4:F0}\r\n" +
                    "目前影像的實測值可在主視窗按「影像品質檢查」查看。",
                    def.MinBrightness, def.MaxBrightness, def.MaxSaturationRatio,
                    def.MinBlurScore, def.MinContrast)
            };
            AddSpanRow(layout, _hintLabel, 48F);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };
            var cancel = new Button { Text = "取消", Width = 80, DialogResult = DialogResult.Cancel };
            var ok = new Button { Text = "確定", Width = 80 };
            ok.Click += OnOk;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AddSpanRow(layout, buttons, 40F);

            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = cancel;

            ApplyEnabledState();
        }

        private void ApplyEnabledState()
        {
            bool custom = !_useDefaultCheck.Checked;
            _minBrightness.Enabled = custom;
            _maxBrightness.Enabled = custom;
            _maxSaturation.Enabled = custom;
            _minBlur.Enabled = custom;
            _minContrast.Enabled = custom;
        }

        private void OnOk(object sender, EventArgs e)
        {
            if (_useDefaultCheck.Checked)
            {
                Result = null;
                DialogResult = DialogResult.OK;
                return;
            }

            // 在這裡就擋下設反的範圍：讓它存進配方雖然 RecipeValidator 之後也會報，
            // 但在按下確定的當下就講清楚，比等到量測前才發現省事。
            if (_minBrightness.Value >= _maxBrightness.Value)
            {
                MessageBox.Show(this,
                    "亮度下限須小於上限，否則任何影像都會被判為不合格。",
                    "影像品質門檻", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Result = new ImageQualityThresholds
            {
                MinBrightness = (double)_minBrightness.Value,
                MaxBrightness = (double)_maxBrightness.Value,
                MaxSaturationRatio = (double)_maxSaturation.Value,
                MinBlurScore = (double)_minBlur.Value,
                MinContrast = (double)_minContrast.Value
            };
            DialogResult = DialogResult.OK;
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

        private static NumericUpDown AddRow(TableLayoutPanel layout, string label, NumericUpDown input)
        {
            int row = layout.RowCount;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);
            layout.Controls.Add(input, 1, row);
            layout.RowCount = row + 1;
            return input;
        }

        private static void AddSpanRow(TableLayoutPanel layout, Control content, float height)
        {
            int row = layout.RowCount;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(content, 0, row);
            layout.SetColumnSpan(content, 2);
            layout.RowCount = row + 1;
        }
    }
}
