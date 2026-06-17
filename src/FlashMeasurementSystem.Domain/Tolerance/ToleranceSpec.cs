namespace FlashMeasurementSystem.Domain.Tolerance
{
    /// <summary>
    /// 單一量測項目的公差規格。實際允許範圍為 [Nominal+LowerTolerance, Nominal+UpperTolerance]。
    /// LowerTolerance 通常為負值（下偏差），UpperTolerance 通常為正值（上偏差）。
    /// </summary>
    public class ToleranceSpec
    {
        public double Nominal { get; set; }
        public double LowerTolerance { get; set; }   // 通常負值，例如 -0.005
        public double UpperTolerance { get; set; }   // 通常正值，例如 +0.005
        public string Unit { get; set; } = "mm";

        public double LowerLimit { get { return Nominal + LowerTolerance; } }
        public double UpperLimit { get { return Nominal + UpperTolerance; } }

        public static ToleranceSpec Default()
        {
            return new ToleranceSpec();
        }
    }
}
