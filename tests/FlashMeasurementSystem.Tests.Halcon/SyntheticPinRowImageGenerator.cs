using System;
using System.IO;
using System.Text;
using HalconDotNet;

namespace FlashMeasurementSystem.Tests.Halcon
{
    /// <summary>
    /// 產生引腳間距（pin_pitch）工具的合成 GUI 驗收圖（已知像素真值）寫入 data/images，
    /// 供操作員在主頁一鍵量測 / 編輯器試測時對照。畫進去的幾何 == PIN_ROW_GROUNDTRUTH.md 名目值。
    /// backlit 極性：亮背景(220) + 暗引腳(30)，對應 PinIsDark=true。
    /// </summary>
    public static class SyntheticPinRowImageGenerator
    {
        // 兩圖共用版面（像素）：8 腳、pitch 60、row 200、first col 100、pin box 30×16（半長 15、半寬 8）、影像 800×400。
        private const int Width = 800, Height = 400;
        private const int Row = 200, Col0 = 100, Pitch = 60, Count = 8;
        private const int PinHalfLen = 15, PinHalfWid = 8;
        private const int MissingIndex = 3;   // 移除第 4 顆（col=280）內部引腳 → 缺口 2× pitch

        public static void Run()
        {
            string dir = ResolveDataImagesDir();
            Directory.CreateDirectory(dir);

            int n = 0;
            n += Write(dir, "pin_row_ok",
                TestImageGenerator.CreatePinRowImage(Width, Height, Row, Col0, Pitch, Count, PinHalfLen, PinHalfWid, -1));
            n += Write(dir, "pin_row_missing",
                TestImageGenerator.CreatePinRowImage(Width, Height, Row, Col0, Pitch, Count, PinHalfLen, PinHalfWid, MissingIndex));

            foreach (string name in new[] { "pin_row_ok", "pin_row_missing" })
            {
                string path = Path.Combine(dir, name + ".png");
                if (!File.Exists(path))
                    throw new InvalidOperationException("SyntheticPinRowImageGenerator: missing " + path);
            }

            WriteGroundTruth(dir);
            Console.WriteLine("SyntheticPinRowImageGenerator: wrote " + n + " images to " + dir);
        }

        private static int Write(string dir, string name, HImage img)
        {
            using (img)
            {
                // HALCON write_image 會自動補上副檔名 → 傳不含 .png 的基底路徑。
                img.WriteImage("png", 0, Path.Combine(dir, name));
            }
            return 1;
        }

        // 覆寫（非附加）確保多次執行 idempotent，不會累積重複段落。
        private static void WriteGroundTruth(string dir)
        {
            int lastCol = Col0 + (Count - 1) * Pitch;   // 520
            var sb = new StringBuilder();
            sb.AppendLine("# Pin-row synthetic images — ground truth (pixels)");
            sb.AppendLine();
            sb.AppendLine("由 `SyntheticPinRowImageGenerator.Run()`（Tests.Halcon 套件）產生。");
            sb.AppendLine("極性：backlit — 亮背景灰階 220、暗引腳灰階 30，對應 `PinIsDark = true`。");
            sb.AppendLine("影像尺寸：" + Width + "×" + Height + "（w×h），引腳沿水平主軸（row 固定）排列。");
            sb.AppendLine("引腳外框：約 " + (2 * PinHalfLen) + "×" + (2 * PinHalfWid) + " px（沿主軸 × 垂直主軸）。");
            sb.AppendLine();
            sb.AppendLine("| 影像 | 引腳數 | pitch(px) | first col | last col | row | 說明 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            sb.AppendLine("| pin_row_ok.png | " + Count + " | " + Pitch + " | " + Col0 + " | " + lastCol + " | " + Row + " | 乾淨等距一排，全 PASS |");
            sb.AppendLine("| pin_row_missing.png | " + (Count - 1) + " | " + Pitch + "（缺口 2×） | " + Col0 + " | " + lastCol + " | " + Row +
                " | 移除第 " + (MissingIndex + 1) + " 顆內部引腳（col=" + (Col0 + MissingIndex * Pitch) + "）→ 缺腳 NG |");
            sb.AppendLine();
            sb.AppendLine("> 註：本 app 未校正（uncalibrated）。量測平均間距會以「量測分頁目前的像素尺寸」由像素換算成 mm，");
            sb.AppendLine("> 因此第一次量測請先讀主頁 / 試測顯示的「平均間距 mm」，再把 NominalPitchMm 設在該值附近，判定才有意義。");
            sb.AppendLine("> pin_row_ok 的名目像素 pitch 為 " + Pitch + " px；pin_row_missing 的最大相鄰間距約為 " + (2 * Pitch) + " px（缺口處）。");

            File.WriteAllText(Path.Combine(dir, "PIN_ROW_GROUNDTRUTH.md"), sb.ToString());
        }

        // 從執行目錄（bin/x64/Debug）往上找含 FlashMeasurementSystem.sln 的 repo 根，回傳 root/data/images。
        private static string ResolveDataImagesDir()
        {
            var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (d != null)
            {
                if (File.Exists(Path.Combine(d.FullName, "FlashMeasurementSystem.sln")))
                    return Path.Combine(d.FullName, "data", "images");
                d = d.Parent;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "data", "images");
        }
    }
}
