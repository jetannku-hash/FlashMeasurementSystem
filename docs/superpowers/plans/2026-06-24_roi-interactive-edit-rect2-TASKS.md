# ROI 完整互動編輯（rect2）— 小任務執行清單（給小模型逐條照抄）

> 來源設計：`docs/superpowers/plans/2026-06-24_roi-interactive-edit-rect2-plan.md`
> 目標：讓 ROI 矩形可用滑鼠 **移動 / 縮放（對稱繞中心）/ 旋轉**，套用於 MainWindow（Edge Detection 分頁）與 RecipeEditor，畫完新框自動進編輯。
> 本文件為「可一步到位完成」的施工清單。**請依 T1→T8 順序執行，每完成一個任務就建置驗證再繼續。**

---

## 0. 執行者規則（務必遵守）

1. **只改本文件每個任務「檔案」欄列出的檔案**；不要動其他檔案、不要重排版、不要改無關程式碼、不要刪既有方法（即使看起來沒用到）。
2. 既有檔案的修改一律用「找到 FIND 區塊 → 換成 REPLACE 區塊」或「在 ANCHOR 之後插入」。FIND 區塊必須與現況逐字相符。
3. 新檔案要 **手動加 `<Compile Include="..." />`**（本專案是 old-style csproj，不會自動 glob）。
4. 每個任務最後都有「驗證」。**未通過驗證不要進下一個任務**。
5. 全程不要 `git commit`。
6. 角度、向量、命中等數學一律照本文件公式 **照抄**，不要自行推導或「優化」。

### 建置 / 測試指令（在 repo 根 `FlashMeasurementSystem/` 執行）

```powershell
# 建置（動到 HALCON 顯示 → 兩個平台都要過）
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform="Any CPU"
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64

# 跑測試（建置後）
.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
```

> 注意：若 App 正在執行會鎖住 DLL 造成建置失敗（MSB3026/MSB3027）→ 先關閉 App。

### 座標 / 角度慣例（已對 HALCON `gen_rectangle2` 參考鎖定，全程沿用）

- 座標 `(row, col)`，原點左上、**row 向下**。
- `Phi` = Length1 長軸對水平軸的弧度（數學正向），與 `gen_rectangle2` / `disp_rectangle2` 一致。
- 主軸單位向量 `e1 = (-sinφ, cosφ)`；垂直軸 `e2 = (cosφ, sinφ)`（均為 (row, col)）。
- 旋轉：`phi = atan2(-(mouseRow - centerRow), mouseCol - centerCol)`。
- **不可對 phi 取負**（取負會造成顯示框與量測框鏡像，是已修過的舊 bug）。

---

## T1：新增純幾何 `Rect2EditMath`（Domain，無相依，可測試）

**檔案**：
- 新增 `src/FlashMeasurementSystem.Domain/Roi/Rect2EditMath.cs`
- 改 `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`

**動作 1**：建立檔案，內容如下（整檔照抄）：

