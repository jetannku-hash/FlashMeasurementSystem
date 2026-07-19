using FlashMeasurementSystem.Domain.Tolerance;

namespace FlashMeasurementSystem.Domain.MetrologyModel
{
    /// <summary>量測模型物件對「某個判定量」(Quantity key) 的公差（沿用 ToleranceSpec，px）。</summary>
    public class MetrologyItemTolerance
    {
        public string Quantity { get; set; } = "";   // diameter/major_axis/minor_axis/side1/side2/length
        public ToleranceSpec Spec { get; set; } = ToleranceSpec.Default();
    }
}
