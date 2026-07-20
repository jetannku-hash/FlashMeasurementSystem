using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using FlashMeasurementSystem.Application.Reporting;
using FlashMeasurementSystem.Domain.Reporting;
using FlashMeasurementSystem.Reporting.Pdf;

namespace FlashMeasurementSystem.Tests
{
    /// <summary>
    /// PdfMeasurementReportWriter 冒煙測試：確認能產生合法 PDF，且中文字型真的被嵌入
    /// （FontFile 標記＝擋掉 .ttc 解析失敗的那個坑），以及影像路徑三種情況都不擲例外。
    /// </summary>
    public static class PdfReportWriterTests
    {
        public static void Run()
        {
            IMeasurementPdfReportWriter writer = new PdfMeasurementReportWriter();

            // --- 案例 1：完整報表（中文項目名、OK/NG 混合、有訊息、有姿態、有影像）---
            string imgPath = TempPath("png");
            string pdf1 = TempPath("pdf");
            try
            {
                CreateSampleImage(imgPath);

                MeasurementReportModel model = BuildModel();
                model.ImagePath = imgPath;
                writer.Write(model, pdf1);

                Assert(File.Exists(pdf1), "案例1：PDF 檔案應存在");
                long len = new FileInfo(pdf1).Length;
                Assert(len > 0, "案例1：PDF 檔案不應為空");

                Assert(StartsWithPdfHeader(pdf1), "案例1：前 5 bytes 應為 %PDF-");

                // 嵌入字型證據：TrueType 子集會產生 FontFile2（無壓縮時可直接在原始 bytes 找到）。
                // 這正是 .ttc 解析失敗模式會被抓到的地方。
                string raw = ReadRawIso(pdf1);
                bool fontEmbedded = raw.IndexOf("FontFile2", StringComparison.Ordinal) >= 0
                                    || raw.IndexOf("FontFile", StringComparison.Ordinal) >= 0;
                Assert(fontEmbedded, "案例1：PDF 應含 FontFile/FontFile2（中文字型已嵌入）");

                Console.WriteLine("  PDF(含影像) = " + len + " bytes, 字型已嵌入 = " + fontEmbedded);
            }
            finally
            {
                Delete(pdf1);
                Delete(imgPath);
            }

            // --- 案例 2：ImagePath 為空 → 仍成功，只是沒有影像 ---
            string pdf2 = TempPath("pdf");
            try
            {
                MeasurementReportModel model = BuildModel();
                model.ImagePath = "";
                writer.Write(model, pdf2);
                Assert(File.Exists(pdf2) && new FileInfo(pdf2).Length > 0, "案例2：空影像路徑仍應產出 PDF");
                Assert(StartsWithPdfHeader(pdf2), "案例2：前 5 bytes 應為 %PDF-");
            }
            finally { Delete(pdf2); }

            // --- 案例 3：ImagePath 指向不存在的檔案 → 靜默略過，不擲例外 ---
            string pdf3 = TempPath("pdf");
            try
            {
                MeasurementReportModel model = BuildModel();
                model.ImagePath = Path.Combine(Path.GetTempPath(), "fms_no_such_image_" + Guid.NewGuid().ToString("N") + ".png");
                writer.Write(model, pdf3);
                Assert(File.Exists(pdf3) && new FileInfo(pdf3).Length > 0, "案例3：影像不存在時仍應產出 PDF");
                Assert(StartsWithPdfHeader(pdf3), "案例3：前 5 bytes 應為 %PDF-");
            }
            finally { Delete(pdf3); }

            // --- 案例 4：字型未安裝必須「大聲失敗」，不可產出中文空白的 PDF ---
            // PdfSharp 對不存在的字型名稱【不會拋例外】，會靜默代換成無中文 glyph 的字型，
            // 結果是「看似成功、實際整份中文空白」——比直接失敗更糟（操作員可能歸檔不可用的報表）。
            // 這個測試釘住「必須擋下來」的行為。
            string pdf4 = TempPath("pdf");
            try
            {
                var badWriter = new PdfMeasurementReportWriter("NoSuchFont-XYZ-12345");
                bool threw = false;
                string msg = "";
                try { badWriter.Write(BuildModel(), pdf4); }
                catch (InvalidOperationException ex) { threw = true; msg = ex.Message; }

                Assert(threw, "案例4：字型不存在時 Write 應擲 InvalidOperationException");
                Assert(msg.Contains("NoSuchFont-XYZ-12345"), "案例4：錯誤訊息應指出是哪個字型（實得：" + msg + "）");
                Assert(!File.Exists(pdf4), "案例4：不應留下半成品 PDF");
            }
            finally { Delete(pdf4); }

            Console.WriteLine("PdfReportWriterTests passed");
        }

        private static MeasurementReportModel BuildModel()
        {
            return new MeasurementReportModel
            {
                RecipeName = "0720-孔陣列檢測",
                Timestamp = new DateTime(2026, 7, 20, 14, 37, 5),
                AllOk = false,
                OkCount = 3,
                NgCount = 2,
                PixelSizeText = "10.00 µm (量測分頁)",
                HasMatch = true,
                MatchText = "分數 0.92　位移 (12.3, -4.5) px　角度 1.35°",
                Message = "本次量測有 2 項超出公差，請確認治具定位。",
                Rows = new List<MeasurementReportRow>
                {
                    Row("孔陣列-孔數", "20", "20", "20", "OK", true, ""),
                    Row("孔陣列-平均孔徑", "0.360 mm", "0.310 ~ 0.410", "0.362 mm", "OK", true, ""),
                    Row("孔陣列-X 間距", "1.200 mm", "1.100 ~ 1.300", "1.418 mm", "NG", false, "超出上限 0.118 mm"),
                    Row("引腳間距-缺腳偵測", "—", "—", "疑似缺腳", "NG", false, "第 7 腳間距異常放大"),
                    Row("外框-真直度", "0 mm", "0 ~ 0.050", "0.021 mm", "OK", true, "")
                }
            };
        }

        private static MeasurementReportRow Row(string name, string nominal, string limits,
            string measured, string verdict, bool? isOk, string note)
        {
            return new MeasurementReportRow
            {
                ItemName = name,
                NominalText = nominal,
                LimitsText = limits,
                MeasuredText = measured,
                VerdictText = verdict,
                IsOk = isOk,
                Note = note
            };
        }

        private static void CreateSampleImage(string path)
        {
            using (var bmp = new Bitmap(320, 200))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.DimGray);
                g.DrawEllipse(Pens.Lime, 40, 30, 120, 120);
                g.DrawRectangle(Pens.Red, 180, 40, 100, 80);
                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static bool StartsWithPdfHeader(string path)
        {
            var head = new byte[5];
            using (FileStream fs = File.OpenRead(path))
            {
                int read = fs.Read(head, 0, 5);
                if (read < 5) return false;
            }
            return Encoding.ASCII.GetString(head) == "%PDF-";
        }

        private static string ReadRawIso(string path)
        {
            return Encoding.GetEncoding("ISO-8859-1").GetString(File.ReadAllBytes(path));
        }

        private static string TempPath(string ext)
        {
            return Path.Combine(Path.GetTempPath(),
                "fms_pdf_" + Guid.NewGuid().ToString("N").Substring(0, 8) + "." + ext);
        }

        private static void Delete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("PdfReportWriterTests 失敗：" + name);
        }
    }
}
