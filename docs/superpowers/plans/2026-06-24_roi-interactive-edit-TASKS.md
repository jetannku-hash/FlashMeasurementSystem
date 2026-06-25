# ROI 完整互動編輯（rect2）— 小模型可執行任務清單（一步到位）

- 日期：2026-06-24
- 設計來源：`docs/superpowers/plans/2026-06-24_roi-interactive-edit-rect2-plan.md`
- 決策：矩形 rect2、對稱繞中心、畫完自動進編輯、MainWindow 與 RecipeEditor 都要。
- 本文件目標：讓執行者（小模型）**按 T1→T8 順序逐一照抄**完成，不需自行設計或推導。

---

## 0. 執行者必讀（全域規則）

1. **嚴格照本文件的程式碼照抄**。除非任務明確要求，不要改動任何其他程式碼、不要重排、不要刪除既有方法。
2. **每完成一個任務就建置一次**（指令見下），建置綠燈才進下一個任務。任一任務建置失敗，先修正該任務再繼續。
3. **座標慣例已鎖死，禁止更改正負號**：
   - 長軸單位向量 `e1 = (-sin φ, cos φ)`（(row, col)）
   - 短軸單位向量 `e2 = (cos φ, sin φ)`
   - 由滑鼠點求角度 `φ = atan2(-(mouseRow - centerRow), mouseCol - centerCol)`
   - 顯示用 `disp_rectangle2` 一律 `+phi`（不可取負；取負會造成顯示框與量測框鏡像）。
4. **不可更動的既有 API 與功能**（模板比對在用）：`HWindowControlHelper.GetCurrentRoi()`、`HasRoi`、`IsRoiMode` 一律保留；`MainWindow.cs` L468–472 的「模板比對」ROI 程式碼**完全不要動**。
5. 角度單位：MainWindow 數值框是「度」，RecipeEditor 數值框是「弧度」；rect2 內部 `phi` 一律「弧度」。
6. 防遞迴旗標：MainWindow 用 `_updatingEdgeRoiControls`，RecipeEditor 用 `_updatingControls`，照本文件使用，不要拿掉。
7. 專案是 **old-style csproj**，新檔案必須手動加 `<Compile Include="..." />`，否則不會編譯。

### 建置與測試指令（PowerShell，於 repo 根目錄）

```powershell
# 一般建置
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"

# 動到 HALCON 顯示後，另外要 x64
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64

# 跑測試（建置後）
.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
```

> 若 App 正在執行會鎖住 DLL 導致建置失敗（MSB3026/MSB3027）→ 先關閉 App 再建置。

---

## 任務總覽與依賴

| 任務 | 內容 | 依賴 | 檔案 |
|---|---|---|---|
| T1 | 純幾何 `Rect2EditMath`（Domain） | — | 新檔 + Domain csproj |
| T2 | `Rect2EditMath` 測試套 | T1 | 新檔 + Tests csproj + Main 接線 |
| T3 | `OverlayAnnotator.DrawEditRect2` | T1 | OverlayAnnotator.cs |
| T4 | Helper 編輯狀態 + API + 繪製 | T1,T3 | HWindowControlHelper.cs |
| T5 | Helper 滑鼠互動整合 | T4 | HWindowControlHelper.cs |
| T6 | MainWindow 接線（含修正範圍） | T4 | MainWindow.cs |
| T7 | RecipeEditor 接線 | T4 | RecipeEditor.cs |
| T8 | 最終建置 + 測試 + 手動驗收 | T1–T7 | — |

---

## T1 — 新增純幾何 `Rect2EditMath`（Domain 層，可單元測試）

**目標**：把所有易錯幾何（軸向量、把手位置、命中判定、移動/縮放/旋轉換算）集中成無相依純函式。

**步驟 1**：新增檔案 `src/FlashMeasurementSystem.Domain/Roi/Rect2EditMath.cs`，內容如下（完整照抄）：

