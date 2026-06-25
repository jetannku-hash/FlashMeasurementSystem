using System;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Tests
{
    public static class Rect2EditMathTests
    {
        public static void Run()
        {
            // Axes phi=0 -> e1=(0,1), e2=(1,0)
            Rect2EditMath.Axes(0.0, out double e1r, out double e1c, out double e2r, out double e2c);
            Near(0.0, e1r, "Axes phi0 e1r"); Near(1.0, e1c, "Axes phi0 e1c");
            Near(1.0, e2r, "Axes phi0 e2r"); Near(0.0, e2c, "Axes phi0 e2c");

            // Axes phi=pi/2 -> e1=(-1,0)
            Rect2EditMath.Axes(Math.PI / 2.0, out e1r, out e1c, out e2r, out e2c);
            Near(-1.0, e1r, "Axes pi/2 e1r"); Near(0.0, e1c, "Axes pi/2 e1c");

            // Rotate
            Near(0.0, Rect2EditMath.Rotate(100, 200, 100, 100), "Rotate right -> 0");
            Near(Math.PI / 2.0, Rect2EditMath.Rotate(0, 100, 100, 100), "Rotate up -> pi/2");

            // HitTest: center=(100,100), phi=0, l1=50, l2=30, tol=5, knobGap=20
            AssertHandle(Rect2Handle.Corner, Rect2EditMath.HitTest(130, 150, 100, 100, 0, 50, 30, 5, 20), "Corner hit");
            AssertHandle(Rect2Handle.Len1, Rect2EditMath.HitTest(100, 150, 100, 100, 0, 50, 30, 5, 20), "Len1 hit");
            AssertHandle(Rect2Handle.Len2, Rect2EditMath.HitTest(130, 100, 100, 100, 0, 50, 30, 5, 20), "Len2 hit");
            AssertHandle(Rect2Handle.Rotate, Rect2EditMath.HitTest(100, 170, 100, 100, 0, 50, 30, 5, 20), "Rotate hit");
            AssertHandle(Rect2Handle.Body, Rect2EditMath.HitTest(100, 100, 100, 100, 0, 50, 30, 5, 20), "Body hit");
            AssertHandle(Rect2Handle.None, Rect2EditMath.HitTest(100, 300, 100, 100, 0, 50, 30, 5, 20), "None hit");

            // ApplyResize corner mouse(160,170) -> l1=70, l2=60
            double l1 = 50, l2 = 30;
            Rect2EditMath.ApplyResize(Rect2Handle.Corner, 160, 170, 100, 100, 0, ref l1, ref l2);
            Near(70.0, l1, "Resize corner l1"); Near(60.0, l2, "Resize corner l2");

            // ApplyResize len1 only
            l1 = 50; l2 = 30;
            Rect2EditMath.ApplyResize(Rect2Handle.Len1, 100, 180, 100, 100, 0, ref l1, ref l2);
            Near(80.0, l1, "Resize len1 l1"); Near(30.0, l2, "Resize len1 l2 unchanged");

            // Min clamp
            l1 = 50; l2 = 30;
            Rect2EditMath.ApplyResize(Rect2Handle.Corner, 100, 100, 100, 100, 0, ref l1, ref l2);
            Near(Rect2EditMath.MinHalfLen, l1, "Resize clamp l1");
            Near(Rect2EditMath.MinHalfLen, l2, "Resize clamp l2");
        }

        private static void Near(double expected, double actual, string msg)
        {
            if (Math.Abs(expected - actual) > 1e-6)
                throw new InvalidOperationException("Rect2EditMathTests FAILED: " + msg + " expected " + expected + " got " + actual);
        }

        private static void AssertHandle(Rect2Handle expected, Rect2Handle actual, string msg)
        {
            if (expected != actual)
                throw new InvalidOperationException("Rect2EditMathTests FAILED: " + msg + " expected " + expected + " got " + actual);
        }
    }
}
