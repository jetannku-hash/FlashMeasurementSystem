using System;
using System.Diagnostics;
using FlashMeasurementSystem.Domain.Roi;
using HalconDotNet;

namespace FlashMeasurementSystem
{
    public class OverlayAnnotator
    {
        private readonly HWindow _window;

        public OverlayAnnotator(HWindow window) { _window = window; }

        public void DrawCross(double row, double col, double size, string color = null)
        {
            HOperatorSet.SetColor(_window, color ?? "yellow");
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.DispCross(_window, row, col, size, 0);
        }

        public void DrawRectangle2(double row, double col, double phi, double length1, double length2, string color = null)
        {
            HOperatorSet.SetColor(_window, color ?? "yellow");
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.SetDraw(_window, "margin");
            // 用 +phi（不取負）：disp_rectangle2 與 gen_measure_rectangle2 / gen_rectangle2 使用
            // 完全相同的 Phi 慣例（longitudinal axis 對水平的弧度），所以同一個 phi 畫出的框
            // 必須等於實際量測 (measure_pos) 與 subpix (edges_sub_pix) 所用的框。先前這裡傳 -phi
            // 會把顯示框畫成與量測框「鏡像」——對非軸向 (Angle≠0,90) 的 ROI，使用者照螢幕對齊
            // 邊緣後，量測卻發生在鏡像方向，導致斜邊永遠掃不到。DrawMatchResult 的 match 角度是另一個
            // 慣例（需 -phi 顯示），與此處無關，不要照抄。
            HOperatorSet.DispRectangle2(_window, row, col, phi, length1, length2);
        }

        /// <summary>
        /// 互動編輯外觀：綠色 rect2 外框 + 8 個把手方塊（4 角 + 4 邊中點）+ 旋轉圓鈕與連接桿。
        /// handleHalf 與 knobGapImg 皆為影像像素（由 helper 依縮放換算，確保螢幕上恆定大小）。
        /// </summary>
        public void DrawEditRect2(double cr, double cc, double phi, double l1, double l2,
            double handleHalf, double knobGapImg)
        {
            HOperatorSet.SetColor(_window, "green");
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.SetDraw(_window, "margin");
            HOperatorSet.DispRectangle2(_window, cr, cc, phi, l1, l2);

            Rect2EditMath.Axes(phi, out double e1r, out double e1c, out double e2r, out double e2c);

            double endR = cr + l1 * e1r, endC = cc + l1 * e1c;
            Rect2EditMath.RotateKnobPos(cr, cc, phi, l1, knobGapImg, out double kr, out double kc);
            HOperatorSet.DispLine(_window, endR, endC, kr, kc);

            HOperatorSet.SetDraw(_window, "fill");
            DrawHandleSquare(cr + l1 * e1r + l2 * e2r, cc + l1 * e1c + l2 * e2c, handleHalf);
            DrawHandleSquare(cr + l1 * e1r - l2 * e2r, cc + l1 * e1c - l2 * e2c, handleHalf);
            DrawHandleSquare(cr - l1 * e1r + l2 * e2r, cc - l1 * e1c + l2 * e2c, handleHalf);
            DrawHandleSquare(cr - l1 * e1r - l2 * e2r, cc - l1 * e1c - l2 * e2c, handleHalf);
            DrawHandleSquare(cr + l1 * e1r, cc + l1 * e1c, handleHalf);
            DrawHandleSquare(cr - l1 * e1r, cc - l1 * e1c, handleHalf);
            DrawHandleSquare(cr + l2 * e2r, cc + l2 * e2c, handleHalf);
            DrawHandleSquare(cr - l2 * e2r, cc - l2 * e2c, handleHalf);
            HOperatorSet.DispCircle(_window, kr, kc, handleHalf);

            HOperatorSet.SetDraw(_window, "margin");
        }

        private void DrawHandleSquare(double r, double c, double half)
        {
            HOperatorSet.DispRectangle1(_window, r - half, c - half, r + half, c + half);
        }

        public void DrawLine(double row1, double col1, double row2, double col2, string color = null)
        {
            HOperatorSet.SetColor(_window, color ?? "yellow");
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.DispLine(_window, row1, col1, row2, col2);
        }

        public void DrawCircle(double row, double col, double radius, string color = null)
        {
            HOperatorSet.SetColor(_window, color ?? "yellow");
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.SetDraw(_window, "margin");
            HOperatorSet.DispCircle(_window, row, col, radius);
        }

        public void DrawRoiRectangle(double row1, double col1, double row2, double col2)
        {
            HOperatorSet.SetColor(_window, "blue");
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.SetDraw(_window, "margin");
            // disp_rectangle1 要求 Row1<=Row2 且 Column1<=Column2（左上→右下），
            // 否則畫出空矩形。拖曳起點可在任一角，故先正規化 min/max，
            // 確保四個方向拖曳都能即時顯示藍框（先前只有左上→右下會顯示）。
            double r1 = Math.Min(row1, row2);
            double c1 = Math.Min(col1, col2);
            double r2 = Math.Max(row1, row2);
            double c2 = Math.Max(col1, col2);
            HOperatorSet.DispRectangle1(_window, r1, c1, r2, c2);
        }

