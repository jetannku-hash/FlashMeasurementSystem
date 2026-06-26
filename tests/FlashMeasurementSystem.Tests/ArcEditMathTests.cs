using System;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Tests
{
    // 弧形互動編輯純幾何測試（console-style；assert 以丟例外表示失敗）。
    public static class ArcEditMathTests
    {
        public static void Run()
        {
            TestPointOnArcConvention();
            TestAngleRoundTrip();
            TestHitTestEachHandle();
            TestHitTestCenterAndBandAndNone();
            TestApplyDragRadiusAndAnnulus();
            TestApplyDragAngleStartKeepsEndFixed();
            TestApplyDragAngleEndChangesExtent();
        }

        // phi=0 -> 正右 (cr, cc+R)；phi=pi/2 -> 正上 (cr-R, cc)。
        private static void TestPointOnArcConvention()
        {
            ArcEditMath.PointOnArc(100, 200, 50, 0, out double r0, out double c0);
            Near(r0, 100); Near(c0, 250);
            ArcEditMath.PointOnArc(100, 200, 50, Math.PI / 2, out double r1, out double c1);
            Near(r1, 50); Near(c1, 200);
        }

        private static void TestAngleRoundTrip()
        {
            double phi = 0.7;
            ArcEditMath.PointOnArc(100, 200, 40, phi, out double r, out double c);
            Near(ArcEditMath.AngleOf(r, c, 100, 200), phi);
            Near(ArcEditMath.RadiusOf(r, c, 100, 200), 40);
        }

        // 把滑鼠放在每個把手的精確位置 -> 應命中該把手。
        private static void TestHitTestEachHandle()
        {
            double cr = 100, cc = 200, radius = 80, a0 = 0, extent = Math.PI / 2, annulus = 20, tol = 6;
            double aMid = a0 + extent / 2;

            ArcEditMath.PointOnArc(cr, cc, radius, a0, out double sr, out double sc);
            AssertHandle(ArcEditMath.HitTest(sr, sc, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.AngleStart);

            ArcEditMath.PointOnArc(cr, cc, radius, a0 + extent, out double er, out double ec);
            AssertHandle(ArcEditMath.HitTest(er, ec, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.AngleEnd);

            ArcEditMath.PointOnArc(cr, cc, radius, aMid, out double rr, out double rc);
            AssertHandle(ArcEditMath.HitTest(rr, rc, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.Radius);

            ArcEditMath.PointOnArc(cr, cc, radius + annulus, aMid, out double ar, out double ac);
            AssertHandle(ArcEditMath.HitTest(ar, ac, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.Annulus);
        }

        private static void TestHitTestCenterAndBandAndNone()
        {
            double cr = 100, cc = 200, radius = 80, a0 = 0, extent = Math.PI / 2, annulus = 20, tol = 6;
            // 中心點 -> Center
            AssertHandle(ArcEditMath.HitTest(cr, cc, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.Center);
            // 帶內、角度範圍內（內弧、靠近起角 0.1rad，距任何點把手 > tol 因 annulus=20）-> Center
            ArcEditMath.PointOnArc(cr, cc, radius - annulus, a0 + 0.1, out double br, out double bc);
            AssertHandle(ArcEditMath.HitTest(br, bc, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.Center);
            // 遠處 -> None
            AssertHandle(ArcEditMath.HitTest(cr + 500, cc + 500, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.None);
            // 角度範圍外（-pi/2，半徑在帶上但角度不在 sweep 內）-> None
            ArcEditMath.PointOnArc(cr, cc, radius, -Math.PI / 2, out double or, out double oc);
            AssertHandle(ArcEditMath.HitTest(or, oc, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.None);
        }

        private static void TestApplyDragRadiusAndAnnulus()
        {
            double cr = 100, cc = 200, radius = 80, a0 = 0, extent = Math.PI / 2, annulus = 20;
            // 把 Radius 把手拖到距中心 150 的點（任意角度）
            ArcEditMath.PointOnArc(cr, cc, 150, 0.3, out double pr, out double pc);
            ArcEditMath.ApplyDrag(ArcHandle.Radius, pr, pc, cr, cc, ref radius, ref a0, ref extent, ref annulus);
            Near(radius, 150); Near(annulus, 20);

            // 把 Annulus 把手拖到距中心 radius+35 -> annulus=35
            ArcEditMath.PointOnArc(cr, cc, radius + 35, 0.4, out double ar, out double ac);
            ArcEditMath.ApplyDrag(ArcHandle.Annulus, ar, ac, cr, cc, ref radius, ref a0, ref extent, ref annulus);
            Near(annulus, 35);

            // 夾限：Radius 拖到中心 (0) -> 夾到 MinRadius
            ArcEditMath.ApplyDrag(ArcHandle.Radius, cr, cc, cr, cc, ref radius, ref a0, ref extent, ref annulus);
            Near(radius, ArcEditMath.MinRadius);
        }

        // 拖 AngleStart：另一端 (end = a0+extent) 固定，extent 跟著變。
        private static void TestApplyDragAngleStartKeepsEndFixed()
        {
            double cr = 100, cc = 200, radius = 80, a0 = 0, extent = Math.PI / 2, annulus = 20; // end = pi/2
            ArcEditMath.PointOnArc(cr, cc, radius, -Math.PI / 4, out double pr, out double pc);
            ArcEditMath.ApplyDrag(ArcHandle.AngleStart, pr, pc, cr, cc, ref radius, ref a0, ref extent, ref annulus);
            Near(a0, -Math.PI / 4);
            Near(a0 + extent, Math.PI / 2); // end 不動
        }

        private static void TestApplyDragAngleEndChangesExtent()
        {
            double cr = 100, cc = 200, radius = 80, a0 = 0, extent = Math.PI / 2, annulus = 20;
            ArcEditMath.PointOnArc(cr, cc, radius, Math.PI * 0.75, out double pr, out double pc);
            ArcEditMath.ApplyDrag(ArcHandle.AngleEnd, pr, pc, cr, cc, ref radius, ref a0, ref extent, ref annulus);
            Near(a0, 0);
            Near(extent, Math.PI * 0.75);
        }

        private static void Near(double actual, double expected, double tol = 1e-6)
        {
            if (Math.Abs(actual - expected) > tol)
                throw new InvalidOperationException($"Expected {expected}, got {actual}");
        }

        private static void AssertHandle(ArcHandle actual, ArcHandle expected)
        {
            if (actual != expected)
                throw new InvalidOperationException($"Expected handle {expected}, got {actual}");
        }
    }
}
