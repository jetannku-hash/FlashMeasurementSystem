using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.CircleFitting;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Tests
{
    public static class CircleFittingDomainTests
    {
        public static void Run()
        {
            CircleFittingParameters parameters = CircleFittingParameters.Default();

            AssertEqual("geotukey", parameters.Algorithm, "Default algorithm");
            AssertEqual(-1, parameters.MaxNumPoints, "Default MaxNumPoints");
            AssertEqual(0.0, parameters.MaxClosureDist, "Default MaxClosureDist");
            AssertEqual(0, parameters.ClippingEndPoints, "Default ClippingEndPoints");
            AssertEqual(3, parameters.Iterations, "Default Iterations");
            AssertEqual(2.0, parameters.ClippingFactor, "Default ClippingFactor");
            AssertEqual(3, parameters.MinPoints, "Default MinPoints");

            if (!CircleFittingParameters.IsSupportedAlgorithm("algebraic"))
                throw new InvalidOperationException("algebraic should be supported");
            if (!CircleFittingParameters.IsSupportedAlgorithm("geotukey"))
                throw new InvalidOperationException("geotukey should be supported");
            if (CircleFittingParameters.IsSupportedAlgorithm("ransac"))
                throw new InvalidOperationException("ransac should not be supported by FitCircleContourXld");

            CircleFittingResult result = new CircleFittingResult();
            AssertEqual(false, result.Success, "Default Success");
            AssertEqual(0.0, result.CenterRow, "Default CenterRow");
            AssertEqual(0.0, result.CenterColumn, "Default CenterColumn");
            AssertEqual(0.0, result.RadiusPx, "Default RadiusPx");
            AssertEqual(0.0, result.DiameterPx, "Default DiameterPx");
            AssertEqual(0.0, result.StartPhi, "Default StartPhi");
            AssertEqual(0.0, result.EndPhi, "Default EndPhi");
            AssertEqual(string.Empty, result.PointOrder, "Default PointOrder");
            AssertEqual(0.0, result.ResidualRms, "Default ResidualRms");
            AssertEqual(0.0, result.Roundness, "Default Roundness");
            AssertEqual(0, result.UsedPoints, "Default UsedPoints");
            AssertEqual(string.Empty, result.ErrorMessage, "Default ErrorMessage");

            // Arc detection (IsClosed)
            CircleFittingResult fullCircle = new CircleFittingResult
            {
                Success = true,
                StartPhi = 0.0,
                EndPhi = 2.0 * Math.PI,
                PointOrder = "positive"
            };
            AssertEqual(true, fullCircle.IsClosed, "Full circle (0→2pi) should be closed");

            CircleFittingResult arc = new CircleFittingResult
            {
                Success = true,
                StartPhi = 0.0,
                EndPhi = Math.PI,
                PointOrder = "positive"
            };
            AssertEqual(false, arc.IsClosed, "180deg arc should not be closed");

            CircleFittingResult defaultResult = new CircleFittingResult();
            AssertEqual(false, defaultResult.IsClosed, "Default (0→0) should not be closed");

            ICircleFitter fitter = new FakeCircleFitter();
            CircleFittingResult fakeResult = fitter.FitCircle(new List<EdgePoint>(), parameters);
            AssertEqual(true, fakeResult.Success, "Fake circle fitter should satisfy interface contract");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
            }
        }

        private sealed class FakeCircleFitter : ICircleFitter
        {
            public CircleFittingResult FitCircle(IList<EdgePoint> edgePoints, CircleFittingParameters parameters)
            {
                return new CircleFittingResult { Success = true };
            }
        }
    }
}
