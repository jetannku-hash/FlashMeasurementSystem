using FlashMeasurementSystem.Domain.CoordinateSystem;

namespace FlashMeasurementSystem.Application.CoordinateSystem
{
    /// <summary>
    /// 座標系定位：把模板建立時「參考座標系」下定義的 ROI，
    /// 依模板匹配找到的當前工件姿態，轉換到當前影像座標系。
    /// </summary>
    public interface ICoordinateMapper
    {
        /// <summary>
        /// 由「參考姿態」與「當前匹配姿態」建立剛體變換（含平移 + 旋轉）。
        /// 角度單位為 radian。
        /// </summary>
        RigidTransform CreateFromMatch(
            double refRow, double refCol, double refAngleRad,
            double curRow, double curCol, double curAngleRad);

        /// <summary>
        /// 將參考座標系下的 ROI 中心與角度，套用變換轉到當前影像座標系。
        /// </summary>
        TransformedRoi TransformRoi(
            double refRoiRow, double refRoiCol, double refRoiAngleRad,
            RigidTransform transform);
    }
}
