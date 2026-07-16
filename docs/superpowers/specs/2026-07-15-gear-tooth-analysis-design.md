# 齒輪齒數/齒距量測方案 — 設計文件（v1）

> 狀態：設計已確認（2026-07-15），**尚未實作**。
> 定位：Phase 5 應用方案庫的「基元→方案」範本（見 `docs/superpowers/plans/2026-06-26_應用落地量測方案庫_建議文件.md` §4 建議落地順序第 1 項）。
> **⚠️ 前置依賴：需先完成「弧形 ROI 進配方」基礎建設（見 §4），本方案才能以配方工具型別落地。**

## 1. 目標與價值

把既有的「弧形卡尺基元」包成具名方案：操作員畫一個涵蓋齒圈的弧形 ROI、填標稱齒數與公差，一鍵得到**齒數 / 齒距均勻度 / 齒寬均勻度 + PASS/FAIL**，不需碰底層 operator。

演算法**在像素/角度空間完成，不需硬體校正**（角度齒距天生免校正），故在目前無相機/無校正片的條件下可全驗。

## 2. 鎖定決策（brainstorm 2026-07-15）

| # | 決策 | 選定 |
|---|------|------|
| 去重機制 | **雙極性配對**：`Polarity='all'`，用 `measure_pos` 回傳的 Amplitude **正負號**分辨進/出齒 → 配對成齒 → 得齒中心 + **齒寬** | 定 |
| 齒極性 | **參數指定，預設「齒為暗」**（閃測儀背光剪影典型）：進齒=亮→暗(負)、出齒=暗→亮(正) | 定 |
| 中心來源 | **arc ROI 中心**（操作員以既有互動式弧形卡尺對準） | 定 |
| 判定 | **三條件**：齒數==標稱 AND 齒距最大偏差≤T_pitch AND 齒寬最大偏差≤T_width | 定 |
| 整合 | **配方工具型別**（進配方/一鍵/報表）——**依賴 §4 基礎建設** | 定 |

## 3. 範圍

### v1 做
- 純 Domain 齒輪分析器：邊點 → 齒數 / 齒距統計 / 齒寬統計 / 三條件判定 / 缺齒位置提示。
- 以配方工具型別落地（弧 ROI + 標稱齒數 + 公差存進配方；一鍵量測自動跑；結果進 CSV 報表）。

### v1 非目標
- 中心擬合 / 去除「每轉一次」誤差成分（見 §7）。
- 完整斷齒檢測（齒寬只在量測半徑那一圈量，見 §7）。
- 模數/壓力角/齒形輪廓度等齒輪專業參數。
- mm 絕對準度（角度免校正；弧長輸出 px，mm 需 A1 校正）。
- 部分弧（非整圈）的齒數統計（v1 要求完整 2π 掃描，見 §6.1）。

## 4. 前置依賴：弧形 ROI 進配方（另立專案，先做）

**現況查證**：配方目前對弧形 ROI **零支援**——
- `RoiGeometry` 只有 rect2（CenterRow/Col/Length1/Length2/AngleRad），無弧。
- `Recipe` schema v6 的工具全為 rect2-based；`RecipeRunner` 無任何 arc 分支。
- `RecipeEditor` 的 ROI 擷取只做 rect2。
- `DetectEdgesOnArc` **不在 `IEdgeDetector<TImage>` 介面上**（只在具體 `HalconEdgeDetector`；MainWindow 以具體型別呼叫）。

A3 互動式弧形卡尺當初只做進 MainWindow 的邊緣分頁，從未進配方。因此「齒輪做成配方工具」實際包含兩個子系統，本 spec **只涵蓋第 2 項**：

1. **弧形 ROI 成為配方一等公民**（另立 spec）：schema v7 加弧 ROI、RecipeEditor 弧 ROI 擷取 UI、RecipeRunner 弧分支、**弧 ROI 的姿態變換**（跟隨工件旋轉；rect2 走既有 `TransformRoi`，弧需對應處理）、`IEdgeDetector` 加 arc 方法、報表列。此基礎建設一次解鎖 **gear + PCD + 孔位陣列** 等所有 arc-based 方案。
2. **齒輪分析器 + 齒輪工具**（本 spec）。

