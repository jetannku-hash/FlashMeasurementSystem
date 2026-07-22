using System;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// B1 fuzzy 邊緣參數（EdgeDetectionParameters）測試。純 Domain、可全驗。
    /// HALCON fuzzy_measure_pos 的行為（adapter）需在 HALCON 機台 GUI 驗證；此處只鎖 DTO 契約。
    /// </summary>
    public static class EdgeParametersFuzzyDomainTests
    {
        public static void Run()
        {
            DefaultsKeepFuzzyOff();
            FuzzyThreshRangeValidation();

            Console.WriteLine("EdgeParametersFuzzyDomainTests passed");
        }

        // 預設關閉 → 既有 measure_pos 行為不受影響（向後相容）。
        private static void DefaultsKeepFuzzyOff()
        {
            var p = EdgeDetectionParameters.Default();
            AssertEqual(false, p.FuzzyEnabled, "default FuzzyEnabled = false");
            AssertClose(0.5, p.FuzzyThresh, 1e-9, "default FuzzyThresh = 0.5");
        }

        // 模糊分數門檻僅在 [0,1] 有效。
        private static void FuzzyThreshRangeValidation()
        {
            AssertEqual(true, EdgeDetectionParameters.IsValidFuzzyThresh(0.0), "0.0 valid");
            AssertEqual(true, EdgeDetectionParameters.IsValidFuzzyThresh(1.0), "1.0 valid");
            AssertEqual(true, EdgeDetectionParameters.IsValidFuzzyThresh(0.5), "0.5 valid");
            AssertEqual(false, EdgeDetectionParameters.IsValidFuzzyThresh(-0.01), "negative invalid");
            AssertEqual(false, EdgeDetectionParameters.IsValidFuzzyThresh(1.01), ">1 invalid");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException("FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }
    }
}
