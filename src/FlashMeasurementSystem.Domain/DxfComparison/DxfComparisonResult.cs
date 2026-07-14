namespace FlashMeasurementSystem.Domain.DxfComparison
{
    /// <summary>
    /// DXF 輪廓度比對結果（純 DTO）。Success=false 表示流程失敗（見 Message）。
    /// </summary>
    public class DxfComparisonResult
    {
        public bool Success { get; set; }
        public bool IsPass { get; set; }
        public double MaxDevPx { get; set; }
        public double MeanDevPx { get; set; }
        public double RmsDevPx { get; set; }
        public int PointsEvaluated { get; set; }
        public int PointsOverTolerance { get; set; }
        public double MatchScore { get; set; }
        public double PoseRow { get; set; }
        public double PoseCol { get; set; }
        public double PoseAngleRad { get; set; }
        public double PoseScale { get; set; }
        public string Message { get; set; } = "";

        public static DxfComparisonResult Failed(string message) =>
            new DxfComparisonResult { Success = false, IsPass = false, Message = message };
    }
}
