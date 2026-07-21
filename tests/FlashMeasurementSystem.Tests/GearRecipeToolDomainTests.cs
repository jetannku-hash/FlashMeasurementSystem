using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.GearAnalysis;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    public static class GearRecipeToolDomainTests
    {
        public static void Run()
        {
            AssertEqual(15, Recipe.Default().SchemaVersion, "SchemaVersion is 15");
            var plain = new MeasurementTool();
            AssertEqual(null, plain.Gear, "Default Gear is null");

            var recipe = Recipe.Default();
            recipe.Tools = new List<MeasurementTool>
            {
                new MeasurementTool
                {
                    Id = "G1", Name = "齒輪", ToolType = "gear",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 500, CenterCol = 500, Radius = 200,
                        AngleStart = 0, AngleExtent = 2 * Math.PI, AnnulusRadius = 10 },
                    Gear = new GearAnalysisParameters { NominalToothCount = 20, ToothIsDark = true,
                        PitchToleranceDeg = 1.5, WidthToleranceDeg = 2.5 }
                }
            };
            string path = Path.Combine(Path.GetTempPath(), "fms_gear_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                new RecipeStore().Save(recipe, path);
                Recipe rt = new RecipeStore().Load(path);
                GearAnalysisParameters g = rt.Tools[0].Gear;
                if (g == null) throw new InvalidOperationException("round-trip Gear null");
                AssertEqual(20, g.NominalToothCount, "rt NominalToothCount");
                AssertEqual(true, g.ToothIsDark, "rt ToothIsDark");
                AssertClose(1.5, g.PitchToleranceDeg, 1e-9, "rt PitchTol");
                AssertClose(2.5, g.WidthToleranceDeg, 1e-9, "rt WidthTol");
                if (rt.Tools[0].ArcRoi == null) throw new InvalidOperationException("rt ArcRoi null");
                AssertClose(200, rt.Tools[0].ArcRoi.Radius, 1e-9, "rt ArcRoi radius");
            }
            finally { if (File.Exists(path)) File.Delete(path); }

            string oldPath = Path.Combine(Path.GetTempPath(), "fms_gear_old_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(oldPath, "{ \"SchemaVersion\": 7, \"Tools\": [ { \"Id\": \"C1\", \"ToolType\": \"circle\" } ] }");
                Recipe old = new RecipeStore().Load(oldPath);
                AssertEqual(null, old.Tools[0].Gear, "old Gear null");
            }
            finally { if (File.Exists(oldPath)) File.Delete(oldPath); }

            AssertEqual(0, Errors(ValidGearRecipe()), "valid gear no error");
            var noGear = ValidGearRecipe(); noGear.Tools[0].Gear = null;
            if (Errors(noGear) == 0) throw new InvalidOperationException("gear without Gear → error");
            var noArc = ValidGearRecipe(); noArc.Tools[0].ArcRoi = null;
            if (Errors(noArc) == 0) throw new InvalidOperationException("gear without ArcRoi → error");
            var badN = ValidGearRecipe(); badN.Tools[0].Gear.NominalToothCount = 0;
            if (Errors(badN) == 0) throw new InvalidOperationException("gear NominalToothCount 0 → error");
        }

        private static Recipe ValidGearRecipe()
        {
            var r = Recipe.Default();
            r.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "G1", Name = "齒輪", ToolType = "gear",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 500, CenterCol = 500, Radius = 200,
                        AngleStart = 0, AngleExtent = 2 * Math.PI, AnnulusRadius = 10 },
                    Gear = new GearAnalysisParameters { NominalToothCount = 20, PitchToleranceDeg = 1, WidthToleranceDeg = 2 } }
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
