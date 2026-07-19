using System.Collections.Generic;
using FlashMeasurementSystem.Domain.PinPitchAnalysis;

namespace FlashMeasurementSystem.Domain.PinDetection
{
    /// <summary>矩形 ROI 內引腳 blob 偵測結果（純 DTO）。Success=false 見 ErrorMessage。</summary>
    public class PinDetectionResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public List<PinPoint> Pins { get; set; } = new List<PinPoint>();
    }
}
