using System;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// 「哪些工具型別以雙邊公差判定」的共用定義。
    ///
    /// 這組測試守的是一個真實缺陷：報表的判定分支原本是 catch-all，構造工具
    /// （intersection / midline / projection）沒有可判定的純量，取值函式對它們回傳 0，
    /// 而 RecipeEditor 一律給預設公差 [0,0] —— 0 落在 [0,0] 內，於是每一次量測都在
    /// CSV/PDF 產生一列偽造的「OK（偏差 0）」，連構造失敗時也照樣寫 OK。
    ///
    /// 同一份知識原本也在 RecipeValidator 各寫一份；兩處漂移的症狀就是假 OK，
    /// 故集中到 Domain 並由本測試鎖定。
    /// </summary>
    public static class ToolTypesTests
    {
        public static void Run()
        {
            MeasurableScalarTypes();
            ConstructionToolsAreNotJudged();
            MultiJudgementToolsAreNotDoubleSided();
            UnknownAndEmptyAreNotDoubleSided();

            Console.WriteLine("ToolTypesTests passed");
        }

        // 真的量得出單一純量的型別。
        private static void MeasurableScalarTypes()
        {
            foreach (string t in new[] { "circle", "line", "distance", "angle", "arc" })
                Assert(ToolTypes.IsDoubleSidedTolerance(t), t + " 應以雙邊公差判定");
        }

        // 構造工具產出幾何、不是合格與否的量；納入判定就會變成偽造的 OK。
        private static void ConstructionToolsAreNotJudged()
        {
            foreach (string t in new[] { "intersection", "midline", "projection" })
                Assert(!ToolTypes.IsDoubleSidedTolerance(t),
                    t + " 沒有可判定的量，不可走雙邊公差（否則 0 落在預設 [0,0] → 假 OK）");
        }

        // 這些型別各有自己的多項/單邊判定，重複判定會與它們自己的結果矛盾。
        private static void MultiJudgementToolsAreNotDoubleSided()
        {
            foreach (string t in new[] { "gear", "pcd", "pin_pitch", "hole_array",
                                         "roundness", "straightness", "parallelism",
                                         "perpendicularity", "concentricity" })
                Assert(!ToolTypes.IsDoubleSidedTolerance(t), t + " 有自己的判定路徑，不走雙邊公差");
        }

        // 缺型別或未知型別不可被當成可判定——那正是「工具沒跑卻顯示 PASS」的入口。
        private static void UnknownAndEmptyAreNotDoubleSided()
        {
            Assert(!ToolTypes.IsDoubleSidedTolerance(null), "null 型別不可判定");
            Assert(!ToolTypes.IsDoubleSidedTolerance(""), "空型別不可判定");
            Assert(!ToolTypes.IsDoubleSidedTolerance("edge"),
                "edge 是幽靈型別（RecipeRunner 無此分支），不可被當成可判定");
            Assert(!ToolTypes.IsDoubleSidedTolerance("frobnicate"), "未知型別不可判定");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("ToolTypes " + message);
        }
    }
}
