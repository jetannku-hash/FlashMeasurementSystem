# 操作員／工程師介面分流 計畫書

- 日期：2026-07-20
- 狀態：**討論中，尚未實作**
- 前置：本文取代原本排定的「WinForms 外觀重新設計（配色／間距）」優先順序

---

## 1. 問題陳述

目前主視窗把「量測儀器的所有能力」平鋪在三個分頁上，是一個**開發期的功能測試台**，不是工業現場的操作介面。

具體症狀：

- 操作員在生產時實際只會用到 3 個動作，但畫面上有 20 幾個按鈕。
- 會改變判定基準的危險操作（校正、Set Ref、略過 IQC）與日常操作混在同一層，沒有任何保護。
- 多數單項工具是 Recipe 系統成熟之前留下的腳手架，在生產路徑上零貢獻。

---

## 2. 已查證的事實

以下皆在程式碼中查證，附行號。

### 2.1 六項主視窗工具與 Recipe 純重複

| 主視窗按鈕 | handler | Recipe 對應 tool type |
|---|---|---|
| Fit Line | `FitLineButton_Click` | `line`（`RecipeEditor.cs:285`） |
| Fit Circle | `FitCircleButton_Click` | `circle`（`RecipeEditor.cs:283`） |
| Detect Arc | `DetectArcButton_Click` | `arc`（`RecipeEditor.cs:287`） |
| Measure Distance | `MeasureDistanceButton_Click` | `distance`（`RecipeEditor.cs:297`） |
| Measure Angle | `MeasureAngleButton_Click` | `angle`（`RecipeEditor.cs:299`） |
| Detect（edge） | `RunEdgeDetectionButton_Click` | 各 tool 攜帶的 `EdgeParameters` |

Fit Ellipse / Fit Rectangle 不在 Recipe 的 17 種 tool type 內，但 `Recipe.MetrologyModel` 的 `MetrologyObjectType` 有 Ellipse / Rectangle（`MetrologyObjectType.cs:10-11`），由 `MetrologyModelEditorForm` 定義、`RecipeRunner.cs:534-547` 執行。

### 2.2 一鍵量測已涵蓋整條生產鏈

`OneClickMeasureButton_Click`（`MainWindow.cs:1805-1873`）→ `MeasurementWorkflow.RunOnce`：

```
CheckingImage → MatchingTemplate → TransformingRois → Measuring
              → Evaluating → Reporting(CSV) → Completed → PDF
```

**未涵蓋者只有兩項**：載入配方（`MainWindow.cs:1807` 僅檢查）、載入影像（`MainWindow.cs:1808` 僅檢查；`MeasurementWorkflow.cs:65-76` 只接收已載入的 `HImage`）。

### 2.3 完全沒有角色／權限機制

全 repo 查無 role / access level / 登入 / 唯讀機制。唯一接近「模式」的是 `_skipIqcCheckBox`（`MainWindow.cs:183-189`），且其 tooltip 自述用途為 "for testing with synthetic images"。

---

## 3. 設計原則

**分流軸線是「角色」，不是「常用度」。**

判準：**這個操作按下去，會不會改變判定基準？**

- `校正...` 不常用，但一改就動到所有量測的尺度基準 → 必須擋在操作員之外
- `Draw ROI` 很常用，但屬工程行為 → 不該出現在生產畫面

用常用度切，會把這兩者都放錯位置。

---

## 4. 提議的介面結構

### 4.1 主頁（操作員模式）

只保留生產閉環所需：

| 元素 | 來源 | 備註 |
|---|---|---|
| 目前配方名稱 | 新增顯示 | 讓操作員確認跑的是對的配方 |
| 報表輸出路徑 | 新增顯示 | 現場常需要知道檔案去哪 |
| 載入影像 | `LoadTestImageButton_Click` | 生產必須，一鍵未涵蓋 |
| 一鍵量測 | `OneClickMeasureButton_Click` | 整條生產鏈 |
| PASS/FAIL 大橫幅 | `resultBannerLabel`（已存在，24pt） | 沿用 |
| 結果表 + 失敗項目 | 現有結果繫結 | 沿用 |
| 重新載入配方 | `LoadRecipeButton_Click` | 換料號時使用 |

