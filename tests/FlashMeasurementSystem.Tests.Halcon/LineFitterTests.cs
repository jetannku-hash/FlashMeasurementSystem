using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.LineFitting;
using FlashMeasurementSystem.Halcon.LineFitting;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class LineFitterTests
    {
        public static void Run()
        {
            var fitter = new HalconLineFitter();

            // ── 1. Enough edge points → Success ──
            var pts = GenerateLinePoints(64, 128, 192, 128, 30);
            LineFittingResult r = fitter.FitLine(pts, LineFittingParameters.Default());
            Console.WriteLine("  vertical line: Success=" + r.Success + " Row1=" + r.Row1.ToString("F1")
                + " Col1=" + r.Column1.ToString("F1") + " Row2=" + r.Row2.ToString("F1")
                + " Col2=" + r.Column2.ToString("F1") + " RMS=" + r.ResidualRms.ToString("F4"));
            Assert(r.Success, "vertical: Success (err=" + r.ErrorMessage + ")");
            // Fitted vertical line should have Col≈128 on both endpoints
            Assert(Math.Abs(r.Column1 - 128.0) < 5.0, "vertical: Col1~128, got=" + r.Column1.ToString("F1"));
            Assert(Math.Abs(r.Column2 - 128.0) < 5.0, "vertical: Col2~128, got=" + r.Column2.ToString("F1"));

            // ── 2. Too few points → fail ──
            var fewPts = new List<EdgePoint> { new EdgePoint { Row = 0, Column = 0 } };
            LineFittingResult rFew = fitter.FitLine(fewPts, LineFittingParameters.Default());
            Assert(!rFew.Success, "too few points: !Success");

            // ── 3. null points → fail ──
            LineFittingResult rNull = fitter.FitLine(null, LineFittingParameters.Default());
            Assert(!rNull.Success, "null points: !Success");

            // ── 4. Unsupported algorithm ──
            var badParams = LineFittingParameters.Default();
            badParams.Algorithm = "bogus_algo";
            LineFittingResult rBadAlgo = fitter.FitLine(pts, badParams);
            Assert(!rBadAlgo.Success, "unsupported algorithm: !Success");

            // ── 5. Horizontal line ──
            var hPts = GenerateLinePoints(64, 64, 64, 192, 30);
            LineFittingResult rH = fitter.FitLine(hPts, LineFittingParameters.Default());
            Console.WriteLine("  horizontal: Success=" + rH.Success + " Row1=" + rH.Row1.ToString("F1")
                + " Col1=" + rH.Column1.ToString("F1") + " Col2=" + rH.Column2.ToString("F1"));
            Assert(rH.Success, "horizontal: Success (err=" + rH.ErrorMessage + ")");
            Assert(Math.Abs(rH.Row1 - 64.0) < 5.0, "horizontal: Row~64, got=" + rH.Row1.ToString("F1"));

            // ── 6. Residual RMS small for collinear points ──
            Assert(rH.ResidualRms < 2.0, "collinear: RMS < 2px, got=" + rH.ResidualRms.ToString("F4"));

            Console.WriteLine("LineFitterTests passed");
        }

        private static List<EdgePoint> GenerateLinePoints(double r1, double c1, double r2, double c2, int n)
        {
            var list = new List<EdgePoint>();
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / (n - 1);
                list.Add(new EdgePoint
                {
                    Row = r1 + (r2 - r1) * t,
                    Column = c1 + (c2 - c1) * t,
                    Amplitude = 50.0
                });
            }
            return list;
        }

        private static void Assert(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
    }
}
