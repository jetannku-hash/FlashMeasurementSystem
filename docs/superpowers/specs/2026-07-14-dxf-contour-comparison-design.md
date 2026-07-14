# DXF/CAD 輪廓度比對 — 設計文件（v1）

> 狀態：設計已確認（2026-07-14），待 writing-plans 產出實作計畫。
> 淵源：Phase 2 曾評估「Option B — CAD/DXF master import」並延後（見 `2026-06-30-gsd-phase1-2-design.md` Locked Decisions）；本文件為該想法在應用庫層的 v1 落地，已登記於 `docs/ROADMAP.md` Phase 5。
> 所有 HALCON 算子簽章均對照離線 reference（`../../../halcon_pdf/reference/reference_hdevelop.txt`）驗證，非憑記憶。

## 1. 目標與價值

把量到的實際工件輪廓疊到 CAD/DXF 標稱輪廓上，計算輪廓度偏差並判 PASS/FAIL。v1 為**像素空間**比對，**不需硬體校正**——校正僅影響絕對 mm 準度，不影響像素/相對的合格判定。

## 2. 鎖定決策（brainstorm 2026-07-14）

| # | 決策 | 選定 |
|---|------|------|
| Q1 目的/輸出 | 輪廓度 **PASS/FAIL**：`max|偏差|`（+ 平均/RMS）對公差 T 判定，輸出統計 | 定 |
| Q2 對位 | DXF 自建 **scaled shape model** → `find_scaled_shape_model`，scale 吸收 mm→px | 定 |
| Q3 實際輪廓 | 對位後自動 `edges_sub_pix`（限制在標稱膨脹帶內）取整條 | 定 |
| Q4 整合 | **獨立動作**（獨立面板，不進配方工具清單/一鍵流程） | 定 |
| 偏差性質 | 無號距離、對稱公差帶 | 定 |
| 單位 | 像素空間；mm 僅顯示用（沿用量測分頁 pixel-size），非驗收準度 | 定 |

## 3. 範圍

### v1 做
- 讀 DXF 標稱輪廓、以 scaled shape model 定位、對位、自動取實際輪廓、算逐點偏差、輪廓度 PASS/FAIL、overlay、可選 CSV 一列。
- 獨立 UI 面板：選 DXF 檔、設公差 T、設 min score、執行、顯示結果與 overlay。

### v1 非目標（明確不做）
- 絕對 mm 計量準度驗收（需相機 + 校正片）。
- 有號偏差 / 缺料 vs 毛邊方向、非對稱公差帶 → v2。
- 缺料 / 缺特徵偵測（需雙向 nominal↔actual 距離）→ v2。
- 配方整合、一鍵流程、報表自動化 → 後續增量。
- 非 R12 DXF：`read_contour_xld_dxf` **只支援 AC1009 / AutoCAD R12**；其他版本以明確錯誤提示使用者另存 R12。
- 多零件 / 多獨立輪廓的分別判定。

## 4. 架構（沿用專案 feature-adapter 四塊 + 嚴格單向分層）

```
Domain  ←  Application  ←  Halcon  ←  App.Wpf
```

- **`Domain/DxfComparison/`**（純 DTO，無 HALCON/IO/UI）
  - `DxfComparisonParameters`：`TolerancePx`、`MinScore`、`ScaleMin`/`ScaleMax`（scale 搜尋範圍）、`ScaleSeedPxPerMm`（可選種子）、edge 參數（`Alpha`/`LowThreshold`/`HighThreshold`）、`BandWidthPx`（邊緣框帶寬）、`MinNumPoints`/`MaxApproxError`（DXF 曲線取樣）。含 `Default()`。
  - `DxfComparisonResult`：`Success`、`IsPass`、`MaxDevPx`、`MeanDevPx`、`RmsDevPx`、`PointsEvaluated`、`PointsOverTolerance`、`MatchScore`、`PoseRow`/`PoseCol`/`PoseAngleRad`/`PoseScale`、`Message`。
- **`Application/DxfComparison/IDxfContourComparer.cs`**：介面，僅用 Domain 型別 + 影像/DXF 路徑輸入。
- **`Halcon/DxfComparison/HalconDxfContourComparer.cs`**：唯一碰 `HalconDotNet` 之處。驗證輸入 → 執行資料流（§5）→ 映射 `DxfComparisonResult`；`HalconException` 轉 failed result（回傳 `Success=false` + Message，不外拋）；所有 `HObject`/`HImage`/`HTuple` 以 `using`/`finally` 釋放。
- **`App.Wpf`（WinForms）**：獨立面板（比照模板匹配 / IQC 的獨立動作模式）。選 DXF、設 T/min score、執行 → PASS/FAIL 橫幅 + 統計 + overlay（對位標稱 + 依偏差上色的實際邊）；可選寫 CSV。