```csharp
using System;

namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// rect2 互動編輯的純幾何運算（無 UI/HALCON 相依，可單元測試）。
    /// 慣例：座標 (row, col)，原點左上、row 向下；Phi 為 Length1 長軸對水平軸的弧度
    /// （數學正向），與 gen_rectangle2 / disp_rectangle2 一致。
    /// 主軸單位向量 e1 = (-sinφ, cosφ)；垂直軸 e2 = (cosφ, sinφ)。
    /// </summary>
    public static class Rect2EditMath
    {
        /// <summary>把手命中與縮放的半長下限（影像像素）。</summary>
        public const double MinHalfLen = 3.0;

        public enum HandleKind
        {
            None,    // 未命中
            Rotate,  // 旋轉把手
            Corner,  // 角把手：同時改 Length1, Length2
            Len1,    // 長軸端把手：只改 Length1
            Len2,    // 短軸端把手：只改 Length2
            Body     // 框內部：移動
        }

        /// <summary>主軸 e1 與垂直軸 e2 的單位向量（row, col）。</summary>
        public static void Axes(double phi,
            out double e1Row, out double e1Col, out double e2Row, out double e2Col)
        {
            e1Row = -Math.Sin(phi); e1Col = Math.Cos(phi);
            e2Row =  Math.Cos(phi); e2Col = Math.Sin(phi);
        }

        /// <summary>旋轉把手位置 = 中心 + (Length1 + knobGap) * e1。</summary>
        public static void RotateHandlePos(double cr, double cc, double phi, double l1, double knobGap,
            out double row, out double col)
        {
            Axes(phi, out double e1r, out double e1c, out double _e2r, out double _e2c);
            row = cr + (l1 + knobGap) * e1r;
            col = cc + (l1 + knobGap) * e1c;
        }

        /// <summary>角把手位置（中心 ± l1*e1 ± l2*e2）。s1,s2 各為 ±1。</summary>
        public static void CornerPos(double cr, double cc, double phi, double l1, double l2,
            int s1, int s2, out double row, out double col)
        {
            Axes(phi, out double e1r, out double e1c, out double e2r, out double e2c);
            row = cr + s1 * l1 * e1r + s2 * l2 * e2r;
            col = cc + s1 * l1 * e1c + s2 * l2 * e2c;
        }

        /// <summary>長軸端把手位置（中心 ± l1*e1）。s1 為 ±1。</summary>
        public static void Len1HandlePos(double cr, double cc, double phi, double l1, int s1,
            out double row, out double col)
        {
            Axes(phi, out double e1r, out double e1c, out double _e2r, out double _e2c);
            row = cr + s1 * l1 * e1r; col = cc + s1 * l1 * e1c;
        }

        /// <summary>短軸端把手位置（中心 ± l2*e2）。s2 為 ±1。</summary>
        public static void Len2HandlePos(double cr, double cc, double phi, double l2, int s2,
            out double row, out double col)
        {
            Axes(phi, out double _e1r, out double _e1c, out double e2r, out double e2c);
            row = cr + s2 * l2 * e2r; col = cc + s2 * l2 * e2c;
        }

        /// <summary>把向量 (dRow,dCol) 投影到 (e1,e2)，回傳沿兩軸的有號分量。</summary>
        public static void Project(double dRow, double dCol, double phi,
            out double along1, out double along2)
        {
            Axes(phi, out double e1r, out double e1c, out double e2r, out double e2c);
            along1 = dRow * e1r + dCol * e1c;
            along2 = dRow * e2r + dCol * e2c;
        }

        /// <summary>命中判定。優先序：Rotate > Corner > Len1/Len2 > Body > None。</summary>
        public static HandleKind HitTest(double pr, double pc,
            double cr, double cc, double phi, double l1, double l2,
            double tol, double knobGap)
        {
            RotateHandlePos(cr, cc, phi, l1, knobGap, out double rr, out double rc);
            if (Dist(pr, pc, rr, rc) <= tol) return HandleKind.Rotate;

            for (int s1 = -1; s1 <= 1; s1 += 2)
                for (int s2 = -1; s2 <= 1; s2 += 2)
                {
                    CornerPos(cr, cc, phi, l1, l2, s1, s2, out double hr, out double hc);
                    if (Dist(pr, pc, hr, hc) <= tol) return HandleKind.Corner;
                }

            for (int s1 = -1; s1 <= 1; s1 += 2)
            {
                Len1HandlePos(cr, cc, phi, l1, s1, out double hr, out double hc);
                if (Dist(pr, pc, hr, hc) <= tol) return HandleKind.Len1;
            }
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                Len2HandlePos(cr, cc, phi, l2, s2, out double hr, out double hc);
                if (Dist(pr, pc, hr, hc) <= tol) return HandleKind.Len2;
            }

            Project(pr - cr, pc - cc, phi, out double d1, out double d2);
            if (Math.Abs(d1) <= l1 && Math.Abs(d2) <= l2) return HandleKind.Body;

            return HandleKind.None;
        }

        /// <summary>依把手種類縮放（對稱繞中心，中心不動）。更新 ref l1/l2（已夾下限）。</summary>
        public static void ApplyResize(HandleKind kind, double mouseRow, double mouseCol,
            double cr, double cc, double phi, ref double l1, ref double l2)
        {
            Project(mouseRow - cr, mouseCol - cc, phi, out double d1, out double d2);
            if (kind == HandleKind.Corner)
            {
                l1 = Math.Max(MinHalfLen, Math.Abs(d1));
                l2 = Math.Max(MinHalfLen, Math.Abs(d2));
            }
            else if (kind == HandleKind.Len1)
            {
                l1 = Math.Max(MinHalfLen, Math.Abs(d1));
            }
            else if (kind == HandleKind.Len2)
            {
                l2 = Math.Max(MinHalfLen, Math.Abs(d2));
            }
        }

        /// <summary>旋轉：回傳滑鼠相對中心的角度（與 e1 同慣例）。</summary>
        public static double ApplyRotate(double mouseRow, double mouseCol, double cr, double cc)
        {
            return Math.Atan2(-(mouseRow - cr), mouseCol - cc);
        }

        private static double Dist(double r1, double c1, double r2, double c2)
        {
            double dr = r1 - r2, dc = c1 - c2;
            return Math.Sqrt(dr * dr + dc * dc);
        }
    }
}
```

