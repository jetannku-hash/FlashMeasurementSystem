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
    }
}
