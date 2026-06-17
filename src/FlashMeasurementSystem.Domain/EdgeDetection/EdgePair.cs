namespace FlashMeasurementSystem.Domain.EdgeDetection
{
    public class EdgePair
    {
        public double FirstRow { get; set; }
        public double FirstColumn { get; set; }
        public double FirstAmplitude { get; set; }
        public double SecondRow { get; set; }
        public double SecondColumn { get; set; }
        public double SecondAmplitude { get; set; }
        public double IntraDistance { get; set; }
        public double InterDistance { get; set; }
    }
}
