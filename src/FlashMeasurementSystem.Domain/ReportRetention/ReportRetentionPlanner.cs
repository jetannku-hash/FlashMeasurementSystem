using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlashMeasurementSystem.Domain.ReportRetention
{
    /// <summary>
    /// 純報表保留決策（無 System.IO）：吃檔名清單與策略，回傳「應該刪哪些檔名」。
    /// 只認得本系統一鍵量測產出的兩種檔名：
    ///   {recipe}_{yyyyMMdd_HHmmss}.pdf 與 {recipe}_{yyyyMMdd_HHmmss}_overlay.png
    /// 不符合的檔名（CSV、暫存檔、人工放進來的檔）一律忽略，永遠不會被選中刪除。
    /// PDF 與其 _overlay.png 以「檔名去掉副檔名/去掉 _overlay 後的前綴」配成一組，同進同出，
    /// 不會留下孤兒 PNG。
    /// </summary>
    public static class ReportRetentionPlanner
    {
        private const string StampFormat = "yyyyMMdd_HHmmss";
        private const string OverlaySuffix = "_overlay.png";
        private const string PdfSuffix = ".pdf";

        /// <summary>
        /// 選出應刪除的檔名。保留時戳最新的 policy.RetainedSetCount 組，其餘全部列入刪除。
        /// </summary>
        /// <param name="entries">報表目錄本層的候選檔案（可為 null/空）。</param>
        /// <param name="policy">保留策略；null 時使用預設值。</param>
        /// <returns>應刪除的檔名清單（不含目錄）；沒有東西要刪時為空清單，不回傳 null。</returns>
        public static IList<string> SelectFilesToDelete(
            IList<ReportFileEntry> entries, ReportRetentionPolicy policy)
        {
            var doomed = new List<string>();
            if (entries == null || entries.Count == 0) return doomed;

            ReportRetentionPolicy p = policy ?? ReportRetentionPolicy.Default();
            // 負數視為設定錯誤：寧可不刪，也不要在異常設定下清空整個目錄。
            if (p.RetainedSetCount < 0) return doomed;

            var sets = new Dictionary<string, ReportSet>(StringComparer.Ordinal);
            foreach (ReportFileEntry entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.FileName)) continue;

                string setKey;
                DateTime stamp;
                if (!TryParseReportFileName(entry.FileName, out setKey, out stamp)) continue;

                ReportSet set;
                if (!sets.TryGetValue(setKey, out set))
                {
                    set = new ReportSet { Key = setKey, Stamp = stamp, LatestUtc = entry.TimestampUtc };
                    sets.Add(setKey, set);
                }
                else if (entry.TimestampUtc > set.LatestUtc)
                {
                    set.LatestUtc = entry.TimestampUtc;
                }
                set.FileNames.Add(entry.FileName);
            }

            if (sets.Count <= p.RetainedSetCount) return doomed;

            var ordered = new List<ReportSet>(sets.Values);
            // 新 → 舊：檔名時戳為主，時戳相同再看檔案時間，最後以 key 排序確保結果可重現。
            ordered.Sort(delegate (ReportSet a, ReportSet b)
            {
                int c = b.Stamp.CompareTo(a.Stamp);
                if (c != 0) return c;
                c = b.LatestUtc.CompareTo(a.LatestUtc);
                if (c != 0) return c;
                return string.CompareOrdinal(a.Key, b.Key);
            });

            for (int i = p.RetainedSetCount; i < ordered.Count; i++)
                doomed.AddRange(ordered[i].FileNames);

            doomed.Sort(StringComparer.Ordinal);
            return doomed;
        }

        /// <summary>
        /// 解析本系統產出的報表檔名，取出組鍵（recipe_時戳）與時戳。不符合格式回傳 false。
        /// </summary>
        private static bool TryParseReportFileName(string fileName, out string setKey, out DateTime stamp)
        {
            setKey = null;
            stamp = DateTime.MinValue;

            string baseName;
            if (EndsWith(fileName, OverlaySuffix))
                baseName = fileName.Substring(0, fileName.Length - OverlaySuffix.Length);
            else if (EndsWith(fileName, PdfSuffix))
                baseName = fileName.Substring(0, fileName.Length - PdfSuffix.Length);
            else
                return false;

            // 至少要有 1 個字元的配方名 + '_' + 15 字元時戳。
            if (baseName.Length < StampFormat.Length + 2) return false;
            if (baseName[baseName.Length - StampFormat.Length - 1] != '_') return false;

            string stampText = baseName.Substring(baseName.Length - StampFormat.Length);
            if (!DateTime.TryParseExact(stampText, StampFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out stamp))
                return false;

            setKey = baseName;
            return true;
        }

        private static bool EndsWith(string value, string suffix)
        {
            return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ReportSet
        {
            public string Key;
            public DateTime Stamp;
            public DateTime LatestUtc;
            public readonly List<string> FileNames = new List<string>();
        }
    }
}
