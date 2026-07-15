# Arc ROI in Recipe + Arc Caliper Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make arc ROIs a first-class recipe citizen and ship a real `ToolType="arc"` caliper tool that counts edges around a circle, judged by the existing tolerance machinery — unlocking gear / PCD / hole-array solutions later.

**Architecture:** Additive schema (v7) — `MeasurementTool.ArcRoi` reuses the existing `ArcMeasureRoi` DTO, null for every existing tool so old recipes are untouched. Pose-following reuses the existing `TransformRoi`, wrapped in a small **Application-layer** helper (`ArcRoiTransform`) so production (RecipeRunner) and the HALCON alignment test share one code path. The tool measures edge count and is judged by the existing `ToleranceSpec`/`ToleranceJudger`/CSV pipeline — zero new judgment or report code.

**Tech Stack:** .NET Framework 4.8, WinForms, HALCON 17.12, old-style `.csproj` (new files need explicit `<Compile Include>`), console-style test suites.

**Spec:** `docs/superpowers/specs/2026-07-15-arc-roi-recipe-tool-design.md`

**Branch:** create `feature/arc-roi-recipe-tool` before Task 1.

**Build/test commands (PowerShell):**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe"; "EXITCODE=$LASTEXITCODE"
& ".\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe"; "EXITCODE=$LASTEXITCODE"
```
Close the running app before building (it locks DLLs → MSB3026).

---

## Plan-level decision (refines spec §4.3 placement)

Spec §4.3 defines the arc-transform rule; it does **not** say where the code lives. Both test projects reference Domain/Application but **not** App.Wpf — so anything inside `RecipeRunner` is unreachable by tests. That is the structural cause of the Phase 2 verification hole (tests had to re-implement the flow instead of guarding it). Therefore the composition goes in **`Application/CoordinateSystem/ArcRoiTransform.cs`** (uses only the `ICoordinateMapper` interface + Domain types, no HALCON), and both `RecipeRunner` and the §7.2 alignment test call it. Rule and behaviour are exactly as specified.

## File Structure

**Create:**
- `src/FlashMeasurementSystem.Application/CoordinateSystem/ArcRoiTransform.cs` — shared, testable arc pose transform.
- `tests/FlashMeasurementSystem.Tests/ArcRecipeToolDomainTests.cs` — schema defaults, RecipeStore round-trip, backward compat, validator rules.

**Modify:**
- `src/FlashMeasurementSystem.Domain/Roi/MeasurementTool.cs` — `ArcRoi` field.
- `src/FlashMeasurementSystem.Domain/Roi/Recipe.cs` — SchemaVersion 6→7 + v7 comment.
- `src/FlashMeasurementSystem.Domain/Roi/RecipeValidator.cs` — `"arc"` in KnownTypes + arc ROI rule.
- `src/FlashMeasurementSystem.Application/EdgeDetection/IEdgeDetector.cs` — add `DetectEdgesOnArc`.
- `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj` — register ArcRoiTransform.cs.
- `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs` — `FakeEdgeDetector` must implement the new method; wire the new suite into `Main()`.
- `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj` — register the new test file.
- `tests/FlashMeasurementSystem.Tests.Halcon/CoordinateMapperTests.cs` — arc alignment test (spec §7.2).
- `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs` — `ToolRunResult` arc fields + Pass 1 arc branch.
- `src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs` — Add Arc button + arc panel + capture.
- `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs` — arc overlay in `DrawRecipeResults`.

---

## Task 1: Schema v7 (ArcRoi) + Validator + Domain tests

**Files:**
- Modify: `src/FlashMeasurementSystem.Domain/Roi/MeasurementTool.cs`
- Modify: `src/FlashMeasurementSystem.Domain/Roi/Recipe.cs`
- Modify: `src/FlashMeasurementSystem.Domain/Roi/RecipeValidator.cs`
- Create: `tests/FlashMeasurementSystem.Tests/ArcRecipeToolDomainTests.cs`
- Modify: `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs` (Main wiring only)

- [ ] **Step 1: Create the branch**
```bash
git checkout -b feature/arc-roi-recipe-tool
```

- [ ] **Step 2: Write the failing test `tests/FlashMeasurementSystem.Tests/ArcRecipeToolDomainTests.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Infrastructure.Roi;

