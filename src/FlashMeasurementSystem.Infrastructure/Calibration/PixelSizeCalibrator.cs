using System;
using FlashMeasurementSystem.Application.Calibration;
using FlashMeasurementSystem.Domain.Calibration;

namespace FlashMeasurementSystem.Infrastructure.Calibration
{
    /// <summary>
    /// 簡易等向 pixel-size 校正（純數學，不依賴 HALCON）。
    /// pixelSizeUm = knownDistanceMm * 1000 / 像素間距。
    /// 注意：簡易版預設 X = Y（等向），不做手冊的「斜向 pixelSize/cosAngle」拆解
    /// （語意可疑）。需 X/Y 各別校正時，應以兩條正交標準件各校一次。
    /// </summary>
    public class PixelSizeCalibrator : ICalibrator
    {
        public CalibrationProfile CalibrateTwoPoint(
            string profileId,
            double knownDistanceMm,
            double p1Row, double p1Col,
            double p2Row, double p2Col,
            int imageWidth, int imageHeight)
        {
            double dRow = p2Row - p1Row;
            double dCol = p2Col - p1Col;
            double distPx = Math.Sqrt(dRow * dRow + dCol * dCol);

            if (distPx < 1e-9)
            {
                throw new ArgumentException("兩校正點重合，無法計算 pixel size");
            }
            if (knownDistanceMm <= 0.0)
            {
                throw new ArgumentException("已知距離必須為正值", "knownDistanceMm");
            }

            double pixelSizeUm = knownDistanceMm * 1000.0 / distPx;

            return new CalibrationProfile
            {
                ProfileId = profileId ?? "",
                PixelSizeUmX = pixelSizeUm,
                PixelSizeUmY = pixelSizeUm,   // 等向（簡易版）
                CalibrationStandardMm = knownDistanceMm,
                MeasuredPixels = distPx,
                FieldOfViewMmX = imageWidth * pixelSizeUm / 1000.0,
                FieldOfViewMmY = imageHeight * pixelSizeUm / 1000.0,
                DistortionCorrected = false,
                CreatedAt = DateTime.Now
            };
        }
    }
}
