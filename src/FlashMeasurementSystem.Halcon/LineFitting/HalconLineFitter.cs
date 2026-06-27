using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.LineFitting;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.LineFitting;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.LineFitting
{
    public class HalconLineFitter : ILineFitter
    {
        public LineFittingResult FitLine(IList<EdgePoint> edgePoints, LineFittingParameters parameters)
        {
            LineFittingResult result = new LineFittingResult();
            LineFittingParameters effective = parameters ?? LineFittingParameters.Default();

            if (!LineFittingParameters.IsSupportedAlgorithm(effective.Algorithm))
            {
                result.ErrorMessage = "不支援的直線擬合演算法: " + effective.Algorithm;
                return result;
            }

            int halconMinimum = 2 + 2 * Math.Max(0, effective.ClippingEndPoints);
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

                    HOperatorSet.FitLineContourXld(
                        contour,
                        effective.Algorithm,
                        effective.MaxNumPoints,
                        effective.ClippingEndPoints,
                        effective.Iterations,
                        effective.ClippingFactor,
                        out HTuple rowBegin,
                        out HTuple colBegin,
                        out HTuple rowEnd,
                        out HTuple colEnd,
                        out HTuple nr,
                        out HTuple nc,
                        out HTuple distance);

                    result.Row1 = rowBegin.D;
                    result.Column1 = colBegin.D;
                    result.Row2 = rowEnd.D;
                    result.Column2 = colEnd.D;
                    result.UsedPoints = n;

                    // fit_line_contour_xld 對「封閉/雙邊輪廓」（例：ROI 框住整條厚特徵時 edges_sub_pix
                    // 回的閉環外輪廓）會回退化線（起訖點重合、長度≈0）。此時改用點雲主成分(PCA)方向
                    // 求線方向與端點。僅在退化時觸發，正常單邊擬合不受影響。
                    double fitLenSq = (result.Row2 - result.Row1) * (result.Row2 - result.Row1)
                                    + (result.Column2 - result.Column1) * (result.Column2 - result.Column1);
                    if (fitLenSq < 1.0)
                    {
                        FitLineByPca(rows, columns,
                            out double pr1, out double pc1, out double pr2, out double pc2);
                        result.Row1 = pr1; result.Column1 = pc1;
                        result.Row2 = pr2; result.Column2 = pc2;
                    }

                    double deltaRow = result.Row2 - result.Row1;
                    double deltaCol = result.Column2 - result.Column1;
                    // 慣例：影像座標系（Row 向下），故 AngleDeg 為「相對水平、順時針為正」。
                    // 這與 HALCON angle_lx（數學慣例、逆時針為正）符號相反——勿與 HalconAngleMeasurer
                    // 的輸出直接相加比較。本值僅用於 line 角度公差判定（量測值 vs nominal 同屬此慣例、
                    // 由 AngleNormalizer 以 180° 週期比對），自洽無誤。
                    result.AngleDeg = Math.Atan2(deltaRow, deltaCol) * 180.0 / Math.PI;
                    result.Length = Math.Sqrt(deltaRow * deltaRow + deltaCol * deltaCol);
                    result.ResidualRms = CalculateResidualRms(edgePoints, result);
                    result.Success = true;
                }
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "直線擬合失敗: " + ex.Message;
            }

            return result;
        }

        // 點雲主成分(PCA)求線：取最大特徵值方向為線方向，投影 min/max 為端點。
        // 對封閉/雙邊輪廓也穩健（不像 fit_line_contour_xld 會退化）。
        private static void FitLineByPca(double[] rows, double[] cols,
            out double r1, out double c1, out double r2, out double c2)
        {
            int n = rows.Length;
            double mr = 0.0, mc = 0.0;
            for (int i = 0; i < n; i++) { mr += rows[i]; mc += cols[i]; }
            mr /= n; mc /= n;

            double srr = 0.0, scc = 0.0, src = 0.0;
            for (int i = 0; i < n; i++)
            {
                double dr = rows[i] - mr, dc = cols[i] - mc;
                srr += dr * dr; scc += dc * dc; src += dr * dc;
            }

            // 2x2 對稱協方差 [[srr,src],[src,scc]] 的最大特徵值與其特徵向量 (src, λ1-srr)。
            double half = (srr - scc) / 2.0;
            double lambda1 = (srr + scc) / 2.0 + Math.Sqrt(half * half + src * src);
            double dirRow, dirCol;
            if (Math.Abs(src) > 1e-9)
            {
                dirRow = src;
                dirCol = lambda1 - srr;
            }
            else
            {
                // src≈0：主軸沿 row 或 col。
                if (srr >= scc) { dirRow = 1.0; dirCol = 0.0; }
                else { dirRow = 0.0; dirCol = 1.0; }
            }
            double dlen = Math.Sqrt(dirRow * dirRow + dirCol * dirCol);
            if (dlen < 1e-12) { dirRow = 0.0; dirCol = 1.0; dlen = 1.0; }
            dirRow /= dlen; dirCol /= dlen;

            double tMin = double.MaxValue, tMax = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                double t = (rows[i] - mr) * dirRow + (cols[i] - mc) * dirCol;
                if (t < tMin) tMin = t;
                if (t > tMax) tMax = t;
            }
            r1 = mr + tMin * dirRow; c1 = mc + tMin * dirCol;
            r2 = mr + tMax * dirRow; c2 = mc + tMax * dirCol;
        }

        private static double CalculateResidualRms(IList<EdgePoint> edgePoints, LineFittingResult line)
        {
            double deltaRow = line.Row2 - line.Row1;
            double deltaCol = line.Column2 - line.Column1;
            double denominator = Math.Sqrt(deltaRow * deltaRow + deltaCol * deltaCol);

            if (denominator <= 0.0)
            {
                return 0.0;
            }

            double sumSq = 0.0;

            for (int i = 0; i < edgePoints.Count; i++)
            {
                double pointRow = edgePoints[i].Row;
                double pointCol = edgePoints[i].Column;
                double distance = Math.Abs(deltaRow * (pointCol - line.Column1) - deltaCol * (pointRow - line.Row1)) / denominator;
                sumSq += distance * distance;
            }

            return Math.Sqrt(sumSq / edgePoints.Count);
        }
    }
}