```csharp
using System;

namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// rect2 ROI 互動編輯的純幾何（無 HALCON / 無 UI 相依）。
    /// 慣例：e1=(-sinφ,cosφ)=長軸(Length1)方向，e2=(cosφ,sinφ)=短軸(Length2)方向，
    /// 座標 (row, col)，row 向下；與 HALCON gen_rectangle2 / disp_rectangle2 的 +phi 慣例一致。
    /// </summary>
    public static class Rect2EditMath
    {
        /// <summary>把手可縮放的最小半長（避免縮成 0）。</summary>
        public const double MinHalfLen = 3.0;

        /// <summary>長軸 e1、短軸 e2 的單位向量（row, col）。</summary>
        public static void Axes(double phi, out double e1r, out double e1c, out double e2r, out double e2c)
        {
            e1r = -Math.Sin(phi); e1c = Math.Cos(phi);
            e2r = Math.Cos(phi); e2c = Math.Sin(phi);
        }

        /// <summary>旋轉把手圓鈕位置：中心沿 e1 方向、距離 (l1 + knobGap)。</summary>
        public static void RotateKnobPos(double cr, double cc, double phi, double l1,
            double knobGap, out double kr, out double kc)
        {
            Axes(phi, out double e1r, out double e1c, out double _, out double _);
            double d = l1 + knobGap;
            kr = cr + d * e1r;
            kc = cc + d * e1c;
        }

        /// <summary>把滑鼠點相對中心投影到本地軸：d1 沿 e1、d2 沿 e2。</summary>
        public static void Project(double pr, double pc, double cr, double cc, double phi,
            out double d1, out double d2)
        {
            Axes(phi, out double e1r, out double e1c, out double e2r, out double e2c);
            double vr = pr - cr, vc = pc - cc;
            d1 = vr * e1r + vc * e1c;
            d2 = vr * e2r + vc * e2c;
        }

        /// <summary>
        /// 命中判定（影像座標，tol/knobGap 皆為影像像素）。優先序：
        /// Rotate &gt; Corner &gt; Len1 &gt; Len2 &gt; Body &gt; None。
        /// </summary>
        public static Rect2Handle HitTest(double pr, double pc, double cr, double cc, double phi,
            double l1, double l2, double tol, double knobGap)
        {
            Axes(phi, out double e1r, out double e1c, out double e2r, out double e2c);

            RotateKnobPos(cr, cc, phi, l1, knobGap, out double kr, out double kc);
            if (Dist(pr, pc, kr, kc) <= tol) return Rect2Handle.Rotate;

            for (int s1 = -1; s1 <= 1; s1 += 2)
                for (int s2 = -1; s2 <= 1; s2 += 2)
                {
                    double rr = cr + s1 * l1 * e1r + s2 * l2 * e2r;
                    double rc = cc + s1 * l1 * e1c + s2 * l2 * e2c;
                    if (Dist(pr, pc, rr, rc) <= tol) return Rect2Handle.Corner;
                }

            for (int s = -1; s <= 1; s += 2)
            {
                double rr = cr + s * l1 * e1r;
                double rc = cc + s * l1 * e1c;
                if (Dist(pr, pc, rr, rc) <= tol) return Rect2Handle.Len1;
            }

            for (int s = -1; s <= 1; s += 2)
            {
                double rr = cr + s * l2 * e2r;
                double rc = cc + s * l2 * e2c;
                if (Dist(pr, pc, rr, rc) <= tol) return Rect2Handle.Len2;
            }

            Project(pr, pc, cr, cc, phi, out double d1, out double d2);
            if (Math.Abs(d1) <= l1 && Math.Abs(d2) <= l2) return Rect2Handle.Body;

            return Rect2Handle.None;
        }

        /// <summary>對稱繞中心縮放：依把手種類更新 l1/l2（取投影絕對值，夾 MinHalfLen）。</summary>
        public static void ApplyResize(Rect2Handle handle, double pr, double pc,
            double cr, double cc, double phi, ref double l1, ref double l2)
        {
            Project(pr, pc, cr, cc, phi, out double d1, out double d2);
            if (handle == Rect2Handle.Len1 || handle == Rect2Handle.Corner)
                l1 = Math.Max(MinHalfLen, Math.Abs(d1));
            if (handle == Rect2Handle.Len2 || handle == Rect2Handle.Corner)
                l2 = Math.Max(MinHalfLen, Math.Abs(d2));
        }

        /// <summary>由滑鼠點求新角度（弧度）：φ = atan2(-(Δrow), Δcol)。</summary>
        public static double Rotate(double pr, double pc, double cr, double cc)
        {
            return Math.Atan2(-(pr - cr), pc - cc);
        }

        private static double Dist(double r1, double c1, double r2, double c2)
        {
            double dr = r1 - r2, dc = c1 - c2;
            return Math.Sqrt(dr * dr + dc * dc);
        }
    }

    /// <summary>rect2 互動把手種類（命中判定與拖曳模式共用）。</summary>
    public enum Rect2Handle
    {
        None,
        Body,
        Len1,
        Len2,
        Corner,
        Rotate
    }
}
```

**步驟 2**：在 `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj` 中，找到這一行：

```xml
    <Compile Include="Roi\RoiGeometry.cs" />
```

在它**正下方**新增一行：

```xml
    <Compile Include="Roi\Rect2EditMath.cs" />
```

**驗收**：執行「一般建置」綠燈。

---

## T2 — 新增 `Rect2EditMath` 測試套並接進 Main()

**目標**：用 console 測試套驗證易錯幾何（符合既有測試模式）。

**步驟 1**：新增檔案 `tests/FlashMeasurementSystem.Tests/Rect2EditMathTests.cs`，內容如下（完整照抄）：

