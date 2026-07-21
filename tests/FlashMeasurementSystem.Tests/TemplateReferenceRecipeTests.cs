using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Application.Roi;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// 配方的模板參照（schema v16）。
    ///
    /// 這組測試守的是一條會**靜默量錯**的路徑：RecipeRunner 以「配方的參考姿態 vs 當前匹配姿態」
    /// 算剛體變換來搬 ROI，而兩個姿態只有在來自同一個 .shm 時才可比較。模板若不跟著配方走，
    /// ROI 會落在錯的位置，工具照樣量、照樣判定 OK/NG，不會有任何錯誤訊息。
    /// </summary>
    public static class TemplateReferenceRecipeTests
    {
        private const int W = 640, H = 480;

        public static void Run()
        {
            DefaultsToUnset();
            RoundTripsThroughStore();
            LegacyFileLoadsUnset();
            WarnsWhenPoseHasNoTemplate();
            NoWarningWhenTemplateRecorded();
            NoWarningWithoutReferencePose();

            Console.WriteLine("TemplateReferenceRecipeTests passed");
        }

        private static void DefaultsToUnset()
        {
            var r = new Recipe();
            Assert(r.TemplateModelId == "", "new recipe should have no template recorded");
        }

        // 存檔名而非完整路徑：配方要能跨機器搬，路徑會因機器而異。
        private static void RoundTripsThroughStore()
        {
            var src = ValidRecipe();
            src.HasReferencePose = true;
            src.TemplateModelId = "template_20260618_181432.shm";

            IRecipeStore store = new RecipeStore();
            string path = Path.Combine(Path.GetTempPath(),
                "fms_tpl_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                store.Save(src, path);
                Recipe rt = store.Load(path);
                AssertEq(rt.TemplateModelId, "template_20260618_181432.shm", "round-trip TemplateModelId");
                Assert(rt.HasReferencePose, "round-trip HasReferencePose");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // v16 之前存的檔沒有這個欄位 → 載入後為空，執行期退回畫面選取的模板（行為與過去相同）。
        private static void LegacyFileLoadsUnset()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "fms_tpl_legacy_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(path,
                    "{ \"SchemaVersion\": 15, \"Name\": \"legacy\", \"HasReferencePose\": true, " +
                    "\"Tools\": [ { \"Id\": \"C1\", \"ToolType\": \"circle\" } ] }");

                Recipe legacy = new RecipeStore().Load(path);
                Assert(string.IsNullOrEmpty(legacy.TemplateModelId), "legacy file has no template recorded");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // 有參考姿態卻沒記錄模板 = 正是會悄悄量錯的情境，必須提出警告。
        private static void WarnsWhenPoseHasNoTemplate()
        {
            var r = ValidRecipe();
            r.HasReferencePose = true;
            r.RefRow = 100; r.RefCol = 200;   // 避開「參考姿態全為 0」那條既有警告
            r.TemplateModelId = "";
            AssertHasTemplateWarning(RecipeValidator.Validate(r, W, H), "pose without template → warning");
        }

        private static void NoWarningWhenTemplateRecorded()
        {
            var r = ValidRecipe();
            r.HasReferencePose = true;
            r.RefRow = 100; r.RefCol = 200;
            r.TemplateModelId = "t.shm";
            AssertNoTemplateWarning(RecipeValidator.Validate(r, W, H), "pose with template → no warning");
        }

        // 沒有參考姿態就不做姿態變換，模板與否無關緊要，不該吵。
        private static void NoWarningWithoutReferencePose()
        {
            var r = ValidRecipe();
            r.HasReferencePose = false;
            r.TemplateModelId = "";
            AssertNoTemplateWarning(RecipeValidator.Validate(r, W, H), "no reference pose → no warning");
        }

        private static Recipe ValidRecipe()
        {
            var r = new Recipe();
            r.Tools.Add(new MeasurementTool
            {
                Id = "c1", Name = "c1", ToolType = "circle",
                Roi = new RoiGeometry { CenterRow = 300, CenterCol = 320, Length1 = 60, Length2 = 50 }
            });
            return r;
        }

        private static bool IsTemplateIssue(RecipeIssue i)
        {
            return i.Message != null && i.Message.Contains("未記錄模板");
        }

        private static void AssertHasTemplateWarning(List<RecipeIssue> issues, string message)
        {
            foreach (RecipeIssue i in issues)
                if (i.Severity == RecipeIssueSeverity.Warning && IsTemplateIssue(i)) return;
            throw new InvalidOperationException("TemplateReference " + message + ": expected a Warning");
        }

        private static void AssertNoTemplateWarning(List<RecipeIssue> issues, string message)
        {
            foreach (RecipeIssue i in issues)
            {
                if (IsTemplateIssue(i))
                    throw new InvalidOperationException(
                        "TemplateReference " + message + ": unexpected issue '" + i.Message + "'");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("TemplateReference " + message);
        }

        private static void AssertEq(string actual, string expected, string message)
        {
            if (actual != expected)
            {
                throw new InvalidOperationException(string.Format(
                    "TemplateReference {0}: expected '{1}', actual '{2}'", message, expected, actual));
            }
        }
    }
}
