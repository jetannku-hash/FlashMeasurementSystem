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
                if (tool != null && tool.Gdt != null)
                {
                    // GD&T 形位公差為單邊（0 ≤ 偏差 ≤ T），不走雙邊判定器。
                    // 直接用 RecipeRunner 算好的偏差與判定（GdtEvaluation 的結果）組報表列，
                    // 避免落入下方雙邊分支用 MeasuredValue=0 誤判為 OK。
                    judgments.Add(new ItemJudgment
                    {
                        ToolId = tool.Id ?? "",
                        ToolName = tool.Name ?? r.Name,
                        MeasuredValue = r.GdtDeviationMm,
                        Nominal = 0,
                        LowerLimit = 0,
                        UpperLimit = tool.Gdt.ToleranceZoneMm,
                        Unit = "mm",
                        Deviation = r.GdtDeviationMm,
                        IsOk = r.IsOk ?? false,
                        Message = r.Message ?? ""
                    });
                }
                else if (tool != null && tool.Gear != null)
                {
                    // 齒輪為三判定（齒數/齒距/齒寬），不走單值雙邊判定器（會用 MeasuredValue=0 誤判）。
                    // 比照 GD&T：齒輪工具的「所有」報表列都由本分支發出，成功發三列、失敗發一列，
                    // 兩種情況都不落入下方雙邊 Tolerance 分支。齒輪工具的 Tolerance 仍是預設 [0,0]
                    // （非 null，與 GD&T 相同），若失敗時落入雙邊分支，GetMeasuredValue 會回 0，
                    // 而 0∈[0,0] 會被判為 OK，與上方依 r.IsOk 累計的 NG 計數矛盾（正是本任務要避免的陷阱）。
                    string baseName = tool.Name ?? r.Name;
                    if (r.Gear != null && r.Gear.Success)
                    {
                        // 由 RecipeRunner 算好的 GearAnalysisResult 直接發三個 ItemJudgment → CSV 三列。
                        var g = r.Gear;
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = baseName + "-齒數",
                            MeasuredValue = g.ToothCount,
                            Nominal = tool.Gear.NominalToothCount,
                            LowerLimit = tool.Gear.NominalToothCount,
                            UpperLimit = tool.Gear.NominalToothCount,
                            Unit = "count",
                            Deviation = g.ToothCount - tool.Gear.NominalToothCount,
                            IsOk = g.CountOk,
                            Message = "齒數"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = baseName + "-齒距",
                            MeasuredValue = g.PitchMaxDevDeg,
                            Nominal = 0,
                            LowerLimit = 0,
                            UpperLimit = tool.Gear.PitchToleranceDeg,
                            Unit = "deg",
                            Deviation = g.PitchMaxDevDeg,
                            IsOk = g.PitchOk,
                            Message = "齒距最大偏差"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = baseName + "-齒寬",
                            MeasuredValue = g.WidthMaxDevDeg,
                            Nominal = 0,
                            LowerLimit = 0,
                            UpperLimit = tool.Gear.WidthToleranceDeg,
                            Unit = "deg",
                            Deviation = g.WidthMaxDevDeg,
                            IsOk = g.WidthOk,
                            Message = "齒寬最大偏差"
                        });
                    }
                    else
                    {
                        // 量測失敗：發一列失敗訊息（比照最終空公差 else 列），不做雙邊重判。
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = baseName,
                            MeasuredValue = 0,
                            IsOk = r.IsOk ?? false,
                            Message = r.Message ?? ""
                        });
                    }
                }
                else if (tool != null && tool.Tolerance != null)
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
            // 弧形卡尺量的是邊數：報表值必須與 RecipeRunner/overlay 判定的同一個量（邊數），
            // 否則會落到下方 return 0 被重新判定，CSV 與畫面互相矛盾（比照 GD&T 的相同陷阱）。
            // 量測失敗時 ArcEdgeRows 為空 → 0，與 circle 失敗時 DiameterMm=0 一致。
            if (r.ToolType == "arc")
                return r.ArcEdgeRows.Count;
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
