using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Domain.ReportRetention;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// 報表目錄清理的薄 IO 層：列舉本層檔案 → 交給純決策 <see cref="ReportRetentionPlanner"/>
    /// → 刪除它指名的檔案。決策邏輯不在這裡，這裡也不做任何比對規則。
    /// 全程不丟例外——清理只是維護動作，任何失敗都只回傳一段附在結果標籤上的訊息，
    /// 不中斷、不阻擋量測流程（比照 WritePdfReport 的處理方式）。
    /// </summary>
    internal static class ReportRetentionSweep
    {
        /// <summary>
        /// 清理報表目錄，只保留最近 <see cref="ReportRetentionPolicy.DefaultRetainedSetCount"/> 組。
        /// </summary>
        /// <returns>要接在結果標籤後面的字串；沒有動作時為空字串。</returns>
        public static string Sweep(string reportDir)
        {
            try
            {
                if (string.IsNullOrEmpty(reportDir) || !Directory.Exists(reportDir)) return "";

                // GetFiles() 只列本層，不遞迴子資料夾。
                FileInfo[] files = new DirectoryInfo(reportDir).GetFiles();
                var entries = new List<ReportFileEntry>(files.Length);
                foreach (FileInfo fi in files)
                {
                    entries.Add(new ReportFileEntry
                    {
                        FileName = fi.Name,
                        TimestampUtc = fi.LastWriteTimeUtc,
                        SizeBytes = fi.Length
                    });
                }

                IList<string> doomed = ReportRetentionPlanner.SelectFilesToDelete(
                    entries, ReportRetentionPolicy.Default());
                if (doomed.Count == 0) return "";

                int deleted = 0, skipped = 0;
                foreach (string name in doomed)
                {
                    try
                    {
                        File.Delete(Path.Combine(reportDir, name));
                        deleted++;
                    }
                    catch
                    {
                        // 單檔鎖定/唯讀就跳過，不影響其他檔案。
                        skipped++;
                    }
                }

                if (skipped == 0) return " | 報表清理：刪除 " + deleted + " 檔";
                return " | 報表清理：刪除 " + deleted + " 檔、略過 " + skipped + " 檔";
            }
            catch (Exception ex)
            {
                // 揭露但不中斷：操作員看得到清理沒成功的原因，量測結果與報表不受影響。
                return " | 報表清理失敗：" + ex.Message;
            }
        }
    }
}
