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

        // annular-sector ROI：edges_sub_pix 限定在扇形環帶內（gen_circle_sector 差集），
        // 供下游 fitter 在楔形 ROI 內擬合邊。與 DetectEdgesOnArc 共用 ArcMeasureRoi，
        // 但這裡回傳整個環帶內的亞像素邊緣點（非沿弧掃描的量測線邊）。
        EdgeResult DetectEdgesInAnnularSector(TImage image, ArcMeasureRoi roi, EdgeDetectionParameters parameters);
    }
}
