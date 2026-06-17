using System.IO;
using FlashMeasurementSystem.Application.Roi;
using FlashMeasurementSystem.Domain.Roi;
using Newtonsoft.Json;

namespace FlashMeasurementSystem.Infrastructure.Roi
{
    /// <summary>
    /// 配方持久化（.zcp，內容為 JSON，Newtonsoft）。存於 data/recipes。
    /// 巢狀 DTO（RoiGeometry / EdgeDetectionParameters / ToleranceSpec）一併往返。
    /// 序列化封在 Infrastructure，App 層不直接相依 Newtonsoft。
    /// </summary>
    public class RecipeStore : IRecipeStore
    {
        public void Save(Recipe recipe, string filePath)
        {
            string json = JsonConvert.SerializeObject(recipe, Formatting.Indented);
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filePath, json);
        }

        public Recipe Load(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Recipe>(json);
        }
    }
}
