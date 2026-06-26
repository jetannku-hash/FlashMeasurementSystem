# Arc Caliper Interactive Drag Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add mouse drag-editing to the arc caliper ROI (center / radius / annulus / start-angle / end-angle handles), two-way synced with the existing numeric fields, mirroring the proven rect2 edit pattern.

**Architecture:** Pure unit-tested geometry (`ArcEditMath` in Domain, no HALCON/UI) drives both hit-testing and handle drawing. `HWindowControlHelper` gains a second, mutually-exclusive edit mode (`_arcEditActive`) alongside the existing rect2 mode. `MainWindow` owns the two-way sync between the drag callback and the six arc `NumericUpDown` controls, guarded by a re-entrancy flag, plus a checkbox to enter/leave edit mode. The orange/yellow scan band is still drawn by the existing `DrawFittingLayers`; the new green handles are drawn on top.

**Tech Stack:** .NET Framework 4.8, WinForms, HALCON 17.12 (`HalconDotNet`), C# LangVersion 7.3, console-style test runner (no framework).

**Angle convention (verified against shipped band, `gen_circle_contour_xld` + `gen_measure_arc`):**
- Forward: `row = cr - radius*sin(phi)`, `col = cc + radius*cos(phi)`
- Inverse angle: `phi = atan2(-(pr-cr), pc-cc)` ∈ (-π, π]
- `AngleExtent > 0` = counterclockwise.

**Repo root (git lives here):** `F:\C#\FlashMeasurementSystem\FlashMeasurementSystem`
**Build:** `dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64`
**Tests:** `.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe`
**Close the running app before any rebuild** (`Stop-Process -Name FlashMeasurementSystem.App.Wpf`) — it locks the output DLLs (MSB3026/27).

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `src/FlashMeasurementSystem.Domain/Roi/ArcEditMath.cs` | Pure arc edit geometry + `ArcHandle` enum | Create |
| `tests/FlashMeasurementSystem.Tests/ArcEditMathTests.cs` | Console-style unit tests for the math | Create |
| `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs` | Wire `ArcEditMathTests.Run()` into `Main()` | Modify |
| `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj` | Register new Domain file | Modify |
| `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj` | Register new test file | Modify |
| `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs` | `DrawEditArc` (green handles) | Modify |
| `src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs` | Arc edit state, mouse wiring, redraw, mutual exclusion | Modify |
| `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs` | Numeric ↔ drag two-way sync, edit toggle handler | Modify |
| `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs` | `_arcEditCheck` checkbox | Modify |

---

## Task 1: ArcEditMath pure geometry + ArcHandle enum

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/Roi/ArcEditMath.cs`
- Create: `tests/FlashMeasurementSystem.Tests/ArcEditMathTests.cs`
- Modify: `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs` (wire `Run()`)
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`

- [ ] **Step 1: Register the two new files in the old-style csprojs**

In `FlashMeasurementSystem.Domain.csproj`, find the `<ItemGroup>` containing `<Compile Include="Roi\Rect2EditMath.cs" />` and add directly after it:

```xml
    <Compile Include="Roi\ArcEditMath.cs" />
```

In `FlashMeasurementSystem.Tests.csproj`, find the `<Compile Include="Rect2EditMathTests.cs" />` line and add directly after it:

```xml
    <Compile Include="ArcEditMathTests.cs" />
```

- [ ] **Step 2: Write the failing test file**

Create `tests/FlashMeasurementSystem.Tests/ArcEditMathTests.cs`:

