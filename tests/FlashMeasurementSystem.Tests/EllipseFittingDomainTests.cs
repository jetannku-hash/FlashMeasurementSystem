using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.EllipseFitting;
using FlashMeasurementSystem.Domain.EllipseFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Tests
{
    public static class EllipseFittingDomainTests
    {
        public static void Run()
        {
            EllipseFittingParameters parameters = EllipseFittingParameters.Default();

            AssertEqual("geotukey", parameters.Algorithm, "Default algorithm");
            AssertEqual(-1, parameters.MaxNumPoints, "Default MaxNumPoints");
            AssertEqual(0.0, parameters.MaxClosureDist, "Default MaxClosureDist");
            AssertEqual(0, parameters.ClippingEndPoints, "Default ClippingEndPoints");
            AssertEqual(100, parameters.VossTabSize, "Default VossTabSize");
            AssertEqual(3, parameters.Iterations, "Default Iterations");
            AssertEqual(2.0, parameters.ClippingFactor, "Default ClippingFactor");
            // 橢圓最少 5 點（reference L175690），與 circle 的 3 不同。
            AssertEqual(5, parameters.MinPoints, "Default MinPoints");

            if (!EllipseFittingParameters.IsSupportedAlgorithm("geotukey"))
                throw new InvalidOperationException("geotukey should be supported");
            if (!EllipseFittingParameters.IsSupportedAlgorithm("fitzgibbon"))
                throw new InvalidOperationException("fitzgibbon should be supported");
            if (!EllipseFittingParameters.IsSupportedAlgorithm("voss"))
                throw new InvalidOperationException("voss should be supported");
            // 'algebraic' 是 circle 的演算法名，fit_ellipse_contour_xld 沒有，不可誤收。
            if (EllipseFittingParameters.IsSupportedAlgorithm("algebraic"))
                throw new InvalidOperationException("algebraic should not be supported by FitEllipseContourXld");
            if (EllipseFittingParameters.IsSupportedAlgorithm("ransac"))
                throw new InvalidOperationException("ransac should not be supported");

            EllipseFittingResult result = new EllipseFittingResult();
            AssertEqual(false, result.Success, "Default Success");
            AssertEqual(0.0, result.CenterRow, "Default CenterRow");
            AssertEqual(0.0, result.CenterColumn, "Default CenterColumn");
            AssertEqual(0.0, result.Phi, "Default Phi");
            AssertEqual(0.0, result.Radius1Px, "Default Radius1Px");
            AssertEqual(0.0, result.Radius2Px, "Default Radius2Px");
            AssertEqual(0.0, result.StartPhi, "Default StartPhi");
            AssertEqual(0.0, result.EndPhi, "Default EndPhi");
            AssertEqual(string.Empty, result.PointOrder, "Default PointOrder");
            AssertEqual(0.0, result.ResidualRms, "Default ResidualRms");
            AssertEqual(0, result.UsedPoints, "Default UsedPoints");
            AssertEqual(string.Empty, result.ErrorMessage, "Default ErrorMessage");

            IEllipseFitter fitter = new FakeEllipseFitter();
            EllipseFittingResult fakeResult = fitter.FitEllipse(new List<EdgePoint>(), parameters);
            AssertEqual(true, fakeResult.Success, "Fake ellipse fitter should satisfy interface contract");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
            }
        }

        private sealed class FakeEllipseFitter : IEllipseFitter
        {
            public EllipseFittingResult FitEllipse(IList<EdgePoint> edgePoints, EllipseFittingParameters parameters)
            {
                return new EllipseFittingResult { Success = true };
            }
        }
    }
}
