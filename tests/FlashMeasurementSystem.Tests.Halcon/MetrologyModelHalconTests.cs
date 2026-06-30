using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.MetrologyModel;
using FlashMeasurementSystem.Halcon.MetrologyModel;
using HalconDotNet;

namespace FlashMeasurementSystem.Tests.Halcon
{
    /// <summary>
    /// MET2D-02：四種形狀在合成圖（已知像素真值）上的擬合精度 + 量測點非空 + Score。
    /// MET2D-04：單次 Apply 處理多物件（line + circle + ellipse → 3 成功）。
    /// 穩健性：多通道單通道保護；MeasureLength1 違規 → 該物件失敗、不丟例外中斷整批。
    /// 所有合成測試 hasReferencePose=false / hasMatch=false（標稱幾何即絕對影像座標）。
    /// 容差帶：合成離散形狀的子像素邊界本有 ~1px 量化偏差，圓/橢圓半徑與矩形邊長用 ±1px，
    /// 中心 ±1px、角度 ±0.03 rad（較 VALIDATION 名目值略放寬以避免離散化造成的偽失敗）。
    /// </summary>
    public static class MetrologyModelHalconTests
    {
        public static void Run()
        {
            var runner = new HalconMetrologyModelRunner();
            TestCircle(runner);
            TestLine(runner);
            TestRectangle(runner);
            TestEllipse(runner);
            TestRgbGuard(runner);
            TestMultiFeature(runner);
            TestBadMeasureLength(runner);
            Console.WriteLine("MetrologyModelHalconTests passed");
        }

        private static void TestCircle(HalconMetrologyModelRunner runner)
        {
            using (HImage img = TestImageGenerator.CreateCircleImage(256, 256, 128, 128, 50))
            {
                var model = OneObject(new MetrologyObjectDef
                {
                    Id = "c", Name = "circle", Shape = MetrologyObjectType.Circle,
                    Row = 128, Column = 128, Radius = 50, MeasureLength1 = 15
                });
                MetrologyObjectResult r = ApplyOne(runner, model, img);
                Assert(r.Success, "circle Success");
                Assert(r.Score >= 0.6, "circle Score >= 0.6 (got " + r.Score.ToString("F2") + ")");
                Assert(Math.Abs(r.FitRow - 128) < 1.0, "circle FitRow ~128 (got " + r.FitRow.ToString("F2") + ")");
                Assert(Math.Abs(r.FitColumn - 128) < 1.0, "circle FitColumn ~128 (got " + r.FitColumn.ToString("F2") + ")");
                Assert(Math.Abs(r.FitRadius - 50) < 1.0, "circle FitRadius ~50 (got " + r.FitRadius.ToString("F2") + ")");
                Assert(r.MeasurePointRows.Count > 0, "circle measure points non-empty");
            }
        }

        private static void TestLine(HalconMetrologyModelRunner runner)
        {
            // 垂直階梯邊（左暗右亮，邊在 col≈127.5）→ 沿 col 128 放標稱垂直線。
            using (HImage img = TestImageGenerator.CreateEdgeImage(256, 256))
            {
                var model = OneObject(new MetrologyObjectDef
                {
                    Id = "l", Name = "line", Shape = MetrologyObjectType.Line,
                    RowBegin = 50, ColumnBegin = 128, RowEnd = 200, ColumnEnd = 128,
                    MeasureLength1 = 30
                });
                MetrologyObjectResult r = ApplyOne(runner, model, img);
                Assert(r.Success, "line Success");
                Assert(Math.Abs(r.FitColumnBegin - 127.5) < 1.0, "line begin col ~127.5 (got " + r.FitColumnBegin.ToString("F2") + ")");
                Assert(Math.Abs(r.FitColumnEnd - 127.5) < 1.0, "line end col ~127.5 (got " + r.FitColumnEnd.ToString("F2") + ")");
                Assert(r.MeasurePointRows.Count > 0, "line measure points non-empty");
            }
        }

        private static void TestRectangle(HalconMetrologyModelRunner runner)
        {
            using (HImage img = TestImageGenerator.CreateRectangleImage(256, 256, 128, 128, 0.0, 60, 40))
            {
                var model = OneObject(new MetrologyObjectDef
                {
                    Id = "r", Name = "rect", Shape = MetrologyObjectType.Rectangle,
                    Row = 128, Column = 128, Phi = 0.0, Length1 = 60, Length2 = 40,
                    MeasureLength1 = 15
                });
                MetrologyObjectResult r = ApplyOne(runner, model, img);
                Assert(r.Success, "rect Success");
                Assert(Math.Abs(r.FitRow - 128) < 1.0, "rect FitRow ~128 (got " + r.FitRow.ToString("F2") + ")");
                Assert(Math.Abs(r.FitColumn - 128) < 1.0, "rect FitColumn ~128 (got " + r.FitColumn.ToString("F2") + ")");
                Assert(Math.Abs(r.FitPhi) < 0.03, "rect FitPhi ~0 (got " + r.FitPhi.ToString("F3") + ")");
                Assert(Math.Abs(r.FitLength1 - 60) < 1.5, "rect FitLength1 ~60 (got " + r.FitLength1.ToString("F2") + ")");
                Assert(Math.Abs(r.FitLength2 - 40) < 1.5, "rect FitLength2 ~40 (got " + r.FitLength2.ToString("F2") + ")");
            }
        }

