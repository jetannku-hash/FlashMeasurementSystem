using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.GearAnalysis
{
    /// <summary>單一齒（供 overlay 標記）。角度為度。</summary>
    public class GearTooth
    {
        public double CenterAngleDeg { get; set; }
        public double WidthDeg { get; set; }
    }

    /// <summary>齒輪分析結果（純 DTO）。Success=false 表流程失敗（見 Message）。</summary>
    public class GearAnalysisResult
    {
        public bool Success { get; set; }
        public bool IsPass { get; set; }
        public int ToothCount { get; set; }

        public double PitchMeanDeg { get; set; }
        public double PitchMinDeg { get; set; }
        public double PitchMaxDeg { get; set; }
        public double PitchMaxDevDeg { get; set; }

        public double WidthMeanDeg { get; set; }
        public double WidthMinDeg { get; set; }
        public double WidthMaxDeg { get; set; }
        public double WidthMaxDevDeg { get; set; }
        public double WidthMeanPx { get; set; }

        public bool CountOk { get; set; }
        public bool PitchOk { get; set; }
        public bool WidthOk { get; set; }

        public List<GearTooth> Teeth { get; set; } = new List<GearTooth>();
        public List<double> MissingToothHintsDeg { get; set; } = new List<double>();
        public string Message { get; set; } = "";

        public static GearAnalysisResult Failed(string message) =>
            new GearAnalysisResult { Success = false, IsPass = false, Message = message };
    }
}
