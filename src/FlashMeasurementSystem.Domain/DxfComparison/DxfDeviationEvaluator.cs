using System;

namespace FlashMeasurementSystem.Domain.DxfComparison
{
    /// <summary>
    /// 純偏差統計與輪廓度判定（無 HALCON）。輸入逐點偏差（px），輸出 max/mean/RMS/超差點數 + PASS/FAIL。
    /// HALCON adapter 取得 distance 屬性 tuple 後呼叫；判定含邊界（max ≤ T 為 PASS）。
    /// </summary>
    public static class DxfDeviationEvaluator
    {
        public static DxfComparisonResult Evaluate(double[] deviationsPx, double tolerancePx)
        {
            if (deviationsPx == null || deviationsPx.Length == 0)
                return DxfComparisonResult.Failed("無偏差資料（框帶內取不到實際輪廓點）");

            double max = 0.0, sum = 0.0, sumSq = 0.0;
            int over = 0;
            for (int i = 0; i < deviationsPx.Length; i++)
            {
                double d = Math.Abs(deviationsPx[i]); // 無號
                if (d > max) max = d;
                sum += d;
                sumSq += d * d;
                if (d > tolerancePx) over++;
            }
            int n = deviationsPx.Length;

            return new DxfComparisonResult
            {
                Success = true,
                MaxDevPx = max,
                MeanDevPx = sum / n,
                RmsDevPx = Math.Sqrt(sumSq / n),
                PointsEvaluated = n,
                PointsOverTolerance = over,
                IsPass = max <= tolerancePx
            };
        }
    }
}
