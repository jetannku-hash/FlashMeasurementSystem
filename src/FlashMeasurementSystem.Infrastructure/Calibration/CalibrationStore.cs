using System;
using System.Globalization;
using System.IO;
using FlashMeasurementSystem.Application.Calibration;
using FlashMeasurementSystem.Domain.Calibration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

            JObject parsed;
            try
            {
                parsed = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("校正檔格式錯誤（JSON 解析失敗）：" + filePath + " — " + ex.Message, ex);
            }

            CalibrationProfile profile;
            try
            {
                profile = parsed.ToObject<CalibrationProfile>();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("校正檔格式錯誤（欄位型別不符）：" + filePath + " — " + ex.Message, ex);
            }

            if (profile == null)
                throw new InvalidOperationException("校正檔內容為空或無效：" + filePath);

            // #7：缺 PixelSizeUmX/Y 欄位時，CalibrationProfile 的 C# 初始值(10.0 µm/px) 會被靜默補上，
            // 下游每個尺寸量測都被錯誤比例縮放且無任何警告。改為明確要求關鍵欄位存在且為正有限值，
            // 否則擲例外讓操作員看到，而非載入一個看似有效、實則錯誤比例的校正。
            if (parsed["PixelSizeUmX"] == null || parsed["PixelSizeUmY"] == null)
                throw new InvalidOperationException(
                    "校正檔缺少 PixelSizeUmX/PixelSizeUmY 欄位，無法確定像素尺寸：" + filePath);

            if (!IsPositiveFinite(profile.PixelSizeUmX) || !IsPositiveFinite(profile.PixelSizeUmY))
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "校正檔像素尺寸無效（需為正有限值）：X={0}, Y={1} — {2}",
                    profile.PixelSizeUmX, profile.PixelSizeUmY, filePath));

            return profile;
        }

        private static bool IsPositiveFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v) && v > 0.0;
        }
    }
}