        private static void TestEllipse(HalconMetrologyModelRunner runner)
        {
            using (HImage img = TestImageGenerator.CreateEllipseImage(256, 256, 128, 128, 0.0, 60, 35))
            {
                var model = OneObject(new MetrologyObjectDef
                {
                    Id = "e", Name = "ellipse", Shape = MetrologyObjectType.Ellipse,
                    Row = 128, Column = 128, Phi = 0.0, Radius1 = 60, Radius2 = 35,
                    MeasureLength1 = 15
                });
                MetrologyObjectResult r = ApplyOne(runner, model, img);
                Assert(r.Success, "ellipse Success");
                Assert(Math.Abs(r.FitRow - 128) < 1.5, "ellipse FitRow ~128 (got " + r.FitRow.ToString("F2") + ")");
                Assert(Math.Abs(r.FitColumn - 128) < 1.5, "ellipse FitColumn ~128 (got " + r.FitColumn.ToString("F2") + ")");
                Assert(Math.Abs(r.FitRadius1 - 60) < 1.5, "ellipse FitRadius1 ~60 (got " + r.FitRadius1.ToString("F2") + ")");
                Assert(Math.Abs(r.FitRadius2 - 35) < 1.5, "ellipse FitRadius2 ~35 (got " + r.FitRadius2.ToString("F2") + ")");
            }
        }

        private static void TestRgbGuard(HalconMetrologyModelRunner runner)
        {
            // 在 RGB 三通道影像上仍能擬合圓 → 證明 Rgb1ToGray 保護有效（否則靜默回零結果）。
            using (HImage gray = TestImageGenerator.CreateCircleImage(256, 256, 128, 128, 50))
            using (HImage rgb = gray.Compose3(gray, gray))
            {
                var model = OneObject(new MetrologyObjectDef
                {
                    Id = "c", Shape = MetrologyObjectType.Circle,
                    Row = 128, Column = 128, Radius = 50, MeasureLength1 = 15
                });
                MetrologyObjectResult r = ApplyOne(runner, model, rgb);
                Assert(r.Success, "RGB-guarded circle Success");
                Assert(Math.Abs(r.FitRadius - 50) < 1.0, "RGB-guarded circle radius ~50");
            }
        }

        private static void TestMultiFeature(HalconMetrologyModelRunner runner)
        {
            using (HImage img = TestImageGenerator.CreateCompositeImage(256, 256))
            {
                var model = new MetrologyModelDef
                {
                    ImageWidth = 256, ImageHeight = 256,
                    Objects = new List<MetrologyObjectDef>
                    {
                        new MetrologyObjectDef
                        {
                            Id = "line", Shape = MetrologyObjectType.Line,
                            RowBegin = TestImageGenerator.CompLineRow, ColumnBegin = TestImageGenerator.CompLineColBegin,
                            RowEnd = TestImageGenerator.CompLineRow, ColumnEnd = TestImageGenerator.CompLineColEnd,
                            MeasureLength1 = 10
                        },
                        new MetrologyObjectDef
                        {
                            Id = "circle", Shape = MetrologyObjectType.Circle,
                            Row = TestImageGenerator.CompCircleRow, Column = TestImageGenerator.CompCircleCol,
                            Radius = TestImageGenerator.CompCircleRadius, MeasureLength1 = 12
                        },
                        new MetrologyObjectDef
                        {
                            Id = "ellipse", Shape = MetrologyObjectType.Ellipse,
                            Row = TestImageGenerator.CompEllipseRow, Column = TestImageGenerator.CompEllipseCol,
                            Phi = TestImageGenerator.CompEllipsePhi,
                            Radius1 = TestImageGenerator.CompEllipseR1, Radius2 = TestImageGenerator.CompEllipseR2,
                            MeasureLength1 = 12
                        }
                    }
                };

                MetrologyModelResult res = runner.Apply(model, 0, 0, 0, false, img, 0, 0, 0, false);
                Assert(res.Objects.Count == 3, "multi-feature returns 3 results (got " + res.Objects.Count + ")");
                for (int i = 0; i < res.Objects.Count; i++)
                    Assert(res.Objects[i].Success, "multi-feature object[" + i + "] Success (" + res.Objects[i].ErrorMessage + ")");
            }
        }

        private static void TestBadMeasureLength(HalconMetrologyModelRunner runner)
        {
            // MeasureLength1 (60) >= Radius (50) → 該物件失敗、整批不丟例外。
            using (HImage img = TestImageGenerator.CreateCircleImage(256, 256, 128, 128, 50))
            {
                var model = OneObject(new MetrologyObjectDef
                {
                    Id = "bad", Shape = MetrologyObjectType.Circle,
                    Row = 128, Column = 128, Radius = 50, MeasureLength1 = 60
                });
                MetrologyModelResult res = runner.Apply(model, 0, 0, 0, false, img, 0, 0, 0, false);
                Assert(res.Objects.Count == 1, "bad-ML1 returns one result");
                Assert(!res.Objects[0].Success, "bad-ML1 object Success == false");
                Assert(!string.IsNullOrEmpty(res.Objects[0].ErrorMessage), "bad-ML1 has error message");
            }
        }

        private static MetrologyModelDef OneObject(MetrologyObjectDef obj)
        {
            return new MetrologyModelDef
            {
                ImageWidth = 256,
                ImageHeight = 256,
                Objects = new List<MetrologyObjectDef> { obj }
            };
        }

        private static MetrologyObjectResult ApplyOne(HalconMetrologyModelRunner runner, MetrologyModelDef model, HImage img)
        {
            MetrologyModelResult res = runner.Apply(model, 0, 0, 0, false, img, 0, 0, 0, false);
            if (res.Objects.Count != 1)
                throw new InvalidOperationException("MetrologyModelHalconTests: expected 1 result, got "
                    + res.Objects.Count + (string.IsNullOrEmpty(res.ErrorMessage) ? "" : " — " + res.ErrorMessage));
            return res.Objects[0];
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("MetrologyModelHalconTests: " + message);
        }
    }
}
