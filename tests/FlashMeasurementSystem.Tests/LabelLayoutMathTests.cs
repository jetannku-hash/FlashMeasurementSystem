using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.Overlay;

namespace FlashMeasurementSystem.Tests
{
    // 標籤防碰撞排版純測試（console-style；assert 以丟例外表示失敗）。
    public static class LabelLayoutMathTests
    {
        public static void Run()
        {
            TestNonOverlappingUnchanged();
            TestSameAnchorStacksDownward();
            TestThreeSameAnchorStackInOrder();
            TestPartialOverlapPushedBelow();
            TestChainPush();
            TestHorizontallySeparatedUntouched();
            TestFixedObstacleNeverMovesAndIsAvoided();
            TestLabelBeforeFixedObstacleStillAvoidsIt();
            TestNeedsLeaderThreshold();
            Console.WriteLine("LabelLayoutMathTests passed");
        }

        private static LabelBox Box(double top, double left, double w = 100, double h = 20)
        {
            return new LabelBox { Top = top, Left = left, Width = w, Height = h };
        }

        private static void AssertNoOverlaps(IList<LabelBox> boxes, double margin, string label)
        {
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    if (LabelLayoutMath.Intersects(boxes[i], boxes[j], margin))
                        throw new InvalidOperationException(label + $"：box {i} 與 {j} 仍重疊");
        }

        private static void TestNonOverlappingUnchanged()
        {
            var boxes = new List<LabelBox> { Box(0, 0), Box(100, 0), Box(0, 300) };
            LabelLayoutMath.PlaceWithoutOverlap(boxes, 3.0);
            Near(boxes[0].Top, 0, "unchanged: box0");
            Near(boxes[1].Top, 100, "unchanged: box1");
            Near(boxes[2].Top, 0, "unchanged: box2");
        }

        // 同錨點兩標籤：第一個不動，第二個排到第一個正下方（含 margin）。
        private static void TestSameAnchorStacksDownward()
        {
            var boxes = new List<LabelBox> { Box(50, 50), Box(50, 50) };
            LabelLayoutMath.PlaceWithoutOverlap(boxes, 3.0);
            Near(boxes[0].Top, 50, "stack2: 第一個不動");
            Near(boxes[1].Top, 50 + 20 + 3, "stack2: 第二個在正下方");
            AssertNoOverlaps(boxes, 3.0, "stack2");
        }

        private static void TestThreeSameAnchorStackInOrder()
        {
            var boxes = new List<LabelBox> { Box(50, 50), Box(50, 50), Box(50, 50) };
            LabelLayoutMath.PlaceWithoutOverlap(boxes, 3.0);
            Near(boxes[0].Top, 50, "stack3: 第一個不動");
            Near(boxes[1].Top, 73, "stack3: 第二個");
            Near(boxes[2].Top, 96, "stack3: 第三個");
            AssertNoOverlaps(boxes, 3.0, "stack3");
        }

        // 部分重疊（第二個略低於第一個但仍相交）→ 推到第一個正下方。
        private static void TestPartialOverlapPushedBelow()
        {
            var boxes = new List<LabelBox> { Box(0, 0), Box(10, 40) };
            LabelLayoutMath.PlaceWithoutOverlap(boxes, 3.0);
            Near(boxes[0].Top, 0, "partial: 第一個不動");
            Near(boxes[1].Top, 23, "partial: 推到下方");
            AssertNoOverlaps(boxes, 3.0, "partial");
        }

        // 連鎖：第三個原位與第一個重疊，下移後又撞到已下移的第二個 → 再往下。
        private static void TestChainPush()
        {
            var boxes = new List<LabelBox> { Box(0, 0), Box(0, 0), Box(5, 10) };
            LabelLayoutMath.PlaceWithoutOverlap(boxes, 3.0);
            AssertNoOverlaps(boxes, 3.0, "chain");
            AssertTrue(boxes[2].Top >= boxes[1].Top + 20 + 3 - 1e-9, "chain: 第三個在第二個下方");
        }

        // 水平間距足夠（含 margin）的標籤不互相影響。
        private static void TestHorizontallySeparatedUntouched()
        {
            var boxes = new List<LabelBox> { Box(0, 0, 100, 20), Box(0, 104, 100, 20) };
            LabelLayoutMath.PlaceWithoutOverlap(boxes, 3.0);
            Near(boxes[1].Top, 0, "hsep: 不動");
        }

        // 固定障礙物（HUD）：自己不動，與其重疊的可動標籤被推到其下方。
        private static void TestFixedObstacleNeverMovesAndIsAvoided()
        {
            var hud = Box(6, 6, 300, 200); hud.Fixed = true;
            var boxes = new List<LabelBox> { hud, Box(50, 50) };
            LabelLayoutMath.PlaceWithoutOverlap(boxes, 3.0);
            Near(hud.Top, 6, "obstacle: HUD 不動");
            Near(boxes[1].Top, 6 + 200 + 3, "obstacle: 標籤推到 HUD 下方");
            AssertNoOverlaps(boxes, 3.0, "obstacle");
        }

        // 障礙物排在清單後面也一樣要被避開（可動框須避「所有」Fixed 框，與順序無關）。
        private static void TestLabelBeforeFixedObstacleStillAvoidsIt()
        {
            var hud = Box(6, 6, 300, 200); hud.Fixed = true;
            var boxes = new List<LabelBox> { Box(50, 50), hud };
            LabelLayoutMath.PlaceWithoutOverlap(boxes, 3.0);
            Near(hud.Top, 6, "obstacle-order: HUD 不動");
            Near(boxes[0].Top, 209, "obstacle-order: 標籤仍避開 HUD");
            AssertNoOverlaps(boxes, 3.0, "obstacle-order");
        }

        private static void TestNeedsLeaderThreshold()
        {
            AssertTrue(!LabelLayoutMath.NeedsLeader(100, 100, 20, 1.5), "leader: 未位移不畫");
            AssertTrue(!LabelLayoutMath.NeedsLeader(100, 128, 20, 1.5), "leader: 位移 28 ≤ 30 不畫");
            AssertTrue(LabelLayoutMath.NeedsLeader(100, 131, 20, 1.5), "leader: 位移 31 > 30 要畫");
        }

        private static void Near(double actual, double expected, string label)
        {
            if (Math.Abs(actual - expected) > 1e-9)
                throw new InvalidOperationException(label + $"：expected {expected}, got {actual}");
        }

        private static void AssertTrue(bool cond, string label)
        {
            if (!cond) throw new InvalidOperationException(label);
        }
    }
}