```csharp
using System;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Tests
{
    public static class Rect2EditMathTests
    {
        public static void Run()
        {
            // Axes phi=0 -> e1=(0,1), e2=(1,0)
            Rect2EditMath.Axes(0.0, out double e1r, out double e1c, out double e2r, out double e2c);
            Near(0.0, e1r, "Axes phi0 e1r"); Near(1.0, e1c, "Axes phi0 e1c");
            Near(1.0, e2r, "Axes phi0 e2r"); Near(0.0, e2c, "Axes phi0 e2c");

            // Axes phi=pi/2 -> e1=(-1,0)
            Rect2EditMath.Axes(Math.PI / 2.0, out e1r, out e1c, out e2r, out e2c);
            Near(-1.0, e1r, "Axes pi/2 e1r"); Near(0.0, e1c, "Axes pi/2 e1c");

            // Rotate
            Near(0.0, Rect2EditMath.Rotate(100, 200, 100, 100), "Rotate right -> 0");
            Near(Math.PI / 2.0, Rect2EditMath.Rotate(0, 100, 100, 100), "Rotate up -> pi/2");

            // HitTest: center=(100,100), phi=0, l1=50, l2=30, tol=5, knobGap=20
            AssertHandle(Rect2Handle.Corner, Rect2EditMath.HitTest(130, 150, 100, 100, 0, 50, 30, 5, 20), "Corner hit");
            AssertHandle(Rect2Handle.Len1, Rect2EditMath.HitTest(100, 150, 100, 100, 0, 50, 30, 5, 20), "Len1 hit");
            AssertHandle(Rect2Handle.Len2, Rect2EditMath.HitTest(130, 100, 100, 100, 0, 50, 30, 5, 20), "Len2 hit");
            AssertHandle(Rect2Handle.Rotate, Rect2EditMath.HitTest(100, 170, 100, 100, 0, 50, 30, 5, 20), "Rotate hit");
            AssertHandle(Rect2Handle.Body, Rect2EditMath.HitTest(100, 100, 100, 100, 0, 50, 30, 5, 20), "Body hit");
            AssertHandle(Rect2Handle.None, Rect2EditMath.HitTest(100, 300, 100, 100, 0, 50, 30, 5, 20), "None hit");

            // ApplyResize corner mouse(160,170) -> l1=70, l2=60
            double l1 = 50, l2 = 30;
            Rect2EditMath.ApplyResize(Rect2Handle.Corner, 160, 170, 100, 100, 0, ref l1, ref l2);
            Near(70.0, l1, "Resize corner l1"); Near(60.0, l2, "Resize corner l2");

            // ApplyResize len1 only
            l1 = 50; l2 = 30;
            Rect2EditMath.ApplyResize(Rect2Handle.Len1, 100, 180, 100, 100, 0, ref l1, ref l2);
            Near(80.0, l1, "Resize len1 l1"); Near(30.0, l2, "Resize len1 l2 unchanged");

            // Min clamp
            l1 = 50; l2 = 30;
            Rect2EditMath.ApplyResize(Rect2Handle.Corner, 100, 100, 100, 100, 0, ref l1, ref l2);
            Near(Rect2EditMath.MinHalfLen, l1, "Resize clamp l1");
            Near(Rect2EditMath.MinHalfLen, l2, "Resize clamp l2");
        }

        private static void Near(double expected, double actual, string msg)
        {
            if (Math.Abs(expected - actual) > 1e-6)
                throw new InvalidOperationException("Rect2EditMathTests FAILED: " + msg + " expected " + expected + " got " + actual);
        }

        private static void AssertHandle(Rect2Handle expected, Rect2Handle actual, string msg)
        {
            if (expected != actual)
                throw new InvalidOperationException("Rect2EditMathTests FAILED: " + msg + " expected " + expected + " got " + actual);
        }
    }
}
```

**步驟 2**：在 `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj` 中，找到：

```xml
    <Compile Include="RoiDomainTests.cs" />
```

在它正下方新增：

```xml
    <Compile Include="Rect2EditMathTests.cs" />
```

**步驟 3**：在 `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs` 的 `Main()` 內，找到這兩行（約 L130–132）：

```csharp
            WorkflowDomainTests.Run();
            Console.WriteLine("WorkflowDomainTests passed");
            Console.WriteLine("EdgeDetectionDomainTests passed");
```

改成（在中間插入兩行）：

```csharp
            WorkflowDomainTests.Run();
            Console.WriteLine("WorkflowDomainTests passed");
            Rect2EditMathTests.Run();
            Console.WriteLine("Rect2EditMathTests passed");
            Console.WriteLine("EdgeDetectionDomainTests passed");
```

**驗收**：x64 建置綠燈 → 跑測試 exe → 輸出包含 `Rect2EditMathTests passed`，process 回傳 0。

---

## T3 — `OverlayAnnotator.DrawEditRect2`（畫綠色編輯框 + 把手 + 旋轉鈕）

檔案：`src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`

**步驟 1**：在檔案頂端 using 區（目前是 `using System; using System.Diagnostics; using HalconDotNet;`）新增一行：

```csharp
using FlashMeasurementSystem.Domain.Roi;
```

**步驟 2**：在 `DrawRectangle2(...)` 方法的結尾大括號之後（即該方法下方、`DrawLine` 方法之前）插入以下兩個方法：