## 5. 架構

本方案分兩塊，**相依性不同（對排程很重要）**：

| 塊 | 內容 | 依賴 §4 基礎建設？ |
|---|------|------|
| **A. 齒輪分析器** | 純 Domain 函式（邊點→齒數/齒距/齒寬/判定） | **否** — 可獨立實作並 100% 單元測試（合成邊點），現在就能做完 |
| **B. 配方工具接線** | `MeasurementTool.Gear` 欄位、RecipeRunner 分支、RecipeEditor 面板、報表列、overlay | **是** — 需先有弧 ROI 進配方 |

**分析器（A）零新 HALCON、零新 Application 介面**：邊點由既有弧形卡尺（`DetectEdgesOnArc`）提供；分析器是純函式，比照既有無介面的純 Domain 前例（`DxfDeviationEvaluator`、`GeometryConstruction`）。

- **`Domain/GearAnalysis/GearAnalysisParameters.cs`**
  - `NominalToothCount`（標稱齒數）、`ToothIsDark`（預設 `true`）、`PitchToleranceDeg`、`WidthToleranceDeg`、`Default()`。
- **`Domain/GearAnalysis/GearAnalysisResult.cs`**
  - `Success`、`IsPass`、`Message`；`ToothCount`；
  - 齒距：`PitchMeanDeg`/`PitchMinDeg`/`PitchMaxDeg`/`PitchMaxDevDeg`；
  - 齒寬：`WidthMeanDeg`/`WidthMinDeg`/`WidthMaxDeg`/`WidthMaxDevDeg`/`WidthMeanPx`；
  - 判定分項：`CountOk`/`PitchOk`/`WidthOk`；
  - `Teeth`（每齒 `CenterAngleDeg` + `WidthDeg`，供 overlay 標示）；
  - `MissingToothHintsDeg`（齒距≈2×中位數處的角度）。
  - `Failed(string)` 工廠。
- **`Domain/GearAnalysis/GearToothAnalyzer.cs`**（純靜態）
  ```csharp
  public static GearAnalysisResult Analyze(
      IList<EdgePoint> edgePoints, double centerRow, double centerCol,
      double radiusPx, GearAnalysisParameters parameters)
  ```
- **配方層（B，依賴 §4）**：`MeasurementTool` 新增 `Gear`（nullable 加性欄，比照 `Gdt`/`MetrologyModel` 的向後相容做法，schema 純加欄、舊檔載入為 null 行為不變）；`RecipeRunner` 於齒輪工具分支呼叫弧卡尺 → `GearToothAnalyzer` → `ToolRunResult`。
- **UI（B）**：`RecipeEditor` 齒輪工具面板（標稱齒數、齒為暗/亮、兩個公差）；overlay 標出各齒中心與超差處。

**建議實作順序**：A（分析器）可立即單獨成案並全驗；B 待 §4 基礎建設落地後再接線。若要更早看到成效，A 完成後可暫以既有 MainWindow 弧形卡尺分頁手動餵邊點驗證，不必等 B。

## 6. 演算法

