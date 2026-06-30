using System;
using System.IO;
using HalconDotNet;

namespace FlashMeasurementSystem.Tests.Halcon
{
    /// <summary>
    /// 產生 2D 量測模型的合成測試圖（已知像素真值）寫入 data/images，供操作員 GUI 功能驗收。
    /// 畫進去的幾何 == data/images/SYNTHETIC_METROLOGY_GROUNDTRUTH.md 的名目值（單一真相來源）。
    /// 與 MetrologyModelHalconTests / TestImageGenerator 用相同的幾何，確保一致。
    /// </summary>
    public static class SyntheticMetrologyImageGenerator
    {
        private static readonly string[] Names =
        {
            "synthetic_metrology_line",
            "synthetic_metrology_circle",
            "synthetic_metrology_ellipse",
            "synthetic_metrology_rectangle",
            "synthetic_metrology_composite"
        };

        public static void Run()
        {
            string dir = ResolveDataImagesDir();
            Directory.CreateDirectory(dir);

            int n = 0;
            n += Write(dir, "synthetic_metrology_line", TestImageGenerator.CreateEdgeImage(256, 256)); // 垂直階梯邊 col≈127.5
            n += Write(dir, "synthetic_metrology_circle", TestImageGenerator.CreateCircleImage(256, 256, 128, 128, 50));
            n += Write(dir, "synthetic_metrology_ellipse", TestImageGenerator.CreateEllipseImage(256, 256, 128, 128, 0.0, 60, 35));
            n += Write(dir, "synthetic_metrology_rectangle", TestImageGenerator.CreateRectangleImage(256, 256, 128, 128, 0.0, 60, 40));
            n += Write(dir, "synthetic_metrology_composite", TestImageGenerator.CreateCompositeImage(256, 256));

            foreach (string name in Names)
            {
                string path = Path.Combine(dir, name + ".png");
                if (!File.Exists(path))
                    throw new InvalidOperationException("SyntheticMetrologyImageGenerator: missing " + path);
            }
            Console.WriteLine("SyntheticMetrologyImageGenerator: wrote " + n + " images to " + dir);
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
            // 退路：相對執行目錄回推固定層數（bin/x64/Debug → 專案 → tests → repo 根）。
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "data", "images");
        }
    }
}
