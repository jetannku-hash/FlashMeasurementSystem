# Deep Audit — 2026-07-01

4-agent 平行審查（HALCON 層 / Domain+Application / WinForms UI / Infrastructure+Reporting+Mes）。
現況：24 測試套件全綠；git 乾淨已 push（`origin/main..HEAD = 0`）；Domain/Application 零分層違規；無 Critical。
**本輪僅出報告，未動手修改。**

## 值得處理的 BUG（依嚴重度 + 驗證信心）

| # | 嚴重度 | 位置 | 問題 | 信心 |
|---|--------|------|------|------|
| 1 | High | `App.Wpf/MainWindow.cs:2117-2149`（`OnImageRoiSelected`） | 畫完邊緣 ROI 未 seed `_editCenterRow/_editCenterCol`（宣告 :54，只在拖曳回呼 `OnEdgeRect2Changed` :2154 寫入）。先改數值框（未先拖把手）→ `OnEdgeRoiNumericChanged` :1092 用中心=0 重建 → ROI 跳到 (0,0) 量錯區域。修法：在 :2147 前 seed 兩個欄位=`_latestEdgeRoi.CenterRow/Col`。 | ✅ 已讀碼驗證 |
| 2 | High | `Halcon/TemplateMatching/HalconTemplateManager.cs:19-32`、`HalconTemplateMatcher.cs:61,69` | `create_shape_model`/`find_shape_model` 需單通道，這兩個 adapter 未做 `rgb1_to_gray`/`access_channel`（其他 adapter 都有）。載入彩色圖 → 建模丟例外 / 每個 part 回「模板匹配錯誤」。 | 高 |
| 3 | Med | `App.Wpf/RecipeEditor.cs:646`（`OnTrialMeasure`）/ `:132-137`（FormClosed） | 試測 overlay 裝在**共用**主視窗 helper；關閉只 `EndRect2Edit`+`ClearSelectionHighlight`，未 `ClearOverlay()` → 殘留橘 ROI+綠擬合+值，且保留已 Dispose 編輯器捕獲物件引用。 | 高 |
| 4 | Med | `Halcon/CoordinateSystem/HalconCoordinateMapper.cs:27,55`；`HalconTemplateMatcher.cs:27`（LoadModel）、`:114-135`（GetMatchContour） | HALCON 例外未轉 failed result 直接逸出（與全層「回傳失敗結果」慣例相反）→ 壞 `.shm`/退化姿態可 crash UI。 | 高 |
| 5 | High* | `Domain/Gdt/GdtCalculator.cs:46-66`；`App.Wpf/RecipeRunner.cs:660-666` | 平行/垂直度公差帶 = `擬合線長 × sin(角)`，用 ROI 繪製長度而非真實特徵長 → 同傾斜、ROI 畫多長判定不同。**語意題非純 bug**：平行度本質長度相依；真正問題只在「用哪個長度」。閃測儀操作員會沿特徵畫 ROI，故偏向「確認語意 + 提示畫滿特徵」。 | ⚠️ 保留 |
| 6 | Med | `Domain/Geometry/GeometryConstruction.cs:24`（`TryLineIntersection`）、`:90`（`Midline`） | `denom` 用未正規化 segment deltas 對固定 `1e-9` 判平行，非 scale-invariant → 近平行長線（如 0.01°）判成相交、交點飛到畫面外。正解：正規化方向、以 `sin θ` 設閾值。 | 中高 |
| 7 | High* | `Infrastructure/Calibration/CalibrationStore.cs:50` + `Domain/Calibration/CalibrationProfile.cs:16-17` | `PixelSizeUmX/Y` 有初始值 `=10.0`；Newtonsoft `MissingMemberHandling.Ignore` + Load 無驗證（SchemaVersion 從不檢查、無正值/範圍檢查）→ 缺欄位校正檔靜默載入為 10µm/px，下游尺寸全被錯縮放且無警告。 | 中（需手改檔觸發，衝擊大） |

