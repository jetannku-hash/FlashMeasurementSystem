namespace FlashMeasurementSystem.Domain.Gdt
{
    /// <summary>
    /// 形位公差規格（單邊）。允許範圍為 [0, ToleranceZoneMm]，無 Nominal——
    /// 與尺寸公差的雙邊 <see cref="Tolerance.ToleranceSpec"/> 本質不同，故獨立。
    /// 基準（若該類型需要）以 MeasurementTool.RefToolIds[1] 指定，不放此規格內。
    /// </summary>
    public class GdtToleranceSpec
    {
        public GdtCharacteristic Characteristic { get; set; }

        /// <summary>公差帶寬 T（mm），須 &gt; 0。</summary>
        public double ToleranceZoneMm { get; set; }

        public static GdtToleranceSpec Default()
        {
            return new GdtToleranceSpec();
        }
    }
}
