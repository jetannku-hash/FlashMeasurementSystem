# GUI 建議優化項目計畫書

> 建立日期：2026-06-25
> 目的：提升 FlashMeasurementSystem 操作人性化與上手難度，依「好上手 CP 值」排序。
> 參考來源：KEYENCE IM 系列「Place & Press」哲學、HDevelop Measure Assistant、MVTec MERLIC / Mech-MSR wizard UX、WinForms 佈局實務。
> 狀態：**待審閱**。審閱通過後逐項實作，每項一個小 commit + build/test 驗證。

---

## 0. 重要前提（已對照實際程式碼查證）

| 查證項 | 結論 | 對計畫的影響 |
|--------|------|------------|
| RecipeEditor 啟動方式 | `editor.Show(this)` **非模態**，與 MainWindow **共用同一個 `HWindowControlHelper`**（`RecipeEditor.cs:30,98`） | 編輯器已能即時把 ROI 畫在主視窗影像上 → 項目 A1（試測）成本大降 |
| 公差輸入方式 | RecipeEditor 已是 **Nominal ± Lower/Upper** 友善輸入（`_nominalNumeric/_lowerNumeric/_upperNumeric`） | 不需再做「公差輸入改善」 |
| 配方驗證 | **完全不存在**（grep 無 `Validate`） | 新增 N1，列為最高實質價值 |
| 工具列表控制項 | `ListBox _toolListBox`（`RecipeEditor.cs:56`） | N? TreeView 升級有對象 |
| Undo/Redo | 只有單一 `bool _dirty`，無歷史 | 項目 A5 成立 |
| 一鍵結果顯示 | 只有 `measureResultLabel`（有顏色的文字） | 新增 N2 大字橫幅 |

### 撤回項目
- **配方建立精靈（Wizard）**：原列為建議2。查證後認定對單一操作員場景**過度設計**；RecipeEditor 已是堪用的建立面板。改以 **N3 空狀態引導**取代（70% 引導價值、10% 成本）。

---

## 1. 總覽（依建議執行順序）

> **狀態欄於 2026-07-20 依實際程式碼逐項查證更新**（此表先前未同步，多數項目其實早已交付）。

| 順序 | 編號 | 項目 | 工時 | 風險 | 類型 | 狀態（2026-07-20 查證） |
|------|------|------|------|------|------|------|
| 1 | N3 | 空狀態工作流引導 | ~1hr | 低 | 新增 | ✅ 已交付（`MainWindow.UpdateEmptyState`，三步驟引導 + 灰底「—」橫幅） |
| 2 | N2 | 大字 PASS/FAIL 橫幅 | ~1hr | 低 | 新增 | ✅ 已交付（綠 PASS／紅 FAIL（NG n）） |
| 3 | N5 | 編輯器即時顯示公差上下限 | ~30分 | 低 | 新增 | ✅ 已交付（`_tolerancePreviewLabel`，含「⚠ 上限 < 下限」警示、純顯示不擋存檔） |
| 4 | N1 | 配方驗證（Pre-run diagnostic） | ~3-4hr | 低-中 | 新增 | ✅ 已交付（`RecipeValidator` + Run/一鍵閘門，merge 8abaa99） |
| 5 | A1 | 編輯器內試測 | ~1-2hr | 中 | 改善 | ✅ 已交付（RecipeEditor「在此試測」，`RefreshTrialButtonEnabled` 控制可用型別） |
| 6 | N6 | 量測快捷鍵 + 拖放影像 | ~30分 | 低 | 新增 | ❌ **未做**（`MainWindow` 無 AllowDrop／DragDrop，也無 KeyPreview／ProcessCmdKey） |
| 7 | A3 | 即時邊緣預覽 | ~1-2hr | 低-中 | 改善 | 🔶 **部分**：弧形卡尺已有即時預覽（見 memory `arc-caliper-usability-improvements`）；通用邊緣預覽未確認 |
| 8 | A5 | Undo/Redo | ~2-3hr | 中 | 改善 | ❌ **未做**（RecipeEditor 無 undo/redo） |
| 9 | N7 | 最近開啟的配方/影像 | ~1-2hr | 低 | 新增 | ✅ 已交付 |
| 10 | A4 | 工具列表 TreeView | ~2-3hr | 中 | 改善（選配） | ❌ 未做（原即標為選配；工具列表仍為 ListBox） |
| 11 | A6 | Tooltip 內容加強（edge 參數） | ~15分 | 低 | 改善 |
| 12 | A7 | SplitContainer 可調佈局 | ~10分 | 低 | 改善 |