```csharp
using System;
using FlashMeasurementSystem.Domain.Roi;

namespace FlashMeasurementSystem.Tests
{
    // 弧形互動編輯純幾何測試（console-style；assert 以丟例外表示失敗）。
    public static class ArcEditMathTests
    {
        public static void Run()
        {
            TestPointOnArcConvention();
            TestAngleRoundTrip();
            TestHitTestEachHandle();
            TestHitTestCenterAndBandAndNone();
            TestApplyDragRadiusAndAnnulus();
            TestApplyDragAngleStartKeepsEndFixed();
            TestApplyDragAngleEndChangesExtent();
            Console.WriteLine("ArcEditMathTests passed");
        }

        // phi=0 -> 正右 (cr, cc+R)；phi=pi/2 -> 正上 (cr-R, cc)。
        private static void TestPointOnArcConvention()
        {
            ArcEditMath.PointOnArc(100, 200, 50, 0, out double r0, out double c0);
            Near(r0, 100); Near(c0, 250);
            ArcEditMath.PointOnArc(100, 200, 50, Math.PI / 2, out double r1, out double c1);
            Near(r1, 50); Near(c1, 200);
        }

        private static void TestAngleRoundTrip()
        {
            double phi = 0.7;
            ArcEditMath.PointOnArc(100, 200, 40, phi, out double r, out double c);
            Near(ArcEditMath.AngleOf(r, c, 100, 200), phi);
            Near(ArcEditMath.RadiusOf(r, c, 100, 200), 40);
        }

        // 把滑鼠放在每個把手的精確位置 -> 應命中該把手。
        private static void TestHitTestEachHandle()
        {
            double cr = 100, cc = 200, radius = 80, a0 = 0, extent = Math.PI / 2, annulus = 20, tol = 6;
            double aMid = a0 + extent / 2;

            ArcEditMath.PointOnArc(cr, cc, radius, a0, out double sr, out double sc);
            AssertHandle(ArcEditMath.HitTest(sr, sc, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.AngleStart);

            ArcEditMath.PointOnArc(cr, cc, radius, a0 + extent, out double er, out double ec);
            AssertHandle(ArcEditMath.HitTest(er, ec, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.AngleEnd);

            ArcEditMath.PointOnArc(cr, cc, radius, aMid, out double rr, out double rc);
            AssertHandle(ArcEditMath.HitTest(rr, rc, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.Radius);

            ArcEditMath.PointOnArc(cr, cc, radius + annulus, aMid, out double ar, out double ac);
            AssertHandle(ArcEditMath.HitTest(ar, ac, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.Annulus);
        }

        private static void TestHitTestCenterAndBandAndNone()
        {
            double cr = 100, cc = 200, radius = 80, a0 = 0, extent = Math.PI / 2, annulus = 20, tol = 6;
            // 中心點 -> Center
            AssertHandle(ArcEditMath.HitTest(cr, cc, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.Center);
            // 帶內、角度範圍內（中弧、中角度，距任何點把手 > tol 因 annulus=20）-> Center
            ArcEditMath.PointOnArc(cr, cc, radius - annulus, a0 + 0.1, out double br, out double bc);
            AssertHandle(ArcEditMath.HitTest(br, bc, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.Center);
            // 遠處 -> None
            AssertHandle(ArcEditMath.HitTest(cr + 500, cc + 500, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.None);
            // 帶外角度（範圍外）-> None
            ArcEditMath.PointOnArc(cr, cc, radius, -Math.PI / 2, out double or, out double oc);
            AssertHandle(ArcEditMath.HitTest(or, oc, cr, cc, radius, a0, extent, annulus, tol), ArcHandle.None);
        }

        private static void TestApplyDragRadiusAndAnnulus()
        {
            double cr = 100, cc = 200, radius = 80, a0 = 0, extent = Math.PI / 2, annulus = 20;
            // 把 Radius 把手拖到距中心 150 的點（任意角度）
            ArcEditMath.PointOnArc(cr, cc, 150, 0.3, out double pr, out double pc);
            ArcEditMath.ApplyDrag(ArcHandle.Radius, pr, pc, cr, cc, ref radius, ref a0, ref extent, ref annulus);
            Near(radius, 150); Near(annulus, 20);

            // 把 Annulus 把手拖到距中心 radius+35 -> annulus=35
            ArcEditMath.PointOnArc(cr, cc, radius + 35, 0.4, out double ar, out double ac);
            ArcEditMath.ApplyDrag(ArcHandle.Annulus, ar, ac, cr, cc, ref radius, ref a0, ref extent, ref annulus);
            Near(annulus, 35);

            // 夾限：Radius 拖到 0 -> 夾到 MinRadius
            ArcEditMath.ApplyDrag(ArcHandle.Radius, cr, cc, cr, cc, ref radius, ref a0, ref extent, ref annulus);
            Near(radius, ArcEditMath.MinRadius);
        }

        // 拖 AngleStart：另一端 (end = a0+extent) 固定，extent 跟著變。
        private static void TestApplyDragAngleStartKeepsEndFixed()
        {
            double cr = 100, cc = 200, radius = 80, a0 = 0, extent = Math.PI / 2, annulus = 20; // end = pi/2
            ArcEditMath.PointOnArc(cr, cc, radius, -Math.PI / 4, out double pr, out double pc);
            ArcEditMath.ApplyDrag(ArcHandle.AngleStart, pr, pc, cr, cc, ref radius, ref a0, ref extent, ref annulus);
            Near(a0, -Math.PI / 4);
            Near(a0 + extent, Math.PI / 2); // end 不動
        }

        private static void TestApplyDragAngleEndChangesExtent()
        {
            double cr = 100, cc = 200, radius = 80, a0 = 0, extent = Math.PI / 2, annulus = 20;
            ArcEditMath.PointOnArc(cr, cc, radius, Math.PI * 0.75, out double pr, out double pc);
            ArcEditMath.ApplyDrag(ArcHandle.AngleEnd, pr, pc, cr, cc, ref radius, ref a0, ref extent, ref annulus);
            Near(a0, 0);
            Near(extent, Math.PI * 0.75);
        }

        private static void Near(double actual, double expected, double tol = 1e-6)
        {
            if (Math.Abs(actual - expected) > tol)
                throw new InvalidOperationException($"Expected {expected}, got {actual}");
        }

        private static void AssertHandle(ArcHandle actual, ArcHandle expected)
        {
            if (actual != expected)
                throw new InvalidOperationException($"Expected handle {expected}, got {actual}");
        }
    }
}
```

