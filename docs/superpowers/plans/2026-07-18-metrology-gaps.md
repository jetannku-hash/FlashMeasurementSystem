# Metrology Model 缺口補齊 — 執行計畫

依價值排序，逐階段做。每階段：實作 → 建置/測試 → （UI 者）GUI 驗收 → commit。分支：`feature/metrology-gaps`（做前開）。

## 背景（調查結論）
metrology 幾何量測是好的（Line/Circle/Ellipse/Rectangle 都能擬合、跟隨姿態、存配方），但：
1. **公差判定整條沒接**：編輯器無公差欄位；runner 不讀 Tolerance、不設 IsOk（永遠 null）→ 一鍵量測 CSV 把成功的 metrology 列寫成 `MeasuredValue=0 / NG`（假 NG），overlay 永不變紅。← 最大、屬 bug。
2. 編輯器全數字輸入，無影像上點選/拖曳。
3. 編輯器內無「試測/預覽」。
4. `MinMeasureRegions`（如橢圓 5）定義了但未強制。
5. `MeasureDistance`/`NumMeasures` 同時填時 Distance 靜默勝出、無警告。

## 待你敲定的設計決定（Phase 1 前）
**公差要判「哪個量」？** ToleranceSpec 是單一 Nominal+Lower/Upper。各形狀可判量不同：
- **方案 A（單一主量，v1 最簡）**：circle→直徑、ellipse→長軸(2·R1)、rectangle→長邊(Length1)、line→長度。一個物件一個 ToleranceSpec、一列判定。缺點：橢圓只判一軸。
- **方案 B（多量、比照 pcd 四判定）**：每形狀判自然多量——ellipse: 長軸+短軸(2 列)、rectangle: 長+寬(2 列)、circle: 直徑(1)、line: 長度(1)。完整、但 schema/UI/判定較多。

建議 **B**（橢圓/矩形本來就該判兩軸；且 pcd 已有「一工具多判定列」前例可重用）。你選 A 或 B。

---

## Phase 1 — 公差判定端到端（核心 bug 修復）
**目標**：metrology 物件能設公差、判 OK/NG、CSV 出真值與判定、overlay 依判定上色。
- 1a. Domain：依選定方案定「判定量」；`MetrologyObjectDef` 加公差欄（方案 A：一個 ToleranceSpec；方案 B：每量一個，或一個 list）。純加欄、schema bump、向後相容。
- 1b. 判定：新增純 Domain judger（或在 runner 後）由擬合結果算判定量 → 對公差判 IsOk。`MetrologyObjectResult` 帶 判定量測值/Nominal/上下限/IsOk。
- 1c. `RecipeRunner.MapToToolRunResult`：把 IsOk + 判定明細帶進 `ToolRunResult`。
- 1d. `MeasurementWorkflow`：加 metrology 分支（ToolType 前綴 `metrology_`），直接由 result 發 `ItemJudgment`（不走 FindTool，比照 pcd/gear），修掉 CSV 假 NG；工具計一次 OK/NG。
- 1e. 編輯器 UI：加公差欄位（Nominal/Lower/Upper/Unit；方案 B 則每判定量一組），標示「本形狀判定量」。
- 1f. 測試：Domain 判定測試（有效/超差/邊界）。
- **驗收**：合成橢圓跑一鍵量測 → CSV 出真長短軸值 + 正確 OK/NG；超差時 overlay 變紅。

## Phase 2 — 便宜護欄（快速勝）
- 2a. 強制 `MinMeasureRegions`：編輯器 `UpdateWarning` + 驗證，若設定的 Distance/NumMeasures 會佈少於該形狀最少區數 → 事前警告/擋。
- 2b. `MeasureDistance` 與 `NumMeasures` 同時非 0 → 編輯器警告「NumMeasures 會被忽略」。

## Phase 3 — 編輯器內即時試測/預覽
- 在 `MetrologyModelEditorForm` 加「試測」：用目前模型在已載入影像上跑一次、把擬合 overlay 畫在主視窗（比照 RecipeEditor 的「在此試測」委派），免去 Save→Run→重開的來回。需要編輯器（modal）與主視窗共用影像/overlay 的接線。

## Phase 4 — 影像上互動繪製（最大，最後做）
- 讓使用者在影像上點/拉出標稱形狀（圓心、半徑、角度、橢圓兩軸）＋可視化量測區，取代純數字輸入。需 `HWindowControlHelper` 互動（比照 ROI/arc 編輯），且要處理 modal 編輯器 vs 主視窗影像的互動。工作量最大、風險最高，放最後。

## 執行順序
Phase 1（核心，先做）→ Phase 2（便宜護欄）→ Phase 3（試測預覽）→ Phase 4（影像互動）。每階段獨立 commit、可分次驗收。
