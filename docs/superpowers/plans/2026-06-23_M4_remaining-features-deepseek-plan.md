# M4 剩餘高優先功能 — DeepSeek V4 執行計畫

**日期**: 2026-06-23
**狀態**: 待使用者審閱後開工
**目標讀者**: DeepSeek V4（程式碼撰寫者）
**前置**: 手冊 4.1–4.13、M3c-1（配方執行）、M3c-2（配方編輯器）均已完成並實機驗證。

---

## 0. 給 DeepSeek V4 的全域工作守則（每個任務都適用）

> 開工前先讀這節，每完成一個任務都要照「驗收」跑過再進下一個。

1. **分層鐵律**：`Domain ← Application ← {Halcon, Infrastructure, Reporting} ← App.Wpf`。
   - `Domain` 專案**禁止**出現 `using HalconDotNet`、UI、檔案系統、硬體。只能放純 DTO / enum / 值物件。
   - HALCON operator（`HOperatorSet` / `HImage` / `HXLDCont`）**只能**出現在 `FlashMeasurementSystem.Halcon` 或既有的 `App.Wpf` 轉接碼。
2. **old-style csproj**：新增 `.cs` 檔**必須**在對應 `.csproj` 手動加 `<Compile Include="路徑" />`，不會自動 glob。新檔是 Form 時加 `<SubType>Form</SubType>`。
3. **語言版本 7.3**：禁用 C# 8+ 語法（switch expression、range `..`、`using var` 宣告式、可空參考型別 `?`、`record`、target-typed `new()`）。可用 `out int x` inline、`int.TryParse`、`?.`、`??`、tuple。
4. **最小變更**：只動任務指定的檔案與方法，不重排 import、不重命名無關變數、不順手重構。
5. **HALCON 參數順序**：任何 operator 都先查 `halcon_pdf/reference/halcon_operator_index.md` 找行號 → 讀 `reference_hdevelop.txt` 確認簽章，**不可憑記憶**。
6. **例外處理**：HALCON 呼叫包 `try/catch (HalconException)`，轉成「失敗結果」(Success=false + ErrorMessage)，不可吞掉或讓它往上炸。
7. **每任務驗收**（兩步，皆需 0 錯誤 / 全 pass）：
   ```powershell
   # 先關掉執行中的 app（會鎖 DLL）
   Stop-Process -Name FlashMeasurementSystem.App.Wpf -Force -ErrorAction SilentlyContinue
   # build x64
   dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
   # 跑 console 測試
   .\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
   ```
8. **測試型態**：本專案沒有測試框架。測試是 console exe，`Main()` 在 `tests/.../EdgeDetectionDomainTests.cs`，依序呼叫各 `XxxTests.Run()`。新增測試套件 = 新增一個 class 含 `public static void Run()`，並在 `Main()` 加一行呼叫。斷言用 `throw new InvalidOperationException(...)`。
9. **HALCON adapter 不做單元測試**（需實機 GUI 驗證），只測 Domain DTO 預設值與純邏輯。

---

## 功能 A — 擴充 distance 組合（circle↔circle、line↔circle）

### 背景與現況
- `RecipeRunner.MeasureDistance`（`src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs`）目前**只支援 line↔line**，其餘組合回「僅支援 line↔line」。
- `IDistanceMeasurer`（`Application/DistanceMeasurement/IDistanceMeasurer.cs`）**已具備** `MeasureCircleToCircle(中心對中心)` 與 `MeasurePointToLine`，HALCON adapter 已實作，**無需新增 operator**。
- 元素量測結果 `ToolRunResult`：circle 有 `FitCenterRow/FitCenterCol/FitRadiusPx`；line 有 `LineRow1/Col1/Row2/Col2`。

### 設計決策（MVP，先做最常用語意）
| 組合 | 距離語意 | 用的方法 | 連線端點（繪圖） |
|------|----------|----------|------------------|
| line↔line | 兩線段垂直最近距離 | `MeasureLineToLine`（現況不動） | A 中點→垂足（現況） |
| circle↔circle | **圓心對圓心距離** | `MeasureCircleToCircle` | 圓心 A→圓心 B |
| line↔circle | **圓心到線的垂直距離** | `MeasurePointToLine`（point=圓心） | 圓心→其在線上的垂足 |

> 「圓邊到圓邊」「線到圓邊」等進階語意留待之後；本期不做，並在程式碼註解標明。

### 任務拆解

