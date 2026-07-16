# FlashMeasurementSystem — Roadmap（canonical 儀表板）

> 2026-07-01 起，本檔為前瞻路線圖與決策的 canonical 來源（取代已退役的 `docs/ROADMAP_待辦與決策.md`，以及移除的 `.planning/` GSD 目錄）。
> 完整需求/研究/Phase 2 執行細節的逐檔備份見 `docs/superpowers/specs/2026-06-30-gsd-phase1-2-design.md`。
> 進度已改為**手動維護**（不再有 `/gsd-*` 指令推進）。

## 現況基線（已交付並確認）

一鍵式閃測儀已具備完整 pass/fail 量測管線：影像品質 → 模板匹配 → 次像素邊緣 → line/circle/ellipse/rectangle/arc 擬合 → 距離/角度 → 幾何構建 → GD&T 形位公差 → 容差 OK/NG → overlay，含 CSV 報表與 2 點等向 pixel→mm 校正。

已完成里程碑：M0–M4 核心管線、A3 幾何基元擴充（含互動弧形卡尺）、A5 幾何構建（交線/對稱中線/點到線投影）、GD&T v1（真圓度/真直度/平行/垂直/同心，單基準單邊）、N1 配方驗證、Deep-audit P0/P1/P2 修正、UI overlay 殘留修正。

## 前向 Phases

| # | Phase | 目標 | 狀態 |
|---|-------|------|------|
| 1 | Operator Experience | 空狀態引導、PASS/FAIL 橫幅、公差上下限即時顯示、編輯器內試測 | ✅ 完成 2026-06-30 |
| 2 | **2D Metrology Model** | 標稱幾何上自動佈點的量測矩形 + 穩健一鍵多特徵量測（主流差異化） | ✅ 完成 2026-07-01（GUI 驗收通過，fix d08bc33） |
| 3 | Production Robustness | fuzzy/robust 邊緣量測（B1）+ GR&R/重複性自測（B2） | ⬜ 未開始 |
| 4 | PDF Reporting | 格式化 PDF 量測報表（超越 CSV） | ⬜ 未開始 |
| 5 | Application Solutions Library | 命名量測方案（齒輪齒數/齒距、PCD、直徑、pin 間距）+ DXF/CAD 輪廓比對，疊在既有基元上 | 🔄 進行中：**DXF/CAD 輪廓度比對 v1+v1.1**（2026-07-14，merge 5b67bae）+ **弧形卡尺工具進配方 v1**（2026-07-16，merge 8a34cb5；schema v7、圓周邊數計數、共用基礎建設**解鎖 gear/PCD/孔陣列**）+ **齒輪齒數/齒距/齒寬方案**（2026-07-16，merge 7585741；schema v8、`ToolType="gear"`、純 Domain 分析器、三判定 CSV、齒中心/缺齒 overlay；GUI 驗收修正 HALCON 弧掃描 handedness 極性 + 缺齒提示定位）已交付；PCD/孔陣列/pin 間距/直徑未開始，皆可重用弧形 ROI 基礎建設 |

Phase 2 已驗收並 push（`origin/main`）。驗收期補了 6 個「純 2D 量測模型配方一等公民 + on-image 標籤可讀性」修正（d08bc33）。Designer 重生 landmine 已還原 HEAD、未帶進版控（⚠️用 VS 開專案會再自動重生，commit 前檢查 `git status`）。**剩餘非阻擋測試強化**：①旋轉工件 GUI 複驗對齊（commit 6713fcc 的剛體變換對齊）；②補「對齊路徑」自動測試（現有 Wave 2 測試全 `hasReferencePose=false`，從未測有旋轉/姿態的對齊，是先前對齊 bug 5ce575b/6713fcc 的根因所在＝驗證洞）。詳見備份附錄與 memory `phase2-2d-metrology-plan`。

