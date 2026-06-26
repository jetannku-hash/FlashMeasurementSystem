using System;
using FlashMeasurementSystem.Domain.Geometry;

namespace FlashMeasurementSystem.Tests
{
    // A5 幾何構造純數學測試（console-style；assert 以丟例外表示失敗）。
    public static class GeometryConstructionDomainTests
    {
        public static void Run()
        {
            TestPrimitiveFactories();
            TestTryAsPoint();
            TestLineIntersection();
            TestProjectPointOntoLine();
            TestMidline();
        }

        private static void TestPrimitiveFactories()
        {
            var p = GeometricPrimitive.Point(10, 20);
            if (p.Kind != GeometricPrimitiveKind.Point) throw new InvalidOperationException("Point kind");
            Near(p.Row, 10); Near(p.Col, 20);

            var l = GeometricPrimitive.Line(1, 2, 3, 4);
            if (l.Kind != GeometricPrimitiveKind.Line) throw new InvalidOperationException("Line kind");
            Near(l.Row1, 1); Near(l.Col1, 2); Near(l.Row2, 3); Near(l.Col2, 4);

            var c = GeometricPrimitive.Circle(50, 60, 25);
            if (c.Kind != GeometricPrimitiveKind.Circle) throw new InvalidOperationException("Circle kind");
            Near(c.CenterRow, 50); Near(c.CenterCol, 60); Near(c.RadiusPx, 25);
        }

        private static void TestTryAsPoint()
        {
            if (!GeometricPrimitive.Point(7, 8).TryAsPoint(out double pr, out double pc))
                throw new InvalidOperationException("point->point");
            Near(pr, 7); Near(pc, 8);

            if (!GeometricPrimitive.Circle(3, 4, 9).TryAsPoint(out double cr, out double cc))
                throw new InvalidOperationException("circle->center");
            Near(cr, 3); Near(cc, 4);

            if (GeometricPrimitive.Line(0, 0, 1, 1).TryAsPoint(out double _, out double _))
                throw new InvalidOperationException("line should not be a point");
        }

        private static void TestLineIntersection()
        {
            // x 軸線 (row=0) 與 y 軸線 (col=0) 交於 (0,0)
            bool ok = GeometryConstruction.TryLineIntersection(
                0, -10, 0, 10,      // line A: row=0
                -10, 0, 10, 0,      // line B: col=0
                out double r, out double c);
            if (!ok) throw new InvalidOperationException("should intersect");
            Near(r, 0); Near(c, 0);

            // 平行（兩條 row=常數）→ false
            bool par = GeometryConstruction.TryLineIntersection(
                0, 0, 0, 10,
                5, 0, 5, 10,
                out double _, out double _);
            if (par) throw new InvalidOperationException("parallel should return false");

            // 一般相交：line A 過 (0,0)-(10,10)；line B 過 (0,10)-(10,0) → 交於 (5,5)
            bool ok2 = GeometryConstruction.TryLineIntersection(
                0, 0, 10, 10,
                0, 10, 10, 0,
                out double r2, out double c2);
            if (!ok2) throw new InvalidOperationException("should intersect 2");
            Near(r2, 5); Near(c2, 5);
        }

        private static void TestProjectPointOntoLine()
        {
            // 點 (5,5) 投影到 row=0 線 → 垂足 (0,5)
            GeometryConstruction.ProjectPointOntoLine(5, 5, 0, 0, 0, 10, out double fr, out double fc);
            Near(fr, 0); Near(fc, 5);

            // 點已在線上 → 回自身
            GeometryConstruction.ProjectPointOntoLine(0, 7, 0, 0, 0, 10, out double fr2, out double fc2);
            Near(fr2, 0); Near(fc2, 7);

            // 垂直線 col=3：點 (4,9) → 垂足 (4,3)
            GeometryConstruction.ProjectPointOntoLine(4, 9, 0, 3, 10, 3, out double fr3, out double fc3);
            Near(fr3, 4); Near(fc3, 3);
        }

        private static void TestMidline()
        {
            // 平行兩線 row=0 與 row=10 → 置中線 row=5；線上各點到兩線垂距皆 = 5
            GeometryConstruction.Midline(
                0, 0, 0, 100,
                10, 0, 10, 100,
                out double r1, out double c1, out double r2, out double c2);
            AssertEquidistant(r1, c1, r2, c2, /*lineA*/0, 0, 0, 100, /*lineB*/10, 0, 10, 100, (mr, mc) =>
            {
                Near(PerpDistance(mr, mc, 0, 0, 0, 100), 5, 1e-6);
                Near(PerpDistance(mr, mc, 10, 0, 10, 100), 5, 1e-6);
            });

            // 相交兩線：row=0 與 col=0，交於 (0,0)。平分線上的點到兩線垂距相等。
            GeometryConstruction.Midline(
                0, -50, 0, 50,
                -50, 0, 50, 0,
                out double br1, out double bc1, out double br2, out double bc2);
            // 取平分線上兩個取樣點，驗證等距
            CheckEquidistantSample(br1, bc1, br2, bc2, 0, -50, 0, 50, -50, 0, 50, 0);
        }

        // 對 midline 端點之間取樣，套用 assertFn 驗證每個取樣點性質。
        private static void AssertEquidistant(double mr1, double mc1, double mr2, double mc2,
            double aR1, double aC1, double aR2, double aC2,
            double bR1, double bC1, double bR2, double bC2,
            Action<double, double> assertFn)
        {
            for (double t = 0.2; t <= 0.8; t += 0.3)
            {
                double mr = mr1 + t * (mr2 - mr1);
                double mc = mc1 + t * (mc2 - mc1);
                assertFn(mr, mc);
            }
        }

        private static void CheckEquidistantSample(double mr1, double mc1, double mr2, double mc2,
            double aR1, double aC1, double aR2, double aC2,
            double bR1, double bC1, double bR2, double bC2)
        {
            for (double t = 0.2; t <= 0.8; t += 0.3)
            {
                double mr = mr1 + t * (mr2 - mr1);
                double mc = mc1 + t * (mc2 - mc1);
                double da = PerpDistance(mr, mc, aR1, aC1, aR2, aC2);
                double db = PerpDistance(mr, mc, bR1, bC1, bR2, bC2);
                Near(da, db, 1e-6);
            }
        }

        private static double PerpDistance(double pr, double pc, double r1, double c1, double r2, double c2)
        {
            GeometryConstruction.ProjectPointOntoLine(pr, pc, r1, c1, r2, c2, out double fr, out double fc);
            double dr = pr - fr, dc = pc - fc;
            return Math.Sqrt(dr * dr + dc * dc);
        }

        private static void Near(double actual, double expected, double tol = 1e-6)
        {
            if (Math.Abs(actual - expected) > tol)
                throw new InvalidOperationException($"Expected {expected}, got {actual}");
        }
    }
}
