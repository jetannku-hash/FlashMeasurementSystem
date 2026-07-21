using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.ImageQuality;
using FlashMeasurementSystem.Domain.MetrologyModel;

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
        // v6：2D 量測模型（MetrologyModel，加性 nullable 欄）。純加欄位、向後相容、無遷移碼：
        //     舊檔載入時 MetrologyModel=null，1D 流程行為不變。
        // v7：弧形 ROI（MeasurementTool.ArcRoi，加性 nullable 欄）+ ToolType="arc" 弧形卡尺工具。
        //     純加欄位、向後相容、無遷移碼：舊檔載入時 ArcRoi=null、無 arc 工具，1D 流程行為不變。
        // v8：齒輪工具（MeasurementTool.Gear，加性 nullable 欄）+ ToolType="gear"。
        //     純加欄位、向後相容、無遷移碼：舊檔載入時 Gear=null、無 gear 工具，行為不變。
        // v9：PCD 螺栓孔圈工具（MeasurementTool.Pcd，加性 nullable 欄）+ ToolType="pcd"。
        //     純加欄位、向後相容、無遷移碼：舊檔載入 Pcd=null、無 pcd 工具，行為不變。
        // v10：circle 工具的 ROI 形狀選擇（MeasurementTool.RoiShape，加性欄，預設 "rect"）。
        //     純加欄位、向後相容、無遷移碼：舊檔載入 RoiShape="rect"＝走既有矩形路徑，行為不變。
        // v11：量測模型物件每判定量公差（MetrologyObjectDef.Tolerances，加性欄，預設空清單）。
        //     純加欄位、向後相容、無遷移碼：舊檔載入 Tolerances=空清單＝不判定（IsOk=null），行為不變。
        // v12：引腳間距工具（MeasurementTool.PinPitch，加性 nullable 欄）+ ToolType="pin_pitch"。
        //     純加欄位、向後相容、無遷移碼：舊檔載入 PinPitch=null、無 pin_pitch 工具，行為不變。
        // v13：孔陣列工具（MeasurementTool.HoleArray，加性 nullable 欄）+ ToolType="hole_array"。
        //     純加欄位、向後相容、無遷移碼：舊檔載入 HoleArray=null、無 hole_array 工具，行為不變。
        // v14：孔陣列圓度濾波（HoleArrayAnalysisParameters.MinCircularity，加性欄，預設 0.80）。
        //     純加欄位、向後相容、無遷移碼：舊檔載入時該欄取預設 0.80＝啟用併塊濾除。這會改變舊配方的
        //     偵測行為（沾黏孔不再被當成一個大孔），屬「修正錯誤量測」而非破壞相容；需要舊行為者設 0 停用。
        // v15：每配方影像品質門檻（IqcThresholds，加性 nullable 欄）。純加欄位、向後相容、無遷移碼：
        //     舊檔載入時 IqcThresholds=null → EffectiveIqcThresholds() 回退到既有的全域預設，行為不變。
        //     動機：正確的亮度/銳利度門檻取決於工件、鏡頭與打光，單一全域值不可能對所有料號都對；
        //     門檻寫死時，一旦不適用當前設定，操作員除了請工程師勾「略過IQC」之外毫無出路。
        public int SchemaVersion { get; set; } = 15;
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

        // v6：選用的 2D 量測模型；null = 無（向後相容，舊 .zcp 載入時為 null、行為不變）。
        // 與 Tools 並存：執行期 RecipeRunner 在 1D passes 之後加一個 metrology pass。
        public MetrologyModelDef MetrologyModel { get; set; } = null;

        // v15：選用的每配方影像品質門檻；null = 沿用全域預設（向後相容，舊 .zcp 載入時為 null）。
        public ImageQualityThresholds IqcThresholds { get; set; } = null;

        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }

        /// <summary>
        /// 本配方實際採用的影像品質門檻：有設就用配方的，沒設就回退全域預設。
        ///
        /// 刻意集中於此而非在各呼叫端各寫一次 null 判斷——量測流程（MeasurementWorkflow）與
        /// 單獨的影像品質檢查按鈕是兩個入口，兩邊若各自實作 fallback 就可能漂移，
        /// 導致「單獨檢查說 PASS、一鍵量測卻 FAIL」這種最難查的不一致。
        /// </summary>
        public ImageQualityThresholds EffectiveIqcThresholds()
        {
            return IqcThresholds ?? ImageQualityThresholds.Default();
        }

        public static Recipe Default()
        {
            return new Recipe();
        }
    }
}
