using System;
using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// 量測配方（持久化為 .zcp，內容為 JSON）。
    /// SchemaVersion 用於向後相容（日後欄位變動時據此遷移）——第一天即保留。
    /// 校正以 <see cref="CalibrationProfileId"/> 參考獨立的校正檔，不內嵌校正資料。
    /// Tools 內的 ROI 幾何皆定義於參考座標系（見 <see cref="MeasurementTool"/>）。
    /// </summary>
    public class Recipe
    {
        public int SchemaVersion { get; set; } = 1;
        public string RecipeId { get; set; } = "";
        public string Name { get; set; } = "";

        // 以 id 參考 data/calibrations 下的校正檔；空字串表示尚未指定校正。
        public string CalibrationProfileId { get; set; } = "";

        public List<MeasurementTool> Tools { get; set; } = new List<MeasurementTool>();

        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }

        public static Recipe Default()
        {
            return new Recipe();
        }
    }
}