---

## 2. 逐項詳述

### N3 — 空狀態工作流引導

**目的**：新使用者開啟軟體時，影像區一片空白、不知從何開始。給出明確的三步引導。

**具體做法**：
- 在主視窗影像顯示區（HWindowControl 上方或之上）疊一個半透明 WinForms `Label`/`Panel`。
- 當「無影像」或「無配方」時顯示：
  ```
  ① 載入影像（Load Reference / Test Image）
  ② 載入或建立配方（Load / Edit Recipe）
  ③ 按一鍵量測（One-Click）
  ```
- 載入影像後自動隱藏；可依狀態動態更新（例如已載入影像但未載配方 → 只亮 ②③）。

**選項**：
- **選項 A（建議）**：用 WinForms `Label` 疊在影像區（`Visible` 切換）。**理由**：無影像時 HALCON 視窗是空的，WinForms label 不依賴 HALCON、最簡單可靠。
- 選項 B：用 HALCON `WriteString` 畫在 HWindow 上。缺點：無影像時要先開一個空 HALCON 視窗，較麻煩。

**影響檔案**：`MainWindow.Designer.cs`（加 Label）、`MainWindow.cs`（依 `_imageHelper.CurrentImage`/`_loadedRecipe` 切換 `Visible`）。

**風險**：低。純疊加顯示，不動量測邏輯。

**驗證操作**：
1. 啟動 app（不載入任何東西）→ 影像區應顯示三步引導。
2. 載入影像 → ① 應消失/變灰，引導更新。
3. 載入配方 → 引導完全消失。
4. 關閉影像（若有此操作）→ 引導重新出現。

---

### N2 — 大字 PASS/FAIL 橫幅

**目的**：目前一鍵/Run Recipe 的結果只在 `measureResultLabel`（小文字）。操作員需要遠看、一眼可辨的大面積綠/紅 PASS/FAIL。

**具體做法**：
- 加一個固定高度（~50-70px）的 `Label`（或 `Panel` + `Label`），大字體、置中。
- 一鍵/Run Recipe 完成後：全 OK → 綠底白字「PASS」；有 NG → 紅底白字「FAIL（NG n）」；未量測 → 灰底「—」。

**選項**：
- **選項 A（建議）**：固定在主視窗頂部或結果分頁上方的 Designer `Panel`。**理由**：永遠可見、不受影像縮放影響、實作最單純。
- 選項 B：用 `OverlayAnnotator` 畫在影像 HUD（已有 `DrawResultTable` 基礎）。缺點：影像平移縮放時要重畫、且影像未載入時看不到。

**影響檔案**：`MainWindow.Designer.cs`（加 Panel/Label）、`MainWindow.cs`（`OneClickMeasureButton_Click`、`DrawRecipeResults` 結尾依 okCount/ngCount 設定）。

**風險**：低。

**驗證操作**：
1. 載入會 PASS 的配方+影像 → 一鍵量測 → 橫幅應大字綠「PASS」。
2. 故意讓一個工具超出公差（改 nominal）→ 橫幅應紅「FAIL（NG 1）」。
3. 換圖未量測 → 橫幅回到灰「—」。

---

### N5 — 編輯器即時顯示公差上下限

**目的**：RecipeEditor 輸入 Nominal/Lower/Upper 時，操作員不易心算實際 [下限, 上限]；且反向公差（Upper<Lower）目前只在 Run 時被擋（P1 已修），最好在**輸入當下**就提示。

**具體做法**：
- 在 `_upperNumeric` 下方加一個 `Label`，三個 numeric 任一變動（已有 `WriteTolerance` ValueChanged）時即時更新：
  ```
  = [49.9900, 50.0100] mm
  ```
- 若 `UpperLimit < LowerLimit` → label 轉紅、顯示「⚠ 上限 < 下限」。

**選項**：
- **選項 A（建議）**：唯讀 Label 即時計算顯示。**理由**：零風險、純顯示、就地防呆。
- 選項 B：直接擋住存檔（Upper<Lower 不准 save）。缺點：較強硬，可能干擾暫存中間狀態；建議只警示不阻擋。

**影響檔案**：`RecipeEditor.cs`（`WriteTolerance` 末端更新 label；新增一個唯讀 Label）。

**風險**：低。

**驗證操作**：
1. 開編輯器、選一個工具 → 改 Nominal=50、Lower=-0.01、Upper=0.01 → label 應顯示 `= [49.9900, 50.0100]`。
2. 把 Lower 改成 +0.02（>Upper）→ label 應轉紅顯示警告。
3. 改回正常 → 紅色消失。

