using FlashMeasurementSystem.Domain.TemplateMatching;

namespace FlashMeasurementSystem.Application.TemplateMatching
{
    public interface ITemplateMatcher<TImage, TRegion>
    {
        void LoadModel(string modelFilePath);
        TemplateMatchResult FindMatches(TImage image, TRegion searchRegion, TemplateMatchingParameters parameters);
    }
}