```csharp
        /// <summary>
        /// 互動編輯外觀：綠色 rect2 外框 + 8 個把手方塊（4 角 + 4 邊中點）+ 旋轉圓鈕與連接桿。
        /// handleHalf 與 knobGapImg 皆為影像像素（由 helper 依縮放換算，確保螢幕上恆定大小）。
        /// </summary>
        public void DrawEditRect2(double cr, double cc, double phi, double l1, double l2,
            double handleHalf, double knobGapImg)
        {
            HOperatorSet.SetColor(_window, "green");
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.SetDraw(_window, "margin");
            HOperatorSet.DispRectangle2(_window, cr, cc, phi, l1, l2);

            Rect2EditMath.Axes(phi, out double e1r, out double e1c, out double e2r, out double e2c);

            double endR = cr + l1 * e1r, endC = cc + l1 * e1c;
            Rect2EditMath.RotateKnobPos(cr, cc, phi, l1, knobGapImg, out double kr, out double kc);
            HOperatorSet.DispLine(_window, endR, endC, kr, kc);

            HOperatorSet.SetDraw(_window, "fill");
            DrawHandleSquare(cr + l1 * e1r + l2 * e2r, cc + l1 * e1c + l2 * e2c, handleHalf);
            DrawHandleSquare(cr + l1 * e1r - l2 * e2r, cc + l1 * e1c - l2 * e2c, handleHalf);
            DrawHandleSquare(cr - l1 * e1r + l2 * e2r, cc - l1 * e1c + l2 * e2c, handleHalf);
            DrawHandleSquare(cr - l1 * e1r - l2 * e2r, cc - l1 * e1c - l2 * e2c, handleHalf);
            DrawHandleSquare(cr + l1 * e1r, cc + l1 * e1c, handleHalf);
            DrawHandleSquare(cr - l1 * e1r, cc - l1 * e1c, handleHalf);
            DrawHandleSquare(cr + l2 * e2r, cc + l2 * e2c, handleHalf);
            DrawHandleSquare(cr - l2 * e2r, cc - l2 * e2c, handleHalf);
            HOperatorSet.DispCircle(_window, kr, kc, handleHalf);

            HOperatorSet.SetDraw(_window, "margin");
        }

        private void DrawHandleSquare(double r, double c, double half)
        {
            HOperatorSet.DispRectangle1(_window, r - half, c - half, r + half, c + half);
        }
```

**驗收**：一般建置綠燈。

---

## T4 — Helper 編輯狀態、公開 API 與繪製

檔案：`src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs`

**步驟 1**：在檔案頂端 using 區（`using System; using System.Windows.Forms; using HalconDotNet;`）新增：

```csharp
using FlashMeasurementSystem.Domain.Roi;
```

**步驟 2**：在欄位宣告區（找到 `private Action<double, double, double, double> _roiCallback;` 這一行）下方新增：

```csharp
        private bool _editActive;
        private double _editCenterRow, _editCenterCol, _editPhi, _editLen1, _editLen2;
        private double _editLastRow, _editLastCol;
        private Rect2Handle _editMode = Rect2Handle.None;
        private Action<double, double, double, double, double> _editCallback;
```

**步驟 3**：在 `public OverlayAnnotator Annotator { get; }` 這一行下方新增公開屬性：

```csharp
        public bool IsEditingRect2 => _editActive;
```

**步驟 4**：在 `SetPersistentOverlayAction(...)` 方法下方，新增以下三個方法：

```csharp
        /// <summary>螢幕像素 → 影像像素（依目前縮放），用於把手大小與命中容差。</summary>
        public double ScreenPxToImage(double px)
        {
            if (_control.Width <= 0) return px;
            return px * (_imgCol2 - _imgCol1) / _control.Width;
        }

        /// <summary>開始/取代可編輯 rect2，進入編輯模式並重繪。onChanged 為本次編輯的回呼擁有者。</summary>
        public void BeginRect2Edit(double cr, double cc, double phi, double l1, double l2,
            Action<double, double, double, double, double> onChanged)
        {
            _editCenterRow = cr;
            _editCenterCol = cc;
            _editPhi = phi;
            _editLen1 = l1;
            _editLen2 = l2;
            _editCallback = onChanged;
            _editMode = Rect2Handle.None;
            _editActive = true;
            Redraw();
        }

        /// <summary>結束編輯模式（隱藏把手），清回呼並重繪。</summary>
        public void EndRect2Edit()
        {
            _editActive = false;
            _editMode = Rect2Handle.None;
            _editCallback = null;
            Redraw();
        }
```

**步驟 5**：修改 `Redraw()`。找到結尾這段：

```csharp
            _persistentOverlayAction?.Invoke();
            // 非拖曳中、且沒有 persistent overlay 時才畫 raw ROI 框。
            // persistent overlay（例如 DrawFittingLayers）有自己的 ROI 繪製邏輯，
            // 兩者同時畫會出現兩個重疊的藍色矩形。
            if (HasRoi && _persistentOverlayAction == null)
            {
                Annotator.DrawRoiRectangle(_roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol);
            }
        }
```

改成（多一個 `&& !_editActive` 條件，並在最後加上編輯外觀）：

```csharp
            _persistentOverlayAction?.Invoke();
            // 非拖曳中、且沒有 persistent overlay、且非編輯模式時才畫 raw ROI 框。
            // persistent overlay（例如 DrawFittingLayers）有自己的 ROI 繪製邏輯，
            // 兩者同時畫會出現兩個重疊的藍色矩形；編輯模式則由下方綠色編輯框取代。
            if (HasRoi && _persistentOverlayAction == null && !_editActive)
            {
                Annotator.DrawRoiRectangle(_roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol);
            }

            if (_editActive)
            {
                double half = ScreenPxToImage(5);
                double knobGap = ScreenPxToImage(22);
                Annotator.DrawEditRect2(_editCenterRow, _editCenterCol, _editPhi,
                    _editLen1, _editLen2, half, knobGap);
            }
        }
```