### 6.1 步驟
1. **輸入驗證**：邊點數 ≥ 4 且為偶數（每齒 2 邊）；`radiusPx > 0`；`NominalToothCount > 0`。否則 `Failed(...)`。
2. **轉角度**：每個邊點 `θ = atan2(row − centerRow, col − centerCol)`，正規化到 `[0, 2π)`。
3. **排序**：依 θ 遞增排序（固定掃描方向）。
4. **分類**：依 Amplitude 正負號分進/出齒。`ToothIsDark=true` 時：進齒=負（亮→暗）、出齒=正（暗→亮）；`false` 時相反。
5. **配對**（含環繞）：沿排序序列配「進齒→出齒」成一齒。若序列首個邊是「出齒」，代表該齒跨越 2π→0 邊界 → 與**最後一個進齒**配成環繞齒。若正負號序列**未乾淨交替**（兩類數量不等或連續同號）→ `Failed("邊序列未交替，請調整 Sigma/Threshold 或環寬")`。
6. **每齒量值**：中心角 = 配對兩角的中點（環繞齒需跨 2π 取中點）；齒寬(角度) = 兩角差；齒寬(px) = `radiusPx × 齒寬(rad)`。
7. **齒距**：`pitch[i] = θc[i+1] − θc[i]`；最後一項環繞 `pitch[N−1] = (θc[0] + 2π) − θc[N−1]`。
8. **整圈驗證**：`Σpitch ≈ 2π`（容差內）。否則 `Failed("弧未涵蓋整圈，齒數無意義")` —— v1 要求完整 2π 掃描。
9. **統計**：齒距/齒寬各取 mean/min/max/最大偏差（`max|x − mean|`）。
10. **缺齒提示**：`pitch[i] > 1.5 × median(pitch)` → 記錄該處角度到 `MissingToothHintsDeg`（≈2× 中位數即漏一齒）。
11. **判定**：
    - `CountOk = (ToothCount == NominalToothCount)`
    - `PitchOk = (PitchMaxDevDeg ≤ PitchToleranceDeg)`
    - `WidthOk = (WidthMaxDevDeg ≤ WidthToleranceDeg)`
    - `IsPass = CountOk && PitchOk && WidthOk`（邊界含公差，比照專案既有 `≤` 慣例）

### 6.2 角度慣例
`atan2(row−cr, col−cc)` 在 HALCON `(row=y 向下, col=x)` 下為左手系，角度視覺上順時針遞增。**這不影響齒距/齒寬**（皆為角度差），只要排序方向一致即可。

## 7. 已知限制（誠實記錄）

1. **中心誤差 → 假性齒距不均**：中心來自 ROI，偏移 δ 會在齒距上造成約 `δ/R` 弧度、**每轉一次**的正弦起伏。例：R=200px、δ=2px → 約 0.57°，對 20 齒輪（齒距 18°）約 3%。緩解：操作員用互動式弧形卡尺對準（環帶是否整圈貼齊齒是可見的自我校正）。v2 可加中心擬合或去除每轉一次成分。
2. **齒寬只量一圈**：齒寬在**量測半徑那一圈**取得；若斷齒發生在齒頂（量測半徑之外）可能量不到 → **不可當完整斷齒檢測**，僅為齒寬異常指標。
3. **齒極性設錯的症狀**：`measure_pos` 的 Amplitude 正負號相對於量測物件的掃描方向；若 `AngleExtent` 正負使慣例看似翻轉，配對會框到**齒隙**而非齒。因兩種配對的**齒數與齒距完全相同**，唯一症狀是「齒寬報成齒隙寬」→ 操作員翻轉「齒為暗/亮」參數即可修正。
4. **單位**：角度齒距免校正；齒寬 px 需 A1 校正才能轉 mm。

## 8. 錯誤處理與邊界

- 邊點 < 4 / 奇數 / 正負號未交替 → `Failed` + 明確訊息（提示調 Sigma/Threshold/環寬）。
- `Σpitch` 不足 2π → `Failed("弧未涵蓋整圈")`。
- `NominalToothCount ≤ 0` 或公差 ≤ 0 → `Failed`（比照 GD&T 擋 `T≤0` 的既有慣例）。
- 齒數與標稱不符 → **不是錯誤**，是 `IsPass=false` 的正常 NG（Message 標明實測 vs 標稱）。
- 分析器為純函式，不擲例外給 UI；弧卡尺本身的 HALCON 例外由既有 adapter 轉 failed result。

## 9. 測試策略