namespace FlashMeasurementSystem.Tests
{
    // 弧形 ROI 進配方（schema v7）：預設值、序列化 round-trip、向後相容、Validator 規則。
    public static class ArcRecipeToolDomainTests
    {
        public static void Run()
        {
            // ─── 預設：非弧工具 ArcRoi 為 null（既有工具不受影響）───
            var plain = new MeasurementTool();
            AssertEqual(null, plain.ArcRoi, "Default ArcRoi is null");
            AssertEqual(7, Recipe.Default().SchemaVersion, "SchemaVersion is 7");

            // ─── RecipeStore round-trip：ArcRoi 六個欄位逐一保留 ───
            var arc = new ArcMeasureRoi
            {
                CenterRow = 250.5, CenterCol = 300.25, Radius = 120.75,
                AngleStart = 0.5, AngleExtent = 2.0 * Math.PI, AnnulusRadius = 8.5
            };
            var recipe = Recipe.Default();
            recipe.Name = "ARC-RT";
            recipe.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "A1", Name = "孔數", ToolType = "arc", ArcRoi = arc }
            };
            string path = Path.Combine(Path.GetTempPath(),
                "fms_arc_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                var store = new RecipeStore();
                store.Save(recipe, path);
                Recipe rt = store.Load(path);
                AssertEqual(1, rt.Tools.Count, "round-trip tool count");
                ArcMeasureRoi a = rt.Tools[0].ArcRoi;
                if (a == null) throw new InvalidOperationException("round-trip ArcRoi is null");
                AssertClose(250.5, a.CenterRow, 1e-9, "round-trip CenterRow");
                AssertClose(300.25, a.CenterCol, 1e-9, "round-trip CenterCol");
                AssertClose(120.75, a.Radius, 1e-9, "round-trip Radius");
                AssertClose(0.5, a.AngleStart, 1e-9, "round-trip AngleStart");
                AssertClose(2.0 * Math.PI, a.AngleExtent, 1e-9, "round-trip AngleExtent");
                AssertClose(8.5, a.AnnulusRadius, 1e-9, "round-trip AnnulusRadius");
                AssertEqual("arc", rt.Tools[0].ToolType, "round-trip ToolType");
            }
            finally { if (File.Exists(path)) File.Delete(path); }

            // ─── 向後相容：無 ArcRoi 欄位的舊 JSON → ArcRoi=null、其餘不受影響 ───
            string oldJson = "{ \"SchemaVersion\": 6, \"Name\": \"OLD\", \"Tools\": [ " +
                             "{ \"Id\": \"C1\", \"Name\": \"circle1\", \"ToolType\": \"circle\" } ] }";
            string oldPath = Path.Combine(Path.GetTempPath(),
                "fms_arc_old_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zcp");
            try
            {
                File.WriteAllText(oldPath, oldJson);
                Recipe old = new RecipeStore().Load(oldPath);
                AssertEqual(1, old.Tools.Count, "old recipe tool count");
                AssertEqual(null, old.Tools[0].ArcRoi, "old recipe ArcRoi is null");
                AssertEqual("circle", old.Tools[0].ToolType, "old recipe ToolType intact");
                AssertEqual("OLD", old.Name, "old recipe Name intact");
            }
            finally { if (File.Exists(oldPath)) File.Delete(oldPath); }

            // ─── Validator：合法 arc 工具 → 無 issue ───
            var okRecipe = Recipe.Default();
            okRecipe.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "A1", Name = "孔數", ToolType = "arc",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 200, CenterCol = 200, Radius = 100,
                        AngleStart = 0, AngleExtent = 2.0 * Math.PI, AnnulusRadius = 5 } }
            };
            AssertEqual(0, CountErrors(RecipeValidator.Validate(okRecipe, 640, 480)),
                "valid arc tool has no errors");

            // ─── Validator：arc 工具缺 ArcRoi → Error ───
            var missing = Recipe.Default();
            missing.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "A2", Name = "無弧", ToolType = "arc", ArcRoi = null }
            };
            if (CountErrors(RecipeValidator.Validate(missing, 640, 480)) == 0)
                throw new InvalidOperationException("arc tool without ArcRoi should be an Error");

            // ─── Validator：arc 工具 ArcRoi 無效（半徑 0）→ Error ───
            var bad = Recipe.Default();
            bad.Tools = new List<MeasurementTool>
            {
                new MeasurementTool { Id = "A3", Name = "壞弧", ToolType = "arc",
                    ArcRoi = new ArcMeasureRoi { CenterRow = 200, CenterCol = 200, Radius = 0,
                        AngleStart = 0, AngleExtent = 2.0 * Math.PI, AnnulusRadius = 5 } }
            };
            if (CountErrors(RecipeValidator.Validate(bad, 640, 480)) == 0)
                throw new InvalidOperationException("arc tool with invalid ArcRoi should be an Error");
        }

        private static int CountErrors(List<RecipeIssue> issues)
        {
            int n = 0;
            foreach (RecipeIssue i in issues)
                if (i.Severity == RecipeIssueSeverity.Error) n++;
            return n;
        }

        private static void AssertEqual<T>(T expected, T actual, string name)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }

        private static void AssertClose(double expected, double actual, double tol, string name)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new InvalidOperationException(name + " expected " + expected + " but got " + actual);
        }
    }
}
```

- [ ] **Step 3: Register the test file + wire it into `Main()`**

In `tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj`, next to the other suite entries:
```xml
    <Compile Include="ArcRecipeToolDomainTests.cs" />
