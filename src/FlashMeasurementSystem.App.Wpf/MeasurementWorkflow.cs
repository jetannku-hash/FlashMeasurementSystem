using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FlashMeasurementSystem.Application.ImageQuality;
using FlashMeasurementSystem.Application.Reporting;
using FlashMeasurementSystem.Application.TemplateMatching;
using FlashMeasurementSystem.Application.Tolerance;
using FlashMeasurementSystem.Domain.ImageQuality;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.TemplateMatching;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Domain.Workflow;
using HalconDotNet;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// One-click measurement workflow (M4-B3, software-only).
    /// Orchestrates: image quality check → template matching → measurement → evaluation → CSV report.
    /// Hardware stages (WaitingPart/Acquiring/Outputting) are excluded — see manual §4.14.
    /// </summary>
    public sealed class MeasurementWorkflow
    {
        private readonly IImageQualityChecker<HImage> _iqc;
        private readonly ITemplateMatcher<HImage, HRegion> _templateMatcher;
        private readonly RecipeRunner _recipeRunner;
        private readonly IToleranceJudger _judger;
        private readonly IMeasurementReportWriter _reportWriter;

        public event Action<MeasurementState> StateChanged;

        public MeasurementWorkflow(
            IImageQualityChecker<HImage> iqc,
            ITemplateMatcher<HImage, HRegion> templateMatcher,
            RecipeRunner recipeRunner,
            IToleranceJudger judger,
            IMeasurementReportWriter reportWriter)
        {
            _iqc = iqc ?? throw new ArgumentNullException(nameof(iqc));
            _templateMatcher = templateMatcher ?? throw new ArgumentNullException(nameof(templateMatcher));
            _recipeRunner = recipeRunner ?? throw new ArgumentNullException(nameof(recipeRunner));
            _judger = judger ?? throw new ArgumentNullException(nameof(judger));
            _reportWriter = reportWriter ?? throw new ArgumentNullException(nameof(reportWriter));
        }

        /// <summary>
        /// Run the full software measurement pipeline once.
        /// </summary>
        /// <param name="recipe">Loaded measurement recipe.</param>
        /// <param name="image">Current image (must be single-channel or convertible).</param>
        /// <param name="pixelSizeUmX">X pixel size in µm.</param>
        /// <param name="pixelSizeUmY">Y pixel size in µm.</param>
        /// <param name="reportDir">Directory for CSV output (e.g. "data/reports").</param>
        /// <param name="templateModelPath">Shape-model file path; null to skip template matching.</param>
        /// <param name="searchRegion">Search region for matching; null = full image.</param>
        /// <param name="matchParams">Matching parameters; null = defaults.</param>
        /// <param name="toolResults">[out] Per-tool results for UI overlay drawing.</param>
        /// <returns>Overall workflow result.</returns>
        public WorkflowResult RunOnce(
            Recipe recipe,
            HImage image,
            double pixelSizeUmX,
            double pixelSizeUmY,
            string reportDir,
            string templateModelPath,
            HRegion searchRegion,
            TemplateMatchingParameters matchParams,
            bool skipImageQualityCheck,
            out List<ToolRunResult> toolResults)
        {
            var result = new WorkflowResult
            {
                RecipeName = recipe != null ? recipe.Name : "",
                Timestamp = DateTime.Now
            };

            toolResults = new List<ToolRunResult>();

            if (recipe == null)
            {
                result.Success = false;
                result.FinalState = MeasurementState.Failed;
                result.Message = "Recipe is null";
                return result;
            }
            if (image == null || !image.IsInitialized())
            {
                result.Success = false;
                result.FinalState = MeasurementState.Failed;
                result.Message = "Image is null or not initialized";
                return result;
            }

            // ── 1. CheckingImage ──（skipImageQualityCheck=true 時跳過此關，供合成圖測試）
            SetState(MeasurementState.CheckingImage);
            if (!skipImageQualityCheck)
            try
            {
                ImageQualityResult iqResult = _iqc.Check(image, ImageQualityThresholds.Default());
                if (!iqResult.Pass)
                {
                    result.Success = false;
                    result.FinalState = MeasurementState.Failed;
                    result.Message = "Image quality check failed: " + (iqResult.Message ?? "");
                    return result;
                }
            }
            catch (HalconException ex)
            {
                result.Success = false;
                result.FinalState = MeasurementState.Failed;
                result.Message = "Image quality check error: " + ex.Message;
                return result;
            }

            // ── 2. MatchingTemplate ──
            bool hasMatch = false;
            double matchRow = 0, matchCol = 0, matchAngleRad = 0;

            if (recipe.HasReferencePose)
            {
                SetState(MeasurementState.MatchingTemplate);

                if (string.IsNullOrEmpty(templateModelPath))
                {
                    result.Success = false;
                    result.FinalState = MeasurementState.Failed;
                    result.Message = "Recipe requires template matching but no template model path provided";
                    return result;
                }

                try
                {
                    _templateMatcher.LoadModel(templateModelPath);
                    var tmParams = matchParams ?? TemplateMatchingParameters.Default();
                    TemplateMatchResult match = _templateMatcher.FindMatches(image, searchRegion, tmParams);

                    if (match == null || !match.Found)
                    {
                        result.Success = false;
                        result.FinalState = MeasurementState.Failed;
                        result.Message = "Template matching failed: pattern not found";
                        return result;
                    }

                    hasMatch = true;
                    matchRow = match.Row;
                    matchCol = match.Column;
                    matchAngleRad = match.AngleDeg * Math.PI / 180.0;

                    result.HasMatch = true;
                    result.MatchRow = match.Row;
                    result.MatchCol = match.Column;
                    result.MatchAngleDeg = match.AngleDeg;
                }
                catch (HalconException ex)
                {
                    result.Success = false;
                    result.FinalState = MeasurementState.Failed;
                    result.Message = "Template matching error: " + ex.Message;
                    return result;
                }
            }

            // ── 3. TransformingRois (done inside RecipeRunner) ──
            SetState(MeasurementState.TransformingRois);

            // ── 4. Measuring ──
            SetState(MeasurementState.Measuring);
            try
            {
                toolResults = _recipeRunner.Run(
                    recipe, image,
                    hasMatch, matchRow, matchCol, matchAngleRad,
                    pixelSizeUmX, pixelSizeUmY);
            }
            catch (HalconException ex)
            {
                result.Success = false;
                result.FinalState = MeasurementState.Failed;
                result.Message = "Measurement error: " + ex.Message;
                return result;
            }

            // ── 5. Evaluating ──
            SetState(MeasurementState.Evaluating);
            var judgments = new List<ItemJudgment>();
            foreach (ToolRunResult r in toolResults)
            {
                if (r == null) continue;
                if (r.IsOk == true) result.OkCount++;
                else if (r.IsOk == false) result.NgCount++;

                // Build ItemJudgment for reporting: look up the recipe tool by name
                // to get tolerance spec, then re-judge to produce full judgment data.
                MeasurementTool tool = FindTool(recipe, r.Name, r.ToolType);
                if (tool != null && tool.Tolerance != null)
                {
                    double measuredValue = GetMeasuredValue(r, tool);
                    var input = new ToleranceItemInput
                    {
                        ToolId = tool.Id ?? "",
                        ToolName = tool.Name ?? r.Name,
                        MeasuredValue = measuredValue,
                        Spec = tool.Tolerance
                    };
                    OverallJudgment overall = _judger.Judge(new List<ToleranceItemInput> { input });
                    if (overall.Items.Count > 0)
                        judgments.Add(overall.Items[0]);
                }
                else
                {
                    judgments.Add(new ItemJudgment
                    {
                        ToolName = r.Name ?? "",
                        MeasuredValue = 0,
                        IsOk = r.IsOk ?? false,
                        Message = r.Message ?? ""
                    });
                }
            }

            result.AllOk = result.NgCount == 0;

            // ── 6. Reporting ──
            SetState(MeasurementState.Reporting);
            try
            {
                string reportPath = BuildReportPath(reportDir);
                _reportWriter.Append(result, judgments, reportPath);
                result.ReportPath = reportPath;
            }
            catch (Exception ex)
            {
                // Report failure is non-fatal: log but don't fail the run.
                result.Message = "Report write warning: " + ex.Message;
            }

            result.Success = true;
            result.FinalState = MeasurementState.Completed;
            SetState(MeasurementState.Completed);
            return result;
        }

        private void SetState(MeasurementState state)
        {
            StateChanged?.Invoke(state);
        }

        private static MeasurementTool FindTool(Recipe recipe, string name, string toolType)
        {
            if (recipe == null || recipe.Tools == null) return null;
            foreach (MeasurementTool t in recipe.Tools)
            {
                if (t != null && t.Name == name && t.ToolType == toolType)
                    return t;
            }
            return null;
        }

        private static double GetMeasuredValue(ToolRunResult r, MeasurementTool tool)
        {
            if (r.ToolType == "circle")
                return r.DiameterMm;
            if (r.ToolType == "line")
            {
                // If unit is deg, use angle (copy C2 pattern for alignment)
                if (tool.Tolerance != null && tool.Tolerance.Unit == "deg")
                {
                    double norm = Domain.AngleMeasurement.AngleNormalizer.ToHalfCircle(r.LineAngleDeg);
                    return tool.Tolerance.Nominal
                        + Domain.AngleMeasurement.AngleNormalizer.CircularSignedDiffDeg(norm, tool.Tolerance.Nominal);
                }
                return r.LineAngleDeg;
            }
            if (r.ToolType == "distance")
                return r.DistMm;
            if (r.ToolType == "angle")
                return r.AngleDeg;
            return 0;
        }

        private static string BuildReportPath(string reportDir)
        {
            string dir = string.IsNullOrEmpty(reportDir) ? "data/reports" : reportDir;
            string fileName = "measure_" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".csv";
            return Path.Combine(dir, fileName);
        }
    }
}
