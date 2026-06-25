using System;
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
            // 原子寫入：先寫同目錄暫存檔再 rename 覆蓋，避免中途崩潰/斷電截斷既有好檔。
            string tmp = filePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(filePath))
                File.Replace(tmp, filePath, null);
            else
                File.Move(tmp, filePath);
        }

        public CalibrationProfile Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("找不到校正檔：" + filePath, filePath);

            string json;
            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException("讀取校正檔失敗：" + filePath + " — " + ex.Message, ex);
            }

            CalibrationProfile profile;
            try
            {
                profile = JsonConvert.DeserializeObject<CalibrationProfile>(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("校正檔格式錯誤（JSON 解析失敗）：" + filePath + " — " + ex.Message, ex);
            }

            if (profile == null)
                throw new InvalidOperationException("校正檔內容為空或無效：" + filePath);

            return profile;
        }
    }
}
