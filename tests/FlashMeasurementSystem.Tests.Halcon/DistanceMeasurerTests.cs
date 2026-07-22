using System;
using FlashMeasurementSystem.Domain.DistanceMeasurement;
using FlashMeasurementSystem.Halcon.DistanceMeasurement;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class DistanceMeasurerTests
    {
        private const double TolMm = 0.01;

        public static void Run()
        {
            var dm = new HalconDistanceMeasurer();
            var dp = new DistanceMeasurementParameters { PixelSizeUmX = 1.0, PixelSizeUmY = 1.0 };

            // ── PointToPoint ──
            DistanceMeasurementResult pp = dm.MeasurePointToPoint(0, 0, 100, 0, dp);
            Assert(pp.Success, "Pt2Pt: Success");
            AssertClose(100.0, pp.DistancePx, 0.01, "Pt2Pt: 100 px");
            AssertClose(0.1, pp.DistanceMm, TolMm, "Pt2Pt: 0.1 mm");

            DistanceMeasurementResult pp2 = dm.MeasurePointToPoint(10, 10, 10, 110, dp);
            Assert(pp2.Success, "Pt2Pt horizontal: Success");
            AssertClose(100.0, pp2.DistancePx, 0.01, "Pt2Pt horizontal: 100 px");

            // ── PointToLine: point (50,0) to horizontal line at row=0 spanning (0,0)→(0,100) ──
            // distance_pl: distance from point to the infinite line through endpoints
            // (row1,col1)=(0,0), (row2,col2)=(0,100): horizontal line along row=0
            // point (50,0): row distance = 50
            DistanceMeasurementResult pl = dm.MeasurePointToLine(50, 0, 0, 0, 0, 100, dp);
            Assert(pl.Success, "Pt2Line: Success, got=" + pl.DistancePx.ToString("F2"));
            AssertClose(50.0, pl.DistancePx, 0.01, "Pt2Line: 50px to horizontal y=0");

            // ── LineToLine ──
            DistanceMeasurementResult ll = dm.MeasureLineToLine(0, 0, 100, 0, 0, 30, 100, 30, dp);
            Assert(ll.Success, "Ln2Ln: Success, got=" + ll.DistancePx.ToString("F2"));
            AssertClose(30.0, ll.DistancePx, 0.01, "Ln2Ln: 30px between parallel vertical lines");

            // ── CircleToCircle ──
            DistanceMeasurementResult cc = dm.MeasureCircleToCircle(50, 50, 50, 250, dp);
            Assert(cc.Success, "Cir2Cir: Success, got=" + cc.DistancePx.ToString("F2"));
            AssertClose(200.0, cc.DistancePx, 0.01, "Cir2Cir: 200px center distance");

            // ── Pixel-to-mm scaling ──
            var dpUm = new DistanceMeasurementParameters { PixelSizeUmX = 2.5, PixelSizeUmY = 2.5 };
            DistanceMeasurementResult ppUm = dm.MeasurePointToPoint(0, 0, 0, 1000, dpUm);
            Assert(ppUm.Success, "Pt2Pt um scale: Success, got=" + ppUm.DistanceMm.ToString("F4"));
            AssertClose(2.5, ppUm.DistanceMm, TolMm, "Pt2Pt um: 1000px*2.5um=2.5mm");

            Console.WriteLine("DistanceMeasurerTests passed");
        }

        private static void Assert(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
        private static void AssertClose(double exp, double act, double tol, string msg)
        {
            if (Math.Abs(exp - act) > tol)
                throw new InvalidOperationException(msg + " exp=" + exp + " act=" + act);
        }
    }
}
