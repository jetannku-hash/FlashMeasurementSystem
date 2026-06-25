# ROI 完整互動編輯（rect2 滑鼠拖曳：移動／縮放／旋轉）實作計畫

- 日期：2026-06-24
- 範圍決策（已與使用者確認）：
  - **適用範圍**：MainWindow Edge Detection 分頁 **與** RecipeEditor 工具編輯器（共用 `HWindowControlHelper` 互動，資料綁定各自不同）。
  - **ROI 形狀**：只支援矩形 `rect2`（圓／線暫不納入）。
  - **縮放錨點**：對稱繞中心（中心固定、兩側等量變化）。
  - **模式切換**：畫完新框後自動進入編輯模式（顯示把手）。
- 狀態：**待使用者確認**，確認後才實作。

---

## 0. 問題與現況

目前 ROI 角度只能用 NumericUpDown 改，滑鼠無法旋轉。根因：

- `HWindowControlHelper` 只用「起點/終點兩角」描述正交框（`_roiStartRow/_roiStartCol/_roiEndRow/_roiEndCol`），`GetCurrentRoi()` 回傳 `min/max` bounding box，**會遺失旋轉資訊**。
- 滑鼠互動只有：右鍵 pan、滾輪 zoom、左鍵在 `IsRoiMode` 下拖兩角畫正交框（`OnMouseDown/Move/Up`，`HWindowControlHelper.cs:181-239`）。
- 角度（rect2 的 `phi`）只能由數值框輸入：
  - MainWindow：`_edgeAngleNumeric`（度）→ `CreateEdgeDetectionRoiFromNumeric` 以 `GetCurrentRoi()` 的 bbox 中心 + 數值角度重建 `EdgeDetectionRoi`。
  - RecipeEditor：`_angleRadNumeric`（弧度）→ `WriteRoi()` 寫入 `tool.Roi.AngleRad`。
- 顯示用 `OverlayAnnotator.DrawRectangle2(row,col,phi,l1,l2)`，採 **`+phi`（不取負）** 慣例，與 `gen_measure_rectangle2` / `measure_pos` 完全一致（`OverlayAnnotator.cs:20-32` 註解強調過，先前傳 `-phi` 造成顯示框與量測框鏡像的 bug）。

---

## 1. 核心設計：互動層擁有「可編輯 rect2」，host 鏡像

讓 `HWindowControlHelper` 在編輯期間擁有一個真正的 rect2；host（MainWindow / RecipeEditor）的數值框與 domain ROI 只是鏡像。拖曳期間的單一真相在 helper，確保把手與數值框永遠一致，兩個 host 共用同一套互動程式碼。

### 1.1 Helper 新增狀態

```
private double _editCenterRow, _editCenterCol, _editPhi, _editLen1, _editLen2;
private bool _editActive;
private enum EditMode { None, Move, ResizeLen1, ResizeLen2, ResizeCorner, Rotate }
private EditMode _editMode;
private bool _syncingFromMouse; // 防遞迴（host 端另有 _updatingControls）
```

### 1.2 雙向合約

- **host → helper**
  - `void BeginRect2Edit(double cr, double cc, double phi, double l1, double l2)`：設定/取代可編輯框並進編輯模式（`_editActive = true`），重繪。
  - `void EndRect2Edit()`：`_editActive = false`，清除把手，重繪。
- **helper → host**
  - `event Action<double,double,double,double> Rect2Changed`：滑鼠每次改動（拖曳中即時 + MouseUp）發出 `(centerRow, centerCol, phi, l1, l2)`。

兩端各以旗標擋事件迴圈：helper 用 `_syncingFromMouse`，host 用既有 `_updatingControls` / `_updatingEdgeRoiControls`。

---

## 2. 互動狀態機（`HWindowControlHelper`）

### 2.1 命中優先序（`OnMouseDown`，左鍵且 `_editActive`）

全部在影像座標進行，容差由螢幕像素換算（見 §4）：