**A1 — RecipeRunner.MeasureDistance 改為依型別分派**
- 檔案：`src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs`，方法 `MeasureDistance`。
- 實作要點：
  1. 取出兩個 ref 結果 `a`、`b`（沿用現有「找不到/未量測」防呆，**保留**）。
  2. 移除「僅支援 line↔line」的硬擋；改判 `(a.ToolType, b.ToolType)`：
     - 兩者皆 `line` → 現有 `MeasureLineToLine` 路徑（含 `ProjectPointOntoLine` 垂足繪圖），原樣保留。
     - 兩者皆 `circle` → 呼叫 `_distanceMeasurer.MeasureCircleToCircle(a.FitCenterRow, a.FitCenterCol, b.FitCenterRow, b.FitCenterCol, dp)`；繪圖端點 `DistRow1/Col1=圓心A`、`DistRow2/Col2=圓心B`。
     - 一 `line` 一 `circle` → 以圓心為 point、線為 line 呼叫 `MeasurePointToLine`；繪圖端點：圓心 →（用既有 `ProjectPointOntoLine`）線上垂足。注意參數順序：`MeasurePointToLine(pointRow, pointCol, lineRow1, lineCol1, lineRow2, lineCol2, dp)`。
     - 其他（含 circle/line 以外）→ 維持「不支援」失敗結果。
  3. `res.DistMm`、`res.ValueText`、公差判定區塊**沿用現有寫法**（值來源改為對應 result 的 `DistanceMm`）。
- 驗收：build x64 0 錯誤；console test 全 pass（此任務不新增測試，靠下一步 GUI）。

**A2 — RecipeEditor distance 的 Ref 下拉加入 circle 選項**
- 檔案：`src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`，方法 `PopulateRefCombos`。
- 現況：`PopulateRefCombos` 只列 `ToolType == "line"`。
- 實作要點：新增參數或分支——**distance** 工具列 `line` **與** `circle`；**angle** 工具**維持只列 line**（angle 僅支援 line↔line）。
  - 作法：`PopulateRefCombos` 內依 `tool.ToolType` 決定允許型別集合：`distance → {line, circle}`、`angle → {line}`。其餘不變（`ToolRef` 顯示 `Name (Id)`、`SelectRefCombo` 還原選取）。
- 驗收：build x64 0 錯誤；GUI 手動——新增 2 circle + 1 distance，distance 的 Ref1/Ref2 下拉應能選到 circle。

**A3 — 端到端 GUI 驗收（純手動，無程式碼）**
- 步驟：Edit Recipe → 建 2 個 circle + 1 個 distance（Ref 選兩 circle）→ Save → 主畫面 Run Recipe。
- 預期：距離線畫在兩圓心之間、`D=...mm` 顯示、公差 OK/NG 上色正確。
- 再建 line+circle+distance 驗證 line↔circle（圓心→垂足）。
- 產出：把結果記到 `docs/每日進度/`。

---

## 功能 C — line 元素角度公差判定

> 先做 C（比 B 小、且與 A 同檔案區域），再做 B。

### 背景與現況
- `RecipeRunner.MeasureLine` 目前固定 `res.IsOk = null`（元素不判定），只回 `LineAngleDeg`。
- 需求：讓 line 工具可對「線對水平軸的角度」做公差判定（前提：工件不旋轉，或已用參考姿態校正方向）。

### 設計決策（不改 Domain schema，用既有欄位當開關）
- **判定開關 = `tool.Tolerance.Unit == "deg"`**：若 line 工具的公差單位是 `deg`，就把 `LineAngleDeg` 正規化後與 `[Nominal+Lower, Nominal+Upper]` 比對；否則維持 `IsOk=null`（純元素）。
- **角度正規化**：線角度有 180° 週期與方向歧義。比對前把實測角與 Nominal 都正規化到 `[0,180)`，並取「環狀最小差」（例：179° 與 1° 差 2°，不是 178°）。
- 理由：不動 `MeasurementTool` schema（避免 schema v3→v4 遷移）；沿用 `ToleranceJudger`。

### 任務拆解

**C1 — Domain 新增角度正規化純函式 + 測試**
- 檔案（新）：`src/FlashMeasurementSystem.Domain/AngleMeasurement/AngleNormalizer.cs`
  - `public static class AngleNormalizer`，方法 `public static double ToHalfCircle(double deg)`（回傳 `[0,180)`）與 `public static double CircularDiffDeg(double a, double b)`（回傳 `[0,90]` 的環狀最小差，週期 180）。
  - 純數學、無相依。記得在 `FlashMeasurementSystem.Domain.csproj` 加 `<Compile Include>`。
- 檔案（新）：`tests/FlashMeasurementSystem.Tests/AngleNormalizerTests.cs`
  - `public static void Run()`：驗 `ToHalfCircle(190)=10`、`ToHalfCircle(-10)=170`、`CircularDiffDeg(179,1)=2`、`CircularDiffDeg(10,100)=90` 等；失敗 `throw new InvalidOperationException`。結尾 `Console.WriteLine("AngleNormalizerTests passed")`。
  - 在 `tests/.../EdgeDetectionDomainTests.cs` 的 `Main()` 加 `AngleNormalizerTests.Run();`；`tests` csproj 加 `<Compile Include>`。
