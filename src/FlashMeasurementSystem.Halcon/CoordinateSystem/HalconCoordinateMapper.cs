using System;
using FlashMeasurementSystem.Application.CoordinateSystem;
using FlashMeasurementSystem.Domain.CoordinateSystem;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.CoordinateSystem
{
    /// <summary>
    /// 用 HALCON 算子實作座標系定位，避免手刻旋轉矩陣的誤差與原點假設問題。
    /// CreateFromMatch 用 vector_angle_to_rigid 取得剛體變換矩陣；
    /// TransformRoi 用 affine_trans_point_2d 把參考 ROI 中心映射到當前影像座標系。
    /// 與 HalconTemplateMatcher.GetMatchContour 的 VectorAngleToRigid 慣例一致。
    ///
    /// 注意：必須用 affine_trans_point_2d 而非 affine_trans_pixel。後者有像素中心慣例
    /// 偏移，套用 vector_angle_to_rigid 矩陣時旋轉會產生 1 像素誤差（已用已知旋轉
    /// 案例驗證：affine_trans_pixel 使中心 (100,100) 旋轉後落在 (99,100)，
    /// affine_trans_point_2d 則精確映回 (100,100)）。
    /// </summary>
    public class HalconCoordinateMapper : ICoordinateMapper
    {
        public RigidTransform CreateFromMatch(
            double refRow, double refCol, double refAngleRad,
            double curRow, double curCol, double curAngleRad)
        {
            // vector_angle_to_rigid：把 (refRow, refCol, refAngle) 映射到
            // (curRow, curCol, curAngle) 的剛體變換（平移 + 旋轉）。
            HOperatorSet.VectorAngleToRigid(
                refRow, refCol, refAngleRad,
                curRow, curCol, curAngleRad,
                out HTuple homMat2D);

            return new RigidTransform
            {
                HomMat2D = HTupleToDoubleArray(homMat2D),
                RotationRad = curAngleRad - refAngleRad,
                TranslationRow = curRow - refRow,
                TranslationCol = curCol - refCol
            };
        }

        public TransformedRoi TransformRoi(
            double refRoiRow, double refRoiCol, double refRoiAngleRad,
            RigidTransform transform)
        {
            if (transform == null || !transform.IsValid)
            {
                throw new ArgumentException(
                    "RigidTransform 無效（HomMat2D 需為 6 個元素）", "transform");
            }

            HTuple homMat2D = new HTuple(transform.HomMat2D);

            // affine_trans_point_2d：套用 hom_mat2d 到單點，引數與輸出皆 (Row, Col) 順序。
            // 不可用 affine_trans_pixel（像素中心慣例偏移會在旋轉時產生 1px 誤差）。
            HOperatorSet.AffineTransPoint2d(homMat2D, refRoiRow, refRoiCol,
                out HTuple newRow, out HTuple newCol);

            return new TransformedRoi
            {
                Row = newRow.D,
                Col = newCol.D,
                // ROI 自身角度跟著旋轉量轉。
                AngleRad = refRoiAngleRad + transform.RotationRad
            };
        }

        private static double[] HTupleToDoubleArray(HTuple tuple)
        {
            int n = tuple == null ? 0 : tuple.Length;
            double[] arr = new double[n];
            for (int i = 0; i < n; i++)
            {
                arr[i] = tuple[i].D;
            }
            return arr;
        }
    }
}
