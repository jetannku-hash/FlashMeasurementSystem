# PCD 螺栓孔圈量測方案設計（Phase 5 第三個 arc 工具）

**日期**：2026-07-17
**狀態**：設計定案，待實作
**前置**：弧形 ROI 進配方基礎建設（merge `8a34cb5`）、齒輪方案（merge `7585741`）

## 1. 目標

量測工件上一圈螺栓孔，輸出並判定：**孔數**、**PCD 節圓直徑（mm）**、**角度均勻度（等分）**、**徑向真圓度（孔心是否共圓，mm）**，並偵測**缺孔**（洋紅提示）。像素空間可全驗；mm 絕對值用既有 pixel→mm 校正（無新硬體依賴）。

## 2. 與 gear 的本質差異（決定管線）

gear 量「弧上的角度特徵」（齒沿掃描弧跨越），只需角度 → 弧邊掃描（`DetectEdgesOnArc`）即可。**PCD 的「D」是節圓直徑，必須拿到每個孔的 2D 中心再擬合圓**；沿固定半徑掃弧只得角度、量不到真 PCD（掃描半徑≠真節圓半徑時就錯）。

故 PCD 偵測管線 = **環帶內 blob 偵測孔 → 孔質心 → 代數圓擬合**，與 gear 的弧邊配對不同。重用弧形基礎建設的是**幾何與框架**（`ArcRoi` 當環狀搜尋區、編輯器弧面板、`ArcRoiTransform` 姿態、overlay、CSV 多判定框架），偵測層另建。

## 3. 架構與重用

比照 gear 拆 **Part A 純 Domain 分析器 + Part B 配方工具**。

```
Domain: PcdAnalyzer + PcdAnalysisParameters + PcdAnalysisResult + HolePoint
Application: IHoleDetector<TImage>（新介面）
Halcon: HalconHoleDetector（新 adapter）
App.Wpf: RecipeRunner Pass 1.4 + MeasurementWorkflow 四判定 + RecipeEditor pcd 面板 + MainWindow overlay
Domain/Roi: MeasurementTool.Pcd（schema v9）+ RecipeValidator pcd 規則
```

## 4. Part A — `PcdAnalyzer`（純 Domain，合成點全驗）

### 4.1 資料型別
```
HolePoint { double Row; double Col; }   // 孔質心（px）

PcdAnalysisParameters {
  int    NominalHoleCount = 6;
  double NominalPcdMm     = 0.0;    // 標稱節圓直徑（mm）
  double PcdToleranceMm   = 0.1;    // PCD 公差（mm，單邊：|PCD−標稱|≤此值）
  double AngularToleranceDeg = 1.0; // 角度均勻度公差（deg）
  double RadialToleranceMm   = 0.05;// 徑向真圓度公差（mm）
  bool   HoleIsDark       = true;   // 背光穿孔＝暗（偵測層用；分析器不使用）
  double MinHoleAreaPx    = 20.0;   // blob 最小面積濾雜訊（偵測層用；分析器不使用）
  static Default();
}
// 註：HoleIsDark/MinHoleAreaPx 僅偵測層（RecipeRunner→HALCON）使用，純分析器忽略；
//     放同一 DTO 以便配方序列化與編輯器單一群組（比照 gear 的 ToothIsDark）。

PcdAnalysisResult {
  bool Success; bool IsPass;
  int  HoleCount;
  double PcdMm; double PcdPx;
  double CenterRow; double CenterCol;          // 擬合圓心（px，供 overlay）
  double AngularMeanDeg; double AngularMaxDevDeg;
  double RadialMaxDevMm; double RadialMaxDevPx;
  bool CountOk; bool PcdOk; bool AngularOk; bool RadialOk;
  List<HolePoint> Holes;                        // 依角度排序（供 overlay 標記）
  List<double> MissingHoleHintsDeg;             // 缺孔提示角度（deg）
  string Message;
  static Failed(string);
}
```

