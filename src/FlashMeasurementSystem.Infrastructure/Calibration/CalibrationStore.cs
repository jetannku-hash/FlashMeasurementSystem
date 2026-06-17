using System.IO;
using FlashMeasurementSystem.Application.Calibration;
using FlashMeasurementSystem.Domain.Calibration;
using Newtonsoft.Json;

namespace FlashMeasurementSystem.Infrastructure.Calibration
{
    /// <summary>
    /// 校正檔持久化（JSON，Newtonsoft）。存於 data/calibrations，配方以 ProfileId 參考。
    /// 序列化封在 Infrastructure，App 層不直接相依 Newtonsoft。
    /// </summary>
    public class CalibrationStore : ICalibrationStore
    {
        public void Save(CalibrationProfile profile, string filePath)
        {
            string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filePath, json);
        }

        public CalibrationProfile Load(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<CalibrationProfile>(json);
        }
    }
}
