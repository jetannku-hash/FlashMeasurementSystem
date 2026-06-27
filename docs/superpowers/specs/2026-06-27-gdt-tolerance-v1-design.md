# GD&T 形位公差 v1 — 設計規格

> 產出日期：2026-06-27
> 來源：能力差距清單 `docs/superpowers/plans/2026-06-25_現況到主流量測儀_能力差距清單.md` 的 **A4 GD&T**（含 A5 殘留的切線/最佳擬合基準收尾）。
> 前置：A5 幾何構造已完成併入 main（intersection / midline / projection、`GeometricPrimitive`、RecipeRunner Pass 1.5）。
> 狀態：**待使用者確認**。確認後才依對應 plan 逐 task 實作。

---

## 1. 範圍（v1）

實作 5 項形位公差，依「不需基準 → 單一基準」「可複用既有碼」排序：

| # | 公差 | 需基準 | RefToolIds | 偏差來源 | 複用 |
|---|------|--------|-----------|---------|------|
| 1 | 真圓度 Roundness | 否 | `[circle]` | `CircleFittingResult.Roundness`（max−min 徑向，**真值**） | CircleFitter |
| 2 | 真直度 Straightness | 否 | `[line]` | `LineFittingResult.ResidualRms`（**RMS 近似**，v1） | LineFitter |
| 3 | 平行度 Parallelism | 單一 A | `[line 量測, line 基準]` | `L·sin(Δθ)`（純數學） | 新 GdtCalculator |
| 4 | 垂直度 Perpendicularity | 單一 A | `[line 量測, line 基準]` | `L·sin(90°−θ)`（純數學） | 新 GdtCalculator |
| 5 | 同心度/同軸度 Concentricity | 單一 A | `[circle 量測, circle 基準]` | `2·圓心距`（直徑帶） | 新 GdtCalculator |

**明確不做（留 v2）**：位置度 Position（需完整 datum reference frame：原點+主次基準定向，最易算錯、最需對標 CMM，本機無硬體不宜）、對稱度、傾斜度、**切線 tangent**（非任何 GD&T 前置、目前無具體零件需求）、MMC/LMC 材料條件修飾。

---

## 2. 已確認的設計決策（對使用者四問的結論）

1. **5 項範圍**：確認採用。
2. **形狀公差準確度**：v1 用現成擬合殘差近似，之後升級。
   - 修正：**真圓度直接用 `Roundness`（max−min）＝GD&T 真值**，非近似（adapter 已算，零成本）。
   - **真直度用 `ResidualRms`（RMS 近似）**；真值（峰對峰垂距帶）留 v2。
3. **切線**：defer，不做。
4. **基準數量**：採**單一基準**。平行/垂直/同心各只需 1 個基準（RefToolIds[1]），足夠 v1。完整 datum frame（A+B+C）隨位置度一起留 v2。

---

## 3. 判定模型（與既有尺寸公差的根本差異）

- 既有 `ToleranceSpec` / `ToleranceJudger` 為**雙邊**：`[Nominal+Lower, Nominal+Upper]`。
- 形位公差為**單邊**：偏差恆 `≥ 0`，OK 條件 `0 ≤ deviation ≤ T`（T = 公差帶寬，mm），無 Nominal。
- **決策**：不硬套雙邊判定器（會對 deviation≈0 的「完美」誤報「接近下限 ⚠️」）。新增**純 Domain 單邊評估** `GdtEvaluation.Evaluate(deviation, T)`：
  - `IsOk = deviation ≤ T`（deviation 由構造保證 ≥0；負值/NaN/Inf 視為 NG 並給明確訊息）。
  - 接近邊界警告**只在接近上限 T**（餘量 `(T−deviation)/T < 20%`）。
  - 訊息中文化，比照 `ToleranceJudger` 風格。
  - 純靜態、無 DI（比照 `GeometryConstruction` 在 runner 內直接呼叫）。

---

## 4. 資料模型

新增（`FlashMeasurementSystem.Domain/Gdt/`）：

