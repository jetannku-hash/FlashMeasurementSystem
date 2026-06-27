# GD&T 形位公差 v1 — 實作計畫

> 對應 spec：`docs/superpowers/specs/2026-06-27-gdt-tolerance-v1-design.md`
> 分支建議：`feature/gdt-tolerance-v1`（從 main 開）。
> 慣例：repo root `F:\C#\FlashMeasurementSystem\FlashMeasurementSystem`；old-style csproj 新檔需手動 `<Compile Include>`；Domain 無 HALCON/UI；LangVersion 7.3；console 測試接進 `EdgeDetectionDomainTests.Main()`；build 前先關 app（`Stop-Process FlashMeasurementSystem.App.Wpf`）。
> 每 task：先測試後整合，build x64 0/0 + console 測試 + 必要時 GUI 手動驗，小 commit。

10 個 task。T1–T3 為純 Domain（可嚴格自動測，先做完）；T4 唯一動到 runner 既有碼（含回歸閘）；T5–T6 UI；T7 schema；T8 合成影像；T9 報表；T10 GUI 總驗。

---

## T1 — Domain 資料模型

**新檔**
- `Domain/Gdt/GdtCharacteristic.cs`：`enum { Roundness, Straightness, Parallelism, Perpendicularity, Concentricity }`
- `Domain/Gdt/GdtToleranceSpec.cs`：
  ```csharp
  public class GdtToleranceSpec {
      public GdtCharacteristic Characteristic { get; set; }
      public double ToleranceZoneMm { get; set; }   // T，> 0
      public static GdtToleranceSpec Default() => new GdtToleranceSpec();
  }
  ```

**改檔**
- `Domain/Roi/MeasurementTool.cs`：加 `public GdtToleranceSpec Gdt { get; set; } = null;`；`ToolType` 註解補 5 種字串。
- `FlashMeasurementSystem.Domain.csproj`：兩個新檔 `<Compile Include>`。

**驗證**：build Any CPU + x64 0/0。

---

## T2 — Domain 偏差計算 GdtCalculator + 測試

**新檔** `Domain/Gdt/GdtCalculator.cs`（純靜態，spec §5 公式）：
```csharp
public static double LineLengthPx(double r1,double c1,double r2,double c2);
public static double AcuteAngleBetweenLinesDeg(/* a1,a2,b1,b2 八參數 */);  // [0,90]
public static double ParallelismZonePx(/* 量測線, 基準線 */);     // L·sinΔθ
public static double PerpendicularityZonePx(/* 量測線, 基準線 */); // L·sin(90−θ)
public static double ConcentricityDiametralPx(double cr1,double cc1,double cr2,double cc2); // 2·圓心距
```
- 退化線段（長度<eps）：角度視為 0；於文件註明。

**新測試** `tests/.../GdtCalculatorDomainTests.cs`（spec §8.1，閉合解）：
- 平行：L=100 水平 vs 基準 0°→≈0；vs 30°→100·sin30°=50（多角度，容差 1e-6）。
- 垂直：垂直 vs 水平→≈0；偏 10°→100·sin10°。
- 同心：(0,0) 與 (3,4)→10.0。
- 銳角折疊：170/10、95/0 等。

**接線**：`GdtCalculatorDomainTests.Run()` 加入 `Main()` + csproj `<Compile Include>`。

**驗證**：build + `FlashMeasurementSystem.Tests.exe` 印 `GdtCalculator passed`。

---

## T3 — Domain 單邊判定 GdtEvaluation + 測試

**新檔** `Domain/Gdt/GdtEvaluation.cs`（純靜態，spec §3）：
```csharp
public struct GdtJudgment { public bool IsOk; public double MarginPercent; public bool NearBoundary; public string Message; }
public static GdtJudgment Evaluate(double deviation, double toleranceZoneMm);
```
- NaN/Inf→NG；負 deviation→夾為 0（或 NG，spec 註明取「夾 0」並警示）；`IsOk = dev ≤ T`；接近上限（餘量<20%）→ NearBoundary；中文訊息。

**新測試** `tests/.../GdtEvaluationDomainTests.cs`（spec §8.2 全案例）。

**接線**：`Run()` 入 `Main()` + csproj。

**驗證**：`FlashMeasurementSystem.Tests.exe` 印 `GdtEvaluation passed`。

---

## T4 — RecipeRunner 整合（唯一動既有碼，含回歸閘）

