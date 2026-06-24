using System;
using FlashMeasurementSystem.Domain.AngleMeasurement;

namespace FlashMeasurementSystem.Tests
{
    public static class AngleNormalizerTests
    {
        public static void Run()
        {
            // ToHalfCircle
            AssertEqual(10.0, AngleNormalizer.ToHalfCircle(190.0), "ToHalfCircle(190)");
            AssertEqual(170.0, AngleNormalizer.ToHalfCircle(-10.0), "ToHalfCircle(-10)");
            AssertEqual(0.0, AngleNormalizer.ToHalfCircle(0.0), "ToHalfCircle(0)");
            AssertEqual(0.0, AngleNormalizer.ToHalfCircle(180.0), "ToHalfCircle(180)");
            AssertEqual(179.0, AngleNormalizer.ToHalfCircle(179.0), "ToHalfCircle(179)");
            AssertEqual(0.0, AngleNormalizer.ToHalfCircle(360.0), "ToHalfCircle(360)");
            AssertEqual(170.0, AngleNormalizer.ToHalfCircle(-190.0), "ToHalfCircle(-190)");

            // CircularDiffDeg
            AssertEqual(2.0, AngleNormalizer.CircularDiffDeg(179.0, 1.0), "CircularDiff(179,1)");
            AssertEqual(2.0, AngleNormalizer.CircularDiffDeg(1.0, 179.0), "CircularDiff(1,179) symmetric");
            AssertEqual(90.0, AngleNormalizer.CircularDiffDeg(10.0, 100.0), "CircularDiff(10,100)");
            AssertEqual(90.0, AngleNormalizer.CircularDiffDeg(0.0, 90.0), "CircularDiff(0,90)");
            AssertEqual(0.0, AngleNormalizer.CircularDiffDeg(0.0, 0.0), "CircularDiff(0,0) zero");
            AssertEqual(0.0, AngleNormalizer.CircularDiffDeg(10.0, 190.0), "CircularDiff(10,190) same line");

            // CircularSignedDiffDeg
            AssertEqual(-2.0, AngleNormalizer.CircularSignedDiffDeg(179.0, 1.0), "SignedDiff(179,1) → -2");
            AssertEqual(2.0, AngleNormalizer.CircularSignedDiffDeg(1.0, 179.0), "SignedDiff(1,179) → +2");
            AssertEqual(0.0, AngleNormalizer.CircularSignedDiffDeg(10.0, 10.0), "SignedDiff(10,10) zero");
            AssertEqual(90.0, AngleNormalizer.CircularSignedDiffDeg(100.0, 10.0), "SignedDiff(100,10) → +90");
            AssertEqual(90.0, AngleNormalizer.CircularSignedDiffDeg(10.0, 100.0), "SignedDiff(10,100) → +90 (exact quadrature)");
            AssertEqual(2.0, AngleNormalizer.CircularSignedDiffDeg(182.0, 0.0), "SignedDiff(182,0) → +2");

            // Alignment identity: nominal + signedDiff should represent measured in nominal's neighborhood
            AssertEqual(-1.0, 1.0 + AngleNormalizer.CircularSignedDiffDeg(179.0, 1.0), "align 179 near nominal 1");

            Console.WriteLine("AngleNormalizerTests passed");
        }

        private static void AssertEqual(double expected, double actual, string name)
        {
            if (Math.Abs(expected - actual) > 1e-9)
            {
                throw new InvalidOperationException(
                    string.Format("{0}: expected {1}, actual {2}", name, expected, actual));
            }
        }
    }
}
