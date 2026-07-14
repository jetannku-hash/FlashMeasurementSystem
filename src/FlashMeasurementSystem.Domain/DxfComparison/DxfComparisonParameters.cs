namespace FlashMeasurementSystem.Domain.DxfComparison
{
    /// <summary>
    /// DXF 輪廓度比對輸入參數（純 DTO，無 HALCON）。像素空間；mm 僅顯示用。
    /// </summary>
    public class DxfComparisonParameters
    {
        public double TolerancePx { get; set; } = 2.0;       // 輪廓度公差 T（px）
        public double MinScore { get; set; } = 0.5;          // find_scaled_shape_model 最低分
        public double ScaleMin { get; set; } = 0.5;          // scale 搜尋下界（無種子時用）
        public double ScaleMax { get; set; } = 2.0;          // scale 搜尋上界（無種子時用）
        public double ScaleSeedPxPerMm { get; set; } = 0.0;  // >0 時作為 scale 種子（px/mm），收在 ±30%
        public double EdgeAlpha { get; set; } = 1.0;         // edges_sub_pix 濾波 alpha
        public double EdgeLowThreshold { get; set; } = 20.0;
        public double EdgeHighThreshold { get; set; } = 40.0;
        public double BandWidthPx { get; set; } = 10.0;      // 邊緣框帶半徑（≈ 數倍 T）
        public int MinNumPoints { get; set; } = 20;          // DXF 曲線最小取樣點
        public double MaxApproxError { get; set; } = 0.25;   // DXF 曲線近似最大誤差（px）

        public static DxfComparisonParameters Default() => new DxfComparisonParameters();
    }
}