**Phase 5「DXF/CAD 輪廓比對」**（把量到的輪廓疊到 CAD 標準輪廓找偏差/缺料/毛邊）：
- **像素空間可做（無硬體）**：`read_contour_xld_dxf` 讀入標稱輪廓 → `find_shape_model`/仿射對位 → `dist_contour_xld` 類算點到輪廓偏差 → 偏差圖/超差標記。演算法可用合成/replay 影像全驗。
- **mm 絕對準度待硬體**：要輸出真實 mm 偏差需相機 + 校正片校正；無硬體時只能做像素/相對比對。
- 淵源：Phase 2 曾評估「Option B — CAD/DXF master import」當標稱來源，當時鎖定 Option A（標稱參數）並**延後** Option B（見 `docs/superpowers/specs/2026-06-30-gsd-phase1-2-design.md` Locked Decisions）；本項即該延後想法在應用庫層的落地。

## 關鍵決策（延後/否決，避免反覆爭論）

| 決策 | 理由 | 日期 | 結果 |
|------|------|------|------|
| 延後所有 A1 校正（完整 + 各向異性半校正）至相機+標準件到位 | 無硬體＝無真值可校，現在做是空轉 | 2026-06-27 | Pending |
| GD&T 位置/對稱/方向 + 完整基準框 → v2 | 需完整基準框 + CMM benchmarking；無標準件時誤差風險最高 | 2026-06-27 | Pending |
| 延後切線構建 | 非任何 GD&T 公差前置；無現行零件需要 | 2026-06-27 | Pending |
| 真直度真值（peak-to-peak 垂直帶）→ v2 | v1 用 ResidualRms 近似（UI/報表標「approx」）；升級＝在 HalconLineFitter 加 max−min 帶 | 2026-06-27 | Pending |
| 量測方案庫在基元完成後才建 | 基元＝積木，現已完成，方案庫解鎖 | 2026-06-26 | Pending |
| 撤回配方建立 Wizard，改用空狀態引導（N3） | 對單一操作員過度設計 | 2026-06-25 | Withdrawn |
| 不做完整 WPF 遷移 / 深色模式 / 完整在地化 | 在量測核心價值之外，高風險低回報 | 2026-06-25 | Rejected |
| 不做 BackgroundWorker 執行緒 / 完整 MainWindow 拆分 | 次秒級操作不值執行緒風險；拆分低價值高風險 | 2026-06-25 | Rejected |
| 1D + 2D 量測共存（非取代） | 避免打破既有已驗證管線 | 2026-06-30 | Pending |

## Deferred & Blocked（達成解鎖條件時再升為 phase）

| 能力 | 阻擋原因 |
|------|---------|
| A1 完整相機校正 + 畸變 + 世界平面 | 硬體：caltab + 相機 |
| A1 各向異性 X/Y 半校正 | 硬體：兩個正交標準件（無它們無真值） |
| B4 Z 軸自動對焦 | 硬體：Z 軸馬達 |
| MES 整合骨架 | 外部：客戶 MES 協定規格 |
| CMM benchmarking / 計量準度驗證 | 硬體：標準件 + 相機 + CMM |
| GD&T 位置/對稱/方向 + 完整基準框 | 決策：需 CMM benchmarking，延至 v2 |
| 完整真實影像 HALCON 適配器單元測試 | 硬體：真實成像（合成子集可較早做） |

## 關鍵背景約束

- **目前無相機 / 無校正片(caltab) / 無標準件 / 無 Z 軸** → 凡需「真實計量準度」的項目皆無法驗收，只能做「演算法可全驗（replay/合成影像）」的軟體項。
- 技術棧固定：.NET Framework 4.8 / WinForms / HALCON 17.12（不遷 WPF）；嚴格單向分層，Domain 保持 HALCON-free（由 `AGENTS.md` 檢查清單強制）。
- 能力差距總表：`docs/superpowers/plans/2026-06-25_現況到主流量測儀_能力差距清單.md`。
