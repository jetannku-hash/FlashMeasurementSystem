using System;

namespace FlashMeasurementSystem.Domain.Geometry
{
    /// <summary>
    /// A5 幾何構造純數學（無 HALCON / 無 UI）。座標 (row, col)，row 向下。
    /// </summary>
    public static class GeometryConstruction
    {
        /// <summary>方向外積絕對值門檻，低於此視為平行。</summary>
        public const double ParallelEpsilon = 1e-9;

        /// <summary>
        /// 兩條無限直線交點。以方向向量外積為分母求解；平行（|cross| &lt; eps）回 false。
        /// </summary>
        public static bool TryLineIntersection(
            double a_r1, double a_c1, double a_r2, double a_c2,
            double b_r1, double b_c1, double b_r2, double b_c2,
            out double row, out double col)
        {
            double dAr = a_r2 - a_r1, dAc = a_c2 - a_c1;
            double dBr = b_r2 - b_r1, dBc = b_c2 - b_c1;
            double denom = dAr * dBc - dAc * dBr;   // 方向外積
            if (Math.Abs(denom) < ParallelEpsilon)
            {
                row = 0; col = 0;
                return false;
            }
            // A1 + t*dA = B1 + s*dB，解 t
            double t = ((b_r1 - a_r1) * dBc - (b_c1 - a_c1) * dBr) / denom;
            row = a_r1 + t * dAr;
            col = a_c1 + t * dAc;
            return true;
        }

        /// <summary>
        /// 點 (pRow,pCol) 垂直投影到通過 (r1,c1)-(r2,c2) 的無限直線，回傳垂足。
        /// 線段退化（長度 0）時回傳起點，避免除以 0。
        /// </summary>
        public static void ProjectPointOntoLine(
            double pRow, double pCol,
            double r1, double c1, double r2, double c2,
            out double footRow, out double footCol)
        {
            double dRow = r2 - r1;
            double dCol = c2 - c1;
            double lenSq = dRow * dRow + dCol * dCol;
            if (lenSq < ParallelEpsilon)
            {
                footRow = r1;
                footCol = c1;
                return;
            }
            double t = ((pRow - r1) * dRow + (pCol - c1) * dCol) / lenSq;
            footRow = r1 + t * dRow;
            footCol = c1 + t * dCol;
        }

        /// <summary>
        /// 對稱中線 / 角平分線（等距軌跡），回傳一條線段端點。
        /// 平行：置中線（與兩線平行、置於正中）。相交：角平分線，通過交點，
        /// 方向 = normalize(dirA') + normalize(dirB')（dirB' 依 dot 正負消除無向性歧义）。
        /// 端點長 = 兩輸入線段平均半長。
        /// </summary>
        public static void Midline(
            double a_r1, double a_c1, double a_r2, double a_c2,
            double b_r1, double b_c1, double b_r2, double b_c2,
            out double row1, out double col1, out double row2, out double col2)
        {
            double dAr = a_r2 - a_r1, dAc = a_c2 - a_c1;
            double dBr = b_r2 - b_r1, dBc = b_c2 - b_c1;
            double lenA = Math.Sqrt(dAr * dAr + dAc * dAc);
            double lenB = Math.Sqrt(dBr * dBr + dBc * dBc);
            double half = (lenA + lenB) / 4.0;  // 平均全長的一半 = 平均半長
            if (half < 1.0) half = 1.0;

            // 單位方向；退化線段以 (0,1) 代替避免除 0。
            double uAr = lenA > ParallelEpsilon ? dAr / lenA : 0.0;
            double uAc = lenA > ParallelEpsilon ? dAc / lenA : 1.0;
            double uBr = lenB > ParallelEpsilon ? dBr / lenB : 0.0;
            double uBc = lenB > ParallelEpsilon ? dBc / lenB : 1.0;

            // 消除線無向性：讓 B 方向與 A 同半邊。
            if (uAr * uBr + uAc * uBc < 0.0) { uBr = -uBr; uBc = -uBc; }

            double cross = dAr * dBc - dAc * dBr;
            double cr, cc;     // 中線通過點
            double dirR, dirC; // 中線方向

            if (Math.Abs(cross) < ParallelEpsilon)
            {
                // 平行：方向取 A，通過點取「A 中點與其在 B 上垂足」的中點。
                dirR = uAr; dirC = uAc;
                double aMidR = (a_r1 + a_r2) / 2.0, aMidC = (a_c1 + a_c2) / 2.0;
                ProjectPointOntoLine(aMidR, aMidC, b_r1, b_c1, b_r2, b_c2, out double footR, out double footC);
                cr = (aMidR + footR) / 2.0;
                cc = (aMidC + footC) / 2.0;
            }
            else
            {
                // 相交：方向取單位方向和（角平分線），通過點取交點。
                dirR = uAr + uBr; dirC = uAc + uBc;
                double dl = Math.Sqrt(dirR * dirR + dirC * dirC);
                if (dl < ParallelEpsilon) { dirR = uAr; dirC = uAc; dl = 1.0; }
                dirR /= dl; dirC /= dl;
                TryLineIntersection(a_r1, a_c1, a_r2, a_c2, b_r1, b_c1, b_r2, b_c2, out cr, out cc);
            }

            row1 = cr - half * dirR; col1 = cc - half * dirC;
            row2 = cr + half * dirR; col2 = cc + half * dirC;
        }
    }
}
