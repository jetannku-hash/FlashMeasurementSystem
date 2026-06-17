using System;
using FlashMeasurementSystem.Application.DistanceMeasurement;
using FlashMeasurementSystem.Domain.DistanceMeasurement;

namespace FlashMeasurementSystem.Tests
{
    public static class DistanceMeasurementDomainTests
    {
        public static void Run()
        {
            DistanceMeasurementParameters defaults = DistanceMeasurementParameters.Default();
            AssertEqual(DistanceMeasurementType.PointToPoint, defaults.Type, "Default type");
            AssertEqual(10.0, defaults.PixelSizeUmX, "Default pixelSizeUmX");
            AssertEqual(10.0, defaults.PixelSizeUmY, "Default pixelSizeUmY");
            AssertEqual("point_to_point", defaults.ContourMode, "Default contour mode");

            var result = new DistanceMeasurementResult();
            AssertEqual(false, result.Success, "New result should not default to success");
            AssertEqual(0.0, result.DistancePx, "New result distance px should be 0");
            AssertEqual(0.0, result.DistanceMm, "New result distance mm should be 0");
            AssertEqual(string.Empty, result.ErrorMessage, "New result error message should be empty");

            IDistanceMeasurer<object> fake = new FakeDistanceMeasurer();
            DistanceMeasurementResult fakeResult = fake.MeasurePointToPoint(0, 0, 1, 1, defaults);
            AssertEqual(true, fakeResult.Success, "Fake measurer should satisfy interface contract");

            Console.WriteLine("DistanceMeasurementDomainTests passed");
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(string.Format(
                    "{0}: expected {1}, actual {2}", message, expected, actual));
            }
        }

        private sealed class FakeDistanceMeasurer : IDistanceMeasurer<object>
        {
            public DistanceMeasurementResult MeasurePointToPoint(
                double row1, double col1, double row2, double col2,
                DistanceMeasurementParameters parameters)
            {
                return new DistanceMeasurementResult { Success = true };
            }

            public DistanceMeasurementResult MeasurePointToLine(
                double pointRow, double pointCol,
                double lineRow1, double lineCol1,
                double lineRow2, double lineCol2,
                DistanceMeasurementParameters parameters)
            {
                return new DistanceMeasurementResult { Success = true };
            }

            public DistanceMeasurementResult MeasureLineToLine(
                double line1Row1, double line1Col1,
                double line1Row2, double line1Col2,
                double line2Row1, double line2Col1,
                double line2Row2, double line2Col2,
                DistanceMeasurementParameters parameters)
            {
                return new DistanceMeasurementResult { Success = true };
            }

            public DistanceMeasurementResult MeasureCircleToCircle(
                double circle1Row, double circle1Col,
                double circle2Row, double circle2Col,
                DistanceMeasurementParameters parameters)
            {
                return new DistanceMeasurementResult { Success = true };
            }

            public DistanceMeasurementResult MeasureContourMaxMin(
                object contour1, object contour2,
                DistanceMeasurementParameters parameters)
            {
                return new DistanceMeasurementResult { Success = true };
            }
        }
    }
}
