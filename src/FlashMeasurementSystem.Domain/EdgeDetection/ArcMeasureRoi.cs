using System;

namespace FlashMeasurementSystem.Domain.EdgeDetection
{
    /// <summary>
    /// 弧形量測 ROI（gen_measure_arc）：沿環形弧佈設量測線、抓垂直於弧的邊。
    /// AngleStart/AngleExtent 為弧度；AngleExtent > 0 逆時針、< 0 順時針。
    /// AnnulusRadius 為環寬一半。
    /// </summary>
    public class ArcMeasureRoi
    {
        public double CenterRow { get; set; }
        public double CenterCol { get; set; }
        public double Radius { get; set; }
        public double AngleStart { get; set; }
        public double AngleExtent { get; set; }
        public double AnnulusRadius { get; set; }

        public bool IsDefined
        {
            get
            {
                return Radius > 1.0
                    && AnnulusRadius > 0.5
                    && Math.Abs(AngleExtent) > 1e-9;
            }
        }

        public string ValidationError
        {
            get
            {
                if (Radius <= 1.0) return "半徑必須 > 1px";
                if (AnnulusRadius <= 0.5) return "環寬一半必須 > 0.5px";
                if (Math.Abs(AngleExtent) <= 1e-9) return "角度範圍不可為 0";
                return null;
            }
        }
    }
}
