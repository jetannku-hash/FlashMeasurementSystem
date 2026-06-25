using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.RectangleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Halcon.RectangleFitting;

namespace FlashMeasurementSystem.Tests.Halcon
{
    public static class RectangleFitterTests
    {
        public static void Run()
        {
            var fitter = new HalconRectangleFitter();

            // ── 1. 軸對齊矩形（Phi=0，Length1=80 沿 Col，Length2=50 沿 Row）→ Success ──
            var pts = GenerateRectanglePoints(128, 128, 80, 50, 0.0, 12);
            RectangleFittingResult r = fitter.FitRectangle(pts, RectangleFittingParameters.Default());
            Assert(r.Success, "axis-aligned: Success");
            Assert(Math.Abs(r.CenterRow - 128.0) < 3.0, "axis-aligned: CenterRow ~128");
            Assert(Math.Abs(r.CenterColumn - 128.0) < 3.0, "axis-aligned: CenterCol ~128");
            Assert(Math.Abs(r.Length1Px - 80.0) < 3.0, "axis-aligned: Length1 ~80");
            Assert(Math.Abs(r.Length2Px - 50.0) < 3.0, "axis-aligned: Length2 ~50");
            Assert(r.ResidualRms < 1.0, "axis-aligned: RMS < 1px");

            // ── 2. 點數不足（< 8）→ fail ──
            var fewPts = new List<EdgePoint>
            {
                new EdgePoint { Row = 0, Column = 0 },
                new EdgePoint { Row = 0, Column = 10 },
                new EdgePoint { Row = 10, Column = 0 },
                new EdgePoint { Row = 10, Column = 10 },
                new EdgePoint { Row = 0, Column = 5 },
                new EdgePoint { Row = 10, Column = 5 },
                new EdgePoint { Row = 5, Column = 0 },
            };
            RectangleFittingResult rFew = fitter.FitRectangle(fewPts, RectangleFittingParameters.Default());
            Assert(!rFew.Success, "too few points: !Success");

            // ── 3. null → fail ──
            RectangleFittingResult rNull = fitter.FitRectangle(null, RectangleFittingParameters.Default());
            Assert(!rNull.Success, "null points: !Success");

            // ── 4. 不支援的演算法 → fail ──
            var badParams = RectangleFittingParameters.Default();
            badParams.Algorithm = "bogus_algo";
            RectangleFittingResult rBad = fitter.FitRectangle(pts, badParams);
            Assert(!rBad.Success, "unsupported algorithm: !Success");

            // ── 5. 旋轉矩形（Phi=15°）→ 中心與邊長仍正確 ──
            double phi = 15.0 * Math.PI / 180.0;
            var rotPts = GenerateRectanglePoints(200, 90, 70, 40, phi, 12);
            RectangleFittingResult rRot = fitter.FitRectangle(rotPts, RectangleFittingParameters.Default());
            Assert(rRot.Success, "rotated: Success");
            Assert(Math.Abs(rRot.CenterRow - 200.0) < 3.0, "rotated: CenterRow ~200");
            Assert(Math.Abs(rRot.CenterColumn - 90.0) < 3.0, "rotated: CenterCol ~90");
            Assert(Math.Abs(rRot.Length1Px - 70.0) < 3.0, "rotated: Length1 ~70");
            Assert(Math.Abs(rRot.Length2Px - 40.0) < 3.0, "rotated: Length2 ~40");

            Console.WriteLine("RectangleFitterTests passed");
        }

        // 產生矩形四邊均勻分佈的點集：依序走四條邊（上、右、下、左），
        // 每邊 nPerSide 點。點從矩形的四個角開始，回到起點，形成封閉輪廓。
        // 旋轉公式與 HalconEllipseFitter.CalculateResiduals / HalconRectangleFitter 一致：
        //   dc = xp*cos(phi) - yp*sin(phi)，dr = xp*sin(phi) + yp*cos(phi)。
        private static List<EdgePoint> GenerateRectanglePoints(
            double cRow, double cCol, double l1, double l2, double phiRad, int nPerSide)
        {
            double cosP = Math.Cos(phiRad);
            double sinP = Math.Sin(phiRad);
            var list = new List<EdgePoint>();

            // 矩形四個角在局部座標（l1,l2→右下, -l1,l2→左下, -l1,-l2→左上, l1,-l2→右上）。
            double[][] corners = new[]
            {
                new[] {  l1,  l2 },  // 右下
                new[] { -l1,  l2 },  // 左下
                new[] { -l1, -l2 },  // 左上
                new[] {  l1, -l2 },  // 右上
            };

            // 四條邊：corner[i] → corner[(i+1)%4]。
            for (int side = 0; side < 4; side++)
            {
                double x0 = corners[side][0], y0 = corners[side][1];
                double x1 = corners[(side + 1) % 4][0], y1 = corners[(side + 1) % 4][1];

                for (int j = 0; j < nPerSide; j++)
                {
                    double t = (double)j / nPerSide;
                    double xp = x0 + (x1 - x0) * t;
                    double yp = y0 + (y1 - y0) * t;
                    double dc = xp * cosP - yp * sinP;
                    double dr = xp * sinP + yp * cosP;
                    list.Add(new EdgePoint
                    {
                        Row = cRow + dr,
                        Column = cCol + dc,
                        Amplitude = 50.0
                    });
                }
            }
            return list;
        }

        private static void Assert(bool cond, string msg) { if (!cond) throw new InvalidOperationException(msg); }
    }
}
