using System;
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.PinPitchAnalysis
{
    /// <summary>
    /// 純引腳間距分析（無 HALCON）。引腳質心 → 主軸線擬合（2×2 共變異的主特徵向量，
    /// 支援水平/垂直/傾斜列）→ 沿線投影排序 → 相鄰間距/均勻度/缺腳/直線度 + 四條件判定。
    /// 判定全在此層（pixelSizeUm 由呼叫端傳入，間距轉 mm），合成質心可全驗。
    /// </summary>
    public static class PinPitchAnalyzer
    {
        public static PinPitchAnalysisResult Analyze(
            IList<PinPoint> pins, double pixelSizeUm, PinPitchAnalysisParameters parameters)
        {
            var p = parameters ?? PinPitchAnalysisParameters.Default();
            if (pins == null || pins.Count < 2)
                return PinPitchAnalysisResult.Failed("引腳不足（需 ≥ 2 個引腳）");
            if (pixelSizeUm <= 0)
                return PinPitchAnalysisResult.Failed("像素尺寸無效");

            int n = pins.Count;
            double mmPerPx = pixelSizeUm / 1000.0;

            // 質心
            double meanRow = 0, meanCol = 0;
            foreach (PinPoint pt in pins) { meanRow += pt.Row; meanCol += pt.Col; }
            meanRow /= n; meanCol /= n;

            // 2×2 共變異矩陣 [[a,b],[b,c]]（a=Row 變異、c=Col 變異、b=交叉）
            double a = 0, b = 0, c = 0;
            foreach (PinPoint pt in pins)
            {
                double dr = pt.Row - meanRow, dc = pt.Col - meanCol;
                a += dr * dr; b += dr * dc; c += dc * dc;
            }
            a /= n; b /= n; c /= n;

            // 主特徵向量（較大特徵值）：λ1 = (a+c)/2 + sqrt(((a-c)/2)²+b²)
            double half = (a - c) / 2.0;
            double lambda1 = (a + c) / 2.0 + Math.Sqrt(half * half + b * b);
            double ur, uc; // 沿線單位方向（Row, Col）
            if (Math.Abs(b) > 1e-12)
            {
                ur = b; uc = lambda1 - a;
            }
            else
            {
                // 對角化：軸對齊，取變異較大的軸
                if (a >= c) { ur = 1; uc = 0; } else { ur = 0; uc = 1; }
            }
            double norm = Math.Sqrt(ur * ur + uc * uc);
            if (norm < 1e-12)
                return PinPitchAnalysisResult.Failed("引腳質心退化（無法擬合主軸）");
            ur /= norm; uc /= norm;

            // 沿線投影參數 + 至線垂距（法向 = (-uc, ur)）
            var proj = new List<KeyValuePair<double, PinPoint>>(n);
            double straightnessDevPx = 0;
            foreach (PinPoint pt in pins)
            {
                double dr = pt.Row - meanRow, dc = pt.Col - meanCol;
                double t = dr * ur + dc * uc;
                double perp = Math.Abs(dr * (-uc) + dc * ur);
                if (perp > straightnessDevPx) straightnessDevPx = perp;
                proj.Add(new KeyValuePair<double, PinPoint>(t, pt));
            }
            proj.Sort((x, y) => x.Key.CompareTo(y.Key));

            var result = new PinPitchAnalysisResult { Success = true, PinCount = n };
            result.StraightnessDevPx = straightnessDevPx;
            for (int i = 0; i < n; i++) result.Pins.Add(proj[i].Value);

            // 相鄰間距（px → mm）
            var pitchesPx = new double[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                pitchesPx[i] = proj[i + 1].Key - proj[i].Key;
                result.PitchesMm.Add(pitchesPx[i] * mmPerPx);
            }

            // 平均間距、對均值的最大偏差
            double meanMm = 0;
            foreach (double v in result.PitchesMm) meanMm += v;
            meanMm /= result.PitchesMm.Count;
            result.PitchMeanMm = meanMm;
            double maxDev = 0;
            foreach (double v in result.PitchesMm) { double d = Math.Abs(v - meanMm); if (d > maxDev) maxDev = d; }
            result.PitchMaxDevMm = maxDev;

            // 缺腳：任一間距 > 1.5×中位數 → MissingOk false，提示落在間隙位置
            double median = Median(pitchesPx);
            var gaps = new List<int>();
            for (int i = 0; i < pitchesPx.Length; i++)
                if (pitchesPx[i] > 1.5 * median) gaps.Add(i);
            result.MissingOk = gaps.Count == 0;
            if (gaps.Count > 0)
            {
                var parts = new List<string>();
                foreach (int i in gaps)
                    parts.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "引腳#{0}–#{1} 間距 {2:F3}mm(≈{3:F1}×中位)", i, i + 1,
                        pitchesPx[i] * mmPerPx, pitchesPx[i] / median));
                result.MissingHint = "疑似缺腳: " + string.Join("; ", parts.ToArray());
            }

            // 判定
            result.CountOk = p.NominalPinCount <= 0 ? true : n == p.NominalPinCount;
            result.PitchOk = Math.Abs(result.PitchMeanMm - p.NominalPitchMm) <= p.PitchToleranceMm;
            result.UniformityOk = result.PitchMaxDevMm <= p.UniformityToleranceMm;
            result.IsPass = result.CountOk && result.PitchOk && result.UniformityOk && result.MissingOk;
            return result;
        }

        private static double Median(double[] v)
        {
            var s = (double[])v.Clone(); Array.Sort(s);
            int m = s.Length / 2;
            return (s.Length % 2 == 0) ? (s[m - 1] + s[m]) / 2.0 : s[m];
        }
    }
}
