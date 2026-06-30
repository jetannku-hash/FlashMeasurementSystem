using System;
using System.Collections.Generic;
using System.Globalization;
using FlashMeasurementSystem.Application.AngleMeasurement;
using FlashMeasurementSystem.Application.CircleFitting;
using FlashMeasurementSystem.Application.CoordinateSystem;
using FlashMeasurementSystem.Application.DistanceMeasurement;
using FlashMeasurementSystem.Application.EdgeDetection;
using FlashMeasurementSystem.Application.LineFitting;
using FlashMeasurementSystem.Application.MetrologyModel;
using FlashMeasurementSystem.Application.Tolerance;
using FlashMeasurementSystem.Domain.AngleMeasurement;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Domain.DistanceMeasurement;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.LineFitting;
using FlashMeasurementSystem.Domain.MetrologyModel;
using FlashMeasurementSystem.Domain.Roi;
using FlashMeasurementSystem.Domain.Geometry;
using FlashMeasurementSystem.Domain.Gdt;
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
        // 2D metrology 擬合幾何：circle 用 FitCenter*/FitRadiusPx、line 用 LineRow1..LineCol2、
        // ellipse 用 FitCenter*/FitPhi/FitRadius1/FitRadius2、rectangle 用 FitCenter*/FitPhi/FitLength1/FitLength2。
        public double FitPhi, FitRadius1, FitRadius2, FitLength1, FitLength2;
        // metrology 量測區邊點（供顯示 cyan 十字）
        public List<double> MetrologyMeasureRows = new List<double>();
        public List<double> MetrologyMeasureCols = new List<double>();
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
        public double ResidualRmsPx;     // line/circle 擬合殘差 RMS（px）；供真直度（近似）與品質
        public double CircleRoundnessPx; // circle max-min 徑向（px）；供真圓度真值
        public double GdtDeviationMm;    // GD&T：形位偏差（mm）
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
        // v6：2D 量測模型（加性、可為 null）。null 時 Pass 3 整段跳過，既有建構點/單元測試不受影響。
        private readonly IMetrologyModelRunner<HImage> _metrologyRunner;

        public RecipeRunner(IEdgeDetector<HImage> edgeDetector, ICircleFitter circleFitter,
            ILineFitter lineFitter, IDistanceMeasurer<HXLDCont> distanceMeasurer,
            IAngleMeasurer angleMeasurer, IToleranceJudger judger, ICoordinateMapper mapper,
            IMetrologyModelRunner<HImage> metrologyRunner = null)
        {
            _edgeDetector = edgeDetector;
            _circleFitter = circleFitter;
            _lineFitter = lineFitter;
            _distanceMeasurer = distanceMeasurer;
            _angleMeasurer = angleMeasurer;
            _judger = judger;
            _mapper = mapper;
            _metrologyRunner = metrologyRunner;
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

            // ── Pass 1.7：GD&T 形位公差工具（單邊判定，0 ≤ 偏差 ≤ T）──
            // 僅參照 line/circle 基礎元件（不支援鏈式參照構造結果）。
            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null) continue;
                if (!IsGdtType(tool.ToolType)) continue;

                var res = new ToolRunResult { Name = tool.Name, ToolType = tool.ToolType };
                MeasureGdt(res, tool, byId, pixelSizeUm);
                results.Add(res);
                if (!string.IsNullOrEmpty(tool.Id)) byId[tool.Id] = res;
            }

            // ── Pass 2：複合工具（distance）與其他 ──
            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null) continue;
                if (tool.ToolType == "circle" || tool.ToolType == "line") continue;  // 已於 Pass 1 處理
                if (tool.ToolType == "intersection" || tool.ToolType == "midline" || tool.ToolType == "projection") continue;  // 已於 Pass 1.5 處理
                if (IsGdtType(tool.ToolType)) continue;  // 已於 Pass 1.7 處理

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

            // ── Pass 3：2D 量測模型（MET2D；加性，在 1D passes 之後，與其並存）──
            // _metrologyRunner==null 或無 MetrologyModel → 整段跳過，1D 結果一字不差。
            if (_metrologyRunner != null
                && recipe.MetrologyModel != null
                && recipe.MetrologyModel.Objects != null
                && recipe.MetrologyModel.Objects.Count > 0)
            {
                // reference_system 與 align 必須成對：唯有真的要對齊（有參考姿態且有匹配）時才設
                // reference_system，否則標稱幾何視為「絕對影像座標」直接量測。傳 hasAlign 給
                // hasReferencePose 引數，避免「有參考姿態但未匹配」時 reference_system 把幾何位移錯位。
                bool hasAlign = recipe.HasReferencePose && hasMatch;
                MetrologyModelResult mResult = _metrologyRunner.Apply(
                    recipe.MetrologyModel,
                    recipe.RefRow, recipe.RefCol, recipe.RefAngleRad, hasAlign,
                    image,
                    hasAlign ? matchRow : 0.0, hasAlign ? matchCol : 0.0, hasAlign ? matchAngleRad : 0.0,
                    hasAlign);

                if (mResult != null && mResult.Objects != null)
                {
                    foreach (MetrologyObjectResult o in mResult.Objects)
                    {
                        ToolRunResult res = MapToToolRunResult(o);
                        results.Add(res);
                        if (!string.IsNullOrEmpty(o.Id)) byId[o.Id] = res;
                    }
                }
            }

            return results;
        }

        // 把 metrology 物件結果轉成 ToolRunResult，ToolType 用 "metrology_*"（與 1D 型別區隔，
        // 任何 pass 都不會重複處理）。純像素，不做 mm 轉換。
        private static ToolRunResult MapToToolRunResult(MetrologyObjectResult o)
        {
            var res = new ToolRunResult
            {
                Name = o.Name,
                ToolType = "metrology_" + o.Shape.ToString().ToLowerInvariant(),
                Supported = true,
                Measured = o.Success,
                IsOk = o.IsOk,
                ValueText = o.ValueText,
                Message = o.ErrorMessage
            };
            switch (o.Shape)
            {
                case MetrologyObjectType.Circle:
                    res.FitCenterRow = o.FitRow; res.FitCenterCol = o.FitColumn; res.FitRadiusPx = o.FitRadius;
                    break;
                case MetrologyObjectType.Line:
                    res.LineRow1 = o.FitRowBegin; res.LineCol1 = o.FitColumnBegin;
                    res.LineRow2 = o.FitRowEnd; res.LineCol2 = o.FitColumnEnd;
                    break;
                case MetrologyObjectType.Ellipse:
                    res.FitCenterRow = o.FitRow; res.FitCenterCol = o.FitColumn;
                    res.FitPhi = o.FitPhi; res.FitRadius1 = o.FitRadius1; res.FitRadius2 = o.FitRadius2;
                    break;
                case MetrologyObjectType.Rectangle:
                    res.FitCenterRow = o.FitRow; res.FitCenterCol = o.FitColumn;
                    res.FitPhi = o.FitPhi; res.FitLength1 = o.FitLength1; res.FitLength2 = o.FitLength2;
                    break;
            }
            if (o.MeasurePointRows != null) res.MetrologyMeasureRows.AddRange(o.MeasurePointRows);
            if (o.MeasurePointCols != null) res.MetrologyMeasureCols.AddRange(o.MeasurePointCols);
            // 結果表不留空白：ValueText 為空時退回顯示訊息/失敗原因。
            if (string.IsNullOrEmpty(res.ValueText))
                res.ValueText = !string.IsNullOrEmpty(o.ErrorMessage) ? o.ErrorMessage : (o.Success ? "(no value)" : "量測失敗");
            return res;
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
                res.ResidualRmsPx = circle.ResidualRms;
                res.CircleRoundnessPx = circle.Roundness;   // max-min 徑向＝GD&T 真圓度帶（真值）
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
                res.ResidualRmsPx = line.ResidualRms;   // 供真直度（RMS 近似，v1）

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
                    // 與 Fit Line 顯示一致：同時顯示線長與（判定用的）角度。
                    res.ValueText = string.Format(CultureInfo.InvariantCulture,
                        "Len={0:F1}px Ang={1:F2}deg", line.Length, aligned);
                }
                else
                {
                    res.IsOk = null;  // 元素不判定
                    // 顯示用正規化到無方向慣例 [0,180)：線無方向，0° 與 180° 同一條線。
                    // 否則擬合端點排序會讓水平線顯示 180°，操作員判讀困惑。與 deg-公差判定路徑一致。
                    double displayAngle = AngleNormalizer.ToHalfCircle(line.AngleDeg);
                    // 與 Fit Line 顯示一致：Len=…px Ang=…deg（先前只有角度，缺線長）。
                    res.ValueText = string.Format(CultureInfo.InvariantCulture,
                        "Len={0:F1}px Ang={1:F2}deg", line.Length, displayAngle);
                }
            }
            catch (HalconException ex)
            {
                res.Measured = false;
                res.ValueText = "量測異常";
                res.Message = ex.Message;
            }
        }

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

        private static bool IsGdtType(string t)
        {
            return t == "roundness" || t == "straightness" || t == "parallelism"
                || t == "perpendicularity" || t == "concentricity";
        }

        // Pass 1.7 GD&T 形位公差：偏差(px) → mm → 單邊判定(0 ≤ dev ≤ T)。
        // roundness 需 1 circle（偏差=max-min 徑向，真值）；straightness 需 1 line（偏差=ResidualRms 近似，v1）；
        // parallelism/perpendicularity 需 2 line（量測線, 基準線）；concentricity 需 2 circle（量測, 基準）。
        // 僅允許參照 line/circle 基礎元件（不支援鏈式參照構造結果）。
        private void MeasureGdt(ToolRunResult res, MeasurementTool tool,
            Dictionary<string, ToolRunResult> byId, double pixelSizeUm)
        {
            res.Supported = true;

            if (tool.Gdt == null)
            {
                res.Measured = false;
                res.ValueText = "缺 GD&T 規格";
                res.Message = tool.ToolType + " 工具缺 Gdt 規格";
                return;
            }
            if (tool.RefToolIds == null || tool.RefToolIds.Count < 1)
            {
                res.Measured = false;
                res.ValueText = "需參考元素";
                res.Message = tool.ToolType + " 需 RefToolIds";
                return;
            }

            ToolRunResult a;
            if (!byId.TryGetValue(tool.RefToolIds[0], out a))
            {
                res.Measured = false; res.ValueText = "找不到參考元素";
                res.Message = "RefToolIds[0] 指向的元素不存在"; return;
            }
            if (!a.Measured) { res.Measured = false; res.ValueText = "參考元素未量測"; return; }
            if (!IsBaseElement(a))
            {
                res.Measured = false; res.ValueText = "不支援鏈式參照";
                res.Message = "GD&T v1 只能參照 line/circle 基礎元件"; return;
            }

            double deviationPx;

            if (tool.ToolType == "roundness")
            {
                if (a.ToolType != "circle")
                {
                    res.Measured = false; res.ValueText = "需 circle";
                    res.Message = "真圓度需參照一個 circle 元素"; return;
                }
                deviationPx = a.CircleRoundnessPx;
            }
            else if (tool.ToolType == "straightness")
            {
                if (a.ToolType != "line")
                {
                    res.Measured = false; res.ValueText = "需 line";
                    res.Message = "真直度需參照一個 line 元素"; return;
                }
                deviationPx = a.ResidualRmsPx;
            }
            else
            {
                // 方位/位置類：需第二個 ref（基準 A）
                if (tool.RefToolIds.Count < 2)
                {
                    res.Measured = false; res.ValueText = "需 2 參考元素";
                    res.Message = tool.ToolType + " 需量測元素 + 基準元素"; return;
                }
                ToolRunResult b;
                if (!byId.TryGetValue(tool.RefToolIds[1], out b))
                {
                    res.Measured = false; res.ValueText = "找不到基準元素";
                    res.Message = "RefToolIds[1] 指向的基準不存在"; return;
                }
                if (!b.Measured) { res.Measured = false; res.ValueText = "基準元素未量測"; return; }
                if (!IsBaseElement(b))
                {
                    res.Measured = false; res.ValueText = "不支援鏈式參照";
                    res.Message = "GD&T v1 基準只能是 line/circle 基礎元件"; return;
                }

                if (tool.ToolType == "parallelism" || tool.ToolType == "perpendicularity")
                {
                    if (a.ToolType != "line" || b.ToolType != "line")
                    {
                        res.Measured = false; res.ValueText = "需兩條線";
                        res.Message = tool.ToolType + " 需量測線與基準線（皆 line）"; return;
                    }
                    deviationPx = tool.ToolType == "parallelism"
                        ? GdtCalculator.ParallelismZonePx(
                            a.LineRow1, a.LineCol1, a.LineRow2, a.LineCol2,
                            b.LineRow1, b.LineCol1, b.LineRow2, b.LineCol2)
                        : GdtCalculator.PerpendicularityZonePx(
                            a.LineRow1, a.LineCol1, a.LineRow2, a.LineCol2,
                            b.LineRow1, b.LineCol1, b.LineRow2, b.LineCol2);
                    // 視覺化：量測線中點 → 基準線上垂足（T5 細修；此處先給連線錨點）
                    double aMidRow = (a.LineRow1 + a.LineRow2) / 2.0, aMidCol = (a.LineCol1 + a.LineCol2) / 2.0;
                    ProjectPointOntoLine(aMidRow, aMidCol, b.LineRow1, b.LineCol1, b.LineRow2, b.LineCol2,
                        out double fR, out double fC);
                    res.DistRow1 = aMidRow; res.DistCol1 = aMidCol; res.DistRow2 = fR; res.DistCol2 = fC;
                }
                else // concentricity
                {
                    if (a.ToolType != "circle" || b.ToolType != "circle")
                    {
                        res.Measured = false; res.ValueText = "需兩個圓";
                        res.Message = "同心度需量測圓與基準圓（皆 circle）"; return;
                    }
                    deviationPx = GdtCalculator.ConcentricityDiametralPx(
                        a.FitCenterRow, a.FitCenterCol, b.FitCenterRow, b.FitCenterCol);
                    // 視覺化：量測圓心 → 基準圓心 偏移線
                    res.DistRow1 = a.FitCenterRow; res.DistCol1 = a.FitCenterCol;
                    res.DistRow2 = b.FitCenterRow; res.DistCol2 = b.FitCenterCol;
                }
            }

            double deviationMm = deviationPx * pixelSizeUm / 1000.0;
            GdtJudgment j = GdtEvaluation.Evaluate(deviationMm, tool.Gdt.ToleranceZoneMm);

            res.Measured = true;
            res.GdtDeviationMm = deviationMm;
            res.IsOk = j.IsOk;
            res.ValueText = string.Format(CultureInfo.InvariantCulture, "偏差={0:F4}mm", deviationMm);
            res.Message = j.Message;
        }

        // A5：把參考工具結果解析成幾何基元（即其 OutputPrimitive）。
        private static GeometricPrimitive ResolvePrimitive(ToolRunResult r)
        {
            return r != null ? r.OutputPrimitive : null;
        }

        // distance：解析兩個 ref 為幾何基元，依 (Kind,Kind) 路由到既有 measurer
        // （line↔line / circle↔circle / 點(或圓心)↔線 / 點↔點）；mm 由 measurer 內部換算 → 公差判定。
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
                        out double footRow, out double footCol);
                    res.DistRow1 = pr; res.DistCol1 = pc; res.DistRow2 = footRow; res.DistCol2 = footCol;
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

        // angle（B2c）：參考兩條線（line 元素或構造 midline，皆為 Line primitive），
        // 用 angle_ll 算夾角的 AcuteAngleDeg（0~90）。旋轉不變量：工件轉 30°，兩線各多 30°，夾角仍同。
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

            try
            {
                var ap = AngleMeasurementParameters.Default();
                AngleMeasurementResult ar = _angleMeasurer.MeasureAngle(
                    pa.Row1, pa.Col1, pa.Row2, pa.Col2,
                    pb.Row1, pb.Col1, pb.Row2, pb.Col2, ap);

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
                res.AngleCenterRow = (pa.Row1 + pa.Row2 + pb.Row1 + pb.Row2) / 4.0;
                res.AngleCenterCol = (pa.Col1 + pa.Col2 + pb.Col1 + pb.Col2) / 4.0;
                res.AngleRadiusPx = 80.0;
                res.AngleStartRad = Math.Atan2(pa.Row1 - res.AngleCenterRow, pa.Col1 - res.AngleCenterCol);

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
