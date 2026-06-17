namespace FlashMeasurementSystem.Domain.TemplateMatching
{
    public class TemplateMatchingParameters
    {
        public double MinScore { get; set; } = 0.75;
        public int NumMatches { get; set; } = 1;
        public double MaxOverlap { get; set; } = 0.5;
        public double AngleStart { get; set; } = -10.0;
        public double AngleExtent { get; set; } = 20.0;

        public static TemplateMatchingParameters Default()
        {
            return new TemplateMatchingParameters();
        }
    }
}
