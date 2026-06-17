using System.Collections.Generic;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Application.CircleFitting
{
    public interface ICircleFitter
    {
        CircleFittingResult FitCircle(IList<EdgePoint> edgePoints, CircleFittingParameters parameters);
    }
}
