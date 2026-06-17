using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Infrastructure.Roi
{
    /// <summary>
    /// 配方內量測工具的 CRUD（純邏輯，不依賴 HALCON / 序列化器）。
    /// 操作傳入的 <see cref="Recipe"/> 物件；持久化由 IRecipeStore 負責。
    /// </summary>
    public class RecipeManager
    {
        private readonly Recipe _recipe;

        public RecipeManager(Recipe recipe)
        {
            _recipe = recipe ?? throw new ArgumentNullException("recipe");
            if (_recipe.Tools == null)
            {
                _recipe.Tools = new List<MeasurementTool>();
            }
        }

        public IReadOnlyList<MeasurementTool> Tools
        {
            get { return _recipe.Tools.AsReadOnly(); }
        }

        /// <summary>
        /// 新增工具。若 Id 為空或與既有重複，會指派一個新的唯一 Id。
        /// </summary>
        public MeasurementTool Add(MeasurementTool tool)
        {
            if (tool == null) throw new ArgumentNullException("tool");

            if (string.IsNullOrEmpty(tool.Id) || Exists(tool.Id))
            {
                tool.Id = NewId();
            }
            _recipe.Tools.Add(tool);
            return tool;
        }

        public bool Remove(string toolId)
        {
            return _recipe.Tools.RemoveAll(t => t.Id == toolId) > 0;
        }

        public MeasurementTool Find(string toolId)
        {
            return _recipe.Tools.Find(t => t.Id == toolId);
        }

        public List<MeasurementTool> GetByType(string toolType)
        {
            return _recipe.Tools.FindAll(t => t.ToolType == toolType);
        }

        private bool Exists(string id)
        {
            return _recipe.Tools.Exists(t => t.Id == id);
        }

        private string NewId()
        {
            string id;
            do
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8);
            }
            while (Exists(id));
            return id;
        }
    }
}