## 5. 資料流（HALCON 管線，簽章已驗證）

### 5.1 載入標稱（換 DXF 時一次）
1. `read_contour_xld_dxf( : Contours : FileName, GenParamName, GenParamValue : DxfStatus )`（ref L46458）
   - GenParam：`min_num_points`（預設 20）、`max_approx_error`（預設 0.25 px）控 CIRCLE/ARC/ELLIPSE/SPLINE 取樣密度。
   - 檢查 `DxfStatus`：0 輪廓或含警告 → 回明確錯誤（可能非 R12 或實體不支援）。
   - 注意：x→Column、y→Row、z 忽略；座標為 DXF 原始單位（通常 mm）。
2. `create_scaled_shape_model_xld`（ref L113760）由標稱輪廓建 isotropic scaled 模型。

### 5.2 每次量測
3. `EnsureSingleChannel`（沿用既有慣例：3ch→`rgb1_to_gray`，其他→`access_channel(1)`）。
4. `find_scaled_shape_model`（ref L115459）→ `Row, Col, Angle, Scale, Score`；`Score < MinScore` 或無匹配 → failed result。
5. 由 `(Row, Col, Angle, Scale)` 組 `hom_mat2d`（identity → scale → rotate → translate）→ `affine_trans_contour_xld`（專案已在用）把標稱轉到影像 px = **對位標稱**。
6. **取實際輪廓（框帶濾雜訊）**：對「對位標稱」膨脹出寬 `BandWidthPx`（≈ 數倍 T）的區域 → `reduce_domain` → `edges_sub_pix`（ref L51434）→ 得實際邊，天然排除內部特徵/背景雜訊邊。
7. **算偏差**：`distance_contours_xld(實際, 對位標稱 : ContourOut : 'point_to_segment' : )`（ref L155816）→ `get_contour_attrib_xld(ContourOut, 'distance')` → 逐點偏差 tuple。
   - 方向：From=實際、To=標稱（量實際表面對標稱的偏離，輪廓度標準做法）。
8. **統計 + 判定**：`MaxDevPx = max`、`MeanDevPx = mean`、`RmsDevPx = sqrt(mean(d²))`、`PointsOverTolerance = count(d > T)`；`IsPass = MaxDevPx ≤ TolerancePx`。

## 6. 兩個關鍵設計細節（brainstorm 中驗證後補入）

### 6.1 scale 搜尋要種子
`find_scaled_shape_model` 的 scale 範圍全開（未知 mm→px）會又慢又不穩。v1 用**量測分頁既有 pixel-size 當 scale 種子**，把 `ScaleMin`/`ScaleMax` 收在種子附近（例如 ±30%）。這不是為計量準度，是為搜尋可行性——「免校正」指免**校正準度**，仍需一個**粗略 scale 種子**。pixel-size 未設時退回寬範圍（較慢）並於 UI 提示。

### 6.2 邊緣雜訊必須框帶
`edges_sub_pix` 會抓到內部特徵、背景與雜訊邊；若不限制，內部邊會被算成巨大偏差、污染 max。故 §5.2 步驟 6 必須把邊緣萃取限制在對位標稱的膨脹帶（`BandWidthPx`）內。帶太窄會漏真實大偏差、太寬會混入雜訊——`BandWidthPx` 預設 ≈ 數倍 T，並開放參數調整。

## 7. 錯誤處理與邊界

- DXF 讀取失敗 / 0 輪廓 / 疑似非 R12 → `Success=false` + 明確 Message（提示另存 R12）。
- 無匹配 / `Score < MinScore` → `Success=false` + Message（工件未定位，無法比對）。
- 框帶內取不到實際邊（0 contour）→ `Success=false` + Message。
- 所有 `HalconException` 在 adapter 內轉 failed result，不外拋到 UI（與專案既有 adapter 慣例一致）。
- 多通道影像 → `EnsureSingleChannel`。
- 資源：所有 HObject/HImage/HTuple/shape model 以 `using`/`finally` 釋放；shape model handle 成功路徑設 null 避免重複 clear（比照 `HalconTemplateManager`）。

## 8. 測試策略

- **Domain 全驗（console 套件 `DxfComparisonDomainTests`）**：Result 統計與判定純邏輯——max/mean/RMS 計算、T 邊界（=T 為 PASS，含邊界）、PASS/FAIL、空輸入、`Default()` 值。
- **Halcon adapter 手驗（合成，無硬體可做）**：造一個已知輪廓 DXF（R12）、渲染成影像、施加已知平移/旋轉/縮放與局部小變形，驗 `MaxDevPx` 落在預期範圍、PASS/FAIL 符合 T。
- 每步 `dotnet build … /p:Platform=x64` 0/0；Domain 測試綠。因觸及 HALCON，adapter 一律 x64 驗證。

## 9. HALCON 算子清單（已驗證，附 reference 行號）

