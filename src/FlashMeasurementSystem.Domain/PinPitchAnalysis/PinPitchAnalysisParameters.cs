namespace FlashMeasurementSystem.Domain.PinPitchAnalysis
{
    /// <summary>引腳間距分析參數（純 DTO，無 HALCON）。距離公差以 mm。</summary>
    public class PinPitchAnalysisParameters
    {
        public int    NominalPinCount      { get; set; } = 0;      // ≤0＝不判定引腳數（同 PCD 對 nominal 的處理慣例）
        public double NominalPitchMm        { get; set; } = 0.0;   // 標稱相鄰引腳間距（mm）；使用者需設定
        public double PitchToleranceMm      { get; set; } = 0.1;   // |平均間距−標稱| ≤ 此值
        public double UniformityToleranceMm { get; set; } = 0.05;  // 各間距對均值的最大偏差上限
        public bool   PinIsDark             { get; set; } = true;  // 背光＝亮背景暗引腳（偵測層用；分析器忽略）
        public double MinPinAreaPx          { get; set; } = 20.0;  // blob 最小面積濾雜訊（偵測層用；分析器忽略）

        public static PinPitchAnalysisParameters Default() => new PinPitchAnalysisParameters();
    }
}
