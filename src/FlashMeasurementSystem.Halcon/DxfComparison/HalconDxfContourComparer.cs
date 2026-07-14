using System;
using System.Collections.Generic;
using FlashMeasurementSystem.Application.DxfComparison;
using FlashMeasurementSystem.Domain.DxfComparison;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.DxfComparison
{
    /// <summary>
    /// DXF 輪廓度比對的 HALCON 實作（唯一碰 HalconDotNet 之處）。
    /// 管線：讀 DXF → 建 scaled shape model → 影像定位 → 對位標稱 → 框帶內取實際邊 →
    /// distance_contours_xld 逐點偏差 → Domain 判定。所有 HalconException 轉 failed result，不外拋；
    /// 所有 HObject/HImage/shape model 句柄以 finally 釋放。簽章對照離線 reference 驗證。
    /// </summary>
    public class HalconDxfContourComparer : IDxfContourComparer<HImage>
    {
        // 讀 DXF，回傳標稱輪廓供載入時預覽（呼叫端 dispose；失敗回 null）。
        public HObject LoadNominalContour(string dxfFilePath, DxfComparisonParameters parameters)
        {
            var p = parameters ?? DxfComparisonParameters.Default();
            if (string.IsNullOrEmpty(dxfFilePath)) return null;
            HObject nominal = null;
            try
            {
                HTuple genNames = new HTuple(new string[] { "min_num_points", "max_approx_error" });
                HTuple genVals = new HTuple(new HTuple(p.MinNumPoints)).TupleConcat(new HTuple(p.MaxApproxError));
                HOperatorSet.ReadContourXldDxf(out nominal, dxfFilePath, genNames, genVals, out HTuple _);
                HOperatorSet.CountObj(nominal, out HTuple n);
                if (n.Length == 0 || n.I == 0) { nominal?.Dispose(); return null; }
                return nominal; // caller disposes
            }
            catch (HalconException) { nominal?.Dispose(); return null; }
        }

        // 介面方法：委派 CompareWithOverlay 並釋放回傳的 iconic（維持原行為，不外拋）。
        public DxfComparisonResult Compare(HImage image, string dxfFilePath, DxfComparisonParameters parameters)
        {
            HObject aligned = null, actual = null;
            try { return CompareWithOverlay(image, dxfFilePath, parameters, out aligned, out actual, out double[] _, out double[] _); }
            finally { aligned?.Dispose(); actual?.Dispose(); }
        }

        // 與 Compare 相同管線，但將對位標稱輪廓 + 實際邊 + 超差點座標交給 UI 疊圖。
        // alignedNominal / actualEdges 所有權移交呼叫端（不在此 finally 釋放）；其餘 iconic 內部釋放。
        public DxfComparisonResult CompareWithOverlay(HImage image, string dxfFilePath, DxfComparisonParameters parameters,
            out HObject alignedNominal, out HObject actualEdges, out double[] overRows, out double[] overCols)
        {
            alignedNominal = null; actualEdges = null; overRows = new double[0]; overCols = new double[0];

            var p = parameters ?? DxfComparisonParameters.Default();
            if (image == null) return DxfComparisonResult.Failed("影像為空");
            if (string.IsNullOrEmpty(dxfFilePath)) return DxfComparisonResult.Failed("未指定 DXF 檔");

            HObject nominal = null, modelContours = null;
            HObject distContour = null;
            HObject bandMargin = null, band = null, reduced = null;
            HImage gray = null;
            HTuple modelId = null;
            try
            {
                // 1. 讀 DXF → 標稱輪廓（x→col、y→row、z 忽略；只吃 AC1009/R12）
                HTuple genNames = new HTuple(new string[] { "min_num_points", "max_approx_error" });
                HTuple genVals = new HTuple(new HTuple(p.MinNumPoints)).TupleConcat(new HTuple(p.MaxApproxError));
                HOperatorSet.ReadContourXldDxf(out nominal, dxfFilePath, genNames, genVals, out HTuple dxfStatus);
                HOperatorSet.CountObj(nominal, out HTuple nContours);
                if (nContours.Length == 0 || nContours.I == 0)
                    return DxfComparisonResult.Failed("DXF 無可讀輪廓（可能非 AC1009/R12 或實體不支援）");

                // 2. scaled shape model；scale 種子收斂搜尋範圍（spec §6.1）
                double scaleMin = p.ScaleMin, scaleMax = p.ScaleMax;
                if (p.ScaleSeedPxPerMm > 0)
                {
                    scaleMin = p.ScaleSeedPxPerMm * 0.7;
                    scaleMax = p.ScaleSeedPxPerMm * 1.3;
                }
                // 參數順序（ref L113760）：NumLevels, AngleStart, AngleExtent, AngleStep,
                // ScaleMin, ScaleMax, ScaleStep, Optimization, Metric, MinContrast。
                // Optimization='none' 保留全部模型點，供 distance ContourTo 用高密度標稱；
                // Metric='ignore_local_polarity'（DXF 輪廓無 edge_direction，polarity 類 metric 不可用）。
                HOperatorSet.CreateScaledShapeModelXld(nominal, new HTuple("auto"),
                    new HTuple(0.0), new HTuple(2.0 * Math.PI), new HTuple("auto"),
                    new HTuple(scaleMin), new HTuple(scaleMax), new HTuple("auto"),
                    new HTuple("none"), new HTuple("ignore_local_polarity"), new HTuple(5),
                    out modelId);

                // 3. 單通道（find_scaled_shape_model 對多通道只用第 1 通道，仍先統一慣例）
                gray = EnsureSingleChannel(image);
                HImage work = gray ?? image;

                // 4. 定位（參數順序 ref L115459）：ModelID, AngleStart, AngleExtent, ScaleMin, ScaleMax,
                //    MinScore, NumMatches, MaxOverlap, SubPixel, NumLevels, Greediness。
                HOperatorSet.FindScaledShapeModel(work, modelId,
                    new HTuple(0.0), new HTuple(2.0 * Math.PI), new HTuple(scaleMin), new HTuple(scaleMax),
                    new HTuple(p.MinScore), new HTuple(1), new HTuple(0.5), new HTuple("least_squares"),
                    new HTuple(0), new HTuple(0.9),
                    out HTuple row, out HTuple col, out HTuple angle, out HTuple scale, out HTuple score);
                if (score.Length == 0)
                    return DxfComparisonResult.Failed("工件未定位（無匹配或 score < MinScore）");

                // 5. 對位標稱：取模型輪廓（已正規化到參考點 (0,0)，ref L116868）→ scale→rotate→translate。
                //    必須用模型輪廓而非原始 DXF 輪廓：模型參考點為輪廓外接矩形中心，
                //    對原始輪廓以原點縮放會使位置錯置。
                HOperatorSet.GetShapeModelContours(out modelContours, modelId, 1);
                HOperatorSet.HomMat2dIdentity(out HTuple hom);
                HOperatorSet.HomMat2dScale(hom, scale, scale, new HTuple(0.0), new HTuple(0.0), out hom);
                HOperatorSet.HomMat2dRotate(hom, angle, new HTuple(0.0), new HTuple(0.0), out hom);
                HOperatorSet.HomMat2dTranslate(hom, row, col, out hom);
                HOperatorSet.AffineTransContourXld(modelContours, out alignedNominal, hom);

                // 6. 框帶內取實際輪廓（濾內部特徵/背景雜訊邊，spec §6.2）
                HOperatorSet.GenRegionContourXld(alignedNominal, out bandMargin, "margin");
                HOperatorSet.DilationCircle(bandMargin, out band, new HTuple(p.BandWidthPx));
                HOperatorSet.ReduceDomain(work, band, out reduced);
                HOperatorSet.EdgesSubPix(reduced, out actualEdges, "canny",
                    new HTuple(p.EdgeAlpha), new HTuple(p.EdgeLowThreshold), new HTuple(p.EdgeHighThreshold));
                HOperatorSet.CountObj(actualEdges, out HTuple nEdges);
                if (nEdges.Length == 0 || nEdges.I == 0)
                    return DxfComparisonResult.Failed("框帶內取不到實際輪廓（BandWidthPx/邊緣門檻需調整）");

                // 7. 逐點偏差：From=實際、To=對位標稱（量實際對標稱的偏離；ref L155816）
                HOperatorSet.DistanceContoursXld(actualEdges, alignedNominal, out distContour, "point_to_segment");

                var devs = new List<double>();
                var oRows = new List<double>();
                var oCols = new List<double>();
                HOperatorSet.CountObj(distContour, out HTuple nDist);
                for (int i = 1; i <= nDist.I; i++)
                {
                    HObject one = null;
                    try
                    {
                        HOperatorSet.SelectObj(distContour, out one, i);
                        HOperatorSet.GetContourAttribXld(one, "distance", out HTuple dAttr);
                        HOperatorSet.GetContourXld(one, out HTuple pr, out HTuple pc);
                        for (int k = 0; k < dAttr.Length; k++)
                        {
                            double dev = Math.Abs(dAttr[k].D);
                            devs.Add(dev);
                            if (dev > p.TolerancePx && k < pr.Length && k < pc.Length)
                            { oRows.Add(pr[k].D); oCols.Add(pc[k].D); }
                        }
                    }
                    finally { one?.Dispose(); }
                }
                overRows = oRows.ToArray();
                overCols = oCols.ToArray();

                // 8. 判定（純 Domain）+ 附姿態
                DxfComparisonResult result = DxfDeviationEvaluator.Evaluate(devs.ToArray(), p.TolerancePx);
                if (result.Success)
                {
                    result.MatchScore = score[0].D;
                    result.PoseRow = row[0].D;
                    result.PoseCol = col[0].D;
                    result.PoseAngleRad = angle[0].D;
                    result.PoseScale = scale[0].D;
                    result.Message = result.IsPass
                        ? string.Format("PASS  max={0:F3}px  mean={1:F3}px  (T={2:F3}px)", result.MaxDevPx, result.MeanDevPx, p.TolerancePx)
                        : string.Format("FAIL  max={0:F3}px > T={1:F3}px  超差 {2} 點", result.MaxDevPx, p.TolerancePx, result.PointsOverTolerance);
                }
                return result;
            }
            catch (HalconException ex)
            {
                return DxfComparisonResult.Failed("DXF 比對錯誤：" + ex.Message);
            }
            finally
            {
                nominal?.Dispose();
                modelContours?.Dispose();
                distContour?.Dispose();
                bandMargin?.Dispose();
                band?.Dispose();
                reduced?.Dispose();
                gray?.Dispose();
                if (modelId != null) HOperatorSet.ClearShapeModel(modelId);
            }
        }

        // 回傳 null 表示原圖已是單通道（直接用原圖）；非 null 為新建單通道影像，由呼叫端 dispose。
        // 3 通道用 rgb1_to_gray（加權灰階），其他取第 1 通道。與其他 HALCON adapter 相同慣例。
        private static HImage EnsureSingleChannel(HImage source)
        {
            HOperatorSet.CountChannels(source, out HTuple channels);
            int channelCount = (channels != null && channels.Length > 0) ? channels.I : 1;
            if (channelCount <= 1) return null;
            return channelCount == 3 ? source.Rgb1ToGray() : source.AccessChannel(1);
        }
    }
}
