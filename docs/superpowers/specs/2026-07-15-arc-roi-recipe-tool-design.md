# 弧形 ROI 進配方 + 弧形卡尺工具 — 設計文件（v1）

> 狀態：設計已確認（2026-07-15），**尚未實作**。
> 定位：**共用基礎建設**。A3 互動式弧形卡尺當初只做進 MainWindow 的邊緣分頁，從未進配方；本專案讓弧形 ROI 成為配方一等公民，**一次解鎖 gear / PCD / 孔位陣列等所有 arc-based 方案**。
> 下游：`2026-07-15-gear-tooth-analysis-design.md`（齒輪方案，其 §4 宣告依賴本專案）。

## 1. 目標與價值

現況：配方對弧形 ROI **零支援**——`RoiGeometry` 只有 rect2、`RecipeRunner` 無 arc 分支、`RecipeEditor` 只擷取 rect2、`DetectEdgesOnArc` 連 `IEdgeDetector` 介面都沒上。

本專案讓操作員能把「弧形卡尺」存進配方、隨一鍵量測自動執行、跟隨工件姿態、進 CSV 報表。交付一個**真實可用的工具**（圓周等分特徵計數：孔數/齒數/引腳數），同時把基礎建設鋪好給後續 arc 方案。

## 2. 鎖定決策（brainstorm 2026-07-15）

| # | 決策 | 選定 |
|---|------|------|
| 可驗收成果 | **弧形卡尺成為配方工具**（`ToolType="arc"`），而非純管線骨架——有真實消費者才能端到端驗收 | 定 |
| 工具契約 | **量「邊數」**，沿用既有 `ToleranceSpec` 雙邊判定（例 Nominal=8、±0.5 → 恰好 8 個才 PASS）→ **零新判定/報表程式碼** | 定 |
| Schema | `MeasurementTool.ArcRoi`（nullable，**重用既有 `ArcMeasureRoi` DTO**），純加欄、向後相容 | 定 |
| 姿態變換 | **重用既有 `TransformRoi`**，零新 mapper 方法 | 定 |
| 編輯器 | 數值框 + 擷取鈕並存（比照現有 rect2 面板），擷取重用 A3 互動拖曳把手 | 定 |
| 介面改動 | **`DetectEdgesOnArc` 加進 `IEdgeDetector<TImage>`**（唯一既有介面改動） | 定 |

## 3. 範圍

### v1 做
- Schema v7：弧 ROI 存進配方（加性、向後相容）。
- `ToolType="arc"` 工具：弧卡尺量圓周邊點 → 邊數 → 既有容差判定 → 報表列。
- 跟隨工件姿態（旋轉/平移）。
- RecipeEditor 弧工具面板 + 互動擷取。
- RecipeValidator 弧規則、結果 overlay。

### v1 非目標
- 齒配對/齒距統計（＝ gear 方案，另立 spec，本專案完成後疊上）。
- PCD／孔位陣列統計（後續 arc 方案）。
- 弧 ROI 的 2D 量測模型（MetrologyModel）整合。
- 改造 `RoiGeometry` 成多形狀（風險波及全專案，見 §6）。
- mm 絕對準度（邊數為計數，本就無單位；角度免校正）。

## 4. 架構

### 4.1 Schema（v6 → v7）
```csharp
// MeasurementTool 新增（純加欄）：
public ArcMeasureRoi ArcRoi { get; set; } = null;   // null = 非弧工具
```
- 直接重用 `Domain/EdgeDetection/ArcMeasureRoi`（CenterRow/CenterCol/Radius/AngleStart/AngleExtent/AnnulusRadius + `IsDefined`/`ValidationError`），符合 `MeasurementTool` 註解揭示的「以組合方式重用既有 DTO」原則。
- `Recipe.SchemaVersion` 6→7，註解記錄：**純加欄位、向後相容、無遷移碼**——舊 `.zcp` 載入時 `ArcRoi=null`、無 arc 工具，1D 流程行為完全不變（比照 v5 `Gdt` / v6 `MetrologyModel` 的既有做法）。
- 弧工具欄位用法：`ToolType="arc"`、`ArcRoi` 必填、`EdgeParameters` 重用、`Tolerance` 判邊數、`RefToolIds` 空（自足元素工具）。

