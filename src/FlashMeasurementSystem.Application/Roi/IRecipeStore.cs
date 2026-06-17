using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Application.Roi
{
    /// <summary>
    /// 配方（.zcp）的持久化（序列化器無關；實作於 Infrastructure）。
    /// </summary>
    public interface IRecipeStore
    {
        void Save(Recipe recipe, string filePath);
        Recipe Load(string filePath);
    }
}
