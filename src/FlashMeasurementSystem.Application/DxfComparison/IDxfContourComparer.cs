using FlashMeasurementSystem.Domain.DxfComparison;

namespace FlashMeasurementSystem.Application.DxfComparison
{
    /// <summary>
    /// DXF 輪廓度比對介面。實作載入 DXF、定位工件、取實際輪廓、算偏差並判定。
    /// </summary>
    public interface IDxfContourComparer<TImage>
    {
        DxfComparisonResult Compare(TImage image, string dxfFilePath, DxfComparisonParameters parameters);
    }
}
