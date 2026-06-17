namespace FlashMeasurementSystem.Domain.CircleFitting
{
    public class CircleFittingParameters
    {
        // 預設值寫在屬性 initializer 上（而非只在 Default() 裡），確保 new 出來的
        // 物件天生合法：fit_circle_contour_xld 要求 ClippingFactor > 0，Algorithm 不可為 null。
        public string Algorithm { get; set; } = "geotukey";
        public int MaxNumPoints { get; set; } = -1;
        public double MaxClosureDist { get; set; } = 0.0;
        public int ClippingEndPoints { get; set; } = 0;
        public int Iterations { get; set; } = 3;
        public double ClippingFactor { get; set; } = 2.0;
        public int MinPoints { get; set; } = 3;

        public static CircleFittingParameters Default()
        {
            return new CircleFittingParameters();
        }

        public static bool IsSupportedAlgorithm(string algorithm)
        {
            return algorithm == "algebraic"
                || algorithm == "ahuber"
                || algorithm == "atukey"
                || algorithm == "geometric"
                || algorithm == "geohuber"
                || algorithm == "geotukey";
        }
    }
}