- [ ] **Step 3: Wire the suite into Main()**

In `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`, find the line `Rect2EditMathTests.Run();` inside `Main()` and add directly after it:

```csharp
            ArcEditMathTests.Run();
```

- [ ] **Step 4: Build the test project to confirm it fails to compile (ArcEditMath missing)**

Run:
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: FAIL — `error CS0103: The name 'ArcEditMath' does not exist` (and `ArcHandle`).

- [ ] **Step 5: Write the ArcEditMath implementation**

Create `src/FlashMeasurementSystem.Domain/Roi/ArcEditMath.cs`:

```csharp
using System;

namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// 弧形量測 ROI 互動編輯的純幾何（無 HALCON / 無 UI 相依）。
    /// 角度慣例與 gen_circle_contour_xld / gen_measure_arc 一致：
    /// row = cr - R*sin(phi)，col = cc + R*cos(phi)；phi = atan2(-(pr-cr), pc-cc)；
    /// AngleExtent > 0 為逆時針。座標 (row, col)，row 向下。
    /// </summary>
    public static class ArcEditMath
    {
        /// <summary>半徑可縮放的最小值（對應 ArcMeasureRoi.IsDefined: Radius > 1）。</summary>
        public const double MinRadius = 2.0;

        /// <summary>環寬一半可縮放的最小值（對應 IsDefined: AnnulusRadius > 0.5）。</summary>
        public const double MinAnnulus = 1.0;

        private const double TwoPi = 2.0 * Math.PI;

        /// <summary>角度 phi（弧度）在半徑 radius 上的影像座標點。</summary>
        public static void PointOnArc(double cr, double cc, double radius, double phi,
            out double row, out double col)
        {
            row = cr - radius * Math.Sin(phi);
            col = cc + radius * Math.Cos(phi);
        }

        /// <summary>由影像點求方位角（弧度，(-pi, pi]）。</summary>
        public static double AngleOf(double pr, double pc, double cr, double cc)
        {
            return Math.Atan2(-(pr - cr), pc - cc);
        }

        /// <summary>由影像點求到弧心的半徑。</summary>
        public static double RadiusOf(double pr, double pc, double cr, double cc)
        {
            double dr = pr - cr, dc = pc - cc;
            return Math.Sqrt(dr * dr + dc * dc);
        }

        /// <summary>
        /// 命中判定（影像座標，tol 為影像像素）。先取最近的點把手
        /// (AngleStart/AngleEnd/Annulus/Radius)；否則中心或環帶內 -> Center；其餘 None。
        /// </summary>
        public static ArcHandle HitTest(double pr, double pc, double cr, double cc,
            double radius, double a0, double extent, double annulus, double tol)
        {
            double aMid = a0 + extent / 2.0;

            ArcHandle best = ArcHandle.None;
            double bestDist = tol;

            ConsiderPoint(ref best, ref bestDist, pr, pc, cr, cc, radius, a0, ArcHandle.AngleStart);
            ConsiderPoint(ref best, ref bestDist, pr, pc, cr, cc, radius, a0 + extent, ArcHandle.AngleEnd);
            ConsiderPoint(ref best, ref bestDist, pr, pc, cr, cc, radius + annulus, aMid, ArcHandle.Annulus);
            ConsiderPoint(ref best, ref bestDist, pr, pc, cr, cc, radius, aMid, ArcHandle.Radius);
            if (best != ArcHandle.None) return best;

            if (RadiusOf(pr, pc, cr, cc) <= tol) return ArcHandle.Center;

            double rad = RadiusOf(pr, pc, cr, cc);
            double rIn = Math.Max(0.0, radius - annulus);
            double rOut = radius + annulus;
            if (rad >= rIn - tol && rad <= rOut + tol &&
                InSweep(AngleOf(pr, pc, cr, cc), a0, extent))
                return ArcHandle.Center;

            return ArcHandle.None;
        }

        private static void ConsiderPoint(ref ArcHandle best, ref double bestDist,
            double pr, double pc, double cr, double cc, double r, double phi, ArcHandle handle)
        {
            PointOnArc(cr, cc, r, phi, out double hr, out double hc);
            double d = RadiusOf(pr, pc, hr, hc);
            if (d <= bestDist)
            {
                bestDist = d;
                best = handle;
            }
        }

        /// <summary>角度 ang 是否落在從 a0 起、掃 extent（有號）的弧上。</summary>
        public static bool InSweep(double ang, double a0, double extent)
        {
            if (extent >= 0)
            {
                double d = Norm0To2Pi(ang - a0);
                return d <= extent + 1e-9;
            }
            else
            {
                double dn = Norm0To2Pi(a0 - ang);
                return dn <= -extent + 1e-9;
            }
        }

        /// <summary>
        /// 依把手拖曳更新弧形參數。Center 不在此處理（由 helper 以位移平移中心）。
        /// Radius/Annulus 取與中心的距離；AngleStart 固定另一端、AngleEnd 改變張角。
        /// </summary>
        public static void ApplyDrag(ArcHandle handle, double pr, double pc,
            double cr, double cc, ref double radius, ref double a0, ref double extent, ref double annulus)
        {
            switch (handle)
            {
                case ArcHandle.Radius:
                    radius = Math.Max(MinRadius, RadiusOf(pr, pc, cr, cc));
                    break;
                case ArcHandle.Annulus:
                    annulus = Math.Max(MinAnnulus, RadiusOf(pr, pc, cr, cc) - radius);
                    break;
                case ArcHandle.AngleStart:
                    double end = a0 + extent;
                    double na0 = AngleOf(pr, pc, cr, cc);
                    extent = WrapExtent(end - na0, extent);
                    a0 = na0;
                    break;
                case ArcHandle.AngleEnd:
                    double na1 = AngleOf(pr, pc, cr, cc);
                    extent = WrapExtent(na1 - a0, extent);
                    break;
            }
        }

        // 把 raw 角度差調整成與 prevExtent 同向、量值 (0, 2pi] 的代表值，避免拖曳時 extent 變號跳動。
        private static double WrapExtent(double raw, double prevExtent)
        {
            double r = Norm0To2Pi(raw);
            if (prevExtent >= 0)
            {
                if (r < 1e-9) r = TwoPi;
                return r;
            }
            else
            {
                double rn = r - TwoPi;
                if (rn > -1e-9) rn = -TwoPi;
                return rn;
            }
        }

        private static double Norm0To2Pi(double a)
        {
            return a - TwoPi * Math.Floor(a / TwoPi);
        }
    }

    /// <summary>弧形互動把手種類（命中判定與拖曳模式共用）。</summary>
    public enum ArcHandle
    {
        None,
        Center,
        Radius,
        Annulus,
        AngleStart,
        AngleEnd
    }
}
```

