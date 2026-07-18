using System.Collections.Generic;
using FlashMeasurementSystem.Domain.PcdAnalysis;

namespace FlashMeasurementSystem.Domain.HoleDetection
{
    /// <summary>環帶內孔 blob 偵測結果（純 DTO）。Success=false 見 ErrorMessage。</summary>
    public class HoleDetectionResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public List<HolePoint> Holes { get; set; } = new List<HolePoint>();
    }
}
