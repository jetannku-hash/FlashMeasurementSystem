using System;

namespace FlashMeasurementSystem.Domain.ReportRetention
{
    /// <summary>
    /// 報表目錄中的一個候選檔案（純資料，不含任何路徑或檔案系統操作）。
    /// 由呼叫端列舉目錄後填入，讓保留策略可在無 IO 的情況下完整單元測試。
    /// </summary>
    public sealed class ReportFileEntry
    {
        /// <summary>檔名（不含目錄），例如 demo_20260720_101530.pdf。</summary>
        public string FileName { get; set; }

        /// <summary>檔案最後寫入時間（UTC）。僅在檔名時戳相同時作為排序 tie-break。</summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>檔案大小（bytes）。目前的「保留最近 N 組」策略不使用，僅供診斷。</summary>
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// 報表保留策略：只保留最近 N 組報表，其餘刪除（不做年齡/容量規則）。
    /// </summary>
    public sealed class ReportRetentionPolicy
    {
        /// <summary>
        /// 預設保留組數。整個系統唯一的保留數量來源——要調整產線保留量改這裡。
        /// 一組 = 一個 .pdf 加上同名的 _overlay.png。
        /// </summary>
        public const int DefaultRetainedSetCount = 200;

        /// <summary>保留最近幾組報表。</summary>
        public int RetainedSetCount { get; set; }

        public static ReportRetentionPolicy Default()
        {
            return new ReportRetentionPolicy { RetainedSetCount = DefaultRetainedSetCount };
        }
    }
}
