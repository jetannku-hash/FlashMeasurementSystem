using System;
using FlashMeasurementSystem.Domain.AngleMeasurement;

namespace FlashMeasurementSystem.Tests
{
    // 角度標註幾何純測試（console-style；assert 以丟例外表示失敗）。
    // 座標慣例：影像方位角 theta = atan2(dRow, dCol)。
    public static class AngleAnnotationMathTests
    {
        public static void Run()
        {
            TestPerpendicularCrossing();
            TestObtuseDirectionFlippedToAcute();
            TestNearParallelReturnsFalse();
            TestDegenerateSegmentReturnsFalse();
            TestAdaptiveRadius();
            TestNormalizePi();
            Console.WriteLine("AngleAnnotationMathTests passed");
        }

        // 水平線(row=0) × 垂直線(col=5)：頂點 (0,5)，start=線A方向(0)，sweep=+90°。
        private static void TestPerpendicularCrossing()
        {
            bool ok = AngleAnnotationMath.TryCompute(
                0, 0, 0, 10,      // A: (0,0)→(0,10) 沿 +col
                0, 5, 10, 5,      // B: (0,5)→(10,5) 沿 +row
                out double vr, out double vc, out double start, out double sweep, out double radius);
            AssertTrue(ok, "perpendicular: ok");
            Near(vr, 0, "perpendicular: vertex row");
            Near(vc, 5, "perpendicular: vertex col");
            Near(start, 0, "perpendicular: start = 線A方位角");
            Near(sweep, Math.PI / 2, "perpendicular: sweep = +90°");
            AssertTrue(radius >= AngleAnnotationMath.MinRadiusPx
                && radius <= AngleAnnotationMath.MaxRadiusPx, "perpendicular: radius in range");
        }

        // 線B方位角 135°（鈍向）：應翻 180° 使 |sweep| = 45° 銳角，方向為負。
        private static void TestObtuseDirectionFlippedToAcute()
        {
            bool ok = AngleAnnotationMath.TryCompute(
                0, 0, 0, 10,      // A: row=0 沿 +col，theta=0
                -1, 5, 1, 3,      // B: 方向 (2,-2) → theta=135°；過 (0,4)
                out double vr, out double vc, out double start, out double sweep, out double radius);
            AssertTrue(ok, "flip: ok");
            Near(vr, 0, "flip: vertex row");
            Near(vc, 4, "flip: vertex col");
            Near(start, 0, "flip: start");
            Near(sweep, -Math.PI / 4, "flip: sweep 翻成 -45°（銳角）");
        }

        private static void TestNearParallelReturnsFalse()
        {
            bool ok = AngleAnnotationMath.TryCompute(
                0, 0, 0, 10,
                1, 0, 1, 10,      // 平行線
                out double _, out double _, out double _, out double _, out double _);
            AssertTrue(!ok, "parallel: 應回傳 false");
        }

        private static void TestDegenerateSegmentReturnsFalse()
        {
            bool ok = AngleAnnotationMath.TryCompute(
                5, 5, 5, 5,       // 零長度線段
                0, 0, 10, 10,
                out double _, out double _, out double _, out double _, out double _);
            AssertTrue(!ok, "degenerate: 應回傳 false");
        }

        // 兩條長 100 的線在中點相交：reach=50 → radius = 0.4×50 = 20（在 [Min,Max] 內）。
        private static void TestAdaptiveRadius()
        {
            bool ok = AngleAnnotationMath.TryCompute(
                0, -50, 0, 50,    // A: row=0，長 100，中點 (0,0)
                -50, 0, 50, 0,    // B: col=0，長 100，中點 (0,0)
                out double vr, out double vc, out double _, out double _, out double radius);
            AssertTrue(ok, "radius: ok");
            Near(vr, 0, "radius: vertex row");
            Near(vc, 0, "radius: vertex col");
            Near(radius, 20, "radius: 0.4×min(reach)");
        }

        private static void TestNormalizePi()
        {
            Near(AngleAnnotationMath.NormalizePi(3 * Math.PI), Math.PI, "normalize: 3π→π");
            Near(AngleAnnotationMath.NormalizePi(-Math.PI), Math.PI, "normalize: -π→π（半開區間 (-π,π]）");
            Near(AngleAnnotationMath.NormalizePi(0.5), 0.5, "normalize: 不變");
        }

        private static void Near(double actual, double expected, string label)
        {
            if (Math.Abs(actual - expected) > 1e-9)
                throw new InvalidOperationException(label + $"：expected {expected}, got {actual}");
        }

        private static void AssertTrue(bool cond, string label)
        {
            if (!cond) throw new InvalidOperationException(label);
        }
    }
}
