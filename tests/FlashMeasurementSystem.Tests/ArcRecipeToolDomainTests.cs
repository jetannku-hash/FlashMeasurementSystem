using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    // 弧形 ROI 進配方（schema v7）：預設值、序列化 round-trip、向後相容、Validator 規則。
    public static class ArcRecipeToolDomainTests
    {
        public static void Run()
        {
            // ─── 預設：非弧工具 ArcRoi 為 null（既有工具不受影響）───
            var plain = new MeasurementTool();
            AssertEqual(null, plain.ArcRoi, "Default ArcRoi is null");
            AssertEqual(11, Recipe.Default().SchemaVersion, "SchemaVersion is 11");

            // ─── RecipeStore round-trip：ArcRoi 六個欄位逐一保留 ───
            var arc = new ArcMeasureRoi
            {
                CenterRow = 250.5, CenterCol = 300.25, Radius = 120.75,
                AngleStart = 0.5, AngleExtent = 2.0 * Math.PI, AnnulusRadius = 8.5
            };
            var recipe = Recipe.Default();
            recipe.Name = "ARC-RT";
            recipe.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "A1", Name = "孔數", ToolType = "arc", ArcRoi = arc }
            };
            string path = Path.Combine(Path.GetTempPath(),
                "fms_arc_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                var store = new RecipeStore();
                store.Save(recipe, path);
                Recipe rt = store.Load(path);
                AssertEqual(1, rt.Tools.Count, "round-trip tool count");
                ArcMeasureRoi a = rt.Tools[0].ArcRoi;
                if (a == null) throw new InvalidOperationException("round-trip ArcRoi is null");
                AssertClose(250.5, a.CenterRow, 1e-9, "round-trip CenterRow");
                AssertClose(300.25, a.CenterCol, 1e-9, "round-trip CenterCol");
                AssertClose(120.75, a.Radius, 1e-9, "round-trip Radius");
                AssertClose(0.5, a.AngleStart, 1e-9, "round-trip AngleStart");
                AssertClose(2.0 * Math.PI, a.AngleExtent, 1e-9, "round-trip AngleExtent");
                AssertClose(8.5, a.AnnulusRadius, 1e-9, "round-trip AnnulusRadius");
                AssertEqual("arc", rt.Tools[0].ToolType, "round-trip ToolType");
            }
            finally { if (File.Exists(path)) File.Delete(path); }

            // ─── 向後相容：無 ArcRoi 欄位的舊 JSON → ArcRoi=null、其餘不受影響 ───
            string oldJson = "{ \"SchemaVersion\": 6, \"Name\": \"OLD\", \"Tools\": [ " +
                             "{ \"Id\": \"C1\", \"Name\": \"circle1\", \"ToolType\": \"circle\" } ] }";
            string oldPath = Path.Combine(Path.GetTempPath(),
                "fms_arc_old_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(oldPath, oldJson);
                Recipe old = new RecipeStore().Load(oldPath);
                AssertEqual(1, old.Tools.Count, "old recipe tool count");
                AssertEqual(null, old.Tools[0].ArcRoi, "old recipe ArcRoi is null");
                AssertEqual("circle", old.Tools[0].ToolType, "old recipe ToolType intact");
                AssertEqual("OLD", old.Name, "old recipe Name intact");
            }
            finally { if (File.Exists(oldPath)) File.Delete(oldPath); }

            // ─── Validator：合法 arc 工具 → 無 issue ───
            var okRecipe = Recipe.Default();
            okRecipe.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "A1", Name = "孔數", ToolType = "arc",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 200, CenterCol = 200, Radius = 100,
                        AngleStart = 0, AngleExtent = 2.0 * Math.PI, AnnulusRadius = 5 } }
            };
            AssertEqual(0, CountErrors(RecipeValidator.Validate(okRecipe, 640, 480)),
                "valid arc tool has no errors");

            // ─── Validator：arc 工具缺 ArcRoi → Error ───
            var missing = Recipe.Default();
            missing.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "A2", Name = "無弧", ToolType = "arc", ArcRoi = null }
            };
            if (CountErrors(RecipeValidator.Validate(missing, 640, 480)) == 0)
                throw new InvalidOperationException("arc tool without ArcRoi should be an Error");

            // ─── Validator：arc 工具 ArcRoi 無效（半徑 0）→ Error ───
            var bad = Recipe.Default();
            bad.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "A3", Name = "壞弧", ToolType = "arc",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 200, CenterCol = 200, Radius = 0,
                        AngleStart = 0, AngleExtent = 2.0 * Math.PI, AnnulusRadius = 5 } }
            };
            if (CountErrors(RecipeValidator.Validate(bad, 640, 480)) == 0)
                throw new InvalidOperationException("arc tool with invalid ArcRoi should be an Error");
        }

        private static int CountErrors(List<RecipeIssue> issues)
        {
            int n = 0;
            foreach (RecipeIssue i in issues)
                if (i.Severity == RecipeIssueSeverity.Error) n++;
            return n;
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }
    }
}
