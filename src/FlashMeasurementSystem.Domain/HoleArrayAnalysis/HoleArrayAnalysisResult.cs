using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.HoleArrayAnalysis
{
    /// <summary>孔陣列分析結果（純 DTO）。Success=false 表流程失敗（見 ErrorMessage）。</summary>
    public class HoleArrayAnalysisResult
    {
        public bool Success   { get; set; }
        public bool IsPass    { get; set; }
        public int  HoleCount { get; set; }

        public double MeanDiameterMm   { get; set; }   // 各孔等效孔徑平均
        public double DiameterMaxDevMm { get; set; }   // 各孔孔徑對均值的最大偏差
        public double PitchXMm         { get; set; }   // 主軸 u 方向相鄰行群心平均間距（Cols==1 時為 0）
        public double PitchYMm         { get; set; }   // 次軸 v 方向相鄰列群心平均間距（Rows==1 時為 0）
        public double MaxPositionDevMm { get; set; }   // 各孔至其理想網格節點的最大距離

        public bool CountOk    { get; set; }
        public bool DiameterOk { get; set; }
        public bool PitchXOk   { get; set; }
        public bool PitchYOk   { get; set; }
        public bool PositionOk { get; set; }

        public List<HoleArrayPoint> Holes { get; set; } = new List<HoleArrayPoint>(); // 依 (列索引, 行索引) 排序
        public string ErrorMessage { get; set; } = "";  // 流程失敗原因（Success=false 時）

        public static HoleArrayAnalysisResult Failed(string message) =>
            new HoleArrayAnalysisResult { Success = false, IsPass = false, ErrorMessage = message };
    }
}
