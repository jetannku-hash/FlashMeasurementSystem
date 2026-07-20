namespace FlashMeasurementSystem.Domain.Reporting
{
    /// <summary>
    /// 報表中的一列判定結果（純文字，已格式化完畢）。
    /// 渲染層（PDF/HTML）只負責排版，不再做任何數值格式化。
    /// </summary>
    public class MeasurementReportRow
    {
        public string ItemName { get; set; } = "";      // 對應 ItemJudgment.ToolName
        public string NominalText { get; set; } = "";   // 標稱值（含單位，count 無單位無小數）
        public string LimitsText { get; set; } = "";    // "下限 ~ 上限"；上下限相同時只顯示單一值
        public string MeasuredText { get; set; } = "";  // 實測值（含單位）
        public string VerdictText { get; set; } = "";   // "OK" / "NG" / "—"（未判定）
        public bool? IsOk { get; set; }                 // null = 未判定，供渲染層上色用
        public string Note { get; set; } = "";          // 對應 ItemJudgment.Message，可為 ""
    }
}
