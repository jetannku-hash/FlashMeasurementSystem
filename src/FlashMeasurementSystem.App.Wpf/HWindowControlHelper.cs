using System;
using System.Windows.Forms;
using FlashMeasurementSystem.Domain.EdgeDetection;
using FlashMeasurementSystem.Domain.Roi;
using HalconDotNet;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// 封裝 HWindowControl 的縮放、平移、ROI 繪製互動。
    /// 使用標準 WinForms 滑鼠事件確保 Halcon 17.12 相容性。
    /// </summary>
    public class HWindowControlHelper : IDisposable
    {
        private readonly HWindowControl _control;
        private HWindow _window => _control.HalconWindow;

        public HImage CurrentImage { get; private set; }
        public bool IsRoiMode { get; set; }
        /// <summary>
        /// true 期間下一次左鍵按下會開始「從圓心拖曳」建立扇形 ROI（見 RequestSector）。
        /// 設為 false 時一併清掉 pending callback——扇形手勢唯有 IsSectorMode 為真時才會在
        /// OnMouseDown 啟動，所以關掉此旗標即可讓殘留的 request 完全失效，不會誤觸後續拖曳
        /// 或洩漏進共用此 helper 的 RecipeEditor。
        /// </summary>
        public bool IsSectorMode
        {
            get => _isSectorMode;
            set
            {
                _isSectorMode = value;
                if (!value) _sectorCallback = null;
            }
        }
        private bool _isSectorMode;
        /// <summary>
        /// true 期間下一次左鍵按下會開始「兩點拖曳」建立直線標稱幾何（見 RequestLine）。
        /// 設為 false 時一併清掉 pending callback——比照 IsSectorMode，讓殘留的 request 完全失效。
        /// </summary>
        public bool IsLineMode
        {
            get => _isLineMode;
            set
            {
                _isLineMode = value;
                if (!value) _lineCallback = null;
            }
        }
        private bool _isLineMode;
        public bool HasRoi => Math.Abs(_roiEndRow - _roiStartRow) > 0.1 && Math.Abs(_roiEndCol - _roiStartCol) > 0.1;
        public double MouseRow { get; private set; }
        public double MouseCol { get; private set; }
        public OverlayAnnotator Annotator { get; }
        public bool IsEditingRect2 => _editActive;
        public bool IsEditingArc => _arcEditActive;

        public event Action<double, double, double, double> RoiSelected;
        public event Action<double, double> MouseMoved;

        private int _imageWidth, _imageHeight;
        private double _imgRow1, _imgCol1, _imgRow2, _imgCol2;
        private bool _isDrawingRoi, _isPanning;
        private double _roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol;
        private int _panStartX, _panStartY;
        private bool _isResizing;
        private Action _persistentOverlayAction;
        // 選取高亮疊加層：畫在 persistent overlay（量測結果）之上、編輯把手之下。
        // 用於編輯器選取「參照型工具」（GD&T/距離/角度/構造）時高亮其參照的元素，
        // 不覆寫量測結果 overlay。
        private Action _selectionHighlightAction;
        private Action<double, double, double, double> _roiCallback;
        private bool _editActive;
        private double _editCenterRow, _editCenterCol, _editPhi, _editLen1, _editLen2;
        private double _editLastRow, _editLastCol;
        private Rect2Handle _editMode = Rect2Handle.None;
        private Action<double, double, double, double, double> _editCallback;
        // 弧形互動編輯狀態（與 rect2 編輯互斥；同一時間只會有一個 active）。
        private bool _arcEditActive;
        private ArcHandle _arcEditMode = ArcHandle.None;
        private double _arcCr, _arcCc, _arcRadius, _arcA0, _arcExtent, _arcAnnulus;
        private double _arcLastRow, _arcLastCol;
        private Action<double, double, double, double, double, double> _arcEditCallback;
        // 扇形 ROI「從圓心拖曳」建立手勢狀態（與 rect2/arc 編輯互斥；由 RequestSector 一次性請求）。
        private bool _isDrawingSector;
        private double _sectorCenterRow, _sectorCenterCol, _sectorCursorRow, _sectorCursorCol;
        private Action<ArcMeasureRoi> _sectorCallback;
        // 直線標稱幾何「兩點拖曳」建立手勢狀態（與 rect2/arc/扇形互斥；由 RequestLine 一次性請求）。
        private bool _isDrawingLine;
        private double _lineStartRow, _lineStartCol, _lineCurRow, _lineCurCol;
        private Action<double, double, double, double> _lineCallback;

        public HWindowControlHelper(HWindowControl control)
        {
            _control = control;
            Annotator = new OverlayAnnotator(_window);

            // 使用標準 WinForms 滑鼠事件（Halcon 17.12 HMouseEventArgs 相容性不穩定）
            _control.MouseDown += OnMouseDown;
            _control.MouseMove += OnMouseMove;
            _control.MouseUp += OnMouseUp;
            _control.MouseWheel += OnMouseWheel;
            // 確保控制項獲得焦點才能接收 MouseWheel
            _control.MouseEnter += (s, e) => _control.Focus();
            _control.SizeChanged += OnSizeChanged;
        }

        public void DisplayImage(HImage image)
        {
            _persistentOverlayAction = null;
            _selectionHighlightAction = null;
            _editActive = false;
            _editMode = Rect2Handle.None;
            _editCallback = null;
            // 對稱清除弧形編輯狀態——否則換新圖後仍會以舊座標重繪弧形把手（H1）。
            _arcEditActive = false;
            _arcEditMode = ArcHandle.None;
            _arcEditCallback = null;
            // 對稱清除扇形拖曳建立狀態——否則換新圖後 pending RequestSector 仍以舊圖座標觸發。
            IsSectorMode = false;
            _isDrawingSector = false;
            _sectorCallback = null;
            // 對稱清除直線拖曳建立狀態——否則換新圖後 pending RequestLine 仍以舊圖座標觸發。
            IsLineMode = false;
            _isDrawingLine = false;
            _lineCallback = null;
            CurrentImage?.Dispose();
            CurrentImage = image;
            HOperatorSet.GetImageSize(image, out HTuple w, out HTuple h);
            _imageWidth = w.I; _imageHeight = h.I;
            _roiStartRow = _roiEndRow = 0;
            _roiStartCol = _roiEndCol = 0;
            FitToWindow();
            Redraw();
        }

        public void FitToWindow()
        {
            if (CurrentImage == null) return;
            if (_control.Width <= 0 || _control.Height <= 0) return;

            double cAspect = (double)_control.Width / _control.Height;
            double iAspect = (double)_imageWidth / _imageHeight;
            if (iAspect > cAspect)
            {
                _imgCol1 = 0; _imgCol2 = _imageWidth;
                double m = (_imageWidth / cAspect - _imageHeight) / 2.0;
                _imgRow1 = -m; _imgRow2 = _imageHeight + m;
            }
            else
            {
                _imgRow1 = 0; _imgRow2 = _imageHeight;
                double m = (_imageHeight * cAspect - _imageWidth) / 2.0;
                _imgCol1 = -m; _imgCol2 = _imageWidth + m;
            }
            HOperatorSet.SetPart(_window, _imgRow1, _imgCol1, _imgRow2, _imgCol2);
        }

        public void Redraw()
        {
            if (CurrentImage == null) return;
            HOperatorSet.ClearWindow(_window);
            HOperatorSet.SetPart(_window, _imgRow1, _imgCol1, _imgRow2, _imgCol2);
            HOperatorSet.DispObj(CurrentImage, _window);

            // 正在拖曳新 ROI 時：只畫即時藍框，並跳過 persistent overlay。
            // persistent overlay 畫的是上一輪的舊 ROI/量測結果（即將被新量測取代）；
            // 若仍一併畫出，會與正在拖曳的新 ROI 形成兩個藍框。更關鍵的是，下方
            // 「有 overlay 就不畫 ROI」的條件會讓第二次之後的拖曳完全看不到藍框
            // ——第一次拖曳時 overlay 尚為 null 故正常，量測後 overlay 被設值，
            // 之後每次拖曳藍框都消失（first-drag-only bug）。在拖曳期間強制畫藍框可修正。
            if (_isDrawingRoi)
            {
                if (HasRoi)
                {
                    Annotator.DrawRoiRectangle(_roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol);
                }
                return;
            }

            // 正在拖曳新扇形 ROI 時：比照 _isDrawingRoi，只畫即時預覽扇形、跳過 persistent overlay。
            if (_isDrawingSector)
            {
                ComputeSectorFromDrag(_sectorCenterRow, _sectorCenterCol, _sectorCursorRow, _sectorCursorCol,
                    out double pRadius, out double pAnnulus, out double pA0, out double pExtent);
                if (pRadius > 0.5)
                {
                    Annotator.DrawSectorRoi(_sectorCenterRow, _sectorCenterCol, pRadius, pAnnulus, pA0, pExtent);
                }
                return;
            }

            // 正在拖曳新直線時：比照 _isDrawingSector，只畫即時預覽線、跳過 persistent overlay。
            if (_isDrawingLine)
            {
                Annotator.DrawLine(_lineStartRow, _lineStartCol, _lineCurRow, _lineCurCol, "green");
                return;
            }

            _persistentOverlayAction?.Invoke();
            _selectionHighlightAction?.Invoke();   // 疊在結果之上的選取高亮（編輯器用）
            // 非拖曳中、且沒有 persistent overlay、且非編輯模式時才畫 raw ROI 框。
            // persistent overlay（例如 DrawFittingLayers）有自己的 ROI 繪製邏輯，
            // 兩者同時畫會出現兩個重疊的藍色矩形；編輯模式則由下方綠色編輯框取代。
            if (HasRoi && _persistentOverlayAction == null && !_editActive)
            {
                Annotator.DrawRoiRectangle(_roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol);
            }

            if (_editActive)
            {
                double half = ScreenPxToImage(5);
                double knobGap = ScreenPxToImage(22);
                Annotator.DrawEditRect2(_editCenterRow, _editCenterCol, _editPhi,
                    _editLen1, _editLen2, half, knobGap);
            }

            if (_arcEditActive)
            {
                double half = ScreenPxToImage(5);
                Annotator.DrawEditArc(_arcCr, _arcCc, _arcRadius, _arcA0, _arcExtent, _arcAnnulus, half);
            }
        }

        public void ClearOverlay()
        {
            _persistentOverlayAction = null;
            _selectionHighlightAction = null;
            Redraw();
        }

        /// <summary>設定選取高亮疊加層（畫在量測結果之上、不覆寫它）。傳 null 等同清除。</summary>
        public void SetSelectionHighlight(Action action)
        {
            _selectionHighlightAction = action;
            Redraw();
        }

        /// <summary>清除選取高亮疊加層（不影響 persistent overlay）。</summary>
        public void ClearSelectionHighlight()
        {
            if (_selectionHighlightAction == null) return;
            _selectionHighlightAction = null;
            Redraw();
        }

        /// <summary>只清除 ROI 座標（消除 fallback 藍框），不動 overlay 與編輯狀態。</summary>
        public void ClearRoiCoordinates()
        {
            _roiStartRow = 0;
            _roiStartCol = 0;
            _roiEndRow = 0;
            _roiEndCol = 0;
            Redraw();
        }

        public void ClearRoi()
        {
            _roiStartRow = 0;
            _roiStartCol = 0;
            _roiEndRow = 0;
            _roiEndCol = 0;
            IsRoiMode = false;
            _editActive = false;
            _editMode = Rect2Handle.None;
            _editCallback = null;
            _arcEditActive = false;
            _arcEditMode = ArcHandle.None;
            _arcEditCallback = null;
            IsSectorMode = false;
            _isDrawingSector = false;
            _sectorCallback = null;
            IsLineMode = false;
            _isDrawingLine = false;
            _lineCallback = null;
            ClearOverlay();
        }

        /// <summary>
        /// 解除所有互動輸入模式與其 pending callback（矩形繪製/扇形繪製/rect2 編輯/弧形編輯）。
        /// 切換分頁、開編輯器、切換/啟動任一繪製模式前統一呼叫，避免殘留 mode 誤觸
        /// （含洩漏進共用 helper 的 RecipeEditor）。同一時間至多一個互動模式為 active。
        /// </summary>
        public void DisarmInteractiveModes()
        {
            IsRoiMode = false;
            IsSectorMode = false;          // setter 會一併清 _sectorCallback
            IsLineMode = false;            // setter 會一併清 _lineCallback
            _isDrawingRoi = false;
            _isDrawingSector = false;
            _isDrawingLine = false;
            _roiCallback = null;
            _sectorCallback = null;
            _lineCallback = null;
            EndRect2Edit();
            EndArcEdit();
            ClearRoiCoordinates();         // 消除 fallback 藍框殘留（見既有 tab-switch 註解）
        }

        /// <summary>
        /// 請求一次性 ROI 繪製。畫完後以 callback 傳回 (startRow,startCol,endRow,endCol)，
        /// 不觸發現有的 RoiSelected 事件。先解除其他互動模式，確保模式互斥。
        /// </summary>
        public void RequestRoi(Action<double, double, double, double> callback)
        {
            DisarmInteractiveModes();
            _roiCallback = callback;
            _roiStartRow = _roiEndRow = 0;
            _roiStartCol = _roiEndCol = 0;
            _persistentOverlayAction = null;
            _selectionHighlightAction = null;
            IsRoiMode = true;
            Redraw();
        }

        /// <summary>
        /// 請求一次性「從圓心往外拖曳」建立扇形 ROI。MouseDown 記錄圓心，拖曳中即時預覽
        /// 90° 扇形（方向對齊拖曳方向），MouseUp 若拖曳距離 > 5px 則以 callback 傳回建立好
        /// 的 <see cref="ArcMeasureRoi"/>；否則視為取消，保留 pending 狀態讓使用者可重新拖曳。
        /// 先解除其他互動模式（矩形繪製/rect2/弧形編輯），確保模式互斥。
        /// </summary>
        public void RequestSector(Action<ArcMeasureRoi> callback)
        {
            DisarmInteractiveModes();
            _sectorCallback = callback;
            _persistentOverlayAction = null;
            _selectionHighlightAction = null;
            IsSectorMode = true;
            Redraw();
        }

        /// <summary>
        /// 請求一次性「兩點拖曳」建立直線標稱幾何。MouseDown 記錄起點，拖曳中即時預覽直線，
        /// MouseUp 若拖曳距離 > 5px 則以 callback 傳回 (startRow,startCol,endRow,endCol)；
        /// 否則視為取消，保留 pending 狀態讓使用者可重新拖曳。
        /// 先解除其他互動模式（矩形/扇形繪製/rect2/弧形編輯），確保模式互斥。
        /// </summary>
        public void RequestLine(Action<double, double, double, double> callback)
        {
            DisarmInteractiveModes();
            _lineCallback = callback;
            _persistentOverlayAction = null;
            _selectionHighlightAction = null;
            IsLineMode = true;
            Redraw();
        }

        // 角度慣例與 ArcEditMath.AngleOf 一致：phi = atan2(-(row-cr), col-cc)，
        // 90° 扇形置中於拖曳方向（AngleStart = phi - π/4 (45°)）。annulus 為 0.2*半徑，下限 8px。
        private static void ComputeSectorFromDrag(double centerRow, double centerCol,
            double cursorRow, double cursorCol,
            out double radius, out double annulus, out double angleStart, out double angleExtent)
        {
            double dr = cursorRow - centerRow;
            double dc = cursorCol - centerCol;
            radius = Math.Sqrt(dr * dr + dc * dc);
            annulus = Math.Max(8.0, 0.2 * radius);
            double phi = Math.Atan2(-dr, dc);
            angleExtent = Math.PI / 2.0;
            angleStart = phi - Math.PI / 4.0;
        }

        public void SetPersistentOverlayAction(Action action)
        {
            _persistentOverlayAction = action;
            Redraw();
        }

        /// <summary>
        /// 把視窗目前內容（影像 + 已畫上的 overlay）原樣存成 PNG，供報表嵌圖。
        /// 呼叫端必須先完成繪製（SetPersistentOverlayAction 內含同步 Redraw）再呼叫。
        /// 失敗（無影像／HALCON 例外／寫檔失敗）一律回傳 null 而非丟例外——
        /// 存圖只是報表的附加品，不可讓量測流程因此中斷。
        /// </summary>
        /// <returns>實際寫出的檔案路徑；失敗時 null。</returns>
        public string DumpWindowToPng(string filePath)
        {
            if (CurrentImage == null || string.IsNullOrEmpty(filePath)) return null;

            HObject dump = null;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(filePath));
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                // dump_window_image ( : Image : WindowHandle : ) —— 取視窗內容（含 overlay）為 image 物件。
                HOperatorSet.DumpWindowImage(out dump, _window);
                // write_image ( Image : : Format, FillColor, FileName : )
                HOperatorSet.WriteImage(dump, "png", 0, filePath);

                // HALCON 會在副檔名缺漏時自行補上，故兩種可能路徑都確認一次，回傳真正存在的那個。
                if (System.IO.File.Exists(filePath)) return filePath;
                string withExt = filePath + ".png";
                return System.IO.File.Exists(withExt) ? withExt : null;
            }
            catch (HalconException)
            {
                return null;
            }
            catch (System.IO.IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            finally
            {
                if (dump != null) dump.Dispose();
            }
        }

        /// <summary>螢幕像素 → 影像像素（依目前縮放），用於把手大小與命中容差。</summary>
        public double ScreenPxToImage(double px)
        {
            if (_control.Width <= 0) return px;
            return px * (_imgCol2 - _imgCol1) / _control.Width;
        }

        /// <summary>開始/取代可編輯 rect2，進入編輯模式並重繪。onChanged 為本次編輯的回呼擁有者。</summary>
        public void BeginRect2Edit(double cr, double cc, double phi, double l1, double l2,
            Action<double, double, double, double, double> onChanged)
        {
            _editCenterRow = cr;
            _editCenterCol = cc;
            _editPhi = phi;
            _editLen1 = l1;
            _editLen2 = l2;
            _editCallback = onChanged;
            _editMode = Rect2Handle.None;
            _editActive = true;
            _arcEditActive = false;
            IsSectorMode = false;   // 進入編輯即解除 pending 扇形繪製（模式互斥）；setter 清 callback
            Redraw();
        }

        /// <summary>結束編輯模式（隱藏把手），清回呼並重繪。</summary>
        public void EndRect2Edit()
        {
            _editActive = false;
            _editMode = Rect2Handle.None;
            _editCallback = null;
            Redraw();
        }

        /// <summary>開始/取代可編輯弧形，進入弧形編輯模式並重繪。與 rect2 編輯互斥。</summary>
        public void BeginArcEdit(double cr, double cc, double radius, double a0, double extent,
            double annulus, Action<double, double, double, double, double, double> onChanged)
        {
            _editActive = false;                 // 關閉 rect2 編輯，避免兩種模式同時 active
            _editMode = Rect2Handle.None;
            _arcCr = cr; _arcCc = cc; _arcRadius = radius;
            _arcA0 = a0; _arcExtent = extent; _arcAnnulus = annulus;
            _arcEditCallback = onChanged;
            _arcEditMode = ArcHandle.None;
            _arcEditActive = true;
            IsSectorMode = false;   // 進入編輯即解除 pending 扇形繪製（模式互斥）；setter 清 callback
            Redraw();
        }

        /// <summary>結束弧形編輯模式（隱藏把手），清回呼並重繪。</summary>
        public void EndArcEdit()
        {
            _arcEditActive = false;
            _arcEditMode = ArcHandle.None;
            _arcEditCallback = null;
            Redraw();
        }

        private void OnSizeChanged(object sender, EventArgs e)
        {
            if (CurrentImage == null || _isResizing) return;
            _isResizing = true;
            try
            {
                FitToWindow();
                Redraw();
            }
            finally
            {
                _isResizing = false;
            }
        }
        // ─── 標準 WinForms 滑鼠事件（非 Halcon 特定事件）─────

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (CurrentImage == null) return;
            double zoom = e.Delta > 0 ? 0.9 : 1.1;
            double mRow = _imgRow1 + ((double)e.Y / _control.Height) * (_imgRow2 - _imgRow1);
            double mCol = _imgCol1 + ((double)e.X / _control.Width) * (_imgCol2 - _imgCol1);
            double hh = (_imgRow2 - _imgRow1) / (2.0 * zoom);
            double hw = (_imgCol2 - _imgCol1) / (2.0 * zoom);
            _imgRow1 = mRow - hh; _imgRow2 = mRow + hh;
            _imgCol1 = mCol - hw; _imgCol2 = mCol + hw;
            Redraw();
        }
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (CurrentImage == null) return;
            if (e.Button == MouseButtons.Right) { _isPanning = true; _panStartX = e.X; _panStartY = e.Y; return; }

            if (e.Button == MouseButtons.Left && _editActive && !IsRoiMode)
            {
                PixelToImage(e.X, e.Y, out double pr, out double pc);
                double tol = ScreenPxToImage(8);
                double knobGap = ScreenPxToImage(22);
                Rect2Handle h = Rect2EditMath.HitTest(pr, pc, _editCenterRow, _editCenterCol,
                    _editPhi, _editLen1, _editLen2, tol, knobGap);
                if (h != Rect2Handle.None)
                {
                    _editMode = h;
                    _editLastRow = pr;
                    _editLastCol = pc;
                    return;
                }
            }

            if (e.Button == MouseButtons.Left && _arcEditActive && !IsRoiMode)
            {
                PixelToImage(e.X, e.Y, out double pr, out double pc);
                double tol = ScreenPxToImage(8);
                ArcHandle h = ArcEditMath.HitTest(pr, pc, _arcCr, _arcCc,
                    _arcRadius, _arcA0, _arcExtent, _arcAnnulus, tol);
                if (h != ArcHandle.None)
                {
                    _arcEditMode = h;
                    _arcLastRow = pr;
                    _arcLastCol = pc;
                    return;
                }
            }

            if (e.Button == MouseButtons.Left && IsRoiMode)
            {
                _isDrawingRoi = true;
                PixelToImage(e.X, e.Y, out _roiStartRow, out _roiStartCol);
                _roiEndRow = _roiStartRow; _roiEndCol = _roiStartCol;
            }

            if (e.Button == MouseButtons.Left && IsSectorMode)
            {
                _isDrawingSector = true;
                PixelToImage(e.X, e.Y, out _sectorCenterRow, out _sectorCenterCol);
                _sectorCursorRow = _sectorCenterRow;
                _sectorCursorCol = _sectorCenterCol;
            }

            if (e.Button == MouseButtons.Left && IsLineMode)
            {
                _isDrawingLine = true;
                PixelToImage(e.X, e.Y, out _lineStartRow, out _lineStartCol);
                _lineCurRow = _lineStartRow;
                _lineCurCol = _lineStartCol;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (CurrentImage == null) return;
            PixelToImage(e.X, e.Y, out double row, out double col);
            MouseRow = row; MouseCol = col;
            MouseMoved?.Invoke(row, col);

            if (_isPanning)
            {
                int dx = e.X - _panStartX; int dy = e.Y - _panStartY;
                double rRange = _imgRow2 - _imgRow1; double cRange = _imgCol2 - _imgCol1;
                _imgRow1 += -dy * rRange / _control.Height; _imgRow2 += -dy * rRange / _control.Height;
                _imgCol1 += -dx * cRange / _control.Width; _imgCol2 += -dx * cRange / _control.Width;
                _panStartX = e.X; _panStartY = e.Y;
                Redraw();
            }
            else if (_isDrawingRoi)
            {
                // 拖曳中的 ROI 框由 Redraw() 統一繪製（_roiEnd 已先更新、HasRoi 成立）。
                PixelToImage(e.X, e.Y, out _roiEndRow, out _roiEndCol);
                Redraw();
            }
            else if (_isDrawingSector)
            {
                // 拖曳中的扇形預覽由 Redraw() 統一繪製（_sectorCursor 已先更新）。
                PixelToImage(e.X, e.Y, out _sectorCursorRow, out _sectorCursorCol);
                Redraw();
            }
            else if (_isDrawingLine)
            {
                // 拖曳中的直線預覽由 Redraw() 統一繪製（_lineCur 已先更新）。
                PixelToImage(e.X, e.Y, out _lineCurRow, out _lineCurCol);
                Redraw();
            }
            else if (_editMode != Rect2Handle.None)
            {
                if (_editMode == Rect2Handle.Body)
                {
                    _editCenterRow += row - _editLastRow;
                    _editCenterCol += col - _editLastCol;
                    _editLastRow = row;
                    _editLastCol = col;
                }
                else if (_editMode == Rect2Handle.Rotate)
                {
                    _editPhi = Rect2EditMath.Rotate(row, col, _editCenterRow, _editCenterCol);
                }
                else
                {
                    Rect2EditMath.ApplyResize(_editMode, row, col, _editCenterRow, _editCenterCol,
                        _editPhi, ref _editLen1, ref _editLen2);
                }
                _editCallback?.Invoke(_editCenterRow, _editCenterCol, _editPhi, _editLen1, _editLen2);
                Redraw();
            }
            else if (_arcEditMode != ArcHandle.None)
            {
                if (_arcEditMode == ArcHandle.Center)
                {
                    _arcCr += row - _arcLastRow;
                    _arcCc += col - _arcLastCol;
                    _arcLastRow = row;
                    _arcLastCol = col;
                }
                else
                {
                    ArcEditMath.ApplyDrag(_arcEditMode, row, col, _arcCr, _arcCc,
                        ref _arcRadius, ref _arcA0, ref _arcExtent, ref _arcAnnulus);
                }
                _arcEditCallback?.Invoke(_arcCr, _arcCc, _arcRadius, _arcA0, _arcExtent, _arcAnnulus);
                Redraw();
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _isPanning = false;
            if (_editMode != Rect2Handle.None)
            {
                _editMode = Rect2Handle.None;
                return;
            }
            if (_arcEditMode != ArcHandle.None)
            {
                _arcEditMode = ArcHandle.None;
                return;
            }
            if (_isDrawingRoi)
            {
                _isDrawingRoi = false;
                PixelToImage(e.X, e.Y, out _roiEndRow, out _roiEndCol);
                if (Math.Abs(_roiEndRow - _roiStartRow) > 5 && Math.Abs(_roiEndCol - _roiStartCol) > 5)
                {
                    if (_roiCallback != null)
                    {
                        var cb = _roiCallback;
                        _roiCallback = null;
                        IsRoiMode = false;
                        cb(_roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol);
                    }
                    else
                    {
                        RoiSelected?.Invoke(_roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol);
                    }
                }
            }

            if (_isDrawingSector)
            {
                _isDrawingSector = false;
                PixelToImage(e.X, e.Y, out _sectorCursorRow, out _sectorCursorCol);
                ComputeSectorFromDrag(_sectorCenterRow, _sectorCenterCol, _sectorCursorRow, _sectorCursorCol,
                    out double radius, out double annulus, out double a0, out double extent);
                // 拖曳距離 > 5px 才視為成立，否則保留 pending 狀態讓使用者可重新拖曳（比照 RequestRoi）。
                if (radius > 5.0)
                {
                    // 先取出 callback 再清模式：IsSectorMode 的 setter 會 null 掉 _sectorCallback，
                    // 若先設 IsSectorMode=false 再讀 _sectorCallback 會拿到 null → 手勢無回呼 →
                    // 放開後不會建立扇形（比照矩形流程 line 543 先 capture 再清狀態）。
                    var cb = _sectorCallback;
                    _sectorCallback = null;
                    IsSectorMode = false;
                    var roi = new ArcMeasureRoi
                    {
                        CenterRow = _sectorCenterRow,
                        CenterCol = _sectorCenterCol,
                        Radius = radius,
                        AnnulusRadius = annulus,
                        AngleStart = a0,
                        AngleExtent = extent
                    };
                    cb?.Invoke(roi);
                }
                // 成功時 callback 鏈（OnSectorRoiCreated → 勾選互動編輯 → BeginArcEdit）已重繪；
                // 這裡的 Redraw 主要服務「取消」路徑（radius<=5，未觸發 callback），清掉最後一幀預覽。
                Redraw();
            }

            if (_isDrawingLine)
            {
                _isDrawingLine = false;
                PixelToImage(e.X, e.Y, out _lineCurRow, out _lineCurCol);
                double dr = _lineCurRow - _lineStartRow, dc = _lineCurCol - _lineStartCol;
                // 拖曳距離 > 5px 才視為成立，否則保留 pending 狀態讓使用者可重新拖曳（比照 RequestSector）。
                if (Math.Sqrt(dr * dr + dc * dc) > 5.0)
                {
                    // 先取出 callback 再清模式：IsLineMode 的 setter 會 null 掉 _lineCallback，
                    // 若先設 IsLineMode=false 再讀 _lineCallback 會拿到 null → 手勢無回呼（比照扇形 line 567）。
                    var cb = _lineCallback;
                    _lineCallback = null;
                    IsLineMode = false;
                    cb?.Invoke(_lineStartRow, _lineStartCol, _lineCurRow, _lineCurCol);
                }
                Redraw();
            }
        }

        private void PixelToImage(double px, double py, out double row, out double col)
        {
            row = _imgRow1 + (py / _control.Height) * (_imgRow2 - _imgRow1);
            col = _imgCol1 + (px / _control.Width) * (_imgCol2 - _imgCol1);
        }

        public RegionInfo GetCurrentRoi() => !HasRoi ? null : new RegionInfo
        {
            Row1 = Math.Min(_roiStartRow, _roiEndRow),
            Col1 = Math.Min(_roiStartCol, _roiEndCol),
            Row2 = Math.Max(_roiStartRow, _roiEndRow),
            Col2 = Math.Max(_roiStartCol, _roiEndCol),
        };

        public void Dispose()
        {
            _persistentOverlayAction = null;
            CurrentImage?.Dispose();
        }
    }

    public class RegionInfo
    {
        public double Row1 { get; set; }
        public double Col1 { get; set; }
        public double Row2 { get; set; }
        public double Col2 { get; set; }
    }
}
