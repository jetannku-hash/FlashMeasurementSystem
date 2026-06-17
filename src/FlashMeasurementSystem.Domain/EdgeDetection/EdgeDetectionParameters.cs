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
