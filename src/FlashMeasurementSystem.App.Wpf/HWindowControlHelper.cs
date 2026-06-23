using System;
using System.Windows.Forms;
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
        public bool HasRoi => Math.Abs(_roiEndRow - _roiStartRow) > 0.1 && Math.Abs(_roiEndCol - _roiStartCol) > 0.1;
        public double MouseRow { get; private set; }
        public double MouseCol { get; private set; }
        public OverlayAnnotator Annotator { get; }

        public event Action<double, double, double, double> RoiSelected;
        public event Action<double, double> MouseMoved;

        private int _imageWidth, _imageHeight;
        private double _imgRow1, _imgCol1, _imgRow2, _imgCol2;
        private bool _isDrawingRoi, _isPanning;
        private double _roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol;
        private int _panStartX, _panStartY;
        private bool _isResizing;
        private Action _persistentOverlayAction;
        private Action<double, double, double, double> _roiCallback;

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

            _persistentOverlayAction?.Invoke();
            // 非拖曳中、且沒有 persistent overlay 時才畫 raw ROI 框。
            // persistent overlay（例如 DrawFittingLayers）有自己的 ROI 繪製邏輯，
            // 兩者同時畫會出現兩個重疊的藍色矩形。
            if (HasRoi && _persistentOverlayAction == null)
            {
                Annotator.DrawRoiRectangle(_roiStartRow, _roiStartCol, _roiEndRow, _roiEndCol);
            }
        }

        public void ClearOverlay()
        {
            _persistentOverlayAction = null;
            Redraw();
        }

        public void ClearRoi()
        {
            _roiStartRow = 0;
            _roiStartCol = 0;
            _roiEndRow = 0;
            _roiEndCol = 0;
            IsRoiMode = false;
            ClearOverlay();
        }

        /// <summary>
        /// 請求一次性 ROI 繪製。畫完後以 callback 傳回 (startRow,startCol,endRow,endCol)，
        /// 不觸發現有的 RoiSelected 事件。若已有 pending request，舊 request 會被取代。
        /// </summary>
        public void RequestRoi(Action<double, double, double, double> callback)
        {
            _roiCallback = callback;
            _roiStartRow = _roiEndRow = 0;
            _roiStartCol = _roiEndCol = 0;
            _persistentOverlayAction = null;
            IsRoiMode = true;
            Redraw();
        }

        public void SetPersistentOverlayAction(Action action)
        {
            _persistentOverlayAction = action;
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
            if (e.Button == MouseButtons.Left && IsRoiMode)
            {
                _isDrawingRoi = true;
                PixelToImage(e.X, e.Y, out _roiStartRow, out _roiStartCol);
                _roiEndRow = _roiStartRow; _roiEndCol = _roiStartCol;
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
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _isPanning = false;
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
