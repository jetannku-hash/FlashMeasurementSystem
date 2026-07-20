using System;
using System.Collections.Generic;
using System.Globalization;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Domain.Workflow;

namespace FlashMeasurementSystem.Domain.Reporting
{
    /// <summary>
    /// 由一次量測的整體結果 + 逐項判定，組出報表模型（純函式，無副作用）。
    /// 所有數值格式化都在這裡定案，渲染層（PDF）只排版不再改寫文字。
    /// </summary>
    public static class MeasurementReportBuilder
    {
        private const string CountUnit = "count";

        /// <summary>
        /// 組出報表模型。overall 為 null 時不丟例外，回傳含說明訊息、零列的模型；
        /// items 為 null/空時只有表頭、零列。imagePath 原樣攜帶（含 ""）。
        /// </summary>
        public static MeasurementReportModel Build(
            WorkflowResult overall, IList<ItemJudgment> items, string pixelSizeText, string imagePath)
        {
            var model = new MeasurementReportModel
            {
                PixelSizeText = pixelSizeText ?? "",
                ImagePath = imagePath ?? ""
            };

            if (overall == null)
            {
                model.Message = "無量測結果可產生報表。";
                return model;
            }

            model.RecipeName = overall.RecipeName ?? "";
            model.Timestamp = overall.Timestamp;
            model.AllOk = overall.AllOk;
            model.OkCount = overall.OkCount;
            model.NgCount = overall.NgCount;
            model.HasMatch = overall.HasMatch;
            model.MatchText = overall.HasMatch ? FormatMatch(overall) : "";
            model.Message = overall.Message ?? "";

            if (items == null) return model;

            foreach (ItemJudgment item in items)
            {
                if (item == null) continue;
                model.Rows.Add(BuildRow(item));
            }
            return model;
        }

        private static MeasurementReportRow BuildRow(ItemJudgment item)
        {
            string unit = item.Unit ?? "";
            return new MeasurementReportRow
            {
                ItemName = item.ToolName ?? "",
                NominalText = FormatWithUnit(item.Nominal, unit),
                LimitsText = FormatLimits(item.LowerLimit, item.UpperLimit, unit),
                MeasuredText = FormatWithUnit(item.MeasuredValue, unit),
                VerdictText = VerdictOf(item.IsOk),
                IsOk = item.IsOk,
                Note = item.Message ?? ""
            };
        }

        // 小數規則的唯一來源：count 單位視為整數（無小數），其餘 3 位小數。
        private static string FormatNumber(double value, string unit)
        {
            bool isCount = string.Equals(unit, CountUnit, StringComparison.OrdinalIgnoreCase);
            return value.ToString(isCount ? "F0" : "F3", CultureInfo.InvariantCulture);
        }

        // count 不附單位（"20"），其餘附單位（"0.360 mm"）；單位為空也不附。
        private static string FormatWithUnit(double value, string unit)
        {
            string number = FormatNumber(value, unit);
            if (string.IsNullOrEmpty(unit) ||
                string.Equals(unit, CountUnit, StringComparison.OrdinalIgnoreCase))
                return number;
            return number + " " + unit;
        }

        // 上下限相同 = 精確值條件（例如齒數必須剛好 20），只顯示單一值而非 "20 ~ 20"。
        private static string FormatLimits(double lower, double upper, string unit)
        {
            string lo = FormatNumber(lower, unit);
            string hi = FormatNumber(upper, unit);
            return lo == hi ? lo : lo + " ~ " + hi;
        }

        private static string FormatMatch(WorkflowResult overall)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Row {0:F2}, Col {1:F2}, {2:F2}°",
                overall.MatchRow, overall.MatchCol, overall.MatchAngleDeg);
        }

        /// <summary>依 IsOk 決定判定文字（null = 未判定）。渲染層與測試共用同一規則。</summary>
        public static string VerdictOf(bool? isOk)
        {
            return isOk == true ? "OK" : isOk == false ? "NG" : "—";
        }
    }
}
