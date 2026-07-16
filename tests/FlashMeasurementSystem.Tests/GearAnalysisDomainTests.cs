using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.GearAnalysis;

namespace FlashMeasurementSystem.Tests
{
    public static class GearAnalysisDomainTests
    {
        private const double Cr = 500, Cc = 500, R = 200;

        private static List<EdgePoint> Gear(int n, double widthDeg,
            int dropTooth = -1, double[] extraWidthDeg = null, double[] shiftDeg = null)
        {
            var pts = new List<EdgePoint>();
            for (int i = 0; i < n; i++)
            {
                if (i == dropTooth) continue;
                double centerDeg = i * 360.0 / n + (shiftDeg != null ? shiftDeg[i] : 0.0);
                double w = widthDeg + (extraWidthDeg != null ? extraWidthDeg[i] : 0.0);
                // HALCON 弧掃描慣例（見 GearToothAnalyzer 的 amplitude 註解）：暗齒沿 +θ 的
                // 進齒邊(light→dark) amplitude 為【正】、出齒邊(dark→light)為【負】。
                AddEdge(pts, centerDeg - w / 2.0, +30.0);  // 進齒（暗齒起始）
                AddEdge(pts, centerDeg + w / 2.0, -30.0);  // 出齒（暗齒結束）
            }
            return pts;
        }

        private static void AddEdge(List<EdgePoint> pts, double deg, double amp)
        {
            double th = deg * Math.PI / 180.0;
            pts.Add(new EdgePoint
            {
                Row = Cr + R * Math.Sin(th),
                Column = Cc + R * Math.Cos(th),
                Amplitude = amp,
                Distance = 0
            });
        }

