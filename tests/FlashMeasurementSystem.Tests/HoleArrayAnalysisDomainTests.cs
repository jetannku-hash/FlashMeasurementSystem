using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.HoleArrayAnalysis;

namespace FlashMeasurementSystem.Tests
{
    public static class HoleArrayAnalysisDomainTests
    {
        private const double Px = 10.0; // 1px = 0.01mm → 100px = 1.0mm

        // rows×cols 網格：Row = row0 + j*pitchY、Col = col0 + i*pitchX，孔徑固定 diaPx。
        // skipIndex ≥ 0 時略過該顆（缺孔）；bumpIndex ≥ 0 時該顆 Row 額外加 bumpPx（位移）。
        private static List<HoleArrayPoint> Grid(int rows, int cols, double row0, double col0,
            double pitchY, double pitchX, double diaPx,
            int skipIndex = -1, int bumpIndex = -1, double bumpPx = 0.0)
        {
            var pts = new List<HoleArrayPoint>();
            int k = 0;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++, k++)
                {
                    if (k == skipIndex) continue;
                    double rr = row0 + j * pitchY + (k == bumpIndex ? bumpPx : 0.0);
                    pts.Add(new HoleArrayPoint { Row = rr, Col = col0 + i * pitchX, DiameterPx = diaPx });
                }
            }
            return pts;
        }

        // 繞質心整體旋轉 angleRad
        private static List<HoleArrayPoint> Rotate(List<HoleArrayPoint> src, double angleRad)
        {
            double mr = 0, mc = 0;
            foreach (HoleArrayPoint p in src) { mr += p.Row; mc += p.Col; }
            mr /= src.Count; mc /= src.Count;
            double cs = Math.Cos(angleRad), sn = Math.Sin(angleRad);
            var outp = new List<HoleArrayPoint>();
            foreach (HoleArrayPoint p in src)
            {
                double dr = p.Row - mr, dc = p.Col - mc;
                outp.Add(new HoleArrayPoint
                {
                    Row = mr + dr * cs - dc * sn,
                    Col = mc + dr * sn + dc * cs,
                    DiameterPx = p.DiameterPx
                });
            }
            return outp;
        }

        private static HoleArrayAnalysisParameters Params()
        {
            return new HoleArrayAnalysisParameters
            {
                Rows = 3, Cols = 4,
                NominalDiameterMm = 0.4, DiameterToleranceMm = 0.02,
                NominalPitchXMm = 1.0, NominalPitchYMm = 0.8,
                PitchToleranceMm = 0.02, PositionToleranceMm = 0.01
            };
        }

        public static void Run()
        {
            var p = Params();

            // 完美 3 列 × 4 行：行距(主軸) 100px = 1.0mm、列距 80px = 0.8mm、孔徑 40px = 0.4mm → PASS
            var perfect = Grid(3, 4, 100, 200, 80, 100, 40);
            var r = HoleArrayAnalyzer.Analyze(perfect, Px, p);
            AssertEqual(true, r.Success, "perfect Success");
            AssertEqual(12, r.HoleCount, "perfect count 12");
            AssertClose(1.0, r.PitchXMm, 1e-9, "perfect PitchXMm 1.0");
            AssertClose(0.8, r.PitchYMm, 1e-9, "perfect PitchYMm 0.8");
            AssertClose(0.4, r.MeanDiameterMm, 1e-9, "perfect MeanDiameterMm 0.4");
            AssertClose(0.0, r.DiameterMaxDevMm, 1e-9, "perfect DiameterMaxDev 0");
            AssertClose(0.0, r.MaxPositionDevMm, 1e-9, "perfect MaxPositionDev 0");
            AssertEqual(true, r.CountOk, "perfect CountOk");
            AssertEqual(true, r.DiameterOk, "perfect DiameterOk");
            AssertEqual(true, r.PitchXOk, "perfect PitchXOk");
            AssertEqual(true, r.PitchYOk, "perfect PitchYOk");
            AssertEqual(true, r.PositionOk, "perfect PositionOk");
            AssertEqual(true, r.IsPass, "perfect PASS");
            AssertEqual(12, r.Holes.Count, "perfect holes list");

            // 缺一孔（11 顆）→ CountOk false、整體 FAIL（分群仍應正常退化，不丟例外）
            var missing = Grid(3, 4, 100, 200, 80, 100, 40, skipIndex: 5);
            var rm = HoleArrayAnalyzer.Analyze(missing, Px, p);
            AssertEqual(true, rm.Success, "missing Success");
            AssertEqual(11, rm.HoleCount, "missing count 11");
            AssertEqual(false, rm.CountOk, "missing CountOk false");
            AssertEqual(false, rm.IsPass, "missing FAIL");

            // 孔徑整體偏大 60px = 0.6mm（超出 0.4±0.02）→ DiameterOk false
            var bigDia = Grid(3, 4, 100, 200, 80, 100, 60);
            var rd = HoleArrayAnalyzer.Analyze(bigDia, Px, p);
            AssertClose(0.6, rd.MeanDiameterMm, 1e-9, "bigDia mean 0.6");
            AssertEqual(false, rd.DiameterOk, "bigDia DiameterOk false");
            AssertEqual(false, rd.IsPass, "bigDia FAIL");
            AssertEqual(true, rd.CountOk, "bigDia CountOk true");

            // 單孔位移 30px（Row 方向）→ PositionOk false、偏差明顯 > 0
            var shifted = Grid(3, 4, 100, 200, 80, 100, 40, bumpIndex: 0, bumpPx: 30.0);
            var rs = HoleArrayAnalyzer.Analyze(shifted, Px, p);
            AssertEqual(12, rs.HoleCount, "shifted count 12");
            AssertEqual(false, rs.PositionOk, "shifted PositionOk false");
            if (rs.MaxPositionDevMm <= 0.1)
                throw new InvalidOperationException("shifted MaxPositionDevMm should be large, got " + rs.MaxPositionDevMm);
            AssertEqual(false, rs.IsPass, "shifted FAIL");

            // 行距 120px = 1.2mm（超出 1.0±0.02）→ PitchXOk false，PitchYOk 仍 true
            var offPitch = Grid(3, 4, 100, 200, 80, 120, 40);
            var rp = HoleArrayAnalyzer.Analyze(offPitch, Px, p);
            AssertClose(1.2, rp.PitchXMm, 1e-9, "offPitch PitchXMm 1.2");
            AssertEqual(false, rp.PitchXOk, "offPitch PitchXOk false");
            AssertEqual(true, rp.PitchYOk, "offPitch PitchYOk true");
            AssertEqual(false, rp.IsPass, "offPitch FAIL");

            // 傾斜網格（整體繞中心轉 30°）→ 仍 PASS（證明 PCA 主軸/投影處理方向）
            var tilted = Rotate(perfect, 30.0 * Math.PI / 180.0);
            var rt = HoleArrayAnalyzer.Analyze(tilted, Px, p);
            AssertEqual(12, rt.HoleCount, "tilted count 12");
            AssertClose(1.0, rt.PitchXMm, 1e-9, "tilted PitchXMm 1.0");
            AssertClose(0.8, rt.PitchYMm, 1e-9, "tilted PitchYMm 0.8");
            AssertClose(0.0, rt.MaxPositionDevMm, 1e-9, "tilted MaxPositionDev 0");
            AssertEqual(true, rt.IsPass, "tilted PASS");

            // 高瘦網格（4 列 × 3 行，列距 100px > 行距 80px）→ Y 展幅(300px) > X 展幅(160px)。
            // PCA 主軸會落在「列」方向，若直接把主軸當行方向會導致 X/Y 對調（此為回歸測試）。
            var tallP = new HoleArrayAnalysisParameters
            {
                Rows = 4, Cols = 3,
                NominalDiameterMm = 0.4, DiameterToleranceMm = 0.02,
                NominalPitchXMm = 0.8, NominalPitchYMm = 1.0,
                PitchToleranceMm = 0.02, PositionToleranceMm = 0.01
            };
            var tall = Grid(4, 3, 100, 200, 100, 80, 40);
            var rtall = HoleArrayAnalyzer.Analyze(tall, Px, tallP);
            AssertEqual(true, rtall.Success, "tall Success");
            AssertEqual(12, rtall.HoleCount, "tall count 12");
            AssertClose(0.8, rtall.PitchXMm, 1e-9, "tall PitchXMm 0.8");
            AssertClose(1.0, rtall.PitchYMm, 1e-9, "tall PitchYMm 1.0");
            AssertClose(0.0, rtall.MaxPositionDevMm, 1e-9, "tall MaxPositionDev 0");
            AssertEqual(true, rtall.PitchXOk, "tall PitchXOk");
            AssertEqual(true, rtall.PitchYOk, "tall PitchYOk");
            AssertEqual(true, rtall.CountOk, "tall CountOk");
            AssertEqual(true, rtall.IsPass, "tall PASS");

            // 單列（Rows=1）→ PitchYMm 0 且 PitchYOk 不判定為 true，其餘仍照判
            var single = new HoleArrayAnalysisParameters
            {
                Rows = 1, Cols = 4,
                NominalDiameterMm = 0.4, DiameterToleranceMm = 0.02,
                NominalPitchXMm = 1.0, NominalPitchYMm = 0.8,
                PitchToleranceMm = 0.02, PositionToleranceMm = 0.01
            };
            var oneRow = Grid(1, 4, 300, 200, 0, 100, 40);
            var r1 = HoleArrayAnalyzer.Analyze(oneRow, Px, single);
            AssertEqual(4, r1.HoleCount, "oneRow count 4");
            AssertClose(1.0, r1.PitchXMm, 1e-9, "oneRow PitchXMm 1.0");
            AssertClose(0.0, r1.PitchYMm, 1e-9, "oneRow PitchYMm 0");
            AssertEqual(true, r1.PitchYOk, "oneRow PitchYOk not judged");
            AssertEqual(true, r1.PitchXOk, "oneRow PitchXOk");
            AssertEqual(true, r1.CountOk, "oneRow CountOk");
            AssertEqual(true, r1.PositionOk, "oneRow PositionOk");
            AssertEqual(true, r1.IsPass, "oneRow PASS");

            // 失敗路徑
            AssertEqual(false, HoleArrayAnalyzer.Analyze(null, Px, p).Success, "null fail");
            var one = new List<HoleArrayPoint> { new HoleArrayPoint { Row = 100, Col = 100, DiameterPx = 40 } };
            var rf = HoleArrayAnalyzer.Analyze(one, Px, p);
            AssertEqual(false, rf.Success, "<2 holes fail Success");
            AssertEqual(false, rf.IsPass, "<2 holes fail IsPass");
            if (string.IsNullOrEmpty(rf.ErrorMessage))
                throw new InvalidOperationException("<2 holes ErrorMessage non-empty");
            AssertEqual(false, HoleArrayAnalyzer.Analyze(perfect, 0.0, p).Success, "pixelSize<=0 fail");
            AssertEqual(false, HoleArrayAnalyzer.Analyze(perfect, Px,
                new HoleArrayAnalysisParameters { Rows = 0, Cols = 4 }).Success, "Rows<1 fail");
            AssertEqual(false, HoleArrayAnalyzer.Analyze(perfect, Px,
                new HoleArrayAnalysisParameters { Rows = 3, Cols = 0 }).Success, "Cols<1 fail");
        }

        private static void AssertEqual<T>(T e, T a, string n)
        { if (!object.Equals(e, a)) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
        private static void AssertClose(double e, double a, double t, string n)
        { if (Math.Abs(e - a) > t) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
    }
}
