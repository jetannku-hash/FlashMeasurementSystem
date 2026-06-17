using FlashMeasurementSystem.Domain.TemplateMatching;

namespace FlashMeasurementSystem.Application.TemplateMatching
{
    public interface ITemplateManager<TImage, TRegion>
    {
        string CreateAndSave(TImage image, TRegion templateRegion, string modelFilePath, TemplateCreationParameters parameters);
    }
}