**動作 2**：在 Domain csproj 找到這一行（line 83 附近）：

FIND：
```xml
    <Compile Include="Roi\RoiGeometry.cs" />
```
REPLACE（在其後加一行）：
```xml
    <Compile Include="Roi\RoiGeometry.cs" />
    <Compile Include="Roi\Rect2EditMath.cs" />
```

**驗證**：執行兩個 `dotnet build`，皆成功。

---

## T2：新增測試 `Rect2EditMathTests` 並接入 `Main()`

**檔案**：
- 新增 `tests/FlashMeasurementSystem.Tests/Rect2EditMathTests.cs`
- 改 `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`
- 改 `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`

**動作 1**：建立測試檔（整檔照抄）：

```csharp
using System;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Tests
{
    public static class Rect2EditMathTests
    {
        public static void Run()
        {
            TestAxes();
            TestRotate();
            TestHitTest();
            TestResizeSymmetric();
        }

        private static void TestAxes()
        {
            Rect2EditMath.Axes(0.0, out double e1r, out double e1c, out double e2r, out double e2c);
            AssertClose(0.0, e1r, "Axes(0) e1Row");
            AssertClose(1.0, e1c, "Axes(0) e1Col");
            AssertClose(1.0, e2r, "Axes(0) e2Row");
            AssertClose(0.0, e2c, "Axes(0) e2Col");

            Rect2EditMath.Axes(Math.PI / 2.0, out e1r, out e1c, out e2r, out e2c);
            AssertClose(-1.0, e1r, "Axes(pi/2) e1Row");
            AssertClose(0.0, e1c, "Axes(pi/2) e1Col");
        }

        private static void TestRotate()
        {
            AssertClose(0.0, Rect2EditMath.ApplyRotate(100, 200, 100, 100), "Rotate right -> 0");
            AssertClose(Math.PI / 2.0, Rect2EditMath.ApplyRotate(0, 100, 100, 100), "Rotate up -> pi/2");
        }

        private static void TestHitTest()
        {
            // rect: center (100,100), phi=0, l1=50, l2=30, tol=5, knobGap=25
            AssertEqual("Corner", Rect2EditMath.HitTest(130, 150, 100, 100, 0, 50, 30, 5, 25).ToString(), "HitTest corner");
            AssertEqual("Len1", Rect2EditMath.HitTest(100, 150, 100, 100, 0, 50, 30, 5, 25).ToString(), "HitTest len1");
            AssertEqual("Len2", Rect2EditMath.HitTest(130, 100, 100, 100, 0, 50, 30, 5, 25).ToString(), "HitTest len2");
            AssertEqual("Rotate", Rect2EditMath.HitTest(100, 175, 100, 100, 0, 50, 30, 5, 25).ToString(), "HitTest rotate");
            AssertEqual("Body", Rect2EditMath.HitTest(100, 100, 100, 100, 0, 50, 30, 5, 25).ToString(), "HitTest body");
            AssertEqual("None", Rect2EditMath.HitTest(100, 400, 100, 100, 0, 50, 30, 5, 25).ToString(), "HitTest none");
        }

        private static void TestResizeSymmetric()
        {
            double l1 = 50, l2 = 30;
            Rect2EditMath.ApplyResize(Rect2EditMath.HandleKind.Corner, 160, 170, 100, 100, 0, ref l1, ref l2);
            AssertClose(70.0, l1, "Resize corner l1");
            AssertClose(60.0, l2, "Resize corner l2");

            l1 = 50; l2 = 30;
            Rect2EditMath.ApplyResize(Rect2EditMath.HandleKind.Len1, 100, 101, 100, 100, 0, ref l1, ref l2);
            AssertClose(Rect2EditMath.MinHalfLen, l1, "Resize clamps to MinHalfLen");
        }

        private static void AssertClose(double expected, double actual, string msg)
        {
            if (Math.Abs(expected - actual) > 1e-6)
                throw new InvalidOperationException($"{msg}: expected {expected}, got {actual}");
        }

        private static void AssertEqual(string expected, string actual, string msg)
        {
            if (expected != actual)
                throw new InvalidOperationException($"{msg}: expected {expected}, got {actual}");
        }
    }
}
```

