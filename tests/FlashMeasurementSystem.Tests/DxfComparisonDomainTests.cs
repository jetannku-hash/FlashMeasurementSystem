using System;
using FlashMeasurementSystem.Domain.DxfComparison;

namespace FlashMeasurementSystem.Tests
{
    public static class DxfComparisonDomainTests
    {
        public static void Run()
        {
            // ─── DTO 預設值 ───
            var p = DxfComparisonParameters.Default();
            AssertClose(2.0, p.TolerancePx, 1e-9, "Default TolerancePx");
            AssertClose(0.5, p.MinScore, 1e-9, "Default MinScore");
            AssertClose(10.0, p.BandWidthPx, 1e-9, "Default BandWidthPx");

            var failed = DxfComparisonResult.Failed("x");
            AssertEqual(false, failed.Success, "Failed Success false");
            AssertEqual(false, failed.IsPass, "Failed IsPass false");

            // ─── 統計正確性：偏差 {0,1,2,3}，T=2 ───
            // max=3, mean=1.5, rms=sqrt((0+1+4+9)/4)=sqrt(3.5)=1.8708, over(>2)=1(只有3)
            var r = DxfDeviationEvaluator.Evaluate(new double[] { 0, 1, 2, 3 }, 2.0);
            AssertEqual(true, r.Success, "Evaluate Success");
            AssertClose(3.0, r.MaxDevPx, 1e-9, "Max");
            AssertClose(1.5, r.MeanDevPx, 1e-9, "Mean");
            AssertClose(Math.Sqrt(3.5), r.RmsDevPx, 1e-9, "Rms");
            AssertEqual(4, r.PointsEvaluated, "PointsEvaluated");
            AssertEqual(1, r.PointsOverTolerance, "PointsOverTolerance (>2)");
            AssertEqual(false, r.IsPass, "max 3 > T 2 → FAIL");

            // ─── 無號：負偏差取絕對值 ───
            var rn = DxfDeviationEvaluator.Evaluate(new double[] { -3, 1 }, 2.0);
            AssertClose(3.0, rn.MaxDevPx, 1e-9, "Abs max from -3");

            // ─── 邊界含公差：max 恰 = T → PASS ───
            var rb = DxfDeviationEvaluator.Evaluate(new double[] { 0.5, 2.0 }, 2.0);
            AssertEqual(true, rb.IsPass, "max == T is PASS (inclusive)");
            AssertEqual(0, rb.PointsOverTolerance, "boundary not counted as over");

            // ─── 剛超出 ───
            var ro = DxfDeviationEvaluator.Evaluate(new double[] { 2.0001 }, 2.0);
            AssertEqual(false, ro.IsPass, "just over → FAIL");
            AssertEqual(1, ro.PointsOverTolerance, "1 over");

            // ─── 空/null → Success=false ───
            AssertEqual(false, DxfDeviationEvaluator.Evaluate(new double[0], 2.0).Success, "empty → fail");
            AssertEqual(false, DxfDeviationEvaluator.Evaluate(null, 2.0).Success, "null → fail");

            // ─── 介面契約（Fake）───
            FlashMeasurementSystem.Application.DxfComparison.IDxfContourComparer<object> fake =
                new FakeDxfComparer();
            var fr = fake.Compare(new object(), "x.dxf", DxfComparisonParameters.Default());
            AssertEqual(true, fr.Success, "Fake comparer satisfies interface contract");
            AssertEqual(true, fr.IsPass, "Fake comparer returns pass");
        }

        private sealed class FakeDxfComparer
            : FlashMeasurementSystem.Application.DxfComparison.IDxfContourComparer<object>
        {
            public DxfComparisonResult Compare(object image, string dxfFilePath, DxfComparisonParameters parameters)
                => new DxfComparisonResult { Success = true, IsPass = true, Message = "FAKE" };
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }
    }
}
