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

        /// <summary>
        /// 通過面積濾波、但因形狀不夠圓（circularity &lt; MinCircularity）而被剔除的 blob 數。
        /// &gt;0 代表 ROI 內有沾黏/相連的孔或髒污橋接——孔數會少報，必須讓操作員看得到，
        /// 否則「20 孔量到 19 孔」與「兩孔沾黏成一塊」在畫面上無法區分。不影響 Success 判定。
        /// </summary>
        public int RejectedByShapeCount { get; set; }
    }
}
