using FlashMeasurementSystem.Domain.ImageQuality;

namespace FlashMeasurementSystem.Application.ImageQuality
{
    public interface IImageQualityChecker<TImage>
    {
        ImageQualityResult Check(TImage image, ImageQualityThresholds thresholds);
    }
}
