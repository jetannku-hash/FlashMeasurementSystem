using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Domain.Gdt
{
    /// <summary>
    /// 真直度帶寬（真值）：各邊點到擬合線的「有號垂距」的 max − min，即包住所有點的平行雙平面
    /// 公差帶寬度（px）。取代 v1 的 ResidualRms 近似——RMS 會低估離群點，peak-to-peak 才是
    /// GD&T 真直度的定義（zone width）。純幾何、無擬合，可全驗。
    /// </summary>
    public static class StraightnessBand
    {
        /// <param name="points">擬合所用的邊點。</param>
        /// <param name="row1">擬合線端點 1 (row)。</param>
        /// <param name="col1">擬合線端點 1 (col)。</param>
        /// <param name="row2">擬合線端點 2 (row)。</param>
        /// <param name="col2">擬合線端點 2 (col)。</param>
        /// <returns>帶寬 (px)；點數 &lt; 2 或線退化（兩端點重合）時回 0。</returns>
        public static double PeakToPeakPx(IList<EdgePoint> points, double row1, double col1, double row2, double col2)
        {
            if (points == null || points.Count < 2) return 0.0;

            double dr = row2 - row1;
            double dc = col2 - col1;
            double len = Math.Sqrt(dr * dr + dc * dc);
            if (len < 1e-12) return 0.0;   // 端點重合 → 無法定義垂直方向

            double min = double.MaxValue, max = double.MinValue;
            foreach (EdgePoint p in points)
            {
                // 點到直線的有號垂距（2D 外積 / 長度）
                double signed = ((p.Column - col1) * dr - (p.Row - row1) * dc) / len;
                if (signed < min) min = signed;
                if (signed > max) max = signed;
            }
            return max - min;
        }
    }
}
