using System;

namespace FlashMeasurementSystem.Domain.EdgeDetection
{
    public class EdgeDetectionRoi
    {
        public double CenterRow { get; set; }
        public double CenterCol { get; set; }
        public double Length1 { get; set; }
        public double Length2 { get; set; }
        public double AngleRad { get; set; }

        public bool IsDefined
        {
            get { return Length1 >= 1.0 && Length2 >= 1.0; }
        }

        /// <summary>
        /// 從滑鼠拖曳的兩個角點建立 ROI。
        ///
        /// HALCON measure_pos 的搜尋方向（major axis = Phi）必須**垂直於**要偵測的邊緣
        /// （reference L3759-L3763、L3766-L3768 attention）。
        ///
        /// 使用者畫 ROI 的視覺直覺：拖一個**橫長**矩形（colSpan &gt; rowSpan）通常是想
        /// 「沿著」一條水平結構框過去 → 想量**水平邊緣** → measure rectangle 需要
        ///   Phi = π/2（主軸沿 row 方向）穿越這條水平邊。
        ///
        /// 反之，縱長 ROI（rowSpan &gt; colSpan）通常是想量垂直邊緣 → Phi = 0。
        ///
        /// Length1 = 沿 major axis 半長（reference L3520「half width」），
        /// Length2 = 垂直 major axis 半長（reference L3527「half height」）。
        /// </summary>
        public static EdgeDetectionRoi FromBounds(double row1, double col1, double row2, double col2)
        {
            double centerRow = (row1 + row2) / 2.0;
            double centerCol = (col1 + col2) / 2.0;
            double halfRow = Math.Abs(row2 - row1) / 2.0;
            double halfCol = Math.Abs(col2 - col1) / 2.0;

            if (halfCol >= halfRow)
            {
                // 橫長 ROI（寬>高）→ 沿水平結構框 → 量水平邊緣 → 主軸垂直（Phi=π/2）
                return new EdgeDetectionRoi
                {
                    CenterRow = centerRow,
                    CenterCol = centerCol,
                    Length1 = halfRow,            // 沿 major axis (row) 半長
                    Length2 = halfCol,            // 沿 perpendicular (col) 半長
                    AngleRad = Math.PI / 2.0      // 主軸垂直，掃描 row 方向找水平邊
                };
            }
            else
            {
                // 縱長 ROI（高>寬）→ 沿垂直結構框 → 量垂直邊緣 → 主軸水平（Phi=0）
                return new EdgeDetectionRoi
                {
                    CenterRow = centerRow,
                    CenterCol = centerCol,
                    Length1 = halfCol,            // 沿 major axis (col) 半長
                    Length2 = halfRow,            // 沿 perpendicular (row) 半長
                    AngleRad = 0.0                // 主軸水平，掃描 col 方向找垂直邊
                };
            }
        }

        public static EdgeDetectionRoi FromCenter(double centerRow, double centerCol, double length1, double length2, double angleRad)
        {
            return new EdgeDetectionRoi
            {
                CenterRow = centerRow,
                CenterCol = centerCol,
                Length1 = length1,
                Length2 = length2,
                AngleRad = angleRad
            };
        }
    }
}