- 驗收：build x64 0 錯誤；test 印出 `AngleNormalizerTests passed`。

**C2 — RecipeRunner.MeasureLine 接上角度判定**
- 檔案：`src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs`，方法 `MeasureLine`。
- 實作要點：量到 `line.AngleDeg` 後：
  - 若 `tool.Tolerance != null && tool.Tolerance.Unit == "deg"`：
    - 用 `AngleNormalizer.ToHalfCircle` 正規化實測角。
    - 建 `ToleranceItemInput { MeasuredValue = 正規化角, Spec = tool.Tolerance }` 丟 `_judger.Judge`，把 `IsOk` 寫回 `res.IsOk`。
    - **注意**：`ToleranceJudger` 是線性比較，不懂環狀。為避免 179° vs Nominal 1° 誤判，先把實測角對齊到 Nominal 的同圈：`aligned = Nominal + CircularSignedDiff(measured, Nominal)`（C1 可加一個帶正負號的 diff，或在此就地算）。比較用 `aligned`。
    - `res.ValueText` 改為 `string.Format(..., "{0:F2}deg", aligned)`。
  - 否則維持現況 `IsOk = null`。
- 驗收：build x64 0 錯誤；GUI——line 工具設 Unit=deg、Nominal=0、Lower=-1、Upper=1，量水平邊應 OK；量斜邊應 NG。

**C3 — RecipeEditor 提示（極小，可選）**
- 檔案：`src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`。
- 實作要點：line 工具的 Tolerance 區塊加一行說明 Label：「Unit=deg 時對線角度判定」。不改邏輯。
- 驗收：build x64 0 錯誤；GUI 顯示說明文字。

---

## 功能 B — 4.14 一鍵量測流程（軟體版狀態機）

### 背景與現況
- 手冊 4.14 的完整狀態機含相機/光源/IO/MES 硬體服務，**本機無硬體**（用 replay 影像）。
- 現有 `RunRecipeButton_Click`（`MainWindow.cs`）已手動串：pixel size 來源 → `_recipeRunner.Run` → overlay 繪製。但**沒有**：影像品質前置檢查、自動模板匹配、整體 OK/NG 彙整、報表輸出。
- `FlashMeasurementSystem.Reporting` 專案目前**是空的**（只有 AssemblyInfo），CSV 報表需新建。

### 設計決策（軟體 only，不接硬體）
- 範圍縮到：`CheckingImage → MatchingTemplate → TransformingRois → Measuring → Evaluating → Reporting`。略過 `WaitingPart/Acquiring/Outputting(IO)` 硬體階段。
- 流程**同步、循序**（不引入 async/硬體），以既有注入服務（`_iqc`、`_templateMatcher`、`_recipeRunner`、`_judger`）組裝。
- 輸出：每次量測一列 CSV 到 `data/reports/measure_YYYYMMDD.csv`（逐工具一列：時間、配方、工具、實測值、上下限、OK/NG）。

### 任務拆解

**B1 — Domain：狀態 enum + 整體結果 DTO + 測試**
- 檔案（新）：`src/FlashMeasurementSystem.Domain/Workflow/MeasurementState.cs`
  - `public enum MeasurementState { Idle, CheckingImage, MatchingTemplate, TransformingRois, Measuring, Evaluating, Reporting, Completed, Failed }`（精簡版，去掉硬體階段）。
- 檔案（新）：`src/FlashMeasurementSystem.Domain/Workflow/WorkflowResult.cs`
  - 純 DTO：`bool Success`、`bool AllOk`、`int OkCount`、`int NgCount`、`string RecipeName`、`string ReportPath`、`MeasurementState FinalState`、`string Message`、`DateTime Timestamp`。加 `Default()`。
- 檔案（新）：`tests/FlashMeasurementSystem.Tests/WorkflowDomainTests.cs` → `Run()` 驗預設值；接進 `Main()`。
- 三個 csproj（Domain、tests）加 `<Compile Include>`。
- 驗收：build x64 0 錯誤；test 印 `WorkflowDomainTests passed`。

**B2 — Reporting：CSV 報表寫入器 + 介面**
- 檔案（新）：`src/FlashMeasurementSystem.Application/Reporting/IMeasurementReportWriter.cs`
  - 介面 `void Append(WorkflowResult overall, IList<ItemJudgment> items, string filePath)`（型別用 Domain `ItemJudgment`/`WorkflowResult`）。
- 檔案（新）：`src/FlashMeasurementSystem.Reporting/Csv/CsvMeasurementReportWriter.cs`
  - 實作 `IMeasurementReportWriter`：檔不存在先寫表頭；以 `CultureInfo.InvariantCulture`、UTF-8 寫入；逐 `ItemJudgment` 一列（時間、配方、ToolName、MeasuredValue、LowerLimit、UpperLimit、IsOk）。
  - **不可**用 HALCON。用 `System.IO.File.AppendAllText`。
