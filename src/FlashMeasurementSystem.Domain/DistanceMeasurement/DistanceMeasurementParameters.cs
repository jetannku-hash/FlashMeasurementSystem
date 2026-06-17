namespace FlashMeasurementSystem.Domain.DistanceMeasurement
{
    public class DistanceMeasurementParameters
    {
        public DistanceMeasurementType Type { get; set; } = DistanceMeasurementType.PointToPoint;
        public double PixelSizeUmX { get; set; } = 10.0;
        public double PixelSizeUmY { get; set; } = 10.0;
        public string ContourMode { get; set; } = "point_to_point";

        public static DistanceMeasurementParameters Default()
        {
            return new DistanceMeasurementParameters();
        }
    }
}
