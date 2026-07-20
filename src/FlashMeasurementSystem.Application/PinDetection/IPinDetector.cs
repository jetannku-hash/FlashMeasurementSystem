using FlashMeasurementSystem.Domain.PinDetection;
using FlashMeasurementSystem.Domain.PinPitchAnalysis;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Application.PinDetection
{
    /// <summary>矩形 ROI 內以 blob 偵測引腳並回傳質心。TImage 由 Halcon adapter 綁定 HImage。</summary>
    public interface IPinDetector<TImage>
    {
        PinDetectionResult DetectPinsInRect(TImage image, RoiGeometry placedRoi, PinPitchAnalysisParameters parameters);
    }
}
