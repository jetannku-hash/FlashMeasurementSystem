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
            try
            {
                HOperatorSet.ReduceDomain(image, templateRegion, out reduced);

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
                reduced?.Dispose();
                // WriteShapeModel / CreateDirectory 等若擲例外，modelID 仍持有非託管 shape model
                // 句柄，須在此釋放避免洩漏（成功路徑已設為 null）。
                bool leakPrevented = modelID != null;
                if (leakPrevented)
                    HOperatorSet.ClearShapeModel(modelID);
            }
        }
    }
}