### 4.2 Application 介面（唯一既有介面改動）
```csharp
public interface IEdgeDetector<TImage>
{
    EdgeResult DetectEdges(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters);
    EdgeResult DetectEdgesSubPix(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters);
    EdgeResult DetectEdgesOnArc(TImage image, ArcMeasureRoi arcRoi, EdgeDetectionParameters parameters);  // 新增
}
```
`HalconEdgeDetector` **早已實作**同簽章方法，故為純加方法。⚠️ **任何既有 `IEdgeDetector` 實作（含測試 Fake）須補上此方法**，否則編譯失敗。`ArcMeasureRoi` 屬 Domain，介面維持 HALCON-free。

### 4.3 姿態變換（跟隨工件）— 零新程式碼
剛體變換下：中心需變換、起始角需旋轉、**半徑/角度範圍/環寬不變**（無縮放）。既有 mapper 正好提供：
```csharp
TransformedRoi t = _mapper.TransformRoi(arc.CenterRow, arc.CenterCol, arc.AngleStart, transform);
// → t.Row/t.Col = 變換後中心；t.AngleRad = arc.AngleStart + transform.RotationRad
```
組出變換後的 `ArcMeasureRoi`：`{ CenterRow=t.Row, CenterCol=t.Col, AngleStart=t.AngleRad, Radius/AngleExtent/AnnulusRadius 原值 }`。**不需新增 `ICoordinateMapper` 方法。**

### 4.4 RecipeRunner
Pass 1（元素工具）加 arc 分支——弧工具自足、不參考其他工具：
1. 取 `tool.ArcRoi`；若 `recipe.HasReferencePose && hasMatch` → 依 §4.3 變換到當前姿態，否則原值。
2. `_edgeDetector.DetectEdgesOnArc(image, placedArc, tool.EdgeParameters)`。
3. `MeasuredValue = result.EdgePoints.Count`（double）。
4. 沿用既有 `ToleranceJudger` 以 `tool.Tolerance` 判定 → `ToolRunResult`。
5. `ToolRunResult` 加性弧欄位（供 overlay/報表）：
   - `PlacedArc`（`ArcMeasureRoi`）＝變換後的弧
   - `ArcEdgePoints`（`List<EdgePoint>`）＝偵測到的邊點
6. 邊緣偵測失敗（`Success=false`）→ `Measured=false` + 訊息，比照既有元素工具的失敗處理（不擲例外、不漏 0 進判定）。

### 4.5 RecipeEditor
- 工具列加 **「Add Arc」** 鈕。
- 弧 ROI 面板（6 個數值框）：中心 Row/Col、半徑、起始角、角度範圍、環寬。角度單位用**弧度**，與既有 `_angleRadNumeric`（tooltip 明載 "angle in radians"）一致，避免同一編輯器內混用單位；`ArcMeasureRoi` 本身亦為弧度，無轉換。
- **「擷取弧形 ROI」** 鈕：接管主視窗共用影像，重用 A3 既有互動式弧形卡尺（`HWindowControlHelper.BeginArcEdit` 的拖曳把手：中心/起角/終角/半徑/環寬），拖曳結果雙向同步回數值框與 `tool.ArcRoi`。
- 面板顯示規則比照既有：選中 arc 工具才顯示弧面板（rect2 面板隱藏），反之亦然。

### 4.6 RecipeValidator
新增規則：`ToolType=="arc"` 時 `ArcRoi != null && ArcRoi.IsDefined`，否則以 `ArcRoi.ValidationError` 產生 issue（沿用既有 `RecipeIssue` 機制與一鍵閘門）。

