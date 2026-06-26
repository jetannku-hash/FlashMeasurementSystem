# A5 幾何構造運算 — 設計文件 (Design Spec)

> 產出日期：2026-06-26
> 來源：能力差距清單 `docs/superpowers/plans/2026-06-25_現況到主流量測儀_能力差距清單.md` 之 A5。
> 方法：直接讀原始碼確認既有模型；HALCON operator 對 `halcon_pdf/reference/` 核實。
> 狀態：設計已與使用者逐段確認通過，待審後進入 writing-plans。

---

## 1. 目標 (Goal)

讓量測項可以「組合」：由已擬合的 line/circle 元件，構造出新的幾何（**線交點**、**對稱中線/角平分線**、**點到線投影**），其結果成為可被下游 distance/angle 工具參照的一等公民。撐起主流量測儀的典型用例，例如「兩線交點到圓心的距離」。

**第一版範圍（已確認）**：核心三項構造（intersection / midline / projection）。切線（tangent）與最佳擬合基準（best-fit datum）留待後續（datum 與 A4 GD&T 一起做最合適）。

**非目標 (Out of scope, v1)**：
- 鏈式構造（construction 參照另一個 construction 的結果）→ 避免 RecipeRunner 需要拓樸排序。
- 切線、datum 基準系統。
- 對既有 HALCON distance/angle measurer **內部**的任何修改。

---

## 2. 架構決策 (Approach A — 統一幾何基元解析層)

構造數學為純 2D 幾何，**不需 HALCON**，因此放在 `Domain`（比照 `AngleNormalizer` / `Rect2EditMath` 的純靜態 + 單元測試模式），不強套「Domain DTO + Application 介面 + Halcon 轉接」五件式（YAGNI：沒有 HALCON 相依就不造 Halcon adapter）。

引入小抽象 `GeometricPrimitive`，讓每個工具的執行結果都能解析成 `Point | Line | Circle`；distance/angle 改以「先解析 ref 成 primitive、再路由到既有 measurer 方法」的薄前門消費它們。既有 measurer 內部完全不動。

嚴格分層仍遵守 `CLAUDE.md`：`Domain` 無 HALCON/UI/檔案相依；GUI 接線在 `App.Wpf`；`RecipeRunner`（App.Wpf）負責編排。

---

## 3. 資料模型 (Domain)

### 3.1 `GeometricPrimitive`（新，`Domain/Geometry/GeometricPrimitive.cs`）

純 value object，承載一個幾何基元。

```
enum GeometricPrimitiveKind { Point, Line, Circle }

class GeometricPrimitive
{
    GeometricPrimitiveKind Kind
    // Point:  Row, Col
    // Line:   Row1, Col1, Row2, Col2
    // Circle: CenterRow, CenterCol, RadiusPx
    double Row, Col
    double Row1, Col1, Row2, Col2
    double CenterRow, CenterCol, RadiusPx

    static GeometricPrimitive Point(double row, double col)
    static GeometricPrimitive Line(double r1, double c1, double r2, double c2)
    static GeometricPrimitive Circle(double cr, double cc, double radiusPx)

    // 需要「點」時，Circle 退化為圓心、Line 不可退化（呼叫端負責語意）
    bool TryAsPoint(out double row, out double col)   // Point→自身；Circle→圓心；Line→false
}
```

### 3.2 `GeometryConstruction`（新，`Domain/Geometry/GeometryConstruction.cs`）

純靜態構造數學，全部可單元測試。座標慣例 (row, col)，row 向下；與既有 `RecipeRunner.ProjectPointOntoLine`、`LineFittingResult` 端點一致。

```
static class GeometryConstruction
{
    const double ParallelEpsilon = 1e-9;   // 方向外積絕對值門檻，判定平行

    // 兩線交點。以參數式/克拉瑪法解；平行（外積 ~ 0）回 false。
    static bool TryLineIntersection(
        double a_r1, double a_c1, double a_r2, double a_c2,
        double b_r1, double b_c1, double b_r2, double b_c2,
        out double row, out double col);

    // 對稱中線/角平分線（等距軌跡）。回傳一條線段端點（row1..col2）。
    //  - 平行：置中線（與兩線平行、置於正中），端點長 = 兩線平均半長。
    //  - 相交：角平分線；方向 = normalize(dirA') + normalize(dirB')，
    //    其中 dirB' = dirB 若 dot(dirA,dirB) >= 0 否則 -dirB（消除線無向性歧義），
    //    通過交點，端點長 = 兩線平均半長。
    static void Midline(
        double a_r1, double a_c1, double a_r2, double a_c2,
        double b_r1, double b_c1, double b_r2, double b_c2,
        out double row1, out double col1, out double row2, out double col2);

    // 點到（無限）線的垂足。內容同 RecipeRunner 內現有 inline helper，抽出共用。
    static void ProjectPointOntoLine(
        double pRow, double pCol,
        double r1, double c1, double r2, double c2,
        out double footRow, out double footCol);
}
```

