using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Domain.MetrologyModel;
using FlashMeasurementSystem.Halcon.CoordinateSystem;
using FlashMeasurementSystem.Halcon.MetrologyModel;
using HalconDotNet;

namespace FlashMeasurementSystem.Tests.Halcon
{
    /// <summary>
    /// MET2D-02：四種形狀在合成圖（已知像素真值）上的擬合精度 + 量測點非空 + Score。
    /// MET2D-04：單次 Apply 處理多物件（line + circle + ellipse → 3 成功）。
    /// 穩健性：多通道單通道保護；MeasureLength1 違規 → 該物件失敗、不丟例外中斷整批。
    /// 對齊：TestAlignmentToMatchedPose 覆蓋「有匹配姿態」的對齊路徑（CreateFromMatch→TransformRoi
    /// 預轉標稱幾何，再絕對座標 Apply）——先前的驗證洞。其餘基礎擬合測試標稱幾何即絕對座標。
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
            TestAlignmentToMatchedPose(runner);
            TestAlignmentRectangleRotated(runner);
            TestAlignmentLineRotated(runner);
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

        // 對齊路徑（補「驗證洞」：先前 Wave 2 全部 hasReferencePose=false，從未測有姿態的對齊，
        // 正是 5ce575b 把對齊改壞卻無測試攔截的根因）。此測重現 RecipeRunner 6713fcc 的真實流程：
        // 用與 1D 相同的剛體變換 CreateFromMatch→TransformRoi 把標稱幾何預轉到匹配姿態，再以絕對
        // 座標 Apply(hasReferencePose=false/hasMatch=false)。
        //
        // 標稱圓在 master (128,128)；工件實際畫在 (100,150)，姿態帶 30° 旋轉分量。vector_angle_to_rigid
        // 保證 ref 點精確映到 cur 點，故 transform(master)=part（與旋轉角無關），可獨立斷言而不需自算
        // HALCON 旋轉慣例。若對齊被忽略（幾何停在 master 空白處），量測區找不到圓 → 擬合失敗，測試變紅。
        private static void TestAlignmentToMatchedPose(HalconMetrologyModelRunner runner)
        {
            var mapper = new HalconCoordinateMapper();
            const double masterRow = 128, masterCol = 128, radius = 40;
            const double partRow = 100, partCol = 150;   // 工件實際位置（ground truth）
            double angle = Math.PI / 6.0;                 // 帶旋轉分量的姿態

            RigidTransform transform = mapper.CreateFromMatch(masterRow, masterCol, 0.0, partRow, partCol, angle);
            TransformedRoi tc = mapper.TransformRoi(masterRow, masterCol, 0.0, transform);   // 生產端預轉
            Assert(Math.Abs(tc.Row - partRow) < 1.0 && Math.Abs(tc.Col - partCol) < 1.0,
                "aligned centre maps master->part (got " + tc.Row.ToString("F1") + "," + tc.Col.ToString("F1") + ")");
            Assert(Math.Abs(tc.Row - masterRow) > 5 || Math.Abs(tc.Col - masterCol) > 5,
                "alignment actually moved geometry off master (regression guard)");

            using (HImage img = TestImageGenerator.CreateCircleImage(256, 256, (int)partRow, (int)partCol, (int)radius))
            {
                var model = OneObject(new MetrologyObjectDef
                {
                    Id = "aligned", Name = "aligned-circle", Shape = MetrologyObjectType.Circle,
                    Row = tc.Row, Column = tc.Col, Radius = radius, MeasureLength1 = 12
                });
                MetrologyObjectResult r = ApplyOne(runner, model, img);
                Assert(r.Success, "aligned circle Success (" + r.ErrorMessage + ")");
                Assert(r.Score >= 0.6, "aligned circle Score >= 0.6 (got " + r.Score.ToString("F2") + ")");
                Assert(Math.Abs(r.FitRow - partRow) < 1.5, "aligned FitRow ~100 (got " + r.FitRow.ToString("F2") + ")");
                Assert(Math.Abs(r.FitColumn - partCol) < 1.5, "aligned FitColumn ~150 (got " + r.FitColumn.ToString("F2") + ")");
                Assert(Math.Abs(r.FitRadius - radius) < 1.5, "aligned FitRadius ~40 (got " + r.FitRadius.ToString("F2") + ")");
            }
        }

