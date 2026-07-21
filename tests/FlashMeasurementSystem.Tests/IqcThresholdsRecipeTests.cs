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
    /// 每配方影像品質門檻（schema v15）：回退規則與驗證規則。
    ///
    /// 這組測試守的是「舊配方行為不變」與「門檻設反要被擋下」兩件事。前者是加欄位的相容性承諾，
    /// 後者是因為門檻設反的症狀（每張影像都判過亮/過暗）看起來像影像有問題，不像設定有問題。
    /// </summary>
    public static class IqcThresholdsRecipeTests
    {
        private const int W = 640, H = 480;

        public static void Run()
        {
            FallbackWhenNotSet();
            UsesRecipeValueWhenSet();
            DefaultsMatchLegacyGlobalValues();
            ValidatesThresholds();
            RoundTripsThroughStore();
            LegacyFileLoadsWithoutThresholds();

            Console.WriteLine("IqcThresholdsRecipeTests passed");
        }

        // 未設定（舊 .zcp 載入後即為此狀態）→ 回退全域預設，行為與 v15 之前一致。
        private static void FallbackWhenNotSet()
        {
            var recipe = new Recipe();
            Assert(recipe.IqcThresholds == null, "new recipe should not pin thresholds");

            ImageQualityThresholds eff = recipe.EffectiveIqcThresholds();
            ImageQualityThresholds def = ImageQualityThresholds.Default();

            Assert(eff != null, "EffectiveIqcThresholds must never return null");
            AssertEq(eff.MinBrightness, def.MinBrightness, "fallback MinBrightness");
            AssertEq(eff.MaxBrightness, def.MaxBrightness, "fallback MaxBrightness");
            AssertEq(eff.MaxSaturationRatio, def.MaxSaturationRatio, "fallback MaxSaturationRatio");
            AssertEq(eff.MinBlurScore, def.MinBlurScore, "fallback MinBlurScore");
            AssertEq(eff.MinContrast, def.MinContrast, "fallback MinContrast");
        }

        // 有設定 → 用配方的值，不被預設蓋掉。
        private static void UsesRecipeValueWhenSet()
        {
            var recipe = new Recipe
            {
                IqcThresholds = new ImageQualityThresholds
                {
                    MinBrightness = 10.0,
                    MaxBrightness = 250.0,
                    MaxSaturationRatio = 5.0,
                    MinBlurScore = 5.0,
                    MinContrast = 1.0
                }
            };

            ImageQualityThresholds eff = recipe.EffectiveIqcThresholds();
            AssertEq(eff.MaxBrightness, 250.0, "recipe MaxBrightness wins");
            AssertEq(eff.MinBlurScore, 5.0, "recipe MinBlurScore wins");

            // 回傳的必須是配方自己那份，否則呼叫端改動不會反映、也可能誤改全域預設。
            Assert(ReferenceEquals(eff, recipe.IqcThresholds), "should return the recipe's own instance");
        }

        // 全域預設值即 v15 之前寫死在流程裡的那組；若有人改動預設，這裡會先亮。
        private static void DefaultsMatchLegacyGlobalValues()
        {
            ImageQualityThresholds d = ImageQualityThresholds.Default();
            AssertEq(d.MinBrightness, 80.0, "legacy MinBrightness");
            AssertEq(d.MaxBrightness, 180.0, "legacy MaxBrightness");
            AssertEq(d.MaxSaturationRatio, 1.0, "legacy MaxSaturationRatio");
            AssertEq(d.MinBlurScore, 100.0, "legacy MinBlurScore");
            AssertEq(d.MinContrast, 20.0, "legacy MinContrast");
        }

        private static void ValidatesThresholds()
        {
            // 未設門檻的有效配方 → 不因 IQC 產生任何問題。
            var ok = ValidRecipe();
            AssertNoIqcIssue(RecipeValidator.Validate(ok, W, H), "thresholds not set → no IQC issue");

            // 合理門檻 → 不產生問題。
            var sane = ValidRecipe();
            sane.IqcThresholds = new ImageQualityThresholds
            {
                MinBrightness = 60.0, MaxBrightness = 220.0,
                MaxSaturationRatio = 2.0, MinBlurScore = 10.0, MinContrast = 5.0
            };
            AssertNoIqcIssue(RecipeValidator.Validate(sane, W, H), "sane thresholds → no IQC issue");

            // 亮度上下限設反 → Error（否則每張影像都會不合格，且訊息看不出是設定錯）。
            var inverted = ValidRecipe();
            inverted.IqcThresholds = new ImageQualityThresholds { MinBrightness = 200.0, MaxBrightness = 100.0 };
            AssertHasError(RecipeValidator.Validate(inverted, W, H), "inverted brightness range → error");

            // 相等亦視為設反：合格區間為空。
            var equal = ValidRecipe();
            equal.IqcThresholds = new ImageQualityThresholds { MinBrightness = 120.0, MaxBrightness = 120.0 };
            AssertHasError(RecipeValidator.Validate(equal, W, H), "empty brightness range → error");

            // 飽和比例超出 0–100% → Error。
            var sat = ValidRecipe();
            sat.IqcThresholds = new ImageQualityThresholds { MaxSaturationRatio = 150.0 };
            AssertHasError(RecipeValidator.Validate(sat, W, H), "saturation ratio > 100 → error");

            // 負的銳利度下限 → Error。
            var blur = ValidRecipe();
            blur.IqcThresholds = new ImageQualityThresholds { MinBlurScore = -1.0 };
            AssertHasError(RecipeValidator.Validate(blur, W, H), "negative blur score → error");

            // 負的對比度下限 → Error。
            var contrast = ValidRecipe();
            contrast.IqcThresholds = new ImageQualityThresholds { MinContrast = -0.5 };
            AssertHasError(RecipeValidator.Validate(contrast, W, H), "negative contrast → error");

            // 亮度超出 8-bit 範圍 → Warning（可疑但不必然錯，例如非 8-bit 影像）。
            var range = ValidRecipe();
            range.IqcThresholds = new ImageQualityThresholds { MinBrightness = -5.0, MaxBrightness = 300.0 };
            AssertHasWarning(RecipeValidator.Validate(range, W, H), "brightness outside 0-255 → warning");
        }

        // 門檻必須真的寫進 .zcp 並讀得回來。這是最容易「看起來有做、實際沒存」的一環：
        // UI 上設定成功、存檔後重開卻悄悄變回預設，而且沒有任何錯誤訊息。
        private static void RoundTripsThroughStore()
        {
            var src = ValidRecipe();
            src.IqcThresholds = new ImageQualityThresholds
            {
                MinBrightness = 30.5,
                MaxBrightness = 240.25,
                MaxSaturationRatio = 3.5,
                MinBlurScore = 12.5,
                MinContrast = 4.25
            };

            IRecipeStore store = new RecipeStore();
            string path = Path.Combine(Path.GetTempPath(),
                "fms_iqc_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                store.Save(src, path);
                Recipe rt = store.Load(path);

                Assert(rt.IqcThresholds != null, "round-trip must preserve thresholds");
                AssertEq(rt.IqcThresholds.MinBrightness, 30.5, "round-trip MinBrightness");
                AssertEq(rt.IqcThresholds.MaxBrightness, 240.25, "round-trip MaxBrightness");
                AssertEq(rt.IqcThresholds.MaxSaturationRatio, 3.5, "round-trip MaxSaturationRatio");
                AssertEq(rt.IqcThresholds.MinBlurScore, 12.5, "round-trip MinBlurScore");
                AssertEq(rt.IqcThresholds.MinContrast, 4.25, "round-trip MinContrast");

                // 讀回來的配方走同一條回退邏輯時，仍應拿到自訂值而非預設。
                AssertEq(rt.EffectiveIqcThresholds().MaxBrightness, 240.25, "round-trip effective value");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // v15 之前存的檔沒有 IqcThresholds 欄位 → 載入後為 null → 回退全域預設，行為不變。
        // 這是本次加欄位的相容性承諾，必須被測到。
        private static void LegacyFileLoadsWithoutThresholds()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "fms_iqc_legacy_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(path,
                    "{ \"SchemaVersion\": 14, \"Name\": \"legacy\", \"Tools\": [ " +
                    "{ \"Id\": \"C1\", \"ToolType\": \"circle\" } ] }");

                IRecipeStore store = new RecipeStore();
                Recipe legacy = store.Load(path);

                Assert(legacy.IqcThresholds == null, "legacy file must not pin thresholds");
                AssertEq(legacy.EffectiveIqcThresholds().MaxBrightness,
                    ImageQualityThresholds.Default().MaxBrightness, "legacy falls back to global default");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
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

        private static bool IsIqcIssue(RecipeIssue i)
        {
            return i.Message != null && i.Message.Contains("影像品質門檻");
        }

        private static void AssertNoIqcIssue(List<RecipeIssue> issues, string message)
        {
            foreach (RecipeIssue i in issues)
            {
                if (IsIqcIssue(i))
                    throw new InvalidOperationException(
                        "IqcThresholds " + message + ": unexpected issue '" + i.Message + "'");
            }
        }

        private static void AssertHasError(List<RecipeIssue> issues, string message)
        {
            foreach (RecipeIssue i in issues)
                if (i.Severity == RecipeIssueSeverity.Error && IsIqcIssue(i)) return;
            throw new InvalidOperationException("IqcThresholds " + message + ": expected an IQC Error");
        }

        private static void AssertHasWarning(List<RecipeIssue> issues, string message)
        {
            foreach (RecipeIssue i in issues)
                if (i.Severity == RecipeIssueSeverity.Warning && IsIqcIssue(i)) return;
            throw new InvalidOperationException("IqcThresholds " + message + ": expected an IQC Warning");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("IqcThresholds " + message);
        }

        private static void AssertEq(double actual, double expected, string message)
        {
            if (Math.Abs(actual - expected) > 1e-9)
            {
                throw new InvalidOperationException(string.Format(
                    "IqcThresholds {0}: expected {1}, actual {2}", message, expected, actual));
            }
        }
    }
}
