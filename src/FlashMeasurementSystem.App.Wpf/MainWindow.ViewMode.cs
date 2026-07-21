using System;
using System.Drawing;
using System.Windows.Forms;

namespace FlashMeasurementSystem
{
    /// <summary>
    /// 操作員／工程師介面分流（計畫書 docs/superpowers/plans/2026-07-20-operator-engineer-ui-split.md）。
    ///
    /// 分流軸線是「角色」而非「常用度」，判準為：這個操作按下去會不會改變判定基準。
    /// 例如「校正」不常用但會動到所有量測的尺度基準，必須擋在操作員之外；
    /// 「Draw ROI」很常用但屬工程行為，不該出現在生產畫面。
    ///
    /// 本檔以 partial class 獨立於 MainWindow.cs（該檔已逾 3400 行），
    /// 讓模式切換的狀態與套用邏輯集中在單一位置。
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>使用介面模式。</summary>
        private enum ViewMode
        {
            /// <summary>操作員：只有生產閉環所需的動作。</summary>
            Operator,

            /// <summary>工程師：完整功能，含會改變判定基準的設定動作。</summary>
            Engineering
        }

        // Phase 1 預設為 Engineering，使行為與導入前完全一致（此時操作員版面尚未建立）。
        // Phase 3 收攏工程功能後改為預設 Operator——共用機台上，安全的預設是權限較小的那個。
        private ViewMode _viewMode = ViewMode.Engineering;

        private MenuStrip _viewModeMenuStrip;
        private ToolStripMenuItem _engineeringModeMenuItem;

        // 標題列由「基底標題」+「模式後綴」組成。基底由 OnLoad 依 HALCON 版本設定
        // （見 SetBaseWindowTitle 的呼叫端），模式後綴由本檔負責，兩者互不覆寫。
        private string _baseWindowTitle = "Flash Measurement System - Template Matching";

        // 右欄容器：工程分頁（rightPanel）與操作員面板共用 mainTableLayout 的 (1,1) 格。
        // TableLayoutPanel 單一儲存格不支援兩個控制項（直接放會把第二個擠到別格，
        // 見 Designer 內 imageHostPanel 的同款註解），故以一個普通 Panel 承載兩者並切換 Visible。
        private Panel _rightHostPanel;
        private Panel _operatorPanel;
        private Label _operatorResultLabel;

        /// <summary>
        /// 建立模式切換選單。比照既有 topToolbar 的做法以程式碼建構，不動 Designer.cs
        /// （該檔會被 VS 設計器自動重生，手改易被覆蓋）。
        ///
        /// 依使用者 2026-07-20 決策：共用機台、**不做密碼**。先驗證分流本身是對的，
        /// 密碼與操作紀錄日後可低成本疊加（已登錄於 docs/ROADMAP.md）。
        /// </summary>
        private void BuildViewModeMenu()
        {
            _engineeringModeMenuItem = new ToolStripMenuItem("工程模式(&E)")
            {
                CheckOnClick = true,
                Checked = _viewMode == ViewMode.Engineering,
                ToolTipText = "顯示建範本、校正、參數試調等會改變判定基準的功能"
            };
            _engineeringModeMenuItem.CheckedChanged += OnEngineeringModeMenuItemCheckedChanged;

            var viewMenu = new ToolStripMenuItem("檢視(&V)");
            viewMenu.DropDownItems.Add(_engineeringModeMenuItem);

            _viewModeMenuStrip = new MenuStrip();
            _viewModeMenuStrip.Items.Add(viewMenu);

            Controls.Add(_viewModeMenuStrip);
            MainMenuStrip = _viewModeMenuStrip;
            // 讓選單佔據視窗最頂端：mainTableLayout 是 Dock=Fill，若選單 z-order 在其之後
            // 會被 Fill 區塊蓋掉，故明確提到最前面。
            _viewModeMenuStrip.BringToFront();
        }

