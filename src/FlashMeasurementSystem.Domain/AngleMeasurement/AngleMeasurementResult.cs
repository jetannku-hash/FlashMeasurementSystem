namespace FlashMeasurementSystem.Domain.AngleMeasurement
{
    public class AngleMeasurementResult
    {
        public bool Success { get; set; }
        public double AngleDeg { get; set; }       // 主要答案：兩線夾角 [0,180]
        public double AngleRad { get; set; }       // 同上，弧度 [0,π]
        public double AcuteAngleDeg { get; set; }  // 折成銳角 [0,90]，與端點順序無關
        public double RawAngleDeg { get; set; }    // angle_ll 原始有號值 (-180,180]
        public double RefAngle1Deg { get; set; }   // 線1 對水平軸 (angle_lx)
        public double RefAngle2Deg { get; set; }   // 線2 對水平軸 (angle_lx)
        public bool IsNearParallel { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
