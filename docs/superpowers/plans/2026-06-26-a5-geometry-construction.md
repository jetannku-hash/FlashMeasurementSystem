# A5 幾何構造運算 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 由已擬合的 line/circle 構造出線交點、對稱中線、點到線投影，其結果可被下游 distance/angle 工具參照，撐起「兩線交點→到圓心距離」這類組合量測。

**Architecture:** 構造數學為純 Domain（`GeometryConstruction`，無 HALCON、單元測試）。引入 `GeometricPrimitive {Point|Line|Circle}`；每個工具結果填 `ToolRunResult.OutputPrimitive`。`RecipeRunner` 在 elements 與 composites 之間插入 Pass 1.5（constructions），distance/angle 改以「先把 ref 解析成 primitive、再路由到既有 measurer」消費它們——既有 HALCON measurer 內部不動。不做鏈式構造。

**Tech Stack:** .NET Framework 4.8, WinForms, HALCON 17.12, C# LangVersion 7.3, console-style test runner（無框架）。

**設計依據:** `docs/superpowers/specs/2026-06-26-a5-geometry-construction-design.md`（已確認）。

**Repo root:** `F:\C#\FlashMeasurementSystem\FlashMeasurementSystem`
**Build:** `dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64`
**Tests:** `.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe`
**重建前先關 app:** `Stop-Process -Name FlashMeasurementSystem.App.Wpf -ErrorAction SilentlyContinue`
**old-style csproj:** 新檔需手動 `<Compile Include>`。**Domain 不可** 參照 HALCON/UI。

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `src/...Domain/Geometry/GeometricPrimitive.cs` | 幾何基元 value object | Create |
| `src/...Domain/Geometry/GeometryConstruction.cs` | 純構造數學 | Create |
| `tests/.../GeometryConstructionDomainTests.cs` | Domain 單元測試 | Create |
| `tests/.../EdgeDetectionDomainTests.cs` | 接 Run() 進 Main() | Modify |
| `src/...Domain/FlashMeasurementSystem.Domain.csproj` | 註冊 2 新檔 | Modify |
| `tests/.../FlashMeasurementSystem.Tests.csproj` | 註冊 1 新測試檔 | Modify |
| `src/...App.Wpf/RecipeRunner.cs` | OutputPrimitive、Pass 1.5、distance/angle primitive 前門 | Modify |
| `src/...App.Wpf/MainWindow.cs` | overlay 構造繪製分支 | Modify |
| `src/...App.Wpf/RecipeEditor.cs` | 新增工具鈕、ref 過濾、ROI/公差顯示切換 | Modify |
| `src/...Domain/Roi/MeasurementTool.cs` | ToolType 註解 | Modify |
| `src/...Domain/Roi/Recipe.cs` | SchemaVersion v4 | Modify |

---

## Task 1: Domain `GeometricPrimitive` value object

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/Geometry/GeometricPrimitive.cs`
- Create: `tests/FlashMeasurementSystem.Tests/GeometryConstructionDomainTests.cs`
- Modify: `src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`

- [ ] **Step 1: Register new files in csprojs**

In `FlashMeasurementSystem.Domain.csproj`, find `<Compile Include="Roi\ArcEditMath.cs" />` and add after it:
```xml
    <Compile Include="Geometry\GeometricPrimitive.cs" />
    <Compile Include="Geometry\GeometryConstruction.cs" />
```

In `FlashMeasurementSystem.Tests.csproj`, find `<Compile Include="ArcEditMathTests.cs" />` and add after it:
```xml
    <Compile Include="GeometryConstructionDomainTests.cs" />
```

- [ ] **Step 2: Write the failing test file (primitive factories + TryAsPoint only for now)**

Create `tests/FlashMeasurementSystem.Tests/GeometryConstructionDomainTests.cs`:
```csharp
using System;
using FlashMeasurementSystem.Domain.Geometry;

namespace FlashMeasurementSystem.Tests
{
    // A5 幾何構造純數學測試（console-style；assert 以丟例外表示失敗）。
    public static class GeometryConstructionDomainTests
    {
        public static void Run()
        {
            TestPrimitiveFactories();
            TestTryAsPoint();
        }

        private static void TestPrimitiveFactories()
        {
            var p = GeometricPrimitive.Point(10, 20);
            if (p.Kind != GeometricPrimitiveKind.Point) throw new InvalidOperationException("Point kind");
            Near(p.Row, 10); Near(p.Col, 20);

            var l = GeometricPrimitive.Line(1, 2, 3, 4);
            if (l.Kind != GeometricPrimitiveKind.Line) throw new InvalidOperationException("Line kind");
            Near(l.Row1, 1); Near(l.Col1, 2); Near(l.Row2, 3); Near(l.Col2, 4);

            var c = GeometricPrimitive.Circle(50, 60, 25);
            if (c.Kind != GeometricPrimitiveKind.Circle) throw new InvalidOperationException("Circle kind");
            Near(c.CenterRow, 50); Near(c.CenterCol, 60); Near(c.RadiusPx, 25);
        }

        private static void TestTryAsPoint()
        {
            if (!GeometricPrimitive.Point(7, 8).TryAsPoint(out double pr, out double pc)) throw new InvalidOperationException("point->point");
            Near(pr, 7); Near(pc, 8);

            if (!GeometricPrimitive.Circle(3, 4, 9).TryAsPoint(out double cr, out double cc)) throw new InvalidOperationException("circle->center");
            Near(cr, 3); Near(cc, 4);

            if (GeometricPrimitive.Line(0, 0, 1, 1).TryAsPoint(out double _, out double _)) throw new InvalidOperationException("line should not be a point");
        }

        private static void Near(double actual, double expected, double tol = 1e-6)
        {
            if (Math.Abs(actual - expected) > tol)
                throw new InvalidOperationException($"Expected {expected}, got {actual}");
        }
    }
}
```

- [ ] **Step 3: Wire the suite into Main()**

In `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`, find:
```csharp
            ArcEditMathTests.Run();
            Console.WriteLine("ArcEditMathTests passed");
```
and add after it:
```csharp
            GeometryConstructionDomainTests.Run();
            Console.WriteLine("GeometryConstructionDomainTests passed");