**步驟 6**：在 `ClearRoi()` 方法內，找到：

```csharp
            IsRoiMode = false;
            ClearOverlay();
```

改成（離開編輯模式，讓把手消失）：

```csharp
            IsRoiMode = false;
            _editActive = false;
            _editMode = Rect2Handle.None;
            _editCallback = null;
            ClearOverlay();
```

**步驟 7**：在 `DisplayImage(...)` 方法內，找到：

```csharp
            _persistentOverlayAction = null;
```

（它在方法開頭）改成：

```csharp
            _persistentOverlayAction = null;
            _editActive = false;
            _editMode = Rect2Handle.None;
            _editCallback = null;
```

**驗收**：一般建置綠燈（此時新方法尚未被滑鼠事件呼叫，屬正常）。

---

## T5 — Helper 滑鼠互動整合（命中、拖曳移動/縮放/旋轉）

檔案：`src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs`

**步驟 1**：修改 `OnMouseDown`。目前內容：

```csharp
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (CurrentImage == null) return;
            if (e.Button == MouseButtons.Right) { _isPanning = true; _panStartX = e.X; _panStartY = e.Y; return; }
            if (e.Button == MouseButtons.Left && IsRoiMode)
            {
                _isDrawingRoi = true;
                PixelToImage(e.X, e.Y, out _roiStartRow, out _roiStartCol);
                _roiEndRow = _roiStartRow; _roiEndCol = _roiStartCol;
            }
        }
```

改成（在 Right 與 IsRoiMode 之間插入編輯命中判定）：

```csharp
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (CurrentImage == null) return;
            if (e.Button == MouseButtons.Right) { _isPanning = true; _panStartX = e.X; _panStartY = e.Y; return; }

            if (e.Button == MouseButtons.Left && _editActive && !IsRoiMode)
            {
                PixelToImage(e.X, e.Y, out double pr, out double pc);
                double tol = ScreenPxToImage(8);
                double knobGap = ScreenPxToImage(22);
                Rect2Handle h = Rect2EditMath.HitTest(pr, pc, _editCenterRow, _editCenterCol,
                    _editPhi, _editLen1, _editLen2, tol, knobGap);
                if (h != Rect2Handle.None)
                {
                    _editMode = h;
                    _editLastRow = pr;
                    _editLastCol = pc;
                    return;
                }
            }

            if (e.Button == MouseButtons.Left && IsRoiMode)
            {
                _isDrawingRoi = true;
                PixelToImage(e.X, e.Y, out _roiStartRow, out _roiStartCol);
                _roiEndRow = _roiStartRow; _roiEndCol = _roiStartCol;
            }
        }
```

**步驟 2**：修改 `OnMouseMove`。目前結尾的條件鏈：

```csharp
            if (_isPanning)
            {
                int dx = e.X - _panStartX; int dy = e.Y - _panStartY;
                double rRange = _imgRow2 - _imgRow1; double cRange = _imgCol2 - _imgCol1;
                _imgRow1 += -dy * rRange / _control.Height; _imgRow2 += -dy * rRange / _control.Height;
                _imgCol1 += -dx * cRange / _control.Width; _imgCol2 += -dx * cRange / _control.Width;
                _panStartX = e.X; _panStartY = e.Y;
                Redraw();
            }
            else if (_isDrawingRoi)
            {
                // 拖曳中的 ROI 框由 Redraw() 統一繪製（_roiEnd 已先更新、HasRoi 成立）。
                PixelToImage(e.X, e.Y, out _roiEndRow, out _roiEndCol);
                Redraw();
            }
        }
```

改成（在最後加一個 `else if` 編輯拖曳分支，使用上方已算好的 `row`/`col`）：

```csharp
            if (_isPanning)
            {
                int dx = e.X - _panStartX; int dy = e.Y - _panStartY;
                double rRange = _imgRow2 - _imgRow1; double cRange = _imgCol2 - _imgCol1;
                _imgRow1 += -dy * rRange / _control.Height; _imgRow2 += -dy * rRange / _control.Height;
                _imgCol1 += -dx * cRange / _control.Width; _imgCol2 += -dx * cRange / _control.Width;
                _panStartX = e.X; _panStartY = e.Y;
                Redraw();
            }
            else if (_isDrawingRoi)
            {
                // 拖曳中的 ROI 框由 Redraw() 統一繪製（_roiEnd 已先更新、HasRoi 成立）。
                PixelToImage(e.X, e.Y, out _roiEndRow, out _roiEndCol);
                Redraw();
            }
            else if (_editMode != Rect2Handle.None)
            {
                if (_editMode == Rect2Handle.Body)
                {
                    _editCenterRow += row - _editLastRow;
                    _editCenterCol += col - _editLastCol;
                    _editLastRow = row;
                    _editLastCol = col;
                }
                else if (_editMode == Rect2Handle.Rotate)
                {
                    _editPhi = Rect2EditMath.Rotate(row, col, _editCenterRow, _editCenterCol);
                }
                else
                {
                    Rect2EditMath.ApplyResize(_editMode, row, col, _editCenterRow, _editCenterCol,
                        _editPhi, ref _editLen1, ref _editLen2);
                }
                _editCallback?.Invoke(_editCenterRow, _editCenterCol, _editPhi, _editLen1, _editLen2);
                Redraw();
            }
        }
```

