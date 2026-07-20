using System;
using System.Linq;
using FlashMeasurementSystem.Domain.HoleArrayAnalysis;
using FlashMeasurementSystem.Domain.HoleArrayDetection;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Halcon.HoleArrayDetection;
using HalconDotNet;

namespace FlashMeasurementSystem.Tests.Halcon
{
    /// <summary>
    /// HOLE-DET：在背光合成圖（亮背景暗孔）上「真的跑」HalconHoleArrayDetector，驗證極性 + blob 偵測 + 等效孔徑。
    /// 正向：HoleIsDark=true 取 rows*cols 個暗孔，質心對得上網格節點（±2px）、DiameterPx ≈ 2*holeRadius（±2px）。
    /// 反向：同圖 HoleIsDark=false → 不會取到那 rows*cols 個孔（證明極性有效）。
    /// 缺孔：missingIndex 版本 → 取到 rows*cols-1 個。
    /// </summary>
    public static class HoleArrayDetectorHalconTests
    {
        // 合成圖真值
        private const int Width = 800, Height = 600;
        private const int Row0 = 150, Col0 = 150, PitchY = 100, PitchX = 120;
        private const int Rows = 4, Cols = 5, HoleRadius = 18;

        public static void Run()
        {
            RoiGeometry roi = BuildRoi();
            var detector = new HalconHoleArrayDetector();
            int expectedCount = Rows * Cols;

            using (HImage img = TestImageGenerator.CreateHoleGridImage(Width, Height, Row0, Col0, PitchY, PitchX, Rows, Cols, HoleRadius))
            {
                // 正向：預設 HoleIsDark=true → 取暗孔
                HoleArrayDetectionResult pos = detector.DetectHolesInRect(img, roi, new HoleArrayAnalysisParameters());
                Assert(pos.Success, "positive Success (" + pos.ErrorMessage + ")");
                Assert(pos.Holes.Count == expectedCount,
                    "positive detects " + expectedCount + " holes (polarity/detection real-run check; got " + pos.Holes.Count + ")");

                double expectedDiameter = 2.0 * HoleRadius;
                foreach (HoleArrayPoint h in pos.Holes)
                {
                    // 每個質心都要對得上某個理想網格節點
                    bool matched = false;
                    for (int r = 0; r < Rows && !matched; r++)
                        for (int c = 0; c < Cols && !matched; c++)
                            matched = Math.Abs(h.Row - (Row0 + r * PitchY)) < 2.0
                                   && Math.Abs(h.Col - (Col0 + c * PitchX)) < 2.0;
                    Assert(matched, "hole centroid (" + h.Row.ToString("F2") + "," + h.Col.ToString("F2") + ") matches a grid node");
                    Assert(Math.Abs(h.DiameterPx - expectedDiameter) < 2.0,
                        "hole DiameterPx ~" + expectedDiameter + " (equivalent-diameter formula check; got " + h.DiameterPx.ToString("F2") + ")");
                }

                // 觀測值輸出（證明真的跑過）
                var byPos = pos.Holes.OrderBy(h => h.Row).ThenBy(h => h.Col).ToList();
                Console.WriteLine("  HOLE-DET: count=" + byPos.Count
                    + " first=(" + byPos[0].Row.ToString("F2") + "," + byPos[0].Col.ToString("F2") + ",d=" + byPos[0].DiameterPx.ToString("F2") + ")"
                    + " last=(" + byPos[byPos.Count - 1].Row.ToString("F2") + "," + byPos[byPos.Count - 1].Col.ToString("F2") + ",d=" + byPos[byPos.Count - 1].DiameterPx.ToString("F2") + ")"
                    + " expected first=(" + Row0 + "," + Col0 + ",d=" + (2 * HoleRadius) + ")"
                    + " last=(" + (Row0 + (Rows - 1) * PitchY) + "," + (Col0 + (Cols - 1) * PitchX) + ",d=" + (2 * HoleRadius) + ")");

                // 反向：同一背光圖上 HoleIsDark=false → 取亮背景，不該回傳那 rows*cols 個孔（極性重要）。
                var negParams = new HoleArrayAnalysisParameters { HoleIsDark = false };
                HoleArrayDetectionResult neg = detector.DetectHolesInRect(img, roi, negParams);
                Assert(neg.Holes.Count != expectedCount,
                    "negative polarity does NOT return " + expectedCount + " holes (got " + neg.Holes.Count + ")");
            }

            // 缺孔：略過 flat index 7（內部孔）→ 應偵到 rows*cols-1
            using (HImage missImg = TestImageGenerator.CreateHoleGridImage(Width, Height, Row0, Col0, PitchY, PitchX, Rows, Cols, HoleRadius, 7))
            {
                HoleArrayDetectionResult det = detector.DetectHolesInRect(missImg, roi, new HoleArrayAnalysisParameters());
                Assert(det.Success, "missing-hole Success (" + det.ErrorMessage + ")");
                Assert(det.Holes.Count == expectedCount - 1,
                    "missing-hole detects " + (expectedCount - 1) + " (got " + det.Holes.Count + ")");
            }

            Console.WriteLine("HoleArrayDetectorHalconTests passed");
        }

        // ROI 覆蓋整個網格：中心在網格中點，Length1 沿 col、Length2 沿 row（AngleRad=0），各加孔半徑+margin。
        private static RoiGeometry BuildRoi()
        {
            double lastRow = Row0 + (Rows - 1) * PitchY;
            double lastCol = Col0 + (Cols - 1) * PitchX;
            return new RoiGeometry
            {
                CenterRow = (Row0 + lastRow) / 2.0,
                CenterCol = (Col0 + lastCol) / 2.0,
                AngleRad = 0.0,
                Length1 = (lastCol - Col0) / 2.0 + HoleRadius + 15,
                Length2 = (lastRow - Row0) / 2.0 + HoleRadius + 15
            };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("HoleArrayDetectorHalconTests: " + message);
        }
    }
}
