using System;
using System.IO;
using FlashMeasurementSystem.Application.Roi;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    public static class RoiDomainTests
    {
        public static void Run()
        {
            // ─── DTO 預設值 ──────────────────────────────────────────
            RoiGeometry geo = new RoiGeometry();
            AssertEqual(100.0, geo.Length1, "Default Length1");
            AssertEqual(50.0, geo.Length2, "Default Length2");
            AssertEqual(0.0, geo.AngleRad, "Default AngleRad");

            MeasurementTool tool = new MeasurementTool();
            AssertEqual("edge", tool.ToolType, "Default ToolType");
            AssertEqual(false, tool.Roi == null, "Tool has RoiGeometry");
            AssertEqual(false, tool.EdgeParameters == null, "Tool reuses EdgeDetectionParameters");
            AssertEqual(false, tool.Tolerance == null, "Tool reuses ToleranceSpec");

            MeasurementTool defaultTool = new MeasurementTool();
            AssertEqual(0, defaultTool.RefToolIds.Count, "Default tool no RefToolIds");

            Recipe recipe = Recipe.Default();
            AssertEqual(5, recipe.SchemaVersion, "Default Recipe SchemaVersion");
            AssertEqual(0, recipe.Tools.Count, "Default Recipe empty tools");
            AssertEqual("", recipe.CalibrationProfileId, "Default no calibration ref");
            AssertEqual(false, recipe.HasReferencePose, "Default no reference pose");
            AssertEqual(0.0, recipe.RefAngleRad, "Default RefAngleRad");

            // ─── RecipeManager CRUD ─────────────────────────────────
            RecipeManager mgr = new RecipeManager(recipe);

            MeasurementTool a = mgr.Add(new MeasurementTool { Name = "A", ToolType = "circle" });
            AssertEqual(false, string.IsNullOrEmpty(a.Id), "Add assigns Id when empty");
            AssertEqual(1, mgr.Tools.Count, "One tool after add");

            // 重複 Id → 自動指派新 Id
            MeasurementTool b = mgr.Add(new MeasurementTool { Id = a.Id, Name = "B", ToolType = "line" });
            if (b.Id == a.Id)
                throw new InvalidOperationException("Duplicate Id should be reassigned");
            AssertEqual(2, mgr.Tools.Count, "Two tools after second add");

            // Find / GetByType
            AssertEqual("A", mgr.Find(a.Id).Name, "Find by Id");
            AssertEqual(1, mgr.GetByType("circle").Count, "GetByType circle");
            AssertEqual(1, mgr.GetByType("line").Count, "GetByType line");
            AssertEqual(0, mgr.GetByType("angle").Count, "GetByType none");

            // Remove
            AssertEqual(true, mgr.Remove(a.Id), "Remove existing returns true");
            AssertEqual(false, mgr.Remove("nonexistent"), "Remove missing returns false");
            AssertEqual(1, mgr.Tools.Count, "One tool after remove");

            // ─── 介面契約（Fake）────────────────────────────────────
            IRecipeStore fake = new FakeRecipeStore();
            Recipe loaded = fake.Load("dummy");
            AssertEqual("FAKE", loaded.RecipeId, "Fake store satisfies interface contract");

            // ─── 真實 RecipeStore round-trip（含巢狀 DTO）────────────
            Recipe src = new Recipe
            {
                RecipeId = "R-1",
                Name = "demo",
                CalibrationProfileId = "CAL-1",
                HasReferencePose = true,
                RefRow = 1200.5,
                RefCol = 800.25,
                RefAngleRad = 0.3491
            };
            new RecipeManager(src).Add(new MeasurementTool
            {
                Name = "circle1",
                ToolType = "circle",
                Roi = new RoiGeometry { CenterRow = 120, CenterCol = 340, Length1 = 80, Length2 = 25, AngleRad = 0.5 },
                EdgeParameters = new EdgeDetectionParameters { Sigma = 2.0, Threshold = 30, MeasureMode = "edge_pair" },
                Tolerance = new ToleranceSpec { Nominal = 12.5, LowerTolerance = -0.01, UpperTolerance = 0.01, Unit = "mm" }
            });

            IRecipeStore store = new RecipeStore();
            string path = Path.Combine(Path.GetTempPath(),
                "fms_recipe_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                store.Save(src, path);
                if (!File.Exists(path))
                    throw new InvalidOperationException("Recipe file not written");
                Recipe rt = store.Load(path);

                AssertEqual(5, rt.SchemaVersion, "Round-trip SchemaVersion");
                AssertEqual("R-1", rt.RecipeId, "Round-trip RecipeId");
                AssertEqual("CAL-1", rt.CalibrationProfileId, "Round-trip calibration ref by id");
                AssertEqual(true, rt.HasReferencePose, "Round-trip HasReferencePose");
                AssertClose(1200.5, rt.RefRow, 1e-9, "Round-trip RefRow");
                AssertClose(0.3491, rt.RefAngleRad, 1e-9, "Round-trip RefAngleRad");
                AssertEqual(1, rt.Tools.Count, "Round-trip tool count");

                MeasurementTool t = rt.Tools[0];
                AssertEqual("circle", t.ToolType, "Round-trip ToolType");
                // 巢狀 RoiGeometry
                AssertClose(80.0, t.Roi.Length1, 1e-9, "Round-trip Roi.Length1");
                AssertClose(0.5, t.Roi.AngleRad, 1e-9, "Round-trip Roi.AngleRad (radian)");
                // 巢狀 EdgeDetectionParameters
                AssertClose(2.0, t.EdgeParameters.Sigma, 1e-9, "Round-trip EdgeParameters.Sigma");
                AssertEqual("edge_pair", t.EdgeParameters.MeasureMode, "Round-trip EdgeParameters.MeasureMode");
                // 巢狀 ToleranceSpec
                AssertClose(12.5, t.Tolerance.Nominal, 1e-9, "Round-trip Tolerance.Nominal");
                AssertClose(0.01, t.Tolerance.UpperTolerance, 1e-9, "Round-trip Tolerance.UpperTolerance");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }

            // ─── Load 錯誤處理：缺檔擲明確例外（非 raw、非 null）──
            string missing = Path.Combine(Path.GetTempPath(),
                "fms_missing_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            AssertThrows(() => new RecipeStore().Load(missing), "Load missing recipe throws");

            // ─── Load 錯誤處理：損毀 JSON 擲明確例外 ──
            string corrupt = Path.Combine(Path.GetTempPath(),
                "fms_corrupt_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            File.WriteAllText(corrupt, "{ this is not valid json ");
            try { AssertThrows(() => new RecipeStore().Load(corrupt), "Load corrupt recipe JSON throws"); }
            finally { if (File.Exists(corrupt)) File.Delete(corrupt); }

            // ─── 原子覆寫：覆寫既有檔後仍正確載回新內容，且不殘留 .tmp ──
            string ovr = Path.Combine(Path.GetTempPath(),
                "fms_ovr_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                IRecipeStore s = new RecipeStore();
                s.Save(new Recipe { RecipeId = "V1" }, ovr);
                s.Save(new Recipe { RecipeId = "V2" }, ovr); // 覆寫既有檔
                AssertEqual("V2", s.Load(ovr).RecipeId, "Atomic overwrite keeps latest content");
                if (File.Exists(ovr + ".tmp"))
                    throw new InvalidOperationException("Save should not leave a .tmp file behind");
            }
            finally { if (File.Exists(ovr)) File.Delete(ovr); }
        }

        private static void AssertThrows(Action action, string name)
        {
            try { action(); }
            catch { return; }
            throw new InvalidOperationException(name + " — expected an exception but none was thrown");
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual + " (tol " + tol + ")");
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }

        private sealed class FakeRecipeStore : IRecipeStore
        {
            public void Save(Recipe recipe, string filePath) { }
            public Recipe Load(string filePath)
            {
                return new Recipe { RecipeId = "FAKE" };
            }
        }
    }
}