**動作 2**：測試 csproj 找到：

FIND：
```xml
    <Compile Include="RoiDomainTests.cs" />
```
REPLACE：
```xml
    <Compile Include="RoiDomainTests.cs" />
    <Compile Include="Rect2EditMathTests.cs" />
```

**動作 3**：`EdgeDetectionDomainTests.cs` 的 `Main()` 內，找到（line 128-129 附近）：

FIND：
```csharp
            RoiDomainTests.Run();
            Console.WriteLine("RoiDomainTests passed");
```
REPLACE：
```csharp
            RoiDomainTests.Run();
            Console.WriteLine("RoiDomainTests passed");
            Rect2EditMathTests.Run();
            Console.WriteLine("Rect2EditMathTests passed");
```

**驗證**：兩個 `dotnet build` 成功；執行測試 exe，輸出含 `Rect2EditMathTests passed`，行程回傳 0。

---

## T3：`OverlayAnnotator` 新增 `DrawEditRect2`（畫框 + 把手 + 旋轉鈕）

**檔案**：`src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs`

**動作 1**：檔案頂端 using 區，找到：

FIND：
```csharp
using System;
using System.Diagnostics;
using HalconDotNet;
```
REPLACE：
```csharp
using System;
using System.Diagnostics;
using HalconDotNet;
using FlashMeasurementSystem.Domain.Roi;
```

**動作 2**：在 `DrawRectangle2(...)` 方法的右大括號之後（即 line 32 `}` 之後），插入以下兩個方法：

```csharp

        /// <summary>
        /// 互動編輯用：畫綠色 rect2 編輯框 + 8 個把手（4 角 + 4 邊中點）+ 長軸端旋轉鈕。
        /// 與量測藍框區隔（綠＝可編輯）。把手大小/旋轉鈕間距由呼叫端以縮放換算後傳入（影像像素）。
        /// </summary>
        public void DrawEditRect2(double cr, double cc, double phi, double l1, double l2,
            double handleHalfSize, double knobGap)
        {
            HOperatorSet.SetColor(_window, "green");
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.SetDraw(_window, "margin");
            HOperatorSet.DispRectangle2(_window, cr, cc, phi, l1, l2);

            HOperatorSet.SetDraw(_window, "fill");
            for (int s1 = -1; s1 <= 1; s1 += 2)
                for (int s2 = -1; s2 <= 1; s2 += 2)
                {
                    Rect2EditMath.CornerPos(cr, cc, phi, l1, l2, s1, s2, out double hr, out double hc);
                    DispHandle(hr, hc, handleHalfSize, phi);
                }
            for (int s = -1; s <= 1; s += 2)
            {
                Rect2EditMath.Len1HandlePos(cr, cc, phi, l1, s, out double r1, out double c1);
                DispHandle(r1, c1, handleHalfSize, phi);
                Rect2EditMath.Len2HandlePos(cr, cc, phi, l2, s, out double r2, out double c2);
                DispHandle(r2, c2, handleHalfSize, phi);
            }

            Rect2EditMath.RotateHandlePos(cr, cc, phi, l1, knobGap, out double kr, out double kc);
            Rect2EditMath.Len1HandlePos(cr, cc, phi, l1, 1, out double er, out double ec);
            HOperatorSet.SetDraw(_window, "margin");
            HOperatorSet.SetColor(_window, "green");
            HOperatorSet.DispLine(_window, er, ec, kr, kc);
            HOperatorSet.SetDraw(_window, "fill");
            HOperatorSet.SetColor(_window, "yellow");
            HOperatorSet.DispCircle(_window, kr, kc, handleHalfSize);
        }

        private void DispHandle(double row, double col, double halfSize, double phi)
        {
            HOperatorSet.SetColor(_window, "green");
            HOperatorSet.DispRectangle2(_window, row, col, phi, halfSize, halfSize);
        }
```

**驗證**：兩個 `dotnet build` 成功。

---

## T4：`HWindowControlHelper` 新增編輯狀態、公開 API、繪製把手

**檔案**：`src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs`

**動作 1**：頂端 using，找到：

FIND：
```csharp
using System;
using System.Windows.Forms;
using HalconDotNet;
```
REPLACE：
```csharp
using System;
using System.Windows.Forms;
using HalconDotNet;
using FlashMeasurementSystem.Domain.Roi;
```

**動作 2**：欄位區，找到（line 33 附近）：

