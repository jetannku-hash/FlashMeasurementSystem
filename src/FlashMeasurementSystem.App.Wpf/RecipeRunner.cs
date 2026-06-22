using System;
using System.Collections.Generic;
using System.Globalization;
using FlashMeasurementSystem.Application.CircleFitting;
using FlashMeasurementSystem.Application.CoordinateSystem;
using FlashMeasurementSystem.Application.EdgeDetection;
using FlashMeasurementSystem.Application.Tolerance;
using FlashMeasurementSystem.Domain.CircleFitting;
using FlashMeasurementSystem.Domain.CoordinateSystem;
using FlashMeasurementSystem.Domain.EdgeDetection;
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
        private readonly IToleranceJudger _judger;
        private readonly ICoordinateMapper _mapper;

        public RecipeRunner(IEdgeDetector<HImage> edgeDetector, ICircleFitter circleFitter,
            IToleranceJudger judger, ICoordinateMapper mapper)
        {
            _edgeDetector = edgeDetector;
            _circleFitter = circleFitter;
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

            foreach (MeasurementTool tool in recipe.Tools)
            {
                if (tool == null || tool.Roi == null) continue;
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
                    res.Supported = false;
                    res.IsOk = null;
                    res.ValueText = "(B1 未支援)";
                    res.Message = "工具型別 '" + tool.ToolType + "' 於 B1 尚未支援";
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
    }
}
