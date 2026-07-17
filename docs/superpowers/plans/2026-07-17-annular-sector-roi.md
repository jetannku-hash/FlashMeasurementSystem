# Annular-Sector ROI (扇環 ROI) — Implementation Plan

**Goal:** Let the user drag-draw an annular-sector ROI (center, inner/outer radius, start/extent angle) on the Edge Detection tab — a single drag from center creates it, five handles refine it — and feed its region into `edges_sub_pix` so the existing Fit Line/Circle/Ellipse/Rectangle fitters work on edges inside the sector.

**Draw gesture (user-chosen):** MouseDown = center; drag outward sets outer radius + orientation (live preview); release creates an `ArcMeasureRoi` with default annulus (~20% of radius, min ~8px) and default extent (90°, centered on drag direction); then the existing 5-handle `ArcEditMath` editing refines it.

**Reuse (from investigation):**
- `ArcMeasureRoi` (Domain.EdgeDetection: CenterRow/Col, Radius, AnnulusRadius, AngleStart, AngleExtent) — the exact 6 fields of an annular sector. No new model.
- `ArcEditMath` + `BeginArcEdit`/`EndArcEdit` (HWindowControlHelper) — 5-handle editing already works.
- `HalconEdgeDetector.DetectEdgesSubPix` — already HRegion→`ReduceDomain`→`EdgesSubPix`→EdgePoints; only the region build is rectangle-specific.
- `HalconHoleDetector` — already does `gen_circle`(outer/inner)→`Difference`→`ReduceDomain`; sector = swap `GenCircle`→`GenCircleSector`.
- The 4 fitters take an `EdgePoint` list, ROI-shape-agnostic — **no fitter change**.

**HALCON:** `gen_circle_sector(:CircleSector:Row,Column,Radius,StartAngle,EndAngle:)` — angles in radians, positive (CCW) direction, StartAngle≤EndAngle in [0,2π] (ref L133655). `ArcMeasureRoi.AngleExtent` may be negative → **normalize** before calling (see Task 1). Verify handedness with a synthetic image, NOT by analogy (project has a prior arc-scan handedness bug).

**Tech:** .NET 4.8, WinForms, HALCON 17.12, old-style csproj (new files need `<Compile Include>`), x64 for HALCON. Branch: `feature/annular-sector-roi` off the pcd branch tip (so it carries the display fixes it also touches).

---

## Task 1 — Adapter: `DetectEdgesInAnnularSector` (the handedness-critical, verifiable core)
**Files:** `Application/EdgeDetection/IEdgeDetector.cs`, `Halcon/EdgeDetection/HalconEdgeDetector.cs`.
- Add interface method `EdgeDetectionResult DetectEdgesInAnnularSector(TImage image, ArcMeasureRoi roi, EdgeDetectionParameters parameters)` (mirror the existing `DetectEdgesOnArc` addition).
- Implement in `HalconHoleDetector`-style: normalize angles → `GenCircleSector(outer, cr,cc, R+annulus, aStart,aEnd)` , `GenCircleSector(inner, cr,cc, max(R-annulus,0), aStart,aEnd)`, `Difference(outer,inner,ring)`, `ReduceDomain(gray, ring, reduced)`, then the SAME `EdgesSubPix(...)`+contour→`EdgePoint` extraction as `DetectEdgesSubPix`. Reuse its single-channel + error/dispose idiom.
- **Angle normalization:** convert `(AngleStart, AngleExtent)` → `(startCcw, endCcw)` with start≤end, both in [0,2π] (handle negative extent by swapping; wrap). Full ring when |extent|≥2π-ε.
- Degenerate guards (fail-closed result, no throw): annulus≥radius, |extent|≈0, radius≤0.
- **Verify (throwaway, uncommitted):** render a synthetic image with a distinct edge (e.g. a bright disk / a straight bar) crossing a known angular wedge; run the adapter with a sector covering that wedge and a sector covering the COMPLEMENTARY wedge; confirm edges are returned only for the wedge that actually contains the edge (handedness correct), and the edge count/location is sane. Report actual numbers.

## Task 2 — Sector overlay (so the drawn/analyzed ROI is visible correctly)
**Files:** `App.Wpf/OverlayAnnotator.cs` (+ callers).
- Current `DrawArcBand` draws only 3 arcs (inner/mid/outer), no radial edges. Add a `DrawSectorRoi(cr,cc,radius,annulus,angleStart,angleExtent,color)` (or extend DrawArcBand) that also draws the TWO radial edges (start & end angle) connecting inner↔outer arc, so the drawn wedge matches the `reduce_domain` region. Used by the draw preview (Task 3) and any result overlay.
- Keep the existing 5 edit handles (`DrawEditArc`) working.

## Task 3 — Draw-from-center gesture + Edge Detection tab wiring
**Files:** `App.Wpf/HWindowControlHelper.cs`, `App.Wpf/MainWindow.cs`.
- HWindowControlHelper: add a `RequestSector`-style one-shot mode (parallel to `RequestRoi`): MouseDown records center; MouseMove updates outer radius = dist(center,cursor) and orientation = atan2, live-previewing via `DrawSectorRoi`; MouseUp (if drag > ~5px) builds an `ArcMeasureRoi` with annulus = clamp(0.2*R, ≥8px) and extent = 90° centered on the drag direction, fires a callback, then transitions into `BeginArcEdit` for handle refinement.
- MainWindow Edge Detection tab: a button "扇形 ROI（拖曳繪製）" that arms `RequestSector`; on release, populate the existing Arc numeric boxes (Ctr Row/Col, Radius, Annulus, Start/Ext deg) from the new ArcMeasureRoi and check 互動編輯. Reuse the existing arc numeric<->edit two-way sync.

## Task 4 — Detect (sector area) → Fit  [REUSE existing "Detect" button — user decision]
**Files:** `App.Wpf/MainWindow.cs`.
- Do NOT add a new button. Track an "active ROI kind" (rectangle vs sector): drawing a sector (Task 3) sets it to sector; drawing a rectangle ROI sets it back to rectangle. The EXISTING "Detect" (`RunEdgeDetectionButton_Click`) branches: if active kind == sector AND the sector ArcMeasureRoi is valid → `_edgeDetector.DetectEdgesInAnnularSector(image, sectorRoi, edgeParams)`; else → the existing rectangle path unchanged. Result goes into the SAME `_latestEdgeResult.EdgePoints`, so the existing Fit Line/Circle/Ellipse/Rectangle buttons consume them unchanged. Default MUST remain the rectangle path so existing behavior is untouched when no sector is active. (The existing tangential "Detect Arc" button is left as-is.)
- Overlay: draw the sector (Task 2) + the detected edge crosses.

## Demo (after all tasks)
On a synthetic image: click "扇形 ROI（拖曳繪製）" → drag a wedge over a circular edge → refine handles → "Detect（扇形區域）" → "Fit Circle" (and "Fit Line" on a straight-edge wedge) → show the fit result + edge crosses confined to the sector.

## Lessons applied
- New interface method mirrors `DetectEdgesOnArc` (Application over Domain types; HALCON only in adapter).
- gen_circle_sector angle normalization + **synthetic-image handedness verification** (not analogy).
- Fail-closed degenerate guards (annulus≥radius etc.), like `HalconHoleDetector`.
- No new ROI model (reuse `ArcMeasureRoi`); no fitter changes.
- Each UI task GUI-verified (no unit test for WinForms); Task 1 verified by synthetic run.
