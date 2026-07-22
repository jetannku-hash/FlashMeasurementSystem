using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.Gdt;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// StraightnessBand 真直度帶寬測試（真值 peak-to-peak，取代 ResidualRms 近似）。純幾何、可全驗。
    /// 水平擬合線 (0,0)-(0,10)：垂距 = −row，故帶寬 = (max row − min row) of points。
    /// </summary>
    public static class StraightnessBandDomainTests
    {
        public static void Run()
        {
            HorizontalLineBandEqualsRowSpread();
            SlantedLineBand();
            DegenerateLineReturnsZero();
            TooFewPointsReturnsZero();

            Console.WriteLine("StraightnessBandDomainTests passed");
        }

        // 水平線 (0,0)-(0,10)；點 row={0,3,-1} → 有號垂距={0,-3,1} → 帶寬 = 1-(-3) = 4。
        private static void HorizontalLineBandEqualsRowSpread()
        {
            var pts = Pts(new[] { 0.0, 3.0, -1.0 }, new[] { 1.0, 5.0, 9.0 });
            double band = StraightnessBand.PeakToPeakPx(pts, 0, 0, 0, 10);
            AssertClose(4.0, band, 1e-9, "horizontal line: band = row spread (3-(-1))");
        }

        // 45° 線 (0,0)-(10,10)：點在線上 → 帶寬 0；一點偏離 → 帶寬 = 該點垂距。
        // 點 (0,0)、(10,10) 在線上；點 (0, √2*d) 之垂距 = d。取 (0, 2) → 垂距 = 2/√2 = √2。
        private static void SlantedLineBand()
        {
            var pts = Pts(new[] { 0.0, 10.0, 0.0 }, new[] { 0.0, 10.0, 2.0 });
            double band = StraightnessBand.PeakToPeakPx(pts, 0, 0, 10, 10);
            // 線上兩點垂距 0；(0,2) 垂距 = |0*10 ... | 用公式：((pc-c1)*dr-(pr-r1)*dc)/L
            // dr=10,dc=10,L=√200；(0,2): (2*10 - 0*10)/√200 = 20/14.142 = √2 ≈ 1.41421356
            AssertClose(Math.Sqrt(2.0), band, 1e-6, "slanted line: band = offset point perpendicular distance");
        }

        private static void DegenerateLineReturnsZero()
        {
            var pts = Pts(new[] { 0.0, 3.0 }, new[] { 1.0, 5.0 });
            AssertClose(0.0, StraightnessBand.PeakToPeakPx(pts, 5, 5, 5, 5), 1e-12, "degenerate line (coincident endpoints) → 0");
        }

        private static void TooFewPointsReturnsZero()
        {
            AssertClose(0.0, StraightnessBand.PeakToPeakPx(new List<EdgePoint>(), 0, 0, 0, 10), 1e-12, "empty → 0");
            AssertClose(0.0, StraightnessBand.PeakToPeakPx(Pts(new[] { 2.0 }, new[] { 3.0 }), 0, 0, 0, 10), 1e-12, "single point → 0");
            AssertClose(0.0, StraightnessBand.PeakToPeakPx(null, 0, 0, 0, 10), 1e-12, "null → 0");
        }

        private static List<EdgePoint> Pts(double[] rows, double[] cols)
        {
            var list = new List<EdgePoint>();
            for (int i = 0; i < rows.Length; i++) list.Add(new EdgePoint { Row = rows[i], Column = cols[i] });
            return list;
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException("FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }
    }
}
