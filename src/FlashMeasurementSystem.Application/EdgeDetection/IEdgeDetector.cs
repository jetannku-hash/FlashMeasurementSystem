using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Application.EdgeDetection
{
    public interface IEdgeDetector<TImage>
    {
        EdgeResult DetectEdges(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters);
        EdgeResult DetectEdgesSubPix(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters);
        // v7：弧形卡尺（gen_measure_arc）。HalconEdgeDetector 早已實作同簽章；
        // 加進介面讓 RecipeRunner（只持有介面）能執行弧形工具。ArcMeasureRoi 屬 Domain，介面維持 HALCON-free。
        EdgeResult DetectEdgesOnArc(TImage image, ArcMeasureRoi arcRoi, EdgeDetectionParameters parameters);
    }
}
