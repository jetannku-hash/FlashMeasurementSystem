using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Application.Roi;
using FlashMeasurementSystem.Domain.ImageQuality;
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
            SaveStampsCurrentSchemaVersion();
            EditRoundTripKeepsEveryField();

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

        // 載入舊檔 → 改內容 → 存檔，檔案必須標成目前版號。
        // 實際踩到的情況：Set Ref 把 v16 的 TemplateModelId 寫進一個 v12 的檔，版號卻仍留 12，
        // 產生「內容是新的、標籤是舊的」的檔案，日後任何依版號的遷移都會誤判它。
        private static void SaveStampsCurrentSchemaVersion()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "fms_ver_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(path,
                    "{ \"SchemaVersion\": 12, \"Name\": \"old\", \"HasReferencePose\": true, " +
                    "\"Tools\": [ { \"Id\": \"C1\", \"ToolType\": \"circle\" } ] }");

                IRecipeStore store = new RecipeStore();
                Recipe loaded = store.Load(path);
                AssertEqInt(loaded.SchemaVersion, 12, "loading preserves the file's version");

                // 模擬 Set Ref：寫入 v16 欄位後存回原檔。
                loaded.TemplateModelId = "t.shm";
                store.Save(loaded, path);

                Recipe reloaded = store.Load(path);
                AssertEqInt(reloaded.SchemaVersion, new Recipe().SchemaVersion,
                    "saving must stamp the current schema version");
                AssertEq(reloaded.TemplateModelId, "t.shm", "saved template id survives");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // 編輯器的「載入 → 編輯 → 存檔」不可弄丟任何欄位。
        //
        // 這條路徑踩過兩次：編輯器原本逐欄手寫複製，v15 的 IqcThresholds 補上了，
        // v16 的 TemplateModelId 仍然漏掉——症狀是按下 Save 後配方記錄的模板被靜默清空，
        // 檔案看起來一切正常，直到之後量測位置出錯或驗證跳警告才會發現。
        // 改用 Recipe.CloneWithoutTools()（MemberwiseClone）後漏抄不再可能。
        //
        // 本測試鎖的是 CloneWithoutTools 的契約：除 Tools 外每個屬性都必須被保留
        // （以反射逐一比對，新增欄位自動納入檢查）。它**無法**偵測有人把編輯器改回
        // 逐欄手寫複製——那段程式在 App.Wpf 且為 private，測不到。編輯器必須持續
        // 委派給這個方法，這點只能靠 CopyRecipeMetadata 上的註解說明。
        private static void EditRoundTripKeepsEveryField()
        {
            var src = ValidRecipe();
            src.RecipeId = "R-9";
            src.Name = "orig";
            src.CalibrationProfileId = "CAL-9";
            src.RefRow = 11.5; src.RefCol = 22.5; src.RefAngleRad = 0.75;
            src.HasReferencePose = true;
            src.TemplateModelId = "t_edit.shm";
            src.IqcThresholds = new ImageQualityThresholds { MaxBrightness = 199.0 };
            src.CreatedAt = new DateTime(2026, 7, 21, 10, 0, 0);
            src.ModifiedAt = new DateTime(2026, 7, 21, 11, 0, 0);

            Recipe copy = src.CloneWithoutTools();

            Assert(copy.Tools != null && copy.Tools.Count == 0, "clone must start with an empty tool list");
            Assert(!ReferenceEquals(copy.Tools, src.Tools), "clone must not share the tool list");

            foreach (var prop in typeof(Recipe).GetProperties())
            {
                if (prop.Name == "Tools") continue;          // 刻意換成空清單
                object a = prop.GetValue(src, null);
                object b = prop.GetValue(copy, null);
                if (!Equals(a, b))
                {
                    throw new InvalidOperationException(string.Format(
                        "TemplateReference edit round-trip dropped '{0}': expected {1}, actual {2}",
                        prop.Name, a ?? "null", b ?? "null"));
                }
            }
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

        private static void AssertEqInt(int actual, int expected, string message)
        {
            if (actual != expected)
            {
                throw new InvalidOperationException(string.Format(
                    "TemplateReference {0}: expected {1}, actual {2}", message, expected, actual));
            }
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