**改 `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs`**
1. `ToolRunResult` 加 `CircleRoundnessPx / ResidualRmsPx / GdtDeviationMm`。
2. `MeasureCircle`：成功後 `res.ResidualRmsPx = circle.ResidualRms; res.CircleRoundnessPx = circle.Roundness;`
3. `MeasureLine`：成功後 `res.ResidualRmsPx = line.ResidualRms;`
4. 新增 `MeasureGdt(res, tool, byId, pixelSizeUm)` 與一個 Pass（Pass 1.5 之後、Pass 2 之前；Pass 2 的 skip 清單加入 5 種 GD&T 型別）：
   - 驗 ref 數量/型別（spec §6）；僅 line/circle 基礎元件。
   - deviation：roundness=`CircleRoundnessPx`；straightness=`ResidualRmsPx`；parallelism/perp=GdtCalculator（取 ref[0] 線端點長＋兩線方向）；concentricity=GdtCalculator（兩圓心）。再 `× pixelSizeUm/1000` 轉 mm。
   - `GdtEvaluation.Evaluate` → 設 `IsOk/GdtDeviationMm/ValueText/Message`；設 overlay 欄位。

**回歸閘**：完整跑 `FlashMeasurementSystem.Tests.exe`（既有套件全綠），確認 distance/angle/A5 構造未受影響。build x64 0/0。

---

## T5 — MainWindow overlay

- GD&T 工具結果以 OK/NG 顏色 + `GdtDeviationMm` 文字標註。
- concentricity 用 `DistRow1/Col1→DistRow2/Col2` 畫兩圓心偏移線。
- roundness/straightness 標於 ref 元素；parallelism/perp 標兩線。
- **mirror 既有 distance/angle overlay 繪法**（執行時讀 MainWindow 相關行）。

**驗證**：GUI 載合成影像，5 種工具 overlay 正確、顏色隨 OK/NG。

---

## T6 — RecipeEditor 工具加入 UI

- 加 5 種工具型別入口；選 1/2 個既有工具為 ref（第 2＝基準 A）；`T`（mm）數值輸入；型別×ref 前置檢核。
- **mirror 既有 distance/angle/intersection 的 add-tool UI**（宣告樣式、parent、事件接法照周邊行）。

**驗證**：GUI 建出 5 種工具、存檔、重載、跑量測。

---

## T7 — Schema v4→v5

- `Domain/Roi/Recipe.cs`：`SchemaVersion` 預設 `5` + 註解新增「v5：GD&T 公差工具（roundness/straightness/parallelism/perpendicularity/concentricity + GdtToleranceSpec）」。
- 無遷移碼（加版號即可）。

**驗證**：存新配方→重載 round-trip 一致；載入舊 v4 .zcp 不報錯、行為不變。

---

## T8 — 合成測試影像腳本

- `scripts/gen_gdt_test_images.py`（PIL）：瓣形近圓、弓形線、兩近平行線、兩近垂直線、兩偏心圓 → `data/images/gdt_*.png`。
- 每圖檔名/README 註記畫圖時設的缺陷量（供目視比對）。
- **誠實限制**寫進 README：非 ground truth，HALCON 次像素自帶誤差。

**驗證**：腳本產圖；GUI 可載入。

---

## T9 — 報表驗證

- 確認既有 CSV writer 泛用迭代 `ToolRunResult`，GD&T 工具的 `IsOk/ValueText` 正確輸出。
- 必要時把 `GdtDeviationMm` 併入 `ValueText`（T4 已做則略）。

**驗證**：跑含 GD&T 工具的配方→匯出 CSV，欄位正確。

---

## T10 — GUI 總驗 + 收尾

- build x64 0/0 + `FlashMeasurementSystem.Tests.exe` 全綠。
- 對每張合成影像建對應 GD&T 工具，逐一確認偏差量級、OK/NG、overlay、存載、CSV。
- 更新 `docs/每日進度/2026-06-27_*.md`。
- merge 回 main（--no-ff），刪分支。

---

## 交付順序與里程碑

- **里程碑 A（純演算法，可嚴格自動驗收）**：T1–T3。完成即證明 5 項的計算與判定正確。
- **里程碑 B（端到端）**：T4–T7。配方可建/跑/存/載 GD&T 工具。
- **里程碑 C（驗證資料與收尾）**：T8–T10。

> 全程未含「真實零件計量準度」驗證——待相機＋標準件＋對標 CMM，硬體就位前不宣稱。
