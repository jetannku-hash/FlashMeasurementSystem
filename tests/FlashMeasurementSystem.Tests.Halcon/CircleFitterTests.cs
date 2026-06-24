using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Halcon.CircleFitting;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class CircleFitterTests
    {
        public static void Run()
        {
            var fitter = new HalconCircleFitter();

            // ── 1. Enough edge points → Success ──
            var pts = GenerateCirclePoints(128, 128, 50, 36);
            CircleFittingResult r = fitter.FitCircle(pts, CircleFittingParameters.Default());
            Assert(r.Success, "normal: Success");
            Assert(Math.Abs(r.CenterRow - 128.0) < 2.0, "normal: CenterRow ~128");
            Assert(Math.Abs(r.CenterColumn - 128.0) < 2.0, "normal: CenterCol ~128");
            Assert(Math.Abs(r.RadiusPx - 50.0) < 2.0, "normal: Radius ~50");
            Assert(r.DiameterPx > 0, "normal: DiameterPx > 0");

            // ── 2. Too few points → fail ──
            var fewPts = new List<EdgePoint>
            {
                new EdgePoint { Row = 0, Column = 0 },
                new EdgePoint { Row = 10, Column = 10 }
            };
            CircleFittingResult rFew = fitter.FitCircle(fewPts, CircleFittingParameters.Default());
            Assert(!rFew.Success, "too few points: !Success");

            // ── 3. null points → fail ──
            CircleFittingResult rNull = fitter.FitCircle(null, CircleFittingParameters.Default());
            Assert(!rNull.Success, "null points: !Success");

            // ── 4. Unsupported algorithm ──
            var badParams = CircleFittingParameters.Default();
            badParams.Algorithm = "bogus_algo";
            CircleFittingResult rBadAlgo = fitter.FitCircle(pts, badParams);
            Assert(!rBadAlgo.Success, "unsupported algorithm: !Success");

            // ── 5. Residual RMS → small for perfect circle points ──
            Assert(r.ResidualRms < 1.0, "perfect circle: RMS < 1px");

            // ── 6. Offset circle ──
            var offPts = GenerateCirclePoints(200, 60, 30, 24);
            CircleFittingResult rOff = fitter.FitCircle(offPts, CircleFittingParameters.Default());
            Assert(rOff.Success, "offset circle: Success");
            Assert(Math.Abs(rOff.CenterRow - 200.0) < 2.0, "offset: CenterRow ~200");
            Assert(Math.Abs(rOff.CenterColumn - 60.0) < 2.0, "offset: CenterCol ~60");
            Assert(Math.Abs(rOff.RadiusPx - 30.0) < 2.0, "offset: Radius ~30");

            Console.WriteLine("CircleFitterTests passed");
        }

        private static List<EdgePoint> GenerateCirclePoints(double cRow, double cCol, double radius, int n)
        {
            var list = new List<EdgePoint>();
            for (int i = 0; i < n; i++)
            {
                double angle = 2.0 * Math.PI * i / n;
                list.Add(new EdgePoint
                {
                    Row = cRow - radius * Math.Sin(angle),   // HALCON: Row=Y, sin with neg because Row↓
                    Column = cCol + radius * Math.Cos(angle),
                    Amplitude = 50.0
                });
            }
            return list;
        }

        private static void Assert(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
    }
}
