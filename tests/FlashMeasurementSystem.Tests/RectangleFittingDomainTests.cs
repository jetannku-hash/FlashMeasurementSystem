using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.RectangleFitting;
using FlashMeasurementSystem.Domain.RectangleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Tests
{
    public static class RectangleFittingDomainTests
    {
        public static void Run()
        {
            RectangleFittingParameters parameters = RectangleFittingParameters.Default();

            AssertEqual("tukey", parameters.Algorithm, "Default algorithm");
            AssertEqual(-1, parameters.MaxNumPoints, "Default MaxNumPoints");
            AssertEqual(0.0, parameters.MaxClosureDist, "Default MaxClosureDist");
            AssertEqual(0, parameters.ClippingEndPoints, "Default ClippingEndPoints");
            AssertEqual(3, parameters.Iterations, "Default Iterations");
            AssertEqual(2.0, parameters.ClippingFactor, "Default ClippingFactor");
            AssertEqual(8, parameters.MinPoints, "Default MinPoints (8 for rectangle)");

            if (!RectangleFittingParameters.IsSupportedAlgorithm("tukey"))
                throw new InvalidOperationException("tukey should be supported");
            if (!RectangleFittingParameters.IsSupportedAlgorithm("regression"))
                throw new InvalidOperationException("regression should be supported");
            if (!RectangleFittingParameters.IsSupportedAlgorithm("huber"))
                throw new InvalidOperationException("huber should be supported");
            // geometry/geotukey 是 ellipse/circle 演算法，fit_rectangle2_contour_xld 不收。
            if (RectangleFittingParameters.IsSupportedAlgorithm("geotukey"))
                throw new InvalidOperationException("geotukey should not be supported by FitRectangle2ContourXld");
            if (RectangleFittingParameters.IsSupportedAlgorithm("fitzgibbon"))
                throw new InvalidOperationException("fitzgibbon should not be supported");

            RectangleFittingResult result = new RectangleFittingResult();
            AssertEqual(false, result.Success, "Default Success");
            AssertEqual(0.0, result.CenterRow, "Default CenterRow");
            AssertEqual(0.0, result.CenterColumn, "Default CenterColumn");
            AssertEqual(0.0, result.Phi, "Default Phi");
            AssertEqual(0.0, result.Length1Px, "Default Length1Px");
            AssertEqual(0.0, result.Length2Px, "Default Length2Px");
            AssertEqual(string.Empty, result.PointOrder, "Default PointOrder");
            AssertEqual(0.0, result.ResidualRms, "Default ResidualRms");
            AssertEqual(0, result.UsedPoints, "Default UsedPoints");
            AssertEqual(string.Empty, result.ErrorMessage, "Default ErrorMessage");

            IRectangleFitter fitter = new FakeRectangleFitter();
            RectangleFittingResult fakeResult = fitter.FitRectangle(new List<EdgePoint>(), parameters);
            AssertEqual(true, fakeResult.Success, "Fake rectangle fitter should satisfy interface contract");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
            }
        }

        private sealed class FakeRectangleFitter : IRectangleFitter
        {
            public RectangleFittingResult FitRectangle(IList<EdgePoint> edgePoints, RectangleFittingParameters parameters)
            {
                return new RectangleFittingResult { Success = true };
            }
        }
    }
}
