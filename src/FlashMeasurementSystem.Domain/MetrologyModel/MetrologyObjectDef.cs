using FlashMeasurementSystem.Domain.Tolerance;

namespace FlashMeasurementSystem.Domain.MetrologyModel
{
    /// <summary>
    /// 單一量測模型物件的標稱幾何 + 量測參數（純資料，無 HALCON）。
    /// 依 Shape 只用對應的幾何欄位，其餘忽略。量測參數驅動 HALCON 沿輪廓
    /// 自動佈放量測矩形（MeasureDistance 或 NumMeasures）。
    /// </summary>
    public class MetrologyObjectDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public MetrologyObjectType Shape { get; set; } = MetrologyObjectType.Line;

        // 標稱幾何 —— 每種 Shape 一組，其餘忽略。
        // Line：
        public double RowBegin { get; set; }
        public double ColumnBegin { get; set; }
        public double RowEnd { get; set; }
        public double ColumnEnd { get; set; }
        // Circle / Ellipse / Rectangle 中心：
        public double Row { get; set; }
        public double Column { get; set; }
        // Circle：
        public double Radius { get; set; }
        // Ellipse / Rectangle：
        public double Phi { get; set; }       // 主軸方向（radian）
        public double Radius1 { get; set; }   // ellipse 較大半軸
        public double Radius2 { get; set; }   // ellipse 較小半軸
        public double Length1 { get; set; }   // rectangle 較長半邊
        public double Length2 { get; set; }   // rectangle 較短半邊

        // 量測參數（所有 Shape 通用）。MeasureLength1 必須 < 對應的半徑/半邊
        // （HALCON add 時的硬性限制，於適配器層驗證）。
        public double MeasureLength1 { get; set; } = 20.0;
        public double MeasureLength2 { get; set; } = 5.0;
        public double MeasureSigma { get; set; } = 1.0;
        public double MeasureThreshold { get; set; } = 30.0;
        public double MeasureDistance { get; set; } = 10.0; // 0 = 改用 NumMeasures
        public int NumMeasures { get; set; } = 0;            // 0 = 改用 MeasureDistance

        // 對主要擬合參數（如圓的半徑）的選用公差，沿用既有 1D 的 ToleranceSpec。
        public ToleranceSpec Tolerance { get; set; } = null;

        /// <summary>
        /// 各 Shape 在 HALCON 下能擬合的最少量測區數（量測矩形數需 ≥ 此值才有解）。
        /// 數值來源：HALCON 17.12 reference（02-RESEARCH.md 操作表 L159-268）。
        /// Line 2 / Circle 3 / Ellipse 5 / Rectangle 8。
        /// </summary>
        public static int MinMeasureRegions(MetrologyObjectType shape)
        {
            switch (shape)
            {
                case MetrologyObjectType.Line: return 2;       // 兩點定線
                case MetrologyObjectType.Circle: return 3;     // 三點定圓
                case MetrologyObjectType.Ellipse: return 5;    // 五參數橢圓
                case MetrologyObjectType.Rectangle: return 8;  // 四邊各 ≥2
                default: return 2;
            }
        }
    }
}