- [ ] **Step 6: Build and run the tests**

Run:
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
```
Expected: build 0 errors; test output includes `ArcEditMathTests passed`; process exit code 0.

- [ ] **Step 7: Commit**

```powershell
git add src/FlashMeasurementSystem.Domain/Roi/ArcEditMath.cs tests/FlashMeasurementSystem.Tests/ArcEditMathTests.cs tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj
git commit -m "feat: add ArcEditMath pure geometry for arc caliper drag editing"
```

---

## Task 2: DrawEditArc green handle overlay

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs` (add method after `DrawEditRect2`, ~line 65)

No unit test (HALCON drawing is verified visually in Task 6, per project convention). This task is build-only.

- [ ] **Step 1: Add the DrawEditArc method**

In `OverlayAnnotator.cs`, immediately after the closing brace of `DrawEditRect2` (before `private void DrawHandleSquare`), insert:

```csharp
        /// <summary>
        /// 弧形互動編輯外觀：在中心、起角、終角、中弧(半徑)、外弧(環寬) 五處畫綠色實心把手方塊。
        /// 環帶本身（橘/黃弧）仍由 DrawFittingLayers 繪製，此處只疊加把手。
        /// handleHalf 為影像像素（由 helper 依縮放換算，確保螢幕恆定大小）。
        /// </summary>
        public void DrawEditArc(double cr, double cc, double radius, double a0, double extent,
            double annulus, double handleHalf)
        {
            HOperatorSet.SetColor(_window, "green");
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.SetDraw(_window, "fill");

            double aMid = a0 + extent / 2.0;
            ArcEditMath.PointOnArc(cr, cc, radius, a0, out double sr, out double sc);
            ArcEditMath.PointOnArc(cr, cc, radius, a0 + extent, out double er, out double ec);
            ArcEditMath.PointOnArc(cr, cc, radius, aMid, out double rr, out double rc);
            ArcEditMath.PointOnArc(cr, cc, radius + annulus, aMid, out double ar, out double ac);

            DrawHandleSquare(cr, cc, handleHalf);   // 中心（平移整個弧）
            DrawHandleSquare(sr, sc, handleHalf);   // 起始角
            DrawHandleSquare(er, ec, handleHalf);   // 終止角
            DrawHandleSquare(rr, rc, handleHalf);   // 半徑
            DrawHandleSquare(ar, ac, handleHalf);   // 環寬

            HOperatorSet.SetDraw(_window, "margin");
        }
```

(`OverlayAnnotator.cs` already has `using FlashMeasurementSystem.Domain.Roi;` and a private `DrawHandleSquare` — both reused.)

- [ ] **Step 2: Build**

Run:
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: build 0 errors.

- [ ] **Step 3: Commit**