## Low / 次要
- `Halcon/HalconEdgeDetector.cs:757`（`TryGetChannels`）吞例外預設 1 → 極端下可繞過單通道保護、`measure_pos` 靜默回 0 邊。
- `HalconEdgeDetector.cs:161-162` edge_pair fallback 直接索引 `RawSecondRows/Cols` 未 length guard（靠 `measure_pairs` 不變式）。
- `DetectEdgesOnArc`（:219-322）省略了 `DetectEdges` 的 ROI 出界預檢診斷。
- `App.Wpf/MainWindow.cs`：`OneClickMeasureButton_Click`（:1540）finally 未 `ClearProgress()`（狀態列卡住）；`RoiModeCheck_CheckedChanged`（:522）缺 `_imageHelper!=null` guard（latent NRE）；`RefreshMatchContour`（:515）catch 全 Exception 吞掉；ToolTip 未 dispose（`:280`、RecipeEditor `:1403`）；`HWindowControlHelper` `OnMouseWheel`（:298）未 clamp zoom、`PixelToImage`（:450）無 0-size guard；`OnMouseUp`（:433）ROI 門檻 `>5 AND` 丟棄細長 ROI。
- `Infrastructure`：RecipeStore/CalibrationStore 新 schema 被舊碼開啟 → 未知欄位靜默丟失、Save 覆寫永久掉資料（`RecipeStore.cs:51`）；atomic-write 失敗遺留 `.tmp`；`CsvMeasurementReportWriter.cs:31` 檔存在但空/無 header 時後續 append 無 header 行。
- `Domain/Tolerance/ToleranceJudger.cs`（在 Infrastructure）：零寬容差 `Upper==Lower` 未擋（GD&T 有擋 `T<=0`，尺寸路徑無對等）；`:110` NG 訊息印 Deviation 而非「低於下限量」。
- `Domain/AngleMeasurement/AngleNormalizer.cs:47` ±90° 邊界單邊映射到 +90。
- 測試 binary 略舊於 source（3 套件重複輸出）；Mes 專案空殼（ROADMAP 已列 Deferred，符合預期）。

## 一致性債（無硬體暫無真值）
- anisotropic 像素：距離用 X/Y 分開，但圓直徑（`RecipeRunner.cs:368`）與 GD&T mm（:688）塌成單一 `pixelSizeUm` → 非方形像素下兩路徑 mm 不一致。等校正片到位再統一。

## 已驗證乾淨（非發現）
HALCON 資源釋放紀律完整、operator 參數序/最小點數正確、`measure_pos` Distance 少一元素有防禦索引、無跨執行緒 HALCON、CSV 逃逸+公式注入+NaN/Inf 已處理、culture 全 InvariantCulture、ToleranceJudger 攔 NaN/Inf/反向容差、失敗量測不漏 0.0 進判定、value-object Default() 無共享可變別名。

## 建議修復批次（若日後動手）
- **批 A（純軟體、可全驗、低風險）**：#1、#3、#6、#4；Low 中的 OneClickProgress、ToolTip、零寬容差。
- **批 B（需真實/彩色影像手驗）**：#2 彩色模板、#7 校正檔驗證。
- **保留待決**：#5 GD&T 語意（先確認要用真實特徵長還是 ROI 長）、anisotropic 一致性（等校正片）。

---

## 執行記錄（2026-07-14，分支 feature/batch-a-audit-fixes）

