using System;
using System.Linq;
using FlashMeasurementSystem.Domain.PinDetection;
using FlashMeasurementSystem.Domain.PinPitchAnalysis;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Halcon.PinDetection;
using HalconDotNet;

namespace FlashMeasurementSystem.Tests.Halcon
{
    /// <summary>
    /// PIN-DET：在背光合成圖（亮背景暗引腳）上「真的跑」HalconPinDetector，驗證極性 + blob 偵測。
    /// 正向：PinIsDark=true 取 8 個暗引腳，質心（按 Col 排序）落在真值 ±2px。
    /// 反向：同圖 PinIsDark=false → 不會取到那 8 個引腳（證明極性有效）。
    /// </summary>
    public static class PinDetectorHalconTests
    {
        // 合成圖真值
        private const int Width = 800, Height = 400;
        private const int Row = 200, Col0 = 100, Pitch = 60, Count = 8;
        private const int PinHalfLen = 15, PinHalfWid = 8;

        public static void Run()
        {
            using (HImage img = TestImageGenerator.CreatePinRowImage(Width, Height, Row, Col0, Pitch, Count, PinHalfLen, PinHalfWid))
            {
                // ROI 覆蓋整排引腳：中心在引腳排中點，Length1 沿 col 跨整排+邊界，Length2 沿 row 覆蓋引腳高度。
                double firstCol = Col0;
                double lastCol = Col0 + (Count - 1) * Pitch;   // 520
                var roi = new RoiGeometry
                {
                    CenterRow = Row,
                    CenterCol = (firstCol + lastCol) / 2.0,       // 310
                    AngleRad = 0.0,
                    Length1 = (lastCol - firstCol) / 2.0 + PinHalfLen + 15,   // 半跨 + pin 半長 + margin
                    Length2 = PinHalfWid + 12                                  // 覆蓋引腳高度 + margin
                };

                var detector = new HalconPinDetector();

                // 正向：預設 PinIsDark=true → 取暗引腳
                PinDetectionResult pos = detector.DetectPinsInRect(img, roi, new PinPitchAnalysisParameters());
                Assert(pos.Success, "positive Success (" + pos.ErrorMessage + ")");
                Assert(pos.Pins.Count == Count,
                    "positive detects " + Count + " pins (polarity/detection real-run check; got " + pos.Pins.Count + ")");

                var sorted = pos.Pins.OrderBy(p => p.Col).ToList();
                for (int i = 0; i < Count; i++)
                {
                    double expCol = Col0 + i * Pitch;
                    Assert(Math.Abs(sorted[i].Col - expCol) < 2.0,
                        "pin[" + i + "] Col ~" + expCol + " (got " + sorted[i].Col.ToString("F2") + ")");
                    Assert(Math.Abs(sorted[i].Row - Row) < 2.0,
                        "pin[" + i + "] Row ~" + Row + " (got " + sorted[i].Row.ToString("F2") + ")");
                }

                // 反向：同一背光圖上 PinIsDark=false → 取亮背景/雜訊，不該回傳那 8 個引腳（極性重要）。
                var negParams = new PinPitchAnalysisParameters { PinIsDark = false };
                PinDetectionResult neg = detector.DetectPinsInRect(img, roi, negParams);
                Assert(neg.Pins.Count != Count,
                    "negative polarity does NOT return " + Count + " pins (got " + neg.Pins.Count + ")");
            }

            Console.WriteLine("PinDetectorHalconTests passed");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("PinDetectorHalconTests: " + message);
        }
    }
}