```

- [ ] **Step 4: Build to confirm it fails (GeometricPrimitive missing)**

Run:
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: FAIL — `CS0246: GeometricPrimitive`/`GeometricPrimitiveKind` not found.

- [ ] **Step 5: Write GeometricPrimitive**

Create `src/FlashMeasurementSystem.Domain/Geometry/GeometricPrimitive.cs`:
```csharp
namespace FlashMeasurementSystem.Domain.Geometry
{
    public enum GeometricPrimitiveKind { Point, Line, Circle }

    /// <summary>
    /// 幾何基元 value object：統一承載量測工具的幾何輸出，供構造與下游 distance/angle 消費。
    /// 座標 (row, col)，row 向下。依 Kind 使用對應欄位。
    /// </summary>
    public sealed class GeometricPrimitive
    {
        public GeometricPrimitiveKind Kind { get; private set; }

        // Point
        public double Row { get; private set; }
        public double Col { get; private set; }

        // Line
        public double Row1 { get; private set; }
        public double Col1 { get; private set; }
        public double Row2 { get; private set; }
        public double Col2 { get; private set; }

        // Circle
        public double CenterRow { get; private set; }
        public double CenterCol { get; private set; }
        public double RadiusPx { get; private set; }

        public static GeometricPrimitive Point(double row, double col)
        {
            return new GeometricPrimitive { Kind = GeometricPrimitiveKind.Point, Row = row, Col = col };
        }

        public static GeometricPrimitive Line(double r1, double c1, double r2, double c2)
        {
            return new GeometricPrimitive
            {
                Kind = GeometricPrimitiveKind.Line,
                Row1 = r1, Col1 = c1, Row2 = r2, Col2 = c2
            };
        }

        public static GeometricPrimitive Circle(double centerRow, double centerCol, double radiusPx)
        {
            return new GeometricPrimitive
            {
                Kind = GeometricPrimitiveKind.Circle,
                CenterRow = centerRow, CenterCol = centerCol, RadiusPx = radiusPx
            };
        }

        /// <summary>取「點」語意：Point 回自身、Circle 回圓心、Line 回 false。</summary>
        public bool TryAsPoint(out double row, out double col)
        {
            if (Kind == GeometricPrimitiveKind.Point) { row = Row; col = Col; return true; }
            if (Kind == GeometricPrimitiveKind.Circle) { row = CenterRow; col = CenterCol; return true; }
            row = 0; col = 0; return false;
        }
    }
}
```

- [ ] **Step 6: Build + run tests**

Run:
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
```
Expected: 0 errors; output includes `GeometryConstructionDomainTests passed`; exit 0.

- [ ] **Step 7: Commit**

```powershell
git add src/FlashMeasurementSystem.Domain/Geometry/GeometricPrimitive.cs tests/FlashMeasurementSystem.Tests/GeometryConstructionDomainTests.cs tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs src/FlashMeasurementSystem.Domain/FlashMeasurementSystem.Domain.csproj tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj
git commit -m "feat: add GeometricPrimitive value object (A5 geometry construction)"
```

---

## Task 2: `GeometryConstruction` — line intersection + point projection

**Files:**
- Create: `src/FlashMeasurementSystem.Domain/Geometry/GeometryConstruction.cs`
- Modify: `tests/FlashMeasurementSystem.Tests/GeometryConstructionDomainTests.cs`

- [ ] **Step 1: Add failing tests for intersection + projection**

In `GeometryConstructionDomainTests.cs`, add these calls inside `Run()` (after `TestTryAsPoint();`):
```csharp
            TestLineIntersection();
            TestProjectPointOntoLine();
```
And add these methods to the class:
```csharp
        private static void TestLineIntersection()
        {
            // x 軸線 (row=0) 與 y 軸線 (col=0) 交於 (0,0)
            bool ok = GeometryConstruction.TryLineIntersection(
                0, -10, 0, 10,      // line A: row=0
                -10, 0, 10, 0,      // line B: col=0
                out double r, out double c);
            if (!ok) throw new InvalidOperationException("should intersect");
            Near(r, 0); Near(c, 0);

            // 平行（兩條 row=常數）→ false
            bool par = GeometryConstruction.TryLineIntersection(
                0, 0, 0, 10,
                5, 0, 5, 10,
                out double _, out double _);
            if (par) throw new InvalidOperationException("parallel should return false");

            // 一般相交：line A 過 (0,0)-(10,10)；line B 過 (0,10)-(10,0) → 交於 (5,5)
            bool ok2 = GeometryConstruction.TryLineIntersection(
                0, 0, 10, 10,
                0, 10, 10, 0,
                out double r2, out double c2);
            if (!ok2) throw new InvalidOperationException("should intersect 2");
            Near(r2, 5); Near(c2, 5);
        }

        private static void TestProjectPointOntoLine()
        {
            // 點 (5,5) 投影到 row=0 線 → 垂足 (0,5)
            GeometryConstruction.ProjectPointOntoLine(5, 5, 0, 0, 0, 10, out double fr, out double fc);
            Near(fr, 0); Near(fc, 5);

            // 點已在線上 → 回自身
            GeometryConstruction.ProjectPointOntoLine(0, 7, 0, 0, 0, 10, out double fr2, out double fc2);
            Near(fr2, 0); Near(fc2, 7);

            // 垂直線 col=3：點 (4,9) → 垂足 (4,3)
            GeometryConstruction.ProjectPointOntoLine(4, 9, 0, 3, 10, 3, out double fr3, out double fc3);
            Near(fr3, 4); Near(fc3, 3);
        }
```

- [ ] **Step 2: Build to confirm failure**

Run the build. Expected: FAIL — `CS0103: GeometryConstruction` not found.

- [ ] **Step 3: Create GeometryConstruction with intersection + projection**