```
In `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`, after the `DxfComparisonDomainTests.Run();` + its `Console.WriteLine` block:
```csharp
            ArcRecipeToolDomainTests.Run();
            Console.WriteLine("ArcRecipeToolDomainTests passed");
```

- [ ] **Step 4: Build to verify it FAILS**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: **compile error** — `MeasurementTool` has no `ArcRoi`. That is the intended failing state.

- [ ] **Step 5: Add `ArcRoi` to `MeasurementTool.cs`**

Add the using and the field (keep the existing `Roi`/`EdgeParameters`/`Tolerance`/`Gdt`/`RefToolIds` untouched):
```csharp
using FlashMeasurementSystem.Domain.EdgeDetection;   // already present (EdgeDetectionParameters)
```
```csharp
        // v7：弧形量測 ROI（重用既有 ArcMeasureRoi DTO）。null＝非弧工具，走既有 rect2 Roi。
        // 弧工具（ToolType="arc"）必填；Roi(rect2) 對弧工具無用但保留（加性模式，不改造 RoiGeometry）。
        public ArcMeasureRoi ArcRoi { get; set; } = null;
```

- [ ] **Step 6: Bump schema to v7 in `Recipe.cs`**

Change `public int SchemaVersion { get; set; } = 6;` to `= 7;` and add to the version comment block above it:
```csharp
        // v7：弧形 ROI（MeasurementTool.ArcRoi，加性 nullable 欄）+ ToolType="arc" 弧形卡尺工具。
        //     純加欄位、向後相容、無遷移碼：舊檔載入時 ArcRoi=null、無 arc 工具，1D 流程行為不變。
```

- [ ] **Step 7: Add the arc rule to `RecipeValidator.cs`**

Add `"arc"` to the `KnownTypes` HashSet (so it is not flagged as an unknown type):
```csharp
            "circle", "line", "edge", "arc",
```
Then, inside the per-tool loop (alongside the existing per-type checks), add the arc ROI rule:
```csharp
                if (tool.ToolType == "arc")
                {
                    if (tool.ArcRoi == null)
                    {
                        issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                            "弧形工具缺少弧形 ROI（ArcRoi）"));
                    }
                    else if (!tool.ArcRoi.IsDefined)
                    {
                        issues.Add(new RecipeIssue(RecipeIssueSeverity.Error, tool.Id, tool.Name,
                            "弧形 ROI 無效：" + tool.ArcRoi.ValidationError));
                    }
                }
```
Do **not** add `"arc"` to `RoiElementTypes` — that set drives the rect2 `Roi` geometry check, which does not apply to arc tools.

- [ ] **Step 8: Build + run tests to verify PASS**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe"; "EXITCODE=$LASTEXITCODE"
```
Expected: build 0/0; output includes `ArcRecipeToolDomainTests passed`; `EXITCODE=0`.

- [ ] **Step 9: Commit**
```bash
git add src/FlashMeasurementSystem.Domain/Roi/MeasurementTool.cs src/FlashMeasurementSystem.Domain/Roi/Recipe.cs src/FlashMeasurementSystem.Domain/Roi/RecipeValidator.cs tests/FlashMeasurementSystem.Tests/ArcRecipeToolDomainTests.cs tests/FlashMeasurementSystem.Tests/FlashMeasurementSystem.Tests.csproj tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
git commit -m "feat(arc): add ArcRoi to recipe schema v7 + validator rule with tests"
```

---

## Task 2: Add `DetectEdgesOnArc` to `IEdgeDetector<TImage>`

`RecipeRunner` only has the interface, so the arc branch cannot call the concrete adapter. `HalconEdgeDetector` already implements this exact signature — this only declares it. **There are exactly two implementers**: `HalconEdgeDetector` (already compliant) and the test `FakeEdgeDetector`, which must gain the method or the build breaks.

**Files:**
- Modify: `src/FlashMeasurementSystem.Application/EdgeDetection/IEdgeDetector.cs`
- Modify: `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`

- [ ] **Step 1: Declare the method on the interface**
```csharp
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Application.EdgeDetection
{
    public interface IEdgeDetector<TImage>
    {
        EdgeResult DetectEdges(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters);
        EdgeResult DetectEdgesSubPix(TImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters);
        // v7：弧形卡尺（gen_measure_arc）。HalconEdgeDetector 早已實作同簽章；
        // 加進介面讓 RecipeRunner（只持有介面）能執行弧形工具。ArcMeasureRoi 屬 Domain，介面維持 HALCON-free。
        EdgeResult DetectEdgesOnArc(TImage image, ArcMeasureRoi arcRoi, EdgeDetectionParameters parameters);
    }
}
```

- [ ] **Step 2: Build to see the Fake break (expected)**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: **compile error** in `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs` — `FakeEdgeDetector` does not implement `DetectEdgesOnArc`.

- [ ] **Step 3: Implement the method on `FakeEdgeDetector`**