        // 對齊路徑 — 旋轉的「有方向」形狀（rectangle/ellipse 共用 center+Phi 變換路徑）。
        // TestAlignmentToMatchedPose 只用圓（無 Phi），故 Phi 隨姿態旋轉這條從未測到。
        //
        // 非循環設計：同一個 hom_mat2d 走兩條不同程式路徑，必須一致 fit 才會中——
        //   ① 影像：AffineTransImage 把 master 矩形實際轉成 part（方向由矩陣決定）。
        //   ② 標稱：TransformRoi 的角度公式（refPhi + RotationRad）。
        // 若 TransformRoi 漏轉 Phi 或角度符號錯，標稱方向對不上轉過的影像 → 量測區落在旋轉邊外
        // → 擬合失敗，測試變紅。Phi 比較用 sin(Δ) 容許矩形軸 π 翻轉的等價歧義。
        private static void TestAlignmentRectangleRotated(HalconMetrologyModelRunner runner)
        {
            var mapper = new HalconCoordinateMapper();
            const double masterRow = 128, masterCol = 128, l1 = 60, l2 = 40;
            const double partRow = 120, partCol = 140;
            double theta = 20.0 * Math.PI / 180.0;   // 帶方向的姿態

            RigidTransform t = mapper.CreateFromMatch(masterRow, masterCol, 0.0, partRow, partCol, theta);
            TransformedRoi tr = mapper.TransformRoi(masterRow, masterCol, 0.0, t);   // 生產端預轉：center + Phi

            using (HImage master = TestImageGenerator.CreateRectangleImage(256, 256, (int)masterRow, (int)masterCol, 0.0, (int)l1, (int)l2))
            using (HImage part = master.AffineTransImage(new HHomMat2D(new HTuple(t.HomMat2D)), "bilinear", "false"))
            {
                var model = OneObject(new MetrologyObjectDef
                {
                    Id = "rot-rect", Name = "rotated-rect", Shape = MetrologyObjectType.Rectangle,
                    Row = tr.Row, Column = tr.Col, Phi = tr.AngleRad, Length1 = l1, Length2 = l2,
                    MeasureLength1 = 15
                });
                MetrologyObjectResult r = ApplyOne(runner, model, part);
                Assert(r.Success, "rotated rect Success (" + r.ErrorMessage + ")");
                Assert(Math.Abs(r.FitRow - partRow) < 1.5, "rotated rect FitRow ~120 (got " + r.FitRow.ToString("F2") + ")");
                Assert(Math.Abs(r.FitColumn - partCol) < 1.5, "rotated rect FitColumn ~140 (got " + r.FitColumn.ToString("F2") + ")");
                // sin(Δ)≈0 於 Δ=0 與 Δ=π：既確認方向跟隨姿態旋轉、又容許矩形長軸的 π 等價翻轉。
                Assert(Math.Abs(Math.Sin(r.FitPhi - tr.AngleRad)) < 0.08,
                    "rotated rect FitPhi follows pose (fit " + r.FitPhi.ToString("F3") + " vs nominal " + tr.AngleRad.ToString("F3") + ")");
                Assert(Math.Abs(r.FitLength1 - l1) < 2.0, "rotated rect FitLength1 ~60 (got " + r.FitLength1.ToString("F2") + ")");
                Assert(Math.Abs(r.FitLength2 - l2) < 2.0, "rotated rect FitLength2 ~40 (got " + r.FitLength2.ToString("F2") + ")");
            }
        }

        // 對齊路徑 — 旋轉的直線（line 走「兩端點各自變換」路徑，與 center+Phi 不同）。
        // 純旋轉（part center = master center）把整條階梯邊繞影像中心轉；標稱兩端點經 TransformRoi
        // 預轉後仍落在轉過的邊上，量測到才代表兩端點變換與方向都對。
        private static void TestAlignmentLineRotated(HalconMetrologyModelRunner runner)
        {
            var mapper = new HalconCoordinateMapper();
            double theta = 20.0 * Math.PI / 180.0;

            RigidTransform t = mapper.CreateFromMatch(128, 128, 0.0, 128, 128, theta);   // 繞中心純旋轉
            TransformedRoi b = mapper.TransformRoi(50, 128, 0.0, t);
            TransformedRoi e = mapper.TransformRoi(200, 128, 0.0, t);

            using (HImage master = TestImageGenerator.CreateEdgeImage(256, 256))
            using (HImage part = master.AffineTransImage(new HHomMat2D(new HTuple(t.HomMat2D)), "bilinear", "false"))
            {
                var model = OneObject(new MetrologyObjectDef
                {
                    Id = "rot-line", Name = "rotated-line", Shape = MetrologyObjectType.Line,
                    RowBegin = b.Row, ColumnBegin = b.Col, RowEnd = e.Row, ColumnEnd = e.Col,
                    MeasureLength1 = 30
                });
                MetrologyObjectResult r = ApplyOne(runner, model, part);
                Assert(r.Success, "rotated line Success (" + r.ErrorMessage + ")");
                Assert(r.MeasurePointRows.Count > 0, "rotated line measure points non-empty");

                // HALCON line metrology 的端點是「擬合線段在量測區內的終點」，沿線方向會內縮，
                // 不等於標稱端點（同 TestLine 只驗 column、不驗 row 端點）。故驗真正被釘住的量：
                // 擬合線與變換後標稱線「共線」＝方向一致 + 垂直距離≈0，證明兩端點變換與方向都跟隨姿態。
                double ndr = e.Row - b.Row, ndc = e.Col - b.Col;
                double nlen = Math.Sqrt(ndr * ndr + ndc * ndc);
                ndr /= nlen; ndc /= nlen;                                  // 標稱線單位方向
                double fdr = r.FitRowEnd - r.FitRowBegin, fdc = r.FitColumnEnd - r.FitColumnBegin;
                double flen = Math.Sqrt(fdr * fdr + fdc * fdc);
                double sinBetween = (ndr * fdc - ndc * fdr) / flen;       // 兩方向夾角 sin（π 翻轉等價）
                Assert(Math.Abs(sinBetween) < 0.05,
                    "rotated line direction follows pose (sin=" + sinBetween.ToString("F3") + ")");
                double perp = Math.Abs((r.FitRowBegin - b.Row) * ndc - (r.FitColumnBegin - b.Col) * ndr);
                Assert(perp < 2.0,
                    "rotated line collinear with transformed nominal (perp=" + perp.ToString("F2") + "px)");
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
