namespace FlashMeasurementSystem.Domain.Tolerance
{
    /// <summary>
    /// 公差判定的輸入：一個量測項目的實測值 + 其公差規格。
    /// 通常由 .zcp 配方提供 Spec，由量測流程填入 MeasuredValue。
    /// </summary>
    public class ToleranceItemInput
    {
        public string ToolId { get; set; } = "";
        public string ToolName { get; set; } = "";
        public double MeasuredValue { get; set; }
        public ToleranceSpec Spec { get; set; } = ToleranceSpec.Default();
    }
}