分析器 **100% 用合成邊點單元測試**（console 套件 `GearAnalysisDomainTests`，無需影像/硬體）：
- **完美齒輪**：N=20 均勻齒 → 齒數=20、齒距最大偏差≈0、PASS。
- **缺齒**：移除一齒的兩個邊 → 齒數=19（CountOk=false）、該處齒距≈2×中位數 → `MissingToothHintsDeg` 標出角度。
- **窄齒**：單齒齒寬縮小 → WidthOk=false、齒距仍 OK。
- **齒距不均**：偏移一齒中心 → PitchOk=false。
- **環繞**：刻意讓一齒跨 0/2π 邊界 → 齒數與齒距正確（不多不少一齒）。
- **邊界**：最大偏差恰=公差 → PASS（含邊界）。
- **極性翻轉**：`ToothIsDark=false` → 齒寬變成互補（齒隙寬），齒數/齒距不變（驗證 §7.3 的性質）。
- **失敗路徑**：邊點 <4、奇數、未交替、非整圈 → Success=false + 對應訊息。
- 角度單位：所有對外輸出為**度**，內部計算用弧度。

## 10. 後續增量（v2+，非本 spec）

中心擬合/去除每轉一次成分、完整斷齒檢測（多半徑或齒頂圓）、模數/壓力角/齒形輪廓度、部分弧統計、mm 絕對準度（需 A1）、與 PCD/孔陣列共用「圓周等分特徵統計」分析器。

## 11. Part B — 配方整合設計（2026-07-16 補；arc 基礎建設落地後定案）

§4 的前置依賴（弧形 ROI 進配方）已於 2026-07-16 merge（`8a34cb5`）。本節把「齒輪做成配方工具」的整合定案。**全部重用 arc 基礎建設**（`ArcRoi`、`DetectEdgesOnArc`、`ArcRoiTransform`、編輯器弧面板/擷取/環帶）。範圍：**A（純 Domain 分析器）+ B（`ToolType="gear"` 配方工具）一次做到可用**。

### 11.1 型別與 Schema（v7→v8，加性向後相容）
- 新 `ToolType="gear"`（獨立型別，不與 arc 混用）。
- `MeasurementTool.Gear`（nullable，比照 `Gdt`/`ArcRoi` 的加性做法）——**直接重用 Part A 的 `GearAnalysisParameters` Domain DTO**（`NominalToothCount`/`ToothIsDark`/`PitchToleranceDeg`/`WidthToleranceDeg`），不另造型別（比照 `ArcRoi` 重用 `ArcMeasureRoi`）。**量測環帶重用 `MeasurementTool.ArcRoi`**（gear 工具必填 ArcRoi）。RecipeRunner 直接把 `tool.Gear` 當分析器參數傳入，零轉換。
- `Recipe.SchemaVersion` 7→8，註解記錄：純加欄、無遷移碼、舊檔載入 `Gear=null` 行為不變。
- gear 工具的 `Tolerance`(雙邊) 不使用（走三判定，見 §11.3）。

### 11.2 RecipeRunner（新 gear 分支，重用 arc 管線）
在 arc Pass 1.2 之後加 gear 分支（gear 工具自足）：
1. 依 §4.3 的 `ArcRoiTransform.TransformArc` 把 `tool.ArcRoi` 轉到當前姿態（**重用，零新對齊碼**）。
2. `DetectEdgesOnArc(image, placedArc, edgeParams)`——**強制 `Polarity="all"`**（齒配對需正負 amplitude 分進/出齒；於 gear 分支覆寫 `EdgeParameters.Polarity`，不論工具設定）。
3. `GearToothAnalyzer.Analyze(edgePoints, placedArc.CenterRow, placedArc.CenterCol, placedArc.Radius, gearParams)`（`gearParams` 由 `tool.Gear` 組出）。
4. 設 `res.IsOk = result.IsPass`（三條件 AND）；把 `GearAnalysisResult` 存進 `ToolRunResult`（新欄位，供 overlay/CSV）；`res.PlacedArc = placedArc`（重用弧欄位供 overlay 畫環帶）；`res.ValueText` 摘要（如「齒數=20 距偏0.3° 寬偏0.5°」）。
5. 失敗（分析器 `Success=false` 或弧卡尺失敗）→ `Measured=false` + 訊息，比照既有慣例（不擲例外、不漏 0 進判定）。