---

### N1 — 配方驗證（Pre-run diagnostic）

**目的**：Run 前列出配方所有問題，新手不必靠 Run 失敗反推。對應 KEYENCE「programming 階段診斷每個量測點」。

**具體做法**：
- 新增 **Domain 層** `Domain/Roi/RecipeValidator.cs`（純邏輯、無 HALCON，符合架構、可單元測）。
- `Validate(Recipe recipe, int imageWidth, int imageHeight)` → 回傳 `List<RecipeIssue>`，每筆含嚴重度（Error/Warning）+ 訊息 + 關聯 ToolId。
- 檢查項：
  - 工具 ROI 為 null 或 Length<=0
  - ROI 中心/範圍超出影像邊界
  - distance/angle 的 `RefToolIds` 解析不到、或引用了錯型別（distance 引用了 distance）
  - `Tolerance` Upper < Lower（反向）
  - `HasReferencePose=true` 但 RefPose 全 0 / 未設模板
  - 配方零工具
- UI：RunRecipe / 一鍵前自動跑；有 Error → 彈出問題清單；只有 Warning → 顯示但允許繼續。RecipeEditor 也可加 `[檢查配方]` 按鈕。

**選項**：
- **選項 A（建議）**：Error 阻擋 + Warning 放行。**理由**：硬錯誤（NRE 來源）擋掉、軟問題（如近平行）只提醒，平衡安全與彈性。
- 選項 B：全部只警示、一律放行。缺點：硬錯誤仍會讓 Run 失敗。
- 選項 C：全部阻擋。缺點：太強硬，妨礙實驗性操作。

**影響檔案**：新增 `Domain/Roi/RecipeValidator.cs` + `RecipeIssue.cs`（+ csproj Compile）；`MainWindow.cs`（Run/一鍵前呼叫）；新增 `tests/.../RecipeValidatorTests.cs`（+ csproj + Main wiring）。

**風險**：低-中（純邏輯低風險；UI 串接需小心不影響既有 Run 流程）。

**驗證操作**：
1. **自動測試**：`RecipeValidatorTests`（缺 ROI、超界、壞 RefToolId、反向公差、零工具各一案例）→ 跑 `Tests.exe` 應 `RecipeValidatorTests passed`。
2. **GUI**：載入 `test_err.zcp`（含缺欄位工具）→ Run Recipe → 應彈出問題清單而非直接失敗。
3. 正常配方 → 驗證通過、照常量測。

---

### A1 — 編輯器內試測（在 RecipeEditor 直接測一次選中工具）

**目的**：設定 ROI/參數後不必關編輯器、回主視窗、Run Recipe 才知對不對。對應 HDevelop Measure Assistant 即時看結果。

**具體做法**：
- RecipeEditor 已共用 `_imageHelper`、已能畫 ROI。新增 `[在此試測]` 按鈕。
- MainWindow 建立 editor 時，**多傳一個試測委派** `Func<MeasurementTool, ToolRunResult>`（或 `Action<MeasurementTool>`），內部用 MainWindow 的 `_recipeRunner` / 量測 adapter 對「當前影像」跑一次該工具，結果（邊緣點/擬合圓線/數值）畫在共用視窗。
- 按鈕只在選中 circle/line 工具且已載入影像時可用。

**選項**：
- **選項 A（建議）**：MainWindow 傳「試測委派」給 editor。**理由**：尊重分層（HALCON 仍只在 App 層由 MainWindow 持有）、editor 不需直接相依 adapter、耦合最小。
- 選項 B：把 adapter（IEdgeDetector/ICircleFitter…）直接注入 editor。缺點：editor 相依面變大、與 MainWindow 重複組裝邏輯。

**影響檔案**：`MainWindow.cs`（建立 editor 時傳委派）、`RecipeEditor.cs`（加按鈕 + 呼叫委派 + 畫結果）。

**風險**：中（共用 overlay slot 要小心：試測結果與 ROI 編輯框的繪製順序，避免互相覆蓋；HALCON 顯示須在 UI 執行緒）。

**驗證操作**：
1. 開編輯器、選一個 circle 工具、框好 ROI → 按 `[在此試測]` → 主視窗影像上應畫出擬合圓 + 數值。
2. 故意把 ROI 移開目標 → 試測 → 應顯示「未偵測到邊緣」之類訊息、不崩潰。
3. 連續試測多次 → 無殘留、無洩漏（工作管理員看記憶體平穩）。

---

