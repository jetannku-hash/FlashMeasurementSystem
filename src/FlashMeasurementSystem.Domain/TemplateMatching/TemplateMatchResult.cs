namespace FlashMeasurementSystem.Domain.TemplateMatching
{
    public class TemplateMatchResult
    {
        public bool Found { get; set; }
        public double Row { get; set; }
        public double Column { get; set; }
        public double AngleDeg { get; set; }
        public double Score { get; set; }
        public double ScaleX { get; set; } = 1.0;
        public string Message { get; set; } = string.Empty;
    }
}
