using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FlashMeasurementSystem.Application.Reporting;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Domain.Workflow;

namespace FlashMeasurementSystem.Reporting.Csv
{
    /// <summary>
    /// Writes one measurement run as CSV rows. Creates the file with a header if it does not exist.
    /// Each ItemJudgment is one row: Timestamp, Recipe, Tool, MeasuredValue, Nominal,
    /// LowerLimit, UpperLimit, Unit, Deviation, IsOk, Message.
    /// </summary>
    public class CsvMeasurementReportWriter : IMeasurementReportWriter
    {
        private static readonly string Header =
            "Timestamp,Recipe,Tool,MeasuredValue,Nominal,LowerLimit,UpperLimit,Unit,Deviation,IsOk,Message";

        public void Append(WorkflowResult overall, IList<ItemJudgment> items, string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("filePath is required", nameof(filePath));

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            bool writeHeader = !File.Exists(filePath);

            var sb = new StringBuilder();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string recipe = overall != null ? overall.RecipeName : "";

            if (items != null)
            {
                foreach (ItemJudgment item in items)
                {
                    if (item == null) continue;
                    sb.Append(timestamp).Append(',');
                    sb.Append(CsvEscape(recipe)).Append(',');
                    sb.Append(CsvEscape(item.ToolName ?? "")).Append(',');
                    sb.Append(item.MeasuredValue.ToString("F6", CultureInfo.InvariantCulture)).Append(',');
                    sb.Append(item.Nominal.ToString("F6", CultureInfo.InvariantCulture)).Append(',');
                    sb.Append(item.LowerLimit.ToString("F6", CultureInfo.InvariantCulture)).Append(',');
                    sb.Append(item.UpperLimit.ToString("F6", CultureInfo.InvariantCulture)).Append(',');
                    sb.Append(CsvEscape(item.Unit ?? "")).Append(',');
                    sb.Append(item.Deviation.ToString("F6", CultureInfo.InvariantCulture)).Append(',');
                    sb.Append(item.IsOk ? "OK" : "NG").Append(',');
                    sb.AppendLine(CsvEscape(item.Message ?? ""));
                }
            }

            string rows = sb.ToString();
            string mainPayload = writeHeader ? Header + Environment.NewLine + rows : rows;
            WriteWithFallback(filePath, mainPayload, rows);
        }

        // 寫入主檔；若被鎖（操作員以 Excel 開啟 CSV 很常見）先重試數次，仍失敗則寫到
        // 同目錄附時間戳的 fallback 檔，避免遺失本次量測資料。fallback 為獨立新檔，必含 header。
        private static void WriteWithFallback(string filePath, string mainPayload, string rowsOnly)
        {
            const int maxAttempts = 3;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.AppendAllText(filePath, mainPayload, Encoding.UTF8);
                    return;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    System.Threading.Thread.Sleep(100);
                }
                catch (IOException)
                {
                    string dir = Path.GetDirectoryName(filePath);
                    string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture);
                    string fallback = Path.Combine(
                        string.IsNullOrEmpty(dir) ? "." : dir,
                        Path.GetFileNameWithoutExtension(filePath) + ".locked-" + stamp + Path.GetExtension(filePath));
                    File.AppendAllText(fallback, Header + Environment.NewLine + rowsOnly, Encoding.UTF8);
                    return;
                }
            }
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            // CSV formula injection 防護：欄位以 = + - @（或 Tab/CR）開頭時，Excel/Sheets 會當公式執行。
            // 加前綴單引號中和。數值欄位不經此函式，故不影響負數格式。
            string v = "=+-@\t\r".IndexOf(value[0]) >= 0 ? "'" + value : value;
            if (v.Contains(",") || v.Contains("\"") || v.Contains("\n") || v.Contains("\r"))
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }
    }
}
