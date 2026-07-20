using System.Collections.Generic;
using FlashMeasurementSystem.Domain.HoleArrayAnalysis;

namespace FlashMeasurementSystem.Domain.HoleArrayDetection
{
    /// <summary>矩形 ROI 內孔洞 blob 偵測結果（純 DTO）。Success=false 見 ErrorMessage。</summary>
    public class HoleArrayDetectionResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public List<HoleArrayPoint> Holes { get; set; } = new List<HoleArrayPoint>();
    }
}
