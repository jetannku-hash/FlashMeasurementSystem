using System;
using System.Collections.Generic;
using System.Globalization;
using FlashMeasurementSystem.Application.AngleMeasurement;
using FlashMeasurementSystem.Application.CircleFitting;
using FlashMeasurementSystem.Application.CoordinateSystem;
using FlashMeasurementSystem.Application.DistanceMeasurement;
using FlashMeasurementSystem.Application.EdgeDetection;
using FlashMeasurementSystem.Application.LineFitting;
using FlashMeasurementSystem.Application.Tolerance;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Domain.DistanceMeasurement;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.LineFitting;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.Geometry;
using FlashMeasurementSystem.Domain.Tolerance;
using HalconDotNet;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// 轉換後的工具 ROI（供繪製）。
    /// </summary>
    public sealed class PlacedRoi
    {
        public double Row, Col, AngleRad, Length1, Length2;
        public string Name;
    }

    /// <summary>
    /// 單一工具的執行結果（轉換後 ROI + 量測 + 判定，供 MainWindow 繪製與顯示）。
    /// </summary>
    public sealed class ToolRunResult
    {
        public string Name;
        public string ToolType;
        public PlacedRoi Roi;          // 轉換後 ROI（一定有，供繪框）
        public bool Supported;         // 此版是否支援該工具型別
        public bool Measured;          // 量測是否成功
        public double DiameterMm;      // circle：直徑 (mm)
        public double FitCenterRow, FitCenterCol, FitRadiusPx;  // 擬合圓（供繪製）
        public double LineRow1, LineCol1, LineRow2, LineCol2, LineAngleDeg;  // line 元素（供繪製/複合工具引用）
        public double DistMm;          // distance：距離 (mm)
        public double DistRow1, DistCol1, DistRow2, DistCol2;  // distance 連線兩端（供繪製）
        public double AngleDeg;         // angle：夾角 AcuteAngleDeg (0~90)
        public double AngleCenterRow, AngleCenterCol;   // angle 兩線交點（供畫弧）
        public double AngleRadiusPx;    // angle 弧線半徑（供畫弧）
        public double AngleStartRad;    // angle 弧起點方位角（供畫弧）
        public bool? IsOk;             // 公差判定；null = 無判定
        public string ValueText;       // 結果表顯示文字
        public string Message;         // 失敗/說明
        public GeometricPrimitive OutputPrimitive;  // A5：此工具的幾何輸出（resolver / 下游消費）
    }

    /// <summary>
    /// 配方執行引擎（M3c-1 B1）。把每個工具 ROI 由參考座標系轉到當前匹配姿態，
    /// 對 circle 工具量測直徑(mm) 並做公差判定。其他型別此版標示未支援、不擋流程。
    /// 不直接相依具體 HALCON 類別——以 Application 介面注入(可測/可換)。
    /// </summary>
    public sealed class RecipeRunner
    {
        private readonly IEdgeDetector<HImage> _edgeDetector;
        private readonly ICircleFitter _circleFitter;
        private readonly ILineFitter _lineFitter;
        private readonly IDistanceMeasurer<HXLDCont> _distanceMeasurer;
        private readonly IAngleMeasurer _angleMeasurer;
        private readonly IToleranceJudger _judger;
        private readonly ICoordinateMapper _mapper;

        public RecipeRunner(IEdgeDetector<HImage> edgeDetector, ICircleFitter circleFitter,
            ILineFitter lineFitter, IDistanceMeasurer<HXLDCont> distanceMeasurer,
            IAngleMeasurer angleMeasurer, IToleranceJudger judger, ICoordinateMapper mapper)
        {
            _edgeDetector = edgeDetector;
            _circleFitter = circleFitter;
            _lineFitter = lineFitter;
            _distanceMeasurer = distanceMeasurer;
            _angleMeasurer = angleMeasurer;
            _judger = judger;
            _mapper = mapper;
        }

        public List<ToolRunResult> Run(Recipe recipe, HImage image,
            bool hasMatch, double matchRow, double matchCol, double matchAngleRad,
            double pixelSizeUmX, double pixelSizeUmY)
        {
            var results = new List<ToolRunResult>();
            if (recipe == null || recipe.Tools == null) return results;

            RigidTransform transform = null;
            if (recipe.HasReferencePose && hasMatch)
            {
                transform = _mapper.CreateFromMatch(
                    recipe.RefRow, recipe.RefCol, recipe.RefAngleRad,
                    matchRow, matchCol, matchAngleRad);
            }

            // 直徑非軸向，用 X/Y 平均（等向校正時 X=Y）。
            double pixelSizeUm = (pixelSizeUmX + pixelSizeUmY) / 2.0;

            // 兩階段：先量元素（line/circle）並以 Id 索引，再算複合工具（distance）引用它們。
            var byId = new Dictionary<string, ToolRunResult>();

            // ── Pass 1：元素工具（line / circle）──
            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null || tool.Roi == null) continue;
                if (tool.ToolType != "circle" && tool.ToolType != "line") continue;

                RoiGeometry g = tool.Roi;
                double row = g.CenterRow, col = g.CenterCol, ang = g.AngleRad;
                if (transform != null)
                {
                    TransformedRoi tr = _mapper.TransformRoi(g.CenterRow, g.CenterCol, g.AngleRad, transform);
                    row = tr.Row; col = tr.Col; ang = tr.AngleRad;
                }

                var res = new ToolRunResult
                {
                    Name = tool.Name,
                    ToolType = tool.ToolType,
                    Roi = new PlacedRoi
                    {
                        Row = row, Col = col, AngleRad = ang,
                        Length1 = g.Length1, Length2 = g.Length2, Name = tool.Name
                    }
                };

                if (tool.ToolType == "circle")
                {
                    MeasureCircle(image, res, tool, row, col, ang, g.Length1, g.Length2, pixelSizeUm);
                }
                else
                {
                    MeasureLine(image, res, tool, row, col, ang, g.Length1, g.Length2);
                }

                if (res.Measured && tool.ToolType == "circle")
                    res.OutputPrimitive = GeometricPrimitive.Circle(res.FitCenterRow, res.FitCenterCol, res.FitRadiusPx);
                else if (res.Measured && tool.ToolType == "line")
                    res.OutputPrimitive = GeometricPrimitive.Line(res.LineRow1, res.LineCol1, res.LineRow2, res.LineCol2);

                results.Add(res);
                if (!string.IsNullOrEmpty(tool.Id)) byId[tool.Id] = res;
            }

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

            // ── Pass 2：複合工具（distance）與其他 ──
            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null) continue;
                if (tool.ToolType == "circle" || tool.ToolType == "line") continue;  // 已於 Pass 1 處理
                if (tool.ToolType == "intersection" || tool.ToolType == "midline" || tool.ToolType == "projection") continue;  // 已於 Pass 1.5 處理

                var res = new ToolRunResult { Name = tool.Name, ToolType = tool.ToolType };

                if (tool.ToolType == "distance")
                {
                    MeasureDistance(res, tool, byId, pixelSizeUmX, pixelSizeUmY);
                }
                else if (tool.ToolType == "angle")
                {
                    MeasureAngle(res, tool, byId);
                }
                else
                {
                    res.Supported = false;
                    res.IsOk = null;
                    res.ValueText = "(未支援)";
                    res.Message = "工具型別 '" + tool.ToolType + "' 尚未支援";
                }

                results.Add(res);
            }

            return results;
        }

        private void MeasureCircle(HImage image, ToolRunResult res, MeasurementTool tool,
            double row, double col, double ang, double length1, double length2, double pixelSizeUm)
        {
            res.Supported = true;
            try
            {
                EdgeDetectionRoi edgeRoi = EdgeDetectionRoi.FromCenter(row, col, length1, length2, ang);
                EdgeDetectionParameters edgeParams = tool.EdgeParameters ?? EdgeDetectionParameters.Default();

                EdgeResult edges = _edgeDetector.DetectEdgesSubPix(image, edgeRoi, edgeParams);
                if (!edges.Success || edges.EdgePoints == null || edges.EdgePoints.Count < 3)
                {
                    res.Measured = false;
                    res.ValueText = "邊緣不足";
                    res.Message = edges.ErrorMessage;
                    return;
                }

                CircleFittingResult circle = _circleFitter.FitCircle(edges.EdgePoints, CircleFittingParameters.Default());
                if (!circle.Success)
                {
                    res.Measured = false;
                    res.ValueText = "擬合失敗";
                    res.Message = circle.ErrorMessage;
                    return;
                }

                res.Measured = true;
                res.FitCenterRow = circle.CenterRow;
                res.FitCenterCol = circle.CenterColumn;
                res.FitRadiusPx = circle.RadiusPx;
                res.DiameterMm = circle.DiameterPx * pixelSizeUm / 1000.0;
                res.ValueText = string.Format(CultureInfo.InvariantCulture, "D={0:F4}mm", res.DiameterMm);

                if (tool.Tolerance != null)
                {
                    var input = new ToleranceItemInput
                    {
                        ToolId = tool.Id,
                        ToolName = tool.Name,
                        MeasuredValue = res.DiameterMm,
                        Spec = tool.Tolerance
                    };
                    OverallJudgment overall = _judger.Judge(new List<ToleranceItemInput> { input });
                    if (overall.Items.Count > 0)
                    {
                        res.IsOk = overall.Items[0].IsOk;
                    }
                }
            }
            catch (HalconException ex)
            {
                res.Measured = false;
                res.ValueText = "量測異常";
                res.Message = ex.Message;
            }
        }

        // line 元素（B2a）：ROI → subpix 邊緣 → 擬合線。元素本身不判定（IsOk=null），
        // 擬合線供繪製與後續複合工具（distance/angle）引用。
        private void MeasureLine(HImage image, ToolRunResult res, MeasurementTool tool,
            double row, double col, double ang, double length1, double length2)
        {
            res.Supported = true;
            try
            {
                EdgeDetectionRoi edgeRoi = EdgeDetectionRoi.FromCenter(row, col, length1, length2, ang);
                EdgeDetectionParameters edgeParams = tool.EdgeParameters ?? EdgeDetectionParameters.Default();

                EdgeResult edges = _edgeDetector.DetectEdgesSubPix(image, edgeRoi, edgeParams);
                if (!edges.Success || edges.EdgePoints == null || edges.EdgePoints.Count < 2)
                {
                    res.Measured = false;
                    res.ValueText = "邊緣不足";
                    res.Message = edges.ErrorMessage;
                    return;
                }

                LineFittingResult line = _lineFitter.FitLine(edges.EdgePoints, LineFittingParameters.Default());
                if (!line.Success)
                {
                    res.Measured = false;
                    res.ValueText = "擬合失敗";
                    res.Message = line.ErrorMessage;
                    return;
                }

                res.Measured = true;
                res.LineRow1 = line.Row1; res.LineCol1 = line.Column1;
                res.LineRow2 = line.Row2; res.LineCol2 = line.Column2;
                res.LineAngleDeg = line.AngleDeg;

                // C2：若 line 公差單位為 deg，對線角度做環狀公差判定。
                //     否則維持純元素模式（IsOk = null）。
                if (tool.Tolerance != null && tool.Tolerance.Unit == "deg")
                {
                    double norm = AngleNormalizer.ToHalfCircle(line.AngleDeg);
                    double aligned = tool.Tolerance.Nominal
                        + AngleNormalizer.CircularSignedDiffDeg(norm, tool.Tolerance.Nominal);
                    var input = new ToleranceItemInput
                    {
                        ToolId = tool.Id,
                        ToolName = tool.Name,
                        MeasuredValue = aligned,
                        Spec = tool.Tolerance
                    };
                    OverallJudgment overall = _judger.Judge(new List<ToleranceItemInput> { input });
                    if (overall.Items.Count > 0) res.IsOk = overall.Items[0].IsOk;
                    res.ValueText = string.Format(CultureInfo.InvariantCulture, "{0:F2} deg", aligned);
                }
                else
                {
                    res.IsOk = null;  // 元素不判定
                    res.ValueText = string.Format(CultureInfo.InvariantCulture, "Ang={0:F2}deg", line.AngleDeg);
                }
            }
            catch (HalconException ex)
            {
                res.Measured = false;
                res.ValueText = "量測異常";
                res.Message = ex.Message;
            }
        }

        // distance（B2b）：參考兩個 line 元素，用 distance_ss 最小距（M1 MeasureLineToLine，
        // 內部已換 mm）算垂直間距 → 判定。僅支援 line↔line。
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

        private void MeasureDistance(ToolRunResult res, MeasurementTool tool,
            Dictionary<string, ToolRunResult> byId, double pixelSizeUmX, double pixelSizeUmY)
        {
            res.Supported = true;

            if (tool.RefToolIds == null || tool.RefToolIds.Count < 2)
            {
                res.Measured = false;
                res.ValueText = "需 2 參考元素";
                res.Message = "distance 需 RefToolIds 含 2 個元素";
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
            try
            {
                var dp = new DistanceMeasurementParameters
                {
                    PixelSizeUmX = pixelSizeUmX,
                    PixelSizeUmY = pixelSizeUmY
                };

                if (a.ToolType == "line" && b.ToolType == "line")
                {
                    DistanceMeasurementResult dr = _distanceMeasurer.MeasureLineToLine(
                        a.LineRow1, a.LineCol1, a.LineRow2, a.LineCol2,
                        b.LineRow1, b.LineCol1, b.LineRow2, b.LineCol2, dp);

                    if (!dr.Success)
                    {
                        res.Measured = false;
                        res.ValueText = "距離計算失敗";
                        res.Message = dr.ErrorMessage;
                        return;
                    }

                    res.Measured = true;
                    res.DistMm = dr.DistanceMm;
                    // 視覺化：量到的是兩線「垂直最近距離」(distance_ss min)。用「line A 中點 →
                    // 該點在 line B 上的垂足」畫線段，使其與邊垂直、長度等於間距（避免中點對中點
                    // 因兩 ROI 水平偏移而畫成斜線，誤導使用者）。
                    double aMidRow = (a.LineRow1 + a.LineRow2) / 2.0;
                    double aMidCol = (a.LineCol1 + a.LineCol2) / 2.0;
                    ProjectPointOntoLine(aMidRow, aMidCol,
                        b.LineRow1, b.LineCol1, b.LineRow2, b.LineCol2,
                        out double footRow, out double footCol);
                    res.DistRow1 = aMidRow;
                    res.DistCol1 = aMidCol;
                    res.DistRow2 = footRow;
                    res.DistCol2 = footCol;
                    res.ValueText = string.Format(CultureInfo.InvariantCulture, "D={0:F4}mm", res.DistMm);
                }
                else if (a.ToolType == "circle" && b.ToolType == "circle")
                {
                    // A1: 圓心對圓心距離
                    DistanceMeasurementResult dr = _distanceMeasurer.MeasureCircleToCircle(
                        a.FitCenterRow, a.FitCenterCol,
                        b.FitCenterRow, b.FitCenterCol, dp);

                    if (!dr.Success)
                    {
                        res.Measured = false;
                        res.ValueText = "距離計算失敗";
                        res.Message = dr.ErrorMessage;
                        return;
                    }

                    res.Measured = true;
                    res.DistMm = dr.DistanceMm;
                    res.DistRow1 = a.FitCenterRow;
                    res.DistCol1 = a.FitCenterCol;
                    res.DistRow2 = b.FitCenterRow;
                    res.DistCol2 = b.FitCenterCol;
                    res.ValueText = string.Format(CultureInfo.InvariantCulture, "D={0:F4}mm", res.DistMm);
                }
                else if ((a.ToolType == "line" && b.ToolType == "circle") ||
                         (a.ToolType == "circle" && b.ToolType == "line"))
                {
                    // A1: 圓心到線的垂直距離
                    ToolRunResult lineElem = a.ToolType == "line" ? a : b;
                    ToolRunResult circleElem = a.ToolType == "circle" ? a : b;

                    DistanceMeasurementResult dr = _distanceMeasurer.MeasurePointToLine(
                        circleElem.FitCenterRow, circleElem.FitCenterCol,
                        lineElem.LineRow1, lineElem.LineCol1,
                        lineElem.LineRow2, lineElem.LineCol2, dp);

                    if (!dr.Success)
                    {
                        res.Measured = false;
                        res.ValueText = "距離計算失敗";
                        res.Message = dr.ErrorMessage;
                        return;
                    }

                    res.Measured = true;
                    res.DistMm = dr.DistanceMm;
                    res.DistRow1 = circleElem.FitCenterRow;
                    res.DistCol1 = circleElem.FitCenterCol;
                    ProjectPointOntoLine(circleElem.FitCenterRow, circleElem.FitCenterCol,
                        lineElem.LineRow1, lineElem.LineCol1,
                        lineElem.LineRow2, lineElem.LineCol2,
                        out double footRow, out double footCol);
                    res.DistRow2 = footRow;
                    res.DistCol2 = footCol;
                    res.ValueText = string.Format(CultureInfo.InvariantCulture, "D={0:F4}mm", res.DistMm);
                }
                else
                {
                    res.Measured = false;
                    res.ValueText = "不支援的距離組合";
                    res.Message = "距離僅支援 line↔line、circle↔circle、line↔circle";
                    return;
                }

                if (tool.Tolerance != null)
                {
                    var input = new ToleranceItemInput
                    {
                        ToolId = tool.Id,
                        ToolName = tool.Name,
                        MeasuredValue = res.DistMm,
                        Spec = tool.Tolerance
                    };
                    OverallJudgment overall = _judger.Judge(new List<ToleranceItemInput> { input });
                    if (overall.Items.Count > 0) res.IsOk = overall.Items[0].IsOk;
                }
            }
            catch (HalconException ex)
            {
                res.Measured = false;
                res.ValueText = "距離計算異常";
                res.Message = ex.Message;
            }
        }

        // 把點 (pRow,pCol) 垂直投影到通過 (r1,c1)-(r2,c2) 的無限長直線，回傳垂足。
        // 線段退化（長度 0）時回傳該端點，避免除以 0。
        private static void ProjectPointOntoLine(double pRow, double pCol,
            double r1, double c1, double r2, double c2,
            out double footRow, out double footCol)
        {
            double dRow = r2 - r1;
            double dCol = c2 - c1;
            double lenSq = dRow * dRow + dCol * dCol;
            if (lenSq < 1e-9)
            {
                footRow = r1;
                footCol = c1;
                return;
            }
            double t = ((pRow - r1) * dRow + (pCol - c1) * dCol) / lenSq;
            footRow = r1 + t * dRow;
            footCol = c1 + t * dCol;
        }

        // angle（B2c）：參考兩個 line 元素，用 angle_ll 算夾角的 AcuteAngleDeg（0~90）。
        // 旋轉不變量：工件轉 30°，兩線各多 30°，夾角仍同。
        private void MeasureAngle(ToolRunResult res, MeasurementTool tool,
            Dictionary<string, ToolRunResult> byId)
        {
            res.Supported = true;

            if (tool.RefToolIds == null || tool.RefToolIds.Count < 2)
            {
                res.Measured = false;
                res.ValueText = "需 2 參考元素";
                res.Message = "angle 需 RefToolIds 含 2 個 line 元素";
                return;
            }

            ToolRunResult a, b;
            if (!byId.TryGetValue(tool.RefToolIds[0], out a) || !byId.TryGetValue(tool.RefToolIds[1], out b))
            {
                res.Measured = false;
                res.ValueText = "找不到參考元素";
                return;
            }
            if (!a.Measured || !b.Measured)
            {
                res.Measured = false;
                res.ValueText = "參考元素未量測";
                return;
            }
            if (a.ToolType != "line" || b.ToolType != "line")
            {
                res.Measured = false;
                res.ValueText = "僅支援 line↔line";
                res.Message = "B2c 角度僅支援兩 line 元素";
                return;
            }

            try
            {
                var ap = AngleMeasurementParameters.Default();
                AngleMeasurementResult ar = _angleMeasurer.MeasureAngle(
                    a.LineRow1, a.LineCol1, a.LineRow2, a.LineCol2,
                    b.LineRow1, b.LineCol1, b.LineRow2, b.LineCol2, ap);

                if (!ar.Success)
                {
                    res.Measured = false;
                    res.ValueText = "角度計算失敗";
                    res.Message = ar.ErrorMessage;
                    return;
                }

                res.Measured = true;
                res.AngleDeg = ar.AcuteAngleDeg;
                res.ValueText = string.Format(CultureInfo.InvariantCulture, "{0:F2}°", res.AngleDeg);

                // 兩線交點：取 a1→a2 與 b1→b2 的交點（四點重心當近似頂點，對近交點/平行線也安全）。
                res.AngleCenterRow = (a.LineRow1 + a.LineRow2 + b.LineRow1 + b.LineRow2) / 4.0;
                res.AngleCenterCol = (a.LineCol1 + a.LineCol2 + b.LineCol1 + b.LineCol2) / 4.0;
                res.AngleRadiusPx = 80.0;  // 弧線半徑（像素）
                // 弧起點：線 A 的第一端點指向交點（頂點）的方位角。
                res.AngleStartRad = Math.Atan2(a.LineRow1 - res.AngleCenterRow, a.LineCol1 - res.AngleCenterCol);

                if (tool.Tolerance != null)
                {
                    var input = new ToleranceItemInput
                    {
                        ToolId = tool.Id,
                        ToolName = tool.Name,
                        MeasuredValue = res.AngleDeg,
                        Spec = tool.Tolerance
                    };
                    OverallJudgment overall = _judger.Judge(new List<ToleranceItemInput> { input });
                    if (overall.Items.Count > 0) res.IsOk = overall.Items[0].IsOk;
                }
            }
            catch (HalconException ex)
            {
                res.Measured = false;
                res.ValueText = "角度計算異常";
                res.Message = ex.Message;
            }
        }
    }
}
