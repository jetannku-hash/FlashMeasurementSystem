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
        /// <summary>
        /// true 期間下一次左鍵按下會開始拉矩形 ROI。設為 true 時把互動歸屬記到目前最上層的 owner
        /// （lease），關閉該 owner 時可連同 pending 手勢一起收掉。
        /// </summary>
        public bool IsRoiMode
        {
            get => _isRoiMode;
            set
            {
                _isRoiMode = value;
                if (value) _interactionOwner = _baseLease;
            }
        }
        private bool _isRoiMode;
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
                if (value) _interactionOwner = _baseLease;
                else _sectorCallback = null;
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
                if (value) _interactionOwner = _baseLease;
                else _lineCallback = null;
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
        // ── overlay ownership leasing ────────────────────────────────────────
        // 每個 caller（主視窗 + 各 modeless 編輯器）以 AcquireOverlay 取得一張自己的圖層。
        // 圖層堆疊：_leases[0] 永遠是主視窗的 base lease，之後每 Acquire 一個編輯器就疊一層。
        // 繪製時由上往下找第一個非 null 的 persistent/highlight → 上層清空時自動露出下層
        // （關閉編輯器即恢復主視窗的量測結果 overlay，不必任何人記帳）。
        private readonly System.Collections.Generic.List<OverlayLease> _leases =
            new System.Collections.Generic.List<OverlayLease>();
        private readonly OverlayLease _baseLease;
        // 編輯把手/pending 手勢的歸屬 owner。滑鼠只有一個，故互動狀態全域唯一，
        // 但「誰有權拆除它」由此欄位決定：釋放 lease 時只收掉自己武裝的互動。
        private OverlayLease _interactionOwner;
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
            // base lease：主視窗的長期圖層。所有舊 API（SetPersistentOverlayAction 等）
            // 都寫到「目前最上層」的 lease；沒有編輯器開著時最上層就是它。
            _baseLease = new OverlayLease(this, "MainWindow");
            _leases.Add(_baseLease);

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
            // 換圖是全域事件（所有 owner 的 overlay 都以舊圖座標繪製），故清掉每一層而非只清最上層。
            for (int i = 0; i < _leases.Count; i++)
            {
                _leases[i].Persistent = null;
                _leases[i].Highlight = null;
            }
            _interactionOwner = null;
            _editActive = false;
            _editMode = Rect2Handle.None;
            _editCallback = null;
            // 對稱清除弧形編輯狀態——否則換新圖後仍會以舊座標重繪弧形把手（H1）。
            _arcEditActive = false;
            _arcEditMode = ArcHandle.None;
            _arcEditCallback = null;
            // 對稱清除矩形 ROI 擷取狀態——這一組原本被漏掉，後果比另外兩組嚴重：
            // 換圖同時把 _interactionOwner 清成 null，而 ReleaseLease 只在 owner 相符時才解除
            // 手勢，於是編輯器關閉時不會收回自己的 _roiCallback；之後在影像上拖曳，callback
            // 會打進已 Dispose 的編輯器表單 → ObjectDisposedException（由 MouseUp 同步拋出）。
            IsRoiMode = false;
            _isDrawingRoi = false;
            _roiCallback = null;
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

            Action persistent = EffectivePersistent();
            persistent?.Invoke();
            EffectiveHighlight()?.Invoke();   // 疊在結果之上的選取高亮（編輯器用）
            // 非拖曳中、且沒有 persistent overlay、且非編輯模式時才畫 raw ROI 框。
            // persistent overlay（例如 DrawFittingLayers）有自己的 ROI 繪製邏輯，
            // 兩者同時畫會出現兩個重疊的藍色矩形；編輯模式則由下方綠色編輯框取代。
            if (HasRoi && persistent == null && !_editActive)
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

        // ── overlay lease：取得/釋放圖層所有權 ─────────────────────────────────
        // 每個 modeless 視窗開啟時 AcquireOverlay，關閉時 Dispose。lease 釋放後所有寫入
        // 都是 no-op（背景視窗的延遲 callback 再也洗不掉前景的 overlay），且釋放時只收掉
        // 自己那一層與自己武裝的互動，下層（主視窗）的 overlay 會自動重新顯示。

        /// <summary>取得一張屬於 <paramref name="ownerName"/> 的 overlay/編輯圖層。Dispose 即釋放。</summary>
        public IOverlayLease AcquireOverlay(string ownerName)
        {
            var lease = new OverlayLease(this, ownerName);
            _leases.Add(lease);
            // 刻意不 Redraw：新圖層是空的，繪製時會自然穿透到下層，畫面在 owner 真正寫入前不變。
            return lease;
        }

        // 僅供「誰在最上層」的查詢（OverlayLease.IsActive）。
        // helper 層的公開方法一律作用在 _baseLease——那些方法只有 MainWindow 呼叫，
        // 編輯器一律走 _lease.*。原本它們指向 ActiveLease，於是只要任一編輯器開著，
        // 主視窗的繪製與武裝就全部記到編輯器那一層：關閉編輯器時主視窗的 overlay 憑空消失、
        // checkbox 與實際狀態不同步、主視窗的 End*Edit 還會拆掉編輯器正在用的把手。
        private OverlayLease ActiveLease => _leases[_leases.Count - 1];

        private Action EffectivePersistent()
        {
            for (int i = _leases.Count - 1; i >= 0; i--)
                if (_leases[i].Persistent != null) return _leases[i].Persistent;
            return null;
        }

        private Action EffectiveHighlight()
        {
            for (int i = _leases.Count - 1; i >= 0; i--)
                if (_leases[i].Highlight != null) return _leases[i].Highlight;
            return null;
        }

        private void ReleaseLease(OverlayLease lease)
        {
            if (ReferenceEquals(_interactionOwner, lease)) ReleaseOwnedInteractions();
            lease.Persistent = null;
            lease.Highlight = null;
            if (!ReferenceEquals(lease, _baseLease)) _leases.Remove(lease);
            Redraw();   // 上層消失 → 露出下層（主視窗量測結果 overlay 自動回來）
        }

        /// <summary>收掉目前 owner 武裝的編輯把手與 pending 手勢（不動任何 overlay 圖層）。</summary>
        private void ReleaseOwnedInteractions()
        {
            _editActive = false;
            _editMode = Rect2Handle.None;
            _editCallback = null;
            _arcEditActive = false;
            _arcEditMode = ArcHandle.None;
            _arcEditCallback = null;
            _isRoiMode = false;
            _isSectorMode = false;
            _isLineMode = false;
            _isDrawingRoi = false;
            _isDrawingSector = false;
            _isDrawingLine = false;
            _roiCallback = null;
            _sectorCallback = null;
            _lineCallback = null;
            _interactionOwner = null;
        }

        /// <summary>清除主視窗自己那一層的 overlay（不動編輯把手，也不動編輯器的圖層）。</summary>
        public void ClearOverlay()
        {
            _baseLease.Persistent = null;
            _baseLease.Highlight = null;
            Redraw();
        }

        /// <summary>設定選取高亮疊加層（畫在量測結果之上、不覆寫它）。傳 null 等同清除。</summary>
        public void SetSelectionHighlight(Action action)
        {
            _baseLease.Highlight = action;
            Redraw();
        }

        /// <summary>清除選取高亮疊加層（不影響 persistent overlay）。</summary>
        public void ClearSelectionHighlight()
        {
            if (_baseLease.Highlight == null) return;
            _baseLease.Highlight = null;
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
            ReleaseOwnedInteractions();
            ClearOverlay();
        }

        /// <summary>
        /// 解除所有互動輸入模式與其 pending callback（矩形繪製/扇形繪製/rect2 編輯/弧形編輯）。
        /// 切換分頁、開編輯器、切換/啟動任一繪製模式前統一呼叫，避免殘留 mode 誤觸
        /// （含洩漏進共用 helper 的 RecipeEditor）。同一時間至多一個互動模式為 active。
        /// </summary>
        public void DisarmInteractiveModes()
        {
            // 滑鼠只有一個 → 互動模式本質全域互斥，故此處無條件全清（不分 owner），
            // 只是把「誰擁有下一個互動」的記帳一併歸零（見 ReleaseOwnedInteractions）。
            ReleaseOwnedInteractions();
            ClearRoiCoordinates();         // 消除 fallback 藍框殘留（見既有 tab-switch 註解）+ Redraw
        }

        /// <summary>
        /// 請求一次性 ROI 繪製。畫完後以 callback 傳回 (startRow,startCol,endRow,endCol)，
        /// 不觸發現有的 RoiSelected 事件。先解除其他互動模式，確保模式互斥。
        /// </summary>
        public void RequestRoi(Action<double, double, double, double> callback)
            => RequestRoiCore(_baseLease, callback);

        private void RequestRoiCore(OverlayLease owner, Action<double, double, double, double> callback)
        {
            DisarmInteractiveModes();
            _roiCallback = callback;
            _roiStartRow = _roiEndRow = 0;
            _roiStartCol = _roiEndCol = 0;
            owner.Persistent = null;
            owner.Highlight = null;
            _isRoiMode = true;
            _interactionOwner = owner;
            Redraw();
        }

        /// <summary>
        /// 請求一次性「從圓心往外拖曳」建立扇形 ROI。MouseDown 記錄圓心，拖曳中即時預覽
        /// 90° 扇形（方向對齊拖曳方向），MouseUp 若拖曳距離 > 5px 則以 callback 傳回建立好
        /// 的 <see cref="ArcMeasureRoi"/>；否則視為取消，保留 pending 狀態讓使用者可重新拖曳。
        /// 先解除其他互動模式（矩形繪製/rect2/弧形編輯），確保模式互斥。
        /// </summary>
        public void RequestSector(Action<ArcMeasureRoi> callback)
            => RequestSectorCore(_baseLease, callback);

        private void RequestSectorCore(OverlayLease owner, Action<ArcMeasureRoi> callback)
        {
            DisarmInteractiveModes();
            _sectorCallback = callback;
            owner.Persistent = null;
            owner.Highlight = null;
            _isSectorMode = true;
            _interactionOwner = owner;
            Redraw();
        }

        /// <summary>
        /// 請求一次性「兩點拖曳」建立直線標稱幾何。MouseDown 記錄起點，拖曳中即時預覽直線，
        /// MouseUp 若拖曳距離 > 5px 則以 callback 傳回 (startRow,startCol,endRow,endCol)；
        /// 否則視為取消，保留 pending 狀態讓使用者可重新拖曳。
        /// 先解除其他互動模式（矩形/扇形繪製/rect2/弧形編輯），確保模式互斥。
        /// </summary>
        public void RequestLine(Action<double, double, double, double> callback)
            => RequestLineCore(_baseLease, callback);

        private void RequestLineCore(OverlayLease owner, Action<double, double, double, double> callback)
        {
            DisarmInteractiveModes();
            _lineCallback = callback;
            owner.Persistent = null;
            owner.Highlight = null;
            _isLineMode = true;
            _interactionOwner = owner;
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

        /// <summary>
        /// 相容路徑：寫進「目前最上層」的 overlay 圖層。沒有編輯器開著時就是主視窗自己的圖層；
        /// 有編輯器開著時（例如量測模型編輯器按「試測」回呼主視窗繪製）寫進該編輯器的圖層，
        /// 因此關掉編輯器會連同這次繪製一起收回、露出主視窗先前的結果 overlay。
        /// </summary>
        public void SetPersistentOverlayAction(Action action)
        {
            _baseLease.Persistent = action;
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
            => BeginRect2EditCore(_baseLease, cr, cc, phi, l1, l2, onChanged);

        private void BeginRect2EditCore(OverlayLease owner, double cr, double cc, double phi, double l1, double l2,
            Action<double, double, double, double, double> onChanged)
        {
            _interactionOwner = owner;
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
            // 只結束「主視窗自己武裝的」把手。原本是無條件全清，於是主視窗任何一次
            // Run Recipe / 換圖都會把編輯器正在進行的 rect2 編輯把手當場拆掉，
            // 而編輯器毫不知情（仍顯示該工具被選取）。
            if (!ReferenceEquals(_interactionOwner, _baseLease)) return;
            EndRect2EditCore();
        }

        private void EndRect2EditCore()
        {
            _editActive = false;
            _editMode = Rect2Handle.None;
            _editCallback = null;
            Redraw();
        }

        /// <summary>開始/取代可編輯弧形，進入弧形編輯模式並重繪。與 rect2 編輯互斥。</summary>
        public void BeginArcEdit(double cr, double cc, double radius, double a0, double extent,
            double annulus, Action<double, double, double, double, double, double> onChanged)
            => BeginArcEditCore(_baseLease, cr, cc, radius, a0, extent, annulus, onChanged);

        private void BeginArcEditCore(OverlayLease owner, double cr, double cc, double radius, double a0, double extent,
            double annulus, Action<double, double, double, double, double, double> onChanged)
        {
            _interactionOwner = owner;
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
            // 同 EndRect2Edit：只結束主視窗自己武裝的弧形把手。
            if (!ReferenceEquals(_interactionOwner, _baseLease)) return;
            EndArcEditCore();
        }

        private void EndArcEditCore()
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
            for (int i = 0; i < _leases.Count; i++)
            {
                _leases[i].Persistent = null;
                _leases[i].Highlight = null;
            }
            CurrentImage?.Dispose();
        }

        /// <summary>
        /// 單一 caller 對共用影像視窗的 overlay/編輯/手勢所有權。釋放（Dispose）後所有寫入都是
        /// no-op，且只收掉自己那一層與自己武裝的互動，下層 owner 的 overlay 會自動重新顯示。
        /// </summary>
        private sealed class OverlayLease : IOverlayLease
        {
            private readonly HWindowControlHelper _h;
            private bool _released;
            internal Action Persistent;
            internal Action Highlight;

            public string OwnerName { get; }

            internal OverlayLease(HWindowControlHelper helper, string ownerName)
            {
                _h = helper;
                OwnerName = string.IsNullOrEmpty(ownerName) ? "(anonymous)" : ownerName;
            }

            // 已釋放的 lease 一律失效：背景/已關閉視窗的延遲 callback 打進來全部靜默丟棄。
            private bool Live => !_released;

            public bool IsActive => Live && ReferenceEquals(_h.ActiveLease, this);
            public bool IsEditingRect2 => Live && _h._editActive && ReferenceEquals(_h._interactionOwner, this);
            public bool IsEditingArc => Live && _h._arcEditActive && ReferenceEquals(_h._interactionOwner, this);

            public void SetPersistentOverlay(Action action)
            {
                if (!Live) return;
                Persistent = action;
                _h.Redraw();
            }

            public void SetSelectionHighlight(Action action)
            {
                if (!Live) return;
                Highlight = action;
                _h.Redraw();
            }

            public void ClearSelectionHighlight()
            {
                if (!Live || Highlight == null) return;
                Highlight = null;
                _h.Redraw();
            }

            public void ClearPersistentOverlay()
            {
                if (!Live || Persistent == null) return;
                Persistent = null;
                _h.Redraw();
            }

            public void BeginRect2Edit(double centerRow, double centerCol, double phi, double len1, double len2,
                Action<double, double, double, double, double> onChanged)
            {
                if (!Live) return;
                _h.BeginRect2EditCore(this, centerRow, centerCol, phi, len1, len2, onChanged);
            }

            // 只能結束「自己武裝的」編輯把手：已關閉的編輯器或別的 owner 呼叫時為 no-op，
            // 不會拆掉前景視窗正在進行的編輯。
            public void EndRect2Edit()
            {
                if (!IsEditingRect2) return;
                _h.EndRect2EditCore();
            }

            public void BeginArcEdit(double centerRow, double centerCol, double radius, double angleStart,
                double angleExtent, double annulus,
                Action<double, double, double, double, double, double> onChanged)
            {
                if (!Live) return;
                _h.BeginArcEditCore(this, centerRow, centerCol, radius, angleStart, angleExtent, annulus, onChanged);
            }

            public void EndArcEdit()
            {
                if (!IsEditingArc) return;
                _h.EndArcEditCore();
            }

            public void RequestRoi(Action<double, double, double, double> callback)
            {
                if (!Live) return;
                _h.RequestRoiCore(this, callback);
            }

            public void RequestSector(Action<ArcMeasureRoi> callback)
            {
                if (!Live) return;
                _h.RequestSectorCore(this, callback);
            }

            public void RequestLine(Action<double, double, double, double> callback)
            {
                if (!Live) return;
                _h.RequestLineCore(this, callback);
            }

            public void Disarm()
            {
                if (!Live) return;
                _h.DisarmInteractiveModes();
            }

            public void Clear()
            {
                if (!Live) return;
                if (ReferenceEquals(_h._interactionOwner, this)) _h.ReleaseOwnedInteractions();
                Persistent = null;
                Highlight = null;
                _h.Redraw();
            }

            public void Dispose()
            {
                if (_released) return;
                _released = true;
                _h.ReleaseLease(this);
            }
        }
    }

    /// <summary>
    /// 共用影像視窗（<see cref="HWindowControlHelper"/>）的 overlay/編輯/手勢所有權租約。
    /// 每個 modeless 視窗開啟時 AcquireOverlay 取得一份、關閉時 Dispose。
    /// 契約：(1) 每個 owner 有自己的 overlay 圖層，彼此不覆寫；(2) 釋放後任何呼叫都是 no-op；
    /// (3) 釋放時只收自己的圖層與自己武裝的編輯/手勢，下層 owner 的 overlay 自動重新顯示。
    /// </summary>
    public interface IOverlayLease : IDisposable
    {
        /// <summary>診斷用的擁有者名稱。</summary>
        string OwnerName { get; }
        /// <summary>本租約仍有效且位於圖層堆疊最上層。</summary>
        bool IsActive { get; }
        /// <summary>rect2 編輯把手正由「本租約」持有。</summary>
        bool IsEditingRect2 { get; }
        /// <summary>弧形編輯把手正由「本租約」持有。</summary>
        bool IsEditingArc { get; }

        void SetPersistentOverlay(Action action);
        void ClearPersistentOverlay();
        void SetSelectionHighlight(Action action);
        void ClearSelectionHighlight();

        void BeginRect2Edit(double centerRow, double centerCol, double phi, double len1, double len2,
            Action<double, double, double, double, double> onChanged);
        void EndRect2Edit();
        void BeginArcEdit(double centerRow, double centerCol, double radius, double angleStart,
            double angleExtent, double annulus,
            Action<double, double, double, double, double, double> onChanged);
        void EndArcEdit();

        void RequestRoi(Action<double, double, double, double> callback);
        void RequestSector(Action<ArcMeasureRoi> callback);
        void RequestLine(Action<double, double, double, double> callback);

        /// <summary>解除所有 pending 互動手勢/編輯（滑鼠全域互斥，見 DisarmInteractiveModes）。</summary>
        void Disarm();
        /// <summary>清掉本 owner 的 overlay 圖層與自己武裝的編輯/手勢。</summary>
        void Clear();
    }

    public class RegionInfo
    {
        public double Row1 { get; set; }
        public double Col1 { get; set; }
        public double Row2 { get; set; }
        public double Col2 { get; set; }
    }
}
