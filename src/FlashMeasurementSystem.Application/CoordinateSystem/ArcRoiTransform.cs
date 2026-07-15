using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Application.CoordinateSystem
{
    /// <summary>
    /// 弧形 ROI 的姿態變換（跟隨工件）。剛體變換下：中心需變換、起始角需旋轉，
    /// 半徑/角度範圍/環寬不變（無縮放）。既有 ICoordinateMapper.TransformRoi 正好回傳
    /// 「變換後中心 + 旋轉後角度」，故直接重用、不需新增 mapper 方法。
    ///
    /// 放在 Application（只用介面 + Domain 型別、無 HALCON）而非 RecipeRunner：
    /// 測試專案不參考 App.Wpf，若放 Runner 內就無法被測試守護（Phase 2 驗證洞的結構性成因）。
    /// 如此 Runner 與 HALCON 對齊測試共用同一份程式碼。
    /// </summary>
    public static class ArcRoiTransform
    {
        /// <summary>回傳套用姿態後的新弧；transform 為 null 或無效時回傳原弧的複本（不變換）。</summary>
        public static ArcMeasureRoi TransformArc(ICoordinateMapper mapper, ArcMeasureRoi arc, RigidTransform transform)
        {
            if (arc == null) return null;
            if (mapper == null || transform == null || !transform.IsValid)
                return Copy(arc);

            TransformedRoi t = mapper.TransformRoi(arc.CenterRow, arc.CenterCol, arc.AngleStart, transform);
            return new ArcMeasureRoi
            {
                CenterRow = t.Row,
                CenterCol = t.Col,
                AngleStart = t.AngleRad,
                Radius = arc.Radius,
                AngleExtent = arc.AngleExtent,
                AnnulusRadius = arc.AnnulusRadius
            };
        }

        private static ArcMeasureRoi Copy(ArcMeasureRoi a)
        {
            return new ArcMeasureRoi
            {
                CenterRow = a.CenterRow, CenterCol = a.CenterCol, Radius = a.Radius,
                AngleStart = a.AngleStart, AngleExtent = a.AngleExtent, AnnulusRadius = a.AnnulusRadius
            };
        }
    }
}
