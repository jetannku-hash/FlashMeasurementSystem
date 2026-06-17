namespace FlashMeasurementSystem.Domain.Tolerance
{
    /// <summary>
    /// 單一量測項目的判定結果。
    /// MarginPercent = 到最近邊界的餘量 / 公差半寬 × 100：
    ///   OK 時為正（100% = 剛好落在 nominal 中心，越接近 0 越靠邊界）；
    ///   NG 時為負（超出邊界的程度）。
    /// </summary>
    public class ItemJudgment
    {
        public string ToolId { get; set; } = "";
        public string ToolName { get; set; } = "";
        public double MeasuredValue { get; set; }
        public double Nominal { get; set; }
        public double LowerLimit { get; set; }
        public double UpperLimit { get; set; }
        public double Deviation { get; set; }        // 實測 - nominal
        public double DeviationPercent { get; set; } // 偏差 / nominal × 100
        public double MarginPercent { get; set; }    // 到最近邊界餘量百分比
        public bool IsOk { get; set; }
        public bool IsNearBoundary { get; set; }     // OK 但接近公差邊界
        public string Unit { get; set; } = "mm";
        public string Message { get; set; } = "";
    }
}
