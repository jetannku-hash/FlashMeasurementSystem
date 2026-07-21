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
        /// <param name="itemJudgments">
        /// [out] Per-item judgments — exactly the rows written to the CSV. Surfaced so the UI
        /// can build the PDF report from the same data without re-deriving it. Empty on early
        /// failure returns (mirrors <paramref name="toolResults"/>).
        /// </param>
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
            out List<ToolRunResult> toolResults,
            out List<ItemJudgment> itemJudgments)
        {
            var result = new WorkflowResult
            {
                RecipeName = recipe != null ? recipe.Name : "",
                Timestamp = DateTime.Now
            };

            toolResults = new List<ToolRunResult>();
            itemJudgments = new List<ItemJudgment>();

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
                // 門檻取自配方（未設定則回退全域預設）：亮度/銳利度的合格範圍取決於工件、
                // 鏡頭與打光，不同料號本來就該有不同門檻。
                ImageQualityResult iqResult = _iqc.Check(image, recipe.EffectiveIqcThresholds());
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
            // 與 itemJudgments 同一個 list：下方沿用既有的 judgments 區域名稱不動，
            // 但呼叫端（PDF 報表）拿到的就是寫進 CSV 的同一批列。
            List<ItemJudgment> judgments = itemJudgments;
            foreach (ToolRunResult r in toolResults)
            {
                if (r == null) continue;
                // 計數規則走 Domain 的單一來源（MeasurementOutcome）。規則的理由與三態語意
                // 記在該類別上；此處若再寫一份，就會重演「UI 那份少一條規則→假 PASS」。
                if (MeasurementOutcome.CountsAsOk(r.IsOk)) result.OkCount++;
                else if (MeasurementOutcome.CountsAsNg(r.IsOk, r.Supported, r.Measured)) result.NgCount++;

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
                else if (tool != null && tool.Pcd != null)
                {
                    // PCD 為四判定（孔數/PCD/角均勻/徑向真圓度），不走單值雙邊判定器（會用 MeasuredValue=0 誤判）。
                    // 比照齒輪：PCD 工具的「所有」報表列都由本分支發出，成功發四列、失敗發一列，
                    // 兩種情況都不落入下方雙邊 Tolerance 分支。
                    string pcdBaseName = tool.Name ?? r.Name;
                    if (r.Pcd != null && r.Pcd.Success)
                    {
                        var g = r.Pcd;
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = pcdBaseName + "-孔數",
                            MeasuredValue = g.HoleCount,
                            Nominal = tool.Pcd.NominalHoleCount,
                            LowerLimit = tool.Pcd.NominalHoleCount,
                            UpperLimit = tool.Pcd.NominalHoleCount,
                            Unit = "count",
                            Deviation = g.HoleCount - tool.Pcd.NominalHoleCount,
                            IsOk = g.CountOk,
                            Message = "孔數"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = pcdBaseName + "-PCD",
                            MeasuredValue = g.PcdMm,
                            Nominal = tool.Pcd.NominalPcdMm,
                            LowerLimit = tool.Pcd.NominalPcdMm - tool.Pcd.PcdToleranceMm,
                            UpperLimit = tool.Pcd.NominalPcdMm + tool.Pcd.PcdToleranceMm,
                            Unit = "mm",
                            Deviation = g.PcdMm - tool.Pcd.NominalPcdMm,
                            IsOk = g.PcdOk,
                            Message = "節圓直徑"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = pcdBaseName + "-角均勻",
                            MeasuredValue = g.AngularMaxDevDeg,
                            Nominal = 0,
                            LowerLimit = 0,
                            UpperLimit = tool.Pcd.AngularToleranceDeg,
                            Unit = "deg",
                            Deviation = g.AngularMaxDevDeg,
                            IsOk = g.AngularOk,
                            Message = "角度最大偏差"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = pcdBaseName + "-徑向真圓度",
                            MeasuredValue = g.RadialMaxDevMm,
                            Nominal = 0,
                            LowerLimit = 0,
                            UpperLimit = tool.Pcd.RadialToleranceMm,
                            Unit = "mm",
                            Deviation = g.RadialMaxDevMm,
                            IsOk = g.RadialOk,
                            Message = "徑向最大偏差"
                        });
                    }
                    else
                    {
                        // 量測失敗：發一列失敗訊息（比照齒輪分支的失敗 else 列），不做雙邊重判。
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = pcdBaseName,
                            MeasuredValue = 0,
                            IsOk = r.IsOk ?? false,
                            Message = r.Message ?? ""
                        });
                    }
                }
                else if (tool != null && tool.PinPitch != null)
                {
                    // 引腳間距為四判定（引腳數/平均間距/均勻度/缺腳），不走單值雙邊判定器（會用 MeasuredValue=0 誤判）。
                    // 比照 PCD：pin_pitch 工具的「所有」報表列都由本分支發出，成功發四列、失敗發一列，
                    // 兩種情況都不落入下方雙邊 Tolerance 分支（Tolerance 為預設 [0,0]，0∈[0,0] 會被誤判為 OK＝假 PASS）。
                    string ppBaseName = tool.Name ?? r.Name;
                    if (r.PinPitch != null && r.PinPitch.Success)
                    {
                        var g = r.PinPitch;
                        // 引腳數：NominalPinCount ≤ 0 代表不判定（分析器已令 CountOk=true）；仍發一列，用其 CountOk。
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = ppBaseName + "-引腳數",
                            MeasuredValue = g.PinCount,
                            Nominal = tool.PinPitch.NominalPinCount,
                            LowerLimit = tool.PinPitch.NominalPinCount,
                            UpperLimit = tool.PinPitch.NominalPinCount,
                            Unit = "count",
                            Deviation = g.PinCount - tool.PinPitch.NominalPinCount,
                            IsOk = g.CountOk,
                            Message = "引腳數"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = ppBaseName + "-平均間距",
                            MeasuredValue = g.PitchMeanMm,
                            Nominal = tool.PinPitch.NominalPitchMm,
                            LowerLimit = tool.PinPitch.NominalPitchMm - tool.PinPitch.PitchToleranceMm,
                            UpperLimit = tool.PinPitch.NominalPitchMm + tool.PinPitch.PitchToleranceMm,
                            Unit = "mm",
                            Deviation = g.PitchMeanMm - tool.PinPitch.NominalPitchMm,
                            IsOk = g.PitchOk,
                            Message = "平均間距"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = ppBaseName + "-間距均勻度",
                            MeasuredValue = g.PitchMaxDevMm,
                            Nominal = 0,
                            LowerLimit = 0,
                            UpperLimit = tool.PinPitch.UniformityToleranceMm,
                            Unit = "mm",
                            Deviation = g.PitchMaxDevMm,
                            IsOk = g.UniformityOk,
                            Message = "間距最大偏差"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = ppBaseName + "-缺腳",
                            MeasuredValue = 0,
                            IsOk = g.MissingOk,
                            Message = g.MissingOk ? "無缺腳" : ("缺腳" + (string.IsNullOrEmpty(g.MissingHint) ? "" : "：" + g.MissingHint))
                        });
                    }
                    else
                    {
                        // 量測失敗：發一列失敗訊息（比照 PCD 分支的失敗 else 列），不做雙邊重判。
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = ppBaseName + " 引腳間距",
                            MeasuredValue = 0,
                            IsOk = r.IsOk ?? false,
                            Message = string.IsNullOrEmpty(r.ValueText) ? (r.PinPitch != null ? r.PinPitch.ErrorMessage : (r.Message ?? "")) : r.ValueText
                        });
                    }
                }
                else if (tool != null && tool.HoleArray != null)
                {
                    // 孔陣列為六判定（孔數/平均孔徑/X 間距/Y 間距/位置偏差/孔徑最大偏差），不走單值雙邊判定器（會用 MeasuredValue=0 誤判）。
                    // 比照 pin_pitch：hole_array 工具的「所有」報表列都由本分支發出，成功發六列、失敗發一列，
                    // 兩種情況都不落入下方雙邊 Tolerance 分支（Tolerance 為預設 [0,0]，0∈[0,0] 會被誤判為 OK＝假 PASS）。
                    string haBaseName = tool.Name ?? r.Name;
                    if (r.HoleArray != null && r.HoleArray.Success)
                    {
                        var g = r.HoleArray;
                        int nominalCount = tool.HoleArray.Rows * tool.HoleArray.Cols;
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = haBaseName + "-孔數",
                            MeasuredValue = g.HoleCount,
                            Nominal = nominalCount,
                            LowerLimit = nominalCount,
                            UpperLimit = nominalCount,
                            Unit = "count",
                            Deviation = g.HoleCount - nominalCount,
                            IsOk = g.CountOk,
                            Message = "孔數"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = haBaseName + "-平均孔徑",
                            MeasuredValue = g.MeanDiameterMm,
                            Nominal = tool.HoleArray.NominalDiameterMm,
                            LowerLimit = tool.HoleArray.NominalDiameterMm - tool.HoleArray.DiameterToleranceMm,
                            UpperLimit = tool.HoleArray.NominalDiameterMm + tool.HoleArray.DiameterToleranceMm,
                            Unit = "mm",
                            Deviation = g.MeanDiameterMm - tool.HoleArray.NominalDiameterMm,
                            IsOk = g.DiameterOk,
                            Message = "平均孔徑"
                        });
                        // X/Y 間距：Cols ≤ 1 / Rows ≤ 1 代表不判定（分析器已令 PitchXOk / PitchYOk = true）；
                        // 仍發一列，直接採用分析器的旗標（比照引腳數 NominalPinCount ≤ 0 的處理）。
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = haBaseName + "-X間距",
                            MeasuredValue = g.PitchXMm,
                            Nominal = tool.HoleArray.NominalPitchXMm,
                            LowerLimit = tool.HoleArray.NominalPitchXMm - tool.HoleArray.PitchToleranceMm,
                            UpperLimit = tool.HoleArray.NominalPitchXMm + tool.HoleArray.PitchToleranceMm,
                            Unit = "mm",
                            Deviation = g.PitchXMm - tool.HoleArray.NominalPitchXMm,
                            IsOk = g.PitchXOk,
                            // 單行(Cols≤1)無 X 間距可量，分析器令 PitchXOk=true。明講「未判定」，
                            // 否則報表上的 0.000mm/OK 會被誤讀成真的量到 0。
                            Message = tool.HoleArray.Cols <= 1 ? "X 方向孔距（單行，未判定）" : "X 方向孔距"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = haBaseName + "-Y間距",
                            MeasuredValue = g.PitchYMm,
                            Nominal = tool.HoleArray.NominalPitchYMm,
                            LowerLimit = tool.HoleArray.NominalPitchYMm - tool.HoleArray.PitchToleranceMm,
                            UpperLimit = tool.HoleArray.NominalPitchYMm + tool.HoleArray.PitchToleranceMm,
                            Unit = "mm",
                            Deviation = g.PitchYMm - tool.HoleArray.NominalPitchYMm,
                            IsOk = g.PitchYOk,
                            Message = tool.HoleArray.Rows <= 1 ? "Y 方向孔距（單列，未判定）" : "Y 方向孔距"
                        });
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = haBaseName + "-位置偏差",
                            MeasuredValue = g.MaxPositionDevMm,
                            Nominal = 0,
                            LowerLimit = 0,
                            UpperLimit = tool.HoleArray.PositionToleranceMm,
                            Unit = "mm",
                            Deviation = g.MaxPositionDevMm,
                            IsOk = g.PositionOk,
                            Message = "位置最大偏差"
                        });
                        // 平均孔徑會把單顆嚴重超規的孔平均掉，故另發一列逐孔最大偏差（對標稱孔徑）。
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = haBaseName + "-孔徑最大偏差",
                            MeasuredValue = g.DiameterMaxDevMm,
                            Nominal = 0,
                            LowerLimit = 0,
                            UpperLimit = tool.HoleArray.DiameterToleranceMm,
                            Unit = "mm",
                            Deviation = g.DiameterMaxDevMm,
                            IsOk = g.DiameterMaxDevOk,
                            Message = "孔徑最大偏差"
                        });
                    }
                    else
                    {
                        // 量測失敗：發一列失敗訊息（比照 pin_pitch 分支的失敗 else 列），不做雙邊重判。
                        judgments.Add(new ItemJudgment
                        {
                            ToolId = tool.Id ?? "",
                            ToolName = haBaseName + " 孔陣列",
                            MeasuredValue = 0,
                            IsOk = r.IsOk ?? false,
                            Message = string.IsNullOrEmpty(r.ValueText) ? (r.HoleArray != null ? r.HoleArray.ErrorMessage : (r.Message ?? "")) : r.ValueText
                        });
                    }
                }
                // 只有真的量得出單一純量的型別才走雙邊公差判定（共用 Domain 的定義，避免與
                // RecipeValidator 的同一份知識漂移）。原本這裡是 catch-all：構造工具
                // （intersection/midline/projection）沒有可判定的量，GetMeasuredValue 對它們回傳 0，
                // 而 RecipeEditor 一律給預設公差 [0,0]，於是 0 落在 [0,0] 內 → 每次量測都在
                // CSV/PDF 產生一列偽造的「OK（偏差 0.0000）」，構造失敗時也照樣寫 OK。
                // ItemJudgment.IsOk 是 bool 沒有「未判定」狀態，故這類工具改為不產生判定列——
                // 它們的幾何結果仍在畫面 overlay 與結果表上，只是不進合格與否的報表。
                else if (tool != null && tool.Tolerance != null
                         && ToolTypes.IsDoubleSidedTolerance(r.ToolType))
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
                else if (r.ToolType != null && r.ToolType.StartsWith("metrology_"))
                {
                    // metrology 物件：每個「判定量」發一列（比照 pcd 多列，px）。有設公差才有判定列。
                    string mName = r.Name ?? "";
                    if (r.MetrologyJudgments != null && r.MetrologyJudgments.Count > 0)
                    {
                        foreach (var mj in r.MetrologyJudgments)
                            judgments.Add(new ItemJudgment
                            {
                                ToolId = "",
                                ToolName = mName + "-" + mj.Label,
                                MeasuredValue = mj.MeasuredValue,
                                Nominal = mj.Nominal,
                                LowerLimit = mj.LowerLimit,
                                UpperLimit = mj.UpperLimit,
                                Unit = string.IsNullOrEmpty(mj.Unit) ? "px" : mj.Unit,
                                Deviation = mj.MeasuredValue - mj.Nominal,
                                IsOk = mj.IsOk,
                                Message = mj.Label
                            });
                    }
                    else
                    {
                        // 量測成功但未設公差 → 資訊列（以量測成功當 OK）；量測失敗 → NG。
                        judgments.Add(new ItemJudgment
                        {
                            ToolName = mName,
                            MeasuredValue = 0,
                            IsOk = r.Measured,
                            Message = string.IsNullOrEmpty(r.ValueText) ? (r.Message ?? "") : r.ValueText
                        });
                    }
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