**Midline 等距性質（測試依據）**：對相交情形，平分線上任一點到兩線的垂距相等（在浮點容差內）。對平行情形，置中線上任一點到兩線垂距相等且各為半間距。

### 3.3 `GeometricPrimitiveResolver`（新，放 `App.Wpf`，因需讀 `ToolRunResult`）

把一個 `ToolRunResult` 映成 `GeometricPrimitive`：

| 工具 ToolType | 產出 primitive |
|---|---|
| line | Line(端點) |
| circle | Circle(圓心, 半徑) |
| intersection | Point |
| projection | Point |
| midline | Line |

實作上由 `ToolRunResult.OutputPrimitive`（見 §4.1）直接回傳；resolver 是薄包裝 + null/失敗檢查。

---

## 4. 配方整合 (App.Wpf)

### 4.1 `MeasurementTool` / `ToolRunResult`

- `MeasurementTool.ToolType` 新增三值：`"intersection"`、`"midline"`、`"projection"`。沿用既有 `RefToolIds`（不新增欄位）。
  - intersection：`RefToolIds = [lineA, lineB]`
  - midline：`RefToolIds = [lineA, lineB]`
  - projection：`RefToolIds = [circle, line]`（圓心投影到線）
- `ToolRunResult` 新增 `GeometricPrimitive OutputPrimitive`：每個 element（line/circle）與 construction 工具在其 pass 內填入；resolver 與下游統一讀此欄位。既有欄位（CircleCenter*, LineRow*, Distance* …）保留不動。

### 4.2 `RecipeRunner` 執行流程

維持兩段、插入一段：

1. **Pass 1（elements）**：line/circle，**不變**；額外在結尾設 `OutputPrimitive`（line→Line、circle→Circle）。
2. **Pass 1.5（constructions，新）**：處理 intersection/midline/projection。
   - 透過 `byId` 解析 `RefToolIds` 對應的 element 結果 → primitive。
   - 僅允許參照基礎元件（line/circle）；若 ref 指向另一個 construction，視為設定錯誤 → 該工具失敗、訊息「v1 不支援鏈式構造」。
   - 呼叫 `GeometryConstruction.*` 計算，填 `OutputPrimitive`（intersection/projection→Point、midline→Line），存回 `byId`。
   - 失敗情形（平行無交點、ref 缺失/失敗）→ `Success=false` + 明確訊息。
3. **Pass 2（composites）**：distance/angle，**改走 primitive 前門**：
   - 以 resolver 把兩個 ref 解析成 primitive（可能是 element 或 construction）。
   - **distance** 依 (KindA, KindB) 路由到既有 measurer：
     | A \ B | Point | Line | Circle |
     |---|---|---|---|
     | Point | MeasurePointToPoint | MeasurePointToLine | MeasurePointToPoint(point, 圓心) |
     | Line | MeasurePointToLine | MeasureLineToLine | MeasurePointToLine(圓心, line) |
     | Circle | MeasurePointToPoint(圓心, point) | MeasurePointToLine(圓心, line) | MeasureCircleToCircle |
     （對稱填表；圓在「點」語境取圓心，與既有 circle↔circle = 圓心距一致）
   - **angle** 需兩個 Line primitive（line 或 midline）；任一非 Line → 失敗、訊息「角度量測需兩條線（可為構造中線）」。
   - 公差判定（IsOk）邏輯沿用既有。

### 4.3 `RecipeEditor` GUI

- 工具型別下拉新增 intersection / midline / projection。
- construction 工具：隱藏 ROI/邊緣偵測 UI（它們無自有 ROI），只露 ref 選擇器（比照 distance/angle 隱藏 ROI 的既有作法）。
- `PopulateRefCombos` 依型別過濾候選：
  - intersection / midline：ref1, ref2 = line 工具
  - projection：ref1 = circle、ref2 = line
  - distance：ref ∈ {line, circle, intersection, midline, projection}
  - angle：ref ∈ {line, midline}
- construction 工具**不顯示公差欄**（中間幾何、不判定）。

### 4.4 Schema 版本

`Recipe.SchemaVersion` v3 → v4：僅新增 ToolType 值，`RefToolIds` 沿用。舊配方（v3）無 construction 工具，照常載入；load 時不需遷移。

---

