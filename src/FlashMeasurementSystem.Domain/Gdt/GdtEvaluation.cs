using System.Globalization;

namespace FlashMeasurementSystem.Domain.Gdt
{
    /// <summary>單一形位公差項目的判定結果。</summary>
    public struct GdtJudgment
    {
        public bool IsOk;
        public double Deviation;       // 實際採用的偏差（已夾為 ≥0），mm
        public double ToleranceZone;   // T，mm
        public double MarginPercent;   // 到上限餘量 (T-dev)/T*100；OK 時 ≥0
        public bool NearBoundary;      // 接近上限警告（僅單邊上限，無下限概念）
        public string Message;
    }

    /// <summary>
    /// 形位公差**單邊**判定（純邏輯，無 HALCON）。允許範圍 [0, T]，無 Nominal。
    /// 與尺寸公差的雙邊 <see cref="Tolerance.ToleranceJudger"/> 刻意分開：形位偏差只往單方向增長，
    /// 偏差≈0 是「完美」而非「接近下限」，故只在接近上限 T 時警示。
    /// </summary>
    public static class GdtEvaluation
    {
        // 接近上限警告門檻：餘量低於 T 的 20% 時警示。
        private const double NearBoundaryThresholdPercent = 20.0;
        private const double Epsilon = 1e-10;

        public static GdtJudgment Evaluate(double deviation, double toleranceZoneMm)
        {
            var j = new GdtJudgment { Deviation = deviation, ToleranceZone = toleranceZoneMm };

            // 無效偏差（上游量測/構造失敗常產生 NaN/Infinity）：標 NG，不落入數值比較。
            if (double.IsNaN(deviation) || double.IsInfinity(deviation))
            {
                j.IsOk = false;
                j.Message = "NG：無效偏差值（NaN/Infinity）";
                return j;
            }

            // 無效公差帶寬（T≤0，多為未填或填錯）：無值可通過，標 NG 並給明確訊息。
            if (toleranceZoneMm <= Epsilon)
            {
                j.IsOk = false;
                j.Message = string.Format(CultureInfo.InvariantCulture,
                    "NG：公差帶寬無效（T={0:F4} ≤ 0）", toleranceZoneMm);
                return j;
            }

            // 偏差由構造保證 ≥0；防禦性夾為 0，避免負值算出 >100% 餘量。
            double dev = deviation < 0.0 ? 0.0 : deviation;
            j.Deviation = dev;

            j.MarginPercent = (toleranceZoneMm - dev) / toleranceZoneMm * 100.0;
            j.IsOk = dev <= toleranceZoneMm + Epsilon;

            if (j.IsOk)
            {
                j.NearBoundary = j.MarginPercent < NearBoundaryThresholdPercent;
                if (j.NearBoundary)
                {
                    j.Message = string.Format(CultureInfo.InvariantCulture,
                        "OK (偏差 {0:F4}mm / 帶寬 {1:F4}mm) ⚠️ 接近公差上限 (餘量 {2:F0}%)",
                        dev, toleranceZoneMm, j.MarginPercent);
                }
                else
                {
                    j.Message = string.Format(CultureInfo.InvariantCulture,
                        "OK (偏差 {0:F4}mm / 帶寬 {1:F4}mm)", dev, toleranceZoneMm);
                }
            }
            else
            {
                j.NearBoundary = false;
                j.Message = string.Format(CultureInfo.InvariantCulture,
                    "NG：超出公差帶 (偏差 {0:F4}mm > T {1:F4}mm)", dev, toleranceZoneMm);
            }

            return j;
        }
    }
}