```
enum GdtCharacteristic { Roundness, Straightness, Parallelism, Perpendicularity, Concentricity }

class GdtToleranceSpec {
    GdtCharacteristic Characteristic;
    double ToleranceZoneMm;     // T，單邊公差帶寬（mm），> 0
    static GdtToleranceSpec Default();
}
```

修改：
- `MeasurementTool`：新增 `GdtToleranceSpec Gdt { get; set; } = null;`（null＝非 GD&T 工具）。`ToolType` 字串集擴充 5 種：`roundness/straightness/parallelism/perpendicularity/concentricity`。
- `ToolRunResult`（App.Wpf/RecipeRunner.cs）：新增
  - `double CircleRoundnessPx;`（Pass 1 由 `circle.Roundness` 帶入）
  - `double ResidualRmsPx;`（Pass 1 由 line/circle `ResidualRms` 帶入）
  - `double GdtDeviationMm;`（GD&T pass 計算結果，供顯示/報表）

**持久化**：`Recipe` 整包 Newtonsoft JSON 序列化，新增欄位天然向後相容。`SchemaVersion` `4 → 5`（純加版號，**無遷移碼**：舊 .zcp 載入時 `Gdt=null`、無 GD&T 工具，行為不變）。

---

## 5. 偏差計算（純 Domain，`Domain/Gdt/GdtCalculator.cs`）

座標 `(row, col)`，row 向下。所有回傳為**像素**，runner 再乘 `pixelSizeUm/1000` 轉 mm。

- `AcuteAngleBetweenLinesDeg(a1r,a1c,a2r,a2c, b1r,b1c,b2r,b2c)` → `[0,90]`：方向向量夾角，折到銳角。
- `LineLengthPx(r1,c1,r2,c2)` → 端點距離。
- **平行度** `ParallelismZonePx(量測線, 基準線)` = `LineLengthPx(量測線) · sin(Δθ_rad)`，Δθ = 兩線銳角夾角。理想平行 Δθ=0 → 0。
- **垂直度** `PerpendicularityZonePx(量測線, 基準線)` = `LineLengthPx(量測線) · sin((90°−θ)_rad)`，θ = 銳角夾角。理想垂直 θ=90° → 0。
- **同心度** `ConcentricityDiametralPx(cr1,cc1, cr2,cc2)` = `2 · hypot(cr1−cr2, cc1−cc2)`（直徑帶語意）。
- **真圓度 / 真直度**：無新數學。偏差直接取 ref 元素的 `CircleRoundnessPx` / `ResidualRmsPx`，於 runner 轉 mm。

> 註：平行/垂直的 mm 帶寬 = `特徵長度 × sin(角偏差)`，是 2D 下 GD&T 帶寬的正確語意；需量測特徵的像素長度（取 RefToolIds[0] 線元素端點）與 px→mm。無相機校正時為「未校正 mm」，與專案其餘量測一致。

---

## 6. RecipeRunner 整合

- **Pass 1（既有）**：line/circle 元素量測時，補帶 `res.ResidualRmsPx`、`res.CircleRoundnessPx`（circle）/`res.ResidualRmsPx`（line）。其餘不動。
- **新增 Pass「GD&T 公差工具」**（置於 Pass 1.5 之後、Pass 2 之前）：
  - 僅處理 5 種 GD&T ToolType。
  - 解析 RefToolIds → byId；驗證型別與數量（roundness/straightness 需 1 ref 且為 circle/line；parallelism/perpendicularity 需 2 line；concentricity 需 2 circle）。
  - 僅允許參照 **line/circle 基礎元件**（比照 A5 `IsBaseElement`，不支援鏈式參照構造結果）。
  - 計算 deviation（mm）→ `GdtEvaluation.Evaluate(dev, tool.Gdt.ToleranceZoneMm)` → 設 `res.IsOk / res.GdtDeviationMm / res.ValueText / res.Message`。
  - 失敗（ref 缺失/型別錯/未量測）：`Measured=false` + 明確訊息，不擋流程（比照既有）。
  - overlay 欄位：concentricity 用 `DistRow1/Col1→DistRow2/Col2`（兩圓心連線）；roundness/straightness 標註於 ref 元素；parallelism/perpendicularity 標註兩線。

