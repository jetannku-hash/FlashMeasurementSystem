using System;
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.Reporting
{
    /// <summary>
    /// 一份量測報表的完整內容描述（render-agnostic，純 Domain DTO）。
    /// 決定「報表要放什麼」，不決定「怎麼畫」；不碰檔案系統、不依賴任何 PDF 函式庫。
    /// </summary>
    public class MeasurementReportModel
    {
        // ---- 表頭 ----
        public string RecipeName { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool AllOk { get; set; }                  // 整體 PASS/FAIL
        public int OkCount { get; set; }
        public int NgCount { get; set; }
        public string PixelSizeText { get; set; } = "";  // 例："10.00 µm (量測分頁)"
        public bool HasMatch { get; set; }               // 本次是否有模板比對姿態
        public string MatchText { get; set; } = "";      // 姿態摘要；無比對時為 ""
        public string Message { get; set; } = "";

        // ---- 明細 ----
        public List<MeasurementReportRow> Rows { get; set; } = new List<MeasurementReportRow>();

        /// <summary>
        /// 要嵌入報表的標註後量測影像路徑；無影像時為 ""。
        /// Domain 只攜帶路徑字串，不讀取、不驗證此檔案（讀檔由渲染層負責）。
        /// </summary>
        public string ImagePath { get; set; } = "";
    }
}
