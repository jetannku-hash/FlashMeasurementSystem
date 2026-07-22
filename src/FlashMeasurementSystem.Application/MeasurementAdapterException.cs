using System;

namespace FlashMeasurementSystem.Application
{
    /// <summary>
    /// 量測 adapter 在邊界翻譯後對外拋出的例外，取代讓具體 vendor 例外（如 HalconException）
    /// 穿透 Application 介面。orchestrator（RecipeRunner / MeasurementWorkflow）改為捕捉此型別，
    /// 因而不再相依 HalconDotNet，得以搬進 HALCON-free 的 Application 層並被測試專案觸及。
    /// adapter 應在自身 catch 到 vendor 例外時，包成本型別（保留 InnerException）再拋出。
    /// </summary>
    [Serializable]
    public class MeasurementAdapterException : Exception
    {
        public MeasurementAdapterException(string message)
            : base(message) { }

        public MeasurementAdapterException(string message, Exception inner)
            : base(message, inner) { }
    }
}
