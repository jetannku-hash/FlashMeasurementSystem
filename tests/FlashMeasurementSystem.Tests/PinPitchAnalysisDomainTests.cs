using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.PinPitchAnalysis;

namespace FlashMeasurementSystem.Tests
{
    public static class PinPitchAnalysisDomainTests
    {
        private const double Px = 10.0; // 1px = 0.01mm → 100px = 1.0mm

        // 沿 (row0,col0) 起、每步 (dr,dc) 的直列引腳；perpIndex 那顆額外加垂直位移 perpPx。
        private static List<PinPoint> Row(double row0, double col0, double dr, double dc,
            double[] cols = null, int perpIndex = -1, double perpPx = 0.0)
        {
            var pts = new List<PinPoint>();
            if (cols != null) // 水平列：固定 row0，Col 由 cols 指定
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    double rr = row0 + (i == perpIndex ? perpPx : 0.0);
                    pts.Add(new PinPoint { Row = rr, Col = cols[i] });
                }
            }
            return pts;
        }

        public static void Run()
        {
            var p = new PinPitchAnalysisParameters { NominalPinCount = 6, NominalPitchMm = 1.0,
                PitchToleranceMm = 0.05, UniformityToleranceMm = 0.02 };

            // 完美 6 腳，水平等距 100px = 1.0mm → PASS
            var perfect = Row(300, 0, 0, 0, new double[] { 100, 200, 300, 400, 500, 600 });
            var r = PinPitchAnalyzer.Analyze(perfect, Px, p);
            AssertEqual(true, r.Success, "perfect Success");
            AssertEqual(6, r.PinCount, "perfect count 6");
            AssertClose(1.0, r.PitchMeanMm, 1e-9, "perfect PitchMeanMm 1.0");
            AssertClose(0.0, r.PitchMaxDevMm, 1e-9, "perfect PitchMaxDev 0");
            AssertClose(0.0, r.StraightnessDevPx, 1e-9, "perfect straightness 0");
            AssertEqual(true, r.MissingOk, "perfect MissingOk");
            AssertEqual(true, r.CountOk, "perfect CountOk");
            AssertEqual(true, r.PitchOk, "perfect PitchOk");
            AssertEqual(true, r.UniformityOk, "perfect UniformityOk");
            AssertEqual(true, r.IsPass, "perfect PASS");
            AssertEqual(6, r.Pins.Count, "perfect pins list");
            AssertEqual(5, r.PitchesMm.Count, "perfect 5 pitches");

            // 缺第 3 腳（Col=300 移除）→ 一間隙 ~2× → MissingOk false、CountOk false
            var missing = Row(300, 0, 0, 0, new double[] { 100, 200, 400, 500, 600 });
            var rm = PinPitchAnalyzer.Analyze(missing, Px, p);
            AssertEqual(5, rm.PinCount, "missing count 5");
            AssertEqual(false, rm.MissingOk, "missing MissingOk false");
            if (string.IsNullOrEmpty(rm.MissingHint))
                throw new InvalidOperationException("missing MissingHint non-empty");
            AssertEqual(false, rm.CountOk, "missing CountOk false");
            AssertEqual(false, rm.IsPass, "missing FAIL");

            // 均勻但平均間距 120px = 1.2mm（超出 1.0±0.05）→ PitchOk false，UniformityOk true
            var offNominal = Row(300, 0, 0, 0, new double[] { 100, 220, 340, 460, 580, 700 });
            var ro = PinPitchAnalyzer.Analyze(offNominal, Px, p);
            AssertClose(1.2, ro.PitchMeanMm, 1e-9, "offNominal mean 1.2");
            AssertEqual(false, ro.PitchOk, "offNominal PitchOk false");
            AssertEqual(true, ro.UniformityOk, "offNominal UniformityOk true");

            // 一腳垂直位移 20px → StraightnessDevPx 明顯變大
            var bent = Row(300, 0, 0, 0, new double[] { 100, 200, 300, 400, 500, 600 },
                perpIndex: 2, perpPx: 20.0);
            var rb = PinPitchAnalyzer.Analyze(bent, Px, p);
            if (rb.StraightnessDevPx <= 5.0)
                throw new InvalidOperationException("bent StraightnessDevPx should be large, got " + rb.StraightnessDevPx);

            // 間距抖動（110px 那格對均值偏差 0.1mm > 0.02）→ UniformityOk false，MissingOk 仍 true
            var jitter = Row(300, 0, 0, 0, new double[] { 100, 200, 290, 400, 500, 600 });
            var rj = PinPitchAnalyzer.Analyze(jitter, Px, p);
            AssertEqual(false, rj.UniformityOk, "jitter UniformityOk false");
            AssertEqual(true, rj.MissingOk, "jitter MissingOk true");

            // 傾斜列（45°，沿線間距同 100px）→ 仍 PASS（證明主軸擬合/投影處理方向）
            var tilted = new List<PinPoint>();
            double step = 100.0 / Math.Sqrt(2.0);
            for (int i = 0; i < 6; i++)
                tilted.Add(new PinPoint { Row = 100 + i * step, Col = 100 + i * step });
            var rt = PinPitchAnalyzer.Analyze(tilted, Px, p);
            AssertEqual(6, rt.PinCount, "tilted count 6");
            AssertClose(1.0, rt.PitchMeanMm, 1e-9, "tilted PitchMeanMm 1.0");
            AssertClose(0.0, rt.PitchMaxDevMm, 1e-9, "tilted PitchMaxDev 0");
            AssertClose(0.0, rt.StraightnessDevPx, 1e-9, "tilted straightness 0");
            AssertEqual(true, rt.IsPass, "tilted PASS");

            // 失敗路徑
            AssertEqual(false, PinPitchAnalyzer.Analyze(null, Px, p).Success, "null fail");
            var one = new List<PinPoint> { new PinPoint { Row = 100, Col = 100 } };
            var r1 = PinPitchAnalyzer.Analyze(one, Px, p);
            AssertEqual(false, r1.Success, "<2 pins fail Success");
            AssertEqual(false, r1.IsPass, "<2 pins fail IsPass");
            if (string.IsNullOrEmpty(r1.ErrorMessage))
                throw new InvalidOperationException("<2 pins ErrorMessage non-empty");
            AssertEqual(false, PinPitchAnalyzer.Analyze(perfect, 0.0, p).Success, "pixelSize<=0 fail");
        }

        private static void AssertEqual<T>(T e, T a, string n)
        { if (!object.Equals(e, a)) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
        private static void AssertClose(double e, double a, double t, string n)
        { if (Math.Abs(e - a) > t) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
    }
}
