# ⚠️ DEPRECATED — 本檔已退役（2026-06-30）

> **canonical 已改為 `.planning/ROADMAP.md`（GSD，納管 git）。**
> 本檔的 §5 延後決策理由 + §6 里程碑已併入 `.planning/PROJECT.md` 的 Key Decisions / Completed Milestones。
> 保留僅供歷史對照，**請勿更新**；新規劃一律走 `.planning/`。確認無誤後可刪除本檔。

---

# ROADMAP — 待辦、決策與工作紀錄（單一儀表板）

> 最後更新：2026-06-29
> 用途：全專案待辦/卡關/延後決策的**單一索引**。本檔只記「狀態 + 解鎖條件 + 連結」，細節在各 spec/plan/memory，不在此重抄。
> 維護：每次任務開工/完工、或討論後做出延後決策時，更新對應列與「決策紀錄」。
> 相關總表：能力差距清單 `docs/superpowers/plans/2026-06-25_現況到主流量測儀_能力差距清單.md`（A1–A5 / B1–B4 全貌）。

---

## 0. 現況基準線

等級 = **1D 卡尺 + XLD 線/圓/橢圓/矩形/弧擬合 + 樣板定位 + 幾何構造**。
主要缺口集中在：校正精度、2D 量測模型、形位公差、量產穩健性、應用級量測方案。
**硬體現況：尚無相機、無自動對焦 Z 軸、無校正片、無標準件 → 凡需「真實計量準度」的項目皆無法驗收。**

---

## 1. 進行中 / 下一步

| 項目 | 狀態 | 連結 | 備註 |
|------|------|------|------|
| （無進行中項目） | — | — | GD&T v1、N1 配方驗證皆已完成並 merge（見 §6）。 |
| **下一步（建議，待拍板）** | 未開始 | [[gui-optimization-plan-2026-06-25]] | 熱身：GUI **N3 空狀態引導 + N2 PASS/FAIL 橫幅**（各 ~1hr、零風險、立刻見效）→ 主攻：**A2 2D 量測模型**（§2，主流量測儀分水嶺）。 |

---

## 2. 可立即做（不卡硬體，依價值排序）

| 項目 | 來源/連結 | 規模 | 備註 |
|------|-----------|------|------|
| **A2 2D Metrology Model** | 能力差距清單 A2 | ~10–16 天 | 主流量測儀分水嶺；可在 replay 影像驗。較大，需設計 GUI 佈設與既有 recipe 整合。 |
| **應用級量測方案庫**（齒輪數/節距、PCD、針距、直徑、角度…） | [[measurement-solutions-direction]]、`plans/2026-06-26_應用落地量測方案庫_建議文件.md` | 分批 | 把現有基元包成「選任務→框特徵→出值+PASS/FAIL+報表」。齒輪分析為首個範本。**使用者決定：基元做完才動。** |
| **GUI 優化 backlog（剩 11 項）** | [[gui-optimization-plan-2026-06-25]]、`plans/GUI建議優化項目計畫書.md` | 各 0.5–4 hr | **N1 配方驗證 ✅ 已完成（見 §6）**。剩：N3 空狀態引導、N2 PASS/FAIL 橫幅、N5 公差上下限即時顯示、A1 編輯器內試測、N6 快捷鍵/拖放、A3 即時邊緣預覽…起手建議 N3+N2→N5。 |
| **B2 GR&R / 重複性自助工具** | 能力差距清單 B2、手冊 §6.3 | ~3–5 天 | 純統計，現在即可跑、可 demo 系統能力。 |
| **B1 fuzzy / robust 邊緣量測** | 能力差距清單 B1 | ~3–5 天 | 抗雜訊/反光，量產穩健度。與現有 measure_pos 並存。 |
| **B3 PDF 報表**（MES 另計，見 §4） | 能力差距清單 B3 | ~3–5 天 | 現只有 CSV。純整合，不卡硬體。 |
| **HALCON adapter 真實影像單元測試** | [[m4-post-completion-gap-analysis]] | ~4–6 天 | 需 HALCON 授權執行；目前 adapter 僅 GUI 手動驗。**部分卡「真實影像」**（合成影像可先做一部分）。 |
| **UI overlay 審查殘留項**（次要 UI 顯示缺陷） | [[ui-overlay-audit-2026-06-27]] | ~~各 ~0.5–2 hr~~ ✅ 已修 | 2026-06-27 三代理審查共 13 項：主要 8 項於 `feature/gdt-tolerance-v1` 修復；殘留 5 項（H2/M2/L1/L2/Tab）於 6/28 `fix/ui-overlay-residuals` 修復並 merge 回 main。**全部完成。** |

### UI overlay 審查殘留項（細目，2026-06-27）
> ✅ **全部已修並 merge**（6/28，branch `fix/ui-overlay-residuals`，commit `395f55d`→merge `32961b8`）。以下保留為歷史記錄。詳見 [[ui-overlay-audit-2026-06-27]]。

