using FlashMeasurementSystem.Domain.Calibration;

namespace FlashMeasurementSystem.Application.Calibration
{
    /// <summary>
    /// 校正檔的持久化（序列化器無關；實作於 Infrastructure）。
    /// </summary>
    public interface ICalibrationStore
    {
        void Save(CalibrationProfile profile, string filePath);
        CalibrationProfile Load(string filePath);
    }
}
