using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.MetrologyModel
{
    /// <summary>
    /// 一整個 2D 量測模型：多個物件 + 影像尺寸提示（純資料，無 HALCON）。
    /// 建模時記錄影像尺寸，Apply 時可避免重算量測區（0 = 套用時再查詢）。
    /// </summary>
    public class MetrologyModelDef
    {
        public List<MetrologyObjectDef> Objects { get; set; } = new List<MetrologyObjectDef>();
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
    }
}