Create `src/FlashMeasurementSystem.Domain/Geometry/GeometryConstruction.cs`:
```csharp
using System;

namespace FlashMeasurementSystem.Domain.Geometry
{
    /// <summary>
    /// A5 幾何構造純數學（無 HALCON / 無 UI）。座標 (row, col)，row 向下。
    /// </summary>
    public static class GeometryConstruction
    {
        /// <summary>方向外積絕對值門檻，低於此視為平行。</summary>
        public const double ParallelEpsilon = 1e-9;

        /// <summary>
        /// 兩條無限直線交點。以方向向量外積為分母求解；平行（|cross| &lt; eps）回 false。
        /// </summary>
        public static bool TryLineIntersection(
            double a_r1, double a_c1, double a_r2, double a_c2,
            double b_r1, double b_c1, double b_r2, double b_c2,
            out double row, out double col)
        {
            double dAr = a_r2 - a_r1, dAc = a_c2 - a_c1;
            double dBr = b_r2 - b_r1, dBc = b_c2 - b_c1;
            double denom = dAr * dBc - dAc * dBr;   // 方向外積
            if (Math.Abs(denom) < ParallelEpsilon)
            {
                row = 0; col = 0;
                return false;
            }
            // A1 + t*dA = B1 + s*dB，解 t
            double t = ((b_r1 - a_r1) * dBc - (b_c1 - a_c1) * dBr) / denom;
            row = a_r1 + t * dAr;
            col = a_c1 + t * dAc;
            return true;
        }

        /// <summary>
        /// 點 (pRow,pCol) 垂直投影到通過 (r1,c1)-(r2,c2) 的無限直線，回傳垂足。
        /// 線段退化（長度 0）時回傳起點，避免除以 0。
        /// </summary>
        public static void ProjectPointOntoLine(
            double pRow, double pCol,
            double r1, double c1, double r2, double c2,
            out double footRow, out double footCol)
        {
            double dRow = r2 - r1;
            double dCol = c2 - c1;
            double lenSq = dRow * dRow + dCol * dCol;
            if (lenSq < ParallelEpsilon)
            {
                footRow = r1;
                footCol = c1;
                return;
            }
            double t = ((pRow - r1) * dRow + (pCol - c1) * dCol) / lenSq;
            footRow = r1 + t * dRow;
            footCol = c1 + t * dCol;
        }
    }
}
```

- [ ] **Step 4: Build + run tests**

Run build then test exe. Expected: 0 errors, `GeometryConstructionDomainTests passed`, exit 0.

- [ ] **Step 5: Commit**

```powershell
git add src/FlashMeasurementSystem.Domain/Geometry/GeometryConstruction.cs tests/FlashMeasurementSystem.Tests/GeometryConstructionDomainTests.cs
git commit -m "feat: add line intersection + point-to-line projection (A5)"
```

---

## Task 3: `GeometryConstruction.Midline` (bisector / centerline)

**Files:**
- Modify: `src/FlashMeasurementSystem.Domain/Geometry/GeometryConstruction.cs`
- Modify: `tests/FlashMeasurementSystem.Tests/GeometryConstructionDomainTests.cs`

- [ ] **Step 1: Add failing test using the equidistance property**

In `GeometryConstructionDomainTests.cs`, add to `Run()` after `TestProjectPointOntoLine();`:
```csharp
            TestMidline();
```
Add these methods (the helper `PerpDistance` asserts the equidistance property):
```csharp
        private static void TestMidline()
        {
            // 平行兩線 row=0 與 row=10 → 置中線 row=5；線上各點到兩線垂距皆 = 5
            GeometryConstruction.Midline(
                0, 0, 0, 100,
                10, 0, 10, 100,
                out double r1, out double c1, out double r2, out double c2);
            AssertEquidistant(r1, c1, c2, r2, /*lineA*/0, 0, 0, 100, /*lineB*/10, 0, 10, 100, (mr, mc) =>
            {
                Near(PerpDistance(mr, mc, 0, 0, 0, 100), 5);
                Near(PerpDistance(mr, mc, 10, 0, 10, 100), 5);
            });

            // 相交兩線：row=0 與 col=0，交於 (0,0)。平分線上的點到兩線垂距相等。
            GeometryConstruction.Midline(
                0, -50, 0, 50,
                -50, 0, 50, 0,
                out double br1, out double bc1, out double br2, out double bc2);
            // 取平分線上兩個取樣點，驗證等距
            CheckEquidistantSample(br1, bc1, br2, bc2, 0, -50, 0, 50, -50, 0, 50, 0);
        }

        // 對 midline 端點之間取樣，套用 assertFn 驗證每個取樣點性質。
        private static void AssertEquidistant(double mr1, double mc1, double mc2, double mr2,
            double aR1, double aC1, double aR2, double aC2,
            double bR1, double bC1, double bR2, double bC2,
            Action<double, double> assertFn)
        {
            for (double t = 0.2; t <= 0.8; t += 0.3)
            {
                double mr = mr1 + t * (mr2 - mr1);
                double mc = mc1 + t * (mc2 - mc1);
                assertFn(mr, mc);
            }
        }

        private static void CheckEquidistantSample(double mr1, double mc1, double mr2, double mc2,
            double aR1, double aC1, double aR2, double aC2,
            double bR1, double bC1, double bR2, double bC2)
        {
            for (double t = 0.2; t <= 0.8; t += 0.3)
            {
                double mr = mr1 + t * (mr2 - mr1);
                double mc = mc1 + t * (mc2 - mc1);
                double da = PerpDistance(mr, mc, aR1, aC1, aR2, aC2);
                double db = PerpDistance(mr, mc, bR1, bC1, bR2, bC2);
                Near(da, db, 1e-6);
            }
        }

        private static double PerpDistance(double pr, double pc, double r1, double c1, double r2, double c2)
        {
            GeometryConstruction.ProjectPointOntoLine(pr, pc, r1, c1, r2, c2, out double fr, out double fc);
            double dr = pr - fr, dc = pc - fc;
            return Math.Sqrt(dr * dr + dc * dc);
        }
```

- [ ] **Step 2: Build to confirm failure**

Run build. Expected: FAIL — `CS0117/CS0103: Midline` not found on `GeometryConstruction`.

- [ ] **Step 3: Implement Midline**