1. **旋轉把手**（長軸 +e1 端外側圓鈕）→ `Rotate`
2. **角把手**（4 角小方塊）→ `ResizeCorner`（同時改 len1、len2）
3. **邊中點把手**（4 邊）→ `ResizeLen1` 或 `ResizeLen2`（只改一個）
4. **框內部**（點在 rect2 內）→ `Move`
5. 其他 → 不動作

不影響既有行為：右鍵維持 pan；`IsRoiMode` / `RequestRoi` 畫新框流程不變（畫完才進編輯）。

### 2.2 拖曳換算（`OnMouseMove`，對稱繞中心）

- **Move**：`center += (Δrow, Δcol)`（以影像座標位移）。
- **Resize**：把「滑鼠相對中心向量」投影到本地軸 `e1`(長軸)、`e2`(短軸)：
  - 角把手：`len1 = max(minLen, |proj_e1|)`、`len2 = max(minLen, |proj_e2|)`
  - 邊中點：只改對應的 `len1` 或 `len2`
  - 中心不動（對稱）。
- **Rotate**：`phi = atan2(-(row - cr), col - cc)`（與顯示同慣例，見 §3）。

每次改動更新 `_edit*` 並發 `Rect2Changed`（拖曳中即時發以即時預覽）。

### 2.3 `OnMouseUp`

結束 `_editMode = None`，最後再發一次 `Rect2Changed`。

### 2.4 游標回饋（可選，不阻塞）

hover 命中時切換 `Cursor`：Move→`SizeAll`、Resize→雙箭頭、Rotate→`Hand`，提升可發現性。

---

## 3. 座標慣例（最大風險，鎖死方式）

把手位置、旋轉角、顯示框**必須用同一組本地軸向量**，沿用既有 `DrawRectangle2` 的 `+phi`（row 向下）慣例，否則重蹈「顯示／量測鏡像」舊 bug。

實作步驟：

1. 從離線參考 `halcon_pdf/reference/`（`gen_rectangle2` / `disp_rectangle2`）確認角點公式，鎖定 `e1 = (-sinφ, cosφ)`、`e2` 的精確正負號（**不靠記憶**）。
2. 用「把手必須剛好落在 host 畫出的矩形角／邊上」做目視驗證；斜框（phi≠0）對齊邊緣後，量測方向需一致。
3. 旋轉公式 `atan2(-(Δrow), Δcol)` 與 `e1` 同源，天然一致。

> 唯一待實作中查證、不阻塞計畫的點：rect2 軸向量精確正負號（離線參考 + 把手對齊目視驗證）。

---

## 4. 把手繪製（`OverlayAnnotator`）

新增：

```
public void DrawRect2Handles(double cr, double cc, double phi,
    double l1, double l2, double handleImgSize)
```

- 4 角 + 4 邊中點：畫固定大小小方塊（`disp_rectangle2` 填色）。
- 長軸 +e1 端：畫旋轉圓鈕 + 連接桿。
- **只畫把手不畫框本體**——框本體已由 host overlay（`DrawRectangle2`）畫出，避免雙框。

縮放不變大小：helper 新增 `double ScreenPxToImage(double px) => px * (_imgCol2 - _imgCol1) / _control.Width;`，把「固定螢幕像素」換算成影像像素，供 `handleImgSize` 與命中容差使用，確保縮放後把手大小／命中範圍恆定。

helper 在 `Redraw()` 末端、`_editActive` 時（於 persistent overlay 之後）呼叫 `Annotator.DrawRect2Handles(...)`，畫在最上層。

---

## 5. Host 接線

### 5.1 MainWindow（Edge Detection 分頁）— `MainWindow.cs`

- 取代現有「以 `GetCurrentRoi()` bbox + 數值角度」重建 ROI 的路徑（會遺失旋轉）。新增 `_editCenterRow/_editCenterCol` 暫存；rect2 由 helper 當真相。
- `RoiSelected`（畫完新框）：以 `EdgeDetectionRoi.FromBounds` 取得 center/phi/l1/l2 → `BeginRect2Edit(...)`（**自動進編輯**）+ seed 數值框。
- 訂閱 `Rect2Changed`：guarded 更新
  - `_edgeAngleNumeric = phi*180/π`
  - `_edgeScanLengthNumeric = 2*l1`
  - `_edgeRoiWidthNumeric = 2*l2`
  - `_latestEdgeRoi = EdgeDetectionRoi.FromCenter(cr,cc,l1,l2,phi)`
  - `ShowFittingOverlay()`
