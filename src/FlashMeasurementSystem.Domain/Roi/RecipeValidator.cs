using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.Tolerance;

namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// 配方執行前的純邏輯診斷（無 HALCON/UI/檔案）。把會讓 Run 失敗或結果無意義的問題，
    /// 在按下量測「之前」就列出來，操作員不必靠 Run 失敗反推。
    /// 嚴重度：Error＝量測無法進行（應阻擋）；Warning＝可疑但可繼續。
    /// 檢查規則對齊 <c>RecipeRunner</c> 的執行語意（各型別的參考數量/型別、GD&T 規格、ROI、公差）。
    /// imageWidth/Height ≤ 0 時略過影像邊界檢查（例如尚未載入影像即檢查配方）。
    /// </summary>
    public static class RecipeValidator
    {
        // 以 ROI 直接量測的元素型別（需檢查 ROI 幾何）。
        private static readonly HashSet<string> RoiElementTypes =
            new HashSet<string> { "circle", "line", "edge" };

        // 已知型別；未列入者執行時會被略過，故視為 Warning。
        private static readonly HashSet<string> KnownTypes = new HashSet<string>
        {
            "circle", "line", "edge",
            "intersection", "midline", "projection",
            "roundness", "straightness", "parallelism", "perpendicularity", "concentricity",
            "distance", "angle"
        };

        public static List<RecipeIssue> Validate(Recipe recipe, int imageWidth, int imageHeight)
        {
            var issues = new List<RecipeIssue>();

            if (recipe == null)
            {
                issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, "", "", "配方為空 (null)"));
                return issues;
            }
            if (recipe.Tools == null || recipe.Tools.Count == 0)
            {
                issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, "", "", "配方沒有任何量測工具"));
                return issues;
            }

            // 宣告跟隨工件，但參考姿態全為 0 → 可能忘了設模板/參考姿態。
            if (recipe.HasReferencePose
                && recipe.RefRow == 0.0 && recipe.RefCol == 0.0 && recipe.RefAngleRad == 0.0)
            {
                issues.Add(new RecipeIssue(RecipeIssueSeverity.Warning, "", "",
                    "已啟用參考姿態跟隨，但參考姿態全為 0（可能未設定模板/參考姿態）"));
            }

            // 建立 Id→工具索引，順帶偵測重複 Id（會讓參考解析錯亂）。
            var byId = new Dictionary<string, MeasurementTool>();
            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null || string.IsNullOrEmpty(tool.Id)) continue;
                if (byId.ContainsKey(tool.Id))
                {
                    issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                        "工具 Id 重複：'" + tool.Id + "'（會導致參考解析錯誤）"));
                }
                else
                {
                    byId[tool.Id] = tool;
                }
            }

            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null)
                {
                    issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, "", "", "配方含空工具 (null)"));
                    continue;
                }

                string type = tool.ToolType ?? "";

                if (!KnownTypes.Contains(type))
                {
                    issues.Add(new RecipeIssue(RecipeIssueSeverity.Warning, tool.Id, tool.Name,
                        "未支援的工具型別 '" + type + "'，執行時將被略過"));
                    continue;
                }

                if (RoiElementTypes.Contains(type))
                {
                    ValidateRoi(issues, tool, imageWidth, imageHeight);
                }

                // 公差反向（任何可能帶尺寸/角度公差的工具）。
                ToleranceSpec tol = tool.Tolerance;
                if (tol != null && tol.UpperTolerance < tol.LowerTolerance)
                {
                    issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                        "公差上限小於下限（Upper < Lower），請修正"));
                }

                ValidateReferences(issues, tool, type, byId);
            }

            return issues;
        }

        private static void ValidateRoi(List<RecipeIssue> issues, MeasurementTool tool, int w, int h)
        {
            RoiGeometry roi = tool.Roi;
            if (roi == null)
            {
                issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name, "ROI 未設定 (null)"));
                return;
            }
            if (roi.Length1 <= 0.0 || roi.Length2 <= 0.0)
            {
                issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                    "ROI 尺寸無效（半長/半寬須 > 0）"));
            }
            if (w > 0 && h > 0)
            {
                if (roi.CenterCol < 0 || roi.CenterCol >= w || roi.CenterRow < 0 || roi.CenterRow >= h)
                {
                    issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                        "ROI 中心超出影像範圍"));
                }
                else
                {
                    // 旋轉矩形最遠角距中心 = sqrt(L1²+L2²)，用保守外接框避免方向慣例造成誤報。
                    double reach = Math.Sqrt(roi.Length1 * roi.Length1 + roi.Length2 * roi.Length2);
                    if (roi.CenterCol - reach < 0 || roi.CenterCol + reach > w
                        || roi.CenterRow - reach < 0 || roi.CenterRow + reach > h)
                    {
                        issues.Add(new RecipeIssue(RecipeIssueSeverity.Warning, tool.Id, tool.Name,
                            "ROI 部分超出影像範圍"));
                    }
                }
            }
        }

        private static void ValidateReferences(List<RecipeIssue> issues, MeasurementTool tool,
            string type, Dictionary<string, MeasurementTool> byId)
        {
            switch (type)
            {
                // 複合工具：消費參考工具的幾何輸出（line/circle/構造皆有 OutputPrimitive）。
                case "distance":
                    RequireRefs(issues, tool, byId, 2,
                        new[] { "line", "circle", "intersection", "midline", "projection" });
                    break;
                case "angle":
                    // 角度需兩條「線」幾何（line 元素或構造中線）。
                    RequireRefs(issues, tool, byId, 2, new[] { "line", "midline" });
                    break;

                // 構造工具：只參照基礎元件（line/circle），不支援鏈式構造。
                case "intersection":
                case "midline":
                    RequireRefs(issues, tool, byId, 2, new[] { "line" });
                    break;
                case "projection":
                    RequireRefs(issues, tool, byId, 2, new[] { "line", "circle" });
                    RequireProjectionPair(issues, tool, byId);
                    break;

                // GD&T：需 Gdt 規格 + 對應型別的基礎元件。
                case "roundness":
                    ValidateGdtSpec(issues, tool);
                    RequireRefs(issues, tool, byId, 1, new[] { "circle" });
                    break;
                case "straightness":
                    ValidateGdtSpec(issues, tool);
                    RequireRefs(issues, tool, byId, 1, new[] { "line" });
                    break;
                case "parallelism":
                case "perpendicularity":
                    ValidateGdtSpec(issues, tool);
                    RequireRefs(issues, tool, byId, 2, new[] { "line" });
                    break;
                case "concentricity":
                    ValidateGdtSpec(issues, tool);
                    RequireRefs(issues, tool, byId, 2, new[] { "circle" });
                    break;

                // circle / line / edge：自足元素，無參考。
            }
        }

        private static void RequireRefs(List<RecipeIssue> issues, MeasurementTool tool,
            Dictionary<string, MeasurementTool> byId, int count, string[] allowedTypes)
        {
            List<string> refs = tool.RefToolIds;
            int have = refs == null ? 0 : refs.Count;
            if (have < count)
            {
                issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                    tool.ToolType + " 需要 " + count + " 個參考元素，目前 " + have + " 個"));
                return;
            }

            var allowed = new HashSet<string>(allowedTypes);
            for (int i = 0; i < count; i++)
            {
                string refId = refs[i];
                if (string.IsNullOrEmpty(refId))
                {
                    issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                        "第 " + (i + 1) + " 個參考元素未指定"));
                    continue;
                }
                if (!string.IsNullOrEmpty(tool.Id) && refId == tool.Id)
                {
                    issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                        "工具參考了自己"));
                    continue;
                }
                MeasurementTool refTool;
                if (!byId.TryGetValue(refId, out refTool))
                {
                    issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                        "找不到參考元素 '" + refId + "'"));
                    continue;
                }
                if (!allowed.Contains(refTool.ToolType ?? ""))
                {
                    issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                        tool.ToolType + " 不可參考型別 '" + refTool.ToolType + "'"));
                }
            }
        }

        // projection 需要一個 circle 與一個 line（順序不限）。
        private static void RequireProjectionPair(List<RecipeIssue> issues, MeasurementTool tool,
            Dictionary<string, MeasurementTool> byId)
        {
            List<string> refs = tool.RefToolIds;
            if (refs == null || refs.Count < 2) return;  // 數量不足已由 RequireRefs 回報

            bool hasCircle = false, hasLine = false;
            for (int i = 0; i < 2; i++)
            {
                MeasurementTool rt;
                if (!string.IsNullOrEmpty(refs[i]) && byId.TryGetValue(refs[i], out rt))
                {
                    if (rt.ToolType == "circle") hasCircle = true;
                    else if (rt.ToolType == "line") hasLine = true;
                }
            }
            if (!(hasCircle && hasLine))
            {
                issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                    "projection 需要一個 circle 與一個 line 參考元素"));
            }
        }

        private static void ValidateGdtSpec(List<RecipeIssue> issues, MeasurementTool tool)
        {
            if (tool.Gdt == null)
            {
                issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                    "形位公差工具缺少 GD&T 規格"));
                return;
            }
            if (tool.Gdt.ToleranceZoneMm <= 0.0)
            {
                issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                    "形位公差帶寬須 > 0"));
            }
        }
    }
}