## 5. 錯誤處理

- **平行線求交點**：`TryLineIntersection` 回 false → 工具 `Success=false`、訊息「兩線平行，無交點」。
- **ref 缺失 / ref 工具本身失敗**：construction/composite `Success=false`、訊息「參照 {RefId} 無效或失敗」。
- **角度量測 ref 非線**：失敗、訊息「角度量測需兩條線（可為構造中線）」。
- **鏈式構造（v1 不支援）**：construction 的 ref 指向另一 construction → 失敗、訊息「v1 不支援鏈式構造」。
- 所有 HALCON 例外於 measurer 邊界轉為失敗結果（既有約定），不靜默吞。

---

## 6. 視覺化 (Overlay)

construction 工具在 overlay 上：
- **intersection**：於交點畫十字（如 `DrawCross`）。
- **midline**：畫該線段。
- **projection**：畫垂足點 + 從原點到垂足的垂線。

沿用 `RecipeRunner` 既有的 overlay 繪製管線（distance/angle 已有範式）。

---

## 7. 測試

### 7.1 Domain console 套件 `GeometryConstructionDomainTests`（新，接進 `EdgeDetectionDomainTests.Main()`）

- **TryLineIntersection**：相交（已知交點）、平行（回 false）、近平行但非平行（仍求得交點）、垂直/水平特例。
- **Midline**：
  - 平行兩線 → 置中線：取線上多點驗證到兩線垂距相等且 = 半間距。
  - 相交兩線 → 角平分線：取線上多點驗證到兩線垂距相等（等距性質）。
- **ProjectPointOntoLine**：一般點垂足、點已在線上→回自身、水平線、垂直線。
- **GeometricPrimitive**：三個工廠正確設值；`TryAsPoint`（Point→自身、Circle→圓心、Line→false）。

### 7.2 整合驗證（GUI，手動）

依 `CLAUDE.md`：HALCON/GUI 不做單元測試，於 GUI 手動驗。建一份配方含「兩條 line → intersection → distance 到一個 circle」，在 RecipeEditor 設定、Run Recipe，確認：
- 三種 construction 型別可建立、ref 選擇器候選正確。
- intersection 結果點正確、overlay 顯示十字。
- distance 能吃 intersection(Point) ↔ circle，距離數值合理、公差判定正常。
- 舊 v3 配方仍能載入。

---

## 8. 檔案清單 (預估)

| 檔案 | 動作 | 責任 |
|---|---|---|
| `Domain/Geometry/GeometricPrimitive.cs` | 新 | 幾何基元 value object |
| `Domain/Geometry/GeometryConstruction.cs` | 新 | 純構造數學 |
| `tests/.../GeometryConstructionDomainTests.cs` | 新 | Domain 單元測試 |
| `tests/.../EdgeDetectionDomainTests.cs` | 改 | 接 Run() 進 Main() |
| `Domain/FlashMeasurementSystem.Domain.csproj` | 改 | 註冊新檔（old-style csproj） |
| `tests/.../FlashMeasurementSystem.Tests.csproj` | 改 | 註冊新測試檔 |
| `App.Wpf/GeometricPrimitiveResolver.cs` | 新 | ToolRunResult → primitive |
| `App.Wpf/RecipeRunner.cs` | 改 | OutputPrimitive、Pass 1.5、distance/angle primitive 前門、ProjectPointOntoLine 改呼叫 Domain |
| `Domain/Roi/MeasurementTool.cs` | 改（註解/常數） | 新 ToolType 值（字串，無 enum） |
| `Domain/Roi/Recipe.cs` | 改 | SchemaVersion v4 註記 |
| `App.Wpf/RecipeEditor.cs` | 改 | 型別下拉、ref 過濾、隱藏 ROI/公差 |
| `App.Wpf/RecipeEditor.Designer.cs` | 改（如需） | 型別選項 UI |

> ToolRunResult 的確切定義於 plan 階段讀檔確認後再定欄位；csproj 為 old-style 需手動 `<Compile Include>`。

---

## 9. 風險

- **Midline 角平分線方向歧義**：線無向，方向選擇需以 `dot` 正負消歧；以等距性質單元測試把關。
- **distance primitive 前門改動既有 dispatch**：屬 Pass 2 路由層，既有 measurer 內部不動；以「先跑舊配方無 construction，行為不變」回歸驗證。
- **GUI 過濾/隱藏邏輯**：RecipeEditor 既有 ROI/公差顯示切換需正確涵蓋新型別，於 GUI 手動驗。
- 低風險整體：構造數學純 Domain、隔離、易測；整合沿用既有 RefToolIds/byId 組合模型。
