using System;
using FlashMeasurementSystem.Application.AngleMeasurement;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.AngleMeasurement
{
    public class HalconAngleMeasurer : IAngleMeasurer
    {
        // 合成參考向量長度（像素）。只要非零即可定義方向；與線1第一點共用起點，
        // 確保 angle_ll 的旋轉中心（兩線交點）有定義。
        private const double ReferenceLength = 100.0;

        public AngleMeasurementResult MeasureAngle(
            double line1Row1, double line1Col1, double line1Row2, double line1Col2,
            double line2Row1, double line2Col1, double line2Row2, double line2Col2,
            AngleMeasurementParameters parameters)
        {
            AngleMeasurementResult result = new AngleMeasurementResult();
            AngleMeasurementParameters p = parameters ?? AngleMeasurementParameters.Default();

            if (!AngleMeasurementParameters.IsSupportedMode(p.Mode))
            {
                result.ErrorMessage = "不支援的角度量測模式: " + p.Mode;
                return result;
            }

            if (Separation(line1Row1, line1Col1, line1Row2, line1Col2) < p.MinPointSeparation)
            {
                result.ErrorMessage = string.Format(
                    "線 1 兩端點過近，方向不可靠 (需 >= {0} px)", p.MinPointSeparation);
                return result;
            }

            // 依模式決定線 2（水平/垂直模式合成參考向量，忽略傳入的 line2*）。
            if (p.Mode == "line_to_horizontal")
            {
                line2Row1 = line1Row1; line2Col1 = line1Col1;
                line2Row2 = line1Row1; line2Col2 = line1Col1 + ReferenceLength; // +Column = 水平
            }
            else if (p.Mode == "line_to_vertical")
            {
                line2Row1 = line1Row1; line2Col1 = line1Col1;
                line2Row2 = line1Row1 + ReferenceLength; line2Col2 = line1Col1; // +Row = 垂直
            }
            else
            {
                if (Separation(line2Row1, line2Col1, line2Row2, line2Col2) < p.MinPointSeparation)
                {
                    result.ErrorMessage = string.Format(
                        "線 2 兩端點過近，方向不可靠 (需 >= {0} px)", p.MinPointSeparation);
                    return result;
                }
            }

            try
            {
                // angle_ll：旋轉向量A逆時針到向量B，回傳有號弧度 -π..π（reference L155253）。
                HOperatorSet.AngleLl(
                    line1Row1, line1Col1, line1Row2, line1Col2,
                    line2Row1, line2Col1, line2Row2, line2Col2,
                    out HTuple angle);

                double raw = angle.D;
                result.RawAngleDeg = raw * 180.0 / Math.PI;

                double directed = Math.Abs(raw);             // 0..π，方向向量夾角
                result.AngleRad = directed;
                result.AngleDeg = directed * 180.0 / Math.PI;

                result.AcuteAngleDeg = result.AngleDeg > 90.0
                    ? 180.0 - result.AngleDeg
                    : result.AngleDeg;

                // angle_lx：線對水平軸夾角（reference L155322）。
                HOperatorSet.AngleLx(line1Row1, line1Col1, line1Row2, line1Col2, out HTuple ref1);
                result.RefAngle1Deg = ref1.D * 180.0 / Math.PI;
                HOperatorSet.AngleLx(line2Row1, line2Col1, line2Row2, line2Col2, out HTuple ref2);
                result.RefAngle2Deg = ref2.D * 180.0 / Math.PI;

                result.IsNearParallel = result.AcuteAngleDeg < p.NearParallelWarningDeg;
                result.Success = true;
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "角度量測失敗: " + ex.Message;
            }

            return result;
        }

        private static double Separation(double r1, double c1, double r2, double c2)
        {
            double dr = r2 - r1;
            double dc = c2 - c1;
            return Math.Sqrt(dr * dr + dc * dc);
        }
    }
}
