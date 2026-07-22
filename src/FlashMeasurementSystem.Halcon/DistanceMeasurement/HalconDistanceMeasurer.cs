using System;
using System.Globalization;
using System.IO;
using FlashMeasurementSystem.Application.DistanceMeasurement;
using FlashMeasurementSystem.Domain.DistanceMeasurement;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.DistanceMeasurement
{
    public class HalconDistanceMeasurer : IDistanceMeasurer
    {
        // 物理單位換算策略（修正非等向像素 pixelSizeX != pixelSizeY 的錯誤）：
        // 不再用「方向向量加權混合 pixel size」（那對 point-to-line / line-to-line / contour
        // 的距離方向假設是錯的）。改為「先把座標縮放到物理空間 (um)，再讓 HALCON 在物理空間
        // 算距離」——row(垂直/Y) 乘 PixelSizeUmY、col(水平/X) 乘 PixelSizeUmX。這樣不論距離
        // 朝哪個方向，operator 算出的就是正確的物理 um；除以 1000 得 mm。
        // 像素距離 (px) 仍用原始座標另算一次，供使用者對照。

        public DistanceMeasurementResult MeasurePointToPoint(
            double row1, double col1,
            double row2, double col2,
            DistanceMeasurementParameters parameters)
        {
            var result = new DistanceMeasurementResult();
            var p = parameters ?? DistanceMeasurementParameters.Default();

            try
            {
                HOperatorSet.DistancePp(row1, col1, row2, col2, out HTuple distPx);
                HOperatorSet.DistancePp(
                    row1 * p.PixelSizeUmY, col1 * p.PixelSizeUmX,
                    row2 * p.PixelSizeUmY, col2 * p.PixelSizeUmX,
                    out HTuple distUm);

                result.DistancePx = distPx.D;
                result.DistanceMm = distUm.D / 1000.0;
                result.Success = true;
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "Point-to-Point distance failed: " + ex.Message;
            }

            Log("PointToPoint", p, result,
                string.Format(CultureInfo.InvariantCulture, "p1=({0:F2},{1:F2}) p2=({2:F2},{3:F2})", row1, col1, row2, col2));
            return result;
        }

        public DistanceMeasurementResult MeasurePointToLine(
            double pointRow, double pointCol,
            double lineRow1, double lineCol1,
            double lineRow2, double lineCol2,
            DistanceMeasurementParameters parameters)
        {
            var result = new DistanceMeasurementResult();
            var p = parameters ?? DistanceMeasurementParameters.Default();

            try
            {
                HOperatorSet.DistancePl(
                    pointRow, pointCol, lineRow1, lineCol1, lineRow2, lineCol2,
                    out HTuple distPx);
                HOperatorSet.DistancePl(
                    pointRow * p.PixelSizeUmY, pointCol * p.PixelSizeUmX,
                    lineRow1 * p.PixelSizeUmY, lineCol1 * p.PixelSizeUmX,
                    lineRow2 * p.PixelSizeUmY, lineCol2 * p.PixelSizeUmX,
                    out HTuple distUm);

                result.DistancePx = distPx.D;
                result.DistanceMm = distUm.D / 1000.0;
                result.Success = true;
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "Point-to-Line distance failed: " + ex.Message;
            }

            Log("PointToLine", p, result,
                string.Format(CultureInfo.InvariantCulture,
                    "pt=({0:F2},{1:F2}) line=({2:F2},{3:F2})-({4:F2},{5:F2})",
                    pointRow, pointCol, lineRow1, lineCol1, lineRow2, lineCol2));
            return result;
        }

        public DistanceMeasurementResult MeasureLineToLine(
            double line1Row1, double line1Col1,
            double line1Row2, double line1Col2,
            double line2Row1, double line2Col1,
            double line2Row2, double line2Col2,
            DistanceMeasurementParameters parameters)
        {
            var result = new DistanceMeasurementResult();
            var p = parameters ?? DistanceMeasurementParameters.Default();

            try
            {
                // distance_ss = 兩條「線段」之間的對稱 min/max 距離（reference L156538）。
                // 先前用的 distance_sl 是「線段 A 對無限長直線 B」，語意不對稱，不符 LineToLine。
                HOperatorSet.DistanceSs(
                    line1Row1, line1Col1, line1Row2, line1Col2,
                    line2Row1, line2Col1, line2Row2, line2Col2,
                    out HTuple minPx, out HTuple maxPx);
                HOperatorSet.DistanceSs(
                    line1Row1 * p.PixelSizeUmY, line1Col1 * p.PixelSizeUmX,
                    line1Row2 * p.PixelSizeUmY, line1Col2 * p.PixelSizeUmX,
                    line2Row1 * p.PixelSizeUmY, line2Col1 * p.PixelSizeUmX,
                    line2Row2 * p.PixelSizeUmY, line2Col2 * p.PixelSizeUmX,
                    out HTuple minUm, out HTuple maxUm);

                result.DistanceMinPx = minPx.D;
                result.DistanceMaxPx = maxPx.D;
                result.DistanceMinMm = minUm.D / 1000.0;
                result.DistanceMaxMm = maxUm.D / 1000.0;

                // 代表「線距」用 min（兩線段的最近距離）。對量平行邊間距，min 即垂直間距；
                // max 是端點對端點的最遠斜距，對「兩線距離」沒有意義，先前用 (min+max)/2
                // 會把間距灌水（例：平行間距 300px 卻顯示 362px）。
                result.DistancePx = minPx.D;
                result.DistanceMm = result.DistanceMinMm;
                result.Success = true;
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "Line-to-Line distance failed: " + ex.Message;
            }

            Log("LineToLine", p, result,
                string.Format(CultureInfo.InvariantCulture,
                    "L1=({0:F2},{1:F2})-({2:F2},{3:F2}) L2=({4:F2},{5:F2})-({6:F2},{7:F2})",
                    line1Row1, line1Col1, line1Row2, line1Col2,
                    line2Row1, line2Col1, line2Row2, line2Col2));
            return result;
        }

        public DistanceMeasurementResult MeasureCircleToCircle(
            double circle1Row, double circle1Col,
            double circle2Row, double circle2Col,
            DistanceMeasurementParameters parameters)
        {
            var result = new DistanceMeasurementResult();
            var p = parameters ?? DistanceMeasurementParameters.Default();

            try
            {
                HOperatorSet.DistancePp(circle1Row, circle1Col, circle2Row, circle2Col, out HTuple distPx);
                HOperatorSet.DistancePp(
                    circle1Row * p.PixelSizeUmY, circle1Col * p.PixelSizeUmX,
                    circle2Row * p.PixelSizeUmY, circle2Col * p.PixelSizeUmX,
                    out HTuple distUm);

                result.DistancePx = distPx.D;
                result.DistanceMm = distUm.D / 1000.0;
                result.Success = true;
            }
            catch (HalconException ex)
            {
                result.ErrorMessage = "Circle-to-Circle distance failed: " + ex.Message;
            }

            Log("CircleToCircle", p, result,
                string.Format(CultureInfo.InvariantCulture, "c1=({0:F2},{1:F2}) c2=({2:F2},{3:F2})",
                    circle1Row, circle1Col, circle2Row, circle2Col));
            return result;
        }

        // ─── 診斷 log（檔案 + Debug.WriteLine），與 edge detection 同模式 ───────────
        private static readonly object _logLock = new object();
        private static string _cachedLogPath;
        private const long MaxLogBytes = 5L * 1024 * 1024;

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
                        _cachedLogPath = Path.Combine(logsDir, "distance_measurement.log");
                        return _cachedLogPath;
                    }
                    current = current.Parent;
                }
            }
            catch
            {
                // 退回 TEMP
            }
            _cachedLogPath = Path.Combine(Path.GetTempPath(), "FlashMeasurementSystem_distance_measurement.log");
            return _cachedLogPath;
        }

        private static void Log(string type, DistanceMeasurementParameters p, DistanceMeasurementResult r, string inputs)
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string line = string.Format(CultureInfo.InvariantCulture,
                "[{0}] {1} pxSize=({2:F3},{3:F3})um {4} -> success={5} px={6:F3} mm={7:F4} min/max(mm)={8:F4}/{9:F4}{10}",
                stamp, type, p.PixelSizeUmX, p.PixelSizeUmY, inputs,
                r.Success, r.DistancePx, r.DistanceMm, r.DistanceMinMm, r.DistanceMaxMm,
                string.IsNullOrEmpty(r.ErrorMessage) ? "" : " err=" + r.ErrorMessage);

            System.Diagnostics.Debug.WriteLine(line);
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
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch
            {
                // log 不可拋例外
            }
        }
    }
}