FIND：
```csharp
        private Action _persistentOverlayAction;
        private Action<double, double, double, double> _roiCallback;
```
REPLACE：
```csharp
        private Action _persistentOverlayAction;
        private Action<double, double, double, double> _roiCallback;

        // rect2 互動編輯狀態
        private bool _editActive;
        private double _editCenterRow, _editCenterCol, _editPhi, _editLen1, _editLen2;
        private Rect2EditMath.HandleKind _activeHandle = Rect2EditMath.HandleKind.None;
        private double _editLastRow, _editLastCol;
        private Action<double, double, double, double, double> _rect2Changed;
        private const double HandleScreenPx = 6.0;   // 把手命中容差 / 半邊長（螢幕像素）
        private const double KnobScreenPx = 22.0;     // 旋轉鈕距長軸端（螢幕像素）
```

**動作 3**：公開屬性區，找到：

FIND：
```csharp
        public double MouseRow { get; private set; }
        public double MouseCol { get; private set; }
        public OverlayAnnotator Annotator { get; }
```
REPLACE：
```csharp
        public double MouseRow { get; private set; }
        public double MouseCol { get; private set; }
        public OverlayAnnotator Annotator { get; }
        public bool IsEditingRect2 => _editActive;
```

**動作 4**：在 `SetPersistentOverlayAction(...)` 方法之後（line 151 `}` 之後）插入公開 API 與換算工具：

```csharp

        /// <summary>
        /// 開始/取代 rect2 互動編輯：顯示把手，並把改動透過 onChanged 回呼給呼叫端。
        /// onChanged 由本次呼叫綁定（單一擁有者）——最後一次呼叫者即為目前的編輯擁有者，
        /// 不會與其他畫面互相干擾。呼叫端在 onChanged 內只更新自己的模型/數值框，
        /// 不要呼叫 Redraw（本類別會自行重繪）。
        /// </summary>
        public void BeginRect2Edit(double centerRow, double centerCol, double phi,
            double len1, double len2, Action<double, double, double, double, double> onChanged)
        {
            _editCenterRow = centerRow; _editCenterCol = centerCol;
            _editPhi = phi; _editLen1 = len1; _editLen2 = len2;
            _rect2Changed = onChanged;
            _editActive = true;
            Redraw();
        }

        public void EndRect2Edit()
        {
            _editActive = false;
            _activeHandle = Rect2EditMath.HandleKind.None;
            _rect2Changed = null;
            Redraw();
        }

        // 螢幕像素 → 影像像素（用目前 col 方向縮放換算）
        private double ScreenPxToImage(double px) =>
            px * (_imgCol2 - _imgCol1) / Math.Max(1, _control.Width);
```

**動作 5**：在 `Redraw()` 方法尾端、最後一個右大括號 `}` 之前，找到：

FIND：
```csharp
            if (HasRoi && _persistentOverlayAction == null)
            {
                Annotator.DrawRoiRectangle(_roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol);
            }
        }
```
REPLACE：
```csharp
            if (HasRoi && _persistentOverlayAction == null)
            {
                Annotator.DrawRoiRectangle(_roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol);
            }

            if (_editActive)
            {
                double tolImg = ScreenPxToImage(HandleScreenPx);
                double knobImg = ScreenPxToImage(KnobScreenPx);
                Annotator.DrawEditRect2(_editCenterRow, _editCenterCol, _editPhi,
                    _editLen1, _editLen2, tolImg, knobImg);
            }
        }
```

**驗證**：兩個 `dotnet build` 成功（此時 `_rect2Changed`、`_activeHandle` 尚未被滑鼠事件用到，編譯允許）。

---

## T5：`HWindowControlHelper` 滑鼠事件接上命中/拖曳

**檔案**：`src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs`

**動作 1（MouseDown 加入命中）**：找到：

FIND：
```csharp
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (CurrentImage == null) return;
            if (e.Button == MouseButtons.Right) { _isPanning = true; _panStartX = e.X; _panStartY = e.Y; return; }
            if (e.Button == MouseButtons.Left && IsRoiMode)
```
REPLACE：
```csharp
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (CurrentImage == null) return;
            if (e.Button == MouseButtons.Right) { _isPanning = true; _panStartX = e.X; _panStartY = e.Y; return; }
            if (e.Button == MouseButtons.Left && _editActive && !IsRoiMode)
            {
                PixelToImage(e.X, e.Y, out double pr, out double pc);
                double tolImg = ScreenPxToImage(HandleScreenPx);
                double knobImg = ScreenPxToImage(KnobScreenPx);
                _activeHandle = Rect2EditMath.HitTest(pr, pc, _editCenterRow, _editCenterCol,
                    _editPhi, _editLen1, _editLen2, tolImg, knobImg);
                if (_activeHandle != Rect2EditMath.HandleKind.None)
                {
                    _editLastRow = pr; _editLastCol = pc;
                    return;
                }
            }
            if (e.Button == MouseButtons.Left && IsRoiMode)
```