In `tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs`, inside the `FakeEdgeDetector` class (around line 167), add a method mirroring the existing fakes' style (return a canned successful result so the contract test stays meaningful):
```csharp
            public EdgeResult DetectEdgesOnArc(object image, ArcMeasureRoi arcRoi, EdgeDetectionParameters parameters)
            {
                var r = new EdgeResult();
                r.EdgePoints.Add(new EdgePoint { Row = 1.0, Column = 2.0, Amplitude = 30.0, Distance = 0.0 });
                return r;
            }
```
(If the file lacks it, add `using FlashMeasurementSystem.Domain.EdgeDetection;` — it is already imported for the existing fakes.)

- [ ] **Step 4: Build + run tests**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe"; "EXITCODE=$LASTEXITCODE"
```
Expected: build 0/0; `EXITCODE=0`.

- [ ] **Step 5: Commit**
```bash
git add src/FlashMeasurementSystem.Application/EdgeDetection/IEdgeDetector.cs tests/FlashMeasurementSystem.Tests/EdgeDetectionDomainTests.cs
git commit -m "feat(arc): expose DetectEdgesOnArc on IEdgeDetector"
```

---

## Task 3: `ArcRoiTransform` helper + arc alignment test (spec §7.2 — mandatory)

This is the anti-verification-hole task. The helper lives in Application so the HALCON test can exercise **the same code the runner uses**, with the real `HalconCoordinateMapper`.

**Files:**
- Create: `src/FlashMeasurementSystem.Application/CoordinateSystem/ArcRoiTransform.cs`
- Modify: `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`
- Modify: `tests/FlashMeasurementSystem.Tests.Halcon/CoordinateMapperTests.cs`

- [ ] **Step 1: Write the failing test**

In `tests/FlashMeasurementSystem.Tests.Halcon/CoordinateMapperTests.cs`, add these usings if absent (`FlashMeasurementSystem.Application.CoordinateSystem`, `FlashMeasurementSystem.Domain.EdgeDetection`) and append this block inside `Run()` just before the final `Console.WriteLine("CoordinateMapperTests passed");`:
```csharp
            // ─── 弧 ROI 帶旋轉姿態對齊（spec §7.2；防 Phase 2 驗證洞重演）───
            // ref 姿態 (128,128,0°) → cur 姿態 (300,200,30°)。vector_angle_to_rigid 保證 ref 點精確映到 cur 點，
            // 故把弧心放在 ref 點上時，變換後弧心必落在 cur 點；起始角必增加 30°；半徑/範圍/環寬不得變動。
            var mapper2 = new FlashMeasurementSystem.Halcon.CoordinateSystem.HalconCoordinateMapper();
            double rot = 30.0 * Math.PI / 180.0;
            RigidTransform tArc = mapper2.CreateFromMatch(128, 128, 0.0, 300, 200, rot);
            var srcArc = new ArcMeasureRoi
            {
                CenterRow = 128, CenterCol = 128, Radius = 90,
                AngleStart = 0.2, AngleExtent = 2.0 * Math.PI, AnnulusRadius = 6
            };
            ArcMeasureRoi movedArc = ArcRoiTransform.TransformArc(mapper2, srcArc, tArc);
            Assert(Math.Abs(movedArc.CenterRow - 300.0) < Tol, "arc align: CenterRow ~300, got=" + movedArc.CenterRow.ToString("F2"));
            Assert(Math.Abs(movedArc.CenterCol - 200.0) < Tol, "arc align: CenterCol ~200, got=" + movedArc.CenterCol.ToString("F2"));
            Assert(Math.Abs(movedArc.AngleStart - (0.2 + rot)) < 1e-6, "arc align: AngleStart += 30°, got=" + movedArc.AngleStart.ToString("F4"));
            Assert(Math.Abs(movedArc.Radius - 90.0) < 1e-9, "arc align: Radius unchanged");
            Assert(Math.Abs(movedArc.AngleExtent - 2.0 * Math.PI) < 1e-9, "arc align: AngleExtent unchanged");
            Assert(Math.Abs(movedArc.AnnulusRadius - 6.0) < 1e-9, "arc align: AnnulusRadius unchanged");
```

- [ ] **Step 2: Build to verify it FAILS**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: **compile error** — `ArcRoiTransform` does not exist.

- [ ] **Step 3: Create `src/FlashMeasurementSystem.Application/CoordinateSystem/ArcRoiTransform.cs`**
```csharp
using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Domain.EdgeDetection;