### 4.2 工程模式（切換後才出現）

現有三個分頁與工具列全部保留於此，不做功能刪減：

- Inspection：建立範本（Load Ref / Create Template / Clear ROI）、Run Matching、IQC
- Edge Detection：整個分頁（含六項重複工具中的 Detect / Detect Arc / Fit *）
- Measurement：Distance Measurement 群組、Measure Distance / Angle
- 工具列：校正、Set Ref、Edit Recipe、Metrology Model、DXF 比對

---

## 5. 處置分類：該藏 vs 該刪 vs 該移除

| 項目 | v1 處置 | 理由 |
|---|---|---|
| 六項重複量測工具 | **搬進工程模式，先不刪** | 風險低、可回退；是否該刪取決於 §9 待確認事項 |
| Fit Ellipse / Fit Rectangle | 搬進工程模式 | 對應 Metrology Model 子系統，仍有建模價值 |
| 建立範本群組 | 搬進工程模式 | 配方執行的前置產物 |
| 校正 / Set Ref / Edit Recipe / Metrology Model / DXF 比對 | 搬進工程模式 | 明確工程面板 |
| `略過IQC` CheckBox | **從生產路徑移除，不只是隱藏** | 跳過影像品質把關；自述為合成影像測試用；留在可觸及處是實質風險 |
| Load Test Image | 留在主頁 | 生產必須 |
| Run Image Quality Check | 留在主頁（次要位置） | 一鍵已涵蓋，但保留作為失敗時的診斷入口 |

---

## 6. 模式切換的實作選項

系統無既有權限機制，需從零建。三個選項：

| 選項 | 做法 | 適用情境 |
|---|---|---|
| **A. 設定檔／啟動參數** | `App.config` 或命令列旗標決定啟動於哪個模式，UI 上無切換入口 | 工程師調機完交機，現場不會切回 |
| **B. 選單開關（建議 v1）** | 選單列一個「工程模式」勾選項，無密碼 | 同一台機器兩種角色共用 |
| **C. 密碼保護** | B 再加一道密碼 | 有稽核／合規要求 |

**建議 v1 採 B，且不做密碼。** 先驗證分流本身是對的；過早做權限會拖慢驗證，且密碼可在之後以極小成本疊加到 B 上。

若 §9 的待確認事項答案是「交機後不切回」，則 A 更簡單，連 UI 開關都不必做。

---

## 7. 分階段實作

每階段結束都可獨立驗收、可回退。

### Phase 1 — 建立模式骨架（不搬任何功能）
- 加入模式狀態與選單開關
- 工程模式＝現況畫面，操作員模式＝暫時同現況
- **verify**：切換不會造成任何行為改變；建置 x64 + Any CPU；38 套 Domain 測試綠

### Phase 2 — 建立操作員主頁
- **先拆解 `measureResultLabel`（見 §8.1，此為 Phase 2 的第一步，不可略過）**
  - 新增專屬操作員結果顯示控制項，置於分頁之外
  - 操作員路徑的寫入點（`MainWindow.cs:1379, 1411, 1763-1766, 1855`）改寫至新控制項
  - 工程路徑的寫入點（`MainWindow.cs:2785, 2814-2866, 3174, 3191`）維持寫入原 `measureResultLabel`
- 新增操作員版面（配方名／報表路徑／載入影像／一鍵量測／橫幅／結果表）
- 工程模式仍為現況三分頁
- **verify**：手動 GUI — 操作員模式跑完整流程（載配方→載圖→一鍵→PASS/FAIL→CSV+PDF 產出）
- **verify**：切到工程模式跑 Fit Line／Measure Distance，確認工程結果訊息仍正常顯示且未寫進操作員畫面

### Phase 3 — 收攏工程功能
- 三分頁與工具列移入工程模式
- `略過IQC` 自生產路徑移除
- **verify**：手動 GUI — 逐一確認各工程功能在新位置仍可運作，尤其共用影像視窗的 overlay 租約（`AcquireOverlay`）在模式切換時不外洩