---

## 7. UI（RecipeEditor / MainWindow）

- **RecipeEditor**：新增 5 種工具型別到加入工具的入口，比照既有 distance/angle/intersection：
  - 選 1 或 2 個既有工具為 ref（第 2 個為基準 A）。
  - 數值輸入 `T`（mm，> 0）。
  - 型別×ref 數量/型別的前置檢核（錯誤即時提示，不存壞配方）。
- **MainWindow overlay**：GD&T 工具結果以 OK/NG 顏色 + 偏差文字標註；concentricity 畫兩圓心偏移線。比照既有 distance/angle overlay 繪法。

> 實作時讀周邊既有按鈕/overlay 行做 mirror（宣告樣式、parent、RowStyle 索引），不憑空造樣式。

---

## 8. 測試策略（誠實分層）

**因決策 #2，v1 形狀公差偏差來自擬合器純量（真圓度＝真值 max−min 在 adapter；真直度＝RMS 在 adapter），故 v1 不在 Domain 做點雲峰對峰運算**——該運算（含合成瓣形點雲嚴格測）屬 v2 升級。v1 嚴格 Domain 測試聚焦三項幾何特性 + 單邊判定：

1. `GdtCalculatorDomainTests`（合成幾何 + 閉合解，確定性）：
   - 平行度：量測線水平長 L=100px，基準同向 → zone≈0；基準偏 δ° → zone=`100·sin δ`（多組角度核對）。
   - 垂直度：量測線垂直、基準水平（θ=90°）→ zone≈0；偏 δ → `L·sin δ`。
   - 同心度：兩圓心偏移 (3,4) → 距 5px → diametral=10px。
   - 銳角折疊：170° 與 10° 皆折到 10° 等。
2. `GdtEvaluationDomainTests`（單邊判定）：
   - `dev<T`→OK；`dev>T`→NG；`dev==T`→邊界 OK；`dev==0`→OK 且**不**誤報接近下限；負值/NaN/Inf→NG 明確訊息；接近上限觸發 ⚠️。
3. **形狀公差（真圓度/真直度）**：以 `GdtEvaluation` 餵已知純量驗判定；端到端用合成影像於 GUI 目視（真圓度因 adapter 已算 max−min，目視可比對畫圖時設的瓣幅）。

兩個新測試套件 `Run()` 接進 `EdgeDetectionDomainTests.Main()`。

**合成影像**（`scripts/gen_gdt_test_images.py`，輸出至 `data/images`，便利目視，非 ground truth）：瓣形近圓（真圓度）、弓形線（真直度）、兩近平行線、兩近垂直線、兩偏心圓。**誠實限制**：HALCON 次像素邊緣自帶誤差，數值只能粗驗量級；此層驗管線接線，非「量真實零件準度」（後者需實體標準件＋相機＋對標 CMM，本機無硬體，不驗）。

---

## 9. 報表

既有 CSV 報表逐 `ToolRunResult` 輸出 `IsOk + ValueText`；GD&T 工具把偏差寫進 `ValueText`（`GdtDeviationMm`）即自動流經。實作時確認 writer 為泛用迭代（非寫死型別）。

---

## 10. 風險與邊界

- **未校正 mm**：無相機/校正片，所有 mm（含 GD&T 帶寬）為未校正值；演算法可全驗，真實計量準度待硬體。
- **真直度為 RMS 近似**：RMS 低估真值峰對峰帶，v1 標示為近似，v2 升級。UI/報表需標註「近似」以免誤判合格。
- **真圓度對離群點敏感**：max−min 受單一雜訊點放大（adapter 註解已提醒），判讀搭配 RMS。
- **平行/垂直長度依賴**：帶寬含特徵像素長度，過短特徵的角偏差會被低估為小帶寬；於文件與 tooltip 說明。
- **單一基準**：不支援多基準定向與位置度；明確留 v2。
```