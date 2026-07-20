using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Domain.PinPitchAnalysis;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    public static class PinPitchRecipeToolDomainTests
    {
        public static void Run()
        {
            AssertEqual(14, Recipe.Default().SchemaVersion, "SchemaVersion is 14");
            AssertEqual(null, new MeasurementTool().PinPitch, "Default PinPitch is null");

            var recipe = Recipe.Default();
            recipe.Tools = new List<MeasurementTool>
            {
                new MeasurementTool
                {
                    Id = "PP1", Name = "引腳排", ToolType = "pin_pitch",
                    Roi = new RoiGeometry { CenterRow = 400, CenterCol = 600, Length1 = 300, Length2 = 40, AngleRad = 0.1 },
                    PinPitch = new PinPitchAnalysisParameters { NominalPinCount = 8, NominalPitchMm = 0.5,
                        PitchToleranceMm = 0.05, UniformityToleranceMm = 0.02, PinIsDark = true, MinPinAreaPx = 30 }
                }
            };
            string path = Path.Combine(Path.GetTempPath(), "fms_pinpitch_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                new RecipeStore().Save(recipe, path);
                Recipe rt = new RecipeStore().Load(path);
                PinPitchAnalysisParameters g = rt.Tools[0].PinPitch;
                if (g == null) throw new InvalidOperationException("round-trip PinPitch null");
                AssertEqual(8, g.NominalPinCount, "rt NominalPinCount");
                AssertClose(0.5, g.NominalPitchMm, 1e-9, "rt NominalPitchMm");
                AssertClose(0.05, g.PitchToleranceMm, 1e-9, "rt PitchTol");
                AssertClose(0.02, g.UniformityToleranceMm, 1e-9, "rt UniformityTol");
                AssertEqual(true, g.PinIsDark, "rt PinIsDark");
                AssertClose(30, g.MinPinAreaPx, 1e-9, "rt MinPinAreaPx");
                RoiGeometry roi = rt.Tools[0].Roi;
                if (roi == null) throw new InvalidOperationException("rt Roi null");
                AssertClose(400, roi.CenterRow, 1e-9, "rt Roi CenterRow");
                AssertClose(600, roi.CenterCol, 1e-9, "rt Roi CenterCol");
                AssertClose(300, roi.Length1, 1e-9, "rt Roi Length1");
                AssertClose(40, roi.Length2, 1e-9, "rt Roi Length2");
                AssertClose(0.1, roi.AngleRad, 1e-9, "rt Roi AngleRad");
            }
            finally { if (File.Exists(path)) File.Delete(path); }

            // 向後相容：舊 JSON 無 PinPitch → null
            string oldPath = Path.Combine(Path.GetTempPath(), "fms_pinpitch_old_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(oldPath, "{ \"SchemaVersion\": 9, \"Tools\": [ { \"Id\": \"C1\", \"ToolType\": \"circle\" } ] }");
                Recipe old = new RecipeStore().Load(oldPath);
                AssertEqual(null, old.Tools[0].PinPitch, "old PinPitch null");
            }
            finally { if (File.Exists(oldPath)) File.Delete(oldPath); }

            // Validator：合法 pin_pitch → 0 error；缺 PinPitch / 缺 Roi / 標稱間距 ≤ 0 → error
            AssertEqual(0, Errors(ValidPinPitchRecipe()), "valid pin_pitch no error");
            var noParams = ValidPinPitchRecipe(); noParams.Tools[0].PinPitch = null;
            if (Errors(noParams) == 0) throw new InvalidOperationException("pin_pitch without PinPitch → error");
            var noRoi = ValidPinPitchRecipe(); noRoi.Tools[0].Roi = null;
            if (Errors(noRoi) == 0) throw new InvalidOperationException("pin_pitch without Roi → error");
            var badPitch = ValidPinPitchRecipe(); badPitch.Tools[0].PinPitch.NominalPitchMm = 0.0;
            if (Errors(badPitch) == 0) throw new InvalidOperationException("pin_pitch NominalPitchMm 0 → error");
        }

        private static Recipe ValidPinPitchRecipe()
        {
            var r = Recipe.Default();
            r.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "PP1", Name = "引腳排", ToolType = "pin_pitch",
                    Roi = new RoiGeometry { CenterRow = 400, CenterCol = 600, Length1 = 300, Length2 = 40, AngleRad = 0.0 },
                    PinPitch = new PinPitchAnalysisParameters { NominalPinCount = 8, NominalPitchMm = 0.5,
                        PitchToleranceMm = 0.05, UniformityToleranceMm = 0.02 } }
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
