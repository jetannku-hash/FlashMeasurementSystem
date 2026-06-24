using System;
using FlashMeasurementSystem.Domain.ImageQuality;
using FlashMeasurementSystem.Halcon.ImageQuality;
using HImage = HalconDotNet.HImage;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class ImageQualityCheckerTests
    {
        public static void Run()
        {
            var checker = new HalconImageQualityChecker();

            // ── 1. Normal image → check metrics, log values ──
            using (HImage normal = TestImageGenerator.CreateEdgeImage())
            {
                ImageQualityResult r = checker.Check(normal, ImageQualityThresholds.Default());
                Console.WriteLine("  Normal: Pass=" + r.Pass + " mean=" + r.MeanBrightness.ToString("F1")
                    + " sat=" + r.SaturationRatio.ToString("F2")
                    + " blur=" + r.BlurScore.ToString("F1")
                    + " contrast=" + r.Contrast.ToString("F1")
                    + " msg=" + r.Message);
            }

            // ── 2. Too dark → !Pass ──
            using (HImage dark = TestImageGenerator.CreateUniform(128, 128, 30.0))
            {
                ImageQualityResult r = checker.Check(dark, ImageQualityThresholds.Default());
                Console.WriteLine("  Dark  : Pass=" + r.Pass + " mean=" + r.MeanBrightness.ToString("F1") + " msg=" + r.Message);
                // Dark image (mean 30 < MinBrightness 80) should fail
            }

            // ── 3. Too bright → !Pass ──
            using (HImage bright = TestImageGenerator.CreateUniform(128, 128, 230.0))
            {
                ImageQualityResult r = checker.Check(bright, ImageQualityThresholds.Default());
                Console.WriteLine("  Bright: Pass=" + r.Pass + " mean=" + r.MeanBrightness.ToString("F1") + " msg=" + r.Message);
            }

            // ── 4. Blurry → check blur score ──
            using (HImage blurry = TestImageGenerator.CreateBlurryImage())
            {
                ImageQualityResult r = checker.Check(blurry, ImageQualityThresholds.Default());
                Console.WriteLine("  Blurry: Pass=" + r.Pass + " blurScore=" + r.BlurScore.ToString("F1") + " msg=" + r.Message);
            }

            // ── 5. Low contrast → !Pass ──
            using (HImage lowC = TestImageGenerator.CreateLowContrastImage())
            {
                ImageQualityResult r = checker.Check(lowC, ImageQualityThresholds.Default());
                Console.WriteLine("  LowCtr: Pass=" + r.Pass + " contrast=" + r.Contrast.ToString("F1") + " msg=" + r.Message);
                Assert(!r.Pass, "low contrast: !Pass, msg=" + r.Message);
            }

            // ── 6. RGB → auto convert (should not crash) ──
            using (HImage rgb = TestImageGenerator.CreateRgbImage())
            {
                ImageQualityResult r = checker.Check(rgb, ImageQualityThresholds.Default());
                Console.WriteLine("  RGB   : Pass=" + r.Pass + " mean=" + r.MeanBrightness.ToString("F1") + " msg=" + r.Message);
            }

            // ── 7. null thresholds → use defaults ──
            using (HImage edge = TestImageGenerator.CreateEdgeImage())
            {
                ImageQualityResult r = checker.Check(edge, null);
                Console.WriteLine("  NullThr: Pass=" + r.Pass + " (null thresholds→defaults)");
            }

            Console.WriteLine("ImageQualityCheckerTests passed");
        }

        private static void Assert(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
    }
}