> 注意：`OnMouseMove` 開頭已有 `PixelToImage(e.X, e.Y, out double row, out double col);`，直接使用 `row`/`col`，不要重新宣告。

**步驟 3**：修改 `OnMouseUp`。目前開頭：

```csharp
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _isPanning = false;
            if (_isDrawingRoi)
```

改成（在 `_isPanning = false;` 後先處理編輯結束）：

```csharp
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _isPanning = false;
            if (_editMode != Rect2Handle.None)
            {
                _editMode = Rect2Handle.None;
                return;
            }
            if (_isDrawingRoi)
```

**驗收**：一般建置綠燈。（GUI 行為在 T6/T7 接線後才完整。）

---

## T6 — MainWindow 接線（含修正範圍）

檔案：`src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

> 修正範圍重點：真正量測來源是 **Detect 按鈕**（用 `GetCurrentRoi()` bbox），必須改用編輯後的 `_latestEdgeRoi`。模板比對的 `GetCurrentRoi`/`HasRoi`（L468–472 一帶）**不要動**。

**步驟 1（新增欄位）**：找到：

```csharp
        private EdgeDetectionRoi _latestEdgeRoi;
```

在其下方新增：

```csharp
        private double _editCenterRow, _editCenterCol;
```

**步驟 2（新增編輯回呼）**：在 `OnImageRoiSelected(...)` 方法**下方**新增以下方法：

```csharp
        // 滑鼠互動編輯 rect2 的回呼：回寫數值框（度）與 _latestEdgeRoi，並使結果失效。
        // 不在此呼叫 ShowFittingOverlay()/Redraw —— helper 在本回呼後會自行重繪一次。
        private void OnEdgeRect2Changed(double cr, double cc, double phi, double l1, double l2)
        {
            _editCenterRow = cr;
            _editCenterCol = cc;
            _updatingEdgeRoiControls = true;
            try
            {
                _edgeScanLengthNumeric.Value = ClampNumericValue(_edgeScanLengthNumeric, (decimal)(l1 * 2.0));
                _edgeRoiWidthNumeric.Value = ClampNumericValue(_edgeRoiWidthNumeric, (decimal)(l2 * 2.0));
                _edgeAngleNumeric.Value = ClampNumericValue(_edgeAngleNumeric, (decimal)(phi * 180.0 / Math.PI));
            }
            finally
            {
                _updatingEdgeRoiControls = false;
            }

            _latestEdgeRoi = EdgeDetectionRoi.FromCenter(cr, cc, l1, l2, phi);
            _latestEdgeResult = null;
            _latestLineFittingResult = null;
            _latestCircleFittingResult = null;
            UpdateLineFittingResult(null);
            UpdateCircleFittingResult(null);
            RestoreDefaultEdgeGridColumns();
            _edgeResultsGrid.Rows.Clear();
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
        }
```

**步驟 3（畫完自動進編輯）**：在 `OnImageRoiSelected(...)` 方法中，找到結尾：

```csharp
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            ShowFittingOverlay();
        }
```

改成（記住中心並進入編輯模式）：

```csharp
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            _editCenterRow = _latestEdgeRoi.CenterRow;
            _editCenterCol = _latestEdgeRoi.CenterCol;
            ShowFittingOverlay();
            _imageHelper.BeginRect2Edit(_latestEdgeRoi.CenterRow, _latestEdgeRoi.CenterCol,
                _latestEdgeRoi.AngleRad, _latestEdgeRoi.Length1, _latestEdgeRoi.Length2, OnEdgeRect2Changed);
        }
```

**步驟 4（Detect 用編輯後的 ROI）**：找到 Detect 處理中的這段（約 L619、L629）：

```csharp
            if (!_imageHelper.HasRoi)
            {
                SetEdgeStatus(false, "Please draw an ROI region first.");
                return;
            }
```

把 `if (!_imageHelper.HasRoi)` 改為 `if (_latestEdgeRoi == null)`：

```csharp
            if (_latestEdgeRoi == null)
            {
                SetEdgeStatus(false, "Please draw an ROI region first.");
                return;
            }
```

接著找到：

```csharp
                EdgeDetectionRoi roi = CreateEdgeDetectionRoi(_imageHelper.GetCurrentRoi());
```

改成（直接用已同步的編輯 ROI）：

```csharp
                EdgeDetectionRoi roi = _latestEdgeRoi;
