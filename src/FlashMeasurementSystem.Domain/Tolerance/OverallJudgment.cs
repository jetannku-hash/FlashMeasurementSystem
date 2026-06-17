using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.Tolerance
{
    /// <summary>
    /// 整體量測判定結果。AllOk 為 true 表示沒有任何 NG 項目。
    /// </summary>
    public class OverallJudgment
    {
        public bool AllOk { get; set; }
        public int OkCount { get; set; }
        public int NgCount { get; set; }
        public List<ItemJudgment> Items { get; set; } = new List<ItemJudgment>();
    }
}