| 算子 | ref 行 | 用途 | 關鍵注意 |
|------|--------|------|---------|
| `read_contour_xld_dxf` | L46458 | 讀 DXF→XLD | 只吃 AC1009/R12；x→col,y→row,z 忽略；`min_num_points`/`max_approx_error` 控取樣 |
| `create_scaled_shape_model_xld` | L113760 | XLD→scaled 模型 | isotropic；另有 aniso 版 L113160 |
| `find_scaled_shape_model` | L115459 | 影像中定位 | 回 Row/Col/Angle/Scale/Score；scale 需種子收斂 |
| `affine_trans_contour_xld` | (專案已用) | 標稱轉到影像 px | 由姿態組 hom_mat2d |
| `edges_sub_pix` | L51434 | 取實際輪廓 | 需先 `reduce_domain` 到框帶內 |
| `distance_contours_xld` | L155816 | 逐點偏差 | `point_to_segment`（準）；距離存 `distance` 屬性，`get_contour_attrib_xld` 取 |

## 10. 後續增量（v2+，非本 spec）

有號偏差 / 缺料 vs 毛邊、非對稱公差、雙向距離抓缺料/缺特徵、配方整合 + 一鍵 + 報表自動化、非 R12 自動轉換、多零件、mm 絕對準度（需硬體校正）、連續色階 heatmap 偏差圖。

## 11. 視覺化增補（v1.1，2026-07-14 GUI 驗收後追加）

v1 只有文字 PASS/FAIL、影像上無任何顯示 → 對專業量測儀不夠直覺。v1.1 補兩塊 on-image 視覺化（決策：1a ghost 預覽 + 兩色綠/紅；連續 heatmap 延 v2）。**Domain 層零改動**；純 `DxfComparisonResult` 與 `IDxfContourComparer` 介面不變。

### 11.1 載入 DXF 即時預覽（問題 1）
選完 DXF 就把**標稱輪廓**畫在主視窗上（ghost：淡色/虛線），讓操作員確認「檔案／形狀對不對」。未定位，僅作形狀確認——auto-fit 置中（依影像尺寸縮放標稱外接矩形到畫面約 60%），標「標稱預覽（未定位）」。

### 11.2 結果三色偏差圖（問題 2）
執行後在工件影像上疊：
- **藍**：對位後的標稱輪廓（應在位置）。
- **綠**：實際輪廓（`edges_sub_pix` 取得，框帶內）。
- **紅**：超差點（偏差 > T）以紅十字標出，數量比照 `MaxOverlayCrosses` 均勻抽樣上限避免壅塞。
文字統計（max/mean/超差點數）保留。缺陷（如 bump）會直接亮紅，一眼定位。

### 11.3 架構：具體 adapter 暴露 iconic、UI 繪製
`DxfComparisonResult` 不含 HALCON 物件（Domain 純淨），故 overlay 資料由**具體 `HalconDxfContourComparer`** 額外提供（UI 屬 App.Wpf、本就相依 HALCON）：
- `LoadNominalContour(dxfFilePath, parameters) → HObject`：讀 DXF 回標稱輪廓供預覽（失敗回 null）。呼叫端 dispose。
- `CompareWithOverlay(image, dxfFilePath, parameters, out HObject alignedNominal, out HObject actualEdges, out double[] overRows, out double[] overCols) → DxfComparisonResult`：管線同 §5，但額外回傳對位標稱、實際邊（iconic）與超差點座標。呼叫端 dispose iconic。
- 介面 `Compare(...)`（Application）維持不變：內部委派 `CompareWithOverlay` 並釋放 iconic，回傳純結果（DRY，介面契約不動）。

UI 側：
- `DxfComparisonForm` 改持有具體 `HalconDxfContourComparer`（MainWindow 本就以具體型別注入）。
- 預覽/結果 overlay 皆經既有 `SetPersistentOverlayAction` + `OverlayAnnotator`；`OverlayAnnotator` 增最小 helper：`DrawContour(HObject, color)`、`DrawContourFitted(HObject, imgW, imgH, color)`（ghost 置中）、紅十字（可重用既有 cross 繪製）。
- **生命週期**：Form 以欄位持有 iconic（預覽輪廓、對位標稱、實際邊），換檔/重跑/關閉時先 dispose 舊 iconic 再換；關閉表單 `ClearOverlay()`（沿用 deep-audit #3 旗標教訓，避免殘留與洩漏）。

### 11.4 驗證
用既有合成 fixture（`data/dxf/test_house.dxf` + `data/images/dxf_house_ok.png`/`_ng.png`）GUI 複驗：載入 DXF 見 ghost 預覽；OK 圖執行見藍+綠貼合、無紅、PASS；NG 圖見右側 bump 亮紅、FAIL。Domain 測試不受影響（零改動）。
