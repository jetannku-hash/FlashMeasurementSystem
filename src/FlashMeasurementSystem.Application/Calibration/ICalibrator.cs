using FlashMeasurementSystem.Domain.Calibration;

namespace FlashMeasurementSystem.Application.Calibration
{
    /// <summary>
    /// 簡易像素尺寸校正：以標準件上「已知物理距離」與其「像素間距」換算 µm/px。
    /// 影像寬高以 int 傳入，使介面與 HALCON/HImage 解耦、可單元測試。
    /// </summary>
    public interface ICalibrator
    {
        CalibrationProfile CalibrateTwoPoint(
            string profileId,
            double knownDistanceMm,
            double p1Row, double p1Col,
            double p2Row, double p2Col,
            int imageWidth, int imageHeight);
    }
}
