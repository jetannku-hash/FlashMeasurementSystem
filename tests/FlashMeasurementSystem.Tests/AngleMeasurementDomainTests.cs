using System;
using FlashMeasurementSystem.Application.AngleMeasurement;
using FlashMeasurementSystem.Domain.AngleMeasurement;

namespace FlashMeasurementSystem.Tests
{
    public static class AngleMeasurementDomainTests
    {
        public static void Run()
        {
            AngleMeasurementParameters parameters = AngleMeasurementParameters.Default();

            AssertEqual("line_to_line", parameters.Mode, "Default Mode");
            AssertEqual(2.0, parameters.NearParallelWarningDeg, "Default NearParallelWarningDeg");
            AssertEqual(1.0, parameters.MinPointSeparation, "Default MinPointSeparation");

            if (!AngleMeasurementParameters.IsSupportedMode("line_to_line"))
                throw new InvalidOperationException("line_to_line should be supported");
            if (!AngleMeasurementParameters.IsSupportedMode("line_to_horizontal"))
                throw new InvalidOperationException("line_to_horizontal should be supported");
            if (!AngleMeasurementParameters.IsSupportedMode("line_to_vertical"))
                throw new InvalidOperationException("line_to_vertical should be supported");
            if (AngleMeasurementParameters.IsSupportedMode("line_to_diagonal"))
                throw new InvalidOperationException("line_to_diagonal should not be supported");

            AngleMeasurementResult result = new AngleMeasurementResult();
            AssertEqual(false, result.Success, "Default Success");
            AssertEqual(0.0, result.AngleDeg, "Default AngleDeg");
            AssertEqual(0.0, result.AngleRad, "Default AngleRad");
            AssertEqual(0.0, result.AcuteAngleDeg, "Default AcuteAngleDeg");
            AssertEqual(0.0, result.RawAngleDeg, "Default RawAngleDeg");
            AssertEqual(0.0, result.RefAngle1Deg, "Default RefAngle1Deg");
            AssertEqual(0.0, result.RefAngle2Deg, "Default RefAngle2Deg");
            AssertEqual(false, result.IsNearParallel, "Default IsNearParallel");
            AssertEqual(string.Empty, result.ErrorMessage, "Default ErrorMessage");

            IAngleMeasurer measurer = new FakeAngleMeasurer();
            AngleMeasurementResult fake = measurer.MeasureAngle(0, 0, 0, 10, 0, 0, 10, 0, parameters);
            AssertEqual(true, fake.Success, "Fake angle measurer should satisfy interface contract");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
            }
        }

        private sealed class FakeAngleMeasurer : IAngleMeasurer
        {
            public AngleMeasurementResult MeasureAngle(
                double line1Row1, double line1Col1, double line1Row2, double line1Col2,
                double line2Row1, double line2Col1, double line2Row2, double line2Col2,
                AngleMeasurementParameters parameters)
            {
                return new AngleMeasurementResult { Success = true };
            }
        }
    }
}