```powershell
git add src/FlashMeasurementSystem.App.Wpf/OverlayAnnotator.cs
git commit -m "feat: add DrawEditArc handle overlay for arc caliper"
```

---

## Task 3: HWindowControlHelper arc edit mode

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs`

Build-only (interaction verified in Task 6).

- [ ] **Step 1: Add arc edit state fields**

In `HWindowControlHelper.cs`, find the rect2 edit fields (around line 36–40, the block with `private bool _editActive;` … `private Action<double, double, double, double, double> _editCallback;`). Add directly after that block:

```csharp
        // 弧形互動編輯狀態（與 rect2 編輯互斥；同一時間只會有一個 active）。
        private bool _arcEditActive;
        private FlashMeasurementSystem.Domain.Roi.ArcHandle _arcEditMode =
            FlashMeasurementSystem.Domain.Roi.ArcHandle.None;
        private double _arcCr, _arcCc, _arcRadius, _arcA0, _arcExtent, _arcAnnulus;
        private double _arcLastRow, _arcLastCol;
        private Action<double, double, double, double, double, double> _arcEditCallback;
```

- [ ] **Step 2: Add the IsEditingArc property**

Find `public bool IsEditingRect2 => _editActive;` (line 23) and add directly after it:

```csharp
        public bool IsEditingArc => _arcEditActive;
```

- [ ] **Step 3: Add BeginArcEdit / EndArcEdit and make rect2/arc mutually exclusive**

Find `EndRect2Edit()` (around line 197–203). Add directly after its closing brace:

```csharp
        /// <summary>開始/取代可編輯弧形，進入弧形編輯模式並重繪。與 rect2 編輯互斥。</summary>
        public void BeginArcEdit(double cr, double cc, double radius, double a0, double extent,
            double annulus, Action<double, double, double, double, double, double> onChanged)
        {
            _editActive = false;                 // 關閉 rect2 編輯，避免兩種模式同時 active
            _editMode = Rect2Handle.None;
            _arcCr = cr; _arcCc = cc; _arcRadius = radius;
            _arcA0 = a0; _arcExtent = extent; _arcAnnulus = annulus;
            _arcEditCallback = onChanged;
            _arcEditMode = FlashMeasurementSystem.Domain.Roi.ArcHandle.None;
            _arcEditActive = true;
            Redraw();
        }

        /// <summary>結束弧形編輯模式（隱藏把手），清回呼並重繪。</summary>
        public void EndArcEdit()
        {
            _arcEditActive = false;
            _arcEditMode = FlashMeasurementSystem.Domain.Roi.ArcHandle.None;
            _arcEditCallback = null;
            Redraw();
        }
```

In `BeginRect2Edit` (line 182–194), add `_arcEditActive = false;` directly after the existing `_editActive = true;` line so entering rect2 edit cancels any arc edit:

```csharp
            _editActive = true;
            _arcEditActive = false;
            Redraw();
```

- [ ] **Step 4: Draw arc handles in Redraw**

In `Redraw()`, find the rect2 edit block (lines 126–132, `if (_editActive) { … DrawEditRect2 … }`). Add directly after its closing brace:

```csharp
            if (_arcEditActive)
            {
                double half = ScreenPxToImage(5);
                Annotator.DrawEditArc(_arcCr, _arcCc, _arcRadius, _arcA0, _arcExtent, _arcAnnulus, half);
            }
```

- [ ] **Step 5: Hit-test on mouse down**

In `OnMouseDown` (line 233), find the rect2 hit-test block ending with the closing brace of `if (e.Button == MouseButtons.Left && _editActive && !IsRoiMode) { … }` (line 252). Add directly after it:

```csharp
            if (e.Button == MouseButtons.Left && _arcEditActive && !IsRoiMode)
            {
                PixelToImage(e.X, e.Y, out double pr, out double pc);
                double tol = ScreenPxToImage(8);
                FlashMeasurementSystem.Domain.Roi.ArcHandle h =
                    FlashMeasurementSystem.Domain.Roi.ArcEditMath.HitTest(
                        pr, pc, _arcCr, _arcCc, _arcRadius, _arcA0, _arcExtent, _arcAnnulus, tol);
                if (h != FlashMeasurementSystem.Domain.Roi.ArcHandle.None)
                {
                    _arcEditMode = h;
                    _arcLastRow = pr;
                    _arcLastCol = pc;
                    return;
                }
            }
