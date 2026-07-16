namespace FlashMeasurementSystem.Domain.GearAnalysis
{
    /// <summary>齒輪分析輸入參數（純 DTO，無 HALCON）。角度公差以度為單位。</summary>
    public class GearAnalysisParameters
    {
        public int NominalToothCount { get; set; } = 20;
        public bool ToothIsDark { get; set; } = true;        // 背光剪影：齒暗、齒隙亮
        public double PitchToleranceDeg { get; set; } = 1.0;  // 齒距最大偏差上限
        public double WidthToleranceDeg { get; set; } = 2.0;  // 齒寬最大偏差上限

        public static GearAnalysisParameters Default() => new GearAnalysisParameters();
    }
}
