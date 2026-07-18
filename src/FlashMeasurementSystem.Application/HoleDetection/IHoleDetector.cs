using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.HoleDetection;
using FlashMeasurementSystem.Domain.PcdAnalysis;

namespace FlashMeasurementSystem.Application.HoleDetection
{
    /// <summary>環狀 ROI 內以 blob 偵測孔並回傳質心。TImage 由 Halcon adapter 綁定 HImage。</summary>
    public interface IHoleDetector<TImage>
    {
        HoleDetectionResult DetectHolesInAnnulus(TImage image, ArcMeasureRoi placedArc, PcdAnalysisParameters parameters);
    }
}
