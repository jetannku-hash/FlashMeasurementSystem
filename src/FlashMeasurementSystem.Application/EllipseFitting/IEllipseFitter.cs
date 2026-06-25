using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EllipseFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Application.EllipseFitting
{
    public interface IEllipseFitter
    {
        EllipseFittingResult FitEllipse(IList<EdgePoint> edgePoints, EllipseFittingParameters parameters);
    }
}