        public void DrawText(string message, int row, int col, string color = null)
        {
            HOperatorSet.SetColor(_window, color ?? "yellow");
            SetAvailableFont(10);
            HOperatorSet.SetTposition(_window, row, col);
            HOperatorSet.WriteString(_window, message);
        }

        public void DrawMatchResult(double row, double col, double angleDeg, double score,
            double length1 = 50, double length2 = 30)
        {
            string color = score > 0.7 ? "green" : "red";
            double phi = angleDeg * Math.PI / 180.0;

            HOperatorSet.SetColor(_window, color);
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.SetDraw(_window, "margin");
            HOperatorSet.DispRectangle2(_window, row, col, -phi, length1, length2);

            HOperatorSet.SetDraw(_window, "fill");
            HOperatorSet.DispCross(_window, row, col, 30, phi);

            SetAvailableFont(12);
            HOperatorSet.SetTposition(_window, (int)(row - 10), (int)(col + 10));
            HOperatorSet.WriteString(_window, $"score: {score:F4}");
        }

        public void DrawMatchContour(HObject contour, double row, double col, double angleDeg, double score)
        {
            if (contour == null) return;

            string color = score > 0.7 ? "green" : "red";
            double phi = angleDeg * Math.PI / 180.0;

            HOperatorSet.SetColor(_window, color);
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.SetDraw(_window, "margin");
            HOperatorSet.DispObj(contour, _window);

            HOperatorSet.SetDraw(_window, "fill");
            HOperatorSet.DispCross(_window, row, col, 30, phi);

            SetAvailableFont(12);
            HOperatorSet.SetTposition(_window, (int)(row - 10), (int)(col + 10));
            HOperatorSet.WriteString(_window, $"score: {score:F4}");
        }


        // ─── 4.13 尺寸標註：距離 / 角度 / 結果表 ────────────────────────────────

        /// <summary>
        /// 距離標註：兩端連線 + 中點數值文字。isOk 為 null 時用中性黃色，
        /// true 綠 / false 紅（供公差判定上色）。
        /// </summary>
        public void DrawDistance(double row1, double col1, double row2, double col2,
            string valueText, bool? isOk = null)
        {
            string color = isOk == null ? "yellow" : (isOk.Value ? "green" : "red");
            HOperatorSet.SetColor(_window, color);
            HOperatorSet.SetLineWidth(_window, 2);
            HOperatorSet.DispLine(_window, row1, col1, row2, col2);

            int midRow = (int)((row1 + row2) / 2.0);
            int midCol = (int)((col1 + col2) / 2.0);
            SetAvailableFont(12);
            HOperatorSet.SetTposition(_window, midRow - 16, midCol + 8);
            HOperatorSet.WriteString(_window, valueText ?? string.Empty);
        }

        /// <summary>
        /// 角度弧線 + 數值。startAngleRad = 弧起點方位角；extentRad = 弧張角（有號，會取絕對值）。
        /// 採 HALCON disp_arc(Window, CenterRow, CenterCol, Angle(張角,>0), BeginRow, BeginCol)：
        /// 起點由 中心 + 半徑×(sinφ→Row, cosφ→Col) 算出。弧僅作角度視覺指示。
        /// </summary>
        public void DrawAngle(double centerRow, double centerCol, double radiusPx,
            double startAngleRad, double extentRad, string valueText, bool? isOk = null)
        {
            string color = isOk == null ? "yellow" : (isOk.Value ? "green" : "red");
            HOperatorSet.SetColor(_window, color);
            HOperatorSet.SetLineWidth(_window, 2);

            double beginRow = centerRow + radiusPx * Math.Sin(startAngleRad);
            double beginCol = centerCol + radiusPx * Math.Cos(startAngleRad);
            double extent = Math.Abs(extentRad);
            if (extent < 1e-6) extent = 1e-6;  // disp_arc 要求 Angle > 0
            HOperatorSet.DispArc(_window, centerRow, centerCol, extent, beginRow, beginCol);

            double midAngle = startAngleRad + extentRad / 2.0;
            int textRow = (int)(centerRow + (radiusPx + 16) * Math.Sin(midAngle));
            int textCol = (int)(centerCol + (radiusPx + 16) * Math.Cos(midAngle));
            SetAvailableFont(12);
            HOperatorSet.SetTposition(_window, textRow, textCol);
            HOperatorSet.WriteString(_window, valueText ?? string.Empty);
        }

