namespace FlashMeasurementSystem.Domain.PcdAnalysis
{
    /// <summary>PCD 螺栓孔圈分析參數（純 DTO，無 HALCON）。距離公差以 mm、角度以度。</summary>
    public class PcdAnalysisParameters
    {
        public int    NominalHoleCount   = 6;
        public double NominalPcdMm        = 0.0;    // 標稱節圓直徑（mm）；使用者需設定
        public double PcdToleranceMm      = 0.1;    // |PCD−標稱| ≤ 此值
        public double AngularToleranceDeg = 1.0;    // 相鄰孔角距對均值的最大偏差
        public double RadialToleranceMm   = 0.05;   // 孔心徑向偏差上限
        public bool   HoleIsDark          = true;   // 背光穿孔＝暗（偵測層用；分析器忽略）
        public double MinHoleAreaPx       = 20.0;   // blob 最小面積濾雜訊（偵測層用；分析器忽略）

        public static PcdAnalysisParameters Default() => new PcdAnalysisParameters();
    }
}
