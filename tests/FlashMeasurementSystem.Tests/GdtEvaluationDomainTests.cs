using System;
using FlashMeasurementSystem.Domain.Gdt;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>GdtEvaluation 單邊判定測試（spec §8.2 全案例）。</summary>
    public static class GdtEvaluationDomainTests
    {
        public static void Run()
        {
            // dev < T → OK，未接近邊界
            GdtJudgment ok = GdtEvaluation.Evaluate(0.5, 1.0);
            AssertTrue(ok.IsOk, "dev<T should be OK");
            AssertTrue(!ok.NearBoundary, "dev=0.5,T=1 margin 50% not near");

            // dev > T → NG
            GdtJudgment ng = GdtEvaluation.Evaluate(1.5, 1.0);
            AssertTrue(!ng.IsOk, "dev>T should be NG");

            // dev == T → 邊界 OK 且接近上限
            GdtJudgment edge = GdtEvaluation.Evaluate(1.0, 1.0);
            AssertTrue(edge.IsOk, "dev==T should be OK (boundary)");
            AssertTrue(edge.NearBoundary, "dev==T should flag near upper boundary");

            // dev == 0 → 完美 OK，且**不**警示（無接近下限概念）
            GdtJudgment perfect = GdtEvaluation.Evaluate(0.0, 1.0);
            AssertTrue(perfect.IsOk, "dev==0 should be OK");
            AssertTrue(!perfect.NearBoundary, "dev==0 should NOT flag near boundary (single-sided)");
            AssertClose(100.0, perfect.MarginPercent, "dev==0 margin 100%");

            // 接近上限 → 警示（餘量 5% < 20%）
            GdtJudgment near = GdtEvaluation.Evaluate(0.95, 1.0);
            AssertTrue(near.IsOk, "dev=0.95 OK");
            AssertTrue(near.NearBoundary, "dev=0.95 near upper boundary");

            // 負偏差 → 夾為 0，OK，餘量 100%
            GdtJudgment neg = GdtEvaluation.Evaluate(-0.3, 1.0);
            AssertTrue(neg.IsOk, "negative dev clamped, OK");
            AssertClose(0.0, neg.Deviation, "negative dev clamped to 0");
            AssertClose(100.0, neg.MarginPercent, "clamped dev margin 100%");

            // NaN / Infinity → NG
            AssertTrue(!GdtEvaluation.Evaluate(double.NaN, 1.0).IsOk, "NaN dev -> NG");
            AssertTrue(!GdtEvaluation.Evaluate(double.PositiveInfinity, 1.0).IsOk, "Inf dev -> NG");

            // 無效公差帶寬 T<=0 → NG
            AssertTrue(!GdtEvaluation.Evaluate(0.5, 0.0).IsOk, "T=0 -> NG");
            AssertTrue(!GdtEvaluation.Evaluate(0.5, -1.0).IsOk, "T<0 -> NG");
        }

        private static void AssertTrue(bool cond, string message)
        {
            if (!cond) throw new InvalidOperationException("GdtEvaluation: " + message);
        }

        private static void AssertClose(double expected, double actual, string message)
        {
            if (double.IsNaN(actual) || Math.Abs(expected - actual) > 1e-9)
            {
                throw new InvalidOperationException(string.Format(
                    "GdtEvaluation {0}: expected {1}, actual {2}", message, expected, actual));
            }
        }
    }
}