| 項 | 內容 | 狀態 |
|----|------|------|
| **H2 殘留** | RunMatching/DrawMeasurement/DrawAngle/DrawContour overlay producer 接管前結束把手（5 處；ShowFittingOverlay 保留） | ✅ |
| **M2（設計）** | roiModeCheck 畫完後同步取消勾選 | ✅ |
| **L1** | "Model saved" 改走 persistent overlay | ✅ |
| **L2（ownership）** | RecipeEditor._editorOwnsEdit 追蹤編輯持有權 | ✅ |
| **Tab 切換** | FeatureTabControl_SelectedIndexChanged 清除把手/ROI draw | ✅ |

---

## 3. 卡硬體（標明解鎖條件）

| 項目 | 解鎖條件 | 來源 |
|------|----------|------|
| **A1 完整相機校正 + 鏡頭畸變 + world-plane** | 校正片（caltab）+ 相機 | 能力差距清單 A1 |
| **A1 校正半套（非等向 X/Y）** | 相機 + 兩正交標準件（**無相機則無真值可校**，做了也驗不出意義） | 能力差距清單 A1、本檔決策 §5 |
| **B4 自動對焦（Z 軸）** | Z 軸馬達 | 能力差距清單 B4 |
| **真實計量準度 / 對標 CMM** | 標準件 + 相機 + CMM | 全專案橫向需求 |
| **adapter 真實影像測試（完整版）** | 真實取像 | [[m4-post-completion-gap-analysis]] |

---

## 4. 卡外部規格

| 項目 | 解鎖條件 | 來源 |
|------|----------|------|
| **B3 MES 整合 skeleton** | 使用者 MES 的通訊協議規格 | 能力差距清單 B3、[[m4-post-completion-gap-analysis]]（`Mes` 專案目前僅 AssemblyInfo） |

---

## 5. 已決議延後（含「為什麼」）

> 這區是討論後刻意往後排的決策紀錄，避免下次重新爭論。

- **GD&T 位置度 Position / 對稱度 / 傾斜度** → v2。理由：位置度需完整 datum reference frame（原點+主次基準定向），是 GD&T 最易算錯、最需對標 CMM 的部分；**本機無標準件可對標**，先做風險高。（2026-06-27）
- **完整 datum frame（A+B+C 多基準定向）** → 隨位置度一起 v2。v1 只做單一基準 A，已足以撐平行/垂直/同心。（2026-06-27）
- **切線 tangent 構造** → defer，無期程。理由：非任何 GD&T 公差前置，目前無具體零件需要量切線距離；不為填清單而做。（2026-06-27）
- **真直度真值（峰對峰垂距帶）** → v2。v1 用 `ResidualRms`（RMS 近似，UI/報表標「近似」）。升級只需在 `HalconLineFitter` 補 max−min 垂距。理由：使用者無相機/校正片，先求演算法上線。（2026-06-27）
- **A1 校正全段（含半套）** → 全押後到相機+標準件就位、一次做一次驗收。理由：無硬體則無真值可校，現在做是空轉。（2026-06-27）
- **量測方案庫** → 等所有量測基元做完再逐個實作。理由：基元是方案的積木，先補齊積木。（2026-06-26，[[measurement-solutions-direction]]）
- **配方建立 Wizard（原 GUI 構想）** → 撤回，改用 N3 空狀態引導。理由：對單一操作員過度設計。（2026-06-25，[[gui-optimization-plan-2026-06-25]]）
- **全面 WPF 遷移 / Dark mode / 完全中文化** → 不做。理由：超出量測核心價值，高風險低回報。（2026-06-25）
- **BackgroundWorker 多執行緒化 / 全面 MainWindow 拆分** → 不做。理由：子秒級操作不值得執行緒風險；拆分低值高險。（2026-06-25，[[deep-audit-2026-06-25]]）

---

## 6. 已完成（里程碑，細節見連結）

| 里程碑 | 連結 |
|--------|------|
| M0–M4 核心（影像品質→樣板→邊緣→線/圓擬合→距離/角度→公差→標註→一鍵流程） | [[session_checkpoint_2026-06-22]] |
| A3 幾何基元擴充（橢圓/矩形/弧/點，含弧形卡尺互動編輯） | [[arc-caliper-usability-improvements]] |
| A5 幾何構造（intersection/midline/projection），併入 main | [[a5-geometry-construction]] |
| **GD&T 形位公差 v1**（真圓度/真直度/平行/垂直/同心，單邊判定），併入 main `375c5c2` | [[gdt-tolerance-v1]] |
| **N1 配方驗證**（執行前診斷：Error 阻擋/Warning 詢問），併入 main `8abaa99`（`--no-ff`） | [[gui-optimization-plan-2026-06-25]] |
| Deep audit P0/P1/P2 修復 | [[deep-audit-2026-06-25]] |
| GUI 審查（前一輪 8/12 已修） | [[gui-review-2026-06-24]] |
| UI overlay 殘留 13 項全修（merge `32961b8`） | [[ui-overlay-audit-2026-06-27]] |

---

## 7. 維護備忘

- 新增/完成/延後 → 更新對應列 + §5 決策紀錄（附日期與理由）。
- 硬體到位時 → 從 §3/§4 把解鎖的項目搬回 §2 或 §1。
- 本檔為索引；細節一律連到 spec/plan/memory，不在此展開。
- memory 指標：`project-roadmap`（auto-memory，提醒每 session 先讀本檔）。
