using System;
using System.IO;
using FlashMeasurementSystem.Application.TemplateMatching;
using FlashMeasurementSystem.Domain.TemplateMatching;
using HalconDotNet;

namespace FlashMeasurementSystem.Halcon.TemplateMatching
{
    public class HalconTemplateManager : ITemplateManager<HImage, HRegion>
    {
        public string CreateAndSave(HImage image, HRegion templateRegion, string modelFilePath, TemplateCreationParameters parameters)
        {
            var effectiveParams = parameters ?? TemplateCreationParameters.Default();

            HObject reduced = null;
            HTuple modelID = null;
            HImage converted = null;
            try
            {
                // create_shape_model 要求單通道影像；彩色（多通道）圖直接傳入會拋 HalconException、
                // 模板永遠建不起來。與 HalconEdgeDetector / ImageQualityChecker 相同慣例：先轉單通道。
                converted = EnsureSingleChannel(image);
                HImage working = converted ?? image;
                HOperatorSet.ReduceDomain(working, templateRegion, out reduced);

                HOperatorSet.CreateShapeModel(
                    reduced,
                    new HTuple(effectiveParams.PyramidLevel),
                    new HTuple(effectiveParams.AngleStart * Math.PI / 180.0),
                    new HTuple(effectiveParams.AngleExtent * Math.PI / 180.0),
                    new HTuple("auto"),
                    new HTuple("none"),
                    new HTuple(effectiveParams.Metric),
                    new HTuple("auto"),
                    new HTuple(effectiveParams.MinContrast),
                    out modelID
                );

                string dir = Path.GetDirectoryName(modelFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                HOperatorSet.WriteShapeModel(modelID, modelFilePath);
                HOperatorSet.ClearShapeModel(modelID);
                modelID = null; // 已釋放，避免 finally 重複 clear

                return modelFilePath;
            }
            catch (HalconException ex)
            {
                throw new InvalidOperationException($"模板建立失敗：{ex.Message}", ex);
            }
            finally
            {
                converted?.Dispose();  // 僅在多通道轉換時新建，需釋放（單通道路徑為 null）
                reduced?.Dispose();
                // WriteShapeModel / CreateDirectory 等若擲例外，modelID 仍持有非託管 shape model
                // 句柄，須在此釋放避免洩漏（成功路徑已設為 null）。
                bool leakPrevented = modelID != null;
                if (leakPrevented)
                    HOperatorSet.ClearShapeModel(modelID);
            }
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
