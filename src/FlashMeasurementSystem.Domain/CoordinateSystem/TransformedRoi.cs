namespace FlashMeasurementSystem.Domain.CoordinateSystem
{
    /// <summary>
    /// 參考 ROI 經剛體變換後，在當前影像座標系下的中心與角度。
    /// 角度單位為 radian（與專案慣例 Phi ∈ (-π, π] 一致）。
    /// </summary>
    public class TransformedRoi
    {
        public double Row { get; set; }
        public double Col { get; set; }
        public double AngleRad { get; set; }
    }
}
