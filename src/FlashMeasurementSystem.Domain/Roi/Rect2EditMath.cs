using System;

namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// rect2 ROI 互動編輯的純幾何（無 HALCON / 無 UI 相依）。
    /// 慣例：e1=(-sinφ,cosφ)=長軸(Length1)方向，e2=(cosφ,sinφ)=短軸(Length2)方向，
    /// 座標 (row, col)，row 向下；與 HALCON gen_rectangle2 / disp_rectangle2 的 +phi 慣例一致。
    /// </summary>
    public static class Rect2EditMath
    {
        /// <summary>把手可縮放的最小半長（避免縮成 0）。</summary>
        public const double MinHalfLen = 3.0;

        /// <summary>長軸 e1、短軸 e2 的單位向量（row, col）。</summary>
        public static void Axes(double phi, out double e1r, out double e1c, out double e2r, out double e2c)
        {
            e1r = -Math.Sin(phi); e1c = Math.Cos(phi);
            e2r = Math.Cos(phi); e2c = Math.Sin(phi);
        }

        /// <summary>旋轉把手圓鈕位置：中心沿 e1 方向、距離 (l1 + knobGap)。</summary>
        public static void RotateKnobPos(double cr, double cc, double phi, double l1,
            double knobGap, out double kr, out double kc)
        {
            Axes(phi, out double e1r, out double e1c, out double _, out double _);
            double d = l1 + knobGap;
            kr = cr + d * e1r;
            kc = cc + d * e1c;
        }

        /// <summary>把滑鼠點相對中心投影到本地軸：d1 沿 e1、d2 沿 e2。</summary>
        public static void Project(double pr, double pc, double cr, double cc, double phi,
            out double d1, out double d2)
        {
            Axes(phi, out double e1r, out double e1c, out double e2r, out double e2c);
            double vr = pr - cr, vc = pc - cc;
            d1 = vr * e1r + vc * e1c;
            d2 = vr * e2r + vc * e2c;
        }

        /// <summary>
        /// 命中判定（影像座標，tol/knobGap 皆為影像像素）。優先序：
        /// Rotate &gt; Corner &gt; Len1 &gt; Len2 &gt; Body &gt; None。
        /// </summary>
        public static Rect2Handle HitTest(double pr, double pc, double cr, double cc, double phi,
            double l1, double l2, double tol, double knobGap)
        {
            Axes(phi, out double e1r, out double e1c, out double e2r, out double e2c);

            RotateKnobPos(cr, cc, phi, l1, knobGap, out double kr, out double kc);
            if (Dist(pr, pc, kr, kc) <= tol) return Rect2Handle.Rotate;

            for (int s1 = -1; s1 <= 1; s1 += 2)
                for (int s2 = -1; s2 <= 1; s2 += 2)
                {
                    double rr = cr + s1 * l1 * e1r + s2 * l2 * e2r;
                    double rc = cc + s1 * l1 * e1c + s2 * l2 * e2c;
                    if (Dist(pr, pc, rr, rc) <= tol) return Rect2Handle.Corner;
                }

            for (int s = -1; s <= 1; s += 2)
            {
                double rr = cr + s * l1 * e1r;
                double rc = cc + s * l1 * e1c;
                if (Dist(pr, pc, rr, rc) <= tol) return Rect2Handle.Len1;
            }

            for (int s = -1; s <= 1; s += 2)
            {
                double rr = cr + s * l2 * e2r;
                double rc = cc + s * l2 * e2c;
                if (Dist(pr, pc, rr, rc) <= tol) return Rect2Handle.Len2;
            }

            Project(pr, pc, cr, cc, phi, out double d1, out double d2);
            if (Math.Abs(d1) <= l1 && Math.Abs(d2) <= l2) return Rect2Handle.Body;

            return Rect2Handle.None;
        }

        /// <summary>對稱繞中心縮放：依把手種類更新 l1/l2（取投影絕對值，夾 MinHalfLen）。</summary>
        public static void ApplyResize(Rect2Handle handle, double pr, double pc,
            double cr, double cc, double phi, ref double l1, ref double l2)
        {
            Project(pr, pc, cr, cc, phi, out double d1, out double d2);
            if (handle == Rect2Handle.Len1 || handle == Rect2Handle.Corner)
                l1 = Math.Max(MinHalfLen, Math.Abs(d1));
            if (handle == Rect2Handle.Len2 || handle == Rect2Handle.Corner)
                l2 = Math.Max(MinHalfLen, Math.Abs(d2));
        }

        /// <summary>由滑鼠點求新角度（弧度）：φ = atan2(-(Δrow), Δcol)。</summary>
        public static double Rotate(double pr, double pc, double cr, double cc)
        {
            return Math.Atan2(-(pr - cr), pc - cc);
        }

        private static double Dist(double r1, double c1, double r2, double c2)
        {
            double dr = r1 - r2, dc = c1 - c2;
            return Math.Sqrt(dr * dr + dc * dc);
        }
    }

    /// <summary>rect2 互動把手種類（命中判定與拖曳模式共用）。</summary>
    public enum Rect2Handle
    {
        None,
        Body,
        Len1,
        Len2,
        Corner,
        Rotate
    }
}
