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
        // 最小圓度（HALCON 'circularity'，0~1，正圓=1）。沾黏/橋接成一塊的雙孔面積更大，過得了 MinHoleAreaPx，
        // 但圓度會明顯掉下來（實測：單圓 1.00、兩圓重疊 0.63），故以此擋掉併塊而非當成一個大孔回報。
        // 偵測層用；分析器忽略。設 0（或負值）＝停用此濾波，回到只濾面積的舊行為（長孔/腰形孔可如此設定）。
        public double MinCircularity       { get; set; } = 0.80;

        public static HoleArrayAnalysisParameters Default() => new HoleArrayAnalysisParameters();
    }
}
