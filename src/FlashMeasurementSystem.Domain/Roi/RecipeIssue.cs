namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>配方問題嚴重度。Error＝會使量測無法進行（應阻擋）；Warning＝可疑但可繼續。</summary>
    public enum RecipeIssueSeverity
    {
        Error,
        Warning
    }

    /// <summary>
    /// 配方驗證（<see cref="RecipeValidator"/>）找到的單一問題。
    /// ToolId/ToolName 指向關聯工具；配方層級問題（零工具等）兩者為空字串。
    /// </summary>
    public sealed class RecipeIssue
    {
        public RecipeIssueSeverity Severity { get; private set; }
        public string ToolId { get; private set; }
        public string ToolName { get; private set; }
        public string Message { get; private set; }

        public RecipeIssue(RecipeIssueSeverity severity, string toolId, string toolName, string message)
        {
            Severity = severity;
            ToolId = toolId ?? "";
            ToolName = toolName ?? "";
            Message = message ?? "";
        }
    }
}
