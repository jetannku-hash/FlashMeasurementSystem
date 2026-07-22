using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.AngleMeasurement;
using FlashMeasurementSystem.Application.CircleFitting;
using FlashMeasurementSystem.Application.CoordinateSystem;
using FlashMeasurementSystem.Application.DistanceMeasurement;
using FlashMeasurementSystem.Application.EdgeDetection;
using FlashMeasurementSystem.Application.HoleArrayDetection;
using FlashMeasurementSystem.Application.HoleDetection;
using FlashMeasurementSystem.Application.LineFitting;
using FlashMeasurementSystem.Application.MetrologyModel;
using FlashMeasurementSystem.Application.PinDetection;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Domain.DistanceMeasurement;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.GearAnalysis;
using FlashMeasurementSystem.Domain.Gdt;
using FlashMeasurementSystem.Domain.HoleArrayAnalysis;
using FlashMeasurementSystem.Domain.HoleArrayDetection;
using FlashMeasurementSystem.Domain.HoleDetection;
using FlashMeasurementSystem.Domain.LineFitting;
using FlashMeasurementSystem.Domain.MetrologyModel;
using FlashMeasurementSystem.Domain.PcdAnalysis;
using FlashMeasurementSystem.Domain.PinDetection;
using FlashMeasurementSystem.Domain.PinPitchAnalysis;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Infrastructure.Tolerance;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// RecipeRunner 對其餘工具型別（gear / pcd / pin_pitch / hole_array / metrology / construction /
    /// gd&amp;t）的派發接線測試。補齊 RecipeRunnerDomainTests（circle/line/arc/distance/angle/未支援）
    /// 未涵蓋的那一半。重點在「分析器/偵測器失敗 → ToolRunResult 正確標記」的假 PASS 方向：
    /// - pin_pitch / hole_array 分析失敗必須 IsOk=false（audit 假 PASS 修正的回歸守護）；
    /// - metrology 物件沒產生結果必須補一筆失敗列（P1 修正：否則從判定消失→假 PASS）。
    /// 偵測器/量測模型是注入介面（可 fake）；gear/pcd/pin/hole 的分析器是純 Domain（餵不足資料→優雅 Failed）。
    /// </summary>
    public static class RecipeRunnerToolWiringDomainTests
    {
        public static void Run()
        {
            GearAnalyzerFailureIsNotMeasured();
            PcdDetectorFailureIsNotMeasured();
            PcdNullDetectorIsNotMeasured();
            PinPitchAnalysisFailureCountsNg();
            PinPitchNullDetectorIsNotMeasured();
            HoleArrayAnalysisFailureCountsNg();
            MetrologyMissingResultEmitsFailedRow();
            MetrologySuccessMapsToToolResult();
            ConstructionIntersectionSucceeds();
            ConstructionMissingRefFails();
            GdtRoundnessWiring();

            Console.WriteLine("RecipeRunnerToolWiringDomainTests passed");
        }

        // gear：弧邊偵測成功但齒輪分析 Failed（此處用無效標稱齒數觸發）→ 量測失敗、IsOk=null（非假 OK）。
        private static void GearAnalyzerFailureIsNotMeasured()
        {
            var runner = MakeRunner(edge: new FakeEdgeDetector(Edges(10)));
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(new MeasurementTool
            {
                Id = "gr", Name = "gear", ToolType = "gear",
                ArcRoi = Arc(),
                Gear = new GearAnalysisParameters { NominalToothCount = 0 }   // 無效 → 分析器 Failed
            });

            ToolRunResult r = Only(runner, recipe);
            AssertEqual("gear", r.ToolType, "gear: type");
            AssertEqual(true, r.Supported, "gear: supported");
            AssertEqual(false, r.Measured, "gear: analyzer failure → not measured");
            AssertEqual((bool?)null, r.IsOk, "gear: IsOk null (not false-OK)");
        }

        // pcd：孔偵測器回失敗 → 量測失敗，不進分析器。
        private static void PcdDetectorFailureIsNotMeasured()
        {
            var runner = MakeRunner(hole: new FakeHoleDetector(new HoleDetectionResult { Success = false, ErrorMessage = "no holes" }));
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(new MeasurementTool { Id = "p", Name = "pcd", ToolType = "pcd", ArcRoi = Arc(), Pcd = new PcdAnalysisParameters() });

            ToolRunResult r = Only(runner, recipe);
            AssertEqual(false, r.Measured, "pcd detector-fail: not measured");
            AssertEqual("PCD 量測失敗", r.ValueText, "pcd detector-fail: ValueText");
        }

        // pcd：未注入孔偵測器 → 量測失敗（防禦訊息）。
        private static void PcdNullDetectorIsNotMeasured()
        {
            var runner = MakeRunner();   // hole detector = null
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(new MeasurementTool { Id = "p", Name = "pcd", ToolType = "pcd", ArcRoi = Arc(), Pcd = new PcdAnalysisParameters() });

            ToolRunResult r = Only(runner, recipe);
            AssertEqual(false, r.Measured, "pcd null-detector: not measured");
            AssertEqual("未注入孔偵測器", r.Message, "pcd null-detector: message");
        }

        // pin_pitch：偵測成功但分析失敗（<2 腳）→ 量測失敗且 IsOk=false（audit 假 PASS 守護：不可留 null）。
        private static void PinPitchAnalysisFailureCountsNg()
        {
            var runner = MakeRunner(pin: new FakePinDetector(new PinDetectionResult { Success = true }));  // Pins 空 → 分析器 Failed
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(new MeasurementTool
            {
                Id = "pp", Name = "pins", ToolType = "pin_pitch",
                Roi = Rect(), PinPitch = new PinPitchAnalysisParameters()
            });

            ToolRunResult r = Only(runner, recipe);
            AssertEqual(false, r.Measured, "pin_pitch analysis-fail: not measured");
            AssertEqual(false, r.IsOk, "pin_pitch analysis-fail: IsOk=false (audit false-PASS guard, not null)");
            AssertEqual("引腳間距分析失敗", r.ValueText, "pin_pitch analysis-fail: ValueText");
        }

        private static void PinPitchNullDetectorIsNotMeasured()
        {
            var runner = MakeRunner();   // pin detector = null
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(new MeasurementTool { Id = "pp", Name = "pins", ToolType = "pin_pitch", Roi = Rect(), PinPitch = new PinPitchAnalysisParameters() });

            ToolRunResult r = Only(runner, recipe);
            AssertEqual(false, r.Measured, "pin_pitch null-detector: not measured");
            AssertEqual((bool?)null, r.IsOk, "pin_pitch null-detector: IsOk null");
            AssertEqual("未注入引腳偵測器", r.Message, "pin_pitch null-detector: message");
        }

        // hole_array：偵測成功但分析失敗（<2 孔）→ 量測失敗且 IsOk=false（audit 假 PASS 守護）。
        private static void HoleArrayAnalysisFailureCountsNg()
        {
            var runner = MakeRunner(holeArray: new FakeHoleArrayDetector(new HoleArrayDetectionResult { Success = true }));  // Holes 空
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(new MeasurementTool
            {
                Id = "ha", Name = "grid", ToolType = "hole_array",
                Roi = Rect(), HoleArray = new HoleArrayAnalysisParameters()
            });

            ToolRunResult r = Only(runner, recipe);
            AssertEqual(false, r.Measured, "hole_array analysis-fail: not measured");
            AssertEqual(false, r.IsOk, "hole_array analysis-fail: IsOk=false (audit false-PASS guard, not null)");
            AssertEqual("孔陣列分析失敗", r.ValueText, "hole_array analysis-fail: ValueText");
        }

        // metrology（P1 守護）：模型有 1 個物件但套用只回 0 筆結果 → 必須補一筆失敗列，否則該物件從判定消失→假 PASS。
        private static void MetrologyMissingResultEmitsFailedRow()
        {
            var runner = MakeRunner(met: new FakeMetrologyRunner(new MetrologyModelResult { ErrorMessage = "量測區落到影像外" }));  // Objects 空
            var recipe = new Recipe { HasReferencePose = false, MetrologyModel = ModelWithOneCircle() };

            List<ToolRunResult> results = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0);

            AssertEqual(1, results.Count, "metrology missing: one failed row emitted");
            AssertEqual("metrology", results[0].ToolType, "metrology missing: ToolType");
            AssertEqual(false, results[0].Measured, "metrology missing: not measured");
            AssertEqual((bool?)null, results[0].IsOk, "metrology missing: IsOk null (counted NG by MeasurementOutcome)");
        }

        // metrology 成功 → 映射成 metrology_<shape> 結果列（Measured 由物件 Success 決定）。
        private static void MetrologySuccessMapsToToolResult()
        {
            var okObject = new MetrologyObjectResult
            {
                Id = "o1", Name = "hole", Shape = MetrologyObjectType.Circle,
                Success = true, ValueText = "R=5px"
            };
            var runner = MakeRunner(met: new FakeMetrologyRunner(
                new MetrologyModelResult { Objects = new List<MetrologyObjectResult> { okObject } }));
            var recipe = new Recipe { HasReferencePose = false, MetrologyModel = ModelWithOneCircle() };

            List<ToolRunResult> results = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0);

            AssertEqual(1, results.Count, "metrology success: one row");
            AssertEqual("metrology_circle", results[0].ToolType, "metrology success: ToolType mapped from shape");
            AssertEqual(true, results[0].Measured, "metrology success: measured (from object Success)");
        }

        // construction：兩條非平行線 → 交點成功。
        private static void ConstructionIntersectionSucceeds()
        {
            var runner = MakeRunner(
                edge: new FakeEdgeDetector(Edges(5)),
                line: new SeqLineFitter(
                    HLine(row: 100),      // 水平線 row=100
                    VLine(col: 100)));    // 垂直線 col=100 → 交點 (100,100)
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(LineTool("l1"));
            recipe.Tools.Add(LineTool("l2"));
            recipe.Tools.Add(new MeasurementTool
            {
                Id = "ix", Name = "corner", ToolType = "intersection",
                RefToolIds = new List<string> { "l1", "l2" }
            });

            List<ToolRunResult> results = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0);
            ToolRunResult ix = results.Find(x => x.ToolType == "intersection");

            AssertTrue(ix != null, "intersection: result present");
            AssertEqual(true, ix.Measured, "intersection: measured");
            AssertEqual("(100.0,100.0)", ix.ValueText, "intersection: point at line crossing");
        }

        // construction：參照不存在 → 量測失敗。
        private static void ConstructionMissingRefFails()
        {
            var runner = MakeRunner();
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(new MeasurementTool
            {
                Id = "ix", Name = "corner", ToolType = "intersection",
                RefToolIds = new List<string> { "nope1", "nope2" }
            });

            ToolRunResult r = Only(runner, recipe);
            AssertEqual(false, r.Measured, "intersection missing-ref: not measured");
            AssertEqual("找不到參考元素", r.ValueText, "intersection missing-ref: ValueText");
        }

        // gd&t roundness：參照一個 circle 元素 → 偏差(px)→mm→單邊判定。此處圓真圓度 0 → 在容差內 → OK。
        private static void GdtRoundnessWiring()
        {
            var runner = MakeRunner(
                edge: new FakeEdgeDetector(Edges(5)),
                circle: new FakeCircleFitter(Circle(diameterPx: 10.0)));   // Roundness 預設 0
            var recipe = new Recipe { HasReferencePose = false };
            recipe.Tools.Add(new MeasurementTool
            {
                Id = "c1", Name = "hole", ToolType = "circle", RoiShape = "rect",
                Roi = Rect(), Tolerance = new ToleranceSpec { Nominal = 10, LowerTolerance = -1, UpperTolerance = 1, Unit = "mm" }
            });
            recipe.Tools.Add(new MeasurementTool
            {
                Id = "rnd", Name = "roundness", ToolType = "roundness",
                RefToolIds = new List<string> { "c1" },
                Gdt = new GdtToleranceSpec { ToleranceZoneMm = 0.1 }
            });

            List<ToolRunResult> results = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0);
            ToolRunResult g = results.Find(x => x.ToolType == "roundness");

            AssertTrue(g != null, "roundness: result present");
            AssertEqual(true, g.Measured, "roundness: measured");
            AssertClose(0.0, g.GdtDeviationMm, 1e-9, "roundness: deviation mm (roundness 0px)");
            AssertEqual(true, g.IsOk, "roundness: 0 within 0.1 zone → OK");
        }

        // ─── builders ─────────────────────────────────────────────

        private static RecipeRunner<object> MakeRunner(
            IEdgeDetector<object> edge = null,
            ICircleFitter circle = null,
            ILineFitter line = null,
            IMetrologyModelRunner<object> met = null,
            IHoleDetector<object> hole = null,
            IPinDetector<object> pin = null,
            IHoleArrayDetector<object> holeArray = null)
        {
            return new RecipeRunner<object>(
                edge ?? new FakeEdgeDetector(Edges(0)),
                circle ?? new FakeCircleFitter(Circle(0, success: false)),
                line ?? new SeqLineFitter(),
                new ThrowingDistanceMeasurer(),
                new ThrowingAngleMeasurer(),
                new ToleranceJudger(),
                new ThrowingMapper(),
                met, hole, pin, holeArray);
        }

        // 只有一個工具的配方，回傳其唯一結果。
        private static ToolRunResult Only(RecipeRunner<object> runner, Recipe recipe)
        {
            List<ToolRunResult> results = runner.Run(recipe, new object(), false, 0, 0, 0, 1000.0, 1000.0);
            AssertEqual(1, results.Count, "expected exactly one tool result");
            return results[0];
        }

        private static ArcMeasureRoi Arc()
            => new ArcMeasureRoi { CenterRow = 200, CenterCol = 200, Radius = 80, AngleStart = 0, AngleExtent = 2 * Math.PI, AnnulusRadius = 15 };

        private static RoiGeometry Rect()
            => new RoiGeometry { CenterRow = 100, CenterCol = 100, Length1 = 60, Length2 = 40, AngleRad = 0 };

        private static MeasurementTool LineTool(string id)
            => new MeasurementTool { Id = id, Name = id, ToolType = "line", RoiShape = "rect", Roi = Rect() };

        private static MetrologyModelDef ModelWithOneCircle()
        {
            var m = new MetrologyModelDef { ImageWidth = 640, ImageHeight = 480 };
            m.Objects.Add(new MetrologyObjectDef { Id = "o1", Name = "hole", Shape = MetrologyObjectType.Circle });
            return m;
        }

        private static EdgeResult Edges(int count)
        {
            var r = new EdgeResult { Success = count >= 0 };
            for (int i = 0; i < count; i++) r.EdgePoints.Add(new EdgePoint { Row = i, Column = i * 2, Amplitude = 30 });
            return r;
        }

        private static CircleFittingResult Circle(double diameterPx, bool success = true)
            => new CircleFittingResult { Success = success, CenterRow = 100, CenterColumn = 100, RadiusPx = diameterPx / 2.0, DiameterPx = diameterPx };

        private static LineFittingResult HLine(double row)
            => new LineFittingResult { Success = true, Row1 = row, Column1 = 0, Row2 = row, Column2 = 200, Length = 200 };

        private static LineFittingResult VLine(double col)
            => new LineFittingResult { Success = true, Row1 = 0, Column1 = col, Row2 = 200, Column2 = col, Length = 200 };

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

        // 逐次回傳（第 N 次 FitLine 呼叫 → 第 N 個結果）；用光後回最後一個。空 → 一律失敗。
        private sealed class SeqLineFitter : ILineFitter
        {
            private readonly LineFittingResult[] _seq;
            private int _i;
            public SeqLineFitter(params LineFittingResult[] seq) { _seq = seq; }
            public LineFittingResult FitLine(IList<EdgePoint> pts, LineFittingParameters p)
            {
                if (_seq == null || _seq.Length == 0)
                    return new LineFittingResult { Success = false, ErrorMessage = "no line" };
                var r = _seq[Math.Min(_i, _seq.Length - 1)];
                _i++;
                return r;
            }
        }

        private sealed class FakeHoleDetector : IHoleDetector<object>
        {
            private readonly HoleDetectionResult _r;
            public FakeHoleDetector(HoleDetectionResult r) { _r = r; }
            public HoleDetectionResult DetectHolesInAnnulus(object image, ArcMeasureRoi placedArc, PcdAnalysisParameters parameters) => _r;
        }

        private sealed class FakePinDetector : IPinDetector<object>
        {
            private readonly PinDetectionResult _r;
            public FakePinDetector(PinDetectionResult r) { _r = r; }
            public PinDetectionResult DetectPinsInRect(object image, RoiGeometry placedRoi, PinPitchAnalysisParameters parameters) => _r;
        }

        private sealed class FakeHoleArrayDetector : IHoleArrayDetector<object>
        {
            private readonly HoleArrayDetectionResult _r;
            public FakeHoleArrayDetector(HoleArrayDetectionResult r) { _r = r; }
            public HoleArrayDetectionResult DetectHolesInRect(object image, RoiGeometry placedRoi, HoleArrayAnalysisParameters parameters) => _r;
        }

        private sealed class FakeMetrologyRunner : IMetrologyModelRunner<object>
        {
            private readonly MetrologyModelResult _r;
            public FakeMetrologyRunner(MetrologyModelResult r) { _r = r; }
            public MetrologyModelResult Apply(MetrologyModelDef model,
                double refRow, double refCol, double refAngleRad, bool hasReferencePose,
                object image, double matchRow, double matchCol, double matchAngleRad, bool hasMatch) => _r;
        }

        private sealed class ThrowingDistanceMeasurer : IDistanceMeasurer
        {
            public DistanceMeasurementResult MeasurePointToPoint(double ar, double ac, double br, double bc, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance not expected");
            public DistanceMeasurementResult MeasurePointToLine(double pr, double pc, double lr1, double lc1, double lr2, double lc2, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance not expected");
            public DistanceMeasurementResult MeasureLineToLine(double a1r, double a1c, double a2r, double a2c, double b1r, double b1c, double b2r, double b2c, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance not expected");
            public DistanceMeasurementResult MeasureCircleToCircle(double ar, double ac, double br, double bc, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance not expected");
        }

        private sealed class ThrowingAngleMeasurer : IAngleMeasurer
        {
            public AngleMeasurementResult MeasureAngle(
                double l1r1, double l1c1, double l1r2, double l1c2,
                double l2r1, double l2c1, double l2r2, double l2c2,
                AngleMeasurementParameters p) => throw new InvalidOperationException("angle not expected");
        }

        private sealed class ThrowingMapper : ICoordinateMapper
        {
            public RigidTransform CreateFromMatch(double rr, double rc, double ra, double cr, double cc, double ca) => throw new InvalidOperationException("mapper not expected (no reference pose)");
            public TransformedRoi TransformRoi(double rr, double rc, double ra, RigidTransform t) => throw new InvalidOperationException("mapper not expected (no reference pose)");
        }

        // ─── assert helpers ───────────────────────────────────────

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException("FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }

        private static void AssertTrue(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL " + name);
        }
    }
}
