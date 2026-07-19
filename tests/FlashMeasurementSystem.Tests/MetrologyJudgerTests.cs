using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.MetrologyModel;
using FlashMeasurementSystem.Domain.Tolerance;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// MetrologyJudger（方案B：每形狀判自然多量，px）判定測試。
    /// </summary>
    public static class MetrologyJudgerTests
    {
        public static void Run()
        {
            // ── 橢圓：兩量皆有公差、皆在範圍內 → 全 OK ──
            {
                var def = new MetrologyObjectDef
                {
                    Shape = MetrologyObjectType.Ellipse,
                    Tolerances = new List<MetrologyItemTolerance>
                    {
                        new MetrologyItemTolerance { Quantity = "major_axis", Spec = new ToleranceSpec { Nominal = 200, LowerTolerance = -2, UpperTolerance = 2 } },
                        new MetrologyItemTolerance { Quantity = "minor_axis", Spec = new ToleranceSpec { Nominal = 120, LowerTolerance = -2, UpperTolerance = 2 } },
                    }
                };
                var result = new MetrologyObjectResult { Shape = MetrologyObjectType.Ellipse, Success = true, FitRadius1 = 100, FitRadius2 = 60 };

                List<MetrologyJudgment> judgments = MetrologyJudger.Judge(def, result);

                Assert(judgments.Count == 2, "Ellipse OK: 2 judgments");
                Assert(result.IsOk == true, "Ellipse OK: result.IsOk true");
                MetrologyJudgment major = FindByKey(judgments, "major_axis");
                MetrologyJudgment minor = FindByKey(judgments, "minor_axis");
                Assert(major != null && major.MeasuredValue == 200.0, "Ellipse OK: major measured 200");
                Assert(minor != null && minor.MeasuredValue == 120.0, "Ellipse OK: minor measured 120");
                Assert(major.IsOk, "Ellipse OK: major judgment OK");
                Assert(minor.IsOk, "Ellipse OK: minor judgment OK");
            }

            // ── 橢圓：major 超出公差 → 該判定 false，result.IsOk false ──
            {
                var def = new MetrologyObjectDef
                {
                    Shape = MetrologyObjectType.Ellipse,
                    Tolerances = new List<MetrologyItemTolerance>
                    {
                        new MetrologyItemTolerance { Quantity = "major_axis", Spec = new ToleranceSpec { Nominal = 200, LowerTolerance = -1, UpperTolerance = 1 } },
                    }
                };
                var result = new MetrologyObjectResult { Shape = MetrologyObjectType.Ellipse, Success = true, FitRadius1 = 102, FitRadius2 = 60 };

                List<MetrologyJudgment> judgments = MetrologyJudger.Judge(def, result);

                Assert(judgments.Count == 1, "Ellipse NG: 1 judgment (only major has tolerance)");
                MetrologyJudgment major = judgments[0];
                Assert(major.MeasuredValue == 204.0, "Ellipse NG: major measured 204");
                Assert(major.IsOk == false, "Ellipse NG: major judgment false");
                Assert(result.IsOk == false, "Ellipse NG: result.IsOk false");
            }

            // ── 橢圓：無公差 → 不判定 ──
            {
                var def = new MetrologyObjectDef { Shape = MetrologyObjectType.Ellipse, Tolerances = new List<MetrologyItemTolerance>() };
                var result = new MetrologyObjectResult { Shape = MetrologyObjectType.Ellipse, Success = true, FitRadius1 = 100, FitRadius2 = 60 };

                List<MetrologyJudgment> judgments = MetrologyJudger.Judge(def, result);

                Assert(judgments.Count == 0, "Ellipse no-tolerance: 0 judgments");
                Assert(result.IsOk == null, "Ellipse no-tolerance: result.IsOk null");
            }

            // ── 圓：diameter 公差，OK ──
            {
                var def = new MetrologyObjectDef
                {
                    Shape = MetrologyObjectType.Circle,
                    Tolerances = new List<MetrologyItemTolerance>
                    {
                        new MetrologyItemTolerance { Quantity = "diameter", Spec = new ToleranceSpec { Nominal = 100, LowerTolerance = -1, UpperTolerance = 1 } },
                    }
                };
                var result = new MetrologyObjectResult { Shape = MetrologyObjectType.Circle, Success = true, FitRadius = 50 };

                List<MetrologyJudgment> judgments = MetrologyJudger.Judge(def, result);

                Assert(judgments.Count == 1, "Circle OK: 1 judgment");
                Assert(judgments[0].MeasuredValue == 100.0, "Circle OK: diameter measured 100");
                Assert(judgments[0].IsOk, "Circle OK: judgment OK");
                Assert(result.IsOk == true, "Circle OK: result.IsOk true");
            }

            // ── Success == false → 不判定 ──
            {
                var def = new MetrologyObjectDef
                {
                    Shape = MetrologyObjectType.Circle,
                    Tolerances = new List<MetrologyItemTolerance>
                    {
                        new MetrologyItemTolerance { Quantity = "diameter", Spec = new ToleranceSpec { Nominal = 100, LowerTolerance = -1, UpperTolerance = 1 } },
                    }
                };
                var result = new MetrologyObjectResult { Shape = MetrologyObjectType.Circle, Success = false, FitRadius = 50 };

                List<MetrologyJudgment> judgments = MetrologyJudger.Judge(def, result);

                Assert(judgments.Count == 0, "Success false: 0 judgments");
                Assert(result.IsOk == null, "Success false: result.IsOk null");
            }
        }

        private static MetrologyJudgment FindByKey(List<MetrologyJudgment> judgments, string key)
        {
            foreach (MetrologyJudgment j in judgments)
                if (j.Quantity == key) return j;
            return null;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