        /// <summary>
        /// 建立操作員面板，並讓它與工程分頁共用 mainTableLayout 的右欄格子。
        ///
        /// Phase 2a 只放結果顯示；操作動作（載入影像／一鍵量測／重新載入配方等）於 Phase 2b 加入，
        /// 因此此階段的操作員模式**刻意尚不可用**，僅供驗證結果訊息分流是否正確。
        /// </summary>
        private void BuildOperatorPanel()
        {
            _operatorResultLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(4),
                AutoEllipsis = true,
                Text = string.Empty
            };

            _operatorPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4),
                AutoScroll = true,
                Visible = false
            };
            _operatorPanel.Controls.Add(_operatorResultLabel);

            // 把既有的 rightPanel 從 TableLayoutPanel 取出，改掛到共用容器下。
            // 只換父層，不動它自身的 Dock/Padding/AutoScroll 設定。
            mainTableLayout.Controls.Remove(rightPanel);

            _rightHostPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            _rightHostPanel.Controls.Add(rightPanel);
            _rightHostPanel.Controls.Add(_operatorPanel);

            mainTableLayout.Controls.Add(_rightHostPanel, 1, 1);
        }

        /// <summary>
        /// 設定「操作員也需要看到」的量測結果訊息。
        ///
        /// 刻意同時寫入兩個結果面而非只寫其中一個：`measureResultLabel` 實體位於 Measurement
        /// 分頁內（工程模式的結果面），若把配方/一鍵的結果只導到操作員面板，工程模式下跑
        /// Run Recipe 就再也看不到結果。兩個面同一時間只有一個可見，鏡像寫入不會造成混淆。
        ///
        /// 純工程用途的訊息（Fit 結果帶入提示、Distance/Angle 量測結果）**不走本方法**，
        /// 直接寫 measureResultLabel 即可——操作員不需要、也不該看到那些。
        /// </summary>
        private void SetMeasurementResult(string text, Color color)
        {
            measureResultLabel.Text = text;
            measureResultLabel.ForeColor = color;

            if (_operatorResultLabel != null)
            {
                _operatorResultLabel.Text = text;
                _operatorResultLabel.ForeColor = color;
            }
        }

        /// <summary>附加到目前的量測結果訊息後方（一鍵量測會接在配方結果之後）。</summary>
        private void AppendMeasurementResult(string text)
        {
            measureResultLabel.Text += text;
            if (_operatorResultLabel != null) _operatorResultLabel.Text += text;
        }

        private void OnEngineeringModeMenuItemCheckedChanged(object sender, EventArgs e)
        {
            SetViewMode(_engineeringModeMenuItem.Checked ? ViewMode.Engineering : ViewMode.Operator);
        }

        private void SetViewMode(ViewMode mode)
        {
            if (_viewMode == mode) return;
            _viewMode = mode;
            ApplyViewMode();
        }

        /// <summary>
        /// 依目前模式調整介面。**後續階段一律在此擴充**，不要把顯示/隱藏邏輯散落到各處。
        ///
        /// Phase 1 僅同步選單勾選狀態與標題列（刻意不動版面，讓「切換不造成行為改變」可被驗證）。
        /// Phase 2 之後才會在此切換操作員版面與工程分頁的可見性。
        /// </summary>
        private void ApplyViewMode()
        {
            bool engineering = _viewMode == ViewMode.Engineering;

            if (_engineeringModeMenuItem != null && _engineeringModeMenuItem.Checked != engineering)
                _engineeringModeMenuItem.Checked = engineering;

            if (rightPanel != null) rightPanel.Visible = engineering;
            if (_operatorPanel != null) _operatorPanel.Visible = !engineering;

            ApplyWindowTitle();
        }

        /// <summary>
        /// 設定標題列的基底文字（不含模式後綴）。供 OnLoad 寫入 HALCON 版本字串使用。
        /// </summary>
        private void SetBaseWindowTitle(string baseTitle)
        {
            _baseWindowTitle = baseTitle ?? string.Empty;
            ApplyWindowTitle();
        }

        /// <summary>
        /// 標題列標示目前模式：操作員在現場需要一眼確認自己不在工程模式。
        /// </summary>
        private void ApplyWindowTitle()
        {
            Text = _viewMode == ViewMode.Engineering
                ? _baseWindowTitle + " [工程模式]"
                : _baseWindowTitle;
        }
    }
}
