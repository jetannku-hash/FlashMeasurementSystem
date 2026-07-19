using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Domain.MetrologyModel;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// MET2D-01：量測物件 DTO 預設值 + 各 Shape 最少量測區數（2/3/5/8）。
    /// MET2D-03：Recipe 加性 MetrologyModel 欄向後相容（舊 .zcp 無此欄載入為 null）
    ///           + 帶 MetrologyModel 的配方 Save/Load 往返保值。
    /// </summary>
    public static class MetrologyModelDomainTests
    {
        public static void Run()
        {
            // ── MET2D-01：DTO 預設值 ──
            var def = new MetrologyObjectDef();
            Assert(def.MeasureLength1 == 20.0, "MeasureLength1 default 20");
            Assert(def.MeasureLength2 == 5.0, "MeasureLength2 default 5");
            Assert(def.MeasureDistance == 10.0, "MeasureDistance default 10");
            Assert(def.MeasureThreshold == 30.0, "MeasureThreshold default 30");
            Assert(def.MeasureSigma == 1.0, "MeasureSigma default 1");
            Assert(def.NumMeasures == 0, "NumMeasures default 0");
            Assert(def.Tolerance == null, "Tolerance default null");
            Assert(def.Shape == MetrologyObjectType.Line, "Shape default Line");

            // ── MET2D-01：各 Shape 最少量測區數 ──
            Assert(MetrologyObjectDef.MinMeasureRegions(MetrologyObjectType.Line) == 2, "Line min regions 2");
            Assert(MetrologyObjectDef.MinMeasureRegions(MetrologyObjectType.Circle) == 3, "Circle min regions 3");
            Assert(MetrologyObjectDef.MinMeasureRegions(MetrologyObjectType.Ellipse) == 5, "Ellipse min regions 5");
            Assert(MetrologyObjectDef.MinMeasureRegions(MetrologyObjectType.Rectangle) == 8, "Rectangle min regions 8");

            // ── MET2D-03：Recipe 預設（向後相容基線） ──
            Recipe d = Recipe.Default();
            Assert(d.MetrologyModel == null, "Recipe.Default MetrologyModel null");
            Assert(d.SchemaVersion == 12, "Recipe.Default SchemaVersion 12");

            // ── MET2D-03：舊 v5 配方（無 MetrologyModel 欄）載入不丟例外、欄位為 null ──
            string oldPath = TempZcp("old");
            try
            {
                File.WriteAllText(oldPath, "{\"SchemaVersion\":5,\"RecipeId\":\"old\",\"Name\":\"legacy\",\"Tools\":[]}");
                Recipe loaded = new RecipeStore().Load(oldPath);
                Assert(loaded != null, "legacy recipe loads");
                Assert(loaded.MetrologyModel == null, "legacy recipe MetrologyModel null");
                Assert(loaded.RecipeId == "old", "legacy recipe RecipeId preserved");
            }
            finally { TryDelete(oldPath); }

            // ── MET2D-03：帶 MetrologyModel 的配方 Save/Load 往返保值 ──
            string rtPath = TempZcp("rt");
            try
            {
                var recipe = new Recipe
                {
                    RecipeId = "rt",
                    MetrologyModel = new MetrologyModelDef
                    {
                        ImageWidth = 640,
                        ImageHeight = 480,
                        Objects = new List<MetrologyObjectDef>
                        {
                            new MetrologyObjectDef
                            {
                                Id = "c1", Name = "circle1", Shape = MetrologyObjectType.Circle,
                                Row = 100.5, Column = 200.25, Radius = 50.75,
                                MeasureLength1 = 18.0, MeasureDistance = 7.5, NumMeasures = 0
                            },
                            new MetrologyObjectDef
                            {
                                Id = "l1", Name = "line1", Shape = MetrologyObjectType.Line,
                                RowBegin = 10.0, ColumnBegin = 20.0, RowEnd = 10.0, ColumnEnd = 300.0,
                                MeasureLength2 = 6.5
                            }
                        }
                    }
                };

                var store = new RecipeStore();
                store.Save(recipe, rtPath);
                Recipe back = store.Load(rtPath);

                Assert(back.MetrologyModel != null, "round-trip MetrologyModel not null");
                Assert(back.MetrologyModel.Objects.Count == 2, "round-trip object count 2");
                Assert(back.MetrologyModel.ImageWidth == 640, "round-trip ImageWidth");
                Assert(back.MetrologyModel.ImageHeight == 480, "round-trip ImageHeight");

                MetrologyObjectDef c = back.MetrologyModel.Objects[0];
                Assert(c.Shape == MetrologyObjectType.Circle, "round-trip circle shape");
                Assert(c.Row == 100.5 && c.Column == 200.25 && c.Radius == 50.75, "round-trip circle geometry");
                Assert(c.MeasureLength1 == 18.0 && c.MeasureDistance == 7.5, "round-trip circle measure params");

                MetrologyObjectDef l = back.MetrologyModel.Objects[1];
                Assert(l.Shape == MetrologyObjectType.Line, "round-trip line shape");
                Assert(l.RowBegin == 10.0 && l.ColumnBegin == 20.0 && l.RowEnd == 10.0 && l.ColumnEnd == 300.0,
                    "round-trip line geometry");
                Assert(l.MeasureLength2 == 6.5, "round-trip line measure param");
            }
            finally { TryDelete(rtPath); }
        }

        private static string TempZcp(string tag)
        {
            return Path.Combine(Path.GetTempPath(),
                "fms_met_" + tag + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort cleanup */ }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("MetrologyModelDomainTests: " + message);
        }
    }
}
