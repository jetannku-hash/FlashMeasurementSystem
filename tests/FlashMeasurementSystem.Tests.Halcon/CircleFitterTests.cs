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

            // ── 7. Arc (180° half circle, 24 pts) → Success, IsClosed=false ──
            var arcPts = GenerateArcPoints(150, 150, 40, 0.0, Math.PI, 24);
            CircleFittingResult rArc = fitter.FitCircle(arcPts, CircleFittingParameters.Default());
            Assert(rArc.Success, "arc: Success");
            Assert(!rArc.IsClosed, "arc: IsClosed should be false (180deg != 360)");
            Assert(Math.Abs(rArc.CenterRow - 150.0) < 3.0, "arc: CenterRow ~150");
            Assert(Math.Abs(rArc.CenterColumn - 150.0) < 3.0, "arc: CenterCol ~150");
            Assert(Math.Abs(rArc.RadiusPx - 40.0) < 3.0, "arc: Radius ~40");

            // ── 8. Full circle (360°) → IsClosed=true ──
            // 真實整圓的 edges_sub_pix 輪廓是「閉合」的(末點回到首點)。
            // GenerateCirclePoints 產生開放點集(末點停在 ~350°)，fit_circle_contour_xld 會
            // 把它當 350° 弧 → IsClosed=false。故此處補上閉合點(末=首)模擬真實閉合輪廓，
            // fit_circle 才會回 StartPhi=0/EndPhi=2π(已用 raw 驗證)。
            var fullPts = GenerateCirclePoints(100, 100, 50, 36);
            fullPts.Add(new EdgePoint { Row = fullPts[0].Row, Column = fullPts[0].Column, Amplitude = 50.0 });
            CircleFittingResult rFull = fitter.FitCircle(fullPts, CircleFittingParameters.Default());
            Assert(rFull.Success, "full circle: Success");
            Assert(rFull.IsClosed, "full circle: IsClosed should be true");
            Assert(rFull.DiameterPx > 0, "full circle: DiameterPx > 0");

            // ── 9. 90° arc → IsClosed=false ──
            var arc90 = GenerateArcPoints(120, 80, 35, Math.PI / 4.0, Math.PI / 2.0, 16);
            CircleFittingResult r90 = fitter.FitCircle(arc90, CircleFittingParameters.Default());
            Assert(r90.Success, "90deg arc: Success");
            Assert(!r90.IsClosed, "90deg arc: IsClosed should be false");

            Console.WriteLine("CircleFitterTests passed");
        }

        private static List<EdgePoint> GenerateArcPoints(
            double cRow, double cCol, double radius, double startPhi, double extentRad, int n)
        {
            var list = new List<EdgePoint>();
            for (int i = 0; i < n; i++)
            {
                double angle = startPhi + extentRad * i / (n - 1);
                list.Add(new EdgePoint
                {
                    Row = cRow - radius * Math.Sin(angle),
                    Column = cCol + radius * Math.Cos(angle),
                    Amplitude = 50.0
                });
            }
            return list;
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
