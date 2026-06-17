namespace FlashMeasurementSystem.Domain.ImageQuality
{
    public class ImageQualityThresholds
    {
        public double MinBrightness { get; set; } = 80.0;
        public double MaxBrightness { get; set; } = 180.0;
        public double MaxSaturationRatio { get; set; } = 1.0;
        public double MinBlurScore { get; set; } = 100.0;
        public double MinContrast { get; set; } = 20.0;

        public static ImageQualityThresholds Default()
        {
            return new ImageQualityThresholds();
        }
    }
}