In `GeometryConstruction.cs`, add this method inside the class (after `ProjectPointOntoLine`):
```csharp
        /// <summary>
        /// 對稱中線 / 角平分線（等距軌跡），回傳一條線段端點。
        /// 平行：置中線（與兩線平行、置於正中）。相交：角平分線，通過交點，
        /// 方向 = normalize(dirA') + normalize(dirB')（dirB' 依 dot 正負消除無向性歧義）。
        /// 端點長 = 兩輸入線段平均半長。
        /// </summary>
        public static void Midline(
            double a_r1, double a_c1, double a_r2, double a_c2,
            double b_r1, double b_c1, double b_r2, double b_c2,
            out double row1, out double col1, out double row2, out double col2)
        {
            double dAr = a_r2 - a_r1, dAc = a_c2 - a_c1;
            double dBr = b_r2 - b_r1, dBc = b_c2 - b_c1;
            double lenA = Math.Sqrt(dAr * dAr + dAc * dAc);
            double lenB = Math.Sqrt(dBr * dBr + dBc * dBc);
            double half = (lenA + lenB) / 4.0;  // 平均全長的一半 = 平均半長
            if (half < 1.0) half = 1.0;

            // 單位方向；退化線段以 (0,1) 代替避免除 0。
            double uAr = lenA > ParallelEpsilon ? dAr / lenA : 0.0;
            double uAc = lenA > ParallelEpsilon ? dAc / lenA : 1.0;
            double uBr = lenB > ParallelEpsilon ? dBr / lenB : 0.0;
            double uBc = lenB > ParallelEpsilon ? dBc / lenB : 1.0;

            // 消除線無向性：讓 B 方向與 A 同半邊。
            if (uAr * uBr + uAc * uBc < 0.0) { uBr = -uBr; uBc = -uBc; }

            double cross = dAr * dBc - dAc * dBr;
            double cr, cc;     // 中線通過點
            double dirR, dirC; // 中線方向

            if (Math.Abs(cross) < ParallelEpsilon)
            {
                // 平行：方向取 A，通過點取「A 中點與其在 B 上垂足」的中點。
                dirR = uAr; dirC = uAc;
                double aMidR = (a_r1 + a_r2) / 2.0, aMidC = (a_c1 + a_c2) / 2.0;
                ProjectPointOntoLine(aMidR, aMidC, b_r1, b_c1, b_r2, b_c2, out double footR, out double footC);
                cr = (aMidR + footR) / 2.0;
                cc = (aMidC + footC) / 2.0;
            }
            else
            {
                // 相交：方向取單位方向和（角平分線），通過點取交點。
                dirR = uAr + uBr; dirC = uAc + uBc;
                double dl = Math.Sqrt(dirR * dirR + dirC * dirC);
                if (dl < ParallelEpsilon) { dirR = uAr; dirC = uAc; dl = 1.0; }
                dirR /= dl; dirC /= dl;
                TryLineIntersection(a_r1, a_c1, a_r2, a_c2, b_r1, b_c1, b_r2, b_c2, out cr, out cc);
            }

            row1 = cr - half * dirR; col1 = cc - half * dirC;
            row2 = cr + half * dirR; col2 = cc + half * dirC;
        }
```

- [ ] **Step 4: Build + run tests**

Run build then test exe. Expected: 0 errors, `GeometryConstructionDomainTests passed`, exit 0.

- [ ] **Step 5: Commit**

```powershell
git add src/FlashMeasurementSystem.Domain/Geometry/GeometryConstruction.cs tests/FlashMeasurementSystem.Tests/GeometryConstructionDomainTests.cs
git commit -m "feat: add midline/bisector construction (A5)"
```

---

## Task 4: `ToolRunResult.OutputPrimitive` + populate elements in Pass 1

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs`

Build-only (consumed in later tasks).

- [ ] **Step 1: Add OutputPrimitive field + using**

In `RecipeRunner.cs`, ensure the file has `using FlashMeasurementSystem.Domain.Geometry;` at the top (add it next to the other `using FlashMeasurementSystem.Domain.*` lines if missing).

In the `ToolRunResult` class (the field block ending with `public string Message;`), add:
```csharp
        public GeometricPrimitive OutputPrimitive;  // A5：此工具的幾何輸出（resolver / 下游消費）
```

- [ ] **Step 2: Populate OutputPrimitive for circle and line elements**

In Pass 1, the element loop builds `res` then calls `MeasureCircle`/`MeasureLine`. After the `if (tool.ToolType == "circle") {...} else {...}` block and before `results.Add(res);`, add:
```csharp
                if (res.Measured && tool.ToolType == "circle")
                    res.OutputPrimitive = GeometricPrimitive.Circle(res.FitCenterRow, res.FitCenterCol, res.FitRadiusPx);
                else if (res.Measured && tool.ToolType == "line")
                    res.OutputPrimitive = GeometricPrimitive.Line(res.LineRow1, res.LineCol1, res.LineRow2, res.LineCol2);
```

- [ ] **Step 3: Build**

Run build. Expected: 0 errors.

- [ ] **Step 4: Commit**

```powershell
git add src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs
git commit -m "feat: ToolRunResult.OutputPrimitive populated for line/circle elements (A5)"
```

---

## Task 5: RecipeRunner Pass 1.5 — construction tools

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs`

Build-only.

- [ ] **Step 1: Skip construction types in Pass 2's element guard**

In Pass 2's loop, the first line currently is:
```csharp
                if (tool.ToolType == "circle" || tool.ToolType == "line") continue;  // 已於 Pass 1 處理
```
Replace with:
```csharp
                if (tool.ToolType == "circle" || tool.ToolType == "line") continue;  // 已於 Pass 1 處理
                if (tool.ToolType == "intersection" || tool.ToolType == "midline" || tool.ToolType == "projection") continue;  // 已於 Pass 1.5 處理
```

- [ ] **Step 2: Insert Pass 1.5 between Pass 1 and Pass 2**

Immediately after Pass 1's closing brace (the `foreach` that ends with `if (!string.IsNullOrEmpty(tool.Id)) byId[tool.Id] = res; }`) and before the `// ── Pass 2` comment, insert:
```csharp
            // ── Pass 1.5：構造工具（intersection / midline / projection）──
            // 僅參照基礎元件（line/circle），不支援鏈式構造。
            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null) continue;
                if (tool.ToolType != "intersection" && tool.ToolType != "midline" && tool.ToolType != "projection")
                    continue;

                var res = new ToolRunResult { Name = tool.Name, ToolType = tool.ToolType, Supported = true };
                MeasureConstruction(res, tool, byId);
                results.Add(res);
                if (!string.IsNullOrEmpty(tool.Id)) byId[tool.Id] = res;
            }
```

- [ ] **Step 3: Add the MeasureConstruction method**