### 11.3 判定與報表（照 GD&T 先例，三判定）
`MeasurementWorkflow` 加**專屬 gear 分支**（放在 `tool.Gdt` 分支旁、**在 `GetMeasuredValue` 呼叫之前**，避開單值 return-0 陷阱——與 GD&T 相同理由）：
- 由 `ToolRunResult` 上存的 `GearAnalysisResult` 發**三個 `ItemJudgment`**：
  - 「齒輪-齒數」：MeasuredValue=實測齒數、Nominal=標稱、判定 CountOk。
  - 「齒輪-齒距」：MeasuredValue=`PitchMaxDevDeg`、UpperLimit=`PitchToleranceDeg`、Unit="deg"、判定 PitchOk。
  - 「齒輪-齒寬」：MeasuredValue=`WidthMaxDevDeg`、UpperLimit=`WidthToleranceDeg`、Unit="deg"、判定 WidthOk。
- → CSV **三列**（SPC 友善，可分別趨勢/看哪項超差多少）。
- **工具層 `OkCount`/`NgCount` 讀 `r.IsOk`（overall）＝一個 gear 工具計 1 個**（不因三列變三個）。

### 11.4 編輯器（重用 arc 面板 + gear 參數）
gear 工具面板 = 既有 arc ROI 面板（重用 `ArcRoi` 擷取/環帶/度數角度）+ 齒輪參數區：標稱齒數（整數）、齒為暗（checkbox，預設勾）、齒距公差(deg)、齒寬公差(deg)。工具列加「+ 齒輪」鈕，`AddTool("gear")` 給預設 `ArcRoi` + 預設 `Gear`。面板可見性：選 gear 顯示 arc 面板 + gear 參數區，隱藏 rect2 Roi。

### 11.5 Overlay
重用 arc 環帶（`DrawArcBand`）+ **各齒中心標記**（`DrawCross`，色隨 overall 判定）+ 標籤（齒數 N + PASS/FAIL）；`MissingToothHintsDeg` 的缺齒位置以不同色（如洋紅）標出。比照既有 overlay 抽樣上限。

### 11.6 Validator
gear 工具規則：`Gear != null`（缺→Error）、`ArcRoi` 有效（沿用 `IsDefined`）、`NominalToothCount > 0`、兩公差 > 0，否則對應 Error。`"gear"` 加進 `KnownTypes`（不加 `RoiElementTypes`）。

### 11.7 吸取 arc 教訓（強制檢查清單，寫進實作計畫）
新工具型別進配方時**逐項確認**（arc 曾在這些漏過）：
1. `MeasurementWorkflow.GetMeasuredValue`／判定分支——gear 走專屬分支繞過，但**必須確認分支順序在 `GetMeasuredValue` 之前**（否則三判定被單值 return-0 覆蓋）。
2. `RecipeEditor.DeepCopyTool`——**必須深複製 `Gear` 與 `ArcRoi`**（arc 曾漏 ArcRoi 致載入/存檔遺失＝出貨阻斷）。
3. 編輯器面板可見性、試測（`RefreshTrialButtonEnabled` 含 gear、`OnTrialMeasure` 畫 gear overlay）。
4. overlay 對 gear 不畫 rect2 `Roi`（`Roi` 留 null，比照 arc）。
5. RecipeRunner 其他 pass（1.5/1.7/2/3）對 gear 的排除，避免重複「(未支援)」列。

### 11.8 測試（B 部分）
- Domain：`Recipe` 含 gear 工具的 `RecipeStore` round-trip（`Gear` + `ArcRoi` 六欄）、schema v7→v8 向後相容、Validator gear 規則。
- 姿態跟隨：**已由既有 arc 對齊測試守著**（gear 重用 `ArcRoiTransform`，無新對齊碼）。
- 分析器：§9 全套合成邊點單元測試。
- B 接線 UI：GUI 驗收（含旋轉件跟隨、CSV 三列與畫面一致）+ **合成齒輪影像 fixture**（比照 DXF/arc 的做法，用 Python 生一張已知齒數/缺齒的齒輪圖）。
