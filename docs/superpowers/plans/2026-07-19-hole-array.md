# 孔陣列(孔位/孔徑陣列)量測方案 — 執行計畫

Phase 5 命名方案庫第四個(前有 gear、PCD、pin-pitch)。分支 `feature/hole-array`。
subagent-driven-development 逐 task:實作 → 建置/測試 → 審查 → commit。

## 設計決定(已敲定)
- `ToolType = "hole_array"`。schema **v13**。
- **排列**:矩形網格 rows×cols(使用者選定)。
- **幾何**:rect2 `tool.Roi`(同 pin_pitch,非 ArcRoi)。使用者拖矩形框住整個孔群。
- **偵測**:blob(比照 pin_pitch 的 `HalconPinDetector`,但**要多回傳孔徑**)。新 `IHoleArrayDetector.DetectHolesInRect(image, RoiGeometry, params)` + `HalconHoleArrayDetector`:`gen_rectangle2` → `reduce_domain` → `binary_threshold(HoleIsDark?"dark":"light")` → `connection` → `select_shape(area)` → `area_center`(質心)+ 面積 → **等效孔徑 = 2·√(area/π)**。`EnsureSingleChannel` 保護。
- **極性**:預設 `HoleIsDark=true`(亮底暗孔,與 pin_pitch 合成圖慣例一致);可配置。
- **分析**(純 Domain `HoleArrayAnalyzer`):
  1. 需 ≥2 孔、rows/cols ≥1。
  2. 對質心做 PCA 取主軸 u(單位向量),次軸 v = u 的垂直向量。
  3. 每孔投影 `a=p·u`、`b=p·v`。
  4. **分群**:把 a 排序,取最大的 (cols−1) 個間隙當分界 → cols 群;b 同理取 (rows−1) 個 → rows 群。(孔數不對時會退化,由孔數判定攔截。)
  5. `pitchU` = u 方向相鄰群心平均間距;`pitchV` 同理。cols==1 → pitchU=0 且該項不判定;rows==1 同理。
  6. **理想網格**:origin = 全孔質心;理想節點 `(i,j) = origin + (i−(cols−1)/2)·pitchU·u + (j−(rows−1)/2)·pitchV·v`。每孔依其 (群u, 群v) 索引對到理想節點,偏差 = 距離。取 `MaxPositionDevPx`。
  7. 孔徑:各孔等效孔徑平均 `MeanDiameter`、對均值最大偏差。
  8. px→mm 用 `pixelSizeUm`(比照 PcdAnalyzer)。
- **五判定**:①孔數(==rows·cols)②平均孔徑(±tol)③X 間距(±tol)④Y 間距(±tol)⑤最大位置偏差(≤tol)。`IsPass` = 全 OK。CSV 五列,比照 pcd/pin_pitch 自發 ItemJudgment。

## ⚠️ 關鍵接線清單(照 pin_pitch 的完整縱切鏡像)
RecipeRunner Pass + `ToolRunResult.HoleArray` 欄 + Pass-2 skip guard;MeasurementWorkflow 判定分支(**假 NG/PASS 陷阱**:分析失敗須 `Measured=false, IsOk=false`);RecipeValidator KnownTypes + 驗證;RecipeEditor 面板 + **DeepCopyTool 逐欄 clone**;MainWindow overlay + detector 注入;schema bump。

---

## Task 1 — Domain:DTO + 分析器 + 單元測試
`Domain/HoleArrayAnalysis/`:`HoleArrayPoint`(Row/Col/DiameterPx)、`HoleArrayAnalysisParameters`(Rows、Cols、NominalDiameterMm、DiameterToleranceMm、NominalPitchXMm、NominalPitchYMm、PitchToleranceMm、PositionToleranceMm、HoleIsDark、MinHoleAreaPx + `Default()`)、`HoleArrayAnalysisResult`(HoleCount、MeanDiameterMm、DiameterMaxDevMm、PitchXMm、PitchYMm、MaxPositionDevMm、List<HoleArrayPoint> Holes、CountOk/DiameterOk/PitchXOk/PitchYOk/PositionOk、IsPass、ErrorMessage、`Failed()`)、static `HoleArrayAnalyzer.Analyze(IList<HoleArrayPoint>, double pixelSizeUm, params)`。
**測試**:完美 3×4 網格→PASS;缺一孔→CountOk false;孔徑超差→DiameterOk false;某孔位移→PositionOk false;間距超差→PitchX/YOk false;**傾斜網格**(整體旋轉)仍 PASS(證明 PCA 軸處理);單列(rows==1)不判 Y 間距;<2 孔/pixelSize<=0→Failed。
- **verify**:`Tests.exe` 綠、印 `HoleArrayAnalysisDomainTests passed`。

## Task 2 — Application + Halcon 偵測器 + HALCON 合成圖實跑驗
`Application/HoleArrayDetection/IHoleArrayDetector.cs`、`Domain/HoleArrayDetection/HoleArrayDetectionResult.cs`、`Halcon/HoleArrayDetection/HalconHoleArrayDetector.cs`(rect2 blob + 等效孔徑)。
`TestImageGenerator.CreateHoleGridImage`(亮底 220、暗圓孔 30,rows×cols、可指定 missingIndex)。測試:實跑偵測 → 孔數正確、質心與孔徑近真值;反向極性不得同數。
- **verify**:`Tests.Halcon.exe` 全綠。

## Task 3 — 配方模型 + schema v13 + Validator + Runner Pass + round-trip 測試
鏡像 pin_pitch:`MeasurementTool.HoleArray` 欄、`Recipe` v13、Validator(KnownTypes + 驗證 rows/cols>0、nominal>0、tol>=0、Roi 非空;不加 DoubleSidedToleranceTypes)、RecipeRunner `_holeArrayDetector` 注入 + `ToolRunResult.HoleArray` + Pass + Pass-2 skip。
⚠️ **分析失敗必須 `Measured=false, IsOk=false`**(pin_pitch 教訓)。
**測試**:schema==13、預設 null、round-trip 全欄、舊 JSON→null、Validator 各錯誤案例。

## Task 4 — MeasurementWorkflow 判定分支(五判定,避假 NG/PASS)
`else if (tool.HoleArray != null)`:成功發五列、失敗發一列,絕不落入雙邊 else。

## Task 5 — RecipeEditor 面板 + DeepCopyTool
button、`_holeArrayGroup` + 控制項、Fill/Write/Load、AddTool(種 rect2 Roi + params)、可見性(歸 rect-drawing 路徑、隱藏 Tolerance 群組)、**DeepCopyTool 逐欄 clone**。

## Task 6 — MainWindow overlay + 合成 GUI 圖
overlay:rect2 ROI + 逐孔十字/圓 + 判定上色 + 數值文字。合成圖 `data/images/hole_grid_{ok,missing}.png` + groundtruth。

## Task 7 — 最終全鏈審查 + 交付驗證流程

## 驗收(使用者手動 GUI)
載入合成圖 → 加 hole_array 工具框住孔群 → 設 rows/cols/nominal → 一鍵量測 → overlay 逐孔標記 + CSV 五列真值與判定;缺孔圖 → 孔數列 NG。
