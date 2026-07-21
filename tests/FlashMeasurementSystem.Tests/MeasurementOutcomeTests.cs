using System;
using FlashMeasurementSystem.Domain.Workflow;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// 「工具結果 → OK/NG」的判定規則。
    ///
    /// 這組測試存在的理由是一個真實缺陷：規則原本在 MeasurementWorkflow 與
    /// MainWindow.DrawRecipeResults 各寫一份，workflow 那份修過「量測失敗也算 NG」、UI 那份沒有，
    /// 而一鍵量測會在 workflow 之後呼叫 UI 那份並覆蓋 PASS/FAIL 大橫幅 → 工具量測失敗時
    /// 橫幅顯示綠色 PASS。橫幅是操作員唯一會看的東西，等於放行壞件。
    ///
    /// 規則移到 Domain 後才有辦法被測到；先前它住在 App.Wpf，兩個測試專案都碰不到，
    /// 所以那次漂移沒有任何測試會失敗。
    /// </summary>
    public static class MeasurementOutcomeTests
    {
        public static void Run()
        {
            JudgedResults();
            HardMeasurementFailureCountsAsNg();
            MeasuredButUnjudgedStaysNeutral();
            UnsupportedToolIsNotNg();
            OkAndNgAreMutuallyExclusive();

            Console.WriteLine("MeasurementOutcomeTests passed");
        }

        private static void JudgedResults()
        {
            Assert(MeasurementOutcome.CountsAsOk(true), "IsOk=true → OK");
            Assert(!MeasurementOutcome.CountsAsOk(false), "IsOk=false 不是 OK");
            Assert(!MeasurementOutcome.CountsAsOk(null), "IsOk=null 不是 OK");

            Assert(MeasurementOutcome.CountsAsNg(false, true, true), "IsOk=false → NG");
            Assert(!MeasurementOutcome.CountsAsNg(true, true, true), "IsOk=true 不是 NG");
        }

        // 這一條就是先前漂移掉的規則：支援該型別、但沒量到，IsOk 停在 null。
        // 沒量到就不能宣稱合格，否則 NgCount=0 → 橫幅 PASS。
        private static void HardMeasurementFailureCountsAsNg()
        {
            Assert(MeasurementOutcome.CountsAsNg(null, supported: true, measured: false),
                "支援但未量測成功 → 必須算 NG（這是假 PASS 的根源）");
            Assert(!MeasurementOutcome.CountsAsOk(null),
                "量測失敗絕不可被算成 OK");
        }

        // 「成功但不判定」：例如未設公差的元素工具或構造工具，維持三態中的「未判定」，
        // 不可被壓成 NG，否則沒設公差的配方會整份變成 FAIL。
        private static void MeasuredButUnjudgedStaysNeutral()
        {
            Assert(!MeasurementOutcome.CountsAsNg(null, supported: true, measured: true),
                "量測成功但未判定 → 既非 OK 也非 NG");
            Assert(!MeasurementOutcome.CountsAsOk(null),
                "量測成功但未判定 → 不算 OK");
        }

        // 未支援的型別根本沒跑，不該計入 NG；攔截它是 RecipeValidator 的責任。
        private static void UnsupportedToolIsNotNg()
        {
            Assert(!MeasurementOutcome.CountsAsNg(null, supported: false, measured: false),
                "未支援型別 → 不算 NG");
            Assert(!MeasurementOutcome.CountsAsOk(null),
                "未支援型別 → 不算 OK");
        }

        // 兩個計數器必須互斥，否則同一個結果會被重複計數。
        private static void OkAndNgAreMutuallyExclusive()
        {
            bool?[] states = { true, false, null };
            foreach (bool? isOk in states)
            {
                foreach (bool supported in new[] { true, false })
                {
                    foreach (bool measured in new[] { true, false })
                    {
                        bool ok = MeasurementOutcome.CountsAsOk(isOk);
                        bool ng = MeasurementOutcome.CountsAsNg(isOk, supported, measured);
                        if (ok && ng)
                        {
                            throw new InvalidOperationException(string.Format(
                                "MeasurementOutcome 同時算成 OK 與 NG：IsOk={0} Supported={1} Measured={2}",
                                isOk, supported, measured));
                        }
                    }
                }
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("MeasurementOutcome " + message);
        }
    }
}
