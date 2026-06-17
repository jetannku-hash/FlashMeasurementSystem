namespace FlashMeasurementSystem.Domain.LineFitting
{
    public class LineFittingParameters
    {
        // 預設值寫在屬性 initializer 上（而非只在 Default() 裡），確保 new 出來的
        // 物件天生合法：fit_line_contour_xld 要求 ClippingFactor > 0，Algorithm 不可為 null。
        public string Algorithm { get; set; } = "tukey";
        public int MaxNumPoints { get; set; } = -1;
        public int ClippingEndPoints { get; set; } = 0;
        public double ClippingFactor { get; set; } = 2.0;
        public int Iterations { get; set; } = 3;
        public int MinPoints { get; set; } = 2;

        public static LineFittingParameters Default()
        {
            return new LineFittingParameters();
        }

        public static bool IsSupportedAlgorithm(string algorithm)
        {
            return algorithm == "regression"
                || algorithm == "gauss"
                || algorithm == "huber"
                || algorithm == "tukey"
                || algorithm == "drop";
        }
    }
}
