using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.LineFitting;

namespace FlashMeasurementSystem.Application.LineFitting
{
    public interface ILineFitter
    {
        LineFittingResult FitLine(IList<EdgePoint> edgePoints, LineFittingParameters parameters);
    }
}
