namespace FlashMeasurementSystem.Domain.EllipseFitting
{
    public class EllipseFittingParameters
    {
        // 預設值寫在屬性 initializer 上（而非只在 Default() 裡），確保 new 出來的
        // 物件天生合法：fit_ellipse_contour_xld 要求 ClippingFactor > 0、Algorithm 不可為 null。
        // 預設 geotukey：與 CircleFittingParameters 一致、抗 outlier。
        public string Algorithm { get; set; } = "geotukey";
        public int MaxNumPoints { get; set; } = -1;
        public double MaxClosureDist { get; set; } = 0.0;
        public int ClippingEndPoints { get; set; } = 0;

        // VossTabSize 僅 'voss' 演算法使用（標準圓弧段查表大小）；其餘演算法忽略此值，
        // 但 fit_ellipse_contour_xld 仍要求傳入合法正整數，故給預設 100。
        public int VossTabSize { get; set; } = 100;

        public int Iterations { get; set; } = 3;
        public double ClippingFactor { get; set; } = 2.0;

        // fit_ellipse_contour_xld 的最低點數為 5（reference L175690：至少 5 + 2*ClippingEndPoints）。
        public int MinPoints { get; set; } = 5;

        public static EllipseFittingParameters Default()
        {
            return new EllipseFittingParameters();
        }

        public static bool IsSupportedAlgorithm(string algorithm)
        {
            // fit_ellipse_contour_xld 合法演算法（reference L175618-175658）。
            return algorithm == "fitzgibbon"
                || algorithm == "fhuber"
                || algorithm == "ftukey"
                || algorithm == "geometric"
                || algorithm == "geohuber"
                || algorithm == "geotukey"
                || algorithm == "voss"
                || algorithm == "focpoints"
                || algorithm == "fphuber"
                || algorithm == "fptukey";
        }
    }
}
