namespace FlashMeasurementSystem.Domain.AngleMeasurement
{
    public class AngleMeasurementParameters
    {
        // 預設值寫在屬性 initializer 上，確保 new 出來的物件天生合法。
        public string Mode { get; set; } = "line_to_line";
        public double NearParallelWarningDeg { get; set; } = 2.0;
        public double MinPointSeparation { get; set; } = 1.0;

        public static AngleMeasurementParameters Default()
        {
            return new AngleMeasurementParameters();
        }

        public static bool IsSupportedMode(string mode)
        {
            return mode == "line_to_line"
                || mode == "line_to_horizontal"
                || mode == "line_to_vertical";
        }
    }
}