namespace FlashMeasurementSystem.Application.CoordinateSystem
{
    /// <summary>
    /// 弧形 ROI 的姿態變換（跟隨工件）。剛體變換下：中心需變換、起始角需旋轉，
    /// 半徑/角度範圍/環寬不變（無縮放）。既有 ICoordinateMapper.TransformRoi 正好回傳
    /// 「變換後中心 + 旋轉後角度」，故直接重用、不需新增 mapper 方法。
    ///
    /// 放在 Application（只用介面 + Domain 型別、無 HALCON）而非 RecipeRunner：
    /// 測試專案不參考 App.Wpf，若放 Runner 內就無法被測試守護（Phase 2 驗證洞的結構性成因）。
    /// 如此 Runner 與 HALCON 對齊測試共用同一份程式碼。
    /// </summary>
    public static class ArcRoiTransform
    {
        /// <summary>回傳套用姿態後的新弧；transform 為 null 或無效時回傳原弧的複本（不變換）。</summary>
        public static ArcMeasureRoi TransformArc(ICoordinateMapper mapper, ArcMeasureRoi arc, RigidTransform transform)
        {
            if (arc == null) return null;
            if (mapper == null || transform == null || !transform.IsValid)
                return Copy(arc);

            TransformedRoi t = mapper.TransformRoi(arc.CenterRow, arc.CenterCol, arc.AngleStart, transform);
            return new ArcMeasureRoi
            {
                CenterRow = t.Row,
                CenterCol = t.Col,
                AngleStart = t.AngleRad,
                Radius = arc.Radius,
                AngleExtent = arc.AngleExtent,
                AnnulusRadius = arc.AnnulusRadius
            };
        }

        private static ArcMeasureRoi Copy(ArcMeasureRoi a)
        {
            return new ArcMeasureRoi
            {
                CenterRow = a.CenterRow, CenterCol = a.CenterCol, Radius = a.Radius,
                AngleStart = a.AngleStart, AngleExtent = a.AngleExtent, AnnulusRadius = a.AnnulusRadius
            };
        }
    }
}
```

- [ ] **Step 4: Register in the Application `.csproj`**

In `src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj`, next to the other `<Compile Include>` entries:
```xml
    <Compile Include="CoordinateSystem\ArcRoiTransform.cs" />
```

- [ ] **Step 5: Build + run the HALCON test suite to verify PASS**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe"; "EXITCODE=$LASTEXITCODE"
```
Expected: build 0/0; output includes `CoordinateMapperTests passed`; `EXITCODE=0`.

- [ ] **Step 6: Prove the test bites (spec §7.2 requirement)**

Temporarily break the production code: in `ArcRoiTransform.TransformArc`, change `AngleStart = t.AngleRad` to `AngleStart = arc.AngleStart` (i.e. forget to rotate). Rebuild + rerun the HALCON suite → the test **must fail** with the `arc align: AngleStart += 30°` message. Then revert the sabotage, rebuild, and confirm it passes again. Record both outputs in your report.

- [ ] **Step 7: Commit**
```bash
git add src/FlashMeasurementSystem.Application/CoordinateSystem/ArcRoiTransform.cs src/FlashMeasurementSystem.Application/FlashMeasurementSystem.Application.csproj tests/FlashMeasurementSystem.Tests.Halcon/CoordinateMapperTests.cs
git commit -m "feat(arc): add testable ArcRoiTransform + rotated-pose alignment test"
```

---

## Task 4: RecipeRunner arc branch

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs`

- [ ] **Step 1: Add arc fields to `ToolRunResult`**

`ToolRunResult` is a public-field class. Add next to the metrology point lists (follow the existing parallel-list convention used by `MetrologyMeasureRows/Cols`):
```csharp
        // v7 弧形工具：變換後的弧（供 overlay 畫弧）與偵測到的邊點（供 overlay 十字）。
        public ArcMeasureRoi PlacedArc;
        public List<double> ArcEdgeRows = new List<double>();
        public List<double> ArcEdgeCols = new List<double>();
```
Add `using FlashMeasurementSystem.Domain.EdgeDetection;` if not already present (it is, for `EdgeDetectionParameters`).

- [ ] **Step 2: Add the arc branch to Pass 1**

In `Run(...)`, inside the Pass 1 loop, the current guard is:
```csharp
                if (tool.ToolType != "circle" && tool.ToolType != "line") continue;
```
Arc tools are self-sufficient element tools but use `ArcRoi` (not `Roi`), so give them their own loop **immediately after the Pass 1 loop** (before Pass 1.5), keeping the existing loop untouched:
```csharp
            // ── Pass 1.2：弧形卡尺工具（自足元素工具；量圓周邊數）──
            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null || tool.ToolType != "arc") continue;
                if (tool.ArcRoi == null) continue;   // Validator 已擋；此處防禦性略過

                ArcMeasureRoi placed = FlashMeasurementSystem.Application.CoordinateSystem.ArcRoiTransform
                    .TransformArc(_mapper, tool.ArcRoi, transform);

                var res = new ToolRunResult
                {
                    Name = tool.Name,
                    ToolType = tool.ToolType,
                    Roi = new PlacedRoi
                    {
                        Row = placed.CenterRow, Col = placed.CenterCol, AngleRad = placed.AngleStart,
                        Length1 = placed.Radius, Length2 = placed.AnnulusRadius, Name = tool.Name
                    },
                    PlacedArc = placed,
                    Supported = true
                };

                EdgeResult er = _edgeDetector.DetectEdgesOnArc(image, placed, tool.EdgeParameters);
                if (!er.Success)
                {
                    res.Measured = false;
                    res.Message = string.IsNullOrEmpty(er.ErrorMessage) ? "弧形卡尺量測失敗" : er.ErrorMessage;
                    results.Add(res);
                    byId[tool.Id ?? ""] = res;
                    continue;
                }

                foreach (EdgePoint p in er.EdgePoints)
                {
                    res.ArcEdgeRows.Add(p.Row);
                    res.ArcEdgeCols.Add(p.Column);
                }

                double count = er.EdgePoints.Count;
                res.Measured = true;
                res.ValueText = string.Format(CultureInfo.InvariantCulture, "邊數={0}", er.EdgePoints.Count);
                JudgeSingle(res, tool, count);   // 沿用既有單值判定（見 Step 3）
                results.Add(res);
                byId[tool.Id ?? ""] = res;
            }