        /// <summary>
        /// 結果表（視窗錨定 HUD）：左上角顯示「項目 / 實測值 / 判定」，OK 綠 / NG 紅 / 無判定白。
        /// 以 get_window_extents + 暫時 set_part 切到「影像座標 == 視窗像素」，使表格固定於視窗
        /// 左上、不受影像縮放/平移影響；並先畫深色背景框，確保白底影像上仍可讀。
        /// 結束後還原原本的 part（try/finally）。
        /// </summary>
        public void DrawResultTable(System.Collections.Generic.IList<OverlayResultRow> rows)
        {
            if (rows == null || rows.Count == 0) return;

            HOperatorSet.GetWindowExtents(_window, out HTuple _, out HTuple _,
                out HTuple winWidth, out HTuple winHeight);
            HOperatorSet.GetPart(_window, out HTuple pr1, out HTuple pc1, out HTuple pr2, out HTuple pc2);

            try
            {
                // 影像座標 == 視窗像素：之後的 set_tposition / disp_rectangle1 皆以視窗像素計。
                HOperatorSet.SetPart(_window, 0, 0, winHeight - 1, winWidth - 1);

                const int lineH = 22, col1 = 14, col2 = 170, col3 = 285, boxLeft = 6, boxWidth = 350;
                int boxBottom = 6 + (rows.Count + 1) * lineH + 6;

                // 深色背景框（填滿），確保任何影像背景上文字皆可讀。
                HOperatorSet.SetColor(_window, "black");
                HOperatorSet.SetDraw(_window, "fill");
                HOperatorSet.DispRectangle1(_window, 6, boxLeft, boxBottom, boxLeft + boxWidth);
                HOperatorSet.SetDraw(_window, "margin");

                SetAvailableFont(12);

                // 表頭（白字）
                HOperatorSet.SetColor(_window, "white");
                WriteAt(12, col1, "項目");
                WriteAt(12, col2, "實測值");
                WriteAt(12, col3, "判定");

                for (int i = 0; i < rows.Count; i++)
                {
                    OverlayResultRow r = rows[i];
                    if (r == null) continue;
                    string color = r.IsOk == null ? "white" : (r.IsOk.Value ? "green" : "red");
                    HOperatorSet.SetColor(_window, color);
                    int y = 12 + (i + 1) * lineH;
                    WriteAt(y, col1, r.Name ?? string.Empty);
                    WriteAt(y, col2, r.ValueText ?? string.Empty);
                    WriteAt(y, col3, r.IsOk == null ? string.Empty : (r.IsOk.Value ? "OK" : "NG"));
                }
            }
            finally
            {
                // 還原原本 part（回到影像座標），不影響後續重繪。
                HOperatorSet.SetPart(_window, pr1, pc1, pr2, pc2);
            }
        }

        private void WriteAt(int row, int col, string text)
        {
            HOperatorSet.SetTposition(_window, row, col);
            HOperatorSet.WriteString(_window, text);
        }

        public void Clear() { HOperatorSet.ClearWindow(_window); }

        // 已解析字型名快取（依字級）。同一視窗的可用字型清單執行期不變，故每個字級只需
        // QueryFont 列舉一次；否則每筆文字/結果表每列都列舉全系統字型，是 pan/zoom 卡頓主因之一。
        private readonly System.Collections.Generic.Dictionary<int, string> _fontCache =
            new System.Collections.Generic.Dictionary<int, string>();

        private void SetAvailableFont(int requestedSize)
        {
            try
            {
                if (!_fontCache.TryGetValue(requestedSize, out string font))
                {
                    HOperatorSet.QueryFont(_window, out HTuple availableFonts);
                    font = SelectAvailableFont(availableFonts, requestedSize);
                    _fontCache[requestedSize] = font;
                }
                if (!string.IsNullOrEmpty(font))
                {
                    HOperatorSet.SetFont(_window, font);
                }
            }
            catch (HalconException ex)
            {
                Debug.WriteLine($"HALCON font selection failed: {ex.Message}");
            }
        }

        private static string SelectAvailableFont(HTuple availableFonts, int requestedSize)
        {
            string requestedSizeToken = "-" + requestedSize;
            string[] preferredFamilies = { "Courier New", "Consolas", "Arial", "Microsoft Sans Serif" };

            foreach (string family in preferredFamilies)
            {
                string exact = FindFont(availableFonts, family, requestedSizeToken, requireSize: true);
                if (!string.IsNullOrEmpty(exact))
                {
                    return exact;
                }
            }

            foreach (string family in preferredFamilies)
            {
                string fallback = FindFont(availableFonts, family, requestedSizeToken, requireSize: false);
                if (!string.IsNullOrEmpty(fallback))
                {
                    return fallback;
                }
            }

            return availableFonts != null && availableFonts.Length > 0 ? availableFonts[0].S : string.Empty;
        }

        private static string FindFont(HTuple availableFonts, string family, string requestedSizeToken, bool requireSize)
        {
            if (availableFonts == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < availableFonts.Length; i++)
            {
                string font = availableFonts[i].S;
                bool matchesFamily = font.IndexOf(family, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchesSize = font.IndexOf(requestedSizeToken, StringComparison.OrdinalIgnoreCase) >= 0;
                if (matchesFamily && (!requireSize || matchesSize))
                {
                    return font;
                }
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// 結果表的一列。IsOk 為 null 表示無公差判定（只顯示數值，判定欄留白）。
    /// </summary>
    public class OverlayResultRow
    {
        public string Name { get; set; } = "";
        public string ValueText { get; set; } = "";
        public bool? IsOk { get; set; }
    }
}
