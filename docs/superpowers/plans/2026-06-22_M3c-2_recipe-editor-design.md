# M3c-2 配方編輯器 UI — 設計方案

**日期**: 2026-06-22
**狀態**: 待使用者確認後開工
**分支**: 將從 main 開 `feature/m3-recipe-editor`

> 前置：circle / line / distance / angle 四種量測鏈已在 M3c-1 (B1+B2) 完成並實機驗證。目前配方須手寫 JSON，編輯器提供互動式建/編/存 `.zcp` 的能力。

---

## 一、整體架構：獨立編輯器 Form

**決策**：獨立 `RecipeEditor` Form（與 M3b `CalibrationDialog` 模式一致，程式碼建 UI，不動 Designer.cs）。

理由：
- 編輯器需要較大面板空間（工具清單 + 屬性面板 + 參考工具下拉），嵌入右側 253px 分頁太擠。
- 獨立 Form 可同時看到主影像 + 編輯器，使用者在編輯器選工具、在主畫面看 ROI 位置。
- 在 MainWindow 加一個「Edit Recipe」按鈕開啟。

---

## 二、ROI 設定方式

**決策**：A（從影像取 ROI callback）+ B（手動 NumericUpDown 為 fallback）。

### A. 從影像取 ROI（主流程）
1. 使用者在編輯器點「取 ROI from Image」按鈕。
2. 編輯器呼叫 `HWindowControlHelper.RequestRoi(callback)`。
3. 主畫面切到 ROI 繪製模式，畫完後座標經 callback 傳回編輯器，自動填回六個 NumericUpDown。
4. 既有 Edge Detection 的 `RoiSelected` 事件不受影響——只有當編輯器呼叫 `RequestRoi` 時才切到 callback slot。

### B. 手動輸入（fallback）
六個 NumericUpDown（Row / Col / Length1 / Length2 / Angle）供手動輸入數字，與既有 Edge Detection 風格一致。

---

## 三、RefToolIds 設定（distance/angle 的參考關係）

**決策**：下拉 ComboBox 選工具 ID。

- 在 distance/angle 的屬性面板中，兩個 ComboBox 動態列出「目前配方中 `ToolType == line`（或 line/circle）的工具」。
- 下拉顯示 `Name (Id)` 格式，讓使用者選。
- 防呆不需另做——B2 的 runner 層防呆（找不到/元素量測失敗）保留，作為 double safety net。

---

## 四、工具面板設計

編輯器右側屬性面板依選中工具的 `ToolType` 動態切換顯示內容：

| ToolType | 顯示屬性 |
|----------|----------|
| **circle / line** | ROI 幾何（六個 NumericUpDown + 取 ROI 按鈕）、EdgeDetectionParameters（Sigma / Threshold / Polarity / Selector / Interpolation / Mode）、ToleranceSpec（Nominal / Lower / Upper / Unit） |
| **distance** | RefToolIds（兩個 ComboBox）、ToleranceSpec。**無 ROI 欄位**。 |
| **angle** | RefToolIds（兩個 ComboBox，只列 line 型別）、ToleranceSpec。**無 ROI 欄位**。 |

**共用**：Name（TextBox）、Id（TextBox，自動生成可手改）。

---

## 五、Create / Load / Save 流程

| 操作 | 行為 |
|------|------|
| **New Recipe** | 空白配方（`SchemaVersion=3`、`HasReferencePose=false`），等於一張空工具清單 |
| **Load Recipe** | 載入 `.zcp` → 編輯器填入所有工具的屬性 |
| **Save** | 把編輯器內的狀態寫回 `_loadedRecipe` 物件 → `RecipeStore.Save` → 存回原路徑（若為 New 則跳 SaveAs 對話框） |
| **Save As** | `SaveFileDialog` → 另存新 `.zcp` |
| **Set Ref** | 現有按鈕保留，行為不變：把當前匹配姿態寫進 `_loadedRecipe` |
| **Close** | 若有未存變更 → 提示是否存檔 |

Load Recipe → 編輯 → Save → **立刻可在主畫面 Run Recipe**，不需重載。

---

