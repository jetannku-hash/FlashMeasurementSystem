using FlashMeasurementSystem.Domain.Reporting;

namespace FlashMeasurementSystem.Application.Reporting
{
    /// <summary>
    /// 將一份量測報表模型輸出成單一 PDF 檔案（每次量測一個檔）。
    /// 刻意與 <see cref="IMeasurementReportWriter"/> 分開：後者是 CSV 的 append-per-run 語意，
    /// 與 PDF「一次寫完一整份檔案」的語意不同。
    /// </summary>
    public interface IMeasurementPdfReportWriter
    {
        /// <summary>
        /// 將 <paramref name="model"/> 渲染成 PDF 並寫入 <paramref name="filePath"/>（既有檔案會被覆蓋）。
        /// </summary>
        void Write(MeasurementReportModel model, string filePath);
    }
}
