using FlashMeasurementSystem.Domain.AngleMeasurement;

namespace FlashMeasurementSystem.Application.AngleMeasurement
{
    public interface IAngleMeasurer
    {
        AngleMeasurementResult MeasureAngle(
            double line1Row1, double line1Col1, double line1Row2, double line1Col2,
            double line2Row1, double line2Col1, double line2Row2, double line2Col2,
            AngleMeasurementParameters parameters);
    }
}
