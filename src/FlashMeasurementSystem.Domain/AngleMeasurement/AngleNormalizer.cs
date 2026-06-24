using System;

namespace FlashMeasurementSystem.Domain.AngleMeasurement
{
    /// <summary>
    /// Pure math helpers for angle normalization with 180° period (lines have no direction,
    /// so angles 0° and 180° represent the same line orientation).
    /// </summary>
    public static class AngleNormalizer
    {
        /// <summary>
        /// Normalize an angle in degrees to the half-circle range [0, 180).
        /// Examples: 190 → 10, -10 → 170, 180 → 0.
        /// </summary>
        public static double ToHalfCircle(double deg)
        {
            double m = deg % 180.0;
            if (m < 0.0)
                m += 180.0;
            return m;
        }

        /// <summary>
        /// Minimal circular distance between two angles with 180° period.
        /// Returns a value in [0, 90] — the smallest absolute difference regardless of direction.
        /// Examples: (179°, 1°) → 2°, (10°, 100°) → 90°.
        /// </summary>
        public static double CircularDiffDeg(double a, double b)
        {
            double diff = Math.Abs(ToHalfCircle(a) - ToHalfCircle(b));
            if (diff > 90.0)
                diff = 180.0 - diff;
            return diff;
        }

        /// <summary>
        /// Signed minimal difference from <paramref name="nominal"/> toward <paramref name="measured"/>,
        /// with 180° period. Result is in [-90, 90]. The identity
        ///   aligned = nominal + CircularSignedDiffDeg(measured, nominal)
        /// gives the measured angle shifted to the same "circle" as nominal,
        /// suitable for linear tolerance judging.
        /// </summary>
        public static double CircularSignedDiffDeg(double measured, double nominal)
        {
            double raw = measured - nominal;
            double diff = ToHalfCircle(raw); // [0, 180)
            if (diff > 90.0)
                diff -= 180.0;
            return diff;
        }
    }
}
