using System;
using System.IO;
using FlashMeasurementSystem.Application.Reporting;
using FlashMeasurementSystem.Domain.Reporting;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace FlashMeasurementSystem.Reporting.Pdf
{
    /// <summary>
    /// 用 MigraDoc/PdfSharp 把 <see cref="MeasurementReportModel"/> 渲染成 PDF。
    /// 這是整個方案唯一允許碰 PDF 函式庫的地方。
    ///
    /// ⚠️ 字型：PdfSharp 1.50 無法解析 TrueType Collection(.ttc)，Windows 幾乎所有中文字型都是 .ttc。
    /// 本機唯一可用的繁中單檔 TTF 是標楷體 DFKai-SB(kaiu.ttf)，故報表字型固定為此。
    /// 詳見 lib/PdfSharp.MigraDoc.1.50.5147/PROVENANCE.md。
    /// </summary>
    public class PdfMeasurementReportWriter : IMeasurementPdfReportWriter
    {
        /// <summary>預設報表字型。見類別註解與 PROVENANCE.md 的 .ttc 限制。</summary>
        public const string DefaultFontName = "DFKai-SB";
        private const double BaseFontSize = 10.0;

        /// <summary>版面可用寬度（A4 直式扣掉左右邊界後的保守值，公分）。</summary>
        private const double ContentWidthCm = 15.0;

        private readonly string _fontName;

        /// <param name="fontName">報表字型；預設標楷體。必須是單檔 .ttf（PdfSharp 1.50 無法解析 .ttc）。</param>
        public PdfMeasurementReportWriter(string fontName = DefaultFontName)
        {
            _fontName = string.IsNullOrEmpty(fontName) ? DefaultFontName : fontName;
        }

        public void Write(MeasurementReportModel model, string filePath)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("filePath is required", "filePath");

            // ⚠️ 必須先探測字型是否安裝。PdfSharp 對「不存在的字型名稱」【不會拋例外】——它會靜默
            // 代換成 Microsoft Sans Serif，而該字型沒有中文 glyph，結果是「PDF 產生成功但整份中文空白」，
            // 操作員只看到成功訊息、還可能把不可用的報表歸檔。這比直接失敗更糟，故在此擋掉。
            // (只有 .ttc 才會讓 PdfSharp 拋 parsing 例外；不存在的名稱不會。實測驗證。)
            EnsureFontInstalled(_fontName);

            string dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Document doc;
            try
            {
                doc = BuildDocument(model);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "建立 PDF 報表內容失敗（字型 '" + _fontName + "' 或版面設定問題）：" + ex.Message, ex);
            }

            try
            {
                var renderer = new PdfDocumentRenderer(true) { Document = doc };
                renderer.RenderDocument();
                renderer.PdfDocument.Save(filePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "輸出 PDF 報表失敗（" + filePath + "）：" + ex.Message, ex);
            }
        }

        /// <summary>
        /// 確認字型已安裝於本機。GDI 的 FontFamily 建構子對找不到的字型族會拋 ArgumentException，
        /// 這是可靠的探測方式；PdfSharp 自己不會告訴你字型被代換掉了。
        /// </summary>
        private static void EnsureFontInstalled(string fontName)
        {
            try
            {
                using (var probe = new System.Drawing.FontFamily(fontName)) { }
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException(
                    "報表字型 '" + fontName + "' 未安裝於本機，PDF 內的中文會變成空白。" +
                    "請安裝該字型（Windows：設定 → 應用程式 → 選用功能 → 繁體中文補充字型），" +
                    "或改用其他單檔 .ttf 中文字型。");
            }
        }

        private Document BuildDocument(MeasurementReportModel model)
        {
            var doc = new Document();
            doc.Styles["Normal"].Font.Name = _fontName;
            doc.Styles["Normal"].Font.Size = BaseFontSize;

            Section sec = doc.AddSection();

            Paragraph title = sec.AddParagraph("量測報表");
            title.Format.Font.Size = 16;
            title.Format.Font.Bold = true;
            title.Format.SpaceAfter = 10;

            AddHeaderBlock(sec, model);
            AddTable(sec, model);
            AddNotes(sec, model);
            AddImage(sec, model);

            Paragraph footer = sec.AddParagraph("本報表由 FlashMeasurementSystem 自動產生。");
            footer.Format.SpaceBefore = 12;
            footer.Format.Font.Size = 8;
            footer.Format.Font.Color = Colors.Gray;

            return doc;
        }

        private static void AddHeaderBlock(Section sec, MeasurementReportModel model)
        {
            Paragraph p = sec.AddParagraph();
            p.AddText("配方：" + Safe(model.RecipeName) + "　　");
            p.AddText("時間：" + model.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + "　　");
            p.AddText("整體判定：");
            FormattedText verdict = p.AddFormattedText(model.AllOk ? "PASS" : "FAIL", TextFormat.Bold);
            verdict.Font.Color = model.AllOk ? Colors.Green : Colors.Red;

            Paragraph p2 = sec.AddParagraph();
            p2.AddText("OK：" + model.OkCount + "　　NG：" + model.NgCount);
            if (!string.IsNullOrEmpty(model.PixelSizeText))
                p2.AddText("　　像素尺寸：" + model.PixelSizeText);
            if (model.HasMatch && !string.IsNullOrEmpty(model.MatchText))
                p2.AddText("　　模板姿態：" + model.MatchText);
            p2.Format.SpaceAfter = 4;

            if (!string.IsNullOrEmpty(model.Message))
            {
                Paragraph p3 = sec.AddParagraph("訊息：" + model.Message);
                p3.Format.SpaceAfter = 8;
            }
            else
            {
                p2.Format.SpaceAfter = 8;
            }
        }

        private static void AddTable(Section sec, MeasurementReportModel model)
        {
            Table table = sec.AddTable();
            table.Borders.Width = 0.5;
            table.Rows.LeftIndent = 0;

            // 五欄合計 15 cm，剛好填滿 A4 直式的可用寬度。Note 不進表格（太寬），改列在表格下方。
            double[] widths = { 4.4, 2.6, 3.4, 3.0, 1.6 };
            foreach (double w in widths) table.AddColumn(Unit.FromCentimeter(w));

            string[] headers = { "項目", "標稱值", "上下限", "實測值", "判定" };
            Row head = table.AddRow();
            head.HeadingFormat = true;
            head.Shading.Color = Colors.LightGray;
            for (int i = 0; i < headers.Length; i++)
            {
                Paragraph hp = head.Cells[i].AddParagraph(headers[i]);
                hp.Format.Font.Bold = true;
            }

            if (model.Rows == null) return;

            foreach (MeasurementReportRow r in model.Rows)
            {
                if (r == null) continue;
                Row row = table.AddRow();
                row.Cells[0].AddParagraph(Safe(r.ItemName));
                row.Cells[1].AddParagraph(Safe(r.NominalText));
                row.Cells[2].AddParagraph(Safe(r.LimitsText));
                row.Cells[3].AddParagraph(Safe(r.MeasuredText));
                row.Cells[4].AddParagraph(Safe(r.VerdictText));

                // NG 整列標紅加粗，肉眼一掃即見。
                if (r.IsOk.HasValue && !r.IsOk.Value)
                {
                    row.Format.Font.Color = Colors.Red;
                    row.Format.Font.Bold = true;
                }
            }
        }

        private static void AddNotes(Section sec, MeasurementReportModel model)
        {
            if (model.Rows == null) return;

            bool first = true;
            foreach (MeasurementReportRow r in model.Rows)
            {
                if (r == null || string.IsNullOrEmpty(r.Note)) continue;
                if (first)
                {
                    Paragraph label = sec.AddParagraph("備註：");
                    label.Format.SpaceBefore = 6;
                    label.Format.Font.Size = 8;
                    first = false;
                }
                Paragraph np = sec.AddParagraph("・" + Safe(r.ItemName) + "：" + r.Note);
                np.Format.Font.Size = 8;
            }
        }

        private static void AddImage(Section sec, MeasurementReportModel model)
        {
            if (string.IsNullOrEmpty(model.ImagePath)) return;

            string path;
            try
            {
                path = Path.GetFullPath(model.ImagePath);
            }
            catch (Exception)
            {
                return; // 路徑格式無效 → 靜默略過，不讓報表整份失敗
            }
            if (!File.Exists(path)) return;

            Paragraph caption = sec.AddParagraph("量測影像");
            caption.Format.SpaceBefore = 12;
            caption.Format.Font.Bold = true;

            MigraDoc.DocumentObjectModel.Shapes.Image img = sec.AddImage(path);
            img.Width = Unit.FromCentimeter(ContentWidthCm);
            img.LockAspectRatio = true;
        }

        private static string Safe(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : s;
        }
    }
}
