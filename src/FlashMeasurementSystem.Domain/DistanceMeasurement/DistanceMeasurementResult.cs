namespace FlashMeasurementSystem.Domain.DistanceMeasurement
{
    public class DistanceMeasurementResult
    {
        public bool Success { get; set; }
        public double DistancePx { get; set; }
        public double DistanceMm { get; set; }
        public double DistanceMinPx { get; set; }
        public double DistanceMaxPx { get; set; }
        public double DistanceMinMm { get; set; }
        public double DistanceMaxMm { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
