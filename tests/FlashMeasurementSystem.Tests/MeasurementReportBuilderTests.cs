using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.Reporting;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Domain.Workflow;

namespace FlashMeasurementSystem.Tests
{
    public static class MeasurementReportBuilderTests
    {
        private static WorkflowResult Overall()
        {
            return new WorkflowResult
            {
                Success = true,
                AllOk = false,
                OkCount = 2,
                NgCount = 1,
                RecipeName = "demo.zcp",
                Message = "1 項超出公差",
                Timestamp = new DateTime(2026, 7, 20, 13, 45, 0),
                HasMatch = true,
                MatchRow = 240.5,
                MatchCol = 320.25,
                MatchAngleDeg = 12.5
            };
        }

        private static ItemJudgment Item(string name, double measured, double nominal,
            double lower, double upper, string unit, bool isOk, string message = "")
        {
            return new ItemJudgment
            {
                ToolId = name + "-id",
                ToolName = name,
                MeasuredValue = measured,
                Nominal = nominal,
                LowerLimit = lower,
                UpperLimit = upper,
                Unit = unit,
                Deviation = measured - nominal,
                IsOk = isOk,
                Message = message
            };
        }

        public static void Run()
        {
            var items = new List<ItemJudgment>
            {
                Item("寬度", 0.362, 0.360, 0.355, 0.365, "mm", true),
                Item("齒數", 20, 20, 20, 20, "count", true),
                Item("孔徑", 1.480, 1.500, 1.490, 1.510, "mm", false, "低於下限"),
            };

            var m = MeasurementReportBuilder.Build(Overall(), items, "10.00 µm (量測分頁)", @"data\images\run.png");

            // 表頭欄位照抄
            AssertEqual("demo.zcp", m.RecipeName, "RecipeName");
            AssertEqual(new DateTime(2026, 7, 20, 13, 45, 0), m.Timestamp, "Timestamp");
            AssertEqual(false, m.AllOk, "AllOk");
            AssertEqual(2, m.OkCount, "OkCount");
            AssertEqual(1, m.NgCount, "NgCount");
            AssertEqual("10.00 µm (量測分頁)", m.PixelSizeText, "PixelSizeText");
            AssertEqual("1 項超出公差", m.Message, "Message");
            AssertEqual(true, m.HasMatch, "HasMatch");
            if (m.MatchText.Length == 0)
                throw new InvalidOperationException("MatchText should be non-empty when HasMatch");
            AssertEqual(@"data\images\run.png", m.ImagePath, "ImagePath 原樣攜帶");

            // 每個判定一列、順序一致
            AssertEqual(3, m.Rows.Count, "Rows.Count");
            AssertEqual("寬度", m.Rows[0].ItemName, "row0 ItemName");
            AssertEqual("齒數", m.Rows[1].ItemName, "row1 ItemName");
            AssertEqual("孔徑", m.Rows[2].ItemName, "row2 ItemName");

            // mm → 3 位小數 + 單位
            AssertEqual("0.360 mm", m.Rows[0].NominalText, "mm NominalText");
            AssertEqual("0.362 mm", m.Rows[0].MeasuredText, "mm MeasuredText");
            AssertEqual("0.355 ~ 0.365", m.Rows[0].LimitsText, "mm LimitsText");

            // count → 無小數、無單位
            AssertEqual("20", m.Rows[1].NominalText, "count NominalText");
            AssertEqual("20", m.Rows[1].MeasuredText, "count MeasuredText");
            // 上下限相同 → 只顯示單一值，不是 "20 ~ 20"
            AssertEqual("20", m.Rows[1].LimitsText, "count LimitsText single value");

            // 判定映射與備註
            AssertEqual("OK", m.Rows[0].VerdictText, "OK verdict");
            AssertEqual(true, m.Rows[0].IsOk, "row0 IsOk");
            AssertEqual("", m.Rows[0].Note, "row0 Note empty");
            AssertEqual("NG", m.Rows[2].VerdictText, "NG verdict");
            AssertEqual(false, m.Rows[2].IsOk, "row2 IsOk");
            AssertEqual("低於下限", m.Rows[2].Note, "row2 Note");

            // 未判定（ItemJudgment.IsOk 為非 nullable bool，故 null 只會出現在自建/未來未判定列）
            AssertEqual("OK", MeasurementReportBuilder.VerdictOf(true), "VerdictOf(true)");
            AssertEqual("NG", MeasurementReportBuilder.VerdictOf(false), "VerdictOf(false)");
            AssertEqual("—", MeasurementReportBuilder.VerdictOf(null), "VerdictOf(null)");
            var unjudged = new MeasurementReportRow { IsOk = null, VerdictText = MeasurementReportBuilder.VerdictOf(null) };
            AssertEqual("—", unjudged.VerdictText, "unjudged row verdict");

            // AllOk / 計數在全 OK 情況也帶得過
            var allOk = MeasurementReportBuilder.Build(
                new WorkflowResult { AllOk = true, OkCount = 3, NgCount = 0, RecipeName = "r" },
                items, "", "");
            AssertEqual(true, allOk.AllOk, "AllOk true carried");
            AssertEqual(3, allOk.OkCount, "OkCount carried");
            AssertEqual(0, allOk.NgCount, "NgCount carried");
            AssertEqual("", allOk.ImagePath, "空 imagePath 原樣攜帶");
            AssertEqual(false, allOk.HasMatch, "no match");
            AssertEqual("", allOk.MatchText, "MatchText empty when no match");

            // overall 為 null → 不丟例外、零列、有說明訊息
            var nullOverall = MeasurementReportBuilder.Build(null, items, "px", "img.png");
            AssertEqual(0, nullOverall.Rows.Count, "null overall → 0 rows");
            AssertEqual("px", nullOverall.PixelSizeText, "null overall keeps pixelSizeText");
            AssertEqual("img.png", nullOverall.ImagePath, "null overall keeps imagePath");
            if (nullOverall.Message.Length == 0)
                throw new InvalidOperationException("null overall should carry a message");

            // items 為 null / 空 → 只有表頭、零列
            var nullItems = MeasurementReportBuilder.Build(Overall(), null, "", "");
            AssertEqual("demo.zcp", nullItems.RecipeName, "null items keeps header");
            AssertEqual(0, nullItems.Rows.Count, "null items → 0 rows");
            var emptyItems = MeasurementReportBuilder.Build(Overall(), new List<ItemJudgment>(), "", "");
            AssertEqual(0, emptyItems.Rows.Count, "empty items → 0 rows");

            // null 參數不炸
            var nullText = MeasurementReportBuilder.Build(Overall(), items, null, null);
            AssertEqual("", nullText.PixelSizeText, "null pixelSizeText → \"\"");
            AssertEqual("", nullText.ImagePath, "null imagePath → \"\"");
        }

        private static void AssertEqual<T>(T e, T a, string n)
        { if (!object.Equals(e, a)) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
    }
}
