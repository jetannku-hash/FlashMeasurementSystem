using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.Tolerance;

namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// 單一量測工具定義（配方的組成單位）。以組合方式重用既有 DTO：
    /// 幾何用 <see cref="RoiGeometry"/>、邊緣參數重用 <see cref="EdgeDetectionParameters"/>、
    /// 公差重用 <see cref="ToleranceSpec"/>——不重新定義 Sigma/Threshold/公差欄位。
    /// 執行期量測「結果」不放進工具定義（結果為執行期產物，與配方定義分離）。
    /// </summary>
    public class MeasurementTool
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string ToolType { get; set; } = "edge"; // edge / line / circle / distance / angle

        public RoiGeometry Roi { get; set; } = new RoiGeometry();
        public EdgeDetectionParameters EdgeParameters { get; set; } = EdgeDetectionParameters.Default();
        public ToleranceSpec Tolerance { get; set; } = ToleranceSpec.Default();
    }
}
