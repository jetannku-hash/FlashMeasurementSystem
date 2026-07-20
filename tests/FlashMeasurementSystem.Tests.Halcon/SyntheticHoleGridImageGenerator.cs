using System;
using System.IO;
using System.Text;
using HalconDotNet;

namespace FlashMeasurementSystem.Tests.Halcon
{
    /// <summary>
    /// 產生孔陣列（hole_array）工具的合成 GUI 驗收圖（已知像素真值）寫入 data/images，
    /// 供操作員在主頁一鍵量測 / 編輯器試測時對照。畫進去的幾何 == HOLE_GRID_GROUNDTRUTH.md 名目值。
    /// backlit 極性：亮背景(220) + 暗孔(30)，對應 HoleIsDark=true。
    /// </summary>
    public static class SyntheticHoleGridImageGenerator
    {
        // 兩圖共用版面（像素）：4 列 × 5 行、pitchY 100 / pitchX 120、第一孔 (150,150)、孔半徑 18、影像 800×600。
        private const int Width = 800, Height = 600;
        private const int Rows = 4, Cols = 5;
        private const int Row0 = 150, Col0 = 150, PitchY = 100, PitchX = 120;
        private const int HoleRadius = 18;
        private const int MissingIndex = 7;   // row-major 第 8 個（r=1,c=2）內部孔 → 孔數 NG

        public static void Run()
        {
            string dir = ResolveDataImagesDir();
            Directory.CreateDirectory(dir);

            int n = 0;
            n += Write(dir, "hole_grid_ok",
                TestImageGenerator.CreateHoleGridImage(Width, Height, Row0, Col0, PitchY, PitchX, Rows, Cols, HoleRadius));
            n += Write(dir, "hole_grid_missing",
                TestImageGenerator.CreateHoleGridImage(Width, Height, Row0, Col0, PitchY, PitchX, Rows, Cols, HoleRadius, MissingIndex));

            foreach (string name in new[] { "hole_grid_ok", "hole_grid_missing" })
            {
                string path = Path.Combine(dir, name + ".png");
                if (!File.Exists(path))
                    throw new InvalidOperationException("SyntheticHoleGridImageGenerator: missing " + path);
            }

            WriteGroundTruth(dir);
            Console.WriteLine("SyntheticHoleGridImageGenerator: wrote " + n + " images to " + dir);
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
            int total = Rows * Cols;
            int missRow = Row0 + (MissingIndex / Cols) * PitchY;
            int missCol = Col0 + (MissingIndex % Cols) * PitchX;
            var sb = new StringBuilder();
            sb.AppendLine("# Hole-grid synthetic images — ground truth (pixels)");
            sb.AppendLine();
            sb.AppendLine("由 `SyntheticHoleGridImageGenerator.Run()`（Tests.Halcon 套件）產生。");
            sb.AppendLine("極性：backlit — 亮背景灰階 220、暗孔灰階 30，對應 `HoleIsDark = true`。");
            sb.AppendLine("影像尺寸：" + Width + "×" + Height + "（w×h），孔以規則網格排列（AngleRad=0）。");
            sb.AppendLine();
            sb.AppendLine("| 影像 | 孔數 | 列×行 | X 間距(px) | Y 間距(px) | 第一孔中心 (row,col) | 孔半徑(px) | 孔徑(px) | 說明 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            sb.AppendLine("| hole_grid_ok.png | " + total + " | " + Rows + "×" + Cols + " | " + PitchX + " | " + PitchY
                + " | (" + Row0 + "," + Col0 + ") | " + HoleRadius + " | " + (2 * HoleRadius) + " | 乾淨網格，全 PASS |");
            sb.AppendLine("| hole_grid_missing.png | " + (total - 1) + " | " + Rows + "×" + Cols + "（缺 1） | " + PitchX + " | " + PitchY
                + " | (" + Row0 + "," + Col0 + ") | " + HoleRadius + " | " + (2 * HoleRadius)
                + " | 移除 row-major 第 " + (MissingIndex + 1) + " 個內部孔（row=" + missRow + ", col=" + missCol + "）→ 孔數 NG |");
            sb.AppendLine();
            sb.AppendLine("最後一孔中心（ok 圖）：row=" + (Row0 + (Rows - 1) * PitchY) + ", col=" + (Col0 + (Cols - 1) * PitchX) + "。");
            sb.AppendLine();
            sb.AppendLine("> 註：本 app 未校正（uncalibrated）。量測值會以「量測分頁目前的像素尺寸」由像素換算成 mm，");
            sb.AppendLine("> 因此第一次量測請先讀主頁 / 試測顯示的「平均孔徑 / X 間距 / Y 間距（mm）」，");
            sb.AppendLine("> 再把對應的名目值（孔徑、X 間距、Y 間距）設在該值附近，判定才有意義。");
            sb.AppendLine("> 名目像素值：孔徑 " + (2 * HoleRadius) + " px、X 間距 " + PitchX + " px、Y 間距 " + PitchY + " px。");

            File.WriteAllText(Path.Combine(dir, "HOLE_GRID_GROUNDTRUTH.md"), sb.ToString());
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
