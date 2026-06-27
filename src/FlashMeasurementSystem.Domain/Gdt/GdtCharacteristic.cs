namespace FlashMeasurementSystem.Domain.Gdt
{
    /// <summary>
    /// v1 支援的形位公差類型（單邊判定，0 ≤ 偏差 ≤ T）。
    /// Roundness/Straightness 無基準；Parallelism/Perpendicularity/Concentricity 需單一基準（RefToolIds[1]）。
    /// </summary>
    public enum GdtCharacteristic
    {
        Roundness,         // 真圓度（ref=[circle]，偏差=max-min 徑向）
        Straightness,      // 真直度（ref=[line]，偏差=ResidualRms 近似，v1）
        Parallelism,       // 平行度（ref=[line 量測, line 基準]）
        Perpendicularity,  // 垂直度（ref=[line 量測, line 基準]）
        Concentricity      // 同心度/同軸度（ref=[circle 量測, circle 基準]）
    }
}
