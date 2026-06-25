using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EllipseFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Halcon.EllipseFitting;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class EllipseFitterTests
    {
        public static void Run()
        {
            var fitter = new HalconEllipseFitter();

            // ── 1. 軸對齊橢圓（主軸沿 Column）→ Success + 參數正確 ──
            // r1=60(長半軸)、r2=30(短半軸)、phi=0。
            var pts = GenerateEllipsePoints(128, 128, 60, 30, 0.0, 48);
            EllipseFittingResult r = fitter.FitEllipse(pts, EllipseFittingParameters.Default());
            Assert(r.Success, "axis-aligned: Success");
            Assert(Math.Abs(r.CenterRow - 128.0) < 2.0, "axis-aligned: CenterRow ~128");
            Assert(Math.Abs(r.CenterColumn - 128.0) < 2.0, "axis-aligned: CenterCol ~128");
            Assert(Math.Abs(r.Radius1Px - 60.0) < 2.0, "axis-aligned: Radius1 ~60");
            Assert(Math.Abs(r.Radius2Px - 30.0) < 2.0, "axis-aligned: Radius2 ~30");
            Assert(r.ResidualRms < 1.0, "axis-aligned: RMS < 1px");

            // ── 2. 點數不足（< 5）→ fail ──
            var fewPts = new List<EdgePoint>
            {
                new EdgePoint { Row = 0, Column = 0 },
                new EdgePoint { Row = 10, Column = 0 },
                new EdgePoint { Row = 0, Column = 10 },
                new EdgePoint { Row = 10, Column = 10 }
            };
            EllipseFittingResult rFew = fitter.FitEllipse(fewPts, EllipseFittingParameters.Default());
            Assert(!rFew.Success, "too few points: !Success");

            // ── 3. null → fail ──
            EllipseFittingResult rNull = fitter.FitEllipse(null, EllipseFittingParameters.Default());
            Assert(!rNull.Success, "null points: !Success");

            // ── 4. 不支援的演算法 → fail ──
            var badParams = EllipseFittingParameters.Default();
            badParams.Algorithm = "bogus_algo";
            EllipseFittingResult rBad = fitter.FitEllipse(pts, badParams);
            Assert(!rBad.Success, "unsupported algorithm: !Success");

            // ── 5. 旋轉橢圓（phi=30°）→ 半軸與中心不受旋轉影響 ──
            double phi = 30.0 * Math.PI / 180.0;
            var rotPts = GenerateEllipsePoints(200, 90, 50, 25, phi, 48);
            EllipseFittingResult rRot = fitter.FitEllipse(rotPts, EllipseFittingParameters.Default());
            Assert(rRot.Success, "rotated: Success");
            Assert(Math.Abs(rRot.CenterRow - 200.0) < 2.0, "rotated: CenterRow ~200");
            Assert(Math.Abs(rRot.CenterColumn - 90.0) < 2.0, "rotated: CenterCol ~90");
            Assert(Math.Abs(rRot.Radius1Px - 50.0) < 2.0, "rotated: Radius1 ~50");
            Assert(Math.Abs(rRot.Radius2Px - 25.0) < 2.0, "rotated: Radius2 ~25");

            Console.WriteLine("EllipseFitterTests passed");
        }

        // 在橢圓主軸座標系產生點：xp=r1*cos(t)、yp=r2*sin(t)，再以 phi 旋轉回影像座標。
        // 與 HalconEllipseFitter.CalculateResiduals 的座標慣例一致：
        //   dc = xp*cos(phi) - yp*sin(phi)，dr = xp*sin(phi) + yp*cos(phi)，
        //   Row = cRow + dr、Column = cCol + dc。
        private static List<EdgePoint> GenerateEllipsePoints(
            double cRow, double cCol, double r1, double r2, double phiRad, int n)
        {
            double cosP = Math.Cos(phiRad);
            double sinP = Math.Sin(phiRad);
            var list = new List<EdgePoint>();
            for (int i = 0; i < n; i++)
            {
                double t = 2.0 * Math.PI * i / n;
                double xp = r1 * Math.Cos(t);
                double yp = r2 * Math.Sin(t);
                double dc = xp * cosP - yp * sinP;
                double dr = xp * sinP + yp * cosP;
                list.Add(new EdgePoint
                {
                    Row = cRow + dr,
                    Column = cCol + dc,
                    Amplitude = 50.0
                });
            }
            return list;
        }

        private static void Assert(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
    }
}
