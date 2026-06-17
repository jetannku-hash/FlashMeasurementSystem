namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// 量測工具的 ROI 幾何（rect2 慣例）。座標定義於「模板建立時的參考座標系」，
    /// 執行期由座標映射（4.11 ICoordinateMapper）轉到當前影像座標系。
    /// 角度單位為 radian（專案慣例 Phi ∈ (-π, π]）。
    /// </summary>
    public class RoiGeometry
    {
        public double CenterRow { get; set; }
        public double CenterCol { get; set; }
        public double Length1 { get; set; } = 100.0;  // 半長，沿主軸（方向 AngleRad）
        public double Length2 { get; set; } = 50.0;   // 半寬，垂直主軸
        public double AngleRad { get; set; }
    }
}
