using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using FlashMeasurementSystem.Domain.Roi;

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

        // 共用機台上，安全的預設是權限較小的那個：開機即操作員模式，
        // 工程師要動到會改變判定基準的功能時，必須自己去選單切換（此舉即是一次明示的意圖表達）。
        private ViewMode _viewMode = ViewMode.Operator;

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
        private Label _operatorRecipeLabel;
        private Label _operatorReportLabel;

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
            // 不要對選單呼叫 BringToFront()：那會讓它「疊」在 mainTableLayout(Dock=Fill) 之上
            // 而不是把頂端空間讓出來，結果是 PASS/FAIL 橫幅上緣被選單遮掉 ~21px。
            // WinForms 的 dock 依 z-order 決定誰先取得空間，此處保持 Add 後的預設順序即可。
            _viewModeMenuStrip.SendToBack();
        }

        /// <summary>
        /// 建立操作員面板，並讓它與工程分頁共用 mainTableLayout 的右欄格子。
        ///
        /// 只放生產閉環所需：配方/報表資訊、載入影像、一鍵量測、重新載入配方、結果訊息。
        /// 會改變判定基準的動作（校正、Set Ref、建範本、參數試調）一律不放這裡。
        /// PASS/FAIL 大橫幅沿用既有的 resultBannerLabel——它掛在 mainTableLayout(0,0)、
        /// 位於分頁之外，兩種模式都看得到，不需複製。
        /// </summary>
        private void BuildOperatorPanel()
        {
            // 由下往上加入 Dock=Top 的控制項，最後加入的會排在最上面；
            // 結果標籤 Dock=Fill 須「先」加入才能填滿剩餘空間。
            _operatorResultLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(2, 6, 2, 2),
                Text = string.Empty
            };

            var iqcButton = MakeOperatorButton("影像品質檢查", RunIqcButton_Click, 28);
            // IQC 已含在一鍵量測流程內（MeasurementWorkflow 的 CheckingImage 階段），
            // 這裡保留為「一鍵失敗時的診斷入口」，故做成次要樣式、置於最下。
            _toolTip.SetToolTip(iqcButton, "單獨檢查目前影像的品質（一鍵量測已包含此步驟）");

            var reloadRecipeButton = MakeOperatorButton("重新載入配方…", LoadRecipeButton_Click, 28);
            _toolTip.SetToolTip(reloadRecipeButton, "換料號時載入另一個配方 (.zcp)");

            var oneClickButton = MakeOperatorButton("一鍵量測", OneClickMeasureButton_Click, 46);
            oneClickButton.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
            _toolTip.SetToolTip(oneClickButton, "影像品質檢查 → 範本比對 → 量測 → 判定 → 產出 CSV 與 PDF 報表");

            var loadImageButton = MakeOperatorButton("載入影像…", LoadTestImageButton_Click, 34);
            _toolTip.SetToolTip(loadImageButton, "載入待測影像");

            _operatorReportLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _operatorRecipeLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold)
            };

            _operatorPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(6),
                Visible = false
            };
            _operatorPanel.Controls.Add(_operatorResultLabel);
            _operatorPanel.Controls.Add(iqcButton);
            _operatorPanel.Controls.Add(reloadRecipeButton);
            _operatorPanel.Controls.Add(oneClickButton);
            _operatorPanel.Controls.Add(loadImageButton);
            _operatorPanel.Controls.Add(_operatorReportLabel);
            _operatorPanel.Controls.Add(_operatorRecipeLabel);

            UpdateOperatorRecipeInfo();

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
        /// 操作員面板的動作按鈕。刻意共用工程模式既有的 Click handler，
        /// 不另寫一條流程——兩個入口跑同一段程式碼，才不會日後行為漂移。
        /// </summary>
        private Button MakeOperatorButton(string text, EventHandler onClick, int height)
        {
            var b = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = height,
                Margin = new Padding(0, 0, 0, 6)
            };
            b.Click += onClick;
            return b;
        }

        /// <summary>
        /// 解析本次量測要用的模板 .shm 完整路徑（v16）。
        ///
        /// 優先用配方記錄的模板，因為配方的參考姿態就是那個模板量出來的；換一個模板，
        /// 姿態的意義就變了，ROI 會被搬到錯的位置而且不會報錯。
        /// 配方未記錄（v16 之前的舊配方）才退回畫面上選取的模板，維持既有行為。
        /// </summary>
        /// <param name="error">回傳 null 時的原因；成功時為 null。</param>
        private string ResolveTemplatePath(Recipe recipe, out string error)
        {
            error = null;

            if (recipe != null && !string.IsNullOrEmpty(recipe.TemplateModelId))
            {
                string dir = DataPaths.TemplatesDirOrNull();
                if (string.IsNullOrEmpty(dir))
                {
                    error = "找不到 data/templates 目錄，無法解析配方指定的模板 '" + recipe.TemplateModelId + "'。";
                    return null;
                }

                string path = Path.Combine(dir, recipe.TemplateModelId);
                if (!File.Exists(path))
                {
                    // 明確報錯而非默默退回選單：退回等於用「別的模板」跑，正是要防的錯誤量測。
                    error = "配方指定的模板不存在：" + recipe.TemplateModelId +
                            "（預期位於 data/templates）。請重新建立模板並執行 Set Ref。";
                    return null;
                }
                return path;
            }

            // 舊配方：沿用畫面上選取的模板。
            var selected = templateFileCombo.SelectedItem as FileItemWrapper;
            return selected != null && selected.IsRealFile ? selected.FullPath : null;
        }

        /// <summary>
        /// Layer 2 防護：確認目前的匹配姿態是用配方指定的模板量出來的。
        ///
        /// 工程模式下可以「用模板 A 做 Set Ref → 之後用模板 B 按 Run Matching → 再按 Run Recipe」，
        /// 此時參考姿態來自 A、當前姿態來自 B，兩者不可比較，變換出來的 ROI 位置是錯的。
        /// 一鍵量測不會有這個問題（它自己用配方的模板做匹配），但 Run Recipe 吃的是既有的
        /// _lastMatch*，必須在這裡把關。
        /// </summary>
        private bool EnsureMatchTemplateMatchesRecipe()
        {
            if (_loadedRecipe == null || !_loadedRecipe.HasReferencePose) return true;
            if (string.IsNullOrEmpty(_loadedRecipe.TemplateModelId)) return true;  // 舊配方，無從比對
            if (!_hasMatch) return true;                                            // 另有守門負責
            if (string.Equals(_lastMatchTemplateId, _loadedRecipe.TemplateModelId,
                    StringComparison.OrdinalIgnoreCase)) return true;

            MessageBox.Show(this,
                "目前的匹配姿態不是用本配方的模板量出來的，量測位置會錯。\r\n\r\n" +
                "配方模板：" + _loadedRecipe.TemplateModelId + "\r\n" +
                "目前匹配所用：" + (string.IsNullOrEmpty(_lastMatchTemplateId) ? "未知" : _lastMatchTemplateId) + "\r\n\r\n" +
                "請在 Inspection 分頁改選配方的模板後重新執行 Run Matching。",
                "模板不一致", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        /// <summary>
        /// 記住上次使用配方的位置。放在 data/ 下與其他執行期資料一致，
        /// 且該路徑已列入 .gitignore（機器相關狀態不進版控）。
        /// </summary>
        private static string LastRecipeStatePath()
        {
            return Path.Combine(DataPaths.DataDir(), "last-recipe.txt");
        }

        /// <summary>
        /// 由路徑載入配方。手動載入（Load Recipe）與開機自動還原共用此方法，
        /// 兩條路徑才不會在訊息、狀態欄位或錯誤處理上漂移。
        /// </summary>
        /// <param name="rememberAsLast">
        /// 是否把此路徑記為「下次開機要還原的配方」。自動還原時傳 false——
        /// 還原成功不需要重寫同一個值，還原失敗更不該把壞路徑再寫回去。
        /// </param>
        private bool LoadRecipeFromPath(string path, bool rememberAsLast = true)
        {
            try
            {
                _loadedRecipe = _recipeStore.Load(path);
                _loadedRecipePath = path;
                SetMeasurementResult(string.Format(CultureInfo.InvariantCulture,
                    "已載入配方 '{0}'（{1} 工具，SchemaVer {2}{3}）",
                    _loadedRecipe.Name, _loadedRecipe.Tools.Count, _loadedRecipe.SchemaVersion,
                    _loadedRecipe.HasReferencePose ? "，含參考姿態" : "，無參考姿態（需 Set Ref）"),
                    SystemColors.ControlText);
                // 操作員面板的「配方：」欄需同步，否則換配方後仍顯示舊料號。
                UpdateOperatorRecipeInfo();

                if (rememberAsLast) TryRememberLastRecipe(path);
                return true;
            }
            catch (Exception ex)
            {
                _loadedRecipe = null;
                _loadedRecipePath = null;
                SetMeasurementResult("載入配方失敗: " + ex.Message, SystemColors.ControlText);
                UpdateOperatorRecipeInfo();
                return false;
            }
        }

        private void TryRememberLastRecipe(string path)
        {
            try
            {
                Directory.CreateDirectory(DataPaths.DataDir());
                File.WriteAllText(LastRecipeStatePath(), path);
            }
            catch (Exception)
            {
                // 記不住只影響下次開機的便利性，不該讓載入配方這個主要動作失敗。
            }
        }

        /// <summary>
        /// 開機還原上次使用的配方，讓操作員開機後可直接載圖量測，不必每天先找一次配方。
        /// 任何失敗都靜默略過並維持「尚未載入」狀態——配方被刪或換機器都屬正常情況，
        /// 不應該用錯誤對話框擋住啟動。
        /// </summary>
        private void TryRestoreLastRecipe()
        {
            try
            {
                string statePath = LastRecipeStatePath();
                if (!File.Exists(statePath)) return;

                string recipePath = File.ReadAllText(statePath).Trim();
                if (string.IsNullOrEmpty(recipePath) || !File.Exists(recipePath)) return;

                LoadRecipeFromPath(recipePath, rememberAsLast: false);
            }
            catch (Exception)
            {
                // 同上：還原失敗不影響啟動。
            }
        }

        /// <summary>
        /// 更新操作員面板上的配方名稱與報表輸出位置。
        /// 操作員需要在按下量測前，先確認自己跑的是對的配方。
        /// </summary>
        private void UpdateOperatorRecipeInfo()
        {
            if (_operatorRecipeLabel == null) return;

            if (_loadedRecipe == null)
            {
                _operatorRecipeLabel.Text = "配方：（尚未載入）";
                _operatorRecipeLabel.ForeColor = Color.DarkRed;
            }
            else if (_loadedRecipe.HasReferencePose && string.IsNullOrEmpty(_loadedRecipe.TemplateModelId))
            {
                // 有參考姿態卻沒記錄模板：執行時會用畫面上選取的模板，可能與當初 Set Ref 用的不同，
                // 量測位置會錯且不報錯。操作員看不到那個下拉選單，所以必須在這裡把風險說出來。
                _operatorRecipeLabel.Text = "配方：" + _loadedRecipe.Name + "（⚠ 未記錄模板）";
                _operatorRecipeLabel.ForeColor = Color.DarkRed;
            }
            else
            {
                _operatorRecipeLabel.Text = _loadedRecipe.HasReferencePose
                    ? "配方：" + _loadedRecipe.Name + "\r\n模板：" + _loadedRecipe.TemplateModelId
                    : "配方：" + _loadedRecipe.Name;
                _operatorRecipeLabel.ForeColor = SystemColors.ControlText;
            }

            _operatorReportLabel.Text = "報表：" + Path.Combine(ResolveDataDir(), "reports");
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

        /// <summary>
        /// 設定影像品質檢查結果。
        ///
        /// iqcResultLabel 實體位於 Inspection 分頁內（imageQualityBox），操作員模式下是隱藏的；
        /// 影像品質檢查按鈕同時出現在操作員面板上，若只寫該 label，操作員會看到「按了沒反應」。
        /// 與 measureResultLabel 同一類問題，處置方式一致：鏡像到操作員結果面。
        /// </summary>
        private void SetIqcResult(string text, Color color)
        {
            iqcResultLabel.Text = text;
            iqcResultLabel.ForeColor = color;

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

            // 切到操作員模式前，先收掉還開著的工程面板。
            // RecipeEditor / MetrologyModelEditorForm / DxfComparisonForm 都是 modeless
            // （MainWindow.cs 以 .Show(this) 開啟），若放著不管，操作員畫面上會浮著工程視窗，
            // 而且它們仍持有共用影像視窗的 overlay 租約、繼續在影像上畫自己的圖層。
            if (mode == ViewMode.Operator && !CloseEngineeringForms())
            {
                // 有面板拒絕關閉（例如 RecipeEditor 的「未存變更」確認被取消）→ 放棄切換，
                // 並把選單勾選狀態轉回來，否則勾選狀態會與實際模式不一致。
                if (_engineeringModeMenuItem != null) _engineeringModeMenuItem.Checked = true;
                return;
            }

            _viewMode = mode;
            ApplyViewMode();
        }

        /// <summary>
        /// 關閉所有由主視窗擁有的工程面板。回傳 false 表示有面板拒絕關閉。
        /// 各面板在 FormClosed 內會 Dispose 自己的 overlay 租約，故不需在此另外收租約。
        /// </summary>
        private bool CloseEngineeringForms()
        {
            // OwnedForms 會在 Close() 過程中變動，先取一份快照再逐一關閉。
            Form[] owned = OwnedForms;
            foreach (Form f in owned)
            {
                if (!f.IsDisposed) f.Close();
            }

            foreach (Form f in owned)
            {
                if (!f.IsDisposed && f.Visible) return false;
            }
            return true;
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

            // 離開工程模式時清掉「略過IQC」。該旗標只在工程模式生效（見 OneClickMeasureButton_Click），
            // 若讓勾選狀態留著，使用者會遇到「明明勾了卻毫無作用、也沒有任何說明」的情況——
            // 勾選狀態必須誠實反映它是否真的有效。回到工程模式需重新勾選，這也符合它
            // 「臨時拿合成影像測試」的定位，不該跨模式長期存在。
            if (!engineering && _skipIqcCheckBox != null) _skipIqcCheckBox.Checked = false;

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
