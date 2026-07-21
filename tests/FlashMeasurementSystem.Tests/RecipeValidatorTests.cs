using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Domain.Gdt;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>RecipeValidator 純邏輯診斷測試（N1 spec §驗證操作 全案例）。</summary>
    public static class RecipeValidatorTests
    {
        private const int W = 640, H = 480;

        public static void Run()
        {
            // 零工具 → 1 Error
            var empty = new Recipe();
            var r0 = RecipeValidator.Validate(empty, W, H);
            AssertCount(r0, 1, 0, "empty recipe → 1 error");

            // null 配方 → 1 Error，不丟例外
            var rn = RecipeValidator.Validate(null, W, H);
            AssertCount(rn, 1, 0, "null recipe → 1 error");

            // 正常單一 circle 工具 → 無問題
            var good = new Recipe();
            good.Tools.Add(Circle("c1", 300, 320, 60, 50));
            AssertCount(RecipeValidator.Validate(good, W, H), 0, 0, "valid circle → no issues");

            // ROI null → Error
            var roiNull = new Recipe();
            var tNull = Circle("c1", 0, 0, 0, 0);
            tNull.Roi = null;
            roiNull.Tools.Add(tNull);
            AssertHasError(RecipeValidator.Validate(roiNull, W, H), "ROI null → error");

            // ROI 半長/半寬 <= 0 → Error
            var roiZero = new Recipe();
            roiZero.Tools.Add(Circle("c1", 100, 100, 0, 50));
            AssertHasError(RecipeValidator.Validate(roiZero, W, H), "Length1<=0 → error");

            // ROI 中心超界 → Error
            var roiOut = new Recipe();
            roiOut.Tools.Add(Circle("c1", 5000, 5000, 30, 30));
            AssertHasError(RecipeValidator.Validate(roiOut, W, H), "center out of bounds → error");

            // ROI 部分超界（中心在界內，外接框越界）→ Warning（非 Error）
            var roiEdge = new Recipe();
            roiEdge.Tools.Add(Circle("c1", 5, 5, 60, 60));
            var re = RecipeValidator.Validate(roiEdge, W, H);
            AssertCount(re, 0, 1, "partial out of bounds → 1 warning, 0 error");

            // 回歸：水平 ROI（角度 0）完整位於影像內但靠近上緣 —— 舊的圓形上界會把短邊也當對角線
            // 長而誤報；軸對齊外接框不應警告。l1: row 10..110、col 10..210 全在 640x480 內。
            var roiInsideNearEdge = new Recipe();
            roiInsideNearEdge.Tools.Add(Line("l1", 60, 110, 100, 50));
            AssertCount(RecipeValidator.Validate(roiInsideNearEdge, W, H), 0, 0,
                "horizontal ROI fully inside near edge → no warning");

            // 邊界檢查在無影像 (0,0) 時略過
            AssertCount(RecipeValidator.Validate(roiOut, 0, 0), 0, 0, "no image → skip bounds check");

            // 反向公差 Upper<Lower → Error
            var revTol = new Recipe();
            var tc = Circle("c1", 300, 320, 60, 50);
            tc.Tolerance.LowerTolerance = 0.02;
            tc.Tolerance.UpperTolerance = -0.02;
            revTol.Tools.Add(tc);
            AssertHasError(RecipeValidator.Validate(revTol, W, H), "Upper<Lower → error");

            // distance 參考解析不到 → Error
            var badRef = new Recipe();
            badRef.Tools.Add(Compound("d1", "distance", "missingA", "missingB"));
            AssertHasError(RecipeValidator.Validate(badRef, W, H), "unresolved ref → error");

            // distance 引用錯型別（distance 引用 distance）→ Error
            var wrongType = new Recipe();
            wrongType.Tools.Add(Circle("c1", 100, 100, 30, 30));
            wrongType.Tools.Add(Compound("d1", "distance", "c1", "d2"));
            wrongType.Tools.Add(Compound("d2", "distance", "c1", "c1"));
            AssertHasError(RecipeValidator.Validate(wrongType, W, H), "distance ref distance → error");

            // distance 參考數不足 → Error
            var fewRef = new Recipe();
            fewRef.Tools.Add(Circle("c1", 100, 100, 30, 30));
            fewRef.Tools.Add(Compound("d1", "distance", "c1"));
            AssertHasError(RecipeValidator.Validate(fewRef, W, H), "distance <2 refs → error");

            // angle 兩 line ref → 無問題
            var angOk = new Recipe();
            angOk.Tools.Add(Line("l1", 100, 100, 80, 20));
            angOk.Tools.Add(Line("l2", 200, 200, 80, 20));
            angOk.Tools.Add(Compound("a1", "angle", "l1", "l2"));
            AssertCount(RecipeValidator.Validate(angOk, W, H), 0, 0, "angle 2 lines → ok");

            // GD&T 缺規格 → Error；ToleranceZone<=0 → Error
            var gdtNoSpec = new Recipe();
            gdtNoSpec.Tools.Add(Circle("c1", 100, 100, 30, 30));
            var rnd = Compound("g1", "roundness", "c1");
            rnd.Gdt = null;
            gdtNoSpec.Tools.Add(rnd);
            AssertHasError(RecipeValidator.Validate(gdtNoSpec, W, H), "roundness no Gdt → error");

            var gdtBadZone = new Recipe();
            gdtBadZone.Tools.Add(Circle("c1", 100, 100, 30, 30));
            var rnd2 = Compound("g1", "roundness", "c1");
            rnd2.Gdt = new GdtToleranceSpec { Characteristic = GdtCharacteristic.Roundness, ToleranceZoneMm = 0.0 };
            gdtBadZone.Tools.Add(rnd2);
            AssertHasError(RecipeValidator.Validate(gdtBadZone, W, H), "roundness zone<=0 → error");

            // roundness 參考 line（型別錯）→ Error
            var gdtWrongRef = new Recipe();
            gdtWrongRef.Tools.Add(Line("l1", 100, 100, 80, 20));
            var rnd3 = Compound("g1", "roundness", "l1");
            rnd3.Gdt = new GdtToleranceSpec { Characteristic = GdtCharacteristic.Roundness, ToleranceZoneMm = 0.05 };
            gdtWrongRef.Tools.Add(rnd3);
            AssertHasError(RecipeValidator.Validate(gdtWrongRef, W, H), "roundness ref line → error");

            // GD&T 完整正確（roundness 參考 circle）→ 無問題
            var gdtOk = new Recipe();
            gdtOk.Tools.Add(Circle("c1", 300, 320, 60, 50));
            var rndOk = Compound("g1", "roundness", "c1");
            rndOk.Gdt = new GdtToleranceSpec { Characteristic = GdtCharacteristic.Roundness, ToleranceZoneMm = 0.05 };
            gdtOk.Tools.Add(rndOk);
            AssertCount(RecipeValidator.Validate(gdtOk, W, H), 0, 0, "valid roundness → no issues");

            // audit #10：GD&T 工具改用 Gdt 判定，雙邊 Tolerance 未被消費；其反向值不該擋下有效配方。
            var gdtRevTol = new Recipe();
            gdtRevTol.Tools.Add(Circle("c1", 300, 320, 60, 50));
            var rndRev = Compound("g1", "roundness", "c1");
            rndRev.Gdt = new GdtToleranceSpec { Characteristic = GdtCharacteristic.Roundness, ToleranceZoneMm = 0.05 };
            rndRev.Tolerance.LowerTolerance = 0.02;
            rndRev.Tolerance.UpperTolerance = -0.02;   // 反向，但 roundness 不消費雙邊公差
            gdtRevTol.Tools.Add(rndRev);
            AssertCount(RecipeValidator.Validate(gdtRevTol, W, H), 0, 0, "GD&T reversed unused tolerance → no error");

            // 重複 Id → Error
            var dup = new Recipe();
            dup.Tools.Add(Circle("c1", 100, 100, 30, 30));
            dup.Tools.Add(Circle("c1", 200, 200, 30, 30));
            AssertHasError(RecipeValidator.Validate(dup, W, H), "duplicate id → error");

            // 工具參考自己 → Error
            var selfRef = new Recipe();
            selfRef.Tools.Add(Compound("a1", "angle", "a1", "a1"));
            AssertHasError(RecipeValidator.Validate(selfRef, W, H), "self reference → error");

            // 未知型別 → Warning（非 Error）
            // 同型別內重名 → Error。報表以「名稱＋型別」把結果對回工具，重名時永遠命中第一個，
            // 第二個工具會被套上第一個的公差重新判定 → 超規值可能在 CSV/PDF 上顯示 OK，
            // 而判定橫幅（用 RecipeRunner 已算好的 IsOk）是對的：畫面正確、出貨文件錯。
            var dupName = new Recipe();
            var dn1 = Circle("d1", 200, 200, 40, 40); dn1.Name = "孔徑";
            var dn2 = Circle("d2", 400, 400, 40, 40); dn2.Name = "孔徑";
            dupName.Tools.Add(dn1); dupName.Tools.Add(dn2);
            AssertHasError(RecipeValidator.Validate(dupName, W, H), "duplicate tool name → error");

            // 不同型別可以同名——報表的查詢鍵包含型別，不會混淆。
            var sameNameDiffType = new Recipe();
            var sn1 = Circle("s1", 200, 200, 40, 40); sn1.Name = "特徵";
            var sn2 = Line("s2", 400, 400, 40, 40); sn2.Name = "特徵";
            sameNameDiffType.Tools.Add(sn1); sameNameDiffType.Tools.Add(sn2);
            AssertCount(RecipeValidator.Validate(sameNameDiffType, W, H), 0, 0,
                "same name across different types → no issue");

            // 不支援的型別 → Error（原為 Warning）。該工具不會被執行，量測結果因此不完整，
            // 而未執行的工具在 OK/NG 兩邊都不計 → 整份配方顯示 PASS。一份「有工具沒跑」的配方
            // 不可能給出可信的合格判定，故直接擋下，不讓操作員在警告框按「是」放行。
            var unknown = new Recipe();
            var u = Circle("x1", 100, 100, 30, 30);
            u.ToolType = "frobnicate";
            unknown.Tools.Add(u);
            AssertCount(RecipeValidator.Validate(unknown, W, H), 1, 0, "unknown type → error");

            // 缺 ToolType 同樣是 Error：MeasurementTool.ToolType 的 C# 預設值是 "edge"，
            // 而 RecipeRunner 沒有 edge 分支。過去 "edge" 被列在 KnownTypes 中，於是任何
            // 缺 toolType 欄位的 .zcp 都能通過驗證卻完全不量測，最後顯示 PASS。
            var missingType = new Recipe();
            missingType.Tools.Add(Circle("x2", 100, 100, 30, 30));
            missingType.Tools[0].ToolType = null;
            AssertHasError(RecipeValidator.Validate(missingType, W, H), "missing tool type → error");

            var defaultType = new Recipe();
            var d = Circle("x3", 100, 100, 30, 30);
            d.ToolType = "edge";
            defaultType.Tools.Add(d);
            AssertHasError(RecipeValidator.Validate(defaultType, W, H), "phantom 'edge' type → error");

            // HasReferencePose 但姿態全 0 → Warning
            // TemplateModelId 一併給值，讓本案例只驗「姿態全 0」這一件事；v16 起
            // 「有姿態卻沒記錄模板」是另一條獨立警告，由 TemplateReferenceRecipeTests 負責。
            var noPose = new Recipe { HasReferencePose = true, TemplateModelId = "t.shm" };
            noPose.Tools.Add(Circle("c1", 300, 320, 60, 50));
            AssertCount(RecipeValidator.Validate(noPose, W, H), 0, 1, "pose enabled but zero → warning");
        }

        private static MeasurementTool Circle(string id, double r, double c, double l1, double l2)
        {
            return new MeasurementTool
            {
                Id = id, Name = id, ToolType = "circle",
                Roi = new RoiGeometry { CenterRow = r, CenterCol = c, Length1 = l1, Length2 = l2 }
            };
        }

        private static MeasurementTool Line(string id, double r, double c, double l1, double l2)
        {
            return new MeasurementTool
            {
                Id = id, Name = id, ToolType = "line",
                Roi = new RoiGeometry { CenterRow = r, CenterCol = c, Length1 = l1, Length2 = l2 }
            };
        }

        private static MeasurementTool Compound(string id, string type, params string[] refs)
        {
            return new MeasurementTool
            {
                Id = id, Name = id, ToolType = type,
                RefToolIds = new List<string>(refs)
            };
        }

        private static void AssertCount(List<RecipeIssue> issues, int errors, int warnings, string message)
        {
            int e = 0, w = 0;
            foreach (RecipeIssue i in issues)
            {
                if (i.Severity == RecipeIssueSeverity.Error) e++; else w++;
            }
            if (e != errors || w != warnings)
            {
                throw new InvalidOperationException(string.Format(
                    "RecipeValidator {0}: expected {1}E/{2}W, actual {3}E/{4}W", message, errors, warnings, e, w));
            }
        }

        private static void AssertHasError(List<RecipeIssue> issues, string message)
        {
            foreach (RecipeIssue i in issues)
                if (i.Severity == RecipeIssueSeverity.Error) return;
            throw new InvalidOperationException("RecipeValidator " + message + ": expected at least one Error");
        }
    }
}