### N6 — 量測快捷鍵 + 拖放影像

**目的**：KEYENCE 單鍵量測哲學；操作員重複按量測應有快捷鍵。拖放載圖更直覺。

**具體做法**：
- 快捷鍵：主視窗 `KeyPreview=true`，攔 F2（或空白鍵）→ 觸發 `OneClickMeasureButton_Click`。
- 拖放：主視窗 `AllowDrop=true`，`DragEnter`（檢查是影像副檔名）+ `DragDrop`（呼叫 `LoadAndDisplayImage`）。

**選項**：
- 快捷鍵選 **F2（建議）** 或空白鍵。**理由**：空白鍵在有按鈕聚焦時會誤觸該按鈕；F2 較不衝突。
- （可一併）加 F5 = Run Recipe。

**影響檔案**：`MainWindow.cs`（KeyDown handler、DragEnter/DragDrop）、`MainWindow.Designer.cs`（`KeyPreview`、`AllowDrop`）。

**風險**：低。

**驗證操作**：
1. 載入配方+影像 → 按 F2 → 應等同按一鍵量測。
2. 從檔案總管拖一張 png 到視窗 → 應載入並顯示。
3. 拖非影像檔 → 應被忽略、不崩潰。

---

### A3 — 即時邊緣預覽

**目的**：Edge Detection 分頁調 Threshold/Sigma/Polarity 後要按 Detect 才看得到。即時預覽讓調參體驗質變（對應 HDevelop Edges tab）。

**具體做法**：
- Edge 分頁加 `[即時預覽]` checkbox。
- checked 時，邊緣相關 numeric/combo 變動 → debounce（~250ms）後自動呼叫現有的 Detect 流程。
- 已有 `OnEdgeRoiNumericChanged` 在監聽 numeric 變化，掛上去即可。

**選項**：
- **選項 A（建議）**：checkbox 開關 + debounce。**理由**：大影像 detect 可能略慢，預設關、要時才開，且 debounce 避免每按一下就重算。
- 選項 B：永遠即時（無 checkbox）。缺點：慢影像會卡。

**影響檔案**：`MainWindow.cs`（debounce timer + 條件呼叫 Detect）、`MainWindow.Designer.cs`（checkbox）。

**風險**：低-中（debounce timer 與既有事件交互；避免在 detect 進行中重入）。

**驗證操作**：
1. 框 ROI、勾「即時預覽」→ 調 Threshold 滑桿 → 邊緣點應自動跟著變、不必按 Detect。
2. 快速連續調整 → 應只在停頓後算一次（debounce 生效、不卡）。
3. 取消勾選 → 回到手動 Detect。

---

### A5 — Undo/Redo（RecipeEditor）

**目的**：編輯器誤刪工具/改錯公差只能關掉重開（丟掉全部修改）。加復原。

**具體做法**：
- 用 `Stack<string>`（存 Recipe 的 JSON 快照）做 undo/redo，深度上限 20。
- 每次 `MarkDirty()` 前先 push 當前狀態快照；Undo = pop → 反序列化還原 → 重建 UI。
- Recipe 為 JSON 可序列化，深拷貝 = `SerializeObject`→`DeserializeObject`，不必手寫 clone。
- 加 `Ctrl+Z` / `Ctrl+Y`。

**選項**：
- **選項 A（建議）**：有界歷史堆疊（深度 20）。**理由**：完整復原體驗、記憶體可控。
- 選項 B：單層 undo（只記上一步）。缺點：連續誤操作救不回。

**影響檔案**：`RecipeEditor.cs`（undo/redo stack、快照、快捷鍵、UI 重建）。注意序列化封在 Infrastructure，editor 不可直接相依 Newtonsoft → 需經一個 App 層可用的 clone 管道（或新增 `IRecipeStore` 風格的 clone helper）。

**風險**：中（狀態重建要正確刷新所有控制項與選取狀態）。

**驗證操作**：
1. 編輯器刪一個工具 → Ctrl+Z → 工具應復原、選取狀態正確。
2. 改公差 → Ctrl+Z 還原 → Ctrl+Y 重做。
3. 連續操作 10+ 步 → 連按 Undo 應逐步回退、不崩潰。

---

### N7 — 最近開啟的配方/影像

**目的**：免每次走檔案對話框。

**具體做法**：
- 用 `Properties.Settings`（或一個小 JSON）記最近 5 個配方路徑與影像路徑。
- 主視窗加「最近」下拉/選單，點選直接載入；不存在的檔案灰掉或移除。

