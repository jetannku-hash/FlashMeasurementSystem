using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Halcon.EdgeDetection;
using HalconDotNet;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class EdgeDetectorTests
    {
        public static void Run()
        {
            var detector = new HalconEdgeDetector();

            // ── 1. null image ──
            EdgeResult eNullImg = detector.DetectEdges(null, new EdgeDetectionRoi(), EdgeDetectionParameters.Default());
            Assert(!eNullImg.Success, "null image: !Success");

            // ── 2. null / undefined ROI ──
            using (HImage img = TestImageGenerator.CreateEdgeImage())
            {
                EdgeResult eNullRoi = detector.DetectEdges(img, null, EdgeDetectionParameters.Default());
                Assert(!eNullRoi.Success, "null ROI: !Success");

                EdgeResult eBadRoi = detector.DetectEdges(img, new EdgeDetectionRoi(), EdgeDetectionParameters.Default());
                Assert(!eBadRoi.Success, "undefined ROI: !Success");
            }

            // ── 3. DetectEdges: vertical edge at col=128, Phi=π/2 (major axis vertical → scan horizontal) ──
            using (HImage edge = TestImageGenerator.CreateEdgeImage())
            {
                var roi = EdgeDetectionRoi.FromCenter(128, 128, 100, 20, Math.PI / 2);
                var ep = EdgeDetectionParameters.Default();
                ep.Threshold = 10.0;
                EdgeResult eOk = detector.DetectEdges(edge, roi, ep);
                Console.WriteLine("  DetectEdges: Success=" + eOk.Success + " cnt=" + eOk.EdgePoints.Count);
                if (!eOk.Success) Console.WriteLine("    msg=" + eOk.ErrorMessage);
                Assert(eOk.Success, "DetectEdges: Success");
                Assert(eOk.EdgePoints.Count > 0, "DetectEdges: got edges");
            }

            // ── 4. DetectEdgesSubPix ──
            using (HImage edge = TestImageGenerator.CreateEdgeImage())
            {
                var roi = EdgeDetectionRoi.FromCenter(128, 128, 100, 20, Math.PI / 2);
                var @params = EdgeDetectionParameters.Default();
                @params.Threshold = 10.0;
                EdgeResult eSub = detector.DetectEdgesSubPix(edge, roi, @params);
                Console.WriteLine("  SubPix: Success=" + eSub.Success + " cnt=" + eSub.EdgePoints.Count);
                if (!eSub.Success) Console.WriteLine("    msg=" + eSub.ErrorMessage);
                Assert(eSub.Success, "DetectEdgesSubPix: Success");
                Assert(eSub.EdgePoints.Count > 0, "DetectEdgesSubPix: got edges");
            }

            // ── 5. Unsupported interpolation ──
            using (HImage edge = TestImageGenerator.CreateEdgeImage())
            {
                var roi = EdgeDetectionRoi.FromCenter(128, 128, 100, 20, Math.PI / 2);
                var badP = EdgeDetectionParameters.Default();
                badP.Interpolation = "cubic";
                EdgeResult eBadInterp = detector.DetectEdges(edge, roi, badP);
                Assert(!eBadInterp.Success, "unsupported interpolation: !Success");
            }

            // ── 6. Unsupported measure mode ──
            using (HImage edge = TestImageGenerator.CreateEdgeImage())
            {
                var roi = EdgeDetectionRoi.FromCenter(128, 128, 100, 20, Math.PI / 2);
                var badP = EdgeDetectionParameters.Default();
                badP.MeasureMode = "multi_pair";
                EdgeResult eBadMode = detector.DetectEdges(edge, roi, badP);
                Assert(!eBadMode.Success, "unsupported measure mode: !Success");
            }

            // ── 7. ROI outside image ──
            using (HImage edge = TestImageGenerator.CreateEdgeImage(256, 256))
            {
                var outsideRoi = EdgeDetectionRoi.FromCenter(0, 0, 5, 5, 0);
                EdgeResult eOut = detector.DetectEdges(edge, outsideRoi, EdgeDetectionParameters.Default());
                Console.WriteLine("  ROI at edge: Success=" + eOut.Success + " msg=" + eOut.ErrorMessage);
            }

            // ── 8. RGB → auto convert ──
            using (HImage rgb = TestImageGenerator.CreateRgbImage())
            {
                var roi = EdgeDetectionRoi.FromCenter(128, 128, 100, 20, Math.PI / 2);
                var ep = EdgeDetectionParameters.Default();
                ep.Threshold = 10.0;
                EdgeResult eRgb = detector.DetectEdges(rgb, roi, ep);
                Console.WriteLine("  RGB: Success=" + eRgb.Success + " cnt=" + eRgb.EdgePoints.Count);
            }

            // ── 9. Arc caliper: null image ──
            EdgeResult arcNullImg = detector.DetectEdgesOnArc(null, new ArcMeasureRoi(), EdgeDetectionParameters.Default());
            Assert(!arcNullImg.Success, "arc null image: !Success");

            // ── 10. Arc caliper: null/invalid ROI ──
            using (HImage edge = TestImageGenerator.CreateEdgeImage())
            {
                EdgeResult arcNullRoi = detector.DetectEdgesOnArc(edge, null, EdgeDetectionParameters.Default());
                Assert(!arcNullRoi.Success, "arc null ROI: !Success");

                EdgeResult arcBad = detector.DetectEdgesOnArc(edge, new ArcMeasureRoi(), EdgeDetectionParameters.Default());
                Assert(!arcBad.Success, "arc undefined ROI: !Success");
            }

            // ── 11. Arc caliper: valid arc over edge image (horizontal sweep, 90°) ──
            using (HImage edge = TestImageGenerator.CreateEdgeImage())
            {
                var arcRoi = new ArcMeasureRoi
                {
                    CenterRow = 128.0,
                    CenterCol = 80.0,
                    Radius = 50.0,
                    AngleStart = -Math.PI / 4.0,
                    AngleExtent = Math.PI / 2.0,
                    AnnulusRadius = 15.0
                };
                var ep = EdgeDetectionParameters.Default();
                ep.Threshold = 10.0;
                EdgeResult arcOk = detector.DetectEdgesOnArc(edge, arcRoi, ep);
                Console.WriteLine("  Arc caliper: Success=" + arcOk.Success + " cnt=" + arcOk.EdgePoints.Count);
                if (!arcOk.Success) Console.WriteLine("    msg=" + arcOk.ErrorMessage);
                // 弧跨過 vertical edge at col=128 時應該抓到邊點。
                Assert(arcOk.Success, "arc caliper: Success");
                Assert(arcOk.EdgePoints.Count > 0, "arc caliper: got edges");
            }

            Console.WriteLine("EdgeDetectorTests passed");
        }

        private static void Assert(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
    }
}
