using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application;
using FlashMeasurementSystem.Application.AngleMeasurement;
using FlashMeasurementSystem.Application.CircleFitting;
using FlashMeasurementSystem.Application.CoordinateSystem;
using FlashMeasurementSystem.Application.DistanceMeasurement;
using FlashMeasurementSystem.Application.EdgeDetection;
using FlashMeasurementSystem.Application.ImageQuality;
using FlashMeasurementSystem.Application.LineFitting;
using FlashMeasurementSystem.Application.Reporting;
using FlashMeasurementSystem.Application.TemplateMatching;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Domain.DistanceMeasurement;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.ImageQuality;
using FlashMeasurementSystem.Domain.LineFitting;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.TemplateMatching;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Domain.Workflow;
using FlashMeasurementSystem.Infrastructure.Tolerance;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// MeasurementWorkflow.RunOnce 的管線編排測試（IQC → 模板匹配 → 量測 → 判定計數 → 報表）。
    /// 與 RecipeRunnerDomainTests 合起來補上 2026-07-21 審查點名的結構性驗證洞。
    /// 三個 vendor adapter（IQC/matcher/report）以 fake 注入，量測層用真的 RecipeRunner + fake
    /// 偵測器，判定用真的 ToleranceJudger。TImage/TRegion/TContour 皆以 object 具現。
    ///
    /// 兩個重點：
    /// (1) 模板載入的 MeasurementAdapterException 被 workflow 捕捉 → 證明 Option A 的邊界翻譯
    ///     端到端成立（adapter 拋 HALCON-free 例外、住在 Application 的 workflow 接得到）。
    /// (2) 「supported 但未量得」的工具計為 NG（非靜默 OK）→ 正是 P0 假 PASS 的情境，於管線層鎖住。
    /// </summary>
    public static class MeasurementWorkflowDomainTests
    {
        public static void Run()
        {
            NullImageFailsFast();
            UninitializedImageFailsFast();
            ImageQualityFailureStopsPipeline();
            TemplateLoadErrorIsCaught();
            TemplateNotFoundStops();
            HappyPathCountsOkAndWritesReport();
            UnmeasuredSupportedToolCountsNg();

            Console.WriteLine("MeasurementWorkflowDomainTests passed");
        }

        private static void NullImageFailsFast()
        {
            var wf = MakeWorkflow(new FakeIqc(pass: true), new ThrowingMatcher(),
                BuildRunner(Edges(5), Circle(10)), new RecordingWriter());

            WorkflowResult res = wf.RunOnce(OneCircleRecipe(refPose: false), null,
                1000, 1000, "data/reports", null, null, null, skipImageQualityCheck: true,
                out List<ToolRunResult> tr, out List<ItemJudgment> ij);

            AssertEqual(false, res.Success, "null image: not success");
            AssertEqual(MeasurementState.Failed, res.FinalState, "null image: failed state");
            AssertEqual("Image is null or not initialized", res.Message, "null image: message");
            AssertEqual(0, tr.Count, "null image: no tool results");
        }

        private static void UninitializedImageFailsFast()
        {
            var wf = MakeWorkflow(new FakeIqc(pass: true), new ThrowingMatcher(),
                BuildRunner(Edges(5), Circle(10)), new RecordingWriter(),
                isImageInitialized: _ => false);   // 非 null 但「未初始化」

            WorkflowResult res = wf.RunOnce(OneCircleRecipe(refPose: false), new object(),
                1000, 1000, "data/reports", null, null, null, true,
                out _, out _);

            AssertEqual(false, res.Success, "uninit image: not success");
            AssertEqual("Image is null or not initialized", res.Message, "uninit image: message");
        }

        private static void ImageQualityFailureStopsPipeline()
        {
            var writer = new RecordingWriter();
            var wf = MakeWorkflow(new FakeIqc(pass: false, message: "too dark"), new ThrowingMatcher(),
                BuildRunner(Edges(5), Circle(10)), writer);

            WorkflowResult res = wf.RunOnce(OneCircleRecipe(refPose: false), new object(),
                1000, 1000, "data/reports", null, null, null, skipImageQualityCheck: false,
                out List<ToolRunResult> tr, out _);

            AssertEqual(false, res.Success, "iqc fail: not success");
            AssertEqual(MeasurementState.Failed, res.FinalState, "iqc fail: failed state");
            AssertTrue(res.Message.StartsWith("Image quality check failed"), "iqc fail: message");
            AssertEqual(0, tr.Count, "iqc fail: measurement did not run");
            AssertEqual(0, writer.Appends, "iqc fail: report not written");
        }

        // Option A 端到端：模板載入拋 MeasurementAdapterException（HALCON-free），workflow 接住。
        private static void TemplateLoadErrorIsCaught()
        {
            var wf = MakeWorkflow(new FakeIqc(pass: true), new ThrowingMatcher(loadThrows: true),
                BuildRunner(Edges(5), Circle(10)), new RecordingWriter());

            WorkflowResult res = wf.RunOnce(OneCircleRecipe(refPose: true), new object(),
                1000, 1000, "data/reports", templateModelPath: "bad.shm", null, null, true,
                out _, out _);

            AssertEqual(false, res.Success, "template load error: not success");
            AssertEqual(MeasurementState.Failed, res.FinalState, "template load error: failed state");
            AssertTrue(res.Message.StartsWith("Template matching error:"),
                "template load error: caught as matching error (message=" + res.Message + ")");
        }

        private static void TemplateNotFoundStops()
        {
            var wf = MakeWorkflow(new FakeIqc(pass: true), new ThrowingMatcher(found: false),
                BuildRunner(Edges(5), Circle(10)), new RecordingWriter());

            WorkflowResult res = wf.RunOnce(OneCircleRecipe(refPose: true), new object(),
                1000, 1000, "data/reports", templateModelPath: "ok.shm", null, null, true,
                out _, out _);

            AssertEqual(false, res.Success, "match not found: not success");
            AssertEqual("Template matching failed: pattern not found", res.Message, "match not found: message");
        }

        private static void HappyPathCountsOkAndWritesReport()
        {
            var writer = new RecordingWriter();
            var wf = MakeWorkflow(new FakeIqc(pass: true), new ThrowingMatcher(),
                BuildRunner(Edges(5), Circle(10)), writer);   // 直徑 10mm、公差 10±0.5 → OK

            WorkflowResult res = wf.RunOnce(OneCircleRecipe(refPose: false), new object(),
                1000, 1000, "data/reports", null, null, null, skipImageQualityCheck: true,
                out List<ToolRunResult> tr, out List<ItemJudgment> ij);

            AssertEqual(true, res.Success, "happy path: success");
            AssertEqual(MeasurementState.Completed, res.FinalState, "happy path: completed");
            AssertEqual(1, res.OkCount, "happy path: Ok count");
            AssertEqual(0, res.NgCount, "happy path: Ng count");
            AssertEqual(true, res.AllOk, "happy path: all ok");
            AssertEqual(1, tr.Count, "happy path: one tool result");
            AssertEqual(1, ij.Count, "happy path: one judgment row");
            // 透過公開 API 驗證 GetMeasuredValue(circle)=DiameterMm 這條接縫：報表列的量測值必須是直徑 10mm，
            // 而非 0（若 GetMeasuredValue 對 circle 漂移成 return 0，此斷言會抓到）。
            AssertClose(10.0, ij[0].MeasuredValue, 1e-9, "happy path: report MeasuredValue = circle diameter");
            AssertEqual(1, writer.Appends, "happy path: report written once");
        }

        // P0 情境於管線層：supported 但未量得（邊點不足）→ 計為 NG，不靜默 OK。
        private static void UnmeasuredSupportedToolCountsNg()
        {
            var wf = MakeWorkflow(new FakeIqc(pass: true), new ThrowingMatcher(),
                BuildRunner(Edges(2), Circle(10)), new RecordingWriter());   // 邊點<3 → 未量得

            WorkflowResult res = wf.RunOnce(OneCircleRecipe(refPose: false), new object(),
                1000, 1000, "data/reports", null, null, null, skipImageQualityCheck: true,
                out List<ToolRunResult> tr, out _);

            AssertEqual(false, tr[0].Measured, "unmeasured NG: tool not measured");
            AssertEqual(true, tr[0].Supported, "unmeasured NG: tool supported");
            AssertEqual(1, res.NgCount, "unmeasured NG: counted as NG (P0)");
            AssertEqual(0, res.OkCount, "unmeasured NG: not counted OK");
            AssertEqual(false, res.AllOk, "unmeasured NG: not all-ok");
            AssertEqual(true, res.Success, "unmeasured NG: pipeline still completes");
            AssertEqual(MeasurementState.Completed, res.FinalState, "unmeasured NG: completed state");
        }

        // ─── builders ─────────────────────────────────────────────

        private static MeasurementWorkflow<object, object, object> MakeWorkflow(
            IImageQualityChecker<object> iqc,
            ITemplateMatcher<object, object> matcher,
            RecipeRunner<object, object> runner,
            IMeasurementReportWriter writer,
            Func<object, bool> isImageInitialized = null)
        {
            return new MeasurementWorkflow<object, object, object>(
                iqc, matcher, runner, new ToleranceJudger(), writer,
                isImageInitialized ?? (img => img != null));
        }

        private static RecipeRunner<object, object> BuildRunner(EdgeResult edges, CircleFittingResult circle)
        {
            return new RecipeRunner<object, object>(
                new FakeEdgeDetector(edges),
                new FakeCircleFitter(circle),
                new FakeLineFitter(),
                new ThrowingDistanceMeasurer(),
                new ThrowingAngleMeasurer(),
                new ToleranceJudger(),
                new ThrowingMapper());
        }

        private static Recipe OneCircleRecipe(bool refPose)
        {
            var recipe = new Recipe { Name = "wf-test", HasReferencePose = refPose };
            recipe.Tools.Add(new MeasurementTool
            {
                Id = "c1",
                Name = "D10",
                ToolType = "circle",
                RoiShape = "rect",
                Roi = new RoiGeometry { CenterRow = 100, CenterCol = 100, Length1 = 60, Length2 = 60, AngleRad = 0 },
                Tolerance = new ToleranceSpec { Nominal = 10.0, LowerTolerance = -0.5, UpperTolerance = 0.5, Unit = "mm" }
            });
            return recipe;
        }

        private static EdgeResult Edges(int count)
        {
            var r = new EdgeResult { Success = true };
            for (int i = 0; i < count; i++)
                r.EdgePoints.Add(new EdgePoint { Row = i, Column = i * 2, Amplitude = 30 });
            return r;
        }

        private static CircleFittingResult Circle(double diameterPx)
        {
            return new CircleFittingResult
            {
                Success = true,
                CenterRow = 100,
                CenterColumn = 100,
                RadiusPx = diameterPx / 2.0,
                DiameterPx = diameterPx
            };
        }

        // ─── fakes ────────────────────────────────────────────────

        private sealed class FakeIqc : IImageQualityChecker<object>
        {
            private readonly bool _pass;
            private readonly string _message;
            public FakeIqc(bool pass, string message = "") { _pass = pass; _message = message; }
            public ImageQualityResult Check(object image, ImageQualityThresholds thresholds)
                => new ImageQualityResult { Pass = _pass, Message = _message };
        }

        private sealed class ThrowingMatcher : ITemplateMatcher<object, object>
        {
            private readonly bool _loadThrows;
            private readonly bool _found;
            public ThrowingMatcher(bool loadThrows = false, bool found = true)
            {
                _loadThrows = loadThrows;
                _found = found;
            }
            public void LoadModel(string modelFilePath)
            {
                if (_loadThrows) throw new MeasurementAdapterException("載入模板失敗：壞檔");
            }
            public TemplateMatchResult FindMatches(object image, object searchRegion, TemplateMatchingParameters p)
                => new TemplateMatchResult { Found = _found, Row = 10, Column = 20, AngleDeg = 0, Score = 0.9 };
        }

        private sealed class RecordingWriter : IMeasurementReportWriter
        {
            public int Appends { get; private set; }
            public void Append(WorkflowResult overall, IList<ItemJudgment> items, string filePath) { Appends++; }
        }

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
            public LineFittingResult FitLine(IList<EdgePoint> pts, LineFittingParameters p)
                => new LineFittingResult { Success = false, ErrorMessage = "not used" };
        }

        private sealed class ThrowingDistanceMeasurer : IDistanceMeasurer<object>
        {
            public DistanceMeasurementResult MeasurePointToPoint(double r1, double c1, double r2, double c2, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance measurer should not be called");
            public DistanceMeasurementResult MeasurePointToLine(double pr, double pc, double lr1, double lc1, double lr2, double lc2, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance measurer should not be called");
            public DistanceMeasurementResult MeasureLineToLine(double a1r, double a1c, double a2r, double a2c, double b1r, double b1c, double b2r, double b2c, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance measurer should not be called");
            public DistanceMeasurementResult MeasureCircleToCircle(double r1, double c1, double r2, double c2, DistanceMeasurementParameters p) => throw new InvalidOperationException("distance measurer should not be called");
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
            public RigidTransform CreateFromMatch(double rr, double rc, double ra, double cr, double cc, double ca) => throw new InvalidOperationException("mapper should not be called");
            public TransformedRoi TransformRoi(double rr, double rc, double ra, RigidTransform t) => throw new InvalidOperationException("mapper should not be called");
        }

        // ─── assert helpers ───────────────────────────────────────

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    "FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }

        private static void AssertTrue(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException("FAIL " + name);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException(
                    "FAIL " + name + " | expected=" + expected + " actual=" + actual);
        }
    }
}