```

- [ ] **Step 6: Drag on mouse move**

In `OnMouseMove`, find the rect2 edit branch `else if (_editMode != Rect2Handle.None) { … }` (lines 284–304). Add directly after its closing brace:

```csharp
            else if (_arcEditMode != FlashMeasurementSystem.Domain.Roi.ArcHandle.None)
            {
                if (_arcEditMode == FlashMeasurementSystem.Domain.Roi.ArcHandle.Center)
                {
                    _arcCr += row - _arcLastRow;
                    _arcCc += col - _arcLastCol;
                    _arcLastRow = row;
                    _arcLastCol = col;
                }
                else
                {
                    FlashMeasurementSystem.Domain.Roi.ArcEditMath.ApplyDrag(
                        _arcEditMode, row, col, _arcCr, _arcCc,
                        ref _arcRadius, ref _arcA0, ref _arcExtent, ref _arcAnnulus);
                }
                _arcEditCallback?.Invoke(_arcCr, _arcCc, _arcRadius, _arcA0, _arcExtent, _arcAnnulus);
                Redraw();
            }
```

- [ ] **Step 7: Release on mouse up**

In `OnMouseUp`, find the rect2 release block `if (_editMode != Rect2Handle.None) { _editMode = Rect2Handle.None; return; }` (lines 310–314). Add directly after it:

```csharp
            if (_arcEditMode != FlashMeasurementSystem.Domain.Roi.ArcHandle.None)
            {
                _arcEditMode = FlashMeasurementSystem.Domain.Roi.ArcHandle.None;
                return;
            }
```

- [ ] **Step 8: Reset arc edit in ClearRoi**

In `ClearRoi()` (lines 141–152), after the existing `_editCallback = null;` line, add:

```csharp
            _arcEditActive = false;
            _arcEditMode = FlashMeasurementSystem.Domain.Roi.ArcHandle.None;
            _arcEditCallback = null;
```

- [ ] **Step 9: Build**

Run:
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: build 0 errors.

- [ ] **Step 10: Commit**

```powershell
git add src/FlashMeasurementSystem.App.Wpf/HWindowControlHelper.cs
git commit -m "feat: add arc edit mode (state, hit-test, drag, redraw) to HWindowControlHelper"
```

---

## Task 4: MainWindow two-way numeric sync

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

Build-only (sync verified in Task 6).

- [ ] **Step 1: Add the re-entrancy guard field**

In `MainWindow.cs`, find `private bool _updatingEdgeRoiControls = false;` (line 1853) and add directly after it:

```csharp
        private bool _updatingArcControls = false;
```

- [ ] **Step 2: Add a helper to build an ArcMeasureRoi from the numeric controls**

Add a new method directly before `DetectArcButton_Click` (line 716):

```csharp
        // 由六個弧形數值框組出 ArcMeasureRoi（角度轉弧度）。供偵測、即時預覽、互動編輯共用。
        private ArcMeasureRoi BuildArcRoiFromControls()
        {
            return new ArcMeasureRoi
            {
                CenterRow = (double)_arcCenterRowNumeric.Value,
                CenterCol = (double)_arcCenterColNumeric.Value,
                Radius = (double)_arcRadiusNumeric.Value,
                AngleStart = (double)_arcAngleStartNumeric.Value * Math.PI / 180.0,
                AngleExtent = (double)_arcAngleExtentNumeric.Value * Math.PI / 180.0,
                AnnulusRadius = (double)_arcAnnulusNumeric.Value
            };
        }
```

Then replace the inline `var arcRoi = new ArcMeasureRoi { … };` block in `DetectArcButton_Click` (lines 725–733) with:

```csharp
            var arcRoi = BuildArcRoiFromControls();
```

- [ ] **Step 3: Add the numeric-changed and drag-callback handlers**

Add these methods directly after `OnEdgeRect2Changed` (after line 1867):

```csharp
        // 弧形數值框變更：更新 _latestArcRoi 並即時重畫環帶預覽；若正在互動編輯，刷新把手位置。
        // 由 OnArcRoiChanged 回寫數值時以 _updatingArcControls 抑制，避免回授迴圈。
        private void OnArcNumericChanged(object sender, EventArgs e)
        {
            if (_updatingArcControls || _imageHelper == null || _imageHelper.CurrentImage == null)
                return;

            _latestArcRoi = BuildArcRoiFromControls();
            if (!_latestArcRoi.IsDefined)
                return;

            ShowFittingOverlay();
            if (_imageHelper.IsEditingArc)
            {
                _imageHelper.BeginArcEdit(_latestArcRoi.CenterRow, _latestArcRoi.CenterCol,
                    _latestArcRoi.Radius, _latestArcRoi.AngleStart, _latestArcRoi.AngleExtent,
                    _latestArcRoi.AnnulusRadius, OnArcRoiChanged);
            }
        }

        // 滑鼠互動編輯弧形的回呼：回寫六個數值框（角度轉度，起角正規化到 0..360）與 _latestArcRoi。
        private void OnArcRoiChanged(double cr, double cc, double radius, double a0, double extent, double annulus)
        {
            double a0Deg = a0 * 180.0 / Math.PI;
            a0Deg -= 360.0 * Math.Floor(a0Deg / 360.0); // 正規化到 [0, 360)，配合數值框 Minimum=0
            double extentDeg = extent * 180.0 / Math.PI;

            _updatingArcControls = true;
            try
            {
                _arcCenterRowNumeric.Value = ClampNumericValue(_arcCenterRowNumeric, (decimal)cr);
                _arcCenterColNumeric.Value = ClampNumericValue(_arcCenterColNumeric, (decimal)cc);
                _arcRadiusNumeric.Value = ClampNumericValue(_arcRadiusNumeric, (decimal)radius);
                _arcAnnulusNumeric.Value = ClampNumericValue(_arcAnnulusNumeric, (decimal)annulus);
                _arcAngleStartNumeric.Value = ClampNumericValue(_arcAngleStartNumeric, (decimal)a0Deg);
                _arcAngleExtentNumeric.Value = ClampNumericValue(_arcAngleExtentNumeric, (decimal)extentDeg);
            }
            finally
            {
                _updatingArcControls = false;
            }

            _latestArcRoi = new ArcMeasureRoi
            {
                CenterRow = cr,
                CenterCol = cc,
                Radius = radius,
                AngleStart = a0,
                AngleExtent = extent,
                AnnulusRadius = annulus
            };
        }