## 六、UI Layout（草圖）

```
┌─ RecipeEditor ──────────────────────────┐
│ 工具列：[New][Load][Save][Save As][Set Ref]│
├────────────┬─────────────────────────────┤
│ 工具清單    │ 屬性面板（依工具型別切換）     │
│ ┌────────┐ │ ┌─────────────────────────┐ │
│ │ROI-A   │ │ │Name: [ROI-A          ] │ │
│ │circle  │ │ │Id:   [t1             ] │ │
│ │────────│ │ │Type: [circle ▼]       │ │
│ │edge_top│ │ │                         │ │
│ │line    │ │ │── ROI ──────────────── │ │
│ │────────│ │ │Row: [▬▬▬] Col: [▬▬▬] │ │
│ │dist_TB │ │ │L1 : [▬▬▬] L2 : [▬▬▬] │ │
│ │distance│ │ │Ang: [▬▬▬] rad         │ │
│ │────────│ │ │[取 ROI from Image]    │ │
│ │ang_TR  │ │ │                         │ │
│ │angle   │ │ │── Edge Params ─────────│ │
│ └────────┘ │ │Sigma: [▬▬] Thr: [▬▬] │ │
│ [＋ 新增]  │ │Polarity: [all ▼]       │ │
│ [－ 刪除]  │ │...                      │ │
│            │ │                         │ │
│            │ │── Tolerance ─────────── │ │
│            │ │Nominal: [▬▬▬▬] mm     │ │
│            │ │Lower  : [▬▬▬▬] mm     │ │
│            │ │Upper  : [▬▬▬▬] mm     │ │
│            │ └─────────────────────────┘ │
└────────────┴─────────────────────────────┘
```

- 左側工具清單用 `ListBox`，點選切換屬性面板。
- 「＋新增」跳出對話框選擇 ToolType（circle / line / distance / angle）。
- Reference 欄位只在 distance/angle 顯示（ComboBox，列同配方內可參考的工具）。
- ROI 面板只在 circle/line 顯示。

---

## 七、HWindowControlHelper 所需的小改動

為支援「從影像取 ROI」，`HWindowControlHelper` 新增一個方法：

```csharp
// 請求一個一次性 ROI 繪製。畫完後用 callback 傳回(startRow,startCol,endRow,endCol)，
// 不觸發現有的 RoiSelected 事件。若已有 pending request，舊 request 會被取代。
public void RequestRoi(Action<double, double, double, double> callback);
```

- `RoiSelected` 事件保持不變（給 Edge Detection 用）。
- `RequestRoi` 的 callback 在 `_isDrawingRoi` 結束、且 ROI 框大於最小尺寸時呼叫。
- callback 呼叫後自動清除（一次性），下一個 `RequestRoi` 取代上一個。

---

## 八、實作階段

| 步 | 分支 | 內容 | 產出 |
|----|------|------|------|
| **E1** | feature/m3-recipe-editor | `RecipeEditor` Form 框架 + `ListBox` 工具清單 + circle/line ROI 屬性面板 + `HWindowControlHelper.RequestRoi` | 可建/編 circle/line 工具，取 ROI 回填 |
| **E2** | 同上 | distance/angle 屬性面板（RefToolIds ComboBox）+ Save / SaveAs / New / 變更追蹤 | 編輯器完整 CRUD |
| **E3** | 同上 | 整合進 MainWindow（Edit Recipe 按鈕、共用編輯器取代 Load Recipe 按鈕）、端到端測試 | 最終交付 |

每步各自 build + test + GUI 驗證 + commit。

---

## 九、使用者需確認的設計決策

| # | 決策 | 建議 | 狀態 |
|---|------|------|:--:|
| 1 | 整體架構 | A. 獨立 RecipeEditor Form | ✅ 已確認 |
| 2 | ROI 設定 | A+B. 取 ROI callback（主）+ 手動 NumericUpDown（fallback） | ✅ 已確認 |
| 3 | RefToolIds | A. 下拉 ComboBox 選工具 ID | ✅ 已確認 |
| 4 | 分階段 | E1 → E2 → E3 | ✅ 已確認 |
