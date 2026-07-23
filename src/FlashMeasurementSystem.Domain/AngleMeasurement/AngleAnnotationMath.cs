using System;

namespace FlashMeasurementSystem.Domain.AngleMeasurement
{
    /// <summary>
    /// 角度「標註幾何」的純計算（工業製圖慣例）：
    /// 頂點 = 兩線（無限延伸）的真交點；弧掃在兩線方向之間，|掃角| = 銳角；
    /// 值標在角平分線方向。半徑依線段可用長度自適應。
    /// 方位角採全專案影像慣例：theta = atan2(dRow, dCol)，即 row = cr + R*sin(theta)、
    /// col = cc + R*cos(theta)（同 GearToothAnalyzer / DrawAngle）。
    /// </summary>
    public static class AngleAnnotationMath
    {
        /// <summary>自適應半徑上限（px）。與舊版固定半徑一致，避免弧大到蓋住其他標註。</summary>
        public const double MaxRadiusPx = 80.0;

        /// <summary>自適應半徑下限（px）。低於此值弧小到看不清。</summary>
        public const double MinRadiusPx = 15.0;

        /// <summary>
        /// 由兩條線段算角度標註幾何。回傳 false 表示兩線近平行（無可靠交點），
        /// 呼叫端應退回近似頂點、只顯示數值。
        /// startRad 為弧起點方位角（線 A 方向），sweepRad 為「有號」掃角：
        /// startRad + sweepRad = 線 B 方向（必要時已翻 180°，使 |sweepRad| = 兩線銳角）。
        /// </summary>
        public static bool TryCompute(
            double aRow1, double aCol1, double aRow2, double aCol2,
            double bRow1, double bCol1, double bRow2, double bCol2,
            out double vertexRow, out double vertexCol,
            out double startRad, out double sweepRad, out double radiusPx)
        {
            vertexRow = 0; vertexCol = 0; startRad = 0; sweepRad = 0; radiusPx = MaxRadiusPx;

            double daR = aRow2 - aRow1, daC = aCol2 - aCol1;
            double dbR = bRow2 - bRow1, dbC = bCol2 - bCol1;
            double lenA = Math.Sqrt(daR * daR + daC * daC);
            double lenB = Math.Sqrt(dbR * dbR + dbC * dbC);
            if (lenA < 1e-9 || lenB < 1e-9) return false;   // 退化線段

            // 兩線交點：解 a1 + t*da = b1 + s*db。det 是方向向量的 2D 外積，
            // 相對兩線長度正規化後過小 → 近平行，交點會噴到極遠處，不可靠。
            double det = dbC * daR - daC * dbR;
            if (Math.Abs(det) < 1e-6 * lenA * lenB) return false;

            double eR = bRow1 - aRow1, eC = bCol1 - aCol1;
            double t = (dbC * eR - dbR * eC) / det;
            vertexRow = aRow1 + t * daR;
            vertexCol = aCol1 + t * daC;

            // 有號掃角：從線 A 方向掃到線 B 方向。|sweep| > 90° 時把 B 方向翻 180°
            // （線沒有方向性），使 |sweep| 恰為兩線銳角，與 AcuteAngleDeg 一致。
            double thetaA = Math.Atan2(daR, daC);
            double thetaB = Math.Atan2(dbR, dbC);
            double sweep = NormalizePi(thetaB - thetaA);
            if (Math.Abs(sweep) > Math.PI / 2.0)
                sweep -= Math.Sign(sweep) * Math.PI;
            startRad = thetaA;
            sweepRad = sweep;

            // 半徑自適應：取「頂點到各線較遠端點」中較小者的一部分，讓弧落在兩線
            // 可見範圍內；再夾在 [Min, Max] 之間避免過小/過大。
            double reachA = Math.Max(Dist(vertexRow, vertexCol, aRow1, aCol1),
                                     Dist(vertexRow, vertexCol, aRow2, aCol2));
            double reachB = Math.Max(Dist(vertexRow, vertexCol, bRow1, bCol1),
                                     Dist(vertexRow, vertexCol, bRow2, bCol2));
            radiusPx = Math.Max(MinRadiusPx,
                Math.Min(MaxRadiusPx, 0.4 * Math.Min(reachA, reachB)));
            return true;
        }

        /// <summary>把角度正規化到 (-π, π]。</summary>
        public static double NormalizePi(double rad)
        {
            while (rad > Math.PI) rad -= 2.0 * Math.PI;
            while (rad <= -Math.PI) rad += 2.0 * Math.PI;
            return rad;
        }

        private static double Dist(double r1, double c1, double r2, double c2)
        {
            double dr = r2 - r1, dc = c2 - c1;
            return Math.Sqrt(dr * dr + dc * dc);
        }
    }
}
