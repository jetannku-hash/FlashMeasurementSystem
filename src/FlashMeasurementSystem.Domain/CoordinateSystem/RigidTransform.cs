namespace FlashMeasurementSystem.Domain.CoordinateSystem
{
    /// <summary>
    /// 參考座標系 → 當前影像座標系的剛體變換（平移 + 旋轉）。
    /// HomMat2D 為 HALCON hom_mat2d 的 6 個元素（2×3 齊次矩陣），由 vector_angle_to_rigid 產生，
    /// 套用時交回 HALCON（affine_trans_pixel）。Domain 不依賴 HALCON，僅保存數值。
    /// RotationRad / Translation* 為人類可讀的輔助欄位（除錯/顯示用）。
    /// </summary>
    public class RigidTransform
    {
        public double[] HomMat2D { get; set; }

        public double RotationRad { get; set; }       // 旋轉量（當前角度 - 參考角度）
        public double TranslationRow { get; set; }    // Row 方向平移
        public double TranslationCol { get; set; }    // Column 方向平移

        public bool IsValid
        {
            get { return HomMat2D != null && HomMat2D.Length == 6; }
        }
    }
}
