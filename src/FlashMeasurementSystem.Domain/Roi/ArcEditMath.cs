using System;

namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// 弧形量測 ROI 互動編輯的純幾何（無 HALCON / 無 UI 相依）。
    /// 角度慣例與 gen_circle_contour_xld / gen_measure_arc 一致：
    /// row = cr - R*sin(phi)，col = cc + R*cos(phi)；phi = atan2(-(pr-cr), pc-cc)；
    /// AngleExtent > 0 為逆時針。座標 (row, col)，row 向下。
    /// </summary>
    public static class ArcEditMath
    {
        /// <summary>半徑可縮放的最小值（對應 ArcMeasureRoi.IsDefined: Radius > 1）。</summary>
        public const double MinRadius = 2.0;

        /// <summary>環寬一半可縮放的最小值（對應 IsDefined: AnnulusRadius > 0.5）。</summary>
        public const double MinAnnulus = 1.0;

        private const double TwoPi = 2.0 * Math.PI;

        /// <summary>角度 phi（弧度）在半徑 radius 上的影像座標點。</summary>
        public static void PointOnArc(double cr, double cc, double radius, double phi,
            out double row, out double col)
        {
            row = cr - radius * Math.Sin(phi);
            col = cc + radius * Math.Cos(phi);
        }

        /// <summary>由影像點求方位角（弧度，(-pi, pi]）。</summary>
        public static double AngleOf(double pr, double pc, double cr, double cc)
        {
            return Math.Atan2(-(pr - cr), pc - cc);
        }

        /// <summary>由影像點求到弧心的半徑。</summary>
        public static double RadiusOf(double pr, double pc, double cr, double cc)
        {
            double dr = pr - cr, dc = pc - cc;
            return Math.Sqrt(dr * dr + dc * dc);
        }

        /// <summary>
        /// 命中判定（影像座標，tol 為影像像素）。先取最近的點把手
        /// (AngleStart/AngleEnd/Annulus/Radius)；否則中心或環帶內 -> Center；其餘 None。
        /// </summary>
        public static ArcHandle HitTest(double pr, double pc, double cr, double cc,
            double radius, double a0, double extent, double annulus, double tol)
        {
            double aMid = a0 + extent / 2.0;

            ArcHandle best = ArcHandle.None;
            double bestDist = tol;

            ConsiderPoint(ref best, ref bestDist, pr, pc, cr, cc, radius, a0, ArcHandle.AngleStart);
            ConsiderPoint(ref best, ref bestDist, pr, pc, cr, cc, radius, a0 + extent, ArcHandle.AngleEnd);
            ConsiderPoint(ref best, ref bestDist, pr, pc, cr, cc, radius + annulus, aMid, ArcHandle.Annulus);
            ConsiderPoint(ref best, ref bestDist, pr, pc, cr, cc, radius, aMid, ArcHandle.Radius);
            if (best != ArcHandle.None) return best;

            if (RadiusOf(pr, pc, cr, cc) <= tol) return ArcHandle.Center;

            double rad = RadiusOf(pr, pc, cr, cc);
            double rIn = Math.Max(0.0, radius - annulus);
            double rOut = radius + annulus;
            if (rad >= rIn - tol && rad <= rOut + tol &&
                InSweep(AngleOf(pr, pc, cr, cc), a0, extent))
                return ArcHandle.Center;

            return ArcHandle.None;
        }

        private static void ConsiderPoint(ref ArcHandle best, ref double bestDist,
            double pr, double pc, double cr, double cc, double r, double phi, ArcHandle handle)
        {
            PointOnArc(cr, cc, r, phi, out double hr, out double hc);
            double d = RadiusOf(pr, pc, hr, hc);
            if (d <= bestDist)
            {
                bestDist = d;
                best = handle;
            }
        }

        /// <summary>角度 ang 是否落在從 a0 起、掃 extent（有號）的弧上。</summary>
        public static bool InSweep(double ang, double a0, double extent)
        {
            if (extent >= 0)
            {
                double d = Norm0To2Pi(ang - a0);
                return d <= extent + 1e-9;
            }
            else
            {
                double dn = Norm0To2Pi(a0 - ang);
                return dn <= -extent + 1e-9;
            }
        }

        /// <summary>
        /// 依把手拖曳更新弧形參數。Center 不在此處理（由 helper 以位移平移中心）。
        /// Radius/Annulus 取與中心的距離；AngleStart 固定另一端、AngleEnd 改變張角。
        /// </summary>
        public static void ApplyDrag(ArcHandle handle, double pr, double pc,
            double cr, double cc, ref double radius, ref double a0, ref double extent, ref double annulus)
        {
            switch (handle)
            {
                case ArcHandle.Radius:
                    radius = Math.Max(MinRadius, RadiusOf(pr, pc, cr, cc));
                    break;
                case ArcHandle.Annulus:
                    annulus = Math.Max(MinAnnulus, RadiusOf(pr, pc, cr, cc) - radius);
                    break;
                case ArcHandle.AngleStart:
                    double end = a0 + extent;
                    double na0 = AngleOf(pr, pc, cr, cc);
                    extent = WrapExtent(end - na0, extent);
                    a0 = na0;
                    break;
                case ArcHandle.AngleEnd:
                    double na1 = AngleOf(pr, pc, cr, cc);
                    extent = WrapExtent(na1 - a0, extent);
                    break;
            }
        }

        // 把 raw 角度差調整成與 prevExtent 同向、量值 (0, 2pi] 的代表值，避免拖曳時 extent 變號跳動。
        private static double WrapExtent(double raw, double prevExtent)
        {
            double r = Norm0To2Pi(raw);
            if (prevExtent >= 0)
            {
                if (r < 1e-9) r = TwoPi;
                return r;
            }
            else
            {
                double rn = r - TwoPi;
                if (rn > -1e-9) rn = -TwoPi;
                return rn;
            }
        }

        private static double Norm0To2Pi(double a)
        {
            return a - TwoPi * Math.Floor(a / TwoPi);
        }
    }

    /// <summary>弧形互動把手種類（命中判定與拖曳模式共用）。</summary>
    public enum ArcHandle
    {
        None,
        Center,
        Radius,
        Annulus,
        AngleStart,
        AngleEnd
    }
}
