namespace FlashMeasurementSystem.Domain.EllipseFitting
{
    public class EllipseFittingResult
    {
        public bool Success { get; set; }
        public double CenterRow { get; set; }
        public double CenterColumn { get; set; }

        // 主軸方向（弧度，逆時針，相對水平軸）。
        public double Phi { get; set; }

        // Radius1 = 長半軸、Radius2 = 短半軸（fit_ellipse_contour_xld 輸出，像素）。
        public double Radius1Px { get; set; }
        public double Radius2Px { get; set; }

        // 橢圓弧的起訖角；閉合橢圓時 HALCON 回 0 與 2*pi、PointOrder='positive'。
        public double StartPhi { get; set; }
        public double EndPhi { get; set; }
        public string PointOrder { get; set; } = string.Empty;

        public double ResidualRms { get; set; }
        public int UsedPoints { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