### 批 A 執行結果
- **已修**：#1（已讀碼驗證真 bug，seed `_editCenterRow/Col`）、#6（正規化外積 sinθ 判平行，scale-invariant + 退化守衛 + 回歸測試）、#3（RecipeEditor `_editorInstalledOverlay` 旗標，關閉時 `ClearOverlay()`，使用者選「完全清除」）、Low ×3（一鍵 `ClearProgress()`、`RoiModeCheck` null guard、ToolTip dispose）。
- **查證後剔除（非缺陷，改了更糟）**：
  - **#4 例外邊界**：四個呼叫端全部已在邊界 catch（`MeasurementWorkflow.cs:178`、`MainWindow.cs:1375` 註解明確、LoadModel/GetMatchContour 皆有）。無實際 crash 路徑；改成「回傳失敗並繼續」會把計量系統的「大聲失敗」變成「靜默用未變換錯誤座標量測」，更危險。維持現狀。
  - **零寬容差**：`ToleranceDomainTests.cs:94-101` 已鎖定「零寬+命中=OK」為刻意設計（整數計數合法），現行 NG 訊息不誤導；與 GD&T 擋 `T<=0`（幾何無意義）本質不同。改了會破壞既有測試。維持現狀。

### 批 B 執行結果
- **#2**：`HalconTemplateManager.CreateAndSave` + `HalconTemplateMatcher.FindMatches` 加 `EnsureSingleChannel`（3ch→`rgb1_to_gray`，其他→`access_channel(1)`），沿用 `HalconImageQualityChecker` 慣例；轉換圖 `finally` 釋放。**需彩色圖手驗**。
- **#7**：`CalibrationStore.Load` 改用 `JObject` 檢查 `PixelSizeUmX/Y` 鍵存在 + 正有限值，否則擲例外；不再讓缺欄位靜默落回 `10µm/px`。Domain `CalibrationProfile` 保持純淨。加 6 個回歸測試（`{}`/缺鍵/0/負 → 擲例外；8/12µm 正常載入）。

### ⚠️ 並發修改事故（重要教訓）
- **情境**：本 session 開始時 `git status` 乾淨。執行批 B 後發現工作樹多出 3 處**非本 session 所寫**的改動——來自 `/resume` 提到、**仍在背景執行的上一個 session agent**，它併發在同一批檔案做重疊的稽核修正。
- **stray 改動**：①`HWindowControlHelper.GetPersistentOverlayAction()`；②`MainWindow.cs ~1678` 開/關編輯器 overlay「保存並還原」（#3 的**還原**方案，即使用者**否決**的做法）；③`MainWindow.cs ~1514` `ResolvePixelSize` 的 `_loadedRecipe != null &&` null guard。
- **語意衝突**：agent 的 overlay 還原透過 `editor.FormClosed`（在 MainWindow 後訂閱、後執行）壓過 RecipeEditor 建構子內先訂閱的 `ClearOverlay()` → 實際行為變成「還原」而非使用者選的「完全清除」。建置能過（共存不衝突編譯），但語意被覆蓋。
- **處置**（使用者選「保留完全清除、撤掉 agent 改動」）：撤除 ①②（`HWindowControlHelper` 回 HEAD、移除 MainWindow 還原區塊）。
- **刻意偏離使用者「全撤」指示的一處**：③`ResolvePixelSize` 的 null guard **保留**。查證確認它修的是**可觸發真 crash**（載入影像→開 RecipeEditor 未載配方→加 circle→按試測 → `ResolvePixelSize` 用 `_loadedRecipe.CalibrationProfileId`，`_loadedRecipe==null` 時 NRE）。還原 = 重新製造崩潰，故不還原並向使用者說明可反悔。
- **教訓**：多 agent／多 session 併發於同一 repo 時，**動手前後都要 `git status`/`git diff` 核對每個 hunk 的歸屬**；發現非自己所寫的改動先停、surface、確認來源已停止再清理；即使使用者說「全撤」，撞到「還原會製造已驗證 crash」時要 push back 而非盲從。

### 驗證
- x64 建置 0 warning／0 error；24 測試套件全 passed、exit 0（含 #6、#7 新回歸測試）。
- 待手驗：#1/#3/Low（GUI）、#2（彩色圖建模/匹配）。詳見 session 對話的 GUI 驗收清單。
