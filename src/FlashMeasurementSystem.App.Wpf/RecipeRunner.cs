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

                results.Add(res);
                if (!string.IsNullOrEmpty(tool.Id)) byId[tool.Id] = res;
            }

            // ── Pass 2：複合工具（distance）與其他 ──
            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null) continue;
                if (tool.ToolType == "circle" || tool.ToolType == "line") continue;  // 已於 Pass 1 處理

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
                res.IsOk = null;  // 元素不判定（B2a）
                res.ValueText = string.Format(CultureInfo.InvariantCulture, "Ang={0:F2}deg", line.AngleDeg);
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
            if (a.ToolType != "line" || b.ToolType != "line")
            {
                res.Measured = false;
                res.ValueText = "僅支援 line↔line";
                res.Message = "B2b 距離僅支援兩 line 元素";
                return;
            }

            try
            {
                var dp = new DistanceMeasurementParameters
                {
                    PixelSizeUmX = pixelSizeUmX,
                    PixelSizeUmY = pixelSizeUmY
                };
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
                // 連線兩端用兩元素線段中點（供視覺標註）。
                res.DistRow1 = (a.LineRow1 + a.LineRow2) / 2.0;
                res.DistCol1 = (a.LineCol1 + a.LineCol2) / 2.0;
                res.DistRow2 = (b.LineRow1 + b.LineRow2) / 2.0;
                res.DistCol2 = (b.LineCol1 + b.LineCol2) / 2.0;
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
