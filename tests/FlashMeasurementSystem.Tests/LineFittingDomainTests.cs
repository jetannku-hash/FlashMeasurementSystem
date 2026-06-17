using System;
using FlashMeasurementSystem.Domain.LineFitting;

namespace FlashMeasurementSystem.Tests
{
    public static class LineFittingDomainTests
    {
        public static void Run()
        {
            LineFittingParameters parameters = LineFittingParameters.Default();

            AssertEqual("tukey", parameters.Algorithm, "Default algorithm");
            AssertEqual(-1, parameters.MaxNumPoints, "Default MaxNumPoints");
            AssertEqual(0, parameters.ClippingEndPoints, "Default ClippingEndPoints");
            AssertEqual(2.0, parameters.ClippingFactor, "Default ClippingFactor");
            AssertEqual(3, parameters.Iterations, "Default Iterations");
            AssertEqual(2, parameters.MinPoints, "Default MinPoints");

            if (!LineFittingParameters.IsSupportedAlgorithm("regression"))
                throw new InvalidOperationException("regression should be supported");
            if (!LineFittingParameters.IsSupportedAlgorithm("tukey"))
                throw new InvalidOperationException("tukey should be supported");
            if (LineFittingParameters.IsSupportedAlgorithm("ransac"))
                throw new InvalidOperationException("ransac should not be supported by FitLineContourXld");

            LineFittingResult result = new LineFittingResult();
            AssertEqual(false, result.Success, "Default Success");
            AssertEqual(string.Empty, result.ErrorMessage, "Default ErrorMessage");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
            }
        }
    }
}
