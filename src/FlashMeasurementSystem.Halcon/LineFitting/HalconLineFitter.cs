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

                    double deltaRow = result.Row2 - result.Row1;
                    double deltaCol = result.Column2 - result.Column1;
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