Add this private method (place it just before `MeasureDistance`):
```csharp
        // A5 構造工具：intersection（兩 line→點）、midline（兩 line→線）、projection（circle 圓心投影到 line→點）。
        // 僅允許參照 line/circle 基礎元件；參照其他構造 → v1 不支援。
        private void MeasureConstruction(ToolRunResult res, MeasurementTool tool,
            Dictionary<string, ToolRunResult> byId)
        {
            if (tool.RefToolIds == null || tool.RefToolIds.Count < 2)
            {
                res.Measured = false;
                res.ValueText = "需 2 參考元素";
                res.Message = tool.ToolType + " 需 RefToolIds 含 2 個元素";
                return;
            }

            ToolRunResult a, b;
            if (!byId.TryGetValue(tool.RefToolIds[0], out a) || !byId.TryGetValue(tool.RefToolIds[1], out b))
            {
                res.Measured = false;
                res.ValueText = "找不到參考元素";
                res.Message = "RefToolIds 指向的元素不存在";
                return;
            }
            if (!a.Measured || !b.Measured)
            {
                res.Measured = false;
                res.ValueText = "參考元素未量測";
                return;
            }
            // 不支援鏈式構造：ref 必須是 line/circle 基礎元件。
            if (!IsBaseElement(a) || !IsBaseElement(b))
            {
                res.Measured = false;
                res.ValueText = "不支援鏈式構造";
                res.Message = "v1 構造只能參照 line/circle 基礎元件";
                return;
            }

            if (tool.ToolType == "intersection")
            {
                if (a.ToolType != "line" || b.ToolType != "line")
                {
                    res.Measured = false; res.ValueText = "需兩條線";
                    res.Message = "intersection 需兩個 line 元素"; return;
                }
                bool ok = GeometryConstruction.TryLineIntersection(
                    a.LineRow1, a.LineCol1, a.LineRow2, a.LineCol2,
                    b.LineRow1, b.LineCol1, b.LineRow2, b.LineCol2,
                    out double r, out double c);
                if (!ok)
                {
                    res.Measured = false; res.ValueText = "兩線平行，無交點";
                    res.Message = "intersection: 兩線平行"; return;
                }
                res.Measured = true;
                res.OutputPrimitive = GeometricPrimitive.Point(r, c);
                res.ValueText = string.Format(CultureInfo.InvariantCulture, "({0:F1},{1:F1})", r, c);
            }
            else if (tool.ToolType == "midline")
            {
                if (a.ToolType != "line" || b.ToolType != "line")
                {
                    res.Measured = false; res.ValueText = "需兩條線";
                    res.Message = "midline 需兩個 line 元素"; return;
                }
                GeometryConstruction.Midline(
                    a.LineRow1, a.LineCol1, a.LineRow2, a.LineCol2,
                    b.LineRow1, b.LineCol1, b.LineRow2, b.LineCol2,
                    out double r1, out double c1, out double r2, out double c2);
                res.Measured = true;
                res.LineRow1 = r1; res.LineCol1 = c1; res.LineRow2 = r2; res.LineCol2 = c2;
                res.OutputPrimitive = GeometricPrimitive.Line(r1, c1, r2, c2);
                res.ValueText = "中線";
            }
            else // projection
            {
                // ref = [circle, line]：圓心投影到線
                ToolRunResult circleElem = a.ToolType == "circle" ? a : (b.ToolType == "circle" ? b : null);
                ToolRunResult lineElem = a.ToolType == "line" ? a : (b.ToolType == "line" ? b : null);
                if (circleElem == null || lineElem == null)
                {
                    res.Measured = false; res.ValueText = "需 circle + line";
                    res.Message = "projection 需一個 circle 與一個 line"; return;
                }
                GeometryConstruction.ProjectPointOntoLine(
                    circleElem.FitCenterRow, circleElem.FitCenterCol,
                    lineElem.LineRow1, lineElem.LineCol1, lineElem.LineRow2, lineElem.LineCol2,
                    out double footRow, out double footCol);
                res.Measured = true;
                // 視覺化用：原點(圓心)→垂足 連線
                res.DistRow1 = circleElem.FitCenterRow; res.DistCol1 = circleElem.FitCenterCol;
                res.DistRow2 = footRow; res.DistCol2 = footCol;
                res.OutputPrimitive = GeometricPrimitive.Point(footRow, footCol);
                res.ValueText = string.Format(CultureInfo.InvariantCulture, "({0:F1},{1:F1})", footRow, footCol);
            }
        }

        private static bool IsBaseElement(ToolRunResult r)
        {
            return r.ToolType == "line" || r.ToolType == "circle";
        }
```

- [ ] **Step 3a: Confirm `using System.Globalization;` is present**

`MeasureConstruction` uses `CultureInfo.InvariantCulture`. The file already uses it (MeasureDistance does), so no change needed — verify the `using System.Globalization;` is at the top.

- [ ] **Step 4: Build**

Run build. Expected: 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs
git commit -m "feat: RecipeRunner Pass 1.5 construction tools (intersection/midline/projection) (A5)"
```

---

## Task 6: distance/angle primitive front-door

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs`

Build-only. Goal: distance/angle resolve refs to `OutputPrimitive` and route by Kind, so constructed points/lines are consumable. **Existing line/circle behavior must be preserved.**

- [ ] **Step 1: Add a primitive resolver + a Point extractor helper**

Add these private helpers (place before `MeasureDistance`):
```csharp
        // A5：把參考工具結果解析成幾何基元（即其 OutputPrimitive）。
        private static GeometricPrimitive ResolvePrimitive(ToolRunResult r)
        {
            return r != null ? r.OutputPrimitive : null;
        }
```

- [ ] **Step 2: Rewrite MeasureDistance's dispatch body to use primitives**

