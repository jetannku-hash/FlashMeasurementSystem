using System;
using FlashMeasurementSystem.Domain.Gdt;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// GdtCalculator 偏差計算測試（合成幾何 + 閉合解，確定性）。
    /// 座標 (row, col)：水平線＝固定 row、變動 col（方向 (0,1)）；垂直線＝方向 (1,0)。
    /// </summary>
    public static class GdtCalculatorDomainTests
    {
        private const double Tol = 1e-6;
        private const double Deg2Rad = Math.PI / 180.0;

        public static void Run()
        {
            TestLineLength();
            TestAcuteAngleFolding();
            TestParallelism();
            TestPerpendicularity();
            TestConcentricity();
        }

        private static void TestLineLength()
        {
            AssertClose(5.0, GdtCalculator.LineLengthPx(0, 0, 3, 4), "LineLengthPx 3-4-5");
            AssertClose(100.0, GdtCalculator.LineLengthPx(0, 0, 0, 100), "LineLengthPx horizontal 100");
        }

        private static void TestAcuteAngleFolding()
        {
            // 同向 → 0
            AssertClose(0.0, GdtCalculator.AcuteAngleBetweenLinesDeg(0, 0, 0, 1, 0, 0, 0, 5), "angle same dir");
            // 反向（無向性）→ 折到 0
            AssertClose(0.0, GdtCalculator.AcuteAngleBetweenLinesDeg(0, 0, 0, 1, 0, 0, 0, -1), "angle opposite dir folds to 0");
            // 正交 → 90
            AssertClose(90.0, GdtCalculator.AcuteAngleBetweenLinesDeg(0, 0, 0, 1, 0, 0, 1, 0), "angle orthogonal 90");
            // 45°
            AssertClose(45.0, GdtCalculator.AcuteAngleBetweenLinesDeg(0, 0, 0, 1, 0, 0, 1, 1), "angle 45");
            // 第二象限方向 (-1,-1) 仍折到 45°
            AssertClose(45.0, GdtCalculator.AcuteAngleBetweenLinesDeg(0, 0, 0, 1, 0, 0, -1, -1), "angle (-1,-1) folds to 45");
            // 退化基準線 → 0
            AssertClose(0.0, GdtCalculator.AcuteAngleBetweenLinesDeg(0, 0, 0, 1, 5, 5, 5, 5), "angle degenerate datum -> 0");
        }

        private static void TestParallelism()
        {
            // 量測線水平長 100，基準同向 → 0
            AssertClose(0.0,
                GdtCalculator.ParallelismZonePx(0, 0, 0, 100, 0, 0, 0, 50),
                "parallelism aligned -> 0");

            // 基準與量測線夾 30°（基準方向 (sin30,cos30)）→ 100·sin30 = 50
            AssertClose(50.0,
                GdtCalculator.ParallelismZonePx(0, 0, 0, 100, 0, 0, Math.Sin(30 * Deg2Rad), Math.Cos(30 * Deg2Rad)),
                "parallelism 30deg over L=100 -> 50");

            // 帶寬隨量測線長縮放：同 30°，量測線長 40 → 40·sin30 = 20
            AssertClose(20.0,
                GdtCalculator.ParallelismZonePx(0, 0, 0, 40, 0, 0, Math.Sin(30 * Deg2Rad), Math.Cos(30 * Deg2Rad)),
                "parallelism 30deg over L=40 -> 20");
        }

        private static void TestPerpendicularity()
        {
            // 量測線水平、基準垂直（θ=90°）→ 理想垂直 → 0
            AssertClose(0.0,
                GdtCalculator.PerpendicularityZonePx(0, 0, 0, 100, 0, 0, 100, 0),
                "perpendicularity ideal -> 0");

            // 兩線夾角 80°（偏離垂直 10°）→ 100·sin10
            double expected = 100.0 * Math.Sin(10 * Deg2Rad);
            AssertClose(expected,
                GdtCalculator.PerpendicularityZonePx(0, 0, 0, 100, 0, 0, Math.Sin(80 * Deg2Rad), Math.Cos(80 * Deg2Rad)),
                "perpendicularity off-by-10deg -> 100*sin10");
        }

        private static void TestConcentricity()
        {
            // 圓心 (0,0) 與 (3,4) → 距 5 → 直徑帶 10
            AssertClose(10.0, GdtCalculator.ConcentricityDiametralPx(0, 0, 3, 4), "concentricity offset 5 -> 10");
            // 同心 → 0
            AssertClose(0.0, GdtCalculator.ConcentricityDiametralPx(7, 9, 7, 9), "concentricity coincident -> 0");
        }

        private static void AssertClose(double expected, double actual, string message)
        {
            if (double.IsNaN(actual) || Math.Abs(expected - actual) > Tol)
            {
                throw new InvalidOperationException(string.Format(
                    "{0}: expected {1}, actual {2}", message, expected, actual));
            }
        }
    }
}
