using System;
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.Repeatability
{
    /// <summary>
    /// 重複性 / GR&R 統計報告（開發手冊 §6.3）。對「同一工件重複量測 N 次」的一組量測值，
    /// 算出過程能力指標。純 Domain、無硬體相依：對同一 replay 影像重複跑驗證的是「演算法數值
    /// 穩定性」（確定性）；含操作員/零件變異的完整 GR&R 需真實重複拍攝，那部分待硬體。
    ///
    /// StdDev 為樣本標準差（除以 N−1）。SixSigma = 6·StdDev（與公差無關）。
    /// GrrPercent = 6σ / 公差全幅 × 100；無有效公差（≤0）時為 NaN 且 Verdict=NotAvailable。
    /// </summary>
    public sealed class RepeatabilityReport
    {
        public int Count { get; set; }
        public double Mean { get; set; }
        public double StdDev { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Range { get; set; }        // Max − Min
        public double SixSigma { get; set; }      // 6 · StdDev
        public double GrrPercent { get; set; }    // 6σ / toleranceRange × 100（無公差時 NaN）
        public RepeatabilityVerdict Verdict { get; set; }

        /// <param name="values">重複量測值（同一量測項）。需 ≥ 2 筆。</param>
        /// <param name="toleranceRange">公差全幅（上限−下限，例如 ±0.010mm → 0.020）。≤0 時 GR&R% 不適用。</param>
        public static RepeatabilityReport Calculate(IList<double> values, double toleranceRange)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            int n = values.Count;
            if (n < 2) throw new ArgumentException("重複性統計需至少 2 筆量測值", nameof(values));

            double mean = 0.0, min = double.MaxValue, max = double.MinValue;
            foreach (double v in values)
            {
                mean += v;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            mean /= n;

            double sumSq = 0.0;
            foreach (double v in values) sumSq += (v - mean) * (v - mean);
            double stdDev = Math.Sqrt(sumSq / (n - 1));   // 樣本標準差（N−1）
            double sixSigma = 6.0 * stdDev;

            double grr;
            RepeatabilityVerdict verdict;
            if (toleranceRange > 0.0)
            {
                grr = sixSigma / toleranceRange * 100.0;
                if (grr < 10.0) verdict = RepeatabilityVerdict.Excellent;
                else if (grr <= 30.0) verdict = RepeatabilityVerdict.Acceptable;
                else verdict = RepeatabilityVerdict.Unacceptable;
            }
            else
            {
                grr = double.NaN;   // 無有效公差 → GR&R% 不適用
                verdict = RepeatabilityVerdict.NotAvailable;
            }

            return new RepeatabilityReport
            {
                Count = n,
                Mean = mean,
                StdDev = stdDev,
                Min = min,
                Max = max,
                Range = max - min,
                SixSigma = sixSigma,
                GrrPercent = grr,
                Verdict = verdict
            };
        }
    }
}