Replace the entire `if (a.ToolType == "line" && b.ToolType == "line") { ... } else { ... }` dispatch chain (from `if (a.ToolType == "line" && b.ToolType == "line")` down to the closing of the final `else { res.Measured = false; ... return; }`, i.e. everything between the `var dp = new DistanceMeasurementParameters {...};` block and the `if (tool.Tolerance != null)` block) with the following primitive-based routing:
```csharp
                GeometricPrimitive pa = ResolvePrimitive(a);
                GeometricPrimitive pb = ResolvePrimitive(b);
                if (pa == null || pb == null)
                {
                    res.Measured = false;
                    res.ValueText = "參考元素無幾何輸出";
                    res.Message = "ref 工具未提供 OutputPrimitive";
                    return;
                }

                DistanceMeasurementResult dr;
                if (pa.Kind == GeometricPrimitiveKind.Line && pb.Kind == GeometricPrimitiveKind.Line)
                {
                    dr = _distanceMeasurer.MeasureLineToLine(
                        pa.Row1, pa.Col1, pa.Row2, pa.Col2,
                        pb.Row1, pb.Col1, pb.Row2, pb.Col2, dp);
                    if (!FillDistance(res, dr)) return;
                    // 視覺化：line A 中點 → 在 line B 上的垂足（與既有行為一致）
                    double aMidRow = (pa.Row1 + pa.Row2) / 2.0, aMidCol = (pa.Col1 + pa.Col2) / 2.0;
                    ProjectPointOntoLine(aMidRow, aMidCol, pb.Row1, pb.Col1, pb.Row2, pb.Col2,
                        out double fR, out double fC);
                    res.DistRow1 = aMidRow; res.DistCol1 = aMidCol; res.DistRow2 = fR; res.DistCol2 = fC;
                }
                else if (pa.Kind == GeometricPrimitiveKind.Circle && pb.Kind == GeometricPrimitiveKind.Circle)
                {
                    dr = _distanceMeasurer.MeasureCircleToCircle(
                        pa.CenterRow, pa.CenterCol, pb.CenterRow, pb.CenterCol, dp);
                    if (!FillDistance(res, dr)) return;
                    res.DistRow1 = pa.CenterRow; res.DistCol1 = pa.CenterCol;
                    res.DistRow2 = pb.CenterRow; res.DistCol2 = pb.CenterCol;
                }
                else if (pa.Kind == GeometricPrimitiveKind.Line || pb.Kind == GeometricPrimitiveKind.Line)
                {
                    // 一邊是線、另一邊是點/圓（取其點：圓→圓心）→ 點到線
                    GeometricPrimitive linePrim = pa.Kind == GeometricPrimitiveKind.Line ? pa : pb;
                    GeometricPrimitive other = pa.Kind == GeometricPrimitiveKind.Line ? pb : pa;
                    if (!other.TryAsPoint(out double pr, out double pc))
                    {
                        res.Measured = false; res.ValueText = "不支援的距離組合";
                        res.Message = "line 對 line 以外，另一邊需可視為點"; return;
                    }
                    dr = _distanceMeasurer.MeasurePointToLine(pr, pc,
                        linePrim.Row1, linePrim.Col1, linePrim.Row2, linePrim.Col2, dp);
                    if (!FillDistance(res, dr)) return;
                    ProjectPointOntoLine(pr, pc, linePrim.Row1, linePrim.Col1, linePrim.Row2, linePrim.Col2,
                        out double fR, out double fC);
                    res.DistRow1 = pr; res.DistCol1 = pc; res.DistRow2 = fR; res.DistCol2 = fC;
                }
                else
                {
                    // 兩邊都是點/圓 → 點到點（圓取圓心）
                    if (!pa.TryAsPoint(out double ar, out double ac) || !pb.TryAsPoint(out double br, out double bc))
                    {
                        res.Measured = false; res.ValueText = "不支援的距離組合";
                        res.Message = "距離組合無法解析為點/線"; return;
                    }
                    dr = _distanceMeasurer.MeasurePointToPoint(ar, ac, br, bc, dp);
                    if (!FillDistance(res, dr)) return;
                    res.DistRow1 = ar; res.DistCol1 = ac; res.DistRow2 = br; res.DistCol2 = bc;
                }
                res.ValueText = string.Format(CultureInfo.InvariantCulture, "D={0:F4}mm", res.DistMm);
```

- [ ] **Step 3: Add the FillDistance helper**

Add this private helper (place after `MeasureDistance`):
```csharp
        // 把 measurer 結果寫入 res；失敗時設好訊息並回 false。
        private static bool FillDistance(ToolRunResult res, DistanceMeasurementResult dr)
        {
            if (dr == null || !dr.Success)
            {
                res.Measured = false;
                res.ValueText = "距離計算失敗";
                res.Message = dr != null ? dr.ErrorMessage : "null result";
                return false;
            }
            res.Measured = true;
            res.DistMm = dr.DistanceMm;
            return true;
        }
```

- [ ] **Step 4: Rewrite MeasureAngle's ref check to accept Line primitives (line or midline)**

In `MeasureAngle`, replace the block:
```csharp
            if (a.ToolType != "line" || b.ToolType != "line")
            {
                res.Measured = false;
                res.ValueText = "僅支援 line↔line";
                res.Message = "B2c 角度僅支援兩 line 元素";
                return;
            }
```
with:
```csharp
            GeometricPrimitive pa = ResolvePrimitive(a);
            GeometricPrimitive pb = ResolvePrimitive(b);
            if (pa == null || pb == null ||
                pa.Kind != GeometricPrimitiveKind.Line || pb.Kind != GeometricPrimitiveKind.Line)
            {
                res.Measured = false;
                res.ValueText = "角度需兩條線";
                res.Message = "角度量測需兩條線（可為構造中線）";
                return;
            }
```
Then in the same method, change the `_angleMeasurer.MeasureAngle(a.LineRow1, ...)` call to use the primitives:
```csharp
                AngleMeasurementResult ar = _angleMeasurer.MeasureAngle(
                    pa.Row1, pa.Col1, pa.Row2, pa.Col2,
                    pb.Row1, pb.Col1, pb.Row2, pb.Col2, ap);
```
And change the two `AngleCenterRow/Col` and `AngleStartRad` lines that reference `a.LineRow1` etc. to use `pa`/`pb`:
```csharp
                res.AngleCenterRow = (pa.Row1 + pa.Row2 + pb.Row1 + pb.Row2) / 4.0;
                res.AngleCenterCol = (pa.Col1 + pa.Col2 + pb.Col1 + pb.Col2) / 4.0;
                res.AngleRadiusPx = 80.0;
                res.AngleStartRad = Math.Atan2(pa.Row1 - res.AngleCenterRow, pa.Col1 - res.AngleCenterCol);
```

- [ ] **Step 5: Verify `MeasurePointToPoint` exists on the measurer interface**

`IDistanceMeasurer<TContour>` declares `MeasurePointToPoint(double row1, double col1, double row2, double col2, DistanceMeasurementParameters)` — confirmed in spec §3. No change needed; if a build error says it's missing, stop and report.

- [ ] **Step 6: Build**

Run build. Expected: 0 errors.

- [ ] **Step 7: Regression check — existing recipes unchanged**

This refactor must not change line↔line / circle↔circle / line↔circle behavior. Since elements now carry `OutputPrimitive` (Task 4) with identical coordinates, routing is equivalent. Verified end-to-end in Task 10.

- [ ] **Step 8: Commit**

