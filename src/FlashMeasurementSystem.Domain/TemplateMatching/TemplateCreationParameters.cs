namespace FlashMeasurementSystem.Domain.TemplateMatching
{
    public class TemplateCreationParameters
    {
        public double AngleStart { get; set; } = -180.0;
        public double AngleExtent { get; set; } = 360.0;
        public int PyramidLevel { get; set; } = 3;
        public string Metric { get; set; } = "use_polarity";
        public int MinContrast { get; set; } = 10;

        public static TemplateCreationParameters Default()
        {
            return new TemplateCreationParameters();
        }
    }
}