```

- [ ] **Step 4: Build**

Run:
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: build 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
git commit -m "feat: two-way sync between arc numerics and drag callback"
```

---

## Task 5: Arc edit toggle checkbox

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs`
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs` (field wiring + toggle handler)

The arc panel (`arcMeasurePanel`) is a 4-col × 5-row `TableLayoutPanel`; row 4 currently holds only `detectArcButton` at cell (3,4). The checkbox goes in the empty cell (0,4), spanning 2 columns. No new row needed.

- [ ] **Step 1: Declare the checkbox field**

In `MainWindow.Designer.cs`, find `private System.Windows.Forms.Button detectArcButton;` (line 1811) and add directly after it:

```csharp
        private System.Windows.Forms.CheckBox _arcEditCheck;
```

- [ ] **Step 2: Instantiate the checkbox**

Find `this.detectArcButton = new System.Windows.Forms.Button();` (line 96) and add directly after it:

```csharp
            this._arcEditCheck = new System.Windows.Forms.CheckBox();
```

- [ ] **Step 3: Add the checkbox to the arc panel and configure it**

In the `arcMeasurePanel` section, find `this.arcMeasurePanel.Controls.Add(this.detectArcButton, 3, 4);` (line 970) and add directly after it:

```csharp
            this.arcMeasurePanel.Controls.Add(this._arcEditCheck, 0, 4);
            this.arcMeasurePanel.SetColumnSpan(this._arcEditCheck, 2);
```

Then find the `// detectArcButton` configuration block (line 1096–1103) and add directly after the `this.detectArcButton.Click += …;` line:

```csharp
            //
            // _arcEditCheck
            //
            this._arcEditCheck.Dock = System.Windows.Forms.DockStyle.Fill;
            this._arcEditCheck.Name = "_arcEditCheck";
            this._arcEditCheck.TabIndex = 8;
            this._arcEditCheck.Text = "互動編輯";
            this._arcEditCheck.UseVisualStyleBackColor = true;
            this._arcEditCheck.CheckedChanged += new System.EventHandler(this.ArcEditCheck_CheckedChanged);
```

- [ ] **Step 4: Wire the numeric ValueChanged events in the MainWindow constructor**

In `MainWindow.cs`, find the constructor block that wires the edge ROI numerics (lines 112–114, `_edgeAngleNumeric.ValueChanged += OnEdgeRoiNumericChanged;` …). Add directly after that block:

```csharp
            _arcCenterRowNumeric.ValueChanged += OnArcNumericChanged;
            _arcCenterColNumeric.ValueChanged += OnArcNumericChanged;
            _arcRadiusNumeric.ValueChanged += OnArcNumericChanged;
            _arcAnnulusNumeric.ValueChanged += OnArcNumericChanged;
            _arcAngleStartNumeric.ValueChanged += OnArcNumericChanged;
            _arcAngleExtentNumeric.ValueChanged += OnArcNumericChanged;
```

- [ ] **Step 5: Add the toggle handler**

In `MainWindow.cs`, add directly after `OnArcRoiChanged` (from Task 4):

```csharp
        // 互動編輯弧形開關：勾選 -> 以目前數值框內容進入拖曳編輯；取消 -> 離開編輯（保留數值與環帶）。
        private void ArcEditCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (_imageHelper == null) return;

            if (_arcEditCheck.Checked)
            {
                if (_imageHelper.CurrentImage == null)
                {
                    _updatingArcControls = true;
                    try { _arcEditCheck.Checked = false; } finally { _updatingArcControls = false; }
                    _edgeStatusLabel.Text = "Arc: 請先載入影像";
                    _edgeStatusLabel.ForeColor = Color.Red;
                    return;
                }

                _latestArcRoi = BuildArcRoiFromControls();
                if (!_latestArcRoi.IsDefined)
                {
                    _updatingArcControls = true;
                    try { _arcEditCheck.Checked = false; } finally { _updatingArcControls = false; }
                    _edgeStatusLabel.Text = "Arc ROI 無效: " + _latestArcRoi.ValidationError;
                    _edgeStatusLabel.ForeColor = Color.Red;
                    return;
                }

                ShowFittingOverlay();
                _imageHelper.BeginArcEdit(_latestArcRoi.CenterRow, _latestArcRoi.CenterCol,
                    _latestArcRoi.Radius, _latestArcRoi.AngleStart, _latestArcRoi.AngleExtent,
                    _latestArcRoi.AnnulusRadius, OnArcRoiChanged);
            }
            else
            {
                _imageHelper.EndArcEdit();
            }
        }
```

