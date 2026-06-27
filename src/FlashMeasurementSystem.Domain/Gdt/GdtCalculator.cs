using System;

namespace FlashMeasurementSystem.Domain.Gdt
{
    /// <summary>
    /// 形位公差偏差計算（純數學，無 HALCON / 無 UI）。座標 (row, col)，row 向下。
    /// 回傳一律為**像素**；呼叫端再乘 pixelSizeUm/1000 轉 mm。
    /// 真圓度/真直度的偏差來自擬合器純量（max-min / ResidualRms），不在此計算。
    /// </summary>
    public static class GdtCalculator
    {
        /// <summary>方向向量長度門檻，低於此視為退化線段。</summary>
        public const double DegenerateEpsilon = 1e-9;

        /// <summary>線段端點距離（像素）。</summary>
        public static double LineLengthPx(double r1, double c1, double r2, double c2)
        {
            double dr = r2 - r1, dc = c2 - c1;
            return Math.Sqrt(dr * dr + dc * dc);
        }

        /// <summary>
        /// 兩條線方向的銳角夾角（度，[0,90]）。以方向向量點積絕對值消除無向性。
        /// 任一線退化（長度&lt;eps）回 0。
        /// </summary>
        public static double AcuteAngleBetweenLinesDeg(
            double a_r1, double a_c1, double a_r2, double a_c2,
            double b_r1, double b_c1, double b_r2, double b_c2)
        {
            double dAr = a_r2 - a_r1, dAc = a_c2 - a_c1;
            double dBr = b_r2 - b_r1, dBc = b_c2 - b_c1;
            double lenA = Math.Sqrt(dAr * dAr + dAc * dAc);
            double lenB = Math.Sqrt(dBr * dBr + dBc * dBc);
            if (lenA < DegenerateEpsilon || lenB < DegenerateEpsilon) return 0.0;

            double cos = Math.Abs(dAr * dBr + dAc * dBc) / (lenA * lenB);
            if (cos > 1.0) cos = 1.0;   // 數值保護，避免 Acos 域外
            if (cos < 0.0) cos = 0.0;
            return Math.Acos(cos) * 180.0 / Math.PI;
        }

        /// <summary>
        /// 平行度公差帶寬（像素）= 量測線長 × sin(兩線銳角夾角)。理想平行→0。
        /// 第一組參數為量測線（提供帶寬基準長度），第二組為基準線。
        /// </summary>
        public static double ParallelismZonePx(
            double m_r1, double m_c1, double m_r2, double m_c2,
            double d_r1, double d_c1, double d_r2, double d_c2)
        {
            double thetaDeg = AcuteAngleBetweenLinesDeg(m_r1, m_c1, m_r2, m_c2, d_r1, d_c1, d_r2, d_c2);
            double lenM = LineLengthPx(m_r1, m_c1, m_r2, m_c2);
            return lenM * Math.Sin(thetaDeg * Math.PI / 180.0);
        }

        /// <summary>
        /// 垂直度公差帶寬（像素）= 量測線長 × sin(90° − 兩線銳角夾角)。理想垂直(θ=90°)→0。
        /// 第一組參數為量測線，第二組為基準線。
        /// </summary>
        public static double PerpendicularityZonePx(
            double m_r1, double m_c1, double m_r2, double m_c2,
            double d_r1, double d_c1, double d_r2, double d_c2)
        {
            double thetaDeg = AcuteAngleBetweenLinesDeg(m_r1, m_c1, m_r2, m_c2, d_r1, d_c1, d_r2, d_c2);
            double lenM = LineLengthPx(m_r1, m_c1, m_r2, m_c2);
            return lenM * Math.Sin((90.0 - thetaDeg) * Math.PI / 180.0);
        }

        /// <summary>
        /// 同心度公差值（像素，直徑帶語意）= 2 × 兩圓心距離。理想同心→0。
        /// </summary>
        public static double ConcentricityDiametralPx(
            double centerRow1, double centerCol1, double centerRow2, double centerCol2)
        {
            double dr = centerRow1 - centerRow2, dc = centerCol1 - centerCol2;
            return 2.0 * Math.Sqrt(dr * dr + dc * dc);
        }
    }
}
