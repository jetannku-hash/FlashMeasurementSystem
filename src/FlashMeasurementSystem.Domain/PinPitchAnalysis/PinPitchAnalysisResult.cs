using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.PinPitchAnalysis
{
    /// <summary>引腳間距分析結果（純 DTO）。Success=false 表流程失敗（見 Message）。</summary>
    public class PinPitchAnalysisResult
    {
        public bool Success { get; set; }
        public bool IsPass  { get; set; }
        public int  PinCount { get; set; }

        public double PitchMeanMm     { get; set; }
        public double PitchMaxDevMm   { get; set; }   // 各間距對均值的最大偏差（均勻度）
        public double StraightnessDevPx { get; set; } // 各質心至擬合線的最大垂距（px）

        public bool CountOk      { get; set; }
        public bool PitchOk      { get; set; }
        public bool UniformityOk { get; set; }
        public bool MissingOk    { get; set; }

        public List<double> PitchesMm { get; set; } = new List<double>();   // 相鄰間距（mm，依投影排序）
        public List<PinPoint> Pins    { get; set; } = new List<PinPoint>(); // 依沿線投影排序
        public string MissingHint  { get; set; } = "";  // 缺腳間隙位置提示，無缺腳為 ""
        public string ErrorMessage { get; set; } = "";  // 流程失敗原因（Success=false 時）

        public static PinPitchAnalysisResult Failed(string message) =>
            new PinPitchAnalysisResult { Success = false, IsPass = false, ErrorMessage = message };
    }
}