- [ ] **Step 6: Build (x64, HALCON-sensitive)**

First close the app if running:
```powershell
Stop-Process -Name FlashMeasurementSystem.App.Wpf -ErrorAction SilentlyContinue
```
Then:
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: build 0 errors.

- [ ] **Step 7: Commit**

```powershell
git add src/FlashMeasurementSystem.App.Wpf/MainWindow.Designer.cs src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
git commit -m "feat: add 互動編輯 checkbox to enter/leave arc drag editing"
```

---

## Task 6: Integration verification (launch + screenshot)

**Files:** none (verification + any fix found).

This is where the single empirical risk — a sign error in the angle→pixel mapping — surfaces. The math is internally consistent (forward `PointOnArc` and inverse `AngleOf` round-trip in tests), and it matches the convention the shipped band already uses, so handles should land on the band. Verify it.

- [ ] **Step 1: Re-run unit tests (regression guard)**

```powershell
.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
```
Expected: `ArcEditMathTests passed` and exit code 0.

- [ ] **Step 2: Launch the app**

```powershell
Start-Process .\src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe
```
(Do NOT run any stale `FlashMeasurementSystem.exe` — that is a pre-rename zombie build.)

- [ ] **Step 3: Manual verification checklist** (use webapp-testing/screenshot or ask the user to confirm)

1. Load an image with a circular feature (gear/holes).
2. Set arc center near the feature center, radius/annulus/angles to sane values.
3. Check **互動編輯** → five green square handles appear: at the band center, the start end, the end end, the mid of the yellow (radius) arc, and the mid of the outer (annulus) arc. **Confirm each handle sits ON the drawn band**, not mirrored across an axis. If mirrored, the sign in `ArcEditMath.PointOnArc` / `AngleOf` is wrong — fix both consistently and re-verify.
4. Drag the **radius** handle → band grows/shrinks radially; `Radius` numeric updates live.
5. Drag the **annulus** handle → band widens/narrows; `Annulus` numeric updates.
6. Drag the **start** and **end** handles → arc sweep changes; `Start deg` / `Ext deg` numerics update; the opposite end stays put when dragging start.
7. Drag the **center** handle → whole band translates; `Ctr Row`/`Ctr Col` update.
8. Type a new value into each numeric → band + handles move to match (reverse sync), no flicker/infinite loop.
9. Uncheck **互動編輯** → handles disappear, band remains.
10. Click **Detect Arc** → measurement still runs and finds edges on the band.

- [ ] **Step 4: Commit any fix discovered**

If a fix was needed:
```powershell
git add -A
git commit -m "fix: correct arc handle <describe> after visual verification"
```

- [ ] **Step 5: Update project memory**

Append to memory `arc-caliper-usability-improvements.md`: Option B (interactive drag) implemented — handles, two-way numeric sync, 互動編輯 toggle; ArcEditMath unit-tested; verified on real image.

---

## Self-Review

- **Spec coverage:** interactive drag (Tasks 1–3,5), numeric retained + two-way sync (Task 4), live band preview (Task 4 `OnArcNumericChanged` → `ShowFittingOverlay`), mode-switch so rect2/arc don't collide (Task 3 mutual exclusion). All covered.
- **Type consistency:** `ArcHandle` enum, `ArcEditMath.PointOnArc/AngleOf/RadiusOf/HitTest/ApplyDrag/InSweep`, `MinRadius/MinAnnulus`, `BeginArcEdit/EndArcEdit/IsEditingArc`, `OnArcNumericChanged/OnArcRoiChanged/ArcEditCheck_CheckedChanged`, `BuildArcRoiFromControls`, `_updatingArcControls`, `_arcEditCheck` — names used identically across tasks. The drag callback signature is 6-arg `Action<double,double,double,double,double,double>` in both helper and MainWindow.
- **Reused existing members:** `ClampNumericValue`, `ShowFittingOverlay`, `_latestArcRoi`, `DrawHandleSquare`, `ScreenPxToImage`, `PixelToImage` — all confirmed to exist.
- **Known edge case:** full circle (|extent| ≈ 2π) makes start/end handles coincide — acceptable for a caliper; clamps prevent degenerate radius/annulus.
