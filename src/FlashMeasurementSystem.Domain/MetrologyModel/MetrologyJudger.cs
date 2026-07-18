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

        // 各形狀的「判定量」key + 顯示標籤（不需擬合值，供編輯器建公差列）。順序即顯示順序。
        public static List<KeyValuePair<string, string>> JudgedQuantityKeys(MetrologyObjectType shape)
        {
            var list = new List<KeyValuePair<string, string>>();
            switch (shape)
            {
                case MetrologyObjectType.Circle:    list.Add(new KeyValuePair<string,string>("diameter","直徑")); break;
                case MetrologyObjectType.Ellipse:   list.Add(new KeyValuePair<string,string>("major_axis","長軸"));
                                                    list.Add(new KeyValuePair<string,string>("minor_axis","短軸")); break;
                case MetrologyObjectType.Rectangle: list.Add(new KeyValuePair<string,string>("side1","長邊"));
                                                    list.Add(new KeyValuePair<string,string>("side2","短邊")); break;
                case MetrologyObjectType.Line:      list.Add(new KeyValuePair<string,string>("length","長度")); break;
            }
            return list;
        }

        // 各形狀的判定量（key/label/值，值由擬合結果取，px）。key/label 來自 JudgedQuantityKeys，避免與判定端 key 漂移。
        public static List<JudgedQuantity> QuantitiesOf(MetrologyObjectResult r)
        {
            var list = new List<JudgedQuantity>();
            if (r == null) return list;
            foreach (KeyValuePair<string, string> kv in JudgedQuantityKeys(r.Shape))
                list.Add(new JudgedQuantity { Key = kv.Key, Label = kv.Value, Value = ValueOf(r, kv.Key) });
            return list;
        }

        private static double ValueOf(MetrologyObjectResult r, string key)
        {
            switch (key)
            {
                case "diameter": return 2.0 * r.FitRadius;
                case "major_axis": return 2.0 * r.FitRadius1;
                case "minor_axis": return 2.0 * r.FitRadius2;
                case "side1": return 2.0 * r.FitLength1;
                case "side2": return 2.0 * r.FitLength2;
                case "length":
                    double dr = r.FitRowEnd - r.FitRowBegin, dc = r.FitColumnEnd - r.FitColumnBegin;
                    return Math.Sqrt(dr * dr + dc * dc);
                default: return 0.0;
            }
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
