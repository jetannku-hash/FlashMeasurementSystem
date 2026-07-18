using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.PcdAnalysis
{
    /// <summary>PCD 分析結果（純 DTO）。Success=false 表流程失敗（見 Message）。</summary>
    public class PcdAnalysisResult
    {
        public bool Success { get; set; }
        public bool IsPass { get; set; }
        public int  HoleCount { get; set; }

        public double PcdMm { get; set; }
        public double PcdPx { get; set; }
        public double CenterRow { get; set; }   // 擬合圓心（px，供 overlay）
        public double CenterCol { get; set; }

        public double AngularMeanDeg { get; set; }
        public double AngularMaxDevDeg { get; set; }
        public double RadialMaxDevMm { get; set; }
        public double RadialMaxDevPx { get; set; }

        public bool CountOk { get; set; }
        public bool PcdOk { get; set; }
        public bool AngularOk { get; set; }
        public bool RadialOk { get; set; }

        public List<HolePoint> Holes { get; set; } = new List<HolePoint>();      // 依角度排序
        public List<double> MissingHoleHintsDeg { get; set; } = new List<double>();
        public string Message { get; set; } = "";

        public static PcdAnalysisResult Failed(string message) =>
            new PcdAnalysisResult { Success = false, IsPass = false, Message = message };
    }
}