**動作 2（MouseMove 加入拖曳分派）**：找到：

FIND：
```csharp
            PixelToImage(e.X, e.Y, out double row, out double col);
            MouseRow = row; MouseCol = col;
            MouseMoved?.Invoke(row, col);

            if (_isPanning)
```
REPLACE：
```csharp
            PixelToImage(e.X, e.Y, out double row, out double col);
            MouseRow = row; MouseCol = col;
            MouseMoved?.Invoke(row, col);

            if (_activeHandle != Rect2EditMath.HandleKind.None)
            {
                ApplyEditDrag(row, col);
                return;
            }

            if (_isPanning)
```

**動作 3（MouseUp 結束拖曳）**：找到：

FIND：
```csharp
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _isPanning = false;
            if (_isDrawingRoi)
```
REPLACE：
```csharp
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _isPanning = false;
            if (_activeHandle != Rect2EditMath.HandleKind.None)
            {
                _activeHandle = Rect2EditMath.HandleKind.None;
                return;
            }
            if (_isDrawingRoi)
```

**動作 4（新增拖曳套用方法）**：在 `OnMouseUp` 方法的右大括號之後、`PixelToImage(...)` 方法之前插入：

```csharp

        private void ApplyEditDrag(double row, double col)
        {
            switch (_activeHandle)
            {
                case Rect2EditMath.HandleKind.Body:
                    _editCenterRow += row - _editLastRow;
                    _editCenterCol += col - _editLastCol;
                    break;
                case Rect2EditMath.HandleKind.Rotate:
                    _editPhi = Rect2EditMath.ApplyRotate(row, col, _editCenterRow, _editCenterCol);
                    break;
                default: // Corner / Len1 / Len2
                    Rect2EditMath.ApplyResize(_activeHandle, row, col,
                        _editCenterRow, _editCenterCol, _editPhi, ref _editLen1, ref _editLen2);
                    break;
            }
            _editLastRow = row; _editLastCol = col;
            _rect2Changed?.Invoke(_editCenterRow, _editCenterCol, _editPhi, _editLen1, _editLen2);
            Redraw();
        }
```

**驗證**：兩個 `dotnet build` 成功。

---

## T6：MainWindow（Edge Detection 分頁）接線

**檔案**：`src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

**動作 1（新增中心暫存欄位）**：找到（line 45）：

FIND：
```csharp
        private EdgeDetectionRoi _latestEdgeRoi;
```
REPLACE：
```csharp
        private EdgeDetectionRoi _latestEdgeRoi;
        private double _edgeEditCenterRow, _edgeEditCenterCol;
```

**動作 2（畫完新框後自動進編輯）**：找到 `OnImageRoiSelected` 結尾（line 1556）：

FIND：
```csharp
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            ShowFittingOverlay();
        }