**選項**：
- **選項 A（建議）**：存於 `Properties.Settings`（per-user）。**理由**：WinForms 內建、不需自寫檔案管理。
- 選項 B：存一個 `data/recent.json`。缺點：`data/` 被 gitignore、且要自寫讀寫。

**影響檔案**：`MainWindow.cs`（載入後寫入清單、選單建立）、`Settings`。

**風險**：低。

**驗證操作**：
1. 載入幾個配方/影像 → 重啟 app → 「最近」應列出剛才那些。
2. 點選最近項 → 直接載入。
3. 刪掉其中一個檔案後重啟 → 該項應灰掉/消失。

---

### A4 — 工具列表 TreeView（選配）

**目的**：`ListBox` 攤平列出所有工具，配方工具多時看不出 元素 vs 複合 的依賴。

**具體做法**：
- `_toolListBox`（ListBox）→ `TreeView`。
- 兩層：`Elements`（line/circle）/ `Compounds`（distance/angle，子節點括號顯示 `RefToolIds`）。
- 右鍵選單：刪除/複製/上移/下移。

**選項**：
- **選項 A（建議）**：完整 TreeView 兩層分組。**理由**：依賴關係一目了然。
- 選項 B：保留 ListBox 但加分組標頭（owner-draw）。缺點：WinForms ListBox 分組要自繪、不如 TreeView 自然。

**影響檔案**：`RecipeEditor.cs`（控制項換型 + 所有 `_toolListBox.SelectedIndex/Items` 相關邏輯改寫）。

**風險**：中（選取/同步邏輯改動面不小）。**屬選配，工具少時不急。**

**驗證操作**：
1. 開含 6 工具的配方 → 應分 Elements/Compounds 兩組顯示，distance 子節點顯示引用的元素。
2. 選取、刪除、上下移 → 與舊行為一致。
3. 加/刪工具 → 樹即時更新。

---

### A6 — Tooltip 內容加強（僅 edge 參數）

**目的**：多數 tooltip 已堪用；只有 edge 幾個核心參數值得從「是什麼」升級為「何時調 + 建議值」。

**具體做法**：
- 改 `SetToolTip` 字串內容，例如：
  - Sigma：「高斯平滑：調大減少雜訊假邊緣、調小保留細節（常用 0.8–2.0）」
  - Threshold：「邊緣強度門檻：調高只取強邊、調低取更多弱邊（常用 20–40）」
  - Polarity：「邊緣極性：dark→light / light→dark / all」

**選項**：純文字字串調整，無選項。

**影響檔案**：`MainWindow.cs`（既有 SetToolTip 區塊）。

**風險**：低（只改字串）。

**驗證操作**：滑鼠停在 Sigma/Threshold/Polarity 欄位 → tooltip 應顯示新的含建議值說明。

---

### A7 — SplitContainer 可調佈局

**目的**：影像區與控制分頁的比例固定；不同場景（建配方 vs 看結果）需求不同。

**具體做法**：
- 主視窗影像區與 TabControl 之間插入 `SplitContainer`，使分隔線可拖曳調整比例。

**選項**：
- **選項 A（建議）**：內建 `SplitContainer`。**理由**：零新相依、10 分鐘改 Designer。
- 選項 B：引入 WeifenLuo DockPanel Suite（MIT）做可浮動/隱藏分頁。缺點：中大型重構、新相依。**不建議現在做。**

**影響檔案**：`MainWindow.Designer.cs`。

**風險**：低（純佈局；注意 Dock/Anchor 既有設定）。

**驗證操作**：
1. 拖曳分隔線 → 影像區與分頁比例應即時改變。
2. 縮放視窗 → 兩側應依比例縮放、不破版。

---

## 3. 不建議做（明確排除）

- **全面 WPF/XAML 遷移**：WinForms 對此量級完全足夠，業界桌面量測軟體亦多為原生。
- **Dark mode**：工廠高亮環境反而難讀，除非有夜班需求。
- **完全中文化**：保留 Threshold/Sigma/Edge 等業界術語，硬翻更難讀。
- **配方建立 Wizard 全套**：以 N3 空狀態引導取代。

---

## 4. 建議起手

**先做順序 1+2（N3 空狀態引導 + N2 PASS/FAIL 橫幅，合計 ~2hr、零風險）**——視覺與引導立刻見效；
**再做 N5（30 分）與 N1 配方驗證（最高實質價值）**。

> 每項完成後：`dotnet build ... /p:Platform=x64`（0/0）+ 跑 `Tests.exe`（含新測試）+ 上述 GUI 驗證操作；確認後一個小 commit。