- `OnEdgeRoiNumericChanged`（手打數值）：以 `_editCenter*` + 數值換算 → `BeginRect2Edit(...)` 同步把手。

### 5.2 RecipeEditor（工具編輯器）— `RecipeEditor.cs`

- 選取工具：讀 `tool.Roi`（`RoiGeometry`，弧度）→ `BeginRect2Edit(...)` 顯示把手。
- 訂閱 `Rect2Changed`：寫回 `tool.Roi.{CenterRow,CenterCol,AngleRad,Length1,Length2}` + guarded 更新數值框（弧度）+ `MarkDirty()` + 重繪。
- 既有 `_angleRadNumeric.ValueChanged → WriteRoi()`：`WriteRoi()` 末端加 `BeginRect2Edit(...)` 同步把手。

---

## 6. 可驗證性

- 把易錯的純幾何抽成 static `Rect2EditMath`（投影到本地軸、夾下限、`phi`↔把手位置、命中判定），方便單元測試且不汙染互動層。
- 新增 console 測試套 `Rect2EditMathTests`，`Run()` 接進測試專案 `Main()`（符合既有「console 式測試」模式）。
- GUI 拖曳手感手動驗證。

---

## 7. 影響檔案

改：
- `src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs`（編輯狀態、命中、轉換、把手繪製呼叫、`BeginRect2Edit`/`EndRect2Edit`/`Rect2Changed`、`ScreenPxToImage`、游標）
- `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`（`DrawRect2Handles`）
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`（rect2 真相改線、訂閱 `Rect2Changed`、數值雙向同步）
- `src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`（同上，綁 `RoiGeometry` 弧度）

新增（需加 `<Compile Include>`，old-style csproj 不 glob）：
- `Rect2EditMath.cs`
- `tests/.../Rect2EditMathTests.cs`（並把 `Run()` 接進 `Main()`）

不需改：Designer（無新控制項）；右鍵 pan、`IsRoiMode` 畫新框流程不變。

---

## 8. 驗證步驟

```powershell
# 建置（動到 HALCON 顯示 → Any CPU 與 x64 都要）
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64

# 測試
.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
```

GUI 手動驗收清單：
- [ ] 畫框後自動出現把手（角／邊／旋轉鈕）
- [ ] 拖框內部可移動
- [ ] 拖角把手同時改 len1/len2、拖邊把手只改一個，皆對稱繞中心
- [ ] 拖旋轉鈕可改角度，把手與框同步轉
- [ ] 滑鼠改動即時回寫數值框；手打數值即時移動把手（無事件迴圈）
- [ ] 斜框（phi≠0）對齊邊緣後，量測方向一致（不鏡像）
- [ ] 縮放後把手大小／命中範圍維持恆定
- [ ] MainWindow 與 RecipeEditor 兩處皆可用

---

## 9. 風險摘要

| 風險 | 對策 |
|---|---|
| HALCON 視窗無 adorner，需自製影像座標 hit-test | 容差用 `ScreenPxToImage` 換算，縮放恆定 |
| `phi` 慣例陷阱（鏡像舊 bug） | 把手/旋轉/顯示同一組軸向量，離線參考鎖符號 + 目視驗證 |
| 數值框 ↔ 滑鼠雙向同步事件迴圈 | helper `_syncingFromMouse` + host `_updatingControls` 旗標 |
| 滑鼠按鍵衝突（右鍵 pan、左鍵畫框） | 命中優先序明確；畫新框維持既有入口 |
| `GetCurrentRoi()`/`HasRoi` 既有消費者改線後失效 | 改線時審視所有消費點，rect2 真相取代 bbox 路徑 |
