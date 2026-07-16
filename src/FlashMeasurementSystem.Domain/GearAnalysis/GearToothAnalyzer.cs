using System;
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.GearAnalysis
{
    /// <summary>
    /// 純齒輪分析（無 HALCON）。弧卡尺邊點 → 依 amplitude 正負分進/出齒 → 配對成齒（含 0/2π 環繞）
    /// → 齒數/齒距/齒寬統計 + 三條件判定。角度：內部弧度，對外輸出度。中心由呼叫端傳入（弧 ROI 中心）。
    /// </summary>
    public static class GearToothAnalyzer
    {
        public static GearAnalysisResult Analyze(
            System.Collections.Generic.IList<FlashMeasurementSystem.Domain.EdgeDetection.EdgePoint> edgePoints,
            double centerRow, double centerCol, double radiusPx, GearAnalysisParameters parameters)
        {
            var p = parameters ?? GearAnalysisParameters.Default();
            if (p.NominalToothCount <= 0 || p.PitchToleranceDeg <= 0 || p.WidthToleranceDeg <= 0)
                return GearAnalysisResult.Failed("齒輪參數無效（標稱齒數與公差需 > 0）");
            if (edgePoints == null || edgePoints.Count < 4 || (edgePoints.Count % 2) != 0)
                return GearAnalysisResult.Failed("邊點數不足或非偶數（需成對；請調 Sigma/Threshold/環寬）");
            if (radiusPx <= 0)
                return GearAnalysisResult.Failed("量測半徑無效");

            const double TwoPi = 2.0 * Math.PI;
            var arr = new List<KeyValuePair<double, bool>>();
            foreach (var e in edgePoints)
            {
                double th = Math.Atan2(e.Row - centerRow, e.Column - centerCol);
                if (th < 0) th += TwoPi;
                bool entering = p.ToothIsDark ? (e.Amplitude < 0) : (e.Amplitude > 0);
                arr.Add(new KeyValuePair<double, bool>(th, entering));
            }
            arr.Sort((a, b) => a.Key.CompareTo(b.Key));

            int start = arr.FindIndex(kv => kv.Value);
            if (start < 0) return GearAnalysisResult.Failed("未偵測到進齒邊（齒為暗/亮參數可能設反）");

            int n = arr.Count;
            var seq = new List<KeyValuePair<double, bool>>(n);
            for (int i = 0; i < n; i++) seq.Add(arr[(start + i) % n]);
            for (int i = 0; i < n; i++)
                if (seq[i].Value != (i % 2 == 0))
                    return GearAnalysisResult.Failed("進/出齒邊未交替（請調 Sigma/Threshold/環寬或極性）");

            int teeth = n / 2;
            var centers = new double[teeth];
            var widths = new double[teeth];
            for (int t = 0; t < teeth; t++)
            {
                double te = seq[2 * t].Key;
                double tl = seq[2 * t + 1].Key;
                double w = tl - te; if (w < 0) w += TwoPi;
                widths[t] = w;
                double c = te + w / 2.0; if (c >= TwoPi) c -= TwoPi;
                centers[t] = c;
            }

            var pitches = new double[teeth];
            for (int t = 0; t < teeth; t++)
            {
                double d = centers[(t + 1) % teeth] - centers[t];
                if (d <= 0) d += TwoPi;
                pitches[t] = d;
            }

            var result = new GearAnalysisResult { Success = true, ToothCount = teeth };
            Stats(pitches, out double pMean, out double pMin, out double pMax, out double pDev);
            Stats(widths, out double wMean, out double wMin, out double wMax, out double wDev);
            double d2 = 180.0 / Math.PI;
            result.PitchMeanDeg = pMean * d2; result.PitchMinDeg = pMin * d2; result.PitchMaxDeg = pMax * d2; result.PitchMaxDevDeg = pDev * d2;
            result.WidthMeanDeg = wMean * d2; result.WidthMinDeg = wMin * d2; result.WidthMaxDeg = wMax * d2; result.WidthMaxDevDeg = wDev * d2;
            result.WidthMeanPx = radiusPx * wMean;

            for (int t = 0; t < teeth; t++)
                result.Teeth.Add(new GearTooth { CenterAngleDeg = centers[t] * d2, WidthDeg = widths[t] * d2 });

            double median = Median(pitches);
            for (int t = 0; t < teeth; t++)
                if (pitches[t] > 1.5 * median)
                    result.MissingToothHintsDeg.Add(centers[t] * d2);

            result.CountOk = teeth == p.NominalToothCount;
            result.PitchOk = result.PitchMaxDevDeg <= p.PitchToleranceDeg;
            result.WidthOk = result.WidthMaxDevDeg <= p.WidthToleranceDeg;
            result.IsPass = result.CountOk && result.PitchOk && result.WidthOk;
            result.Message = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "齒數={0}(標稱{1}) 齒距偏差={2:F2}° 齒寬偏差={3:F2}° → {4}",
                teeth, p.NominalToothCount, result.PitchMaxDevDeg, result.WidthMaxDevDeg,
                result.IsPass ? "PASS" : "FAIL");
            return result;
        }

        private static void Stats(double[] v, out double mean, out double min, out double max, out double maxDev)
        {
            double sum = 0; min = double.MaxValue; max = double.MinValue;
            for (int i = 0; i < v.Length; i++) { sum += v[i]; if (v[i] < min) min = v[i]; if (v[i] > max) max = v[i]; }
            mean = sum / v.Length;
            maxDev = 0;
            for (int i = 0; i < v.Length; i++) { double d = Math.Abs(v[i] - mean); if (d > maxDev) maxDev = d; }
        }

        private static double Median(double[] v)
        {
            var s = (double[])v.Clone(); Array.Sort(s);
            int m = s.Length / 2;
            return (s.Length % 2 == 0) ? (s[m - 1] + s[m]) / 2.0 : s[m];
        }
    }
}
