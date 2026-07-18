using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.Tolerance;

namespace FlashMeasurementSystem.Domain.MetrologyModel
{
    /// <summary>
    /// 由擬合結果 + 物件公差算判定（純 Domain）。方案B：每形狀判自然多量（px）。
    /// 只判「有設公差」的量；設 result.IsOk = 各判定量 AND（無任何公差→null）。
    /// </summary>
    public static class MetrologyJudger
    {
        public struct JudgedQuantity { public string Key; public string Label; public double Value; }

        // 各形狀的判定量（key/label/值，值由擬合結果取，px）。
        public static List<JudgedQuantity> QuantitiesOf(MetrologyObjectResult r)
        {
            var list = new List<JudgedQuantity>();
            if (r == null) return list;
            switch (r.Shape)
            {
                case MetrologyObjectType.Circle:
                    list.Add(new JudgedQuantity { Key = "diameter", Label = "直徑", Value = 2.0 * r.FitRadius });
                    break;
                case MetrologyObjectType.Ellipse:
                    list.Add(new JudgedQuantity { Key = "major_axis", Label = "長軸", Value = 2.0 * r.FitRadius1 });
                    list.Add(new JudgedQuantity { Key = "minor_axis", Label = "短軸", Value = 2.0 * r.FitRadius2 });
                    break;
                case MetrologyObjectType.Rectangle:
                    list.Add(new JudgedQuantity { Key = "side1", Label = "長邊", Value = 2.0 * r.FitLength1 });
                    list.Add(new JudgedQuantity { Key = "side2", Label = "短邊", Value = 2.0 * r.FitLength2 });
                    break;
                case MetrologyObjectType.Line:
                    double dr = r.FitRowEnd - r.FitRowBegin, dc = r.FitColumnEnd - r.FitColumnBegin;
                    list.Add(new JudgedQuantity { Key = "length", Label = "長度", Value = Math.Sqrt(dr * dr + dc * dc) });
                    break;
            }
            return list;
        }

        public static List<MetrologyJudgment> Judge(MetrologyObjectDef def, MetrologyObjectResult result)
        {
            var judgments = new List<MetrologyJudgment>();
            if (result == null) return judgments;
            if (def == null || !result.Success) { result.IsOk = null; return judgments; }

            var tolByKey = new Dictionary<string, ToleranceSpec>();
            if (def.Tolerances != null)
                foreach (MetrologyItemTolerance t in def.Tolerances)
                    if (t != null && t.Spec != null && !string.IsNullOrEmpty(t.Quantity))
                        tolByKey[t.Quantity] = t.Spec;

            bool anyJudged = false, allOk = true;
            foreach (JudgedQuantity q in QuantitiesOf(result))
            {
                ToleranceSpec spec;
                if (!tolByKey.TryGetValue(q.Key, out spec) || spec == null) continue;
                double lo = spec.Nominal + spec.LowerTolerance;
                double hi = spec.Nominal + spec.UpperTolerance;
                bool ok = q.Value >= lo && q.Value <= hi;
                anyJudged = true; allOk = allOk && ok;
                judgments.Add(new MetrologyJudgment
                {
                    Quantity = q.Key, Label = q.Label, MeasuredValue = q.Value,
                    Nominal = spec.Nominal, LowerLimit = lo, UpperLimit = hi,
                    Unit = spec.Unit ?? "", IsOk = ok
                });
            }
            result.IsOk = anyJudged ? (bool?)allOk : null;
            return judgments;
        }
    }
}
