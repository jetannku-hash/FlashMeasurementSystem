using FlashMeasurementSystem.Domain.HoleArrayAnalysis;
using FlashMeasurementSystem.Domain.HoleArrayDetection;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Application.HoleArrayDetection
{
    /// <summary>矩形 ROI 內以 blob 偵測孔洞並回傳質心與等效孔徑。TImage 由 Halcon adapter 綁定 HImage。</summary>
    public interface IHoleArrayDetector<TImage>
    {
        HoleArrayDetectionResult DetectHolesInRect(TImage image, RoiGeometry placedRoi, HoleArrayAnalysisParameters parameters);
    }
}
