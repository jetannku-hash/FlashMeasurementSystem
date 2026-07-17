using System.Globalization;
using FlashMeasurementSystem.Application.HoleDetection;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.HoleDetection;
using FlashMeasurementSystem.Domain.PcdAnalysis;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.HoleDetection
{
    /// <summary>
    /// 在 ArcRoi 環帶內以 blob 偵測孔：環狀 region reduce_domain → binary_threshold（依 HoleIsDark 取暗/亮）
    /// → connection → select_shape（面積濾雜訊）→ area_center。回傳孔質心（像素）。
    /// v1 假設整圈螺栓圈，直接用整個環（不裁角度扇形）。
    /// </summary>
    public sealed class HalconHoleDetector : IHoleDetector<HImage>
    {
        public HoleDetectionResult DetectHolesInAnnulus(HImage image, ArcMeasureRoi a, PcdAnalysisParameters p)
        {
            var result = new HoleDetectionResult();
            if (image == null) { result.ErrorMessage = "影像為空"; return result; }
            if (a == null || !a.IsDefined) { result.ErrorMessage = "量測環帶無效"; return result; }
            if (p == null) p = PcdAnalysisParameters.Default();

            HObject outer = null, inner = null, ring = null, reduced = null,
                    region = null, connected = null, selected = null;
            HImage convertedImage = null;
            try
            {
                convertedImage = EnsureSingleChannel(image);
                HImage workingImage = convertedImage ?? image;

                double rOut = a.Radius + a.AnnulusRadius, rIn = a.Radius - a.AnnulusRadius;
                if (rIn < 0) rIn = 0;
                HOperatorSet.GenCircle(out outer, a.CenterRow, a.CenterCol, rOut);
                HOperatorSet.GenCircle(out inner, a.CenterRow, a.CenterCol, rIn);
                HOperatorSet.Difference(outer, inner, out ring);
                HOperatorSet.ReduceDomain(workingImage, ring, out reduced);
                HOperatorSet.BinaryThreshold(reduced, out region, "max_separability",
                    p.HoleIsDark ? "dark" : "light", out HTuple _used);
                HOperatorSet.Connection(region, out connected);
                HOperatorSet.SelectShape(connected, out selected, "area", "and",
                    new HTuple(p.MinHoleAreaPx), new HTuple(1e12));
                HOperatorSet.AreaCenter(selected, out HTuple area, out HTuple rows, out HTuple cols);

                int cnt = rows?.Length ?? 0;
                for (int i = 0; i < cnt; i++)
                    result.Holes.Add(new HolePoint { Row = rows[i].D, Col = cols[i].D });
                result.Success = cnt > 0;
                if (!result.Success)
                    result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                        "環帶內未偵測到孔（半徑 {0:F0}±{1:F0}、HoleIsDark={2}、MinArea={3:F0}；請調環帶/極性/面積）",
                        a.Radius, a.AnnulusRadius, p.HoleIsDark, p.MinHoleAreaPx);
            }
            catch (HalconException ex)
            {
                result.Success = false;
                result.ErrorMessage = "孔偵測異常 [" + ex.GetErrorCode() + "]: " + ex.Message;
            }
            finally
            {
                convertedImage?.Dispose();
                outer?.Dispose(); inner?.Dispose(); ring?.Dispose();
                reduced?.Dispose(); region?.Dispose(); connected?.Dispose(); selected?.Dispose();
            }
            return result;
        }

        // 回傳 null 表示原圖已是單通道（直接用原圖）；非 null 為新建的單通道影像，由呼叫端 dispose。
        // 3 通道用 rgb1_to_gray（加權灰階），其他取第 1 通道。與其他 HALCON adapter 相同慣例
        // （見 HalconEdgeDetector / HalconImageQualityChecker / HalconMetrologyModelRunner）。
        private static HImage EnsureSingleChannel(HImage source)
        {
            HOperatorSet.CountChannels(source, out HTuple channels);
            int channelCount = (channels != null && channels.Length > 0) ? channels.I : 1;
            if (channelCount <= 1) return null;
            return channelCount == 3 ? source.Rgb1ToGray() : source.AccessChannel(1);
        }
    }
}
