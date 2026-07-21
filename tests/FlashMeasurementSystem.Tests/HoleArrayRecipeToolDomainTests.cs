using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Domain.HoleArrayAnalysis;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    public static class HoleArrayRecipeToolDomainTests
    {
        public static void Run()
        {
            AssertEqual(16, Recipe.Default().SchemaVersion, "SchemaVersion is 16");
            AssertEqual(null, new MeasurementTool().HoleArray, "Default HoleArray is null");

            var recipe = Recipe.Default();
            recipe.Tools = new List<MeasurementTool>
            {
                new MeasurementTool
                {
                    Id = "HA1", Name = "孔陣列", ToolType = "hole_array",
                    Roi = new RoiGeometry { CenterRow = 500, CenterCol = 700, Length1 = 250, Length2 = 180, AngleRad = 0.2 },
                    HoleArray = new HoleArrayAnalysisParameters { Rows = 3, Cols = 4,
                        NominalDiameterMm = 1.2, DiameterToleranceMm = 0.06,
                        NominalPitchXMm = 2.5, NominalPitchYMm = 2.0,
                        PitchToleranceMm = 0.08, PositionToleranceMm = 0.09,
                        HoleIsDark = false, MinHoleAreaPx = 42, MinCircularity = 0.66 }
                }
            };
            string path = Path.Combine(Path.GetTempPath(), "fms_holearray_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                new RecipeStore().Save(recipe, path);
                Recipe rt = new RecipeStore().Load(path);
                HoleArrayAnalysisParameters h = rt.Tools[0].HoleArray;
                if (h == null) throw new InvalidOperationException("round-trip HoleArray null");
                AssertEqual(3, h.Rows, "rt Rows");
                AssertEqual(4, h.Cols, "rt Cols");
                AssertClose(1.2, h.NominalDiameterMm, 1e-9, "rt NominalDiameterMm");
                AssertClose(0.06, h.DiameterToleranceMm, 1e-9, "rt DiameterToleranceMm");
                AssertClose(2.5, h.NominalPitchXMm, 1e-9, "rt NominalPitchXMm");
                AssertClose(2.0, h.NominalPitchYMm, 1e-9, "rt NominalPitchYMm");
                AssertClose(0.08, h.PitchToleranceMm, 1e-9, "rt PitchToleranceMm");
                AssertClose(0.09, h.PositionToleranceMm, 1e-9, "rt PositionToleranceMm");
                AssertEqual(false, h.HoleIsDark, "rt HoleIsDark");
                AssertClose(42, h.MinHoleAreaPx, 1e-9, "rt MinHoleAreaPx");
                AssertClose(0.66, h.MinCircularity, 1e-9, "rt MinCircularity");
                RoiGeometry roi = rt.Tools[0].Roi;
                if (roi == null) throw new InvalidOperationException("rt Roi null");
                AssertClose(500, roi.CenterRow, 1e-9, "rt Roi CenterRow");
                AssertClose(700, roi.CenterCol, 1e-9, "rt Roi CenterCol");
                AssertClose(250, roi.Length1, 1e-9, "rt Roi Length1");
                AssertClose(180, roi.Length2, 1e-9, "rt Roi Length2");
                AssertClose(0.2, roi.AngleRad, 1e-9, "rt Roi AngleRad");
            }
            finally { if (File.Exists(path)) File.Delete(path); }

            // 向後相容：舊 JSON 無 HoleArray → null
            string oldPath = Path.Combine(Path.GetTempPath(), "fms_holearray_old_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(oldPath, "{ \"SchemaVersion\": 12, \"Tools\": [ { \"Id\": \"C1\", \"ToolType\": \"circle\" } ] }");
                Recipe old = new RecipeStore().Load(oldPath);
                AssertEqual(null, old.Tools[0].HoleArray, "old HoleArray null");
            }
            finally { if (File.Exists(oldPath)) File.Delete(oldPath); }

            // v14 向後相容：v13 舊檔的 hole_array 沒有 MinCircularity 欄 → 取預設 0.80（併塊濾除預設啟用），
            // 其餘欄位照舊讀出，舊配方不需遷移即可載入。
            AssertClose(0.80, HoleArrayAnalysisParameters.Default().MinCircularity, 1e-9, "default MinCircularity 0.80");
            string v13Path = Path.Combine(Path.GetTempPath(), "fms_holearray_v13_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(v13Path,
                    "{ \"SchemaVersion\": 13, \"Tools\": [ { \"Id\": \"HA1\", \"ToolType\": \"hole_array\", " +
                    "\"HoleArray\": { \"Rows\": 2, \"Cols\": 3, \"NominalDiameterMm\": 1.0, \"MinHoleAreaPx\": 30 } } ] }");
                Recipe v13 = new RecipeStore().Load(v13Path);
                HoleArrayAnalysisParameters h13 = v13.Tools[0].HoleArray;
                if (h13 == null) throw new InvalidOperationException("v13 HoleArray null");
                AssertEqual(2, h13.Rows, "v13 Rows preserved");
                AssertClose(30, h13.MinHoleAreaPx, 1e-9, "v13 MinHoleAreaPx preserved");
                AssertClose(0.80, h13.MinCircularity, 1e-9, "v13 missing MinCircularity defaults to 0.80");
            }
            finally { if (File.Exists(v13Path)) File.Delete(v13Path); }

            // Validator：合法 hole_array → 0 error；缺 HoleArray / 缺 Roi / Rows<1 / Cols<1 / 標稱孔徑 ≤ 0 → error
            AssertEqual(0, Errors(ValidHoleArrayRecipe()), "valid hole_array no error");
            var noParams = ValidHoleArrayRecipe(); noParams.Tools[0].HoleArray = null;
            if (Errors(noParams) == 0) throw new InvalidOperationException("hole_array without HoleArray → error");
            var noRoi = ValidHoleArrayRecipe(); noRoi.Tools[0].Roi = null;
            if (Errors(noRoi) == 0) throw new InvalidOperationException("hole_array without Roi → error");
            var badRows = ValidHoleArrayRecipe(); badRows.Tools[0].HoleArray.Rows = 0;
            if (Errors(badRows) == 0) throw new InvalidOperationException("hole_array Rows 0 → error");
            var badCols = ValidHoleArrayRecipe(); badCols.Tools[0].HoleArray.Cols = 0;
            if (Errors(badCols) == 0) throw new InvalidOperationException("hole_array Cols 0 → error");
            var badDia = ValidHoleArrayRecipe(); badDia.Tools[0].HoleArray.NominalDiameterMm = 0.0;
            if (Errors(badDia) == 0) throw new InvalidOperationException("hole_array NominalDiameterMm 0 → error");
            var badTol = ValidHoleArrayRecipe(); badTol.Tools[0].HoleArray.PositionToleranceMm = -0.1;
            if (Errors(badTol) == 0) throw new InvalidOperationException("hole_array negative tolerance → error");
        }

        private static Recipe ValidHoleArrayRecipe()
        {
            var r = Recipe.Default();
            r.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "HA1", Name = "孔陣列", ToolType = "hole_array",
                    Roi = new RoiGeometry { CenterRow = 500, CenterCol = 700, Length1 = 250, Length2 = 180, AngleRad = 0.0 },
                    HoleArray = new HoleArrayAnalysisParameters { Rows = 3, Cols = 4,
                        NominalDiameterMm = 1.2, DiameterToleranceMm = 0.06,
                        NominalPitchXMm = 2.5, NominalPitchYMm = 2.0,
                        PitchToleranceMm = 0.08, PositionToleranceMm = 0.09 } }
            };
            return r;
        }
        private static int Errors(Recipe r)
        {
            int n = 0; foreach (RecipeIssue i in RecipeValidator.Validate(r, 1600, 1600)) if (i.Severity == RecipeIssueSeverity.Error) n++; return n;
        }
        private static void AssertEqual<T>(T e, T a, string n)
        { if (!object.Equals(e, a)) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
        private static void AssertClose(double e, double a, double t, string n)
        { if (Math.Abs(e - a) > t) throw new InvalidOperationException(n + " expected " + e + " got " + a); }
    }
}