```
Note: `transform` is the local already computed at the top of `Run` (null when there is no reference pose/match) — `TransformArc` returns an unchanged copy in that case, so no branching is needed here.

- [ ] **Step 3: Reuse the existing single-value judgment**

Find how Pass 1 judges `circle` (the call that sets `res.IsOk`/appends the tolerance verdict via `_judger`). Reuse that exact helper for the arc count. If the existing code judges inline rather than via a helper, extract the smallest helper that both can call:
```csharp
        // 以既有 ToleranceJudger 對單一量測值判定，並回填 IsOk（與 circle/line 相同語意）。
        private void JudgeSingle(ToolRunResult res, MeasurementTool tool, double value)
        {
            if (tool.Tolerance == null) { res.IsOk = null; return; }
            var judgment = _judger.Judge(new List<ToleranceItemInput>
            {
                new ToleranceItemInput
                {
                    ToolId = tool.Id, ToolName = tool.Name,
                    MeasuredValue = value, Spec = tool.Tolerance
                }
            });
            res.IsOk = judgment.Items.Count > 0 ? judgment.Items[0].IsOk : (bool?)null;
        }
```
If an equivalent helper already exists, call it instead of adding this one (DRY — do not duplicate judgment logic).

- [ ] **Step 4: Build + run both test suites**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe"; "EXITCODE=$LASTEXITCODE"
& ".\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe"; "EXITCODE=$LASTEXITCODE"
```
Expected: build 0/0; both `EXITCODE=0`.

- [ ] **Step 5: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/RecipeRunner.cs
git commit -m "feat(arc): run arc caliper tools in RecipeRunner with pose following"
```

---

## Task 5: RecipeEditor arc tool panel + capture

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs`

Mirror the existing rect2 ROI panel (numeric fields + capture button). **Read the existing `_roiGroup` construction, `_captureRoiButton`/`OnCaptureRoi` flow and `SetPropertyPanelEnabled`/panel-visibility logic first, and follow them exactly.**

- [ ] **Step 1: Add the "Add Arc" toolbar button**

Next to the existing `_addCircleButton`/`_addLineButton` creation and their `AddTool("circle")`-style handlers, add:
```csharp
        private Button _addArcButton;
```
```csharp
            _addArcButton = new Button { Text = "Add Arc", AutoSize = true };
            _addArcButton.Click += (s, e) => AddTool("arc");
```
Add it to the same toolbar `FlowLayoutPanel` as the other Add buttons, and give it a tooltip in `SetupToolTips()` matching the existing style:
```csharp
            _toolTip.SetToolTip(_addArcButton, "弧形卡尺：量圓周等分特徵邊數（孔數/齒數/引腳數）");
```
In `AddTool`, ensure a new `"arc"` tool gets a usable default `ArcRoi` (so it is valid immediately and the Validator does not block a fresh tool):
```csharp
            if (toolType == "arc")
                tool.ArcRoi = new ArcMeasureRoi
                {
                    CenterRow = 200, CenterCol = 200, Radius = 100,
                    AngleStart = 0.0, AngleExtent = 2.0 * Math.PI, AnnulusRadius = 5.0
                };
```

- [ ] **Step 2: Build the arc ROI group (6 numeric fields)**

