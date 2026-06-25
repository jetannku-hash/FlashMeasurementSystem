namespace FlashMeasurementSystem.Domain.RectangleFitting
{
    public class RectangleFittingResult
    {
        public bool Success { get; set; }
        public double CenterRow { get; set; }
        public double CenterColumn { get; set; }

        // 主軸方向（弧度，逆時針）。Phi 為 Length1 邊與水平軸的夾角（reference L175941）。
        public double Phi { get; set; }

        // Length1 / Length2 為半邊長（像素）；Phi 描述 Length1 邊的方向（reference L175941-175942）。
        public double Length1Px { get; set; }
        public double Length2Px { get; set; }
        public string PointOrder { get; set; } = string.Empty;
        public double ResidualRms { get; set; }
        public int UsedPoints { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