```
REPLACE：
```csharp
            _edgeStatusLabel.Text = "Draw ROI, then Detect";
            _edgeStatusLabel.ForeColor = Color.Black;
            ShowFittingOverlay();

            _edgeEditCenterRow = roi.CenterRow;
            _edgeEditCenterCol = roi.CenterCol;
            _imageHelper.BeginRect2Edit(roi.CenterRow, roi.CenterCol,
                _latestEdgeRoi.AngleRad, _latestEdgeRoi.Length1, _latestEdgeRoi.Length2,
                OnEdgeRect2Changed);
        }

        // 滑鼠互動改動 ROI 時的回呼：同步數值框 + 更新 _latestEdgeRoi。
        // 不在此呼叫 Redraw（helper 會自行重繪；persistent overlay 讀 _latestEdgeRoi 欄位）。
        private void OnEdgeRect2Changed(double centerRow, double centerCol,
            double phi, double len1, double len2)
        {
            _edgeEditCenterRow = centerRow;
            _edgeEditCenterCol = centerCol;
            _updatingEdgeRoiControls = true;
            try
            {
                _edgeScanLengthNumeric.Value = ClampNumericValue(_edgeScanLengthNumeric, (decimal)(len1 * 2.0));
                _edgeRoiWidthNumeric.Value = ClampNumericValue(_edgeRoiWidthNumeric, (decimal)(len2 * 2.0));
                _edgeAngleNumeric.Value = ClampNumericValue(_edgeAngleNumeric, (decimal)(phi * 180.0 / Math.PI));
            }
            finally
            {
                _updatingEdgeRoiControls = false;
            }
            _latestEdgeRoi = EdgeDetectionRoi.FromCenter(centerRow, centerCol, len1, len2, phi);
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

**動作 3（手打數值時同步把手；中心改用暫存值）**：找到整個 `OnEdgeRoiNumericChanged`（line 796-820）：

FIND：
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
REPLACE：
```csharp
        private void OnEdgeRoiNumericChanged(object sender, EventArgs e)
        {
            if (_updatingEdgeRoiControls || _imageHelper == null || _imageHelper.CurrentImage == null || !_imageHelper.IsEditingRect2)
            {
                return;
            }

            double phi = (double)_edgeAngleNumeric.Value * Math.PI / 180.0;
            double len1 = (double)_edgeScanLengthNumeric.Value / 2.0;
            double len2 = (double)_edgeRoiWidthNumeric.Value / 2.0;
            _latestEdgeRoi = EdgeDetectionRoi.FromCenter(_edgeEditCenterRow, _edgeEditCenterCol, len1, len2, phi);
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
            _imageHelper.BeginRect2Edit(_edgeEditCenterRow, _edgeEditCenterCol, phi, len1, len2, OnEdgeRect2Changed);
        }
```

> 注意：`CreateEdgeDetectionRoiFromNumeric` 與 `CreateEdgeDetectionRoi` 兩個方法**保留不動、不要刪**（可能有其他呼叫者）。

**驗證**：兩個 `dotnet build` 成功。

---

## T7：RecipeEditor 接線

**檔案**：`src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`

**動作 1（工具選取時顯示/隱藏編輯框）**：找到 `OnToolSelectionChanged`（line 743-756）：

FIND：
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
REPLACE：
```csharp
        private void OnToolSelectionChanged(object sender, EventArgs e)
        {
            int idx = _toolListBox.SelectedIndex;
            if (idx < 0 || idx >= _tools.Count)
            {
                _selectedTool = null;
                SetPropertyPanelEnabled(false);
                _imageHelper?.EndRect2Edit();
                return;
            }

            _selectedTool = _tools[idx];
            SetPropertyPanelEnabled(true);
            PopulateFromTool(_selectedTool);
            ShowRoiEditFor(_selectedTool);
        }

        // element 工具（circle/line）：顯示 rect2 編輯框與把手；其餘：隱藏。
        private void ShowRoiEditFor(MeasurementTool tool)
        {
            if (_imageHelper == null || _imageHelper.CurrentImage == null) return;
            bool isElement = tool != null && (tool.ToolType == "circle" || tool.ToolType == "line");
            if (isElement)
            {
                _imageHelper.ClearOverlay(); // 清掉主視窗殘留 overlay，只顯示編輯框
                _imageHelper.BeginRect2Edit(tool.Roi.CenterRow, tool.Roi.CenterCol,
                    tool.Roi.AngleRad, tool.Roi.Length1, tool.Roi.Length2, OnToolRect2Changed);
            }
            else
            {
                _imageHelper.EndRect2Edit();
            }
        }

        // 滑鼠互動改動 ROI 時的回呼：寫回 tool.Roi + 同步數值框。不在此呼叫 Redraw。
        private void OnToolRect2Changed(double centerRow, double centerCol,
            double phi, double len1, double len2)
        {
            if (_selectedTool == null) return;
            _selectedTool.Roi.CenterRow = centerRow;
            _selectedTool.Roi.CenterCol = centerCol;
            _selectedTool.Roi.AngleRad = phi;
            _selectedTool.Roi.Length1 = len1;
            _selectedTool.Roi.Length2 = len2;

            _updatingControls = true;
            try
            {
                _centerRowNumeric.Value = ClampDecimal(centerRow, _centerRowNumeric.Minimum, _centerRowNumeric.Maximum);
                _centerColNumeric.Value = ClampDecimal(centerCol, _centerColNumeric.Minimum, _centerColNumeric.Maximum);
                _length1Numeric.Value = ClampDecimal(len1, _length1Numeric.Minimum, _length1Numeric.Maximum);
                _length2Numeric.Value = ClampDecimal(len2, _length2Numeric.Minimum, _length2Numeric.Maximum);
                _angleRadNumeric.Value = ClampDecimal(phi, _angleRadNumeric.Minimum, _angleRadNumeric.Maximum);
            }
            finally
            {
                _updatingControls = false;
            }
            MarkDirty();
        }
```

**動作 2（手打數值時同步把手）**：找到 `WriteRoi`（line 446-455）：

FIND：
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
REPLACE：
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
            if (_imageHelper != null && _imageHelper.IsEditingRect2
                && (_selectedTool.ToolType == "circle" || _selectedTool.ToolType == "line"))
            {
                _imageHelper.BeginRect2Edit(_selectedTool.Roi.CenterRow, _selectedTool.Roi.CenterCol,
                    _selectedTool.Roi.AngleRad, _selectedTool.Roi.Length1, _selectedTool.Roi.Length2,
                    OnToolRect2Changed);
            }
        }
