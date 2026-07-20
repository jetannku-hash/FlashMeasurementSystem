using System;
using System.Collections.Generic;
using System.Globalization;
using FlashMeasurementSystem.Domain.ReportRetention;

namespace FlashMeasurementSystem.Tests
{
    public static class ReportRetentionDomainTests
    {
        // 產生第 i 組（i 越大越新）的 PDF + overlay PNG 兩個檔案。
        private static void AddSet(List<ReportFileEntry> entries, string recipe, int i)
        {
            DateTime t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i);
            string stamp = t.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            entries.Add(new ReportFileEntry
            { FileName = recipe + "_" + stamp + ".pdf", TimestampUtc = t, SizeBytes = 1000 });
            entries.Add(new ReportFileEntry
            { FileName = recipe + "_" + stamp + "_overlay.png", TimestampUtc = t, SizeBytes = 2000 });
        }

        private static ReportRetentionPolicy Retain(int n)
        {
            return new ReportRetentionPolicy { RetainedSetCount = n };
        }

        public static void Run()
        {
            // 空輸入 / null → 不刪任何東西
            AssertEqual(0, ReportRetentionPlanner.SelectFilesToDelete(null, Retain(3)).Count,
                "null entries → nothing deleted");
            AssertEqual(0, ReportRetentionPlanner.SelectFilesToDelete(
                new List<ReportFileEntry>(), Retain(3)).Count, "empty entries → nothing deleted");

            // 組數少於 N → 不刪
            var few = new List<ReportFileEntry>();
            for (int i = 1; i <= 3; i++) AddSet(few, "demo", i);
            AssertEqual(0, ReportRetentionPlanner.SelectFilesToDelete(few, Retain(5)).Count,
                "3 sets with N=5 → nothing deleted");
            AssertEqual(0, ReportRetentionPlanner.SelectFilesToDelete(few, Retain(3)).Count,
                "exactly N sets → nothing deleted");

            // 組數多於 N → 只刪最舊的多餘組，最新 N 組保留；PDF 與 PNG 成對被選中
            var many = new List<ReportFileEntry>();
            for (int i = 1; i <= 5; i++) AddSet(many, "demo", i);
            IList<string> doomed = ReportRetentionPlanner.SelectFilesToDelete(many, Retain(2));
            AssertEqual(6, doomed.Count, "5 sets keep 2 → 3 sets × 2 files deleted");
            for (int i = 1; i <= 3; i++)
            {
                string stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(i).ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                AssertContains(doomed, "demo_" + stamp + ".pdf", "oldest pdf selected");
                AssertContains(doomed, "demo_" + stamp + "_overlay.png", "oldest png selected together");
            }
            for (int i = 4; i <= 5; i++)
            {
                string stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(i).ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                AssertNotContains(doomed, "demo_" + stamp + ".pdf", "newest pdf kept");
                AssertNotContains(doomed, "demo_" + stamp + "_overlay.png", "newest png kept");
            }

            // 保留 0 組 → 全部本系統檔案都刪
            AssertEqual(10, ReportRetentionPlanner.SelectFilesToDelete(many, Retain(0)).Count,
                "N=0 → all recognised files deleted");
            // 負數視為設定錯誤 → 保守不刪
            AssertEqual(0, ReportRetentionPlanner.SelectFilesToDelete(many, Retain(-1)).Count,
                "negative N → nothing deleted");

            // 外來檔名永遠不會被選中（即使 N=0）
            var foreign = new List<ReportFileEntry>();
            for (int i = 1; i <= 3; i++) AddSet(foreign, "demo", i);
            string[] foreignNames = {
                "measurements_20260101.csv",   // CSV 日報表
                "report.pdf",                  // 無時戳
                "demo_20260101.pdf",           // 時戳不完整
                "demo_20261301_000000.pdf",    // 月份 13 → 非法日期
                "demo_20260101_000001.txt",    // 非本系統副檔名
                "demo_20260101_000001_overlay.jpg",
                "_20260101_000001.pdf",        // 無配方名
                "20260101_000001.pdf",         // 無配方名與底線
                "demo_20260101_000001-overlay.png"
            };
            foreach (string name in foreignNames)
                foreign.Add(new ReportFileEntry
                { FileName = name, TimestampUtc = DateTime.UtcNow, SizeBytes = 10 });

            IList<string> doomedAll = ReportRetentionPlanner.SelectFilesToDelete(foreign, Retain(0));
            AssertEqual(6, doomedAll.Count, "only the 3 recognised sets are deletable");
            foreach (string name in foreignNames)
                AssertNotContains(doomedAll, name, "foreign file never selected: " + name);

            // 不同配方名各自成組，依時戳（非配方名）決定新舊
            var mixed = new List<ReportFileEntry>();
            AddSet(mixed, "alpha", 1);   // 最舊
            AddSet(mixed, "beta", 2);
            AddSet(mixed, "gamma", 3);   // 最新
            IList<string> mixedDoomed = ReportRetentionPlanner.SelectFilesToDelete(mixed, Retain(2));
            AssertEqual(2, mixedDoomed.Count, "mixed recipes → oldest set only");
            AssertContains(mixedDoomed, "alpha_20260101_000001.pdf", "oldest recipe set deleted");
            AssertContains(mixedDoomed, "alpha_20260101_000001_overlay.png", "oldest recipe png deleted");

            // 只有 PDF、沒有 PNG（截圖失敗）的組也算一組，被刪時不需要 PNG 存在
            var pdfOnly = new List<ReportFileEntry>
            {
                new ReportFileEntry { FileName = "demo_20260101_000001.pdf", TimestampUtc = DateTime.UtcNow },
                new ReportFileEntry { FileName = "demo_20260101_000002.pdf", TimestampUtc = DateTime.UtcNow }
            };
            IList<string> pdfDoomed = ReportRetentionPlanner.SelectFilesToDelete(pdfOnly, Retain(1));
            AssertEqual(1, pdfDoomed.Count, "pdf-only set counts as a set");
            AssertContains(pdfDoomed, "demo_20260101_000001.pdf", "older pdf-only set deleted");

            // null policy → 使用預設 200 組；199 組不刪，201 組刪最舊一組
            var under = new List<ReportFileEntry>();
            for (int i = 1; i <= ReportRetentionPolicy.DefaultRetainedSetCount - 1; i++)
                AddSet(under, "demo", i);
            AssertEqual(0, ReportRetentionPlanner.SelectFilesToDelete(under, null).Count,
                "N-1 sets with default policy → nothing deleted");

            var over = new List<ReportFileEntry>();
            for (int i = 1; i <= ReportRetentionPolicy.DefaultRetainedSetCount + 1; i++)
                AddSet(over, "demo", i);
            AssertEqual(2, ReportRetentionPlanner.SelectFilesToDelete(over, null).Count,
                "N+1 sets with default policy → oldest set deleted");

            // 預設策略常數
            AssertEqual(200, ReportRetentionPolicy.DefaultRetainedSetCount, "default retained set count");
            AssertEqual(200, ReportRetentionPolicy.Default().RetainedSetCount, "Default() retained set count");
        }

        private static void AssertEqual<T>(T e, T a, string n)
        { if (!object.Equals(e, a)) throw new InvalidOperationException(n + " expected " + e + " got " + a); }

        private static void AssertContains(IList<string> list, string value, string n)
        { if (!list.Contains(value)) throw new InvalidOperationException(n + ": missing " + value); }

        private static void AssertNotContains(IList<string> list, string value, string n)
        { if (list.Contains(value)) throw new InvalidOperationException(n + ": unexpected " + value); }
    }
}
