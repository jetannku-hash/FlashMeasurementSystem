---
phase: 01-operator-experience
plan: 01
subsystem: App.Wpf (WinForms UI)
tags: [winforms, gui, operator-experience, empty-state, pass-fail-banner]
requires:
  - MainWindow load/run/clear flow (existing)
  - _loadedRecipe / _imageHelper.CurrentImage state (existing)
  - DrawRecipeResults okCount/ngCount tally (existing)
provides:
  - emptyStateGuideLabel (Designer Label) — 3-step empty-state guide overlay
  - resultBannerPanel + resultBannerLabel (Designer) — fixed PASS/FAIL banner
  - MainWindow.UpdateEmptyState() — guide visibility driver
  - MainWindow.SetResultBanner(int okCount, int ngCount, bool measured) — banner driver
affects:
  - src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs
  - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
tech-stack:
  added: []
  patterns:
    - WinForms Label overlay sibling-inside-cell + BringToFront (N3 Option A)
    - TableLayoutPanel fixed Absolute banner row + Percent content row (N2 Option A)
key-files:
  created: []
  modified:
    - src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs
    - src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
decisions:
  - N3 Option A (WinForms Label overlay, not HALCON WriteString) — empty HALCON window has no image to draw on.
  - N2 Option A (fixed Designer Panel at top, not HUD overlay) — always visible, unaffected by pan/zoom.
  - UpdateEmptyState uses a single ForeColor per branch (white / Gainsboro / hidden) rather than per-line coloring (out of scope).
  - SetResultBanner treats okCount==0 && ngCount==0 && measured as gray "—" (no measured tools).
metrics:
  duration: ~25 min
  completed: 2026-06-30
  tasks: 2 auto + 1 checkpoint (pending human GUI verify)
status: complete
---

# Phase 01 Plan 01: Empty-state guide + PASS/FAIL banner Summary

大型固定 PASS/FAIL 橫幅（綠 PASS / 紅 FAIL（NG n）/ 灰 —）加上影像區上的三步驟空狀態引導覆蓋，兩者皆為純 WinForms 覆蓋，無量測邏輯變更、無新依賴。

## What was built

### GUI-01 / N3 — Empty-state 3-step guide (`emptyStateGuideLabel`)
- **Designer**: 半透明深色 Label (`Color.FromArgb(200,20,20,20)`)，白字 Segoe UI 14 Bold，MiddleCenter，三步驟文字 `① 載入影像 / ② 載入或建立配方 / ③ 按一鍵量測`，加入 `mainTableLayout` 的影像 cell 並 `BringToFront()` 覆蓋在 `hWindowControl` 之上。
- **MainWindow.UpdateEmptyState()**：已載入配方 → 隱藏；有影像無配方 → 整段灰字 (`Gainsboro`)；完全空 → 白字全亮。
- **Wiring (6 sites)**：建構式末端、`LoadAndDisplayImage`、`ClearResultDisplays`、配方載入成功 / 載入失敗 / 編輯器存檔 callback。

### GUI-02 / N2 — Large PASS/FAIL banner (`resultBannerPanel` + `resultBannerLabel`)
- **Designer**：在 `mainTableLayout` 插入固定 56px 頂端列（row 0 `Absolute 56F` + row 1 `Percent 100F`），把 `hWindowControl` / `rightPanel` / `emptyStateGuideLabel` 全部下移到 row 1。`resultBannerPanel` 置於 cell (0,0) 並 `SetColumnSpan(..., 2)` 跨兩欄，內含 `resultBannerLabel`（Segoe UI 24 Bold、白字、MiddleCenter、預設 `—`）。
- **MainWindow.SetResultBanner(int okCount, int ngCount, bool measured)**：`measured && ngCount>0` → 紅 `FAIL（NG n）`；`measured && ngCount==0 && okCount>0` → 綠 `PASS`；其餘 → 灰 `—`。
- **Wiring**：`DrawRecipeResults` 末端以 `(okCount, ngCount, true)` 呼叫；`ClearResultDisplays` 以 `(0,0,false)` 重置（換圖回到灰 `—`）。一鍵量測經 `DrawRecipeResults` 自動覆蓋。

## Verification

| Check | Result |
| ----- | ------ |
| Build Any CPU | 0 warning / 0 error |
| Build x64 | 0 warning / 0 error |
| `FlashMeasurementSystem.Tests.exe` | exit 0（全部 suite passed：EdgeDetection / LineFitting / CircleFitting / GdtEvaluation / RecipeValidator / CsvReportWriter / Rect2Edit / ArcEdit / GeometryConstruction …） |
| grep `emptyStateGuideLabel` declared + instantiated + BringToFront | OK (Designer L243 BringToFront) |
| grep `UpdateEmptyState()` call sites | 7（1 def + 6 calls）≥4 |
| grep `mainTableLayout.RowCount = 2` + Absolute 56F + Percent 100F | OK (Designer L191–193) |
| grep `SetColumnSpan(resultBannerPanel, 2)` | OK (Designer L187) |
| grep `SetResultBanner(...)` from DrawRecipeResults (true) + ClearResultDisplays (false) | OK (MainWindow L468, L1447) |

GUI 視覺驗收（Task 3 checkpoint）尚未執行 — 需由使用者啟動 app 依 §N3（4 步）+ §N2（3 步）程序驗收。

## Deviations from Plan

無 — 完全依計畫 Option A（N3 + N2）實作。兩個 Designer 變更的 row-shift 協調（emptyStateGuideLabel 隨 hWindowControl 從 row 0 移到 row 1）已照計畫 NOTE 一致處理。

## Known Stubs

無。`UpdateEmptyState` 與 `SetResultBanner` 皆已接上真實狀態（`_loadedRecipe` / `_imageHelper.CurrentImage` / `okCount` / `ngCount`），非 placeholder。

## Threat Flags

無新增攻擊面。兩個控制項只顯示通用引導文字與已信任的 in-memory 量測計數，無網路 / 持久化 / 套件安裝（威脅登記表 T-01-01/02/03 全為 accept）。

## Checkpoint (Task 3) — Pending Human GUI Verify

應用程式尚未經 GUI 視覺驗收。請 build x64 後啟動
`src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`
（重新 build 前先關閉 app），執行下列驗證：

**N3（§N3 驗證操作）**
1. 無載入任何東西啟動 → 影像區上方顯示三步驟引導。
2. 載入影像 → 引導更新（整段轉灰，步驟①完成）。
3. 載入配方 → 引導完全消失。
4. （可關閉影像時）→ 引導重新出現。

**N2（§N2 驗證操作）**
1. 載入會 PASS 的配方 + 影像 → Run Recipe / 一鍵量測 → 橫幅顯示大型綠色 `PASS`。
2. 改一個工具 nominal 使其超出公差 → 再跑一次 → 橫幅顯示紅色 `FAIL（NG 1）`。
3. 載入新影像（未量測）→ 橫幅重置為灰色 `—`。

並確認：橫幅跨頂端兩欄，無論切到哪個功能分頁都可見；空狀態引導隱藏時 HALCON pan/zoom/ROI 仍正常。

## Self-Check: PASSED

- `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs` modified — FOUND
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs` modified — FOUND
- commit `5bd643b` (Task 1) — FOUND
- commit `2cb933e` (Task 2) — FOUND