```

> 其餘該方法不變（後面 `_latestEdgeRoi = roi;` 保留，無害）。

**步驟 5（數值框 → 同步把手）**：找到 `OnEdgeRoiNumericChanged(...)` 整個方法：

```csharp
        private void OnEdgeRoiNumericChanged(object sender, EventArgs e)
        {
            if (_updatingEdgeRoiControls || _imageHelper == null || _imageHelper.CurrentImage == null || !_imageHelper.HasRoi)
            {
                return;
            }

            RegionInfo currentRoi = _imageHelper.GetCurrentRoi();
            if (currentRoi == null)
            {
                return;
            }

            _latestEdgeRoi = CreateEdgeDetectionRoiFromNumeric(currentRoi);
            _latestEdgeResult = null;
            _latestLineFittingResult = null;
            _latestCircleFittingResult = null;
            UpdateLineFittingResult(null);
            UpdateCircleFittingResult(null);
            RestoreDefaultEdgeGridColumns();
            _edgeResultsGrid.Rows.Clear();
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            ShowFittingOverlay();
        }
```

整個方法替換為：

```csharp
        private void OnEdgeRoiNumericChanged(object sender, EventArgs e)
        {
            if (_updatingEdgeRoiControls || _imageHelper == null || _imageHelper.CurrentImage == null || !_imageHelper.IsEditingRect2)
            {
                return;
            }

            double phi = (double)_edgeAngleNumeric.Value * Math.PI / 180.0;
            double l1 = (double)_edgeScanLengthNumeric.Value / 2.0;
            double l2 = (double)_edgeRoiWidthNumeric.Value / 2.0;

            _latestEdgeRoi = EdgeDetectionRoi.FromCenter(_editCenterRow, _editCenterCol, l1, l2, phi);
            _latestEdgeResult = null;
            _latestLineFittingResult = null;
            _latestCircleFittingResult = null;
            UpdateLineFittingResult(null);
            UpdateCircleFittingResult(null);
            RestoreDefaultEdgeGridColumns();
            _edgeResultsGrid.Rows.Clear();
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            ShowFittingOverlay();
            _imageHelper.BeginRect2Edit(_editCenterRow, _editCenterCol, phi, l1, l2, OnEdgeRect2Changed);
        }
```

> 註：`CreateEdgeDetectionRoi` 與 `CreateEdgeDetectionRoiFromNumeric` 改線後可能變成未使用，**保留不要刪**（屬既有程式碼）。

**驗收**：x64 建置綠燈。

---

## T7 — RecipeEditor 接線

檔案：`src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`

> RecipeEditor 與 MainWindow 共用同一個 `_imageHelper`；用「`BeginRect2Edit` 傳入自己的回呼」即可取得擁有權，互不干擾。關閉編輯器時要 `EndRect2Edit()`。

**步驟 1（選取工具時顯示/結束把手）**：找到 `OnToolSelectionChanged(...)`：

```csharp
        private void OnToolSelectionChanged(object sender, EventArgs e)
        {
            int idx = _toolListBox.SelectedIndex;
            if (idx < 0 || idx >= _tools.Count)
            {
                _selectedTool = null;
                SetPropertyPanelEnabled(false);
                return;
            }

            _selectedTool = _tools[idx];
            SetPropertyPanelEnabled(true);
            PopulateFromTool(_selectedTool);
        }
```

替換為：

```csharp
        private void OnToolSelectionChanged(object sender, EventArgs e)
        {
            int idx = _toolListBox.SelectedIndex;
            if (idx < 0 || idx >= _tools.Count)
            {
                _selectedTool = null;
                SetPropertyPanelEnabled(false);
                _imageHelper.EndRect2Edit();
                return;
            }

            _selectedTool = _tools[idx];
            SetPropertyPanelEnabled(true);
            PopulateFromTool(_selectedTool);
            ShowRoiEdit();
        }

        // 對 circle/line 工具：清掉 MainWindow 殘留 overlay，進入 rect2 互動編輯。
        private void ShowRoiEdit()
        {
            if (_selectedTool == null) { _imageHelper.EndRect2Edit(); return; }
            bool isElement = _selectedTool.ToolType == "circle" || _selectedTool.ToolType == "line";
            if (!isElement) { _imageHelper.EndRect2Edit(); return; }

            _imageHelper.ClearOverlay();
            var roi = _selectedTool.Roi;
            _imageHelper.BeginRect2Edit(roi.CenterRow, roi.CenterCol, roi.AngleRad,
                roi.Length1, roi.Length2, OnToolRect2Changed);
        }

        // 滑鼠互動編輯回呼：回寫 RoiGeometry（弧度）與數值框，標記 dirty。
        private void OnToolRect2Changed(double cr, double cc, double phi, double l1, double l2)
        {
            if (_selectedTool == null) return;
            _selectedTool.Roi.CenterRow = cr;
            _selectedTool.Roi.CenterCol = cc;
            _selectedTool.Roi.AngleRad = phi;
            _selectedTool.Roi.Length1 = l1;
            _selectedTool.Roi.Length2 = l2;

            _updatingControls = true;
            try
            {
                _centerRowNumeric.Value = ClampDecimal(cr, _centerRowNumeric.Minimum, _centerRowNumeric.Maximum);
                _centerColNumeric.Value = ClampDecimal(cc, _centerColNumeric.Minimum, _centerColNumeric.Maximum);
                _length1Numeric.Value = ClampDecimal(l1, _length1Numeric.Minimum, _length1Numeric.Maximum);
                _length2Numeric.Value = ClampDecimal(l2, _length2Numeric.Minimum, _length2Numeric.Maximum);
                _angleRadNumeric.Value = ClampDecimal(phi, _angleRadNumeric.Minimum, _angleRadNumeric.Maximum);
            }
            finally
            {
                _updatingControls = false;
            }
            MarkDirty();
        }
