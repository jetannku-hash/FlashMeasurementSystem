using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Application.EdgeDetection
{
    public interface IEdgeDetector<TImage>
    {
        EdgeResult DetectEdges(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters);
        EdgeResult DetectEdgesSubPix(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters);
    }
}
