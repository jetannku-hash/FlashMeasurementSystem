using System;
using System.Collections.Generic;
using System.Globalization;
using FlashMeasurementSystem.Application.Tolerance;
using FlashMeasurementSystem.Domain.Tolerance;

namespace FlashMeasurementSystem.Infrastructure.Tolerance
{
    /// <summary>
    /// 公差判定（純邏輯，不依賴 HALCON）。將每個量測項目的實測值與
    /// [Nominal+Lower, Nominal+Upper] 比對，輸出 OK/NG 與接近邊界警告。
    /// </summary>
    public class ToleranceJudger : IToleranceJudger
    {
        // 接近邊界警告門檻：到最近邊界的餘量低於公差半寬的 20% 時警示。
        private const double NearBoundaryThresholdPercent = 20.0;
        private const double Epsilon = 1e-10;

        public OverallJudgment Judge(IList<ToleranceItemInput> items)
        {
            var result = new OverallJudgment();

            if (items == null)
            {
                result.AllOk = true;
                return result;
            }

            foreach (ToleranceItemInput item in items)
            {
                if (item == null) continue;

                ToleranceSpec spec = item.Spec ?? ToleranceSpec.Default();
                double lower = spec.LowerLimit;
                double upper = spec.UpperLimit;
                double v = item.MeasuredValue;

                var j = new ItemJudgment
                {
                    ToolId = item.ToolId,
                    ToolName = item.ToolName,
                    MeasuredValue = v,
                    Nominal = spec.Nominal,
                    LowerLimit = lower,
                    UpperLimit = upper,
                    Unit = spec.Unit,
                    Deviation = v - spec.Nominal
                };

                // 無效實測值（上游量測失敗常產生 NaN/Infinity）：NaN 的所有比較皆為 false，
                // 若不攔截會落入 else 分支誤報「超出上限」。在此標為 NG 並給明確訊息後跳過數值計算。
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    j.IsOk = false;
                    j.Message = "NG：無效量測值（NaN/Infinity）";
                    result.NgCount++;
                    result.Items.Add(j);
                    continue;
                }

                // 偏差百分比（避免除以零）
                if (Math.Abs(spec.Nominal) > Epsilon)
                    j.DeviationPercent = j.Deviation / spec.Nominal * 100.0;

                j.IsOk = v >= lower && v <= upper;

                // 到最近邊界的餘量百分比（改良手冊版的 margin 語意）：
                // marginToNearest = min(v-lower, upper-v)，除以公差半寬。
                // OK 時為正（越接近 0 越靠邊界），NG 時為負。
                double tolRange = upper - lower;
                if (tolRange > Epsilon)
                {
                    double half = tolRange / 2.0;
                    double marginToNearest = Math.Min(v - lower, upper - v);
                    j.MarginPercent = marginToNearest / half * 100.0;
                }

                if (j.IsOk)
                {
                    j.IsNearBoundary = tolRange > Epsilon && j.MarginPercent < NearBoundaryThresholdPercent;
                    if (j.IsNearBoundary)
                    {
                        j.Message = string.Format(CultureInfo.InvariantCulture,
                            "OK (偏差 {0:F4} {1}) ⚠️ 接近公差邊界 (餘量 {2:F0}%)",
                            j.Deviation, j.Unit, j.MarginPercent);
                    }
                    else
                    {
                        j.Message = string.Format(CultureInfo.InvariantCulture,
                            "OK (偏差 {0:F4} {1})", j.Deviation, j.Unit);
                    }
                    result.OkCount++;
                }
                else
                {
                    if (v < lower)
                    {
                        j.Message = string.Format(CultureInfo.InvariantCulture,
                            "NG：低於下限 {0:F4} {1} (下限 {2:F4})", j.Deviation, j.Unit, lower);
                    }
                    else
                    {
                        j.Message = string.Format(CultureInfo.InvariantCulture,
                            "NG：超出上限 +{0:F4} {1} (上限 {2:F4})", j.Deviation, j.Unit, upper);
                    }
                    result.NgCount++;
                }

                result.Items.Add(j);
            }

            result.AllOk = result.NgCount == 0;
            return result;
        }
    }
}
