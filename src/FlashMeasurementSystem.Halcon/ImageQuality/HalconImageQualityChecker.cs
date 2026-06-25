using System.Collections.Generic;
using FlashMeasurementSystem.Application.ImageQuality;
using FlashMeasurementSystem.Domain.ImageQuality;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.ImageQuality
{
    public class HalconImageQualityChecker : IImageQualityChecker<HImage>
    {
        public ImageQualityResult Check(HImage image, ImageQualityThresholds thresholds)
        {
            var result = new ImageQualityResult();
            var effectiveThresholds = thresholds ?? ImageQualityThresholds.Default();

            try
            {
                // intensity / threshold / get_image_pointer1 都要求單通道影像（HALCON 17.12
                // reference），RGB 影像直接傳入會拋 HalconException、整個品質檢查失敗。
                // 與 HalconEdgeDetector 相同慣例：先轉單通道再量測。
                HImage convertedImage = EnsureSingleChannel(image);
                HImage workingImage = convertedImage ?? image;

                try
                {
                    using (HRegion domain = workingImage.GetDomain())
                    {
                        HOperatorSet.Intensity(domain, workingImage, out HTuple meanBrightness, out HTuple _);
                        result.MeanBrightness = meanBrightness.D;

                        using (HRegion saturatedRegion = workingImage.Threshold(254.0, 255.0))
                        {
                            HOperatorSet.AreaCenter(saturatedRegion, out HTuple satArea, out HTuple _, out HTuple _);
                            HOperatorSet.GetImagePointer1(workingImage, out HTuple _, out HTuple _, out HTuple width, out HTuple height);

                            double totalPixels = width.D * height.D;
                            result.SaturationRatio = totalPixels <= 0.0 ? 0.0 : (satArea.D / totalPixels) * 100.0;
                        }

                        // 對焦度量：取絕對值 Laplace 響應的標準差（越高越銳利）。
                        // 已知限制：Laplace("absolute") 對 byte 影像會把高頻響應 clamp 到 0..255，
                        // 在亮/高對比影像上 deviation 被壓縮、略低估銳利度。改用非 clamp 型別
                        // （signed/int16 或 derivate_gauss）可更穩定，但會改變 BlurScore 的數值尺度，
                        // 需對真實影像重新校準 MinBlurScore 門檻——故此處先保留現行尺度，待有真實
                        // 影像樣本時再一併調整門檻。
                        using (HImage laplace = workingImage.Laplace("absolute", 3, "n_4"))
                        {
                            HOperatorSet.Intensity(domain, laplace, out HTuple _, out HTuple blurDeviation);
                            result.BlurScore = blurDeviation.D;
                        }

                        HOperatorSet.Intensity(domain, workingImage, out HTuple _, out HTuple deviation);
                        result.Contrast = deviation.D;
                    }
                }
                finally
                {
                    convertedImage?.Dispose();
                }

                var failures = new List<string>();

                if (result.MeanBrightness < effectiveThresholds.MinBrightness)
                {
                    failures.Add($"過暗 (mean={result.MeanBrightness:F1} < {effectiveThresholds.MinBrightness})");
                }
                else if (result.MeanBrightness > effectiveThresholds.MaxBrightness)
                {
                    failures.Add($"過亮 (mean={result.MeanBrightness:F1} > {effectiveThresholds.MaxBrightness})");
                }

                if (result.SaturationRatio > effectiveThresholds.MaxSaturationRatio)
                {
                    failures.Add($"飽和過高 ({result.SaturationRatio:F2}% > {effectiveThresholds.MaxSaturationRatio}%)");
                }

                if (result.BlurScore < effectiveThresholds.MinBlurScore)
                {
                    failures.Add($"模糊 (blur score={result.BlurScore:F1} < {effectiveThresholds.MinBlurScore})");
                }

                if (result.Contrast < effectiveThresholds.MinContrast)
                {
                    failures.Add($"對比不足 (contrast={result.Contrast:F1} < {effectiveThresholds.MinContrast})");
                }

                result.Pass = failures.Count == 0;
                result.Message = result.Pass ? "影像品質合格" : string.Join("; ", failures);
            }
            catch (HalconException ex)
            {
                result.Pass = false;
                result.Message = $"影像品質檢查異常：{ex.Message}";
            }

            return result;
        }

        // 回傳 null 表示原圖已是單通道（直接用原圖）；非 null 為新建的單通道影像，
        // 由呼叫端負責 dispose。3 通道用 rgb1_to_gray（加權灰階），其他取第 1 通道。
        private static HImage EnsureSingleChannel(HImage source)
        {
            HOperatorSet.CountChannels(source, out HTuple channels);
            int channelCount = (channels != null && channels.Length > 0) ? channels.I : 1;
            if (channelCount <= 1) return null;
            return channelCount == 3 ? source.Rgb1ToGray() : source.AccessChannel(1);
        }
    }
}
