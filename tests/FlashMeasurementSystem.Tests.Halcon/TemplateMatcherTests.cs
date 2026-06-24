using System;
using System.IO;
using FlashMeasurementSystem.Domain.TemplateMatching;
using FlashMeasurementSystem.Halcon.TemplateMatching;
using HImage = HalconDotNet.HImage;
using HRegion = HalconDotNet.HRegion;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class TemplateMatcherTests
    {
        public static void Run()
        {
            // ── 1. No model loaded → Found=false ──
            using (var matcher = new HalconTemplateMatcher())
            using (HImage img = TestImageGenerator.CreateTemplateShapesImage())
            {
                TemplateMatchResult rNoModel = matcher.FindMatches(img, null, TemplateMatchingParameters.Default());
                Console.WriteLine("  no model: Found=" + rNoModel.Found + " msg=" + rNoModel.Message);
                Assert(!rNoModel.Found, "no model: !Found");
            }

            // ── 2. Create model → match on same image → Found=true ──
            string modelPath = Path.Combine(Path.GetTempPath(), "test_shape_" + Guid.NewGuid().ToString("N") + ".shm");
            try
            {
                using (HImage img = TestImageGenerator.CreateTemplateShapesImage())
                {
                    // Region around the white rectangle (140x100 at center of 256x256)
                    HRegion tmplRegion = new HRegion();
                    tmplRegion.GenRectangle1(78.0, 58.0, 178.0, 198.0);

                    var creator = new HalconTemplateManager();
                    creator.CreateAndSave(img, tmplRegion, modelPath, TemplateCreationParameters.Default());
                    tmplRegion.Dispose();
                }

                // Load + match on same image
                using (var matcher = new HalconTemplateMatcher())
                using (HImage img = TestImageGenerator.CreateTemplateShapesImage())
                {
                    matcher.LoadModel(modelPath);
                    TemplateMatchResult r = matcher.FindMatches(img, null, TemplateMatchingParameters.Default());
                    Console.WriteLine("  same image: Found=" + r.Found + " Score=" + r.Score.ToString("F4")
                        + " Row=" + r.Row.ToString("F1") + " Col=" + r.Column.ToString("F1"));
                    Assert(r.Found, "same image: Found=true, msg=" + r.Message);
                    Assert(r.Score > 0.7, "same image: Score > 0.7, got=" + r.Score.ToString("F4"));
                }

                // ── 3. Match on different image → low score ──
                using (var matcher = new HalconTemplateMatcher())
                using (HImage other = TestImageGenerator.CreateNonMatchingImage())
                {
                    matcher.LoadModel(modelPath);
                    TemplateMatchResult r = matcher.FindMatches(other, null, TemplateMatchingParameters.Default());
                    Console.WriteLine("  diff image: Found=" + r.Found + " Score=" + r.Score.ToString("F4"));
                }

                // ── 4. GetMatchContour without model → throws ──
                using (var matcher = new HalconTemplateMatcher())
                {
                    try
                    {
                        matcher.GetMatchContour(0, 0, 0);
                        throw new InvalidOperationException("GetMatchContour should throw without loaded model");
                    }
                    catch (InvalidOperationException) { /* expected */ }
                }
            }
            finally
            {
                if (File.Exists(modelPath))
                {
                    try { File.Delete(modelPath); } catch { }
                }
            }

            Console.WriteLine("TemplateMatcherTests passed");
        }

        private static void Assert(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
    }
}