```

**步驟 2（數值框 → 同步把手）**：找到 `WriteRoi()`：

```csharp
        private void WriteRoi()
        {
            if (_updatingControls || _selectedTool == null) return;
            _selectedTool.Roi.CenterRow = (double)_centerRowNumeric.Value;
            _selectedTool.Roi.CenterCol = (double)_centerColNumeric.Value;
            _selectedTool.Roi.Length1 = (double)_length1Numeric.Value;
            _selectedTool.Roi.Length2 = (double)_length2Numeric.Value;
            _selectedTool.Roi.AngleRad = (double)_angleRadNumeric.Value;
            MarkDirty();
        }
```

在 `MarkDirty();` 之前插入一行同步把手：

```csharp
        private void WriteRoi()
        {
            if (_updatingControls || _selectedTool == null) return;
            _selectedTool.Roi.CenterRow = (double)_centerRowNumeric.Value;
            _selectedTool.Roi.CenterCol = (double)_centerColNumeric.Value;
            _selectedTool.Roi.Length1 = (double)_length1Numeric.Value;
            _selectedTool.Roi.Length2 = (double)_length2Numeric.Value;
            _selectedTool.Roi.AngleRad = (double)_angleRadNumeric.Value;
            if (_imageHelper.IsEditingRect2)
            {
                _imageHelper.BeginRect2Edit(_selectedTool.Roi.CenterRow, _selectedTool.Roi.CenterCol,
                    _selectedTool.Roi.AngleRad, _selectedTool.Roi.Length1, _selectedTool.Roi.Length2,
                    OnToolRect2Changed);
            }
            MarkDirty();
        }
```

**步驟 3（擷取 ROI 後也進編輯）**：找到 `ApplyRoiCapture(...)` 結尾：

```csharp
            MarkDirty();
        }
```

（此為 `ApplyRoiCapture` 的最後一行）改成：

```csharp
            MarkDirty();
            ShowRoiEdit();
        }
```

> 確認改的是 `ApplyRoiCapture` 內的 `MarkDirty();`（其上方緊接 `_updatingControls = false;` 的 finally 區塊）。

**步驟 4（關閉時結束編輯）**：在建構式 `public RecipeEditor(HWindowControlHelper imageHelper, Recipe recipe, string filePath, ...)` 的**最後一行之前**（return 前、所有初始化之後）新增：

```csharp
            this.FormClosed += (s, e) => _imageHelper.EndRect2Edit();
```

> 若不確定建構式結尾位置：在該建構式內、任何欄位都已初始化完成的最後處插入即可（`this.FormClosed += ...` 與順序無關）。

**驗收**：x64 建置綠燈。

---

## T8 — 最終建置、測試與手動驗收

**步驟 1**：兩種平台都建置綠燈：

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```

**步驟 2**：跑測試，確認 `Rect2EditMathTests passed` 且 process 回傳 0：

```powershell
.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
```

**步驟 3**：啟動 App 手動驗收：

```
src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe
```

驗收清單：
- [ ] Edge Detection 分頁：載入影像 → 畫 ROI → **放開後自動出現綠色框 + 8 把手 + 旋轉鈕**。
- [ ] 拖框內部可**移動**；拖角把手同時改長寬、拖邊中點只改一邊，皆**對稱繞中心**。
- [ ] 拖旋轉鈕可**改角度**，框與把手同步轉。
- [ ] 滑鼠改動即時回寫三個數值框；反向手打數值，把手即時移動（無無限迴圈、無閃爍失控）。
- [ ] **斜框（角度≠0）對齊邊緣後按 Detect，量測發生在對齊方向**（不鏡像、斜邊掃得到）。
- [ ] 放大/縮小後，把手大小與命中範圍維持恆定。
- [ ] 模板比對分頁的 ROI 行為**未受影響**（仍以正交框建立模板）。
- [ ] 開啟 Recipe Editor，選 circle/line 工具 → 出現把手可編輯 → 改動後數值框與 dirty 標記更新；關閉編輯器後把手消失。

> 手動項目若無法在本機執行，請明確標註「未手動驗證」與原因，不要宣稱通過。

---

## 附錄：本次修改檔案總表

新增：
- `src/FlashMeasurementSystem.Domain/Roi/Rect2EditMath.cs`（+ Domain csproj）
- `tests/FlashMeasurementSystem.Tests/Rect2EditMathTests.cs`（+ Tests csproj + Main 接線）

修改：
- `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`
- `src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs`
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`
- `src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`

不可更動：
- `MainWindow.cs` 模板比對 ROI（`HasRoi` + `GetCurrentRoi()` 建 `HRegion` 的區段）
- `HWindowControlHelper` 的 `GetCurrentRoi` / `HasRoi` / `IsRoiMode` 既有 API
