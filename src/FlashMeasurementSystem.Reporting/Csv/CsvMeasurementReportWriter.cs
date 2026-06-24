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

            string content = sb.ToString();
            if (!writeHeader)
            {
                File.AppendAllText(filePath, content, Encoding.UTF8);
            }
            else
            {
                File.AppendAllText(filePath, Header + Environment.NewLine + content, Encoding.UTF8);
            }
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
