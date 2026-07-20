using System;
using System.Globalization;
using FlashMeasurementSystem.Application.HoleArrayDetection;
using FlashMeasurementSystem.Domain.HoleArrayAnalysis;
using FlashMeasurementSystem.Domain.HoleArrayDetection;
using FlashMeasurementSystem.Domain.Roi;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.HoleArrayDetection
{
    /// <summary>
    /// 在 rect2 ROI 內以 blob 偵測孔洞：gen_rectangle2 → reduce_domain →
    /// binary_threshold（依 HoleIsDark 取暗/亮）→ connection → select_shape（面積濾雜訊）→ area_center。
    /// 回傳孔質心（像素）與等效孔徑 DiameterPx = 2*sqrt(area/π)（實心圓 area=πr² → 得 2r）。
    /// 不排序、不分析（網格排序/孔距判定為 Domain 分析器職責）。
    /// </summary>
    public sealed class HalconHoleArrayDetector : IHoleArrayDetector<HImage>
    {
        public HoleArrayDetectionResult DetectHolesInRect(HImage image, RoiGeometry placedRoi, HoleArrayAnalysisParameters parameters)
        {
            var result = new HoleArrayDetectionResult();
            if (image == null) { result.ErrorMessage = "影像為空"; return result; }
            if (placedRoi == null) { result.ErrorMessage = "量測 ROI 無效"; return result; }
            if (parameters == null) parameters = HoleArrayAnalysisParameters.Default();

            HObject region = null, reduced = null, blobs = null, connected = null, selected = null;
            HImage convertedImage = null;
            try
            {
                convertedImage = EnsureSingleChannel(image);
                HImage workingImage = convertedImage ?? image;

                HOperatorSet.GenRectangle2(out region, placedRoi.CenterRow, placedRoi.CenterCol,
                    placedRoi.AngleRad, placedRoi.Length1, placedRoi.Length2);
                HOperatorSet.ReduceDomain(workingImage, region, out reduced);
                HOperatorSet.BinaryThreshold(reduced, out blobs, "max_separability",
                    parameters.HoleIsDark ? "dark" : "light", out HTuple _used);
                HOperatorSet.Connection(blobs, out connected);
                HOperatorSet.SelectShape(connected, out selected, "area", "and",
                    new HTuple(parameters.MinHoleAreaPx), new HTuple(1e12));
                HOperatorSet.AreaCenter(selected, out HTuple areas, out HTuple rows, out HTuple cols);

                int cnt = rows?.Length ?? 0;
                for (int i = 0; i < cnt; i++)
                {
                    double area = areas[i].D;
                    result.Holes.Add(new HoleArrayPoint
                    {
                        Row = rows[i].D,
                        Col = cols[i].D,
                        DiameterPx = 2.0 * Math.Sqrt(area / Math.PI)   // 實心圓 area=πr² → 2r
                    });
                }
                result.Success = cnt > 0;
                if (!result.Success)
                    result.ErrorMessage = string.Format(CultureInfo.InvariantCulture,
                        "ROI 內未偵測到孔洞（HoleIsDark={0}、MinArea={1:F0}；請調 ROI/極性/面積）",
                        parameters.HoleIsDark, parameters.MinHoleAreaPx);
            }
            catch (HalconException ex)
            {
                result.Success = false;
                result.ErrorMessage = "孔洞偵測異常 [" + ex.GetErrorCode() + "]: " + ex.Message;
            }
            finally
            {
                convertedImage?.Dispose();
                region?.Dispose(); reduced?.Dispose(); blobs?.Dispose();
                connected?.Dispose(); selected?.Dispose();
            }
            return result;
        }

        // 回傳 null 表示原圖已是單通道（直接用原圖）；非 null 為新建的單通道影像，由呼叫端 dispose。
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
