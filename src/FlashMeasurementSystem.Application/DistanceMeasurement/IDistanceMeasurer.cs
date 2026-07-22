using FlashMeasurementSystem.Domain.DistanceMeasurement;

namespace FlashMeasurementSystem.Application.DistanceMeasurement
{
    public interface IDistanceMeasurer
    {
        DistanceMeasurementResult MeasurePointToPoint(
            double row1, double col1,
            double row2, double col2,
            DistanceMeasurementParameters parameters);

        DistanceMeasurementResult MeasurePointToLine(
            double pointRow, double pointCol,
            double lineRow1, double lineCol1,
            double lineRow2, double lineCol2,
            DistanceMeasurementParameters parameters);

        DistanceMeasurementResult MeasureLineToLine(
            double line1Row1, double line1Col1,
            double line1Row2, double line1Col2,
            double line2Row1, double line2Col1,
            double line2Row2, double line2Col2,
            DistanceMeasurementParameters parameters);

        DistanceMeasurementResult MeasureCircleToCircle(
            double circle1Row, double circle1Col,
            double circle2Row, double circle2Col,
            DistanceMeasurementParameters parameters);
    }
}
