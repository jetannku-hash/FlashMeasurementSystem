namespace FlashMeasurementSystem.Domain.EdgeDetection
{
    /// <summary>
    /// 邊緣偵測參數。欄位定義對應 HALCON 17.12 measure_pos / edges_sub_pix 的輸入：
    /// Sigma/Threshold/Polarity/EdgeSelector 給 measure_pos；HighThreshold 給 canny 的高門檻。
    /// 演算法本身由呼叫端決定（DetectEdges vs DetectEdgesSubPix），不在這個物件裡。
    /// </summary>
    public class EdgeDetectionParameters
    {
        public double Sigma { get; set; } = 1.2;
        public double Threshold { get; set; } = 25.0;
        public string Polarity { get; set; } = "all";
        public string EdgeSelector { get; set; } = "all";
        public double HighThreshold { get; set; } = 40.0;
        public string Interpolation { get; set; } = "nearest_neighbor";
        public string MeasureMode { get; set; } = "single_edge";

        // B1 fuzzy 邊緣量測（HALCON fuzzy_measure_pos）。預設關閉＝既有 measure_pos 行為不變。
        // FuzzyThresh 為模糊分數門檻 [0,1]：分數低於此值的邊被濾除，抗雜訊/干擾邊。
        public bool FuzzyEnabled { get; set; } = false;
        public double FuzzyThresh { get; set; } = 0.5;

        public static bool IsValidFuzzyThresh(double value)
        {
            return value >= 0.0 && value <= 1.0;
        }

        public static bool IsSupportedInterpolation(string value)
        {
            return value == "nearest_neighbor" || value == "bilinear" || value == "bicubic";
        }

        public static bool IsSupportedMeasureMode(string value)
        {
            return value == "single_edge" || value == "edge_pair";
        }


        public static EdgeDetectionParameters Default()
        {
            return new EdgeDetectionParameters();
        }
    }
}