- `FlashMeasurementSystem.Reporting.csproj` 需確認有對 `Domain`、`Application` 的 ProjectReference（若無則加），並加兩個 `<Compile Include>`（注意 Application 介面檔在 Application 專案）。
- 驗收：build x64 0 錯誤。

**B3 — App.Wpf：MeasurementWorkflow 編排器**
- 檔案（新）：`src/FlashMeasurementSystem.App.Wpf/MeasurementWorkflow.cs`
  - 建構子注入既有服務（`HalconImageQualityChecker`、`HalconTemplateMatcher`、`RecipeRunner`、`ToleranceJudger`、`IMeasurementReportWriter`）。
  - `public WorkflowResult RunOnce(Recipe recipe, HImage image, 模板/pixelSize 等參數)`：
    1. `CheckingImage`：呼叫 IQC，不合格 → `Failed` 早退。
    2. `MatchingTemplate`：若 `recipe.HasReferencePose`，跑 `FindShapeModel` 取姿態；失敗 → `Failed`。
    3. `Measuring`：呼叫 `_recipeRunner.Run(...)`（沿用 `RunRecipeButton_Click` 既有參數組裝邏輯）。
    4. `Evaluating`：彙整各 `ToolRunResult.IsOk` → `OkCount/NgCount/AllOk`。
    5. `Reporting`：呼叫 report writer 寫 CSV，填 `ReportPath`。
    6. 回傳 `WorkflowResult`，附帶 `List<ToolRunResult>`（供 UI 繪圖）——可用 out 參數或包一個小回傳型別。
  - 提供 `event Action<MeasurementState> StateChanged`（可選，UI 顯示用）。
  - csproj 加 `<Compile Include>`。
- 驗收：build x64 0 錯誤。

**B4 — MainWindow：「一鍵量測」按鈕串接**
- 檔案：`src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`（工具列 `OnLoad` 內、`RunRecipeButton_Click` 附近）。
- 實作要點：
  1. 在 Measurement 工具列加按鈕 `[一鍵量測]`，事件 `OneClickMeasureButton_Click`。
  2. 事件內：建立/重用 `MeasurementWorkflow`，呼叫 `RunOnce`，把回傳的 `List<ToolRunResult>` 餵給**現有** overlay 繪製邏輯（可抽出 `RunRecipeButton_Click` 的 overlay 區塊成一個私有方法 `DrawRecipeResults(results)` 供兩處共用——這是允許的小重構，因兩按鈕共享繪圖）。
  3. 狀態列顯示整體 `OK/NG`、`OkCount/NgCount`、CSV 路徑。
- 驗收：build x64 0 錯誤；GUI——載入影像+配方→按一鍵量測→畫面出結果、狀態列顯示整體判定、`data/reports/` 出現 CSV。

**B5 — 文件**
- 在 `docs/每日進度/2026-06-23_M4....md` 記錄 B 的設計與驗收結果。

---

## 建議執行順序與相依

```
C1（Domain 角度工具+測試）
  └─ C2（RecipeRunner.MeasureLine 判定）─ C3（編輯器提示）
A1（RecipeRunner 分派）─ A2（編輯器 circle 下拉）─ A3（GUI 驗收）
B1（Domain enum/DTO）─ B2（Reporting CSV）─ B3（Workflow）─ B4（MainWindow 按鈕）─ B5（文件）
```
- A 與 C 都改 `RecipeRunner.cs`，**建議先做完 C2 再做 A1**（避免同檔衝突），或反之，但勿並行。
- B 最大、相依最少（除了會重用 A/C 的量測結果），放最後。

## 風險與注意
| 風險 | 緩解 |
|------|------|
| `RecipeRunner.cs` 同檔被 A、C 連續修改 | 串行執行，每步 build 驗收後再下一步 |
| 角度環狀判定誤判（179 vs 1） | C1 提供環狀 diff 並寫測試覆蓋邊界 |
| Reporting 專案 ProjectReference 缺失 | B2 第一步先確認/補 csproj 參考 |
| 一鍵流程把硬體階段也實作 | 明確縮範圍為軟體 only，硬體階段註解標 TODO |
| LangVersion 7.3 誤用新語法 | 守則 §3 列出禁用清單，build 會直接擋下 |

## 不在本期範圍（明確排除）
- 完整畸變校正（需 HALCON caltab 標定板硬體）。
- 自動對焦 4.9（需 Z 軸馬達）。
- 相機/光源/IO/MES 實際硬體串接。
- 圓邊到圓邊、線到圓邊等進階距離語意。
