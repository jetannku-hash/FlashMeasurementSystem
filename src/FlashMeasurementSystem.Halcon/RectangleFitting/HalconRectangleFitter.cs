using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.RectangleFitting;
using FlashMeasurementSystem.Domain.RectangleFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.RectangleFitting
{
    public class HalconRectangleFitter : IRectangleFitter
    {
        public RectangleFittingResult FitRectangle(IList<EdgePoint> edgePoints, RectangleFittingParameters parameters)
        {
            RectangleFittingResult result = new RectangleFittingResult();
            RectangleFittingParameters effective = parameters ?? RectangleFittingParameters.Default();

            if (!RectangleFittingParameters.IsSupportedAlgorithm(effective.Algorithm))
            {
                result.ErrorMessage = "不支援的矩形擬合演算法: " + effective.Algorithm;
                return result;
            }

            // fit_rectangle2_contour_xld 最少需 8 點（reference L175983）。
            // 注意：此 operator 沒有獨立的 ClippingEndPoints 最低限公式（與 ellipse/circle 不同），
            // 但 ClippingEndPoints 仍會排除頭尾點，保守採 MinPoints 與 8 取大。
            int requiredMinPoints = Math.Max(effective.MinPoints, 8);
            if (edgePoints == null || edgePoints.Count < requiredMinPoints)
            {
                int count = edgePoints == null ? 0 : edgePoints.Count;
                result.ErrorMessage = string.Format(
                    "邊緣點不足 (need >= {0}, got {1})",
                    requiredMinPoints, count);
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

                    // 參數順序依 reference L175931：Algorithm, MaxNumPoints, MaxClosureDist,
                    // ClippingEndPoints, Iterations, ClippingFactor。
                    // 注意：比 fit_ellipse_contour_xld 少 VossTabSize，演算法也只有三種。
                    HOperatorSet.FitRectangle2ContourXld(
                        contour,
                        effective.Algorithm,
                        effective.MaxNumPoints,
                        effective.MaxClosureDist,
                        effective.ClippingEndPoints,
                        effective.Iterations,
                        effective.ClippingFactor,
                        out HTuple row,
                        out HTuple column,
                        out HTuple phi,
                        out HTuple length1,
                        out HTuple length2,
                        out HTuple pointOrder);

                    result.CenterRow = row.D;
                    result.CenterColumn = column.D;
                    result.Phi = phi.D;
                    result.Length1Px = length1.D;
                    result.Length2Px = length2.D;
                    result.PointOrder = pointOrder.S;
                    result.UsedPoints = n;

                    CalculateResiduals(edgePoints, result);
                    result.Success = true;
                }
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "矩形擬合失敗: " + ex.Message;
            }

            return result;
        }

        // 點到矩形最短距離：把點旋轉到矩形主軸座標系，取 (dx, dy) 各自的
        // 超出半邊長部分為沿軸距，兩軸的超出量歐氏合成即為到矩形邊的距離。
        private static void CalculateResiduals(IList<EdgePoint> edgePoints, RectangleFittingResult rect)
        {
            double l1 = rect.Length1Px;
            double l2 = rect.Length2Px;
            if (l1 <= 0.0 || l2 <= 0.0)
            {
                rect.ResidualRms = 0.0;
                return;
            }

            double cosP = Math.Cos(rect.Phi);
            double sinP = Math.Sin(rect.Phi);
            double sumSq = 0.0;

            for (int i = 0; i < edgePoints.Count; i++)
            {
                double dr = edgePoints[i].Row - rect.CenterRow;
                double dc = edgePoints[i].Column - rect.CenterColumn;

                // 旋轉到矩形主軸座標系（主軸沿 Length1 方向）。
                double xp = dc * cosP + dr * sinP;
                double yp = -dc * sinP + dr * cosP;

                double dx = Math.Max(Math.Abs(xp) - l1, 0.0);
                double dy = Math.Max(Math.Abs(yp) - l2, 0.0);
                sumSq += dx * dx + dy * dy;
            }

            rect.ResidualRms = Math.Sqrt(sumSq / edgePoints.Count);
        }
    }
}
