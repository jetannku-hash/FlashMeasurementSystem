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
        // v2：參考姿態（RefRow/RefCol/RefAngleRad + HasReferencePose），供 ROI 跟隨工件。
        // v3：MeasurementTool.RefToolIds（複合工具 distance/angle 參考元素工具）。
        // v4：A5 構造工具（intersection/midline/projection，沿用 RefToolIds）。
        // v5：GD&T 形位公差工具（roundness/straightness/parallelism/perpendicularity/
        //     concentricity + MeasurementTool.Gdt）。純加欄位，向後相容、無遷移碼：
        //     舊檔載入時 Gdt=null、無 GD&T 工具，行為不變。
        public int SchemaVersion { get; set; } = 5;
        public string RecipeId { get; set; } = "";
        public string Name { get; set; } = "";

        // 以 id 參考 data/calibrations 下的校正檔；空字串表示尚未指定校正。
        public string CalibrationProfileId { get; set; } = "";

        // 參考姿態：定義 ROI 時模板匹配到的工件姿態（reference frame）。執行期以此與「當前
        // 匹配姿態」算剛體變換，使各工具 ROI 跟著工件平移/旋轉。角度為 radian。
        // HasReferencePose = false 時，ROI 視為已在當前影像座標系（不做轉換）。
        public double RefRow { get; set; }
        public double RefCol { get; set; }
        public double RefAngleRad { get; set; }
        public bool HasReferencePose { get; set; }

        public List<MeasurementTool> Tools { get; set; } = new List<MeasurementTool>();

        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }

        public static Recipe Default()
        {
            return new Recipe();
        }
    }
}