        public static void Run()
        {
            var g = new GearAnalysisParameters { NominalToothCount = 20, PitchToleranceDeg = 0.5, WidthToleranceDeg = 0.5 };
            var r = GearToothAnalyzer.Analyze(Gear(20, 8.0), Cr, Cc, R, g);
            AssertEqual(true, r.Success, "perfect Success");
            AssertEqual(20, r.ToothCount, "perfect count 20");
            AssertClose(18.0, r.PitchMeanDeg, 1e-6, "perfect pitch mean 18");
            AssertClose(0.0, r.PitchMaxDevDeg, 1e-6, "perfect pitch dev 0");
            AssertClose(8.0, r.WidthMeanDeg, 1e-6, "perfect width mean 8");
            AssertClose(0.0, r.WidthMaxDevDeg, 1e-6, "perfect width dev 0");
            AssertEqual(true, r.IsPass, "perfect PASS");
            AssertEqual(20, r.Teeth.Count, "perfect teeth list");

            var rm = GearToothAnalyzer.Analyze(Gear(20, 8.0, dropTooth: 5), Cr, Cc, R, g);
            AssertEqual(19, rm.ToothCount, "missing count 19");
            AssertEqual(false, rm.CountOk, "missing CountOk false");
            AssertEqual(false, rm.IsPass, "missing FAIL");
            if (rm.MissingToothHintsDeg.Count == 0) throw new InvalidOperationException("missing tooth should hint");
            // 缺第 5 齒（5×18°=90°）→ 提示點應落在缺口中點 ≈ 90°（缺齒該在的位置），不是旁邊的齒上。
            bool hintAtGap = false;
            foreach (double h in rm.MissingToothHintsDeg) if (Math.Abs(h - 90.0) < 1e-6) hintAtGap = true;
            if (!hintAtGap) throw new InvalidOperationException("missing hint should sit at the gap centre ~90deg");

            var ew = new double[20]; ew[3] = -4.0;
            var rw = GearToothAnalyzer.Analyze(Gear(20, 8.0, extraWidthDeg: ew), Cr, Cc, R, g);
            AssertEqual(20, rw.ToothCount, "narrow count 20");
            AssertEqual(false, rw.WidthOk, "narrow WidthOk false");
            AssertEqual(true, rw.PitchOk, "narrow PitchOk true");

            var sh = new double[20]; sh[7] = 3.0;
            var rp = GearToothAnalyzer.Analyze(Gear(20, 8.0, shiftDeg: sh), Cr, Cc, R, g);
            AssertEqual(false, rp.PitchOk, "shift PitchOk false");

            var sh0 = new double[20]; for (int i = 0; i < 20; i++) sh0[i] = -9.0;
            var rc = GearToothAnalyzer.Analyze(Gear(20, 8.0, shiftDeg: sh0), Cr, Cc, R, g);
            AssertEqual(20, rc.ToothCount, "wrap count 20");
            AssertClose(0.0, rc.PitchMaxDevDeg, 1e-6, "wrap pitch dev 0");

            // 齒寬公差邊界：以「剛好內側 / 剛好外側」夾住 inclusive ≤ 判定（避開 atan2 round-trip 的精確 == 脆弱性）
            // 剛好內側：一齒 +0.5° → mean=(19*8+8.5)/20=8.025，maxDev=|8.5-8.025|=0.475 < 0.5 → WidthOk true
            var ewb = new double[20]; ewb[2] = 0.5;
            var gb = new GearAnalysisParameters { NominalToothCount = 20, PitchToleranceDeg = 5.0, WidthToleranceDeg = 0.5 };
            var rb = GearToothAnalyzer.Analyze(Gear(20, 8.0, extraWidthDeg: ewb), Cr, Cc, R, gb);
            AssertEqual(true, rb.WidthOk, "just-inside WidthOk true");
            // 剛好外側：一齒 +0.6° → mean=(19*8+8.6)/20=8.03，maxDev=|8.6-8.03|=0.57 > 0.5 → WidthOk false
            var ewb2 = new double[20]; ewb2[2] = 0.6;
            var rw2 = GearToothAnalyzer.Analyze(Gear(20, 8.0, extraWidthDeg: ewb2), Cr, Cc, R, gb);
            AssertEqual(false, rw2.WidthOk, "just-outside WidthOk false");

            var gf = new GearAnalysisParameters { NominalToothCount = 20, ToothIsDark = false, PitchToleranceDeg = 0.5, WidthToleranceDeg = 100 };
            var rf = GearToothAnalyzer.Analyze(Gear(20, 8.0), Cr, Cc, R, gf);
            AssertEqual(20, rf.ToothCount, "flip count 20");
            AssertClose(18.0, rf.PitchMeanDeg, 1e-6, "flip pitch mean 18");
            AssertClose(10.0, rf.WidthMeanDeg, 1e-6, "flip width = gap = 18-8 = 10");

            AssertEqual(false, GearToothAnalyzer.Analyze(new List<EdgePoint>(), Cr, Cc, R, g).Success, "empty fail");
            AssertEqual(false, GearToothAnalyzer.Analyze(null, Cr, Cc, R, g).Success, "null fail");
            var odd = Gear(20, 8.0); odd.RemoveAt(0);
            AssertEqual(false, GearToothAnalyzer.Analyze(odd, Cr, Cc, R, g).Success, "odd count fail");
            // 未交替（E,E,L,L）：偶數且成對數相等，但序列非交替 → Failed（spec §9）
            var nonAlt = new List<EdgePoint>();
            AddEdge(nonAlt, 10, +30); AddEdge(nonAlt, 20, +30);   // 兩個進齒（新慣例：正）
            AddEdge(nonAlt, 30, -30); AddEdge(nonAlt, 40, -30);   // 兩個出齒（負）
            AssertEqual(false, GearToothAnalyzer.Analyze(nonAlt, Cr, Cc, R, g).Success, "non-alternating fail");
            AssertEqual(false, GearToothAnalyzer.Analyze(Gear(20, 8.0), Cr, Cc, R,
                new GearAnalysisParameters { NominalToothCount = 0 }).Success, "nominal<=0 fail");
        }

        private static void AssertEqual<T>(T e, T a, string n)
        { if (!object.Equals(e, a)) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
        private static void AssertClose(double e, double a, double t, string n)
        { if (Math.Abs(e - a) > t) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
    }
}
