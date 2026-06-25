namespace FlashMeasurementSystem.Domain.RectangleFitting
{
    public class RectangleFittingParameters
    {
        // 預設 tukey：推薦的抗 outlier 演算法（reference L175966）。
        // 合法值只有 regression / huber / tukey 三種（fit_rectangle2_contour_xld reference L175948-175953）。
        public string Algorithm { get; set; } = "tukey";
        public int MaxNumPoints { get; set; } = -1;
        public double MaxClosureDist { get; set; } = 0.0;
        public int ClippingEndPoints { get; set; } = 0;
        public int Iterations { get; set; } = 3;
        public double ClippingFactor { get; set; } = 2.0;

        // fit_rectangle2_contour_xld 最低需 8 點（reference L175983）。
        public int MinPoints { get; set; } = 8;

        public static RectangleFittingParameters Default()
        {
            return new RectangleFittingParameters();
        }

        public static bool IsSupportedAlgorithm(string algorithm)
        {
            return algorithm == "regression"
                || algorithm == "huber"
                || algorithm == "tukey";
        }
    }
}
