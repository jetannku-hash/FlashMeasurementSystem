namespace FlashMeasurementSystem.Domain.Repeatability
{
    /// <summary>
    /// GR&R% 判定（開發手冊 §6.3）。NotAvailable：未提供有效公差範圍時，GR&R% 無意義。
    /// </summary>
    public enum RepeatabilityVerdict
    {
        NotAvailable,   // 無有效公差範圍 → GR&R% 不適用
        Excellent,      // < 10%    量測系統足夠
        Acceptable,     // 10% ~ 30% 視應用要求
        Unacceptable    // > 30%    量測系統需改善
    }
}
