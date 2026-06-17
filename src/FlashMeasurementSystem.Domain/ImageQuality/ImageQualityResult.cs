namespace FlashMeasurementSystem.Domain.ImageQuality
{
    public class ImageQualityResult
    {
        public bool Pass { get; set; }
        public double MeanBrightness { get; set; }
        public double SaturationRatio { get; set; }
        public double BlurScore { get; set; }
        public double Contrast { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
