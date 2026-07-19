namespace FlashMeasurementSystem.Domain.MetrologyModel
{
    /// <summary>單一判定量的判定結果（供 CSV/overlay）。值/上下限皆 px。</summary>
    public class MetrologyJudgment
    {
        public string Quantity { get; set; } = "";
        public string Label { get; set; } = "";
        public double MeasuredValue { get; set; }
        public double Nominal { get; set; }
        public double LowerLimit { get; set; }
        public double UpperLimit { get; set; }
        public string Unit { get; set; } = "";
        public bool IsOk { get; set; }
    }
}
