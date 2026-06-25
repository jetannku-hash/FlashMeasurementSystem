using System;

namespace FlashMeasurementSystem.Domain.CircleFitting
{
    public class CircleFittingResult
    {
        public bool Success { get; set; }
        public double CenterRow { get; set; }
        public double CenterColumn { get; set; }
        public double RadiusPx { get; set; }
        public double DiameterPx { get; set; }
        public double StartPhi { get; set; }
        public double EndPhi { get; set; }
        public string PointOrder { get; set; } = string.Empty;
        public double ResidualRms { get; set; }
        public double Roundness { get; set; }
        public int UsedPoints { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        // 閉合橢圓時 HALCON 回 StartPhi=0、EndPhi=2*pi（reference L175616）。
        // 弧段則起訖角明顯不同，用此判定 overlay 該畫整圓或弧。
        public bool IsClosed
        {
            get
            {
                double extent = Math.Abs(EndPhi - StartPhi);
                const double fullTolerance = 0.01;
                return Math.Abs(extent - 2.0 * Math.PI) < fullTolerance;
            }
        }
    }
}
