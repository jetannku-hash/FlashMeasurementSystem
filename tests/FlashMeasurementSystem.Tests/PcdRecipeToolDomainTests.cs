using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.PcdAnalysis;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    public static class PcdRecipeToolDomainTests
    {
        public static void Run()
        {
            AssertEqual(16, Recipe.Default().SchemaVersion, "SchemaVersion is 16");
            AssertEqual(null, new MeasurementTool().Pcd, "Default Pcd is null");

            var recipe = Recipe.Default();
            recipe.Tools = new List<MeasurementTool>
            {
                new MeasurementTool
                {
                    Id = "P1", Name = "螺栓圈", ToolType = "pcd",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 500, CenterCol = 500, Radius = 250,
                        AngleStart = 0, AngleExtent = 2 * Math.PI, AnnulusRadius = 30 },
                    Pcd = new PcdAnalysisParameters { NominalHoleCount = 6, NominalPcdMm = 5.0,
                        PcdToleranceMm = 0.1, AngularToleranceDeg = 1.0, RadialToleranceMm = 0.05,
                        HoleIsDark = true, MinHoleAreaPx = 25 }
                }
            };
            string path = Path.Combine(Path.GetTempPath(), "fms_pcd_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                new RecipeStore().Save(recipe, path);
                Recipe rt = new RecipeStore().Load(path);
                PcdAnalysisParameters g = rt.Tools[0].Pcd;
                if (g == null) throw new InvalidOperationException("round-trip Pcd null");
                AssertEqual(6, g.NominalHoleCount, "rt NominalHoleCount");
                AssertClose(5.0, g.NominalPcdMm, 1e-9, "rt NominalPcdMm");
                AssertClose(0.1, g.PcdToleranceMm, 1e-9, "rt PcdTol");
                AssertClose(0.05, g.RadialToleranceMm, 1e-9, "rt RadialTol");
                AssertEqual(true, g.HoleIsDark, "rt HoleIsDark");
                if (rt.Tools[0].ArcRoi == null) throw new InvalidOperationException("rt ArcRoi null");
                AssertClose(250, rt.Tools[0].ArcRoi.Radius, 1e-9, "rt ArcRoi radius");
            }
            finally { if (File.Exists(path)) File.Delete(path); }

            // 向後相容：舊 JSON 無 Pcd → null
            string oldPath = Path.Combine(Path.GetTempPath(), "fms_pcd_old_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(oldPath, "{ \"SchemaVersion\": 8, \"Tools\": [ { \"Id\": \"C1\", \"ToolType\": \"circle\" } ] }");
                Recipe old = new RecipeStore().Load(oldPath);
                AssertEqual(null, old.Tools[0].Pcd, "old Pcd null");
            }
            finally { if (File.Exists(oldPath)) File.Delete(oldPath); }

            // Validator：合法 pcd → 0 error；缺 Pcd / 缺 ArcRoi / 標稱孔數 0 → error
            AssertEqual(0, Errors(ValidPcdRecipe()), "valid pcd no error");
            var noPcd = ValidPcdRecipe(); noPcd.Tools[0].Pcd = null;
            if (Errors(noPcd) == 0) throw new InvalidOperationException("pcd without Pcd → error");
            var noArc = ValidPcdRecipe(); noArc.Tools[0].ArcRoi = null;
            if (Errors(noArc) == 0) throw new InvalidOperationException("pcd without ArcRoi → error");
            var badN = ValidPcdRecipe(); badN.Tools[0].Pcd.NominalHoleCount = 0;
            if (Errors(badN) == 0) throw new InvalidOperationException("pcd NominalHoleCount 0 → error");
        }

        private static Recipe ValidPcdRecipe()
        {
            var r = Recipe.Default();
            r.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "P1", Name = "螺栓圈", ToolType = "pcd",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 500, CenterCol = 500, Radius = 250,
                        AngleStart = 0, AngleExtent = 2 * Math.PI, AnnulusRadius = 30 },
                    Pcd = new PcdAnalysisParameters { NominalHoleCount = 6, NominalPcdMm = 5.0,
                        PcdToleranceMm = 0.1, AngularToleranceDeg = 1.0, RadialToleranceMm = 0.05 } }
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
