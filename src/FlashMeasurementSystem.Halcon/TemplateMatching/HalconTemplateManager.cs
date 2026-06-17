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

                return modelFilePath;
            }
            catch (HalconException ex)
            {
                throw new InvalidOperationException($"模板建立失敗：{ex.Message}", ex);
            }
            finally
            {
                reduced?.Dispose();
            }
        }
    }
}