```powershell
git add src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs
git commit -m "feat: distance/angle consume GeometricPrimitive refs (A5 front-door)"
```

---

## Task 7: Overlay drawing for construction tools

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

Build-only (visual verified in Task 10).

- [ ] **Step 1: Add construction draw branches**

In `DrawRecipeResults` (the `foreach (ToolRunResult r in results)` loop), find the angle branch:
```csharp
                    else if (r.Measured && r.ToolType == "angle")
                    {
                        double extent = r.AngleDeg * Math.PI / 180.0;
                        an.DrawAngle(r.AngleCenterRow, r.AngleCenterCol, r.AngleRadiusPx, r.AngleStartRad, extent, r.ValueText, r.IsOk);
                    }
```
Add directly after it:
```csharp
                    else if (r.Measured && r.ToolType == "intersection" && r.OutputPrimitive != null)
                    {
                        an.DrawCross(r.OutputPrimitive.Row, r.OutputPrimitive.Col, 15, "cyan");
                    }
                    else if (r.Measured && r.ToolType == "midline" && r.OutputPrimitive != null)
                    {
                        an.DrawLine(r.OutputPrimitive.Row1, r.OutputPrimitive.Col1,
                            r.OutputPrimitive.Row2, r.OutputPrimitive.Col2, "cyan");
                    }
                    else if (r.Measured && r.ToolType == "projection" && r.OutputPrimitive != null)
                    {
                        an.DrawLine(r.DistRow1, r.DistCol1, r.DistRow2, r.DistCol2, "cyan");      // 圓心→垂足
                        an.DrawCross(r.OutputPrimitive.Row, r.OutputPrimitive.Col, 12, "cyan");   // 垂足
                    }
```

- [ ] **Step 2: Confirm using for GeometricPrimitive type access**

`r.OutputPrimitive.Row` etc. are accessed via the `ToolRunResult` field. `MainWindow.cs` references `ToolRunResult` already; `GeometricPrimitive`'s members are public, so accessing them needs `using FlashMeasurementSystem.Domain.Geometry;` only if the type name is written — here we only access members, no type name, so it compiles. If a build error appears, add `using FlashMeasurementSystem.Domain.Geometry;` at the top of `MainWindow.cs`.

- [ ] **Step 3: Build**

Run build. Expected: 0 errors.

- [ ] **Step 4: Commit**

```powershell
git add src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
git commit -m "feat: overlay draw for construction tools (A5)"
```

---

## Task 8: RecipeEditor — add construction tools + ref filtering + show/hide

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`

Build x64 (HALCON-adjacent UI).

- [ ] **Step 1: Add construction tool buttons**

In `RecipeEditor.cs`, find the button definitions block:
```csharp
            _addAngleButton = new Button { Text = "+ Angle", Width = 70 };
            _addAngleButton.Click += (s, e) => AddTool("angle");
```
Add directly after:
```csharp
            _addIntersectionButton = new Button { Text = "+ 交點", Width = 70 };
            _addIntersectionButton.Click += (s, e) => AddTool("intersection");
            _addMidlineButton = new Button { Text = "+ 中線", Width = 70 };
            _addMidlineButton.Click += (s, e) => AddTool("midline");
            _addProjectionButton = new Button { Text = "+ 投影", Width = 70 };
            _addProjectionButton.Click += (s, e) => AddTool("projection");
```

- [ ] **Step 2: Declare the new button fields**

Find the field declaration for `_addAngleButton` (e.g. `private Button _addAngleButton;` or similar grouping). Add alongside it:
```csharp
        private Button _addIntersectionButton;
        private Button _addMidlineButton;
        private Button _addProjectionButton;
```
> If the existing add-buttons are declared inline (no separate field) and added to a parent panel via a `Controls.Add(...)` list, mirror that exact pattern instead: add the three new buttons to the SAME parent container the other add-buttons use, in the same place. Read the lines around the `_addAngleButton` parent `.Controls.Add` to match.

- [ ] **Step 3: Add the three buttons to their parent container**

Find where `_addAngleButton` is added to its parent (e.g. `someButtonPanel.Controls.Add(_addAngleButton);` or a `Controls.AddRange(new Control[]{...})`). Add the three new buttons to the same container immediately after `_addAngleButton`, preserving the existing style.

- [ ] **Step 4: Extend show/hide in PopulateFromTool**

In `PopulateFromTool`, find:
```csharp
                bool isElement = tool.ToolType == "circle" || tool.ToolType == "line";
                bool isComposite = tool.ToolType == "distance" || tool.ToolType == "angle";
```
Replace with:
```csharp
                bool isElement = tool.ToolType == "circle" || tool.ToolType == "line";
                bool isConstruction = tool.ToolType == "intersection" || tool.ToolType == "midline" || tool.ToolType == "projection";
                bool isComposite = tool.ToolType == "distance" || tool.ToolType == "angle";
                bool usesRefs = isComposite || isConstruction;
```
Then change the visibility lines:
```csharp
                _roiGroup.Visible = isElement;
                _edgeGroup.Visible = isElement;
                _refGroup.Visible = isComposite;
                _angleHintLabel.Visible = tool.ToolType == "line";
```
to:
```csharp
                _roiGroup.Visible = isElement;
                _edgeGroup.Visible = isElement;
                _refGroup.Visible = usesRefs;
                _angleHintLabel.Visible = tool.ToolType == "line";
```
And change the `else if (isComposite) { PopulateRefCombos(tool); }` to:
```csharp
                else if (usesRefs)
                {
                    PopulateRefCombos(tool);
                }
