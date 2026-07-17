using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.PcdAnalysis;

namespace FlashMeasurementSystem.Tests
{
    public static class PcdAnalysisDomainTests
    {
        private const double Cr = 500, Cc = 500, R = 250, Px = 10.0; // 1px=0.01mm

        // N 孔均分於半徑 R 的圓；dropHole 移除該孔；dR[i] 徑向位移；dDeg[i] 角度位移。
        private static List<HolePoint> Ring(int n, int dropHole = -1, double[] dR = null, double[] dDeg = null)
        {
            var pts = new List<HolePoint>();
            for (int i = 0; i < n; i++)
            {
                if (i == dropHole) continue;
                double deg = i * 360.0 / n + (dDeg != null ? dDeg[i] : 0.0);
                double rr = R + (dR != null ? dR[i] : 0.0);
                double th = deg * Math.PI / 180.0;
                pts.Add(new HolePoint { Row = Cr + rr * Math.Sin(th), Col = Cc + rr * Math.Cos(th) });
            }
            return pts;
        }

        public static void Run()
        {
            var p = new PcdAnalysisParameters { NominalHoleCount = 6, NominalPcdMm = 5.0,
                PcdToleranceMm = 0.05, AngularToleranceDeg = 0.5, RadialToleranceMm = 0.02 };

            // 完美 6 孔於 R=250px → 孔數 6、PcdPx=500、PcdMm=5.0、角/徑偏差≈0、PASS
            var r = PcdAnalyzer.Analyze(Ring(6), Px, p);
            AssertEqual(true, r.Success, "perfect Success");
            AssertEqual(6, r.HoleCount, "perfect count 6");
            AssertClose(500.0, r.PcdPx, 1e-6, "perfect PcdPx 500");
            AssertClose(5.0, r.PcdMm, 1e-6, "perfect PcdMm 5.0");
            AssertClose(0.0, r.AngularMaxDevDeg, 1e-6, "perfect angular dev 0");
            AssertClose(0.0, r.RadialMaxDevMm, 1e-6, "perfect radial dev 0");
            AssertEqual(true, r.IsPass, "perfect PASS");
            AssertEqual(6, r.Holes.Count, "perfect holes list");

            // 缺一孔 → 孔數 5、CountOk false、FAIL、缺孔提示落在缺孔角度（3×60=180°）
            var rm = PcdAnalyzer.Analyze(Ring(6, dropHole: 3), Px, p);
            AssertEqual(5, rm.HoleCount, "missing count 5");
            AssertEqual(false, rm.CountOk, "missing CountOk false");
            AssertEqual(false, rm.IsPass, "missing FAIL");
            bool hint = false; foreach (double h in rm.MissingHoleHintsDeg) if (Math.Abs(h - 180.0) < 1e-6) hint = true;
            if (!hint) throw new InvalidOperationException("missing hole hint at ~180deg");

            // 一孔偏半徑 +5px（=0.05mm > 0.02 公差）→ RadialOk false，角度仍 OK
            var dr = new double[6]; dr[2] = 5.0;
            var rr2 = PcdAnalyzer.Analyze(Ring(6, dR: dr), Px, p);
            AssertEqual(false, rr2.RadialOk, "radial dev → RadialOk false");
            AssertEqual(true, rr2.AngularOk, "radial-only → AngularOk true");

            // 一孔偏角 +2° → AngularOk false
            var dd = new double[6]; dd[4] = 2.0;
            var ra = PcdAnalyzer.Analyze(Ring(6, dDeg: dd), Px, p);
            AssertEqual(false, ra.AngularOk, "angular dev → AngularOk false");

            // PCD 邊界：標稱設成剛好內側 / 剛好外側夾 ≤ inclusive
            // PcdToleranceMm 加 1e-9 餘裕以吸收 Kasa 擬合的浮點捨入誤差（PcdMm 理論值 5.0 實際約
            // 4.9999999999999982，若 tolerance 精確取 0.05 會因 ~1.6e-15 的浮點噪聲落在邊界外側）。
            var pIn = new PcdAnalysisParameters { NominalHoleCount = 6, NominalPcdMm = 5.05,
                PcdToleranceMm = 0.05 + 1e-9, AngularToleranceDeg = 5, RadialToleranceMm = 5 }; // |5.0-5.05|=0.05 ≤ 0.05
            AssertEqual(true, PcdAnalyzer.Analyze(Ring(6), Px, pIn).PcdOk, "PCD just-inside inclusive");
            var pOut = new PcdAnalysisParameters { NominalHoleCount = 6, NominalPcdMm = 5.06,
                PcdToleranceMm = 0.05, AngularToleranceDeg = 5, RadialToleranceMm = 5 }; // |5.0-5.06|=0.06 > 0.05
            AssertEqual(false, PcdAnalyzer.Analyze(Ring(6), Px, pOut).PcdOk, "PCD just-outside excluded");

            // 失敗路徑
            AssertEqual(false, PcdAnalyzer.Analyze(null, Px, p).Success, "null fail");
            AssertEqual(false, PcdAnalyzer.Analyze(new List<HolePoint>(), Px, p).Success, "empty fail");
            AssertEqual(false, PcdAnalyzer.Analyze(Ring(2), Px, p).Success, "<3 holes fail");
            AssertEqual(false, PcdAnalyzer.Analyze(Ring(6), 0.0, p).Success, "pixelSize<=0 fail");
            AssertEqual(false, PcdAnalyzer.Analyze(Ring(6), Px,
                new PcdAnalysisParameters { NominalHoleCount = 0 }).Success, "nominal<=0 fail");
            // 共線三點 → 退化擬合失敗
            var collinear = new List<HolePoint> {
                new HolePoint { Row = 100, Col = 100 }, new HolePoint { Row = 100, Col = 200 },
                new HolePoint { Row = 100, Col = 300 } };
            AssertEqual(false, PcdAnalyzer.Analyze(collinear, Px, p).Success, "collinear fail");
        }

        private static void AssertEqual<T>(T e, T a, string n)
        { if (!object.Equals(e, a)) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
        private static void AssertClose(double e, double a, double t, string n)
        { if (Math.Abs(e - a) > t) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
    }
}
