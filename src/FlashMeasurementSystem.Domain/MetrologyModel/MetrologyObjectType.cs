namespace FlashMeasurementSystem.Domain.MetrologyModel
{
    /// <summary>
    /// 2D 量測模型物件型別。對應 HALCON add_metrology_object_*_measure 的四種幾何。
    /// </summary>
    public enum MetrologyObjectType
    {
        Line,
        Circle,
        Ellipse,
        Rectangle
    }
}
