using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application;
using FlashMeasurementSystem.Application.AngleMeasurement;
using FlashMeasurementSystem.Application.CircleFitting;
using FlashMeasurementSystem.Application.CoordinateSystem;
using FlashMeasurementSystem.Application.DistanceMeasurement;
using FlashMeasurementSystem.Application.EdgeDetection;
using FlashMeasurementSystem.Application.LineFitting;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Domain.DistanceMeasurement;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.LineFitting;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Infrastructure.Tolerance;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// RecipeRunner 的編排邏輯測試。這是 2026-07-21 深度審查點名的結構性驗證洞的補漏：
    /// RecipeRunner/MeasurementWorkflow 原本住在 App.Wpf、測試專案觸不到，P0 的 OK/NG 漂移
    /// 才會躲過 40 套綠燈。搬進 Application 後（refactor/move-runner-to-application），
    /// 以 fake adapter 餵罐頭 Domain 結果 + 真的 ToleranceJudger，鎖住量測→判定的編排。
    /// 影像型別以 object 具現（RecipeRunner 只把它轉交注入的 fake，不存取成員）。
    /// </summary>
    public static class RecipeRunnerDomainTests
    {
        public static void Run()
        {
            CircleWithinToleranceIsOk();
            CircleOutOfToleranceIsNg();
            InsufficientEdgesNotMeasured();
            CircleFitFailureNotMeasured();
            EachToolJudgedAgainstItsOwnTolerance();
            LineWithDegToleranceJudged();
            ArcToolCountsEdgesAndJudges();
            DistanceToolComposesTwoElements();
            AngleToolComposesTwoLines();
            UnsupportedToolTypeMarkedUnsupported();

            Console.WriteLine("RecipeRunnerDomainTests passed");
        }

        // 圓在公差內：量得直徑、判定 OK。
        private static void CircleWithinToleranceIsOk()
        {
            var runner = MakeRunner(
                Edges(5),                                   // 足量邊點
                Circle(diameterPx: 10.0),                   // 擬合成功、直徑 10px
                Line(success: false));
            Recipe recipe = OneCircle("D10", nominal: 10.0, plusMinus: 0.5);

            List<ToolRunResult> results = runner.Run(recipe, new object(),
                hasMatch: false, 0, 0, 0, pixelSizeUmX: 1000.0, pixelSizeUmY: 1000.0);

            AssertEqual(1, results.Count, "circle in-tol: one result");
            ToolRunResult r = results[0];
            AssertEqual(true, r.Supported, "circle in-tol: supported");
            AssertEqual(true, r.Measured, "circle in-tol: measured");
            AssertClose(10.0, r.DiameterMm, 1e-9, "circle in-tol: diameter mm (10px * 1000um / 1000)");
            AssertEqual(true, r.IsOk, "circle in-tol: IsOk true");
        }

        // 圓超出公差：仍量得直徑，但判定 NG（IsOk=false，非 null）。
        private static void CircleOutOfToleranceIsNg()
        {
            var runner = MakeRunner(Edges(5), Circle(diameterPx: 10.0), Line(success: false));
            Recipe recipe = OneCircle("D10", nominal: 5.0, plusMinus: 0.1);  // 量到 10、標稱 5±0.1 → 超規

            ToolRunResult r = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0)[0];

            AssertEqual(true, r.Measured, "circle out-of-tol: measured");
            AssertEqual(false, r.IsOk, "circle out-of-tol: IsOk false (not null)");
        }

        // 邊點不足（<3）：不視為量得，ValueText 為「邊緣不足」，IsOk 維持 null。
        private static void InsufficientEdgesNotMeasured()
        {
            var runner = MakeRunner(Edges(2), Circle(diameterPx: 10.0), Line(success: false));
            Recipe recipe = OneCircle("D10", nominal: 10.0, plusMinus: 0.5);

            ToolRunResult r = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0)[0];

            AssertEqual(false, r.Measured, "insufficient edges: not measured");
            AssertEqual("邊緣不足", r.ValueText, "insufficient edges: ValueText");
            AssertEqual((bool?)null, r.IsOk, "insufficient edges: IsOk null");
        }

        // 邊點足夠但圓擬合失敗：不視為量得，ValueText 為「擬合失敗」。
        private static void CircleFitFailureNotMeasured()
        {
            var runner = MakeRunner(Edges(5), Circle(diameterPx: 0.0, success: false), Line(success: false));
            Recipe recipe = OneCircle("D10", nominal: 10.0, plusMinus: 0.5);

            ToolRunResult r = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0)[0];

            AssertEqual(false, r.Measured, "fit failure: not measured");
            AssertEqual("擬合失敗", r.ValueText, "fit failure: ValueText");
        }

        // 兩個圓工具量到相同直徑，但公差不同 → 各自對自己的公差判定（非全部套第一個）。
        private static void EachToolJudgedAgainstItsOwnTolerance()
        {
            var runner = MakeRunner(Edges(5), Circle(diameterPx: 10.0), Line(success: false));
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(CircleTool("t1", "D10-loose", nominal: 10.0, plusMinus: 0.5));  // 含 10 → OK
            recipe.Tools.Add(CircleTool("t2", "D10-tight", nominal: 5.0, plusMinus: 0.1));   // 不含 10 → NG

            List<ToolRunResult> results = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0);

            AssertEqual(2, results.Count, "per-tool tol: two results");
            AssertEqual(true, results[0].IsOk, "per-tool tol: tool1 judged against its own (loose) tol → OK");
            AssertEqual(false, results[1].IsOk, "per-tool tol: tool2 judged against its own (tight) tol → NG");
        }

        // 線工具、deg 公差：走線量測 + 環狀角度判定。
        private static void LineWithDegToleranceJudged()
        {
            var runner = MakeRunner(
                Edges(5),
                Circle(diameterPx: 0.0, success: false),
                Line(success: true, angleDeg: 90.0));
            var recipe = new Recipe { HasReferencePose = false };
            var tool = new MeasurementTool
            {
                Id = "L1",
                Name = "line90",
                ToolType = "line",
                Roi = new RoiGeometry { CenterRow = 100, CenterCol = 100, Length1 = 50, Length2 = 20, AngleRad = 0 },
                Tolerance = new ToleranceSpec { Nominal = 90.0, LowerTolerance = -1.0, UpperTolerance = 1.0, Unit = "deg" }
            };
            recipe.Tools.Add(tool);

            ToolRunResult r = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0)[0];

            AssertEqual(true, r.Measured, "line deg: measured");
            AssertEqual(true, r.IsOk, "line deg: 90 within 90±1 → OK");
        }

        // 弧形卡尺：量圓周邊數（DetectEdgesOnArc）→ 邊數計數 + 判定。ArcEdgeRows 是報表值來源，
        // GetMeasuredValue 對 arc 取 ArcEdgeRows.Count（歷史陷阱：取錯量會讓 CSV 與畫面矛盾）。
        private static void ArcToolCountsEdgesAndJudges()
        {
            var runner = MakeRunner(Edges(6), Circle(0, success: false), Line(success: false));
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(ArcTool("a1", "gear-teeth", nominalCount: 6, plusMinusCount: 0));

            ToolRunResult r = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0)[0];

            AssertEqual("arc", r.ToolType, "arc: tool type");
            AssertEqual(true, r.Measured, "arc: measured");
            AssertEqual(6, r.ArcEdgeRows.Count, "arc: edge count captured");
            AssertEqual("邊數=6", r.ValueText, "arc: ValueText is edge count");
            AssertEqual(true, r.IsOk, "arc: 6 within 6±0 → OK");
        }

        // 距離（複合工具）：解析兩個已量測元素 → measurer → DistMm → 判定。測 byId 組合接線。
        private static void DistanceToolComposesTwoElements()
        {
            var runner = MakeRunnerFull(
                Edges(5), Circle(diameterPx: 10.0), Line(success: false),
                dist: new DistanceMeasurementResult { Success = true, DistanceMm = 5.0 },
                angle: null);

            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(CircleTool("c1", "hole-A", 10.0, 0.5));
            recipe.Tools.Add(CircleTool("c2", "hole-B", 10.0, 0.5));
            recipe.Tools.Add(DistanceTool("d1", "AB", refA: "c1", refB: "c2", nominal: 5.0, plusMinus: 0.5));

            List<ToolRunResult> results = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0);
            ToolRunResult d = results.Find(x => x.ToolType == "distance");

            AssertTrue(d != null, "distance: result present");
            AssertEqual(true, d.Measured, "distance: measured");
            AssertClose(5.0, d.DistMm, 1e-9, "distance: DistMm from measurer");
            AssertEqual(true, d.IsOk, "distance: 5 within 5±0.5 → OK");
        }

        // 角度（複合工具）：兩條 line 元素 → angle measurer → AngleDeg → 判定。
        private static void AngleToolComposesTwoLines()
        {
            var runner = MakeRunnerFull(
                Edges(5), Circle(0, success: false), Line(success: true, angleDeg: 0.0),
                dist: null,
                angle: new AngleMeasurementResult { Success = true, AcuteAngleDeg = 30.0 });

            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(LineTool("l1", "edge-1"));
            recipe.Tools.Add(LineTool("l2", "edge-2"));
            recipe.Tools.Add(AngleTool("g1", "corner", refA: "l1", refB: "l2", nominal: 30.0, plusMinus: 1.0));

            List<ToolRunResult> results = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0);
            ToolRunResult g = results.Find(x => x.ToolType == "angle");

            AssertTrue(g != null, "angle: result present");
            AssertEqual(true, g.Measured, "angle: measured");
            AssertClose(30.0, g.AngleDeg, 1e-9, "angle: AngleDeg from measurer");
            AssertEqual(true, g.IsOk, "angle: 30 within 30±1 → OK");
        }

        // 未知型別：標為不支援（Supported=false、IsOk=null），不擋其他工具、不假 PASS。
        private static void UnsupportedToolTypeMarkedUnsupported()
        {
            var runner = MakeRunner(Edges(5), Circle(0, success: false), Line(success: false));
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(new MeasurementTool { Id = "x1", Name = "mystery", ToolType = "wibble" });

            ToolRunResult r = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0)[0];

            AssertEqual(false, r.Supported, "unsupported: Supported=false");
            AssertEqual((bool?)null, r.IsOk, "unsupported: IsOk null");
            AssertEqual("(未支援)", r.ValueText, "unsupported: ValueText");
        }

        // ─── builders ─────────────────────────────────────────────

        private static RecipeRunner<object, object> MakeRunner(
            EdgeResult edges, CircleFittingResult circle, LineFittingResult line)
        {
            return new RecipeRunner<object, object>(
                new FakeEdgeDetector(edges),
                new FakeCircleFitter(circle),
                new FakeLineFitter(line),
                new ThrowingDistanceMeasurer(),
                new ThrowingAngleMeasurer(),
                new ToleranceJudger(),          // 真判定器：測到真正的公差邏輯
                new ThrowingMapper());          // HasReferencePose=false → transform=null → 不應被呼叫
        }

        // 需要 distance / angle measurer 的複合工具測試用；null → 沿用 throwing（證明該路徑未被觸及）。
        private static RecipeRunner<object, object> MakeRunnerFull(
            EdgeResult edges, CircleFittingResult circle, LineFittingResult line,
            DistanceMeasurementResult dist, AngleMeasurementResult angle)
        {
            return new RecipeRunner<object, object>(
                new FakeEdgeDetector(edges),
                new FakeCircleFitter(circle),
                new FakeLineFitter(line),
                dist != null ? (IDistanceMeasurer<object>)new FakeDistanceMeasurer(dist) : new ThrowingDistanceMeasurer(),
                angle != null ? (IAngleMeasurer)new FakeAngleMeasurer(angle) : new ThrowingAngleMeasurer(),
                new ToleranceJudger(),
                new ThrowingMapper());
        }

        private static MeasurementTool ArcTool(string id, string name, int nominalCount, int plusMinusCount)
        {
            return new MeasurementTool
            {
                Id = id,
                Name = name,
                ToolType = "arc",
                ArcRoi = new ArcMeasureRoi
                {
                    CenterRow = 200, CenterCol = 200, Radius = 80,
                    AngleStart = 0, AngleExtent = 2 * Math.PI, AnnulusRadius = 15
                },
                Tolerance = new ToleranceSpec
                {
                    Nominal = nominalCount,
                    LowerTolerance = -plusMinusCount,
                    UpperTolerance = plusMinusCount,
                    Unit = "count"
                }
            };
        }

        private static MeasurementTool LineTool(string id, string name)
        {
            return new MeasurementTool
            {
                Id = id,
                Name = name,
                ToolType = "line",
                RoiShape = "rect",
                Roi = new RoiGeometry { CenterRow = 100, CenterCol = 100, Length1 = 50, Length2 = 20, AngleRad = 0 }
                // 預設 Tolerance（mm）→ 元素不判定 IsOk=null；此工具只提供 OutputPrimitive 供複合工具引用。
            };
        }

        private static MeasurementTool DistanceTool(string id, string name, string refA, string refB, double nominal, double plusMinus)
        {
            return new MeasurementTool
            {
                Id = id,
                Name = name,
                ToolType = "distance",
                RefToolIds = new List<string> { refA, refB },
                Tolerance = new ToleranceSpec { Nominal = nominal, LowerTolerance = -plusMinus, UpperTolerance = plusMinus, Unit = "mm" }
            };
        }

        private static MeasurementTool AngleTool(string id, string name, string refA, string refB, double nominal, double plusMinus)
        {
            return new MeasurementTool
            {
                Id = id,
                Name = name,
                ToolType = "angle",
                RefToolIds = new List<string> { refA, refB },
                Tolerance = new ToleranceSpec { Nominal = nominal, LowerTolerance = -plusMinus, UpperTolerance = plusMinus, Unit = "deg" }
            };
        }

        private static Recipe OneCircle(string name, double nominal, double plusMinus)
        {
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(CircleTool("t", name, nominal, plusMinus));
            return recipe;
        }

        private static MeasurementTool CircleTool(string id, string name, double nominal, double plusMinus)
        {
            return new MeasurementTool
            {
                Id = id,
                Name = name,
                ToolType = "circle",
                RoiShape = "rect",
                Roi = new RoiGeometry { CenterRow = 100, CenterCol = 100, Length1 = 60, Length2 = 60, AngleRad = 0 },
                Tolerance = new ToleranceSpec
                {
                    Nominal = nominal,
                    LowerTolerance = -plusMinus,
                    UpperTolerance = plusMinus,
                    Unit = "mm"
                }
            };
        }

        private static EdgeResult Edges(int count)
        {
            var r = new EdgeResult { Success = true };
            for (int i = 0; i < count; i++)
                r.EdgePoints.Add(new EdgePoint { Row = i, Column = i * 2, Amplitude = 30 });
            return r;
        }

        private static CircleFittingResult Circle(double diameterPx, bool success = true)
        {
            return new CircleFittingResult
            {
                Success = success,
                CenterRow = 100,
                CenterColumn = 100,
                RadiusPx = diameterPx / 2.0,
                DiameterPx = diameterPx,
                ErrorMessage = success ? string.Empty : "fake fit failure"
            };
        }

        private static LineFittingResult Line(bool success, double angleDeg = 0.0)
        {
            return new LineFittingResult
            {
                Success = success,
                Row1 = 100, Column1 = 80, Row2 = 100, Column2 = 120,
                AngleDeg = angleDeg,
                Length = 40,
                ErrorMessage = success ? string.Empty : "fake line failure"
            };
        }

        // ─── fakes ────────────────────────────────────────────────

        private sealed class FakeEdgeDetector : IEdgeDetector<object>
        {
            private readonly EdgeResult _r;
            public FakeEdgeDetector(EdgeResult r) { _r = r; }
            public EdgeResult DetectEdges(object image, EdgeDetectionRoi roi, EdgeDetectionParameters p) => _r;
            public EdgeResult DetectEdgesSubPix(object image, EdgeDetectionRoi roi, EdgeDetectionParameters p) => _r;
            public EdgeResult DetectEdgesOnArc(object image, ArcMeasureRoi roi, EdgeDetectionParameters p) => _r;
            public EdgeResult DetectEdgesInAnnularSector(object image, ArcMeasureRoi roi, EdgeDetectionParameters p) => _r;
        }

        private sealed class FakeCircleFitter : ICircleFitter
        {
            private readonly CircleFittingResult _r;
            public FakeCircleFitter(CircleFittingResult r) { _r = r; }
            public CircleFittingResult FitCircle(IList<EdgePoint> pts, CircleFittingParameters p) => _r;
        }

        private sealed class FakeLineFitter : ILineFitter
        {
            private readonly LineFittingResult _r;
            public FakeLineFitter(LineFittingResult r) { _r = r; }
            public LineFittingResult FitLine(IList<EdgePoint> pts, LineFittingParameters p) => _r;
        }

        // 以下工具型別不在本測試涵蓋範圍；被呼叫代表 Run 走錯分支，直接讓測試爆掉。
        private sealed class ThrowingDistanceMeasurer : IDistanceMeasurer<object>
        {
            public DistanceMeasurementResult MeasurePointToPoint(double ar, double ac, double br, double bc, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance measurer should not be called");
            public DistanceMeasurementResult MeasurePointToLine(double pr, double pc, double lr1, double lc1, double lr2, double lc2, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance measurer should not be called");
            public DistanceMeasurementResult MeasureLineToLine(double a1r, double a1c, double a2r, double a2c, double b1r, double b1c, double b2r, double b2c, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance measurer should not be called");
            public DistanceMeasurementResult MeasureCircleToCircle(double ar, double ac, double br, double bc, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance measurer should not be called");
            public DistanceMeasurementResult MeasureContourMaxMin(object c1, object c2, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance measurer should not be called");
        }

        private sealed class ThrowingAngleMeasurer : IAngleMeasurer
        {
            public AngleMeasurementResult MeasureAngle(
                double l1r1, double l1c1, double l1r2, double l1c2,
                double l2r1, double l2c1, double l2r2, double l2c2,
                AngleMeasurementParameters p) => throw new InvalidOperationException("angle measurer should not be called");
        }

        private sealed class ThrowingMapper : ICoordinateMapper
        {
            public RigidTransform CreateFromMatch(double rr, double rc, double ra, double cr, double cc, double ca) => throw new InvalidOperationException("mapper should not be called when HasReferencePose is false");
            public TransformedRoi TransformRoi(double rr, double rc, double ra, RigidTransform t) => throw new InvalidOperationException("mapper should not be called when HasReferencePose is false");
        }

        // 複合工具的量測器 fake：回罐頭結果（circle↔circle / line↔line 版本足以覆蓋本測試的路由）。
        private sealed class FakeDistanceMeasurer : IDistanceMeasurer<object>
        {
            private readonly DistanceMeasurementResult _r;
            public FakeDistanceMeasurer(DistanceMeasurementResult r) { _r = r; }
            public DistanceMeasurementResult MeasurePointToPoint(double ar, double ac, double br, double bc, DistanceMeasurementParameters p) => _r;
            public DistanceMeasurementResult MeasurePointToLine(double pr, double pc, double lr1, double lc1, double lr2, double lc2, DistanceMeasurementParameters p) => _r;
            public DistanceMeasurementResult MeasureLineToLine(double a1r, double a1c, double a2r, double a2c, double b1r, double b1c, double b2r, double b2c, DistanceMeasurementParameters p) => _r;
            public DistanceMeasurementResult MeasureCircleToCircle(double ar, double ac, double br, double bc, DistanceMeasurementParameters p) => _r;
            public DistanceMeasurementResult MeasureContourMaxMin(object c1, object c2, DistanceMeasurementParameters p) => _r;
        }

        private sealed class FakeAngleMeasurer : IAngleMeasurer
        {
            private readonly AngleMeasurementResult _r;
            public FakeAngleMeasurer(AngleMeasurementResult r) { _r = r; }
            public AngleMeasurementResult MeasureAngle(
                double l1r1, double l1c1, double l1r2, double l1c2,
                double l2r1, double l2c1, double l2r2, double l2c2,
                AngleMeasurementParameters p) => _r;
        }

        // ─── assert helpers ───────────────────────────────────────

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    "FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException(
                    "FAIL " + name + " | expected=" + expected + " actual=" + actual + " tol=" + tol);
        }

        private static void AssertTrue(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL " + name);
        }
    }
}
