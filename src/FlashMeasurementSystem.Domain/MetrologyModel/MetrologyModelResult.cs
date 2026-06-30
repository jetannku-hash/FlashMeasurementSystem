using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.MetrologyModel
{
    /// <summary>
    /// 整個量測模型套用後的結果（純資料，無 HALCON）。
    /// ErrorMessage 用於整批失敗（如建模/套用例外）；個別物件成敗見各 Objects。
    /// </summary>
    public class MetrologyModelResult
    {
        public List<MetrologyObjectResult> Objects { get; set; } = new List<MetrologyObjectResult>();
        public string ErrorMessage { get; set; } = "";
    }
}
