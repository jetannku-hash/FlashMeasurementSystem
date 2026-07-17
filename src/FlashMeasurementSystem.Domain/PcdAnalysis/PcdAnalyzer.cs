using System;
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.PcdAnalysis
{
    /// <summary>
    /// 純 PCD 分析（無 HALCON）。孔質心 → Kåsa 代數圓擬合 → 節圓直徑/角度均勻/徑向真圓度 + 四條件判定。
    /// 判定全在此層（pixelSizeUm 由呼叫端傳入，PCD/徑向轉 mm），合成質心可全驗。
    /// </summary>
    public static class PcdAnalyzer
    {
        public static PcdAnalysisResult Analyze(
            System.Collections.Generic.IList<HolePoint> holes, double pixelSizeUm, PcdAnalysisParameters parameters)
        {
            var p = parameters ?? PcdAnalysisParameters.Default();
            if (p.NominalHoleCount <= 0 || p.PcdToleranceMm <= 0 || p.AngularToleranceDeg <= 0 || p.RadialToleranceMm <= 0)
                return PcdAnalysisResult.Failed("PCD 參數無效（標稱孔數與公差需 > 0）");
            if (holes == null || holes.Count < 3)
                return PcdAnalysisResult.Failed("孔數不足（圓擬合需 ≥ 3 個孔）");
            if (pixelSizeUm <= 0)
                return PcdAnalysisResult.Failed("像素尺寸無效");

            // Kåsa 代數圓擬合：x²+y²+D·x+E·y+F=0（x=Col, y=Row）；解 3×3 正規方程。
            int n = holes.Count;
            double Sx = 0, Sy = 0, Sxx = 0, Syy = 0, Sxy = 0, Sxz = 0, Syz = 0, Sz = 0;
            foreach (HolePoint h in holes)
            {
                double x = h.Col, y = h.Row, z = x * x + y * y;
                Sx += x; Sy += y; Sxx += x * x; Syy += y * y; Sxy += x * y;
                Sxz += x * z; Syz += y * z; Sz += z;
            }
            // 正規方程 A·[D;E;F] = b，b = -[Sxz; Syz; Sz]
            double[,] A = { { Sxx, Sxy, Sx }, { Sxy, Syy, Sy }, { Sx, Sy, n } };
            double[] b = { -Sxz, -Syz, -Sz };
            if (!Solve3x3(A, b, out double D, out double E, out double F))
                return PcdAnalysisResult.Failed("孔心無法擬合圓（共線或退化）");
            double cc = -D / 2.0, cr = -E / 2.0;
            double rsq = D * D / 4.0 + E * E / 4.0 - F;
            if (double.IsNaN(rsq) || rsq <= 0)
                return PcdAnalysisResult.Failed("孔心無法擬合圓（退化）");
            double R = Math.Sqrt(rsq);

            const double TwoPi = 2.0 * Math.PI;
            double mmPerPx = pixelSizeUm / 1000.0;

            var result = new PcdAnalysisResult { Success = true, HoleCount = n };
            result.CenterRow = cr; result.CenterCol = cc;
            result.PcdPx = 2.0 * R; result.PcdMm = result.PcdPx * mmPerPx;

            // 角度/徑距
            var angs = new double[n];
            double radialMaxDevPx = 0;
            var byAngle = new List<KeyValuePair<double, HolePoint>>(n);
            foreach (HolePoint h in holes)
            {
                double th = Math.Atan2(h.Row - cr, h.Col - cc); if (th < 0) th += TwoPi;
                double radial = Math.Sqrt((h.Row - cr) * (h.Row - cr) + (h.Col - cc) * (h.Col - cc));
                double dev = Math.Abs(radial - R); if (dev > radialMaxDevPx) radialMaxDevPx = dev;
                byAngle.Add(new KeyValuePair<double, HolePoint>(th, h));
            }
            byAngle.Sort((a, b2) => a.Key.CompareTo(b2.Key));
            for (int i = 0; i < n; i++) { angs[i] = byAngle[i].Key; result.Holes.Add(byAngle[i].Value); }

            result.RadialMaxDevPx = radialMaxDevPx;
            result.RadialMaxDevMm = radialMaxDevPx * mmPerPx;

            // 相鄰角距（環繞、和為 2π）→ 對均值 2π/n 的最大偏差
            double d2 = 180.0 / Math.PI;
            var pitches = new double[n];
            for (int i = 0; i < n; i++)
            {
                double d = angs[(i + 1) % n] - angs[i]; if (d <= 0) d += TwoPi;
                pitches[i] = d;
            }
            double meanPitch = TwoPi / n;
            double angMaxDev = 0;
            for (int i = 0; i < n; i++) { double dv = Math.Abs(pitches[i] - meanPitch); if (dv > angMaxDev) angMaxDev = dv; }
            result.AngularMeanDeg = meanPitch * d2;
            result.AngularMaxDevDeg = angMaxDev * d2;

            // 缺孔：角距 > 1.5×中位數 → 提示點放間隙中點
            double median = Median(pitches);
            for (int i = 0; i < n; i++)
                if (pitches[i] > 1.5 * median)
                {
                    double mid = angs[i] + pitches[i] / 2.0; if (mid >= TwoPi) mid -= TwoPi;
                    result.MissingHoleHintsDeg.Add(mid * d2);
                }

            result.CountOk = n == p.NominalHoleCount;
            result.PcdOk = Math.Abs(result.PcdMm - p.NominalPcdMm) <= p.PcdToleranceMm;
            result.AngularOk = result.AngularMaxDevDeg <= p.AngularToleranceDeg;
            result.RadialOk = result.RadialMaxDevMm <= p.RadialToleranceMm;
            result.IsPass = result.CountOk && result.PcdOk && result.AngularOk && result.RadialOk;
            result.Message = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "孔數={0}(標稱{1}) PCD={2:F3}mm 角偏差={3:F2}° 徑偏差={4:F3}mm → {5}",
                n, p.NominalHoleCount, result.PcdMm, result.AngularMaxDevDeg, result.RadialMaxDevMm,
                result.IsPass ? "PASS" : "FAIL");
            return result;
        }

        // 3×3 線性解（高斯消去 + 部分主元）；奇異回 false。
        private static bool Solve3x3(double[,] A, double[] b, out double x0, out double x1, out double x2)
        {
            x0 = x1 = x2 = 0;
            double[,] m = { { A[0, 0], A[0, 1], A[0, 2], b[0] },
                            { A[1, 0], A[1, 1], A[1, 2], b[1] },
                            { A[2, 0], A[2, 1], A[2, 2], b[2] } };
            for (int col = 0; col < 3; col++)
            {
                int piv = col; double best = Math.Abs(m[col, col]);
                for (int r = col + 1; r < 3; r++) { double v = Math.Abs(m[r, col]); if (v > best) { best = v; piv = r; } }
                if (best < 1e-12) return false;
                if (piv != col) for (int c = 0; c < 4; c++) { double t = m[col, c]; m[col, c] = m[piv, c]; m[piv, c] = t; }
                for (int r = 0; r < 3; r++)
                {
                    if (r == col) continue;
                    double f = m[r, col] / m[col, col];
                    for (int c = col; c < 4; c++) m[r, c] -= f * m[col, c];
                }
            }
            x0 = m[0, 3] / m[0, 0]; x1 = m[1, 3] / m[1, 1]; x2 = m[2, 3] / m[2, 2];
            return true;
        }

        private static double Median(double[] v)
        {
            var s = (double[])v.Clone(); Array.Sort(s);
            int m = s.Length / 2;
            return (s.Length % 2 == 0) ? (s[m - 1] + s[m]) / 2.0 : s[m];
        }
    }
}