### 4.2 演算法 `Analyze(IList<HolePoint> holes, double pixelSizeUm, PcdAnalysisParameters p)`
1. **守門**：`p==null`→用 Default；`NominalHoleCount<=0 || PcdToleranceMm<=0 || AngularToleranceDeg<=0 || RadialToleranceMm<=0`→`Failed("PCD 參數無效")`；`holes==null || holes.Count<3`→`Failed("孔數不足（圓擬合需 ≥3 個孔）")`；`pixelSizeUm<=0`→`Failed("像素尺寸無效")`。
2. **代數圓擬合（Kåsa 法，純代數）**：對所有孔質心解圓 `x²+y²+D·x+E·y+F=0`（x=Col、y=Row）的 3×3 正規方程 → 圓心 `(cc,cr)=(−D/2,−E/2)`、半徑 `R=√(D²/4+E²/4−F)`。退化（共線/奇異、`R` 為 NaN 或 ≤0）→`Failed("孔心無法擬合圓（共線或退化）")`。
   - `PcdPx = 2R`；`PcdMm = PcdPx × pixelSizeUm / 1000.0`（比照 circle `DiameterMm`）。
3. **各孔角度/徑距**：`angle=atan2(Row−cr, Col−cc)` 正規化 `[0,2π)`；`radial=√((Row−cr)²+(Col−cc)²)`。
4. **依角度排序** → `HoleCount=N`；**相鄰角距**（環繞、和為 2π）；`AngularMeanDeg=360/N`；`AngularMaxDevDeg=max|pitch_i − 2π/N|`（轉度）。
5. **徑向真圓度**：`RadialMaxDevPx = max|radial_i − R|`；`RadialMaxDevMm = RadialMaxDevPx × pixelSizeUm/1000`。
6. **缺孔**：角距 > 1.5×中位數 → 提示點放**間隙中點** `centers[t]+pitch/2`（比照 gear 缺齒定位修正，避免畫在鄰孔上）。
7. **四條件判定**：
   - `CountOk = HoleCount == NominalHoleCount`
   - `PcdOk = |PcdMm − NominalPcdMm| ≤ PcdToleranceMm`
   - `AngularOk = AngularMaxDevDeg ≤ AngularToleranceDeg`
   - `RadialOk = RadialMaxDevMm ≤ RadialToleranceMm`
   - `IsPass = CountOk && PcdOk && AngularOk && RadialOk`
8. `Message = "孔數={N}(標稱{n}) PCD={PcdMm:F3}mm 角偏差={AngularMaxDevDeg:F2}° 徑偏差={RadialMaxDevMm:F3}mm → PASS/FAIL"`（InvariantCulture）。

**判定全部在純 Domain 分析器內**（`pixelSizeUm` 由呼叫端傳入），刻意不學 circle 把 mm 判定寫在 RecipeRunner（那是測不到的層＝驗證洞）。合成質心 + 給定 `pixelSizeUm` 可全驗四條件。

## 5. Part B — 配方工具 `ToolType="pcd"`（schema v9）

### 5.1 schema v9
`MeasurementTool.Pcd`（`PcdAnalysisParameters`，加性 nullable、向後相容，比照 `Gear`）。量測環帶重用 `MeasurementTool.ArcRoi`。`Recipe.SchemaVersion = 9`。**修 5 個既有硬寫 schema-version 測試**（8→9：`ArcRecipeToolDomainTests`、`GearRecipeToolDomainTests`、`MetrologyModelDomainTests`、`RoiDomainTests` ×2）。

### 5.2 偵測：`IHoleDetector<TImage>`（新 feature-adapter）
`Application/HoleDetection/IHoleDetector.cs`：`HoleDetectionResult DetectHolesInAnnulus(TImage image, ArcMeasureRoi placedArc, PcdAnalysisParameters p)`。
`Halcon/HoleDetection/HalconHoleDetector.cs`：`EnsureSingleChannel` → 由 `placedArc`（中心/半徑/環寬/角範圍）`gen_region` 環狀扇形 + `reduce_domain` → 依 `HoleIsDark` `binary_threshold(...,'max_separability',...)` 取暗/亮 → `connection` → `select_shape('area', MinHoleAreaPx, max)` → `area_center` → 孔質心清單（HalconException→失敗結果）。放 Halcon 層（唯一能碰 HObject 處），`using`/`finally` 釋放所有 region 句柄。

### 5.3 RecipeRunner Pass 1.4（gear Pass 1.3 之後）
`ArcRoiTransform.TransformArc(_mapper, tool.ArcRoi, transform)` 跟隨姿態 → `_holeDetector.DetectHolesInAnnulus(image, placedArc, tool.Pcd)` → `PcdAnalyzer.Analyze(centroids, pixelSizeUm, tool.Pcd)`（`pixelSizeUm` 同 circle 的 `ResolvePixelSize`）→ `ToolRunResult.Pcd = result`、`res.Measured = result.Success`、`res.IsOk = result.Success ? result.IsPass : null`、`PlacedArc = placed`、孔質心存入 result 供 overlay。`res.Roi` 留 null。Pass 2 及後續排除 `pcd`（比照 arc/gear）。

