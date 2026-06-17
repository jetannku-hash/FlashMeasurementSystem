namespace FlashMeasurementSystem.Domain.LineFitting
{
    public class LineFittingResult
    {
        public bool Success { get; set; }
        public double Row1 { get; set; }
        public double Column1 { get; set; }
        public double Row2 { get; set; }
        public double Column2 { get; set; }
        public double AngleDeg { get; set; }
        public double Length { get; set; }
        public double ResidualRms { get; set; }
        public int UsedPoints { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
