using FlashMeasurementSystem.Domain.MetrologyModel;

namespace FlashMeasurementSystem.Application.MetrologyModel
{
    /// <summary>
    /// 套用 2D 量測模型的應用介面，只以 Domain 型別表達。
    /// 影像型別泛型化（與 IEdgeDetector&lt;TImage&gt; 一致），讓 Application 層不相依 HALCON；
    /// Halcon 適配器實作 IMetrologyModelRunner&lt;HImage&gt;。
    /// </summary>
    public interface IMetrologyModelRunner<TImage>
    {
        MetrologyModelResult Apply(
            MetrologyModelDef model,
            double refRow, double refCol, double refAngleRad, bool hasReferencePose,
            TImage image,
            double matchRow, double matchCol, double matchAngleRad, bool hasMatch);
    }
}
