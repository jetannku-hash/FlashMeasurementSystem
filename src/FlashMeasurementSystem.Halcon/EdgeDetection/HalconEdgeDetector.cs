using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FlashMeasurementSystem.Application.EdgeDetection;
using FlashMeasurementSystem.Domain.EdgeDetection;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.EdgeDetection
{
    public class HalconEdgeDetector : IEdgeDetector<HImage>
    {
        public EdgeResult DetectEdges(HImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters)
        {
            var result = new EdgeResult();
            EdgeDetectionParameters effectiveParameters = parameters ?? EdgeDetectionParameters.Default();

            if (image == null)
            {
                result.ErrorMessage = "請先載入影像";
                Log("DetectEdges EARLY-EXIT msg=" + result.ErrorMessage);
                return result;
            }

            if (roi == null || !roi.IsDefined)
            {
                result.ErrorMessage = "請先定義 ROI";
                Log("DetectEdges EARLY-EXIT " + DescribeRoi(roi) + " msg=" + result.ErrorMessage);
                return result;
            }

            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
            int imageWidth = width.I;
            int imageHeight = height.I;
            int channels = TryGetChannels(image);
            string pixelType = TryGetPixelType(image);
            string imgDesc = DescribeImage(imageWidth, imageHeight, channels, pixelType);

            Log("DetectEdges START " + imgDesc + " " + DescribeRoi(roi) + " " + DescribeParams(effectiveParameters));

            if (IsOutsideImage(roi, imageWidth, imageHeight))
            {
                result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                    "ROI 超出影像邊界 ({0}, {1})", imgDesc, DescribeRoi(roi));
                Log("DetectEdges OUTSIDE-IMG msg=" + result.ErrorMessage);
                return result;
            }

            if (!EdgeDetectionParameters.IsSupportedInterpolation(effectiveParameters.Interpolation))
            {
                result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                    "不支援的插值模式: {0} (支援: nearest_neighbor, bilinear, bicubic)",
                    effectiveParameters.Interpolation);
                Log("DetectEdges EARLY-EXIT msg=" + result.ErrorMessage);
                return result;
            }

            if (!EdgeDetectionParameters.IsSupportedMeasureMode(effectiveParameters.MeasureMode))
            {
                result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                    "不支援的量測模式: {0} (支援: single_edge, edge_pair)",
                    effectiveParameters.MeasureMode);
                Log("DetectEdges EARLY-EXIT msg=" + result.ErrorMessage);
                return result;
            }

            // Clamp the ROI to the image bounds so that HALCON never receives a measure
            // rectangle whose projection profiles fall entirely outside the pixel grid.
            // gen_measure_rectangle2 clips internally, but measure_pos (HALCON 17.12)
            // can throw #3104 "HContToPol: distance of points too big" when the clipped
            // portion yields zero or near-zero valid projection lines.
            double clampedCenterRow = Clamp(roi.CenterRow, 0.0, imageHeight - 1.0);
            double clampedCenterCol = Clamp(roi.CenterCol, 0.0, imageWidth - 1.0);
            ClampLengthsToImage(roi, clampedCenterRow, clampedCenterCol, imageWidth, imageHeight,
                out double maxLength1, out double maxLength2);

            Log(string.Format(CultureInfo.InvariantCulture,
                "DetectEdges CLAMP center=(R={0:F1},C={1:F1}) maxL1={2:F1} maxL2={3:F1}",
                clampedCenterRow, clampedCenterCol, maxLength1, maxLength2));

            if (maxLength1 < 1.0 || maxLength2 < 1.0)
            {
                result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                    "ROI 經邊界裁剪後尺寸過小 ({0}, clamped center=(R={1:F0},C={2:F0}), valid L1={3:F1} L2={4:F1})",
                    imgDesc, clampedCenterRow, clampedCenterCol, maxLength1, maxLength2);
                Log("DetectEdges CLAMP-TOO-SMALL msg=" + result.ErrorMessage);
                return result;
            }

            HImage convertedImage = EnsureSingleChannel(image, channels, "DetectEdges");
            HImage workingImage = convertedImage ?? image;

            try
            {
                bool usePairs = effectiveParameters.MeasureMode == "edge_pair";

                // PRIMARY 嘗試：用 FromBounds 推出的方向
                MeasureAttempt primary;
                if (usePairs)
                {
                    primary = RunMeasurePairs(workingImage,
                        clampedCenterRow, clampedCenterCol, roi.AngleRad,
                        maxLength1, maxLength2, width, height, effectiveParameters, "PRIMARY");
                }
                else
                {
                    primary = RunMeasurePos(workingImage,
                        clampedCenterRow, clampedCenterCol, roi.AngleRad,
                        maxLength1, maxLength2, width, height, effectiveParameters, "PRIMARY");
                }

                MeasureAttempt selected = primary;

                // 沒例外且找 0 邊 → FALLBACK：rotate 90°（swap L1/L2 + Phi += π/2）再試
                // 因為 ROI 形狀無法決定 measure 方向是「找水平邊」或「找垂直邊」，
                // 兩種 case 都可能對應同一個 ROI 形狀。自動試另一個方向。
                if (primary.Exception == null && primary.Edges.Count == 0)
                {
                    // 壓到 [0, π) 保證主軸是「正方向」（Phi=π 跟 Phi=0 描述同一矩形但
                    // measure_pos 掃描方向相反、edge 排序倒轉）。
                    double fallbackPhi = NormalizeToPositiveAxis(roi.AngleRad + Math.PI / 2.0);
                    MeasureAttempt fallback;
                    if (usePairs)
                    {
                        fallback = RunMeasurePairs(workingImage,
                            clampedCenterRow, clampedCenterCol, fallbackPhi,
                            maxLength2, maxLength1,  // swap L1/L2
                            width, height, effectiveParameters, "FALLBACK");
                    }
                    else
                    {
                        fallback = RunMeasurePos(workingImage,
                            clampedCenterRow, clampedCenterCol, fallbackPhi,
                            maxLength2, maxLength1,  // swap L1/L2
                            width, height, effectiveParameters, "FALLBACK");
                    }

                    if (fallback.Edges.Count > 0)
                    {
                        selected = fallback;
                    }
                }

                result.EdgePoints = selected.Edges;

                // Build EdgePairs from selected raw tuples when in edge_pair mode
                if (usePairs && selected.Exception == null && selected.RawFirstRows != null)
                {
                    int pairCount = selected.RawFirstRows.Length;
                    int intraLen = selected.RawIntraDistances?.Length ?? 0;
                    int interLen = selected.RawInterDistances?.Length ?? 0;

                    for (int i = 0; i < pairCount; i++)
                    {
                        result.EdgePairs.Add(new EdgePair
                        {
                            FirstRow = selected.RawFirstRows[i].D,
                            FirstColumn = selected.RawFirstCols[i].D,
                            FirstAmplitude = (i < (selected.RawFirstAmplitudes?.Length ?? 0)) ? selected.RawFirstAmplitudes[i].D : 0.0,
                            SecondRow = selected.RawSecondRows[i].D,
                            SecondColumn = selected.RawSecondCols[i].D,
                            SecondAmplitude = (i < (selected.RawSecondAmplitudes?.Length ?? 0)) ? selected.RawSecondAmplitudes[i].D : 0.0,
                            IntraDistance = (i < intraLen) ? selected.RawIntraDistances[i].D : 0.0,
                            InterDistance = (i < interLen) ? selected.RawInterDistances[i].D : 0.0
                        });
                    }
                }

                if (selected.Exception != null)
                {
                    result.Success = false;
                    result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                        "邊緣檢測異常 [{0}]: {1} | {2} {3} {4}",
                        selected.Exception.GetErrorCode(), selected.Exception.Message,
                        DescribeParams(effectiveParameters),
                        DescribeRoi(roi),
                        imgDesc);
                    Log("DetectEdges EXCEPTION " + result.ErrorMessage);
                }
                else if (selected.Edges.Count > 0)
                {
                    result.Success = true;
                    result.ErrorMessage = string.Empty;
                }
                else
                {
                    result.Success = false;
                    // measure_pos 只在「橫跨 ROI 中心掃描線、且大致垂直於主軸 (Angle)」的位置偵測邊：
                    // 它沿主軸從中心往兩側各掃 ScanLength/2，並對垂直主軸、寬 ROIWidth 的條帶取平均。
                    // 因此兩種典型情況會回 0 邊，而「自動旋轉 90°」也救不了：
                    //   (1) ROI 中心落在實心區內、未跨在邊上 → 掃描線在 ±ScanLength/2 內碰不到任何邊。
                    //   (2) 邊是傾斜的、主軸未對齊 → 斜邊在寬條帶平均下被抹平，梯度低於 Threshold。
                    // 訊息直接點出這兩點與對應處置，取代舊的「兩個方向都未檢測到」(會誤導成已涵蓋所有方向)。
                    result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                        "未偵測到邊緣：measure_pos 只偵測「橫跨 ROI 中心掃描線、且垂直於主軸 (Angle)」的邊。" +
                        "請確認 ROI 中心跨在邊上 (勿整個框落在實心區內)；若邊是傾斜的，設定 Angle 使主軸垂直於邊、或縮小 ROI Width 避免斜邊被平均抹平；" +
                        "仍失敗可降低 Threshold/Sigma，或改用 EdgesSubPix。 | {0} {1} {2}",
                        DescribeParams(effectiveParameters),
                        DescribeRoi(roi),
                        imgDesc);
                }
            }
            finally
            {
                convertedImage?.Dispose();
            }

            Log(string.Format(CultureInfo.InvariantCulture,
                "DetectEdges END success={0} edges={1} msg={2}",
                result.Success, result.EdgePoints.Count, string.IsNullOrEmpty(result.ErrorMessage) ? "(none)" : result.ErrorMessage));

            return result;
        }

        private struct MeasureAttempt
        {
            public List<EdgePoint> Edges;
            public HalconException Exception;
            public HTuple RawFirstRows;
            public HTuple RawFirstCols;
            public HTuple RawFirstAmplitudes;
            public HTuple RawSecondRows;
            public HTuple RawSecondCols;
            public HTuple RawSecondAmplitudes;
            public HTuple RawIntraDistances;
            public HTuple RawInterDistances;
        }

        private static double NormalizeToPositiveAxis(double rad)
        {
            // measure_pos 對 Phi 跟 Phi+π 看到同一個矩形（位置/大小相同），但掃描方向相反，
            // 導致 edge tuple 排序倒轉。把 Phi 壓到 [0, π) 確保主軸永遠是「正方向」，
            // edge 自動按 col 從小到大（Phi=0）或 row 從小到大（Phi=π/2）排序。
            double n = rad % Math.PI;
            if (n < 0) n += Math.PI;
            return n;
        }

        private static MeasureAttempt RunMeasurePos(
            HImage image,
            double centerRow, double centerCol, double phi,
            double length1, double length2,
            HTuple width, HTuple height,
            EdgeDetectionParameters p, string label)
        {
            var attempt = new MeasureAttempt { Edges = new List<EdgePoint>() };
            HTuple measureHandle = null;
            try
            {
                HOperatorSet.GenMeasureRectangle2(
                    centerRow, centerCol, phi, length1, length2,
                    width, height, p.Interpolation, out measureHandle);

                HOperatorSet.MeasurePos(image, measureHandle,
                    new HTuple(p.Sigma), new HTuple(p.Threshold),
                    new HTuple(p.Polarity), new HTuple(p.EdgeSelector),
                    out HTuple edgeRow, out HTuple edgeCol,
                    out HTuple edgeAmplitude, out HTuple edgeDistance);

                int lenRow = edgeRow?.Length ?? 0;
                int lenCol = edgeCol?.Length ?? 0;
                int lenAmp = edgeAmplitude?.Length ?? 0;
                int lenDist = edgeDistance?.Length ?? 0;

                Log(string.Format(CultureInfo.InvariantCulture,
                    "DetectEdges {0} MEASUREPOS phi={1:F4} L1={2:F1} L2={3:F1} lenRow={4} lenCol={5} lenAmp={6} lenDist={7}",
                    label, phi, length1, length2, lenRow, lenCol, lenAmp, lenDist));

                for (int i = 0; i < lenRow; i++)
                {
                    attempt.Edges.Add(new EdgePoint
                    {
                        Row = edgeRow[i].D,
                        Column = edgeCol[i].D,
                        Amplitude = (i < lenAmp) ? edgeAmplitude[i].D : 0.0,
                        Distance = (i < lenDist) ? edgeDistance[i].D : 0.0
                    });
                }
            }
            catch (HalconException ex)
            {
                attempt.Exception = ex;
                Log(string.Format(CultureInfo.InvariantCulture,
                    "DetectEdges {0} EXCEPTION [{1}] {2}",
                    label, ex.GetErrorCode(), ex.Message));
            }
            finally
            {
                if (measureHandle != null) HOperatorSet.CloseMeasure(measureHandle);
            }
            return attempt;
        }

        private static MeasureAttempt RunMeasurePairs(
            HImage image,
            double centerRow, double centerCol, double phi,
            double length1, double length2,
            HTuple width, HTuple height,
            EdgeDetectionParameters p, string label)
        {
            var attempt = new MeasureAttempt { Edges = new List<EdgePoint>() };
            HTuple measureHandle = null;
            try
            {
                HOperatorSet.GenMeasureRectangle2(
                    centerRow, centerCol, phi, length1, length2,
                    width, height, p.Interpolation, out measureHandle);

                HOperatorSet.MeasurePairs(image, measureHandle,
                    new HTuple(p.Sigma), new HTuple(p.Threshold),
                    new HTuple(p.Polarity), new HTuple(p.EdgeSelector),
                    out HTuple rowFirst, out HTuple colFirst, out HTuple ampFirst,
                    out HTuple rowSecond, out HTuple colSecond, out HTuple ampSecond,
                    out HTuple intraDist, out HTuple interDist);

                int lenFirst = rowFirst?.Length ?? 0;
                int lenSecond = rowSecond?.Length ?? 0;

                Log(string.Format(CultureInfo.InvariantCulture,
                    "DetectEdges {0} MEASUREPAIRS phi={1:F4} L1={2:F1} L2={3:F1} first={4} second={5}",
                    label, phi, length1, length2, lenFirst, lenSecond));

                // Store raw tuples for EdgePairs construction
                attempt.RawFirstRows = rowFirst;
                attempt.RawFirstCols = colFirst;
                attempt.RawFirstAmplitudes = ampFirst;
                attempt.RawSecondRows = rowSecond;
                attempt.RawSecondCols = colSecond;
                attempt.RawSecondAmplitudes = ampSecond;
                attempt.RawIntraDistances = intraDist;
                attempt.RawInterDistances = interDist;

                // Flatten first and second edges into Edges for overlay compatibility
                for (int i = 0; i < lenFirst; i++)
                {
                    attempt.Edges.Add(new EdgePoint
                    {
                        Row = rowFirst[i].D,
                        Column = colFirst[i].D,
                        Amplitude = (i < (ampFirst?.Length ?? 0)) ? ampFirst[i].D : 0.0,
                        Distance = 0.0
                    });
                }
                for (int i = 0; i < lenSecond; i++)
                {
                    attempt.Edges.Add(new EdgePoint
                    {
                        Row = rowSecond[i].D,
                        Column = colSecond[i].D,
                        Amplitude = (i < (ampSecond?.Length ?? 0)) ? ampSecond[i].D : 0.0,
                        Distance = 0.0
                    });
                }
            }
            catch (HalconException ex)
            {
                attempt.Exception = ex;
                Log(string.Format(CultureInfo.InvariantCulture,
                    "DetectEdges {0} EXCEPTION [{1}] {2}",
                    label, ex.GetErrorCode(), ex.Message));
            }
            finally
            {
                if (measureHandle != null) HOperatorSet.CloseMeasure(measureHandle);
            }
            return attempt;
        }

        public EdgeResult DetectEdgesSubPix(HImage image, EdgeDetectionRoi roi, EdgeDetectionParameters parameters)
        {
            var result = new EdgeResult();
            EdgeDetectionParameters effectiveParameters = parameters ?? EdgeDetectionParameters.Default();

            if (image == null)
            {
                result.ErrorMessage = "請先載入影像";
                Log("DetectEdgesSubPix EARLY-EXIT msg=" + result.ErrorMessage);
                return result;
            }

            if (roi == null || !roi.IsDefined)
            {
                result.ErrorMessage = "請先定義 ROI";
                Log("DetectEdgesSubPix EARLY-EXIT " + DescribeRoi(roi) + " msg=" + result.ErrorMessage);
                return result;
            }

            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
            int imageWidth = width.I;
            int imageHeight = height.I;
            int channels = TryGetChannels(image);
            string pixelType = TryGetPixelType(image);
            string imgDesc = DescribeImage(imageWidth, imageHeight, channels, pixelType);

            Log("DetectEdgesSubPix START " + imgDesc + " " + DescribeRoi(roi) + " " + DescribeParams(effectiveParameters));

            if (IsOutsideImage(roi, imageWidth, imageHeight))
            {
                result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                    "ROI 超出影像邊界 ({0}, {1})", imgDesc, DescribeRoi(roi));
                Log("DetectEdgesSubPix OUTSIDE-IMG msg=" + result.ErrorMessage);
                return result;
            }

            double clampedCenterRow = Clamp(roi.CenterRow, 0.0, imageHeight - 1.0);
            double clampedCenterCol = Clamp(roi.CenterCol, 0.0, imageWidth - 1.0);
            ClampLengthsToImage(roi, clampedCenterRow, clampedCenterCol, imageWidth, imageHeight,
                out double maxLength1, out double maxLength2);

            Log(string.Format(CultureInfo.InvariantCulture,
                "DetectEdgesSubPix CLAMP center=(R={0:F1},C={1:F1}) maxL1={2:F1} maxL2={3:F1}",
                clampedCenterRow, clampedCenterCol, maxLength1, maxLength2));

            if (maxLength1 < 1.0 || maxLength2 < 1.0)
            {
                result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                    "ROI 經邊界裁剪後尺寸過小 ({0}, clamped center=(R={1:F0},C={2:F0}), valid L1={3:F1} L2={4:F1})",
                    imgDesc, clampedCenterRow, clampedCenterCol, maxLength1, maxLength2);
                Log("DetectEdgesSubPix CLAMP-TOO-SMALL msg=" + result.ErrorMessage);
                return result;
            }
            HRegion roiRegion = null;
            HImage reduced = null;
            HObject contours = null;
            HImage convertedImage = EnsureSingleChannel(image, channels, "DetectEdgesSubPix");
            HImage workingImage = convertedImage ?? image;

            try
            {
                roiRegion = new HRegion();
                roiRegion.GenRectangle2(
                    clampedCenterRow,
                    clampedCenterCol,
                    roi.AngleRad,
                    maxLength1,
                    maxLength2);

                reduced = workingImage.ReduceDomain(roiRegion);
                HOperatorSet.EdgesSubPix(
                    reduced,
                    out contours,
                    "canny",
                    new HTuple(effectiveParameters.Sigma),
                    new HTuple(effectiveParameters.Threshold),
                    new HTuple(effectiveParameters.HighThreshold));

                HOperatorSet.CountObj(contours, out HTuple contourCount);
                int totalContours = contourCount.I;

                // edges_sub_pix 在每個控制點上會附加屬性（canny 通常是 'contrast' / 'response'）。
                // 探查第一條輪廓決定 Amplitude 來源，後續輪廓沿用同一個屬性名稱。
                string amplitudeAttr = ProbeAmplitudeAttribute(contours, totalContours);

                Log(string.Format(CultureInfo.InvariantCulture,
                    "DetectEdgesSubPix EDGES_SUB_PIX contours={0} amplitudeAttr={1}",
                    totalContours, amplitudeAttr ?? "(none)"));

                for (int c = 1; c <= totalContours; c++)
                {
                    HObject contour = null;
                    try
                    {
                        HOperatorSet.SelectObj(contours, out contour, c);
                        HOperatorSet.GetContourXld(contour, out HTuple rowTuple, out HTuple colTuple);

                        HTuple amplitudeTuple = null;
                        if (amplitudeAttr != null)
                        {
                            try
                            {
                                HOperatorSet.GetContourAttribXld(contour, amplitudeAttr, out amplitudeTuple);
                            }
                            catch (HalconException)
                            {
                                amplitudeTuple = null;
                            }
                        }

                        int n = rowTuple.Length;
                        for (int i = 0; i < n; i++)
                        {
                            double row = rowTuple[i].D;
                            double col = colTuple[i].D;
                            double amp = (amplitudeTuple != null && i < amplitudeTuple.Length)
                                ? amplitudeTuple[i].D
                                : 0.0;
                            double dist = 0.0;
                            if (i + 1 < n)
                            {
                                double dr = rowTuple[i + 1].D - row;
                                double dc = colTuple[i + 1].D - col;
                                dist = Math.Sqrt(dr * dr + dc * dc);
                            }

                            result.EdgePoints.Add(new EdgePoint
                            {
                                Row = row,
                                Column = col,
                                Amplitude = amp,
                                Distance = dist
                            });
                        }
                    }
                    finally
                    {
                        contour?.Dispose();
                    }
                }

                result.Success = result.EdgePoints.Count > 0;
                result.ErrorMessage = result.Success
                    ? string.Empty
                    : string.Format(CultureInfo.InvariantCulture,
                        "未檢測到亞像素邊緣 | {0} {1} {2} | 提示：降低 threshold/highThreshold 或調整 sigma",
                        DescribeParams(effectiveParameters),
                        DescribeRoi(roi),
                        imgDesc);
            }
            catch (HalconException ex)
            {
                result.Success = false;
                result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                    "亞像素邊緣檢測異常 [{0}]: {1} | {2} {3} {4}",
                    ex.GetErrorCode(), ex.Message,
                    DescribeParams(effectiveParameters),
                    DescribeRoi(roi),
                    imgDesc);
                Log("DetectEdgesSubPix EXCEPTION " + result.ErrorMessage);
            }
            finally
            {
                contours?.Dispose();
                reduced?.Dispose();
                roiRegion?.Dispose();
                convertedImage?.Dispose();
            }

            Log(string.Format(CultureInfo.InvariantCulture,
                "DetectEdgesSubPix END success={0} edges={1} msg={2}",
                result.Success, result.EdgePoints.Count, string.IsNullOrEmpty(result.ErrorMessage) ? "(none)" : result.ErrorMessage));

            return result;
        }

        private static string ProbeAmplitudeAttribute(HObject contours, int totalContours)
        {
            if (totalContours <= 0)
            {
                return null;
            }

            HObject probe = null;
            try
            {
                HOperatorSet.SelectObj(contours, out probe, 1);
                HOperatorSet.QueryContourAttribsXld(probe, out HTuple attribs);
                if (attribs == null)
                {
                    return null;
                }

                // 偏好順序：contrast > response > edge_amplitude（在 canny / deriche / lanser 之間
                // 名稱不完全一致，依序探查最常見的命名）。
                bool hasContrast = false;
                bool hasResponse = false;
                bool hasEdgeAmplitude = false;
                for (int i = 0; i < attribs.Length; i++)
                {
                    string name = attribs[i].S;
                    if (name == "contrast") hasContrast = true;
                    else if (name == "response") hasResponse = true;
                    else if (name == "edge_amplitude") hasEdgeAmplitude = true;
                }

                if (hasContrast) return "contrast";
                if (hasResponse) return "response";
                if (hasEdgeAmplitude) return "edge_amplitude";
                return null;
            }
            catch (HalconException)
            {
                return null;
            }
            finally
            {
                probe?.Dispose();
            }
        }

        // ─── 診斷 log（檔案 + Debug.WriteLine） ───────────────────────────
        private static readonly object _logLock = new object();
        private static string _cachedLogPath;
        private const long MaxLogBytes = 5L * 1024 * 1024; // 超過 5MB 自動截斷

        private static string GetLogPath()
        {
            if (_cachedLogPath != null) return _cachedLogPath;
            try
            {
                var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (current != null)
                {
                    if (File.Exists(Path.Combine(current.FullName, "FlashMeasurementSystem.sln")))
                    {
                        var logsDir = Path.Combine(current.FullName, "data", "logs");
                        Directory.CreateDirectory(logsDir);
                        _cachedLogPath = Path.Combine(logsDir, "edge_detection.log");
                        return _cachedLogPath;
                    }
                    current = current.Parent;
                }
            }
            catch
            {
                // 任何路徑問題都退回到 TEMP
            }
            _cachedLogPath = Path.Combine(Path.GetTempPath(), "FlashMeasurementSystem_edge_detection.log");
            return _cachedLogPath;
        }

        private static void Log(string line)
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string msg = "[" + stamp + "] " + line;
            System.Diagnostics.Debug.WriteLine(msg);
            try
            {
                string path = GetLogPath();
                lock (_logLock)
                {
                    if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
                    {
                        // 截斷時保留上一輪歷史到 .bak（除錯需要回溯），而非直接刪除。
                        // 先刪舊 .bak 再 Move，避免 Move 因目標已存在而拋例外。
                        string bak = path + ".bak";
                        if (File.Exists(bak)) File.Delete(bak);
                        File.Move(path, bak);
                    }
                    File.AppendAllText(path, msg + Environment.NewLine);
                }
            }
            catch
            {
                // log 永遠不可拋例外
            }
        }

        private static int TryGetChannels(HImage image)
        {
            if (image == null) return 0;
            try
            {
                HOperatorSet.CountChannels(image, out HTuple ch);
                if (ch != null && ch.Length > 0) return ch.I;
            }
            catch { /* ignore */ }
            return 1;
        }

        private static string TryGetPixelType(HImage image)
        {
            if (image == null) return "?";
            try
            {
                HOperatorSet.GetImageType(image, out HTuple ty);
                if (ty != null && ty.Length > 0) return ty.S;
            }
            catch { /* ignore */ }
            return "?";
        }

        private static string DescribeImage(int width, int height, int channels, string pixelType)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "img={0}x{1},ch={2},type={3}", width, height, channels, pixelType);
        }

        // HALCON 17.12 的 measure_pos / edges_sub_pix 要求 singlechannelimage（reference L3781、L51523）。
        // 多通道影像若直接傳入，HALCON 不丟例外但會 silently 回傳 0 個邊緣。
        // 此 helper 偵測到 channels > 1 就轉成單通道；回傳 null 表示「不需轉換、用原圖」，
        // 非 null 表示新建的 HImage 需由呼叫端 dispose。
        private static HImage EnsureSingleChannel(HImage source, int channels, string opName)
        {
            if (channels <= 1) return null;
            if (channels == 3)
            {
                Log(opName + " CHANNEL-CONVERT rgb1_to_gray from ch=3");
                return source.Rgb1ToGray();
            }
            Log(string.Format(CultureInfo.InvariantCulture,
                "{0} CHANNEL-CONVERT access_channel(1) from ch={1}", opName, channels));
            return source.AccessChannel(1);
        }

        private static string DescribeRoi(EdgeDetectionRoi roi)
        {
            if (roi == null) return "roi=null";
            return string.Format(CultureInfo.InvariantCulture,
                "roi(CtrRow={0:F1},CtrCol={1:F1},L1={2:F1},L2={3:F1},Phi={4:F4})",
                roi.CenterRow, roi.CenterCol, roi.Length1, roi.Length2, roi.AngleRad);
        }

        private static string DescribeParams(EdgeDetectionParameters p)
        {
            if (p == null) return "params=null";
            return string.Format(CultureInfo.InvariantCulture,
                "params(sig={0},thr={1},pol={2},sel={3},highThr={4},interp={5},mode={6})",
                p.Sigma, p.Threshold, p.Polarity, p.EdgeSelector, p.HighThreshold,
                p.Interpolation, p.MeasureMode);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // rect2 的 Length1 沿主軸（方向 Phi）、Length2 沿垂直軸（Phi+π/2），不能假設
        // Length1=col、Length2=row——那只在 Phi=0 成立。FromBounds 對橫長 ROI 產生
        // Phi=π/2（軸向對調），若用固定軸 clamp，靠近上下邊界的寬 ROI 會被錯誤軸的
        // 可用空間靜默截斷（例如 1600px 掃描長度被砍到 200px 且無錯誤訊息）。
        // 這裡把各軸半長依 Phi 投影回 row/col 空間計算可用半長。
        private static void ClampLengthsToImage(
            EdgeDetectionRoi roi, double centerRow, double centerCol,
            int imageWidth, int imageHeight,
            out double maxLength1, out double maxLength2)
        {
            double availCol = Math.Min(centerCol, imageWidth - 1.0 - centerCol);
            double availRow = Math.Min(centerRow, imageHeight - 1.0 - centerRow);
            double absCos = Math.Abs(Math.Cos(roi.AngleRad));
            double absSin = Math.Abs(Math.Sin(roi.AngleRad));
            const double eps = 1e-9;

            // 主軸方向 Phi：col 投影 = L1·|cosφ|，row 投影 = L1·|sinφ|
            maxLength1 = roi.Length1;
            if (absCos > eps) maxLength1 = Math.Min(maxLength1, availCol / absCos);
            if (absSin > eps) maxLength1 = Math.Min(maxLength1, availRow / absSin);

            // 垂直軸方向 Phi+π/2：col 投影 = L2·|sinφ|，row 投影 = L2·|cosφ|
            maxLength2 = roi.Length2;
            if (absSin > eps) maxLength2 = Math.Min(maxLength2, availCol / absSin);
            if (absCos > eps) maxLength2 = Math.Min(maxLength2, availRow / absCos);
        }

        private static bool IsOutsideImage(EdgeDetectionRoi roi, int imageWidth, int imageHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return true;
            }

            // 同 ClampLengthsToImage：以 Phi 旋轉後的實際包圍盒判斷出界，
            // 而非假設 Length1=col、Length2=row。
            double absCos = Math.Abs(Math.Cos(roi.AngleRad));
            double absSin = Math.Abs(Math.Sin(roi.AngleRad));
            double colHalf = roi.Length1 * absCos + roi.Length2 * absSin;
            double rowHalf = roi.Length1 * absSin + roi.Length2 * absCos;

            double minRow = roi.CenterRow - rowHalf;
            double maxRow = roi.CenterRow + rowHalf;
            double minCol = roi.CenterCol - colHalf;
            double maxCol = roi.CenterCol + colHalf;

            if (maxRow <= 0.0 || minRow >= imageHeight || maxCol <= 0.0 || minCol >= imageWidth)
            {
                return true;
            }

            return false;
        }
    }
}