```

**動作 3（RequestRoi 擷取新框後顯示把手）**：找到 `ApplyRoiCapture` 結尾（line 943）：

FIND：
```csharp
            finally
            {
                _updatingControls = false;
            }
            MarkDirty();
        }
```
REPLACE：
```csharp
            finally
            {
                _updatingControls = false;
            }
            MarkDirty();
            ShowRoiEditFor(_selectedTool);
        }
```

**動作 4（關閉編輯器時清除把手）**：在 `RecipeEditor.cs` 搜尋 `OnFormClosed`。
- 若**找不到**任何 `OnFormClosed`：在類別內任一方法之間插入以下整個方法：
```csharp
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _imageHelper?.EndRect2Edit();
            base.OnFormClosed(e);
        }
```
- 若**已存在** `OnFormClosed`：在其方法本體的第一行加入 `_imageHelper?.EndRect2Edit();`。

**驗證**：兩個 `dotnet build` 成功。

---

## T8：最終建置 + 測試 + GUI 手動驗收

**動作**：
1. 關閉正在執行的 App（避免 DLL 鎖定）。
2. 執行兩個 `dotnet build`（Any CPU + x64），皆成功、無新增 error。
3. 執行測試 exe，輸出含 `Rect2EditMathTests passed`，回傳 0。
4. 啟動 App：`src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`，依下表手動驗收。

**GUI 驗收清單**：

MainWindow（Edge Detection 分頁）：
- [ ] 載入影像 → 拖曳畫一個 ROI → 放開後自動出現綠色編輯框 + 把手 + 旋轉鈕。
- [ ] 拖框內部可移動；數值框（中心對應）即時更新。
- [ ] 拖角把手同時改掃描長度與寬度、拖邊中點把手只改其一；皆對稱繞中心。
- [ ] 拖旋轉鈕可改角度，把手與框同步轉；Angle 數值框即時更新。
- [ ] 在數值框手打角度/長度 → 影像把手即時跟著移動（無抖動、無無限迴圈）。
- [ ] 斜框（Angle≠0）下按 Detect → 邊緣量測方向與綠框一致（非鏡像）。
- [ ] 滾輪縮放後，把手大小與命中範圍維持恆定。

RecipeEditor：
- [ ] 選取 circle/line 工具 → 影像出現該工具 ROI 的綠色編輯框 + 把手。
- [ ] 滑鼠移動/縮放/旋轉 → 右側 ROI 數值框（弧度）即時更新、配方標記 dirty。
- [ ] 在數值框手打 → 影像把手即時跟著移動。
- [ ] 選取 distance/angle（無 ROI）工具 → 編輯框消失。
- [ ] 關閉編輯器 → 編輯框/把手清除。

---

## 已知與設計取捨（執行者需知）

- MainWindow 偵測後 persistent overlay 會畫藍色量測框，與綠色編輯框重疊（綠在上）。這是**刻意**：綠＝可編輯、藍＝量測框，兩者中心/角度一致。
- 編輯回呼採「`BeginRect2Edit` 綁定單一 `onChanged`」而非事件多訂閱，確保 MainWindow 與 RecipeEditor 不會互相干擾（最後呼叫者為擁有者）。
- 旋轉角 `phi` 範圍為 `(-π, π]`，落在兩處 Angle 數值框允許範圍內（MainWindow ±360°、RecipeEditor ±2π）。
- helper 在每次拖曳改動後自行 `Redraw()`；host 回呼**不可**再呼叫 `Redraw`/`SetPersistentOverlayAction`，避免重複重繪與抖動。
- 不在本任務內刪除任何既有方法或既有 dead code（縮小變更面、易 review/rollback）。
```
