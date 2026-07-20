namespace FlashMeasurementSystem.Domain.HoleArrayAnalysis
{
    /// <summary>單一孔的質心與等效孔徑（像素座標）。純 DTO。</summary>
    public class HoleArrayPoint
    {
        public double Row        { get; set; }
        public double Col        { get; set; }
        public double DiameterPx { get; set; }
    }
}
