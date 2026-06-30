using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.MetrologyModel
{
    /// <summary>
    /// 單一量測模型物件的擬合結果（純資料，無 HALCON）。
    /// 只填對應 Shape 的擬合幾何欄位；其餘留預設。
    /// </summary>
    public class MetrologyObjectResult
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public MetrologyObjectType Shape { get; set; }
        public bool Success { get; set; }
        public double Score { get; set; }  // [0,1] 找到邊的量測區比例
        public string ErrorMessage { get; set; } = "";

        // 擬合幾何（只填此 Shape 的欄位）
        // Line：
        public double FitRowBegin { get; set; }
        public double FitColumnBegin { get; set; }
        public double FitRowEnd { get; set; }
        public double FitColumnEnd { get; set; }
        // Circle：
        public double FitRow { get; set; }
        public double FitColumn { get; set; }
        public double FitRadius { get; set; }
        // Ellipse：
        public double FitPhi { get; set; }
        public double FitRadius1 { get; set; }
        public double FitRadius2 { get; set; }
        // Rectangle：
        public double FitLength1 { get; set; }
        public double FitLength2 { get; set; }

        // 量測區內找到的所有邊點（get_metrology_object_measures）
        public List<double> MeasurePointRows { get; set; } = new List<double>();
        public List<double> MeasurePointCols { get; set; } = new List<double>();

        // 選用公差判定（沿用 1D 的 judger）
        public bool? IsOk { get; set; }
        public string ValueText { get; set; } = "";
    }
}
