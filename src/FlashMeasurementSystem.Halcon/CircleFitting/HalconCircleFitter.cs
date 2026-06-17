using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.CircleFitting;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.CircleFitting
{
    public class HalconCircleFitter : ICircleFitter
    {
        public CircleFittingResult FitCircle(IList<EdgePoint> edgePoints, CircleFittingParameters parameters)
        {
            CircleFittingResult result = new CircleFittingResult();
            CircleFittingParameters effective = parameters ?? CircleFittingParameters.Default();

            if (!CircleFittingParameters.IsSupportedAlgorithm(effective.Algorithm))
            {
                result.ErrorMessage = "不支援的圓擬合演算法: " + effective.Algorithm;
                return result;
            }

            // HALCON 17.12 fit_circle_contour_xld reference L175525-526 規定：
            // 「The minimum necessary number of contour points for fitting a circle is three.
            //   Therefore, it is required that the number of contour points is at least
            //   3 + 2 * ClippingEndPoints.」
            // 動態算出 effective 最小值：使用者 MinPoints 跟 HALCON 內建最低限取大。
            // 預設 ClippingEndPoints=0 時等於 3，但若 ClippingEndPoints>0 卻只檢查 3，
            // 會傳不足點數讓 HALCON 丟例外。
            int halconMinimum = 3 + 2 * Math.Max(0, effective.ClippingEndPoints);
            int requiredMinPoints = Math.Max(effective.MinPoints, halconMinimum);
            if (edgePoints == null || edgePoints.Count < requiredMinPoints)
            {
                int count = edgePoints == null ? 0 : edgePoints.Count;
                result.ErrorMessage = string.Format(
                    "邊緣點不足 (need >= {0}, got {1}; ClippingEndPoints={2})",
                    requiredMinPoints, count, effective.ClippingEndPoints);
                return result;
            }

            try
            {
                int n = edgePoints.Count;
                double[] rows = new double[n];
                double[] columns = new double[n];

                for (int i = 0; i < n; i++)
                {
                    rows[i] = edgePoints[i].Row;
                    columns[i] = edgePoints[i].Column;
                }

                using (HXLDCont contour = new HXLDCont())
                {
                    contour.GenContourPolygonXld(rows, columns);

                    HOperatorSet.FitCircleContourXld(
                        contour,
                        effective.Algorithm,
                        effective.MaxNumPoints,
                        effective.MaxClosureDist,
                        effective.ClippingEndPoints,
                        effective.Iterations,
                        effective.ClippingFactor,
                        out HTuple row,
                        out HTuple column,
                        out HTuple radius,
                        out HTuple startPhi,
                        out HTuple endPhi,
                        out HTuple pointOrder);

                    result.CenterRow = row.D;
                    result.CenterColumn = column.D;
                    result.RadiusPx = radius.D;
                    result.DiameterPx = result.RadiusPx * 2.0;
                    result.StartPhi = startPhi.D;
                    result.EndPhi = endPhi.D;
                    result.PointOrder = pointOrder.S;
                    result.UsedPoints = n;

                    CalculateResiduals(edgePoints, result);
                    result.Success = true;
                }
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "圓擬合失敗: " + ex.Message;
            }

            return result;
        }

        // 注意：ResidualRms 跟 Roundness 都是對**所有輸入 edge points**算的，包括
        // fit_circle_contour_xld 內部排除掉的 outlier（geotukey/atukey 的核心特色）。
        // 所以這兩個值反映「fit 對全部 input 的擬合度」，不是「HALCON 演算法內部 inlier 誤差」。
        // 對 quality reporting 是合理指標，但 Roundness（max-min 半徑）對單一離群點特別敏感
        // ——一個雜訊點就會撐大 Roundness。判讀時搭配 RMS 一起看。
        private static void CalculateResiduals(IList<EdgePoint> edgePoints, CircleFittingResult circle)
        {
            double sumSq = 0.0;
            double minPointRadius = double.MaxValue;
            double maxPointRadius = double.MinValue;

            for (int i = 0; i < edgePoints.Count; i++)
            {
                double deltaRow = edgePoints[i].Row - circle.CenterRow;
                double deltaColumn = edgePoints[i].Column - circle.CenterColumn;
                double pointRadius = Math.Sqrt(deltaRow * deltaRow + deltaColumn * deltaColumn);
                double radialError = Math.Abs(pointRadius - circle.RadiusPx);

                sumSq += radialError * radialError;
                if (pointRadius < minPointRadius) minPointRadius = pointRadius;
                if (pointRadius > maxPointRadius) maxPointRadius = pointRadius;
            }

            circle.ResidualRms = Math.Sqrt(sumSq / edgePoints.Count);
            circle.Roundness = maxPointRadius - minPointRadius;
        }
    }
}
