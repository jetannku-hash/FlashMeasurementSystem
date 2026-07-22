using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.Repeatability;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// RepeatabilityReport 統計測試（開發手冊 §6.3）。純 Domain、可全驗。
    /// 已知資料集 [10,12,14]：mean=12、樣本標準差=2（sumSq=8÷(n−1)=4→√=2）、6σ=12、range=4。
    /// 搭配不同公差全幅命中 GR&R% 各判定門檻與邊界。
    /// </summary>
    public static class RepeatabilityDomainTests
    {
        public static void Run()
        {
            BasicStatistics();
            GrrPercentAndVerdicts();
            VerdictBoundaries();
            NoToleranceIsNotAvailable();
            RejectsFewerThanTwo();
            RejectsNull();

            Console.WriteLine("RepeatabilityDomainTests passed");
        }

        private static readonly double[] Sample = { 10.0, 12.0, 14.0 };  // mean=12 σ=2 6σ=12

        private static void BasicStatistics()
        {
            RepeatabilityReport r = RepeatabilityReport.Calculate(new List<double>(Sample), toleranceRange: 40.0);

            AssertEqual(3, r.Count, "count");
            AssertClose(12.0, r.Mean, 1e-9, "mean");
            AssertClose(2.0, r.StdDev, 1e-9, "sample stddev (n-1)");
            AssertClose(10.0, r.Min, 1e-9, "min");
            AssertClose(14.0, r.Max, 1e-9, "max");
            AssertClose(4.0, r.Range, 1e-9, "range = max - min");
            AssertClose(12.0, r.SixSigma, 1e-9, "sixSigma = 6*stddev");
        }

        private static void GrrPercentAndVerdicts()
        {
            // 6σ=12：tolRange=200 → 6% → Excellent
            RepeatabilityReport ex = RepeatabilityReport.Calculate(new List<double>(Sample), 200.0);
            AssertClose(6.0, ex.GrrPercent, 1e-9, "grr% = 6σ/tolRange*100 (6%)");
            AssertEqual(RepeatabilityVerdict.Excellent, ex.Verdict, "verdict <10% → Excellent");

            // tolRange=40 → 30% → Acceptable
            RepeatabilityReport ac = RepeatabilityReport.Calculate(new List<double>(Sample), 40.0);
            AssertClose(30.0, ac.GrrPercent, 1e-9, "grr% 30%");
            AssertEqual(RepeatabilityVerdict.Acceptable, ac.Verdict, "verdict 30% → Acceptable");

            // tolRange=20 → 60% → Unacceptable
            RepeatabilityReport un = RepeatabilityReport.Calculate(new List<double>(Sample), 20.0);
            AssertClose(60.0, un.GrrPercent, 1e-9, "grr% 60%");
            AssertEqual(RepeatabilityVerdict.Unacceptable, un.Verdict, "verdict >30% → Unacceptable");
        }

        private static void VerdictBoundaries()
        {
            // 手冊：< 10% 優良；10%~30% 可接受；> 30% 不合格。邊界 10 與 30 歸「可接受」。
            // 6σ=12 → tolRange=120 → 10.0%
            AssertEqual(RepeatabilityVerdict.Acceptable,
                RepeatabilityReport.Calculate(new List<double>(Sample), 120.0).Verdict,
                "boundary 10% → Acceptable (not Excellent)");
            // tolRange=40 → 30.0%
            AssertEqual(RepeatabilityVerdict.Acceptable,
                RepeatabilityReport.Calculate(new List<double>(Sample), 40.0).Verdict,
                "boundary 30% → Acceptable (not Unacceptable)");
        }

        private static void NoToleranceIsNotAvailable()
        {
            RepeatabilityReport r = RepeatabilityReport.Calculate(new List<double>(Sample), toleranceRange: 0.0);
            AssertEqual(RepeatabilityVerdict.NotAvailable, r.Verdict, "tolRange<=0 → NotAvailable");
            AssertTrue(double.IsNaN(r.GrrPercent), "tolRange<=0 → GrrPercent NaN");
            // 與公差無關的統計仍有效
            AssertClose(12.0, r.SixSigma, 1e-9, "sixSigma still valid without tolerance");
        }

        private static void RejectsFewerThanTwo()
        {
            bool threw = false;
            try { RepeatabilityReport.Calculate(new List<double> { 5.0 }, 10.0); }
            catch (ArgumentException) { threw = true; }
            AssertTrue(threw, "n<2 throws ArgumentException");
        }

        private static void RejectsNull()
        {
            bool threw = false;
            try { RepeatabilityReport.Calculate(null, 10.0); }
            catch (ArgumentException) { threw = true; }
            AssertTrue(threw, "null values throws ArgumentException");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException("FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }

        private static void AssertTrue(bool cond, string name)
        {
            if (!cond) throw new InvalidOperationException("FAIL " + name);
        }
    }
}
