using System;
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
            // 原子寫入：先寫同目錄暫存檔再 rename 覆蓋，避免中途崩潰/斷電截斷既有好檔。
            string tmp = filePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(filePath))
                File.Replace(tmp, filePath, null);
            else
                File.Move(tmp, filePath);
        }

        public Recipe Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("找不到配方檔：" + filePath, filePath);

            string json;
            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException("讀取配方檔失敗：" + filePath + " — " + ex.Message, ex);
            }

            Recipe recipe;
            try
            {
                recipe = JsonConvert.DeserializeObject<Recipe>(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("配方檔格式錯誤（JSON 解析失敗）：" + filePath + " — " + ex.Message, ex);
            }

            if (recipe == null)
                throw new InvalidOperationException("配方檔內容為空或無效：" + filePath);

            return recipe;
        }
    }
}