Add fields and a `_arcGroup` built exactly like the existing `_roiGroup` (same `GroupBox` + label/NumericUpDown layout helper the file already uses). Angles are **radians** (matching the existing `_angleRadNumeric` convention — its tooltip says "in radians"):
```csharp
        private GroupBox _arcGroup;
        private NumericUpDown _arcCenterRowNumeric;
        private NumericUpDown _arcCenterColNumeric;
        private NumericUpDown _arcRadiusNumeric;
        private NumericUpDown _arcAngleStartNumeric;
        private NumericUpDown _arcAngleExtentNumeric;
        private NumericUpDown _arcAnnulusNumeric;
        private Button _captureArcButton;
```
Ranges/decimals: centers/radius/annulus `DecimalPlaces = 2`, `Maximum = 100000`; angles `DecimalPlaces = 4`, `Minimum = -7`, `Maximum = 7` (radians, ±2π). Tooltips (same style as the rect2 panel):
```csharp
            _toolTip.SetToolTip(_arcCenterRowNumeric, "弧心 row（像素）");
            _toolTip.SetToolTip(_arcCenterColNumeric, "弧心 column（像素）");
            _toolTip.SetToolTip(_arcRadiusNumeric, "掃描半徑（像素）");
            _toolTip.SetToolTip(_arcAngleStartNumeric, "起始角（弧度）");
            _toolTip.SetToolTip(_arcAngleExtentNumeric, "角度範圍（弧度，2π≈6.2832 為整圈；負值為順時針）");
            _toolTip.SetToolTip(_arcAnnulusNumeric, "環寬一半（像素）");
            _toolTip.SetToolTip(_captureArcButton, "在主影像上拖曳把手調整弧形 ROI");
```
Each numeric's `ValueChanged` writes back to `_selectedTool.ArcRoi` and calls `MarkDirty()` — guarded by the existing `_updatingControls` flag exactly like the rect2 numerics do.

- [ ] **Step 3: Show the right panel per tool type**

Wherever the file decides panel visibility for the selected tool (the same place `_roiGroup`/`_refGroup`/`_gdtGroup` are shown/hidden), add: `_arcGroup` visible **only** when `_selectedTool.ToolType == "arc"`, and `_roiGroup` hidden for arc tools (its rect2 `Roi` is unused there). Load values in the same `_updatingControls`-guarded loader used for the rect2 fields.

- [ ] **Step 4: Wire the capture button to the interactive arc caliper**

Follow the existing `OnCaptureRoi` pattern (which hands over the shared main-window image, sets `_editorOwnsEdit`, and writes the result back). Use the A3 interactive arc editor on the shared helper:
```csharp
        private void OnCaptureArc(object sender, EventArgs e)
        {
            if (_selectedTool == null || _selectedTool.ArcRoi == null) return;
            if (_imageHelper.CurrentImage == null)
            {
                MessageBox.Show(this, "請先在主視窗載入影像。", "Arc ROI"); return;
            }
            ArcMeasureRoi a = _selectedTool.ArcRoi;
            _editorOwnsEdit = true;
            _imageHelper.BeginArcEdit(a.CenterRow, a.CenterCol, a.Radius, a.AngleStart, a.AngleExtent, a.AnnulusRadius,
                (cr, cc, radius, angleStart, angleExtent, annulus) =>
                {
                    if (_selectedTool == null || _selectedTool.ArcRoi == null) return;
                    _selectedTool.ArcRoi.CenterRow = cr;
                    _selectedTool.ArcRoi.CenterCol = cc;
                    _selectedTool.ArcRoi.Radius = radius;
                    _selectedTool.ArcRoi.AngleStart = angleStart;
                    _selectedTool.ArcRoi.AngleExtent = angleExtent;
                    _selectedTool.ArcRoi.AnnulusRadius = annulus;
                    LoadArcFieldsFromSelectedTool();   // _updatingControls-guarded writer
                    MarkDirty();
                });
        }
```
**Check `HWindowControlHelper.BeginArcEdit`'s real signature/callback shape first and adapt** — if it differs, keep this behaviour (drag handles → write back to `ArcRoi` → refresh numerics → MarkDirty) and match the actual API. The existing `FormClosed` handler already calls `EndRect2Edit()` when `_editorOwnsEdit`; add the matching `EndArcEdit()` there so the editor tears down its own arc edit too.

- [ ] **Step 5: Build x64**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
```
Expected: build 0/0.

- [ ] **Step 6: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/RecipeEditor.cs
git commit -m "feat(arc): add arc tool panel + interactive arc ROI capture to RecipeEditor"
```

---

## Task 6: Arc result overlay

**Files:**
- Modify: `src/FlashMeasurementSystem.App.Wpf/MainWindow.cs`

- [ ] **Step 1: Draw arc results in `DrawRecipeResults`**

`OverlayAnnotator.DrawArc(row, col, radius, startPhi, endPhi, pointOrder, color)` already exists. Inside the existing `SetPersistentOverlayAction` lambda in `DrawRecipeResults`, where per-tool results are drawn, add an arc case (colour by verdict, matching the existing OK/NG colour convention):
```csharp
                foreach (ToolRunResult r in results)
                {
                    if (r == null || r.ToolType != "arc" || r.PlacedArc == null) continue;
                    string color = r.IsOk == null ? "yellow" : (r.IsOk.Value ? "green" : "red");
                    ArcMeasureRoi a = r.PlacedArc;
                    an.DrawArc(a.CenterRow, a.CenterCol, a.Radius,
                        a.AngleStart, a.AngleStart + a.AngleExtent,
                        a.AngleExtent >= 0 ? "positive" : "negative", color);
                    // 邊點十字：比照既有 overlay 上限，均勻抽樣避免壅塞
                    const int maxCrosses = 200;
                    int n = r.ArcEdgeRows.Count;
                    int step = n > maxCrosses ? (int)Math.Ceiling(n / (double)maxCrosses) : 1;
                    for (int i = 0; i < n; i += step)
                        an.DrawCross(r.ArcEdgeRows[i], r.ArcEdgeCols[i], 10, color);
                }
```
Place it alongside the existing per-tool drawing so it repaints with every redraw (the persistent overlay slot re-invokes the whole action — do not add a second `SetPersistentOverlayAction`).