```

- [ ] **Step 5: Extend PopulateRefCombos filtering per construction/composite type**

Replace the body of `PopulateRefCombos` (the `bool allowLine`/`bool allowCircle` logic + the foreach filter) with type-specific allow-lists:
```csharp
        // distance: line/circle/intersection/midline/projection; angle: line/midline;
        // intersection/midline: line; projection: circle + line。
        private void PopulateRefCombos(MeasurementTool tool)
        {
            _ref1Combo.Items.Clear();
            _ref2Combo.Items.Clear();

            foreach (var t in _tools)
            {
                if (!IsAllowedRef(tool.ToolType, t.ToolType)) continue;
                var item = new ToolRef
                {
                    Id = t.Id,
                    Display = string.Format(CultureInfo.InvariantCulture, "{0} ({1})", t.Name, t.Id)
                };
                _ref1Combo.Items.Add(item);
                _ref2Combo.Items.Add(item);
            }

            string id1 = tool.RefToolIds.Count > 0 ? tool.RefToolIds[0] : null;
            string id2 = tool.RefToolIds.Count > 1 ? tool.RefToolIds[1] : null;
            SelectRefCombo(_ref1Combo, id1);
            SelectRefCombo(_ref2Combo, id2);
        }

        // 依「目前工具型別」決定可作為其參考的「候選工具型別」。
        private static bool IsAllowedRef(string ownerType, string candidateType)
        {
            switch (ownerType)
            {
                case "distance":
                    return candidateType == "line" || candidateType == "circle"
                        || candidateType == "intersection" || candidateType == "midline"
                        || candidateType == "projection";
                case "angle":
                    return candidateType == "line" || candidateType == "midline";
                case "intersection":
                case "midline":
                    return candidateType == "line";
                case "projection":
                    return candidateType == "line" || candidateType == "circle";
                default:
                    return false;
            }
        }
```

- [ ] **Step 6: Build (x64)**

First close the app:
```powershell
Stop-Process -Name FlashMeasurementSystem.App.Wpf -ErrorAction SilentlyContinue
```
Then:
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 7: Commit**

```powershell
git add src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs
git commit -m "feat: RecipeEditor construction tools + ref filtering + show/hide (A5)"
```

---

## Task 9: Schema bump + ToolType doc

**Files:**
- Modify: `src/FlashMeasurementSystem.Domain/Roi/Recipe.cs`
- Modify: `src/FlashMeasurementSystem.Domain/Roi/MeasurementTool.cs`

- [ ] **Step 1: Bump SchemaVersion to 4**

In `Recipe.cs`, change:
```csharp
        public int SchemaVersion { get; set; } = 3;
```
to:
```csharp
        public int SchemaVersion { get; set; } = 4;  // v4: A5 construction tools (intersection/midline/projection)
```

- [ ] **Step 2: Document new ToolType values**

In `MeasurementTool.cs`, change:
```csharp
        public string ToolType { get; set; } = "edge"; // edge / line / circle / distance / angle
```
to:
```csharp
        public string ToolType { get; set; } = "edge"; // edge / line / circle / distance / angle / intersection / midline / projection
```

- [ ] **Step 3: Build + run tests**

```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
```
Expected: 0 errors; all suites pass incl. `GeometryConstructionDomainTests passed`; exit 0.

- [ ] **Step 4: Commit**

```powershell
git add src/FlashMeasurementSystem.Domain/Roi/Recipe.cs src/FlashMeasurementSystem.Domain/Roi/MeasurementTool.cs
git commit -m "feat: schema v4 + document construction ToolTypes (A5)"
```

---

## Task 10: Integration verification (GUI manual)

**Files:** none (verification + any fix).

- [ ] **Step 1: Re-run unit tests**

```powershell
.\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe
```
Expected: `GeometryConstructionDomainTests passed`, exit 0.

- [ ] **Step 2: Launch app**

```powershell
Start-Process .\src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe
```

- [ ] **Step 3: Manual checklist** (use the GUI; ask the user to confirm if screen capture is unavailable)

1. Open RecipeEditor. Add two `line` tools on two edges (or one `circle` + two `line`).
2. Add an `intersection` tool → its panel hides ROI/Edge, shows only ref pickers; ref pickers list ONLY line tools. Pick the two lines.
3. Run Recipe → result table shows the intersection coordinate; overlay draws a cyan cross at the crossing.
4. Add a `distance` tool → its ref pickers now include the intersection tool and a circle. Pick the intersection + a circle. Run → distance from the intersection point to the circle center is measured; tolerance judging works if Spec set.
5. Add a `midline` tool (refs two lines) → overlay draws a cyan line; an `angle` tool can now reference the midline.
6. Add a `projection` tool (refs circle + line) → overlay draws a cyan perpendicular from the circle center to its foot on the line; downstream distance can reference the foot point.
7. Parallel lines into `intersection` → result shows "兩線平行，無交點", downstream tools referencing it fail with a clear message (no crash).
8. **Regression:** an existing line↔line / circle↔circle / line↔circle distance recipe still measures the same value as before.
9. Load an old v3 recipe (if any) → still loads.

- [ ] **Step 4: Commit any fix**

```powershell
git add -A
git commit -m "fix: <describe> after A5 integration verification"
```

- [ ] **Step 5: Update memory**

Update memory `a5-geometry-construction.md`: implemented + merged status, commit hashes, any deviations.

---

## Self-Review

- **Spec coverage:** GeometricPrimitive (T1), construction math intersection/projection (T2) + midline (T3), OutputPrimitive (T4), Pass 1.5 (T5), distance/angle front-door (T6), overlay (T7), RecipeEditor UI + ref filtering + show/hide (T8), schema v4 (T9), GUI verification incl. regression + old-recipe load (T10). All spec sections covered.
- **Type consistency:** `GeometricPrimitive` / `GeometricPrimitiveKind` / `Point|Line|Circle` factories / `TryAsPoint`; `GeometryConstruction.TryLineIntersection|ProjectPointOntoLine|Midline` / `ParallelEpsilon`; `ToolRunResult.OutputPrimitive`; `ResolvePrimitive` / `FillDistance` / `IsBaseElement` / `MeasureConstruction`; `IsAllowedRef`. Names used identically across tasks.
- **Reused existing members:** `MeasureLineToLine`/`MeasureCircleToCircle`/`MeasurePointToLine`/`MeasurePointToPoint`, `ProjectPointOntoLine` (existing private), `_judger.Judge`, `DrawCross`/`DrawLine`/`DrawDistance`/`DrawAngle`, `PopulateRefCombos`/`PopulateFromTool`/`AddTool`/`ToolRef`/`SelectRefCombo` — all confirmed to exist.
- **Risk note:** Task 6 is the only refactor of working code; mitigated because elements carry identical-coordinate `OutputPrimitive`, so routing is behavior-equivalent; Task 10 step 8 is the explicit regression gate.
- **Known deviation from spec:** the resolver is a one-line private helper `ResolvePrimitive` in RecipeRunner (reads `OutputPrimitive`) rather than a separate `GeometricPrimitiveResolver.cs` file — YAGNI, since `OutputPrimitive` IS the resolved primitive. Also Task 8 steps 2–3 may need to match however the existing add-buttons are declared/parented (read the surrounding lines and mirror).