### Phase 4 — 現場體驗收尾
- 自動載入上次使用的配方（開機即可直接載圖量測）
- **verify**：重啟 App 後配方自動還原

---

## 8. 風險

### 8.1 `measureResultLabel` 橫跨兩種模式（已查證，必須在 Phase 2 先處理）

**這是本計畫最大的結構性障礙，發現於計畫初稿之後。**

`measureResultLabel` 的父層鏈是：

```
measureResultLabel → measurementTableLayout → measurementBox → measurementTabPage
```

（`MainWindow.Designer.cs:1432, 1402, 1391`）

也就是說，它**實體上位於 Measurement 分頁內**，而本計畫 §4.2 要把整個 Measurement 分頁移入工程模式。但它同時是**操作員路徑唯一的詳細結果輸出面**：

| 寫入點 | 路徑 | 內容 |
|---|---|---|
| `MainWindow.cs:1379` | 操作員 | 載入配方結果 |
| `MainWindow.cs:1411` | 操作員 | Set Ref 結果 |
| `MainWindow.cs:1763-1766` | 操作員 | **配方量測結果 + NG 紅色標示** |
| `MainWindow.cs:1855` | 操作員 | 一鍵量測的 PDF 產出訊息 |
| `MainWindow.cs:2785, 2814-2866` | 工程 | Fit 結果帶入提示 |
| `MainWindow.cs:3174, 3191` | 工程 | Distance／Angle 量測結果 |

**若照原計畫直接搬動分頁，操作員的量測結果顯示會跟著消失。**

處置：Phase 2 第一步拆成兩個獨立的結果面（操作員一個、工程一個），寫入點依路徑分流。詳見 §7 Phase 2。

附註：`resultBannerLabel`（24pt PASS/FAIL 大橫幅）**不受影響**——它的父層是 `resultBannerPanel → mainTableLayout(0,0)`（`MainWindow.Designer.cs:187, 217`），位於分頁之外，可直接沿用。

### 8.2 其他風險

1. **「隱藏」不等於解決重複。** 六項重複工具即使收進工程模式，仍是第二條會與 Recipe 漂移的量測路徑——這正是過往審查抓到假 PASS 那類問題的溫床。v1 先搬不刪是保守選擇，但不能當作問題已解決。

2. **overlay 租約洩漏。** `HWindowControlHelper` 的 overlay/手勢所有權是租約制（`AcquireOverlay`），且共用單一 `SetPersistentOverlayAction` 槽位。模式切換時若未正確 `Dispose` 租約，會出現殘留把手或互動模式卡死——此類缺陷在過往審查中已出現過多次，須列為每階段的必驗項目。

3. **`MainWindow.cs` 已達 3398 行。** 本次會再加入版面切換邏輯。應避免直接在 `MainWindow.cs` 內堆疊，考慮把操作員版面獨立成一個 UserControl。

4. **GUI 無自動化測試。** 本專案 GUI 一律人工驗收（既有結構性限制，非本次引入）。所有 verify 步驟需人工執行。

5. **`MainWindow.Designer.cs` 會被 VS 設計器自動重生**，commit 前須 `git diff` 檢查夾帶。

---

## 9. 已定案事項（使用者於 2026-07-20 確認）

1. **機器由操作員與工程師共用，需區分兩種角色。**
   → §6 採 **選項 B：選單開關，不做密碼**。模式切換入口必須存在於 UI 上。

2. **六項重複工具 v1 採「搬進工程模式，不刪除」。**
   → 維持 §5 的處置。註：判斷依據的原始問題（RecipeEditor 的「試測」是否已覆蓋調參迴圈）**仍未驗證**，此處是直接採取保守決策而非該問題已有答案。若日後要進一步刪除，仍需先完成該項驗證。

3. **稽核／密碼／操作紀錄：v1 不實作，登錄於 ROADMAP 待日後評估。**
   → §6 選項 C 不納入本計畫範圍。

---

## 10. 本計畫不包含

- 配色、間距、控制項外觀等純視覺工作（原 GUI 優化 backlog 項目，優先度低於本案）
- 刪除任何現有量測邏輯
- 新增任何量測功能
