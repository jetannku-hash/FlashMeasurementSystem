using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.Overlay
{
    /// <summary>
    /// 標籤框（座標空間由呼叫端決定；overlay 用「視窗像素」，碰撞結果才不隨縮放改變）。
    /// Top/Left 為左上角，文字往右下延伸（同 disp_text 錨點語意）。
    /// </summary>
    public class LabelBox
    {
        public double Top;
        public double Left;
        public double Width;
        public double Height;
        /// <summary>固定障礙物（如結果表 HUD）：排版時永不移動，但其他標籤必須避開它。</summary>
        public bool Fixed;
    }

    /// <summary>
    /// 標籤防碰撞排版純計算：貪婪法，依輸入順序逐一放置，與「已放定」的框重疊時
    /// 往下跳到擋住它的框正下方，迭代至無重疊。特性：
    /// - 第一個標籤永不移動；同錨點的多個標籤依序往下堆疊（可預期、不閃跳）。
    /// - 只動 Top 不動 Left：標籤水平位置保持在錨點旁，操作員仍看得出歸屬。
    /// - 輸入順序固定 → 輸出固定，pan/zoom 重繪不會重新洗牌。
    /// </summary>
    public static class LabelLayoutMath
    {
        /// <summary>迭代上限（防呆；正常配方十幾個標籤遠用不到）。</summary>
        private const int MaxPasses = 100;

        /// <summary>
        /// 就地調整 boxes 的 Top 使兩兩不重疊（含 margin 間距）。
        /// Fixed=true 的框（HUD 等固定障礙物）永不移動；可動框須避開「所有 Fixed 框」
        /// 與「排在自己前面的可動框」。每次跳躍 Top 嚴格遞增，保證收斂。
        /// </summary>
        public static void PlaceWithoutOverlap(IList<LabelBox> boxes, double margin)
        {
            if (boxes == null) return;
            for (int i = 0; i < boxes.Count; i++)
            {
                LabelBox b = boxes[i];
                if (b == null || b.Fixed) continue;
                bool moved = true;
                int guard = 0;
                while (moved && guard++ < MaxPasses)
                {
                    moved = false;
                    for (int j = 0; j < boxes.Count; j++)
                    {
                        if (j == i) continue;
                        LabelBox p = boxes[j];
                        if (p == null || (!p.Fixed && j >= i)) continue;   // 只避 Fixed 框與前面的可動框
                        if (!Intersects(b, p, margin)) continue;
                        b.Top = p.Top + p.Height + margin;   // 跳到擋住它的框正下方
                        moved = true;
                    }
                }
            }
        }

        /// <summary>
        /// 標籤最終位置偏離期望位置超過 factor 倍字高時，應畫引出線回幾何錨點，
        /// 否則操作員看不出被擠開的標籤屬於哪個特徵。
        /// </summary>
        public static bool NeedsLeader(double desiredTop, double finalTop, double height, double factor)
        {
            return System.Math.Abs(finalTop - desiredTop) > factor * height;
        }

        /// <summary>兩框是否重疊（邊距小於 margin 也視為重疊）。</summary>
        public static bool Intersects(LabelBox a, LabelBox b, double margin)
        {
            return a.Left < b.Left + b.Width + margin
                && b.Left < a.Left + a.Width + margin
                && a.Top < b.Top + b.Height + margin
                && b.Top < a.Top + a.Height + margin;
        }
    }
}
