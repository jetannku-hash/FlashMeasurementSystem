using System.Collections.Generic;
using FlashMeasurementSystem.Domain.RectangleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Application.RectangleFitting
{
    public interface IRectangleFitter
    {
        RectangleFittingResult FitRectangle(IList<EdgePoint> edgePoints, RectangleFittingParameters parameters);
    }
}
