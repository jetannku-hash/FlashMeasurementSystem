namespace FlashMeasurementSystem.Domain.HoleArrayAnalysis
{
    /// <summary>孔陣列（rows×cols 矩形網格）分析參數（純 DTO，無 HALCON）。距離公差以 mm。</summary>
    public class HoleArrayAnalysisParameters
    {
        public int    Rows                 { get; set; } = 1;      // 網格列數（次軸 v 方向），≥1
        public int    Cols                 { get; set; } = 1;      // 網格行數（主軸 u 方向），≥1
        public double NominalDiameterMm    { get; set; } = 0.0;    // 標稱孔徑（mm）；使用者需設定
        public double DiameterToleranceMm  { get; set; } = 0.05;   // |平均孔徑−標稱| ≤ 此值
        public double NominalPitchXMm      { get; set; } = 0.0;    // 標稱 X（主軸）孔距（mm）
        public double NominalPitchYMm      { get; set; } = 0.0;    // 標稱 Y（次軸）孔距（mm）
        public double PitchToleranceMm     { get; set; } = 0.1;    // |量測孔距−標稱| ≤ 此值（X/Y 共用）
        public double PositionToleranceMm  { get; set; } = 0.1;    // 各孔對理想網格節點的最大偏差上限
        public bool   HoleIsDark           { get; set; } = true;   // 背光＝亮背景暗孔（偵測層用；分析器忽略）
        public double MinHoleAreaPx        { get; set; } = 20.0;   // blob 最小面積濾雜訊（偵測層用；分析器忽略）

        public static HoleArrayAnalysisParameters Default() => new HoleArrayAnalysisParameters();
    }
}
