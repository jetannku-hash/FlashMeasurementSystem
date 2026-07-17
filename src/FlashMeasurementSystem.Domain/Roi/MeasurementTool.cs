using System.Collections.Generic;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.Gdt;
using FlashMeasurementSystem.Domain.GearAnalysis;
using FlashMeasurementSystem.Domain.PcdAnalysis;
using FlashMeasurementSystem.Domain.Tolerance;

namespace FlashMeasurementSystem.Domain.Roi
{
    /// <summary>
    /// 單一量測工具定義（配方的組成單位）。以組合方式重用既有 DTO：
    /// 幾何用 <see cref="RoiGeometry"/>、邊緣參數重用 <see cref="EdgeDetectionParameters"/>、
    /// 公差重用 <see cref="ToleranceSpec"/>——不重新定義 Sigma/Threshold/公差欄位。
    /// 執行期量測「結果」不放進工具定義（結果為執行期產物，與配方定義分離）。
    /// </summary>
    public class MeasurementTool
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        // edge / line / circle / distance / angle / intersection / midline / projection
        // / roundness / straightness / parallelism / perpendicularity / concentricity（GD&T 形位公差，v1）
        public string ToolType { get; set; } = "edge";

        public RoiGeometry Roi { get; set; } = new RoiGeometry();
        public EdgeDetectionParameters EdgeParameters { get; set; } = EdgeDetectionParameters.Default();
        public ToleranceSpec Tolerance { get; set; } = ToleranceSpec.Default();

        // GD&T 形位公差規格（單邊判定）。null＝非 GD&T 工具，走既有雙邊 Tolerance。
        public GdtToleranceSpec Gdt { get; set; } = null;

        // 複合工具（distance/angle）參考的元素工具 Id（line/circle）。
        // 自足工具（circle/line 元素）此清單為空。順序有意義（例：distance 取 [0]→[1]）。
        public List<string> RefToolIds { get; set; } = new List<string>();

        // v7：弧形量測 ROI（重用既有 ArcMeasureRoi DTO）。null＝非弧工具，走既有 rect2 Roi。
        // 弧工具（ToolType="arc"）必填；Roi(rect2) 對弧工具無用但保留（加性模式，不改造 RoiGeometry）。
        public ArcMeasureRoi ArcRoi { get; set; } = null;

        // v8：齒輪分析參數（重用 GearAnalysisParameters DTO）。null＝非齒輪工具。
        // 齒輪工具（ToolType="gear"）必填；量測環帶用 ArcRoi。
        public GearAnalysisParameters Gear { get; set; } = null;

        // v9：PCD 螺栓孔圈分析參數（重用 PcdAnalysisParameters DTO）。null＝非 pcd 工具。
        // pcd 工具（ToolType="pcd"）必填；量測環帶用 ArcRoi。
        public PcdAnalysisParameters Pcd { get; set; } = null;
    }
}