### 5.4 MeasurementWorkflow 四判定（GD&T/gear 先例）
`else if (tool != null && tool.Pcd != null)` 分支（在 gear 之後、Tolerance 之前）：以 `tool.Pcd != null` 為鑰、成功發**四列**（孔數/PCD/角均勻/真圓度）、失敗發一列；永遠不落入 `GetMeasuredValue`。單位：孔數 count、PCD mm、角均勻 deg、真圓度 mm。OK/NG 工具層計一次（已含 audit #1 修正：量測失敗計 NG）。

### 5.5 RecipeEditor pcd 面板
「+ 螺栓孔圈」鈕 → `AddTool("pcd")`（預設 ArcRoi + `new PcdAnalysisParameters()`）。pcd 參數群（標稱孔數 int、標稱 PCD mm、PCD 公差 mm、角度公差 deg、徑向公差 mm、孔為暗 checkbox、最小孔面積 px）。重用 arc ROI 群組（擷取/環帶/互動編輯/試測）。隱藏雙邊公差群組。**`DeepCopyTool` 深複製 `Pcd`+`ArcRoi`**（出貨阻斷點）。`_updatingControls` guard 每條載入路徑。

### 5.6 MainWindow overlay
量測環帶 + **擬合的節圓**（`CenterRow/Col`、`PcdPx/2`）+ 各孔中心綠十字（`Holes[]`）+ 缺孔洋紅十字（`MissingHoleHintsDeg`）+ 標籤（`ValueText`）；`Roi` null 不畫退化框。角度→(row,col) 用全專案慣例 `row=cr+R·sinθ、col=cc+R·cosθ`。

### 5.7 Validator
`"pcd"` 加入 `KnownTypes`。per-tool 規則：`Pcd==null`→Error；`NominalHoleCount<=0 || 三公差<=0`→Error；`ArcRoi==null || !IsDefined`→Error。`"pcd"` 加入 `DoubleSidedToleranceTypes`？**否**——pcd 用四判定、不消費雙邊 Tolerance（比照 gear，維持 audit #10 一致）。

## 6. 測試

### Part A（`PcdAnalysisDomainTests`，合成質心全驗）
輔助：造 N 孔於圓心 (cr,cc)、半徑 Rpx 均分（angle=i·360/N）的質心，可擾動（缺一孔、一孔偏半徑、一孔偏角、整體壓成橢圓）。`pixelSizeUm=10`（→ 1px=0.01mm，PcdMm=PcdPx/100）。
- 完美 6 孔於 Rpx=250 → HoleCount 6、PcdPx 500、PcdMm 5.0、角偏差≈0、徑偏差≈0、PASS。
- 缺一孔 → HoleCount 5、CountOk false、FAIL、MissingHoleHints 有值且落在缺孔角度。
- 一孔偏半徑 +Δ → RadialOk false、其餘 OK。
- 一孔偏角 → AngularOk false。
- PCD 邊界：以「剛好內側/剛好外側」夾 `≤` inclusive（避 == 脆弱）。
- 失敗路徑：null、<3 孔、`NominalHoleCount<=0`、`pixelSizeUm<=0`、共線三點（退化擬合）→ Success false。

### Part B
`PcdRecipeToolDomainTests`：schema=9、round-trip `Pcd`+`ArcRoi`、向後相容（舊檔 Pcd=null）、validator 規則。GUI：合成螺栓圈影像（N 個暗孔於已知 PCD）+ 缺孔版。

## 7. §11.7-style 教訓檢查清單（gear/arc 已驗）

新工具進配方必查：`MeasurementWorkflow.GetMeasuredValue`（pcd 走專屬四判定分支、不可達 GetMeasuredValue）、`DeepCopyTool` 複製 `Pcd`+`ArcRoi`、Pass 2 排除、編輯器面板可見性/試測、overlay `Roi` 留 null、schema-version 測試連動、**量測失敗計 NG（audit #1 已修於 workflow 計數，pcd 自動受益）**。

## 8. 不做（YAGNI，v1）

- 各孔直徑/孔徑量測（只量節圓，不量單孔大小）。
- 非圓排列（線性孔陣列另為 pin/hole-array 方案）。
- 部分弧（半圈）螺栓圈——分析器假設環帶涵蓋整圈孔；ArcRoi 應設整圈。
- 多圈同心螺栓圈。
