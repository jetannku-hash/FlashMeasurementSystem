using System;

namespace FlashMeasurementSystem.Domain.Calibration
{
    /// <summary>
    /// 校正設定檔（獨立持久化於 data/calibrations，配方以 ProfileId 參考、不內嵌）。
    /// 簡易版只含等向 pixel size 換算；完整畸變校正為後續里程碑。
    /// SchemaVersion 用於持久化向後相容，日後欄位變動時據此遷移。
    /// </summary>
    public class CalibrationProfile
    {
        public int SchemaVersion { get; set; } = 1;
        public string ProfileId { get; set; } = "";
        public string Description { get; set; } = "";

        public double PixelSizeUmX { get; set; } = 10.0;
        public double PixelSizeUmY { get; set; } = 10.0;
        public double FieldOfViewMmX { get; set; }
        public double FieldOfViewMmY { get; set; }

        public double CalibrationStandardMm { get; set; }  // 校正用的已知距離
        public double MeasuredPixels { get; set; }         // 該已知距離對應的像素數
        public bool DistortionCorrected { get; set; } = false; // 簡易版恆為 false

        public DateTime CreatedAt { get; set; }

        public static CalibrationProfile Default()
        {
            return new CalibrationProfile();
        }
    }
}
