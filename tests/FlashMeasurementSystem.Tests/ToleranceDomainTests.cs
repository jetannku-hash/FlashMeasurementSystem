using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.Tolerance;
using FlashMeasurementSystem.Domain.Tolerance;
using FlashMeasurementSystem.Infrastructure.Tolerance;

namespace FlashMeasurementSystem.Tests
{
    public static class ToleranceDomainTests
    {
        public static void Run()
        {
            // ─── DTO 預設值 ──────────────────────────────────────────
            ToleranceSpec spec = ToleranceSpec.Default();
            AssertEqual(0.0, spec.Nominal, "Default Nominal");
            AssertEqual(0.0, spec.LowerTolerance, "Default LowerTolerance");
            AssertEqual(0.0, spec.UpperTolerance, "Default UpperTolerance");
            AssertEqual("mm", spec.Unit, "Default Unit");

            ToleranceSpec spec2 = new ToleranceSpec
            {
                Nominal = 50.0,
                LowerTolerance = -0.010,
                UpperTolerance = 0.010
            };
            AssertClose(49.990, spec2.LowerLimit, 1e-9, "LowerLimit computed");
            AssertClose(50.010, spec2.UpperLimit, 1e-9, "UpperLimit computed");

            OverallJudgment emptyResult = new OverallJudgment();
            AssertEqual(false, emptyResult.AllOk, "Default OverallJudgment AllOk");
            AssertEqual(0, emptyResult.Items.Count, "Default OverallJudgment empty items");

            ItemJudgment defaultItem = new ItemJudgment();
            AssertEqual(false, defaultItem.IsOk, "Default ItemJudgment IsOk");
            AssertEqual(false, defaultItem.IsNearBoundary, "Default ItemJudgment IsNearBoundary");

            // ─── 介面契約（Fake）────────────────────────────────────
            IToleranceJudger fake = new FakeToleranceJudger();
            OverallJudgment fakeResult = fake.Judge(new List<ToleranceItemInput>());
            AssertEqual(true, fakeResult.AllOk, "Fake judger satisfies interface contract");

            // ─── 真實 ToleranceJudger 邏輯 ──────────────────────────
            IToleranceJudger judger = new ToleranceJudger();

            // null 輸入 → AllOk true、無項目
            OverallJudgment nullResult = judger.Judge(null);
            AssertEqual(true, nullResult.AllOk, "Null input is AllOk");
            AssertEqual(0, nullResult.Items.Count, "Null input no items");

            // 一組混合案例：中央 OK、接近邊界 OK、超上限 NG、低於下限 NG
            var items = new List<ToleranceItemInput>
            {
                MakeItem("T1", 50.000, 50.0, -0.010, 0.010),  // 正中央 → OK，餘量 100%
                MakeItem("T2", 50.009, 50.0, -0.010, 0.010),  // 靠上限 → OK，接近邊界
                MakeItem("T3", 50.011, 50.0, -0.010, 0.010),  // 超上限 → NG
                MakeItem("T4", 49.989, 50.0, -0.010, 0.010),  // 低於下限 → NG
            };
            OverallJudgment r = judger.Judge(items);

            AssertEqual(4, r.Items.Count, "Item count");
            AssertEqual(2, r.OkCount, "OK count");
            AssertEqual(2, r.NgCount, "NG count");
            AssertEqual(false, r.AllOk, "AllOk false when NG present");

            // T1：正中央，OK 且非接近邊界，餘量 100%
            AssertEqual(true, r.Items[0].IsOk, "T1 IsOk");
            AssertEqual(false, r.Items[0].IsNearBoundary, "T1 not near boundary");
            AssertClose(100.0, r.Items[0].MarginPercent, 1e-6, "T1 margin 100%");
            AssertClose(0.0, r.Items[0].Deviation, 1e-9, "T1 deviation 0");

            // T2：靠上限，OK 但接近邊界（餘量約 10% < 20%）
            AssertEqual(true, r.Items[1].IsOk, "T2 IsOk");
            AssertEqual(true, r.Items[1].IsNearBoundary, "T2 near boundary");
            AssertClose(10.0, r.Items[1].MarginPercent, 0.5, "T2 margin ~10%");

            // T3：超上限 → NG，餘量負值
            AssertEqual(false, r.Items[2].IsOk, "T3 NG");
            if (r.Items[2].MarginPercent >= 0.0)
                throw new InvalidOperationException("T3 margin should be negative");

            // T4：低於下限 → NG
            AssertEqual(false, r.Items[3].IsOk, "T4 NG");

            // 邊界精確值：剛好等於上下限 → OK（含邊界）
            var boundaryItems = new List<ToleranceItemInput>
            {
                MakeItem("B-low",  49.990, 50.0, -0.010, 0.010), // = 下限
                MakeItem("B-high", 50.010, 50.0, -0.010, 0.010), // = 上限
            };
            OverallJudgment br = judger.Judge(boundaryItems);
            AssertEqual(2, br.OkCount, "Boundary values are inclusive OK");
            AssertEqual(0, br.NgCount, "Boundary values no NG");

            // 零公差（tolRange = 0）不應除以零、不應 near-boundary 誤報
            var zeroTol = new List<ToleranceItemInput>
            {
                MakeItem("Z", 50.0, 50.0, 0.0, 0.0),
            };
            OverallJudgment zr = judger.Judge(zeroTol);
            AssertEqual(1, zr.OkCount, "Zero tolerance exact hit is OK");
            AssertEqual(false, zr.Items[0].IsNearBoundary, "Zero tolerance no near-boundary flag");

            // 無效實測值（NaN / Infinity）→ NG，且不可誤報「超出上限」
            var invalidItems = new List<ToleranceItemInput>
            {
                MakeItem("NaN", double.NaN, 50.0, -0.010, 0.010),
                MakeItem("PosInf", double.PositiveInfinity, 50.0, -0.010, 0.010),
                MakeItem("NegInf", double.NegativeInfinity, 50.0, -0.010, 0.010),
            };
            OverallJudgment ir = judger.Judge(invalidItems);
            AssertEqual(0, ir.OkCount, "Invalid measurements never OK");
            AssertEqual(3, ir.NgCount, "Invalid measurements all NG");
            for (int i = 0; i < ir.Items.Count; i++)
            {
                AssertEqual(false, ir.Items[i].IsOk, "Invalid item " + i + " IsOk false");
                if (ir.Items[i].Message == null || ir.Items[i].Message.IndexOf("無效", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("Invalid item " + i + " should report invalid-value message, got: " + ir.Items[i].Message);
            }

            // 反向公差規格（Upper < Lower，上下公差填反）→ NG，訊息標示規格無效，不靜默誤判
            var invertedSpec = new List<ToleranceItemInput>
            {
                MakeItem("INV", 50.0, 50.0, 0.010, -0.010), // LowerTol=+0.01, UpperTol=-0.01 → 下限>上限
            };
            OverallJudgment xr = judger.Judge(invertedSpec);
            AssertEqual(0, xr.OkCount, "Inverted spec never OK");
            AssertEqual(1, xr.NgCount, "Inverted spec is NG");
            if (xr.Items[0].Message == null || xr.Items[0].Message.IndexOf("規格無效", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Inverted spec should report invalid-spec message, got: " + xr.Items[0].Message);
        }

        private static ToleranceItemInput MakeItem(
            string id, double measured, double nominal, double lower, double upper)
        {
            return new ToleranceItemInput
            {
                ToolId = id,
                ToolName = id,
                MeasuredValue = measured,
                Spec = new ToleranceSpec
                {
                    Nominal = nominal,
                    LowerTolerance = lower,
                    UpperTolerance = upper,
                    Unit = "mm"
                }
            };
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
            }
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
            {
                throw new InvalidOperationException(
                    name + " expected " + expected + " but got " + actual + " (tol " + tol + ")");
            }
        }

        private sealed class FakeToleranceJudger : IToleranceJudger
        {
            public OverallJudgment Judge(IList<ToleranceItemInput> items)
            {
                return new OverallJudgment { AllOk = true };
            }
        }
    }
}
