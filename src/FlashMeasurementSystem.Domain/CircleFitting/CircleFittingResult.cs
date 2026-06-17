namespace FlashMeasurementSystem.Domain.CircleFitting
{
    public class CircleFittingResult
    {
        public bool Success { get; set; }
        public double CenterRow { get; set; }
        public double CenterColumn { get; set; }
        public double RadiusPx { get; set; }
        public double DiameterPx { get; set; }
        public double StartPhi { get; set; }
        public double EndPhi { get; set; }
        public string PointOrder { get; set; } = string.Empty;
        public double ResidualRms { get; set; }
        public double Roundness { get; set; }
        public int UsedPoints { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