### 4.7 Overlay / 報表
- Overlay：用**既有** `OverlayAnnotator.DrawArc(row, col, radius, startPhi, endPhi, pointOrder, color)` 畫變換後的弧 + 邊點十字（十字數量比照既有 `MaxOverlayCrosses` 均勻抽樣上限，避免壅塞）。
- 報表：沿用既有 `ToolRunResult` → CSV 管線，**零新程式碼**（邊數即 MeasuredValue）。

## 5. 已知限制

1. **`MeasurementTool.Roi`（rect2）對 arc 工具無用但仍存在**（非 null 預設）。這是加性模式的小 wart；刻意不改造 `RoiGeometry` 成多形狀，以免波及全專案既有工具與序列化（風險遠大於收益）。
2. 弧工具**只量邊數**；齒配對/齒距統計屬 gear 方案（下游）。
3. 邊數對雜訊敏感（Sigma/Threshold/環寬設定不當會多抓或漏抓邊）——由操作員以既有邊緣參數調整，錯誤訊息需可指引。

## 6. 錯誤處理與邊界

- `ArcRoi` 未定義/無效 → RecipeValidator 擋（一鍵閘門），Runner 端亦防禦性跳過並回失敗訊息。
- 邊緣偵測失敗/0 邊 → `Measured=false` + 訊息；0 邊時若 Tolerance 期望 >0 → 正常 NG（非例外）。
- 舊 schema（v6 以下）載入 → `ArcRoi=null`、無 arc 工具 → 行為不變（不需遷移碼）。
- HALCON 例外由既有 `HalconEdgeDetector` 轉 failed result（不外拋），Runner 不需額外處理。

## 7. 測試策略

### 7.1 Domain / Infrastructure（可全驗，無需硬體）
- `MeasurementTool.ArcRoi` 預設為 null；arc 工具建立後 `IsDefined` 行為。
- **`RecipeStore` 序列化 round-trip**：含 `ArcRoi` 的配方存→載→六個欄位逐一比對。
- **schema 向後相容**：載入無 `ArcRoi` 欄位的舊 JSON → `ArcRoi=null`、其餘欄位不受影響、行為不變。
- **RecipeValidator**：arc 工具缺 `ArcRoi` / `ArcRoi` 無效 → 產生對應 issue；合法 arc 工具 → 無 issue。
- 沿用既有 console 套件慣例，新增 `ArcRecipeToolDomainTests` 並掛進 `Main()`。

### 7.2 ⚠️ 對齊路徑自動測試（不重蹈 Phase 2 覆轍）
Phase 2 的教訓：對齊路徑的測試全是 `hasReferencePose=false`＝**驗證洞**，正是先前對齊 bug（5ce575b/6713fcc）的根因。本專案**必須**在既有 `tests/FlashMeasurementSystem.Tests.Halcon/CoordinateMapperTests` 補一個 **「弧 ROI 帶旋轉姿態對齊」** 測試：
- 已知 ref 姿態 + 帶旋轉的 cur 姿態 → `CreateFromMatch` → 依 §4.3 變換弧 → 斷言變換後中心落在預期點、`AngleStart` 增加了預期旋轉量、Radius/AngleExtent/AnnulusRadius **不變**。
- 驗證此測試「會 bite」：臨時不套變換 → 測試應變紅。

### 7.3 GUI 手動驗收
配方新增 arc 工具 → 互動擷取弧 ROI → 存檔/載回（弧欄位保留）→ 一鍵量測跑出邊數 + PASS/FAIL → overlay 畫出弧與邊點 → **旋轉工件複驗弧跟隨姿態**。

## 8. 後續（本專案完成後解鎖）

- **齒輪齒數/齒距方案**（`2026-07-15-gear-tooth-analysis-design.md`）：第二個 arc 工具，疊上純 Domain 齒輪分析器。
- PCD（螺孔圓）、孔位陣列：同樣重用弧 ROI 基礎建設。
- 共用「圓周等分特徵統計」分析器（gear/PCD/pin 共用去重+統計）。
