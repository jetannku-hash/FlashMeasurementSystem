using System;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using FlashMeasurementSystem.Halcon.AngleMeasurement;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class AngleMeasurerTests
    {
        private const double TolDeg = 0.5;

        public static void Run()
        {
            var measurer = new HalconAngleMeasurer();

            // ── 1. line_to_line: 90° ──
            var ap = AngleMeasurementParameters.Default();
            ap.Mode = "line_to_line";
            AngleMeasurementResult r90 = measurer.MeasureAngle(
                0, 0, 100, 0,    // vertical
                0, 0, 0, 100,    // horizontal
                ap);
            Assert(r90.Success, "line_to_line 90°: Success");
            AssertClose(90.0, r90.AngleDeg, TolDeg, "90°: AngleDeg");
            AssertClose(90.0, r90.AcuteAngleDeg, TolDeg, "90°: AcuteAngleDeg");

            // ── 2. line_to_horizontal ──
            var apH = AngleMeasurementParameters.Default();
            apH.Mode = "line_to_horizontal";
            AngleMeasurementResult rH = measurer.MeasureAngle(
                0, 0, 100, 0,    // vertical line...
                0, 0, 0, 0,      // ignored
                apH);
            Assert(rH.Success, "line_to_horizontal: Success");
            // Vertical line angle to horizontal = 90° (or close)
            AssertClose(90.0, rH.AngleDeg, TolDeg, "line_to_horizontal: ~90°");

            // ── 3. line_to_vertical ──
            var apV = AngleMeasurementParameters.Default();
            apV.Mode = "line_to_vertical";
            AngleMeasurementResult rV = measurer.MeasureAngle(
                0, 0, 0, 100,    // horizontal line
                0, 0, 0, 0,      // ignored
                apV);
            Assert(rV.Success, "line_to_vertical: Success");
            AssertClose(90.0, rV.AngleDeg, TolDeg, "line_to_vertical: ~90°");

            // ── 4. Near-parallel warning ──
            var apNp = AngleMeasurementParameters.Default();
            apNp.Mode = "line_to_line";
            apNp.NearParallelWarningDeg = 5.0;
            AngleMeasurementResult rNp = measurer.MeasureAngle(
                0, 0, 100, 0,
                0, 2, 100, 2,    // nearly parallel
                apNp);
            Assert(rNp.Success, "near-parallel: Success");
            Assert(rNp.IsNearParallel, "near-parallel: IsNearParallel=true");

            // ── 5. Endpoints too close (line 1) ──
            var apSep = AngleMeasurementParameters.Default();
            apSep.Mode = "line_to_line";
            apSep.MinPointSeparation = 10.0;
            AngleMeasurementResult rClose1 = measurer.MeasureAngle(
                0, 0, 1, 0,      // too short
                0, 0, 0, 100,
                apSep);
            Assert(!rClose1.Success, "endpoints too close (L1): !Success");

            // ── 6. Endpoints too close (line 2) in line_to_line mode ──
            AngleMeasurementResult rClose2 = measurer.MeasureAngle(
                0, 0, 100, 0,
                0, 0, 0, 1,      // too short
                apSep);
            Assert(!rClose2.Success, "endpoints too close (L2): !Success");

            // ── 7. Unsupported mode ──
            var apBad = AngleMeasurementParameters.Default();
            apBad.Mode = "line_to_diagonal";
            AngleMeasurementResult rBad = measurer.MeasureAngle(
                0, 0, 100, 0, 0, 0, 0, 100, apBad);
            Assert(!rBad.Success, "unsupported mode: !Success");

            Console.WriteLine("AngleMeasurerTests passed");
        }

        private static void Assert(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
        private static void AssertClose(double exp, double act, double tol, string msg)
        {
            if (Math.Abs(exp - act) > tol)
                throw new InvalidOperationException(msg + " exp=" + exp + " act=" + act);
        }
    }
}
