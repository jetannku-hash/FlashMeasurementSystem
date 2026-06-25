using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FlashMeasurementSystem.Application.Reporting;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Domain.Workflow;
using FlashMeasurementSystem.Reporting.Csv;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// CsvMeasurementReportWriter 行為測試：CSV formula injection 防護、欄位跳脫
    /// （逗號/引號/換行/CR）、header 只寫一次、append 累積、null 項目。
    /// </summary>
    public static class CsvReportWriterTests
    {
        public static void Run()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "fms_csv_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".csv");
            try
            {
                IMeasurementReportWriter writer = new CsvMeasurementReportWriter();
                var overall = new WorkflowResult { RecipeName = "recipe,A" };

                // 第一次寫：含 header；測試 formula injection + 各種跳脫
                writer.Append(overall, new List<ItemJudgment>
                {
                    Item("=cmd|' /C calc'!A1", 1.5, "mm", "OK normal"),    // formula injection
                    Item("+danger", 2.0, "mm", "@also bad"),               // + 與 @ 開頭
                    Item("has,comma", 3.0, "m\"m", "line1\nline2"),        // 逗號/引號/換行
                    Item("carriage\rreturn", 4.0, "mm", "tail"),           // CR
                }, path);

                string content = ReadAll(path);

                // header 存在
                if (content.IndexOf("Timestamp,Recipe,Tool,", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("CSV header missing");

                // formula injection：= + @ 開頭欄位必須被前綴單引號中和
                AssertContains(content, "'=cmd", "formula '=' neutralized");
                AssertContains(content, "'+danger", "formula '+' neutralized");
                AssertContains(content, "'@also bad", "formula '@' neutralized");

                // Recipe 名含逗號 → 整欄加引號
                AssertContains(content, "\"recipe,A\"", "comma field quoted");

                // 含逗號的 ToolName → 加引號
                AssertContains(content, "\"has,comma\"", "comma toolname quoted");

                // 引號 → 雙寫並整欄加引號（m"m → "m""m"）
                AssertContains(content, "\"m\"\"m\"", "quote doubled and field quoted");

                // 換行欄位被引號包住（不可破壞列結構）
                AssertContains(content, "\"line1\nline2\"", "newline field quoted");

                // CR 欄位被引號包住
                AssertContains(content, "\"carriage\rreturn\"", "CR field quoted");

                // 數值用 InvariantCulture F6（不受 locale 逗號小數影響）
                AssertContains(content, "1.500000", "measured value invariant F6");

                // 第二次 append：不可再寫 header
                writer.Append(overall, new List<ItemJudgment> { Item("t2", 9.0, "mm", "second run") }, path);
                string content2 = ReadAll(path);
                int headerCount = CountOccurrences(content2, "Timestamp,Recipe,Tool,");
                if (headerCount != 1)
                    throw new InvalidOperationException("Header should appear exactly once after append, got " + headerCount);
                AssertContains(content2, "second run", "second append row present");

                // null 項目清單：不擲例外，且至少有 header
                string path2 = Path.Combine(Path.GetTempPath(),
                    "fms_csv_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".csv");
                try
                {
                    writer.Append(overall, null, path2);
                    string nullContent = ReadAll(path2);
                    AssertContains(nullContent, "Timestamp,Recipe,Tool,", "null items still writes header");
                }
                finally { if (File.Exists(path2)) File.Delete(path2); }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static ItemJudgment Item(string toolName, double measured, string unit, string message)
        {
            return new ItemJudgment
            {
                ToolName = toolName,
                MeasuredValue = measured,
                Nominal = 0.0,
                LowerLimit = -1.0,
                UpperLimit = 1.0,
                Unit = unit,
                Deviation = measured,
                IsOk = true,
                Message = message
            };
        }

        private static string ReadAll(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
                return sr.ReadToEnd();
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
            return count;
        }

        private static void AssertContains(string haystack, string needle, string name)
        {
            if (haystack.IndexOf(needle, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(name + " — expected CSV to contain [" + needle + "]");
        }
    }
}
