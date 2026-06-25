using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.EllipseFitting;
using FlashMeasurementSystem.Domain.EllipseFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.EllipseFitting
{
    public class HalconEllipseFitter : IEllipseFitter
    {
        public EllipseFittingResult FitEllipse(IList<EdgePoint> edgePoints, EllipseFittingParameters parameters)
        {
            EllipseFittingResult result = new EllipseFittingResult();
            EllipseFittingParameters effective = parameters ?? EllipseFittingParameters.Default();

            if (!EllipseFittingParameters.IsSupportedAlgorithm(effective.Algorithm))
            {
                result.ErrorMessage = "不支援的橢圓擬合演算法: " + effective.Algorithm;
                return result;
            }

            // HALCON 17.12 fit_ellipse_contour_xld reference L175690 規定：
            // 「The minimum necessary number of contour points for fitting an ellipse is five.
            //   Therefore, it is required that the number of contour points is at least
            //   5 + 2 * ClippingEndPoints.」
            // 與 circle fitter 同樣動態取大：使用者 MinPoints 與 HALCON 內建最低限取大，
            // 避免 ClippingEndPoints>0 卻只檢查 5 而傳不足點數讓 HALCON 丟例外。
            int halconMinimum = 5 + 2 * Math.Max(0, effective.ClippingEndPoints);
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

                    // 參數順序依 reference L175603：Algorithm, MaxNumPoints, MaxClosureDist,
                    // ClippingEndPoints, VossTabSize, Iterations, ClippingFactor。
                    // 注意比 fit_circle_contour_xld 多一個 VossTabSize（在 ClippingEndPoints
                    // 與 Iterations 之間）。
                    HOperatorSet.FitEllipseContourXld(
                        contour,
                        effective.Algorithm,
                        effective.MaxNumPoints,
                        effective.MaxClosureDist,
                        effective.ClippingEndPoints,
                        effective.VossTabSize,
                        effective.Iterations,
                        effective.ClippingFactor,
                        out HTuple row,
                        out HTuple column,
                        out HTuple phi,
                        out HTuple radius1,
                        out HTuple radius2,
                        out HTuple startPhi,
                        out HTuple endPhi,
                        out HTuple pointOrder);

                    result.CenterRow = row.D;
                    result.CenterColumn = column.D;
                    result.Phi = phi.D;
                    result.Radius1Px = radius1.D;
                    result.Radius2Px = radius2.D;
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
                result.ErrorMessage = "橢圓擬合失敗: " + ex.Message;
            }

            return result;
        }

        // 殘差 RMS 對**所有輸入 edge points**計算（與 circle fitter 一致，含演算法內部排除的
        // outlier），反映 fit 對全部 input 的擬合度，作為品質指標。
        //
        // 點到橢圓的精確幾何距離需解四次方程式，成本高；此處用一階近似（Sampson distance）：
        //   令隱式函數 F(x,y) = (x'/r1)^2 + (y'/r2)^2 - 1（x',y' 為點在橢圓主軸座標系的座標），
        //   近似距離 d ≈ F / |∇F|，∇F = (2x'/r1^2, 2y'/r2^2)。
        // 對量測品質報表足夠；若日後需精確距離，改用迭代法（TODO）。
        private static void CalculateResiduals(IList<EdgePoint> edgePoints, EllipseFittingResult ellipse)
        {
            double r1 = ellipse.Radius1Px;
            double r2 = ellipse.Radius2Px;

            // 退化保護：fit_ellipse_contour_xld 在無法擬合時會退化成直線（Radius2=0，
            // reference L175670）。半軸為 0 時無法算 Sampson 距離，殘差留 0 並照常回傳。
            if (r1 <= 0.0 || r2 <= 0.0)
            {
                ellipse.ResidualRms = 0.0;
                return;
            }

            double cosP = Math.Cos(ellipse.Phi);
            double sinP = Math.Sin(ellipse.Phi);
            double sumSq = 0.0;

            for (int i = 0; i < edgePoints.Count; i++)
            {
                double dr = edgePoints[i].Row - ellipse.CenterRow;
                double dc = edgePoints[i].Column - ellipse.CenterColumn;

                // 旋轉到橢圓主軸座標系（Phi 為主軸與水平軸夾角，逆時針）。
                double xp = dc * cosP + dr * sinP;
                double yp = -dc * sinP + dr * cosP;

                double f = (xp * xp) / (r1 * r1) + (yp * yp) / (r2 * r2) - 1.0;
                double gx = 2.0 * xp / (r1 * r1);
                double gy = 2.0 * yp / (r2 * r2);
                double gradNorm = Math.Sqrt(gx * gx + gy * gy);

                double dist = gradNorm > 1e-12 ? f / gradNorm : 0.0;
                sumSq += dist * dist;
            }

            ellipse.ResidualRms = Math.Sqrt(sumSq / edgePoints.Count);
        }
    }
}