- [ ] **Step 2: Build x64 + run both suites**
```powershell
dotnet build .\FlashMeasurementSystem.sln /p:GenerateResourceMSBuildArchitecture=CurrentArchitecture /p:Configuration=Debug /p:Platform=x64
& ".\tests\FlashMeasurementSystem.Tests\bin\x64\Debug\FlashMeasurementSystem.Tests.exe"; "EXITCODE=$LASTEXITCODE"
& ".\tests\FlashMeasurementSystem.Tests.Halcon\bin\x64\Debug\FlashMeasurementSystem.Tests.Halcon.exe"; "EXITCODE=$LASTEXITCODE"
```
Expected: build 0/0; both `EXITCODE=0`.

- [ ] **Step 3: Manual GUI verification (human-driven, spec §7.3)**

Launch `src\FlashMeasurementSystem.App.Wpf\bin\x64\Debug\FlashMeasurementSystem.App.Wpf.exe`:
1. Load an image with a circular array of features (holes/teeth/pins).
2. Edit Recipe → **Add Arc** → **擷取弧形 ROI** → drag the handles so the annulus straddles the features all the way round → numerics update live.
3. Set Tolerance (e.g. Nominal = expected count, Lower −0.5, Upper +0.5). Save the recipe.
4. Reload the recipe → the arc fields survive the round-trip.
5. Run Recipe / 一鍵量測 → 邊數 shows in the result table, PASS/FAIL colours, overlay draws the arc + edge crosses.
6. **Rotate the part and re-run** → the arc follows the matched pose (this is the behaviour Task 3's test guards).

- [ ] **Step 4: Commit**
```bash
git add src/FlashMeasurementSystem.App.Wpf/MainWindow.cs
git commit -m "feat(arc): draw arc caliper results (arc + edge crosses) on the overlay"
```

---

## Self-Review

**Spec coverage:**
- §4.1 schema v7 + ArcRoi reusing ArcMeasureRoi → Task 1 Steps 5-6. ✓
- §4.2 `DetectEdgesOnArc` on IEdgeDetector + all implementers updated → Task 2 (both implementers identified: HalconEdgeDetector already compliant; FakeEdgeDetector fixed). ✓
- §4.3 pose transform reusing TransformRoi, radius/extent/annulus unchanged → Task 3 (`ArcRoiTransform`), used by Task 4. ✓
- §4.4 Runner Pass 1 arc branch, count → existing ToleranceJudger, failure handling → Task 4. ✓
- §4.5 editor Add Arc + 6 radian numerics + capture reusing A3 handles → Task 5. ✓
- §4.6 Validator arc rule → Task 1 Step 7. ✓
- §4.7 overlay via existing DrawArc + capped crosses; report via existing pipeline (no code) → Task 6. ✓
- §7.1 Domain tests (defaults, round-trip, backward compat, validator) → Task 1 Step 2. ✓
- §7.2 arc alignment test **+ prove it bites** → Task 3 Steps 1, 6. ✓
- §7.3 GUI acceptance incl. rotated part → Task 6 Step 3. ✓
- §5 limits (Roi rect2 unused on arc tools) → documented in Task 1 Step 5's comment. ✓

**Placeholder scan:** No TBD/TODO. Three steps intentionally say "read the existing pattern first and match it" (Task 4 Step 3's judgment helper, Task 5 Steps 2-4's panel/capture wiring, Task 6 Step 1's placement) — these are *reuse* instructions against code the implementer must not duplicate, and each still ships concrete code plus the exact behaviour required. `BeginArcEdit`'s signature must be confirmed against the real helper (flagged in Task 5 Step 4).

**Type consistency:** `ArcMeasureRoi` (CenterRow/CenterCol/Radius/AngleStart/AngleExtent/AnnulusRadius), `MeasurementTool.ArcRoi`, `ArcRoiTransform.TransformArc(mapper, arc, transform)`, `ToolRunResult.PlacedArc`/`ArcEdgeRows`/`ArcEdgeCols`, `ToolType == "arc"` are used identically across Tasks 1-6. ✓

**Known risk for the executor:** Task 5 is the largest and touches an existing large file (`RecipeEditor.cs`); its panel/visibility/capture wiring must follow the file's established patterns rather than inventing new ones. If `BeginArcEdit`'s callback shape differs from the sketch, keep the specified behaviour and adapt to the real API.
