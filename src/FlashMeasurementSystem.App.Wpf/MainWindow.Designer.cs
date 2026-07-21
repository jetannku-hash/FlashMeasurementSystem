namespace FlashMeasurementSystem
{
    partial class MainWindow
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.mainTableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.hWindowControl = new HalconDotNet.HWindowControl();
            this.rightPanel = new System.Windows.Forms.Panel();
            this.featureTabControl = new System.Windows.Forms.TabControl();
            this.inspectionTabPage = new System.Windows.Forms.TabPage();
            this.templateMatchingBox = new System.Windows.Forms.GroupBox();
            this.matchResultTextBox = new System.Windows.Forms.TextBox();
            this.runMatchingButton = new System.Windows.Forms.Button();
            this.minScoreLabel = new System.Windows.Forms.Label();
            this.minScoreNumeric = new System.Windows.Forms.NumericUpDown();
            this.templateFileCombo = new System.Windows.Forms.ComboBox();
            this.templateLabel = new System.Windows.Forms.Label();
            this.loadTestImageButton = new System.Windows.Forms.Button();
            this.templateCreationBox = new System.Windows.Forms.GroupBox();
            this.roiClearButton = new System.Windows.Forms.Button();
            this.roiModeCheck = new System.Windows.Forms.CheckBox();
            this.createTemplateButton = new System.Windows.Forms.Button();
            this.loadRefImageButton = new System.Windows.Forms.Button();
            this.pyramidLabel = new System.Windows.Forms.Label();
            this.pyramidNumeric = new System.Windows.Forms.NumericUpDown();
            this.angleExtentLabel = new System.Windows.Forms.Label();
            this.angleExtentNumeric = new System.Windows.Forms.NumericUpDown();
            this.angleStartLabel = new System.Windows.Forms.Label();
            this.angleStartNumeric = new System.Windows.Forms.NumericUpDown();
            this.imageQualityBox = new System.Windows.Forms.GroupBox();
            this.iqcResultLabel = new System.Windows.Forms.Label();
            this.runIqcButton = new System.Windows.Forms.Button();
            this.edgeDetectionTabPage = new System.Windows.Forms.TabPage();
            this._edgeDetectionBox = new System.Windows.Forms.GroupBox();
            this.edgeTableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.edgeAlgorithmLabel = new System.Windows.Forms.Label();
            this.edgeAlgorithmPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._edgeMeasurePosRadio = new System.Windows.Forms.RadioButton();
            this._edgeSubPixRadio = new System.Windows.Forms.RadioButton();
            this.edgeSigmaLabel = new System.Windows.Forms.Label();
            this._edgeSigmaNumeric = new System.Windows.Forms.NumericUpDown();
            this.edgeThresholdLabel = new System.Windows.Forms.Label();
            this._edgeThresholdNumeric = new System.Windows.Forms.NumericUpDown();
            this.edgePolarityLabel = new System.Windows.Forms.Label();
            this._edgePolarityCombo = new System.Windows.Forms.ComboBox();
            this.edgeSelectorLabel = new System.Windows.Forms.Label();
            this._edgeSelectorCombo = new System.Windows.Forms.ComboBox();
            this.edgeSubpixelLabel = new System.Windows.Forms.Label();
            this._edgeSubpixelMethodCombo = new System.Windows.Forms.ComboBox();
            this.edgeRoiWidthLabel = new System.Windows.Forms.Label();
            this._edgeRoiWidthNumeric = new System.Windows.Forms.NumericUpDown();
            this.edgeScanLengthLabel = new System.Windows.Forms.Label();
            this._edgeScanLengthNumeric = new System.Windows.Forms.NumericUpDown();
            this._edgeDrawRoiCheck = new System.Windows.Forms.CheckBox();
            this.edgeMeasureModeLabel = new System.Windows.Forms.Label();
            this._edgeMeasureModeCombo = new System.Windows.Forms.ComboBox();
            this.edgeInterpolationLabel = new System.Windows.Forms.Label();
            this._edgeInterpolationCombo = new System.Windows.Forms.ComboBox();
            this.edgeAngleLabel = new System.Windows.Forms.Label();
            this._edgeAngleNumeric = new System.Windows.Forms.NumericUpDown();
            this.edgeButtonPanel = new System.Windows.Forms.TableLayoutPanel();
            this._runEdgeDetectionButton = new System.Windows.Forms.Button();
            this._clearEdgeDetectionButton = new System.Windows.Forms.Button();
            this.arcMeasurePanel = new System.Windows.Forms.TableLayoutPanel();
            this.arcMeasureHeader = new System.Windows.Forms.Label();
            this.arcCenterRowLabel = new System.Windows.Forms.Label();
            this._arcCenterRowNumeric = new System.Windows.Forms.NumericUpDown();
            this.arcCenterColLabel = new System.Windows.Forms.Label();
            this._arcCenterColNumeric = new System.Windows.Forms.NumericUpDown();
            this.arcRadiusLabel = new System.Windows.Forms.Label();
            this._arcRadiusNumeric = new System.Windows.Forms.NumericUpDown();
            this.arcAngleStartLabel = new System.Windows.Forms.Label();
            this._arcAngleStartNumeric = new System.Windows.Forms.NumericUpDown();
            this.arcAngleExtentLabel = new System.Windows.Forms.Label();
            this._arcAngleExtentNumeric = new System.Windows.Forms.NumericUpDown();
            this.arcAnnulusLabel = new System.Windows.Forms.Label();
            this._arcAnnulusNumeric = new System.Windows.Forms.NumericUpDown();
            this.detectArcButton = new System.Windows.Forms.Button();
            this._arcEditCheck = new System.Windows.Forms.CheckBox();
            this._sectorDrawCheck = new System.Windows.Forms.CheckBox();
            this._edgeStatusLabel = new System.Windows.Forms.Label();
            this._edgeResultsGrid = new System.Windows.Forms.DataGridView();
            this.edgeIndexColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.edgeRowColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.edgeColumnColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.edgeAmplitudeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.edgeDistanceColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.measurementTabPage = new System.Windows.Forms.TabPage();
            this.recipeTabPage = new System.Windows.Forms.TabPage();
            this.measurementBox = new System.Windows.Forms.GroupBox();
            this.measurementTableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.measurementPixelXLabel = new System.Windows.Forms.Label();
            this.measurementPixelSizeXNumeric = new System.Windows.Forms.NumericUpDown();
            this.measurementPixelYLabel = new System.Windows.Forms.Label();
            this.measurementPixelSizeYNumeric = new System.Windows.Forms.NumericUpDown();
            this.measureResultLabel = new System.Windows.Forms.Label();
            this.imageHostPanel = new System.Windows.Forms.Panel();
            this.resultBannerPanel = new System.Windows.Forms.Panel();
            this.resultBannerLabel = new System.Windows.Forms.Label();
            this.emptyStateGuideLabel = new System.Windows.Forms.Label();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.progressLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.coordLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.imageSizeLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.mainTableLayout.SuspendLayout();
            this.imageHostPanel.SuspendLayout();
            this.resultBannerPanel.SuspendLayout();
            this.rightPanel.SuspendLayout();
            this.featureTabControl.SuspendLayout();
            this.inspectionTabPage.SuspendLayout();
            this.templateMatchingBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.minScoreNumeric)).BeginInit();
            this.templateCreationBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pyramidNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.angleExtentNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.angleStartNumeric)).BeginInit();
            this.imageQualityBox.SuspendLayout();
            this.edgeDetectionTabPage.SuspendLayout();
            this._edgeDetectionBox.SuspendLayout();
            this.edgeTableLayout.SuspendLayout();
            this.edgeAlgorithmPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._edgeSigmaNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._edgeThresholdNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._edgeRoiWidthNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._edgeScanLengthNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._edgeAngleNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcCenterRowNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcCenterColNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcRadiusNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcAnnulusNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcAngleStartNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcAngleExtentNumeric)).BeginInit();
            this.arcMeasurePanel.SuspendLayout();
            this.edgeButtonPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._edgeResultsGrid)).BeginInit();
            this.measurementTabPage.SuspendLayout();
            this.measurementBox.SuspendLayout();
            this.measurementTableLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.measurementPixelSizeXNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.measurementPixelSizeYNumeric)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainTableLayout
            // 
            this.mainTableLayout.ColumnCount = 2;
            // 右側控制面板改固定寬度：25% 在 1920 寬螢幕下會變成 480px，
            // 但控制面板的需求寬度是固定的，多出來的寬度應該還給影像區。
            this.mainTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 340F));
            this.mainTableLayout.Controls.Add(this.resultBannerPanel, 0, 0);
            this.mainTableLayout.Controls.Add(this.imageHostPanel, 0, 1);
            this.mainTableLayout.Controls.Add(this.rightPanel, 1, 1);
            this.mainTableLayout.SetColumnSpan(this.resultBannerPanel, 2);
            this.mainTableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainTableLayout.Location = new System.Drawing.Point(0, 0);
            this.mainTableLayout.Name = "mainTableLayout";
            this.mainTableLayout.RowCount = 2;
            this.mainTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.mainTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainTableLayout.Size = new System.Drawing.Size(1100, 648);
            this.mainTableLayout.TabIndex = 0;
            //
            // imageHostPanel
            //
            // 影像格容器：HWindowControl 與空狀態引導同置於此普通 Panel，
            // 以 Z-order/BringToFront 疊放（TableLayoutPanel 單一儲存格不支援兩個控制項，
            // 直接放會把第二個擠到右欄）。
            this.imageHostPanel.Controls.Add(this.emptyStateGuideLabel);
            this.imageHostPanel.Controls.Add(this.hWindowControl);
            this.imageHostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageHostPanel.Location = new System.Drawing.Point(3, 59);
            this.imageHostPanel.Margin = new System.Windows.Forms.Padding(0);
            this.imageHostPanel.Name = "imageHostPanel";
            this.imageHostPanel.Size = new System.Drawing.Size(819, 586);
            this.imageHostPanel.TabIndex = 0;
            //
            // resultBannerPanel
            //
            this.resultBannerPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(160)))), ((int)(((byte)(160)))), ((int)(((byte)(160)))));
            this.resultBannerPanel.Controls.Add(this.resultBannerLabel);
            this.resultBannerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultBannerPanel.Location = new System.Drawing.Point(3, 3);
            this.resultBannerPanel.Name = "resultBannerPanel";
            this.resultBannerPanel.Size = new System.Drawing.Size(1094, 50);
            this.resultBannerPanel.TabIndex = 2;
            //
            // resultBannerLabel
            //
            this.resultBannerLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultBannerLabel.Font = new System.Drawing.Font("Segoe UI", 24F, System.Drawing.FontStyle.Bold);
            this.resultBannerLabel.ForeColor = System.Drawing.Color.White;
            this.resultBannerLabel.Name = "resultBannerLabel";
            this.resultBannerLabel.TabIndex = 0;
            this.resultBannerLabel.Text = "—";
            this.resultBannerLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // emptyStateGuideLabel
            //
            this.emptyStateGuideLabel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(37)))), ((int)(((byte)(38)))));
            this.emptyStateGuideLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.emptyStateGuideLabel.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.emptyStateGuideLabel.ForeColor = System.Drawing.Color.White;
            this.emptyStateGuideLabel.Name = "emptyStateGuideLabel";
            this.emptyStateGuideLabel.TabIndex = 3;
            this.emptyStateGuideLabel.Text = "① 載入影像（Load Image）\n② 載入或建立配方（Load / Edit Recipe）\n③ 按一鍵量測（One-Click）";
            this.emptyStateGuideLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.emptyStateGuideLabel.BringToFront();
            //
            // hWindowControl
            //
            this.hWindowControl.BackColor = System.Drawing.Color.Gray;
            this.hWindowControl.BorderColor = System.Drawing.Color.Gray;
            this.hWindowControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hWindowControl.ImagePart = new System.Drawing.Rectangle(0, 0, 640, 480);
            this.hWindowControl.Location = new System.Drawing.Point(3, 3);
            this.hWindowControl.Name = "hWindowControl";
            this.hWindowControl.Size = new System.Drawing.Size(819, 642);
            this.hWindowControl.TabIndex = 0;
            this.hWindowControl.WindowSize = new System.Drawing.Size(819, 642);
            // 
            // rightPanel
            // 
            this.rightPanel.AutoScroll = true;
            this.rightPanel.Controls.Add(this.featureTabControl);
            this.rightPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightPanel.Location = new System.Drawing.Point(828, 3);
            this.rightPanel.Name = "rightPanel";
            this.rightPanel.Padding = new System.Windows.Forms.Padding(4);
            this.rightPanel.Size = new System.Drawing.Size(269, 642);
            this.rightPanel.TabIndex = 1;
            // 
            // featureTabControl
            // 
            // 分頁順序＝工作流程順序：從左到右做一遍就是建立並驗證一個配方。
            // 原本三個分頁是按「技術分類」切的（Inspection / Edge Detection / Measurement），
            // 導致「建一個新料號」這件事要橫跨兩個分頁加一個彈出視窗，看不出先後。
            this.featureTabControl.Controls.Add(this.inspectionTabPage);      // ① 建立模板
            this.featureTabControl.Controls.Add(this.recipeTabPage);          // ② 配方
            this.featureTabControl.Controls.Add(this.measurementTabPage);     // ③ 量測
            this.featureTabControl.Controls.Add(this.edgeDetectionTabPage);   // ④ 診斷
            this.featureTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.featureTabControl.Location = new System.Drawing.Point(4, 4);
            this.featureTabControl.Name = "featureTabControl";
            this.featureTabControl.SelectedIndex = 0;
            this.featureTabControl.SelectedIndexChanged += new System.EventHandler(this.FeatureTabControl_SelectedIndexChanged);
            this.featureTabControl.Size = new System.Drawing.Size(261, 634);
            this.featureTabControl.TabIndex = 0;
            // 
            // inspectionTabPage
            // 
            // Dock=Fill 的必須先加入，Dock=Top 的後加（後加者顯示在上方），與原本順序一致。
            // imageQualityBox 已移至「④ 診斷」——影像品質檢查是出問題時才用的診斷工具，
            // 不是建立模板的步驟，留在這裡會讓人以為每次建模板都要先跑它。
            this.inspectionTabPage.Controls.Add(this.templateMatchingBox);
            this.inspectionTabPage.Controls.Add(this.templateCreationBox);
            this.inspectionTabPage.Location = new System.Drawing.Point(4, 22);
            this.inspectionTabPage.Name = "inspectionTabPage";
            this.inspectionTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.inspectionTabPage.Size = new System.Drawing.Size(253, 608);
            this.inspectionTabPage.TabIndex = 0;
            this.inspectionTabPage.Text = "① 建立模板";
            this.inspectionTabPage.UseVisualStyleBackColor = true;
            //
            // recipeTabPage
            //
            // 內容（配方工具列）由 MainWindow.cs 在 OnLoad 以程式碼建構，比照既有 topToolbar 做法。
            this.recipeTabPage.Location = new System.Drawing.Point(4, 22);
            this.recipeTabPage.Name = "recipeTabPage";
            this.recipeTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.recipeTabPage.Size = new System.Drawing.Size(253, 608);
            this.recipeTabPage.TabIndex = 1;
            this.recipeTabPage.Text = "② 配方";
            this.recipeTabPage.UseVisualStyleBackColor = true;
            // 
            // templateMatchingBox
            // 
            this.templateMatchingBox.Controls.Add(this.matchResultTextBox);
            this.templateMatchingBox.Controls.Add(this.runMatchingButton);
            this.templateMatchingBox.Controls.Add(this.minScoreLabel);
            this.templateMatchingBox.Controls.Add(this.minScoreNumeric);
            this.templateMatchingBox.Controls.Add(this.templateFileCombo);
            this.templateMatchingBox.Controls.Add(this.templateLabel);
            this.templateMatchingBox.Controls.Add(this.loadTestImageButton);
            this.templateMatchingBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.templateMatchingBox.Location = new System.Drawing.Point(3, 299);
            this.templateMatchingBox.Name = "templateMatchingBox";
            this.templateMatchingBox.Size = new System.Drawing.Size(247, 306);
            this.templateMatchingBox.TabIndex = 3;
            this.templateMatchingBox.TabStop = false;
            this.templateMatchingBox.Text = "模板比對";
            // 
            // matchResultTextBox
            // 
            this.matchResultTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.matchResultTextBox.Location = new System.Drawing.Point(10, 156);
            this.matchResultTextBox.Multiline = true;
            this.matchResultTextBox.Name = "matchResultTextBox";
            this.matchResultTextBox.ReadOnly = true;
            this.matchResultTextBox.Size = new System.Drawing.Size(227, 58);
            this.matchResultTextBox.TabIndex = 6;
            // 
            // runMatchingButton
            // 
            this.runMatchingButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.runMatchingButton.Location = new System.Drawing.Point(10, 110);
            this.runMatchingButton.Name = "runMatchingButton";
            this.runMatchingButton.Size = new System.Drawing.Size(227, 23);
            this.runMatchingButton.TabIndex = 5;
            this.runMatchingButton.Text = "Run &Matching";
            this.runMatchingButton.UseVisualStyleBackColor = true;
            this.runMatchingButton.Click += new System.EventHandler(this.RunMatchingButton_Click);
            // 
            // minScoreLabel
            // 
            this.minScoreLabel.AutoSize = true;
            this.minScoreLabel.Location = new System.Drawing.Point(10, 77);
            this.minScoreLabel.Name = "minScoreLabel";
            this.minScoreLabel.Size = new System.Drawing.Size(56, 12);
            this.minScoreLabel.TabIndex = 3;
            this.minScoreLabel.Text = "Min &Score:";
            // 
            // minScoreNumeric
            // 
            this.minScoreNumeric.DecimalPlaces = 2;
            this.minScoreNumeric.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this.minScoreNumeric.Location = new System.Drawing.Point(80, 74);
            this.minScoreNumeric.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.minScoreNumeric.Name = "minScoreNumeric";
            this.minScoreNumeric.Size = new System.Drawing.Size(80, 22);
            this.minScoreNumeric.TabIndex = 4;
            this.minScoreNumeric.Value = new decimal(new int[] {
            75,
            0,
            0,
            131072});
            // 
            // templateFileCombo
            // 
            this.templateFileCombo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.templateFileCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.templateFileCombo.FormattingEnabled = true;
            this.templateFileCombo.Location = new System.Drawing.Point(10, 34);
            this.templateFileCombo.Name = "templateFileCombo";
            this.templateFileCombo.Size = new System.Drawing.Size(227, 20);
            this.templateFileCombo.TabIndex = 2;
            // 
            // templateLabel
            // 
            this.templateLabel.AutoSize = true;
            this.templateLabel.Location = new System.Drawing.Point(10, 35);
            this.templateLabel.Name = "templateLabel";
            this.templateLabel.Size = new System.Drawing.Size(104, 12);
            this.templateLabel.TabIndex = 1;
            this.templateLabel.Text = "Template File (.shm):";
            // 
            // loadTestImageButton
            // 
            this.loadTestImageButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.loadTestImageButton.Location = new System.Drawing.Point(10, -5);
            this.loadTestImageButton.Name = "loadTestImageButton";
            this.loadTestImageButton.Size = new System.Drawing.Size(227, 21);
            this.loadTestImageButton.TabIndex = 0;
            this.loadTestImageButton.Text = "Load &Test Image";
            this.loadTestImageButton.UseVisualStyleBackColor = true;
            this.loadTestImageButton.Click += new System.EventHandler(this.LoadTestImageButton_Click);
            // 
            // templateCreationBox
            // 
            this.templateCreationBox.Controls.Add(this.roiClearButton);
            this.templateCreationBox.Controls.Add(this.roiModeCheck);
            this.templateCreationBox.Controls.Add(this.createTemplateButton);
            this.templateCreationBox.Controls.Add(this.loadRefImageButton);
            this.templateCreationBox.Controls.Add(this.pyramidLabel);
            this.templateCreationBox.Controls.Add(this.pyramidNumeric);
            this.templateCreationBox.Controls.Add(this.angleExtentLabel);
            this.templateCreationBox.Controls.Add(this.angleExtentNumeric);
            this.templateCreationBox.Controls.Add(this.angleStartLabel);
            this.templateCreationBox.Controls.Add(this.angleStartNumeric);
            this.templateCreationBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.templateCreationBox.Location = new System.Drawing.Point(3, 113);
            this.templateCreationBox.Name = "templateCreationBox";
            this.templateCreationBox.Size = new System.Drawing.Size(247, 186);
            this.templateCreationBox.TabIndex = 2;
            this.templateCreationBox.TabStop = false;
            this.templateCreationBox.Text = "建立模板";
            // 
            // roiClearButton
            // 
            this.roiClearButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
            // roiModeCheck 是 AutoSize，Segoe UI 比原本的 Microsoft Sans Serif 寬，
            // 勾選框會延伸到 x≈130，故本按鈕右移至 140；寬度 60 原本就放不下 "Clear ROI"
            // （一直被截成 "Clear"），一併給足寬度與符合其他按鈕的高度。
            this.roiClearButton.Location = new System.Drawing.Point(140, 118);
            this.roiClearButton.Name = "roiClearButton";
            this.roiClearButton.Size = new System.Drawing.Size(90, 23);
            this.roiClearButton.TabIndex = 8;
            this.roiClearButton.Text = "Clear &ROI";
            this.roiClearButton.UseVisualStyleBackColor = true;
            this.roiClearButton.Click += new System.EventHandler(this.ClearRoiButton_Click);
            // 
            // roiModeCheck
            // 
            this.roiModeCheck.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.roiModeCheck.AutoSize = true;
            this.roiModeCheck.Location = new System.Drawing.Point(10, 120);
            this.roiModeCheck.Name = "roiModeCheck";
            this.roiModeCheck.Size = new System.Drawing.Size(109, 16);
            this.roiModeCheck.TabIndex = 7;
            this.roiModeCheck.Text = "&Draw ROI Region";
            this.roiModeCheck.UseVisualStyleBackColor = true;
            this.roiModeCheck.CheckedChanged += new System.EventHandler(this.RoiModeCheck_CheckedChanged);
            // 
            // createTemplateButton
            // 
            this.createTemplateButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.createTemplateButton.Location = new System.Drawing.Point(10, 155);
            this.createTemplateButton.Name = "createTemplateButton";
            this.createTemplateButton.Size = new System.Drawing.Size(227, 23);
            this.createTemplateButton.TabIndex = 6;
            this.createTemplateButton.Text = "&Create Template";
            this.createTemplateButton.UseVisualStyleBackColor = true;
            this.createTemplateButton.Click += new System.EventHandler(this.CreateTemplateButton_Click);
            // 
            // loadRefImageButton
            // 
            this.loadRefImageButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.loadRefImageButton.Location = new System.Drawing.Point(10, 15);
            this.loadRefImageButton.Name = "loadRefImageButton";
            this.loadRefImageButton.Size = new System.Drawing.Size(227, 21);
            this.loadRefImageButton.TabIndex = 0;
            this.loadRefImageButton.Text = "&Load Reference Image";
            this.loadRefImageButton.UseVisualStyleBackColor = true;
            this.loadRefImageButton.Click += new System.EventHandler(this.LoadRefImageButton_Click);
            // 
            // pyramidLabel
            // 
            this.pyramidLabel.AutoSize = true;
            this.pyramidLabel.Location = new System.Drawing.Point(10, 98);
            this.pyramidLabel.Name = "pyramidLabel";
            this.pyramidLabel.Size = new System.Drawing.Size(76, 12);
            this.pyramidLabel.TabIndex = 4;
            this.pyramidLabel.Text = "Pyramid Level:";
            // 
            // pyramidNumeric
            // 
            this.pyramidNumeric.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.pyramidNumeric.Location = new System.Drawing.Point(96, 95);
            this.pyramidNumeric.Maximum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.pyramidNumeric.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.pyramidNumeric.Name = "pyramidNumeric";
            this.pyramidNumeric.Size = new System.Drawing.Size(80, 22);
            this.pyramidNumeric.TabIndex = 5;
            this.pyramidNumeric.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            // 
            // angleExtentLabel
            // 
            this.angleExtentLabel.AutoSize = true;
            this.angleExtentLabel.Location = new System.Drawing.Point(10, 74);
            this.angleExtentLabel.Name = "angleExtentLabel";
            this.angleExtentLabel.Size = new System.Drawing.Size(69, 12);
            this.angleExtentLabel.TabIndex = 2;
            this.angleExtentLabel.Text = "Angle Extent:";
            // 
            // angleExtentNumeric
            // 
            this.angleExtentNumeric.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.angleExtentNumeric.DecimalPlaces = 1;
            this.angleExtentNumeric.Location = new System.Drawing.Point(96, 71);
            this.angleExtentNumeric.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this.angleExtentNumeric.Name = "angleExtentNumeric";
            this.angleExtentNumeric.Size = new System.Drawing.Size(80, 22);
            this.angleExtentNumeric.TabIndex = 3;
            this.angleExtentNumeric.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // angleStartLabel
            // 
            this.angleStartLabel.AutoSize = true;
            this.angleStartLabel.Location = new System.Drawing.Point(10, 50);
            this.angleStartLabel.Name = "angleStartLabel";
            this.angleStartLabel.Size = new System.Drawing.Size(60, 12);
            this.angleStartLabel.TabIndex = 0;
            this.angleStartLabel.Text = "Angle Start:";
            // 
            // angleStartNumeric
            // 
            this.angleStartNumeric.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.angleStartNumeric.DecimalPlaces = 1;
            this.angleStartNumeric.Location = new System.Drawing.Point(96, 47);
            this.angleStartNumeric.Minimum = new decimal(new int[] {
            30,
            0,
            0,
            -2147483648});
            this.angleStartNumeric.Name = "angleStartNumeric";
            this.angleStartNumeric.Size = new System.Drawing.Size(80, 22);
            this.angleStartNumeric.TabIndex = 1;
            this.angleStartNumeric.Value = new decimal(new int[] {
            5,
            0,
            0,
            -2147483648});
            // 
            // imageQualityBox
            // 
            this.imageQualityBox.Controls.Add(this.iqcResultLabel);
            this.imageQualityBox.Controls.Add(this.runIqcButton);
            this.imageQualityBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.imageQualityBox.Location = new System.Drawing.Point(3, 3);
            this.imageQualityBox.Name = "imageQualityBox";
            this.imageQualityBox.Size = new System.Drawing.Size(247, 110);
            this.imageQualityBox.TabIndex = 0;
            this.imageQualityBox.TabStop = false;
            this.imageQualityBox.Text = "影像品質檢查";
            // 
            // iqcResultLabel
            // 
            this.iqcResultLabel.Location = new System.Drawing.Point(10, 42);
            this.iqcResultLabel.Name = "iqcResultLabel";
            this.iqcResultLabel.Size = new System.Drawing.Size(227, 56);
            this.iqcResultLabel.TabIndex = 1;
            this.iqcResultLabel.Text = "Not tested";
            // 
            // runIqcButton
            // 
            this.runIqcButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.runIqcButton.Location = new System.Drawing.Point(10, 15);
            this.runIqcButton.Name = "runIqcButton";
            this.runIqcButton.Size = new System.Drawing.Size(227, 21);
            this.runIqcButton.TabIndex = 0;
            this.runIqcButton.Text = "Run Image &Quality Check";
            this.runIqcButton.UseVisualStyleBackColor = true;
            this.runIqcButton.Click += new System.EventHandler(this.RunIqcButton_Click);
            // 
            // edgeDetectionTabPage
            // 
            // 診斷分頁：Dock=Fill 的邊緣檢測先加入，Dock=Top 的影像品質檢查後加（顯示在上方）。
            this.edgeDetectionTabPage.Controls.Add(this._edgeDetectionBox);
            this.edgeDetectionTabPage.Controls.Add(this.imageQualityBox);
            this.edgeDetectionTabPage.Location = new System.Drawing.Point(4, 22);
            this.edgeDetectionTabPage.Name = "edgeDetectionTabPage";
            this.edgeDetectionTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.edgeDetectionTabPage.Size = new System.Drawing.Size(253, 608);
            this.edgeDetectionTabPage.TabIndex = 3;
            this.edgeDetectionTabPage.Text = "④ 診斷";
            this.edgeDetectionTabPage.UseVisualStyleBackColor = true;
            // 
            // _edgeDetectionBox
            // 
            this._edgeDetectionBox.Controls.Add(this.edgeTableLayout);
            this._edgeDetectionBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeDetectionBox.Location = new System.Drawing.Point(3, 3);
            this._edgeDetectionBox.Name = "_edgeDetectionBox";
            this._edgeDetectionBox.Padding = new System.Windows.Forms.Padding(8, 18, 8, 8);
            this._edgeDetectionBox.Size = new System.Drawing.Size(247, 602);
            this._edgeDetectionBox.TabIndex = 0;
            this._edgeDetectionBox.TabStop = false;
            this._edgeDetectionBox.Text = "邊緣檢測（探索用）";
            // 
            // edgeTableLayout
            // 
            // 三欄：標籤(110) / 輸入(140) / 剩餘寬度吸收欄。
            // 原本兩欄各佔 48%/52%，視窗一拉寬，Sigma=1.2 這種輸入框會被拉到 200px 以上；
            // 參數輸入框的需求寬度與視窗寬度無關，故改固定寬度，多餘寬度交給第三欄。
            this.edgeTableLayout.ColumnCount = 3;
            this.edgeTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            this.edgeTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 140F));
            this.edgeTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.edgeTableLayout.Controls.Add(this.edgeAlgorithmLabel, 0, 0);
            this.edgeTableLayout.Controls.Add(this.edgeAlgorithmPanel, 1, 0);
            this.edgeTableLayout.Controls.Add(this.edgeSigmaLabel, 0, 1);
            this.edgeTableLayout.Controls.Add(this._edgeSigmaNumeric, 1, 1);
            this.edgeTableLayout.Controls.Add(this.edgeThresholdLabel, 0, 2);
            this.edgeTableLayout.Controls.Add(this._edgeThresholdNumeric, 1, 2);
            this.edgeTableLayout.Controls.Add(this.edgePolarityLabel, 0, 3);
            this.edgeTableLayout.Controls.Add(this._edgePolarityCombo, 1, 3);
            this.edgeTableLayout.Controls.Add(this.edgeSelectorLabel, 0, 4);
            this.edgeTableLayout.Controls.Add(this._edgeSelectorCombo, 1, 4);
            this.edgeTableLayout.Controls.Add(this.edgeSubpixelLabel, 0, 5);
            this.edgeTableLayout.Controls.Add(this._edgeSubpixelMethodCombo, 1, 5);
            this.edgeTableLayout.Controls.Add(this.edgeRoiWidthLabel, 0, 6);
            this.edgeTableLayout.Controls.Add(this._edgeRoiWidthNumeric, 1, 6);
            this.edgeTableLayout.Controls.Add(this.edgeScanLengthLabel, 0, 7);
            this.edgeTableLayout.Controls.Add(this._edgeScanLengthNumeric, 1, 7);
            this.edgeTableLayout.Controls.Add(this._edgeDrawRoiCheck, 0, 8);
            this.edgeTableLayout.Controls.Add(this.edgeMeasureModeLabel, 0, 9);
            this.edgeTableLayout.Controls.Add(this._edgeMeasureModeCombo, 1, 9);
            this.edgeTableLayout.Controls.Add(this.edgeInterpolationLabel, 0, 10);
            this.edgeTableLayout.Controls.Add(this._edgeInterpolationCombo, 1, 10);
            this.edgeTableLayout.Controls.Add(this.edgeAngleLabel, 0, 11);
            this.edgeTableLayout.Controls.Add(this._edgeAngleNumeric, 1, 11);
            this.edgeTableLayout.Controls.Add(this.arcMeasurePanel, 0, 12);
            this.edgeTableLayout.Controls.Add(this.edgeButtonPanel, 0, 13);
            this.edgeTableLayout.Controls.Add(this._edgeStatusLabel, 0, 14);
            this.edgeTableLayout.Controls.Add(this._edgeResultsGrid, 0, 15);
            this.edgeTableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeTableLayout.Location = new System.Drawing.Point(8, 33);
            this.edgeTableLayout.Name = "edgeTableLayout";
            this.edgeTableLayout.RowCount = 16;
            // Algorithm 列：兩個 AutoSize RadioButton 疊放（第二顆 y=25），Segoe UI 下高度較
            // Microsoft Sans Serif 多幾 px，44 會把下面的 EdgesSubPix 切掉，故放寬到 48。
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            // 弧量測面板（arcMeasurePanel）列：內部 6 列均分，144 時每列僅 23px，
            // 扣掉上下 Margin 後 Dock=Fill 的 detectArcButton 只剩 17px，按鈕文字會被切掉
            // （同列的 NumericUpDown 因為 chrome 較薄，17px 仍看得到字，所以只有按鈕出問題）。
            // 174 → 每列 29px → 按鈕 23px，足夠容納 Segoe UI 8.25 的文字與按鈕外框。
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 174F));
            // Detect / Clear 按鈕列：Dock=Fill 扣掉上下 Margin 後需 ~28px 才容得下文字與按鈕外框。
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this.edgeTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.edgeTableLayout.Size = new System.Drawing.Size(231, 561);
            this.edgeTableLayout.TabIndex = 0;
            // 
            // edgeAlgorithmLabel
            // 
            this.edgeAlgorithmLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeAlgorithmLabel.Location = new System.Drawing.Point(3, 0);
            this.edgeAlgorithmLabel.Name = "edgeAlgorithmLabel";
            this.edgeAlgorithmLabel.Size = new System.Drawing.Size(104, 44);
            this.edgeAlgorithmLabel.TabIndex = 0;
            this.edgeAlgorithmLabel.Text = "Algorithm";
            this.edgeAlgorithmLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // edgeAlgorithmPanel
            // 
            this.edgeAlgorithmPanel.Controls.Add(this._edgeMeasurePosRadio);
            this.edgeAlgorithmPanel.Controls.Add(this._edgeSubPixRadio);
            this.edgeAlgorithmPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeAlgorithmPanel.Location = new System.Drawing.Point(113, 3);
            this.edgeAlgorithmPanel.Name = "edgeAlgorithmPanel";
            this.edgeAlgorithmPanel.Size = new System.Drawing.Size(115, 38);
            this.edgeAlgorithmPanel.TabIndex = 1;
            // 
            // _edgeMeasurePosRadio
            // 
            this._edgeMeasurePosRadio.AutoSize = true;
            this._edgeMeasurePosRadio.Checked = true;
            this._edgeMeasurePosRadio.Location = new System.Drawing.Point(3, 3);
            this._edgeMeasurePosRadio.Name = "_edgeMeasurePosRadio";
            this._edgeMeasurePosRadio.Size = new System.Drawing.Size(78, 16);
            this._edgeMeasurePosRadio.TabIndex = 0;
            this._edgeMeasurePosRadio.TabStop = true;
            this._edgeMeasurePosRadio.Text = "MeasurePos";
            this._edgeMeasurePosRadio.UseVisualStyleBackColor = true;
            // 
            // _edgeSubPixRadio
            // 
            this._edgeSubPixRadio.AutoSize = true;
            this._edgeSubPixRadio.Location = new System.Drawing.Point(3, 25);
            this._edgeSubPixRadio.Name = "_edgeSubPixRadio";
            this._edgeSubPixRadio.Size = new System.Drawing.Size(84, 16);
            this._edgeSubPixRadio.TabIndex = 1;
            this._edgeSubPixRadio.Text = "EdgesSubPix";
            this._edgeSubPixRadio.UseVisualStyleBackColor = true;
            this._edgeSubPixRadio.CheckedChanged += new System.EventHandler(this.EdgeAlgorithmRadio_CheckedChanged);
            // 
            // edgeSigmaLabel
            // 
            this.edgeSigmaLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeSigmaLabel.Location = new System.Drawing.Point(3, 44);
            this.edgeSigmaLabel.Name = "edgeSigmaLabel";
            this.edgeSigmaLabel.Size = new System.Drawing.Size(104, 26);
            this.edgeSigmaLabel.TabIndex = 2;
            this.edgeSigmaLabel.Text = "Sigma";
            this.edgeSigmaLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _edgeSigmaNumeric
            // 
            this._edgeSigmaNumeric.DecimalPlaces = 1;
            this._edgeSigmaNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeSigmaNumeric.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this._edgeSigmaNumeric.Location = new System.Drawing.Point(113, 47);
            this._edgeSigmaNumeric.Maximum = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this._edgeSigmaNumeric.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this._edgeSigmaNumeric.Name = "_edgeSigmaNumeric";
            this._edgeSigmaNumeric.Size = new System.Drawing.Size(115, 22);
            this._edgeSigmaNumeric.TabIndex = 3;
            this._edgeSigmaNumeric.Value = new decimal(new int[] {
            12,
            0,
            0,
            65536});
            // 
            // edgeThresholdLabel
            // 
            this.edgeThresholdLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeThresholdLabel.Location = new System.Drawing.Point(3, 70);
            this.edgeThresholdLabel.Name = "edgeThresholdLabel";
            this.edgeThresholdLabel.Size = new System.Drawing.Size(104, 26);
            this.edgeThresholdLabel.TabIndex = 4;
            this.edgeThresholdLabel.Text = "Threshold";
            this.edgeThresholdLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _edgeThresholdNumeric
            // 
            this._edgeThresholdNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeThresholdNumeric.Location = new System.Drawing.Point(113, 73);
            this._edgeThresholdNumeric.Maximum = new decimal(new int[] {
            80,
            0,
            0,
            0});
            this._edgeThresholdNumeric.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this._edgeThresholdNumeric.Name = "_edgeThresholdNumeric";
            this._edgeThresholdNumeric.Size = new System.Drawing.Size(115, 22);
            this._edgeThresholdNumeric.TabIndex = 5;
            this._edgeThresholdNumeric.Value = new decimal(new int[] {
            25,
            0,
            0,
            0});
            // 
            // edgePolarityLabel
            // 
            this.edgePolarityLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgePolarityLabel.Location = new System.Drawing.Point(3, 96);
            this.edgePolarityLabel.Name = "edgePolarityLabel";
            this.edgePolarityLabel.Size = new System.Drawing.Size(104, 26);
            this.edgePolarityLabel.TabIndex = 6;
            this.edgePolarityLabel.Text = "Polarity";
            this.edgePolarityLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _edgePolarityCombo
            // 
            this._edgePolarityCombo.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgePolarityCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._edgePolarityCombo.Items.AddRange(new object[] {
            "all",
            "positive",
            "negative"});
            this._edgePolarityCombo.Location = new System.Drawing.Point(113, 99);
            this._edgePolarityCombo.Name = "_edgePolarityCombo";
            this._edgePolarityCombo.Size = new System.Drawing.Size(115, 20);
            this._edgePolarityCombo.TabIndex = 7;
            // 
            // edgeSelectorLabel
            // 
            this.edgeSelectorLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeSelectorLabel.Location = new System.Drawing.Point(3, 122);
            this.edgeSelectorLabel.Name = "edgeSelectorLabel";
            this.edgeSelectorLabel.Size = new System.Drawing.Size(104, 26);
            this.edgeSelectorLabel.TabIndex = 8;
            this.edgeSelectorLabel.Text = "Selector";
            this.edgeSelectorLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _edgeSelectorCombo
            // 
            this._edgeSelectorCombo.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeSelectorCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._edgeSelectorCombo.Items.AddRange(new object[] {
            "all",
            "first",
            "last"});
            this._edgeSelectorCombo.Location = new System.Drawing.Point(113, 125);
            this._edgeSelectorCombo.Name = "_edgeSelectorCombo";
            this._edgeSelectorCombo.Size = new System.Drawing.Size(115, 20);
            this._edgeSelectorCombo.TabIndex = 9;
            // 
            // edgeSubpixelLabel
            // 
            this.edgeSubpixelLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeSubpixelLabel.Location = new System.Drawing.Point(3, 148);
            this.edgeSubpixelLabel.Name = "edgeSubpixelLabel";
            this.edgeSubpixelLabel.Size = new System.Drawing.Size(104, 26);
            this.edgeSubpixelLabel.TabIndex = 10;
            this.edgeSubpixelLabel.Text = "Subpixel";
            this.edgeSubpixelLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _edgeSubpixelMethodCombo
            // 
            this._edgeSubpixelMethodCombo.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeSubpixelMethodCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._edgeSubpixelMethodCombo.Items.AddRange(new object[] {
            "parabolic",
            "gaussian",
            "none"});
            this._edgeSubpixelMethodCombo.Location = new System.Drawing.Point(113, 151);
            this._edgeSubpixelMethodCombo.Name = "_edgeSubpixelMethodCombo";
            this._edgeSubpixelMethodCombo.Size = new System.Drawing.Size(115, 20);
            this._edgeSubpixelMethodCombo.TabIndex = 11;
            // 
            // edgeRoiWidthLabel
            // 
            this.edgeRoiWidthLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeRoiWidthLabel.Location = new System.Drawing.Point(3, 174);
            this.edgeRoiWidthLabel.Name = "edgeRoiWidthLabel";
            this.edgeRoiWidthLabel.Size = new System.Drawing.Size(104, 26);
            this.edgeRoiWidthLabel.TabIndex = 12;
            this.edgeRoiWidthLabel.Text = "ROI Width";
            this.edgeRoiWidthLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _edgeRoiWidthNumeric
            // 
            this._edgeRoiWidthNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeRoiWidthNumeric.Location = new System.Drawing.Point(113, 177);
            this._edgeRoiWidthNumeric.Maximum = new decimal(new int[] {
            5000,
            0,
            0,
            0});
            this._edgeRoiWidthNumeric.Minimum = new decimal(new int[] {
            20,
            0,
            0,
            0});
            this._edgeRoiWidthNumeric.Name = "_edgeRoiWidthNumeric";
            this._edgeRoiWidthNumeric.Size = new System.Drawing.Size(115, 22);
            this._edgeRoiWidthNumeric.TabIndex = 13;
            this._edgeRoiWidthNumeric.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            // 
            // edgeScanLengthLabel
            // 
            this.edgeScanLengthLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeScanLengthLabel.Location = new System.Drawing.Point(3, 200);
            this.edgeScanLengthLabel.Name = "edgeScanLengthLabel";
            this.edgeScanLengthLabel.Size = new System.Drawing.Size(104, 26);
            this.edgeScanLengthLabel.TabIndex = 14;
            this.edgeScanLengthLabel.Text = "Scan Length";
            this.edgeScanLengthLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _edgeScanLengthNumeric
            // 
            this._edgeScanLengthNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeScanLengthNumeric.Location = new System.Drawing.Point(113, 203);
            this._edgeScanLengthNumeric.Maximum = new decimal(new int[] {
            5000,
            0,
            0,
            0});
            this._edgeScanLengthNumeric.Minimum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this._edgeScanLengthNumeric.Name = "_edgeScanLengthNumeric";
            this._edgeScanLengthNumeric.Size = new System.Drawing.Size(115, 22);
            this._edgeScanLengthNumeric.TabIndex = 15;
            this._edgeScanLengthNumeric.Value = new decimal(new int[] {
            500,
            0,
            0,
            0});
            // 
            // _edgeDrawRoiCheck
            // 
            this.edgeTableLayout.SetColumnSpan(this._edgeDrawRoiCheck, 3);
            this._edgeDrawRoiCheck.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeDrawRoiCheck.Location = new System.Drawing.Point(3, 229);
            this._edgeDrawRoiCheck.Name = "_edgeDrawRoiCheck";
            this._edgeDrawRoiCheck.Size = new System.Drawing.Size(225, 20);
            this._edgeDrawRoiCheck.TabIndex = 16;
            this._edgeDrawRoiCheck.Text = "Draw ROI";
            this._edgeDrawRoiCheck.UseVisualStyleBackColor = true;
            this._edgeDrawRoiCheck.CheckedChanged += new System.EventHandler(this.EdgeDrawRoiCheck_CheckedChanged);
            // 
            // edgeMeasureModeLabel
            // 
            this.edgeMeasureModeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeMeasureModeLabel.Location = new System.Drawing.Point(3, 252);
            this.edgeMeasureModeLabel.Name = "edgeMeasureModeLabel";
            this.edgeMeasureModeLabel.Size = new System.Drawing.Size(104, 26);
            this.edgeMeasureModeLabel.TabIndex = 17;
            this.edgeMeasureModeLabel.Text = "Measure Mode";
            this.edgeMeasureModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _edgeMeasureModeCombo
            // 
            this._edgeMeasureModeCombo.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeMeasureModeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._edgeMeasureModeCombo.Items.AddRange(new object[] {
            "single_edge",
            "edge_pair"});
            this._edgeMeasureModeCombo.Location = new System.Drawing.Point(113, 255);
            this._edgeMeasureModeCombo.Name = "_edgeMeasureModeCombo";
            this._edgeMeasureModeCombo.Size = new System.Drawing.Size(115, 20);
            this._edgeMeasureModeCombo.TabIndex = 18;
            // 
            // edgeInterpolationLabel
            // 
            this.edgeInterpolationLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeInterpolationLabel.Location = new System.Drawing.Point(3, 278);
            this.edgeInterpolationLabel.Name = "edgeInterpolationLabel";
            this.edgeInterpolationLabel.Size = new System.Drawing.Size(104, 26);
            this.edgeInterpolationLabel.TabIndex = 19;
            this.edgeInterpolationLabel.Text = "Interpolation";
            this.edgeInterpolationLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _edgeInterpolationCombo
            // 
            this._edgeInterpolationCombo.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeInterpolationCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._edgeInterpolationCombo.Items.AddRange(new object[] {
            "nearest_neighbor",
            "bilinear",
            "bicubic"});
            this._edgeInterpolationCombo.Location = new System.Drawing.Point(113, 281);
            this._edgeInterpolationCombo.Name = "_edgeInterpolationCombo";
            this._edgeInterpolationCombo.Size = new System.Drawing.Size(115, 20);
            this._edgeInterpolationCombo.TabIndex = 20;
            // 
            // edgeAngleLabel
            // 
            this.edgeAngleLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeAngleLabel.Location = new System.Drawing.Point(3, 304);
            this.edgeAngleLabel.Name = "edgeAngleLabel";
            this.edgeAngleLabel.Size = new System.Drawing.Size(104, 26);
            this.edgeAngleLabel.TabIndex = 21;
            this.edgeAngleLabel.Text = "Angle (deg)";
            this.edgeAngleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _edgeAngleNumeric
            // 
            this._edgeAngleNumeric.DecimalPlaces = 1;
            this._edgeAngleNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeAngleNumeric.Location = new System.Drawing.Point(113, 307);
            this._edgeAngleNumeric.Maximum = new decimal(new int[] {
            180,
            0,
            0,
            0});
            this._edgeAngleNumeric.Minimum = new decimal(new int[] {
            180,
            0,
            0,
            -2147483648});
            this._edgeAngleNumeric.Name = "_edgeAngleNumeric";
            this._edgeAngleNumeric.Size = new System.Drawing.Size(115, 22);
            this._edgeAngleNumeric.TabIndex = 22;
            this._edgeAngleNumeric.Value = new decimal(new int[] {
            0,
            0,
            0,
            0});
            // 
            // arcMeasurePanel
            //
            this.arcMeasurePanel.ColumnCount = 4;
            this.edgeTableLayout.SetColumnSpan(this.arcMeasurePanel, 3);
            this.arcMeasurePanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 26F));
            this.arcMeasurePanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 24F));
            this.arcMeasurePanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 26F));
            this.arcMeasurePanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 24F));
            this.arcMeasurePanel.Controls.Add(this.arcMeasureHeader, 0, 0);
            this.arcMeasurePanel.Controls.Add(this.arcCenterRowLabel, 0, 1);
            this.arcMeasurePanel.Controls.Add(this._arcCenterRowNumeric, 1, 1);
            this.arcMeasurePanel.Controls.Add(this.arcCenterColLabel, 2, 1);
            this.arcMeasurePanel.Controls.Add(this._arcCenterColNumeric, 3, 1);
            this.arcMeasurePanel.Controls.Add(this.arcRadiusLabel, 0, 2);
            this.arcMeasurePanel.Controls.Add(this._arcRadiusNumeric, 1, 2);
            this.arcMeasurePanel.Controls.Add(this.arcAnnulusLabel, 2, 2);
            this.arcMeasurePanel.Controls.Add(this._arcAnnulusNumeric, 3, 2);
            this.arcMeasurePanel.Controls.Add(this.arcAngleStartLabel, 0, 3);
            this.arcMeasurePanel.Controls.Add(this._arcAngleStartNumeric, 1, 3);
            this.arcMeasurePanel.Controls.Add(this.arcAngleExtentLabel, 2, 3);
            this.arcMeasurePanel.Controls.Add(this._arcAngleExtentNumeric, 3, 3);
            this.arcMeasurePanel.Controls.Add(this.detectArcButton, 3, 4);
            this.arcMeasurePanel.Controls.Add(this._arcEditCheck, 0, 4);
            this.arcMeasurePanel.SetColumnSpan(this._arcEditCheck, 2);
            this.arcMeasurePanel.Controls.Add(this._sectorDrawCheck, 0, 5);
            this.arcMeasurePanel.SetColumnSpan(this._sectorDrawCheck, 4);
            this.arcMeasurePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.arcMeasurePanel.Location = new System.Drawing.Point(3, 339);
            this.arcMeasurePanel.Name = "arcMeasurePanel";
            this.arcMeasurePanel.RowCount = 6;
            this.arcMeasurePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66F));
            this.arcMeasurePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66F));
            this.arcMeasurePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66F));
            this.arcMeasurePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66F));
            this.arcMeasurePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66F));
            this.arcMeasurePanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.7F));
            this.arcMeasurePanel.Size = new System.Drawing.Size(225, 144);
            this.arcMeasurePanel.TabIndex = 23;
            //
            // arcMeasureHeader
            //
            this.arcMeasurePanel.SetColumnSpan(this.arcMeasureHeader, 4);
            this.arcMeasureHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            this.arcMeasureHeader.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold);
            this.arcMeasureHeader.Name = "arcMeasureHeader";
            this.arcMeasureHeader.Text = "Arc Measure: 圓周放射特徵 (齒/孔/刻度, 非圓邊界)";
            this.arcMeasureHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // arcCenterRowLabel
            //
            this.arcCenterRowLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.arcCenterRowLabel.Name = "arcCenterRowLabel";
            this.arcCenterRowLabel.Text = "Ctr Row";
            this.arcCenterRowLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _arcCenterRowNumeric
            //
            this._arcCenterRowNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._arcCenterRowNumeric.DecimalPlaces = 1;
            this._arcCenterRowNumeric.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            this._arcCenterRowNumeric.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this._arcCenterRowNumeric.Name = "_arcCenterRowNumeric";
            this._arcCenterRowNumeric.TabIndex = 1;
            this._arcCenterRowNumeric.Value = new decimal(new int[] { 200, 0, 0, 0 });
            //
            // arcCenterColLabel
            //
            this.arcCenterColLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.arcCenterColLabel.Name = "arcCenterColLabel";
            this.arcCenterColLabel.Text = "Ctr Col";
            this.arcCenterColLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _arcCenterColNumeric
            //
            this._arcCenterColNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._arcCenterColNumeric.DecimalPlaces = 1;
            this._arcCenterColNumeric.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            this._arcCenterColNumeric.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this._arcCenterColNumeric.Name = "_arcCenterColNumeric";
            this._arcCenterColNumeric.TabIndex = 2;
            this._arcCenterColNumeric.Value = new decimal(new int[] { 200, 0, 0, 0 });
            //
            // arcRadiusLabel
            //
            this.arcRadiusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.arcRadiusLabel.Name = "arcRadiusLabel";
            this.arcRadiusLabel.Text = "Radius";
            this.arcRadiusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _arcRadiusNumeric
            //
            this._arcRadiusNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._arcRadiusNumeric.DecimalPlaces = 1;
            this._arcRadiusNumeric.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            this._arcRadiusNumeric.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this._arcRadiusNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._arcRadiusNumeric.Name = "_arcRadiusNumeric";
            this._arcRadiusNumeric.TabIndex = 3;
            this._arcRadiusNumeric.Value = new decimal(new int[] { 100, 0, 0, 0 });
            //
            // arcAnnulusLabel
            //
            this.arcAnnulusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.arcAnnulusLabel.Name = "arcAnnulusLabel";
            this.arcAnnulusLabel.Text = "Annulus";
            this.arcAnnulusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _arcAnnulusNumeric
            //
            this._arcAnnulusNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._arcAnnulusNumeric.DecimalPlaces = 1;
            this._arcAnnulusNumeric.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            this._arcAnnulusNumeric.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            this._arcAnnulusNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._arcAnnulusNumeric.Name = "_arcAnnulusNumeric";
            this._arcAnnulusNumeric.TabIndex = 4;
            this._arcAnnulusNumeric.Value = new decimal(new int[] { 10, 0, 0, 0 });
            //
            // arcAngleStartLabel
            //
            this.arcAngleStartLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            // Segoe UI 下 "Start deg" 需 54px，欄寬僅 60px 扣掉左右各 3px 預設邊界後剛好不夠，
            // 會被截成 "Start"。清掉水平邊界換回 6px，不必從數值欄借寬度。
            this.arcAngleStartLabel.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this.arcAngleStartLabel.Name = "arcAngleStartLabel";
            this.arcAngleStartLabel.Text = "Start deg";
            this.arcAngleStartLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _arcAngleStartNumeric
            //
            this._arcAngleStartNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._arcAngleStartNumeric.DecimalPlaces = 1;
            this._arcAngleStartNumeric.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            this._arcAngleStartNumeric.Maximum = new decimal(new int[] { 360, 0, 0, 0 });
            this._arcAngleStartNumeric.Name = "_arcAngleStartNumeric";
            this._arcAngleStartNumeric.TabIndex = 5;
            //
            // arcAngleExtentLabel
            //
            this.arcAngleExtentLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.arcAngleExtentLabel.Name = "arcAngleExtentLabel";
            this.arcAngleExtentLabel.Text = "Ext deg";
            this.arcAngleExtentLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _arcAngleExtentNumeric
            //
            this._arcAngleExtentNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this._arcAngleExtentNumeric.DecimalPlaces = 1;
            this._arcAngleExtentNumeric.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            this._arcAngleExtentNumeric.Maximum = new decimal(new int[] { 360, 0, 0, 0 });
            this._arcAngleExtentNumeric.Minimum = new decimal(new int[] { 360, 0, 0, -2147483648 });
            this._arcAngleExtentNumeric.Name = "_arcAngleExtentNumeric";
            this._arcAngleExtentNumeric.TabIndex = 6;
            this._arcAngleExtentNumeric.Value = new decimal(new int[] { 180, 0, 0, 0 });
            //
            // detectArcButton
            //
            this.detectArcButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.detectArcButton.Name = "detectArcButton";
            this.detectArcButton.TabIndex = 7;
            this.detectArcButton.Text = "Detect &Arc";
            this.detectArcButton.UseVisualStyleBackColor = true;
            this.detectArcButton.Click += new System.EventHandler(this.DetectArcButton_Click);
            //
            // _arcEditCheck
            //
            this._arcEditCheck.Dock = System.Windows.Forms.DockStyle.Fill;
            this._arcEditCheck.Name = "_arcEditCheck";
            this._arcEditCheck.TabIndex = 8;
            this._arcEditCheck.Text = "互動編輯";
            this._arcEditCheck.UseVisualStyleBackColor = true;
            this._arcEditCheck.CheckedChanged += new System.EventHandler(this.ArcEditCheck_CheckedChanged);
            //
            // _sectorDrawCheck
            //
            this._sectorDrawCheck.Dock = System.Windows.Forms.DockStyle.Fill;
            this._sectorDrawCheck.Name = "_sectorDrawCheck";
            this._sectorDrawCheck.TabIndex = 9;
            this._sectorDrawCheck.Text = "扇形 ROI（拖曳繪製）";
            this._sectorDrawCheck.UseVisualStyleBackColor = true;
            this._sectorDrawCheck.CheckedChanged += new System.EventHandler(this.SectorDrawCheck_CheckedChanged);
            //
            // edgeButtonPanel
            //
            this.edgeButtonPanel.ColumnCount = 2;
            this.edgeTableLayout.SetColumnSpan(this.edgeButtonPanel, 3);
            this.edgeButtonPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.edgeButtonPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.edgeButtonPanel.Controls.Add(this._runEdgeDetectionButton, 0, 0);
            this.edgeButtonPanel.Controls.Add(this._clearEdgeDetectionButton, 1, 0);
            this.edgeButtonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.edgeButtonPanel.Location = new System.Drawing.Point(3, 333);
            this.edgeButtonPanel.Name = "edgeButtonPanel";
            // 刪除 Fit×4 後只剩 Detect / Clear 一列。
            this.edgeButtonPanel.RowCount = 1;
            this.edgeButtonPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.edgeButtonPanel.Size = new System.Drawing.Size(225, 84);
            this.edgeButtonPanel.TabIndex = 23;
            // 
            // _runEdgeDetectionButton
            // 
            this._runEdgeDetectionButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._runEdgeDetectionButton.Location = new System.Drawing.Point(3, 3);
            this._runEdgeDetectionButton.Name = "_runEdgeDetectionButton";
            this._runEdgeDetectionButton.Size = new System.Drawing.Size(106, 20);
            this._runEdgeDetectionButton.TabIndex = 0;
            this._runEdgeDetectionButton.Text = "&Detect";
            this._runEdgeDetectionButton.UseVisualStyleBackColor = true;
            this._runEdgeDetectionButton.Click += new System.EventHandler(this.RunEdgeDetectionButton_Click);
            // 
            // _clearEdgeDetectionButton
            // 
            this._clearEdgeDetectionButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._clearEdgeDetectionButton.Location = new System.Drawing.Point(115, 3);
            this._clearEdgeDetectionButton.Name = "_clearEdgeDetectionButton";
            this._clearEdgeDetectionButton.Size = new System.Drawing.Size(107, 20);
            this._clearEdgeDetectionButton.TabIndex = 1;
            this._clearEdgeDetectionButton.Text = "C&lear";
            this._clearEdgeDetectionButton.UseVisualStyleBackColor = true;
            this._clearEdgeDetectionButton.Click += new System.EventHandler(this.ClearEdgeDetectionButton_Click);
            //
            // _edgeStatusLabel
            //
            this.edgeTableLayout.SetColumnSpan(this._edgeStatusLabel, 3);
            this._edgeStatusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeStatusLabel.Location = new System.Drawing.Point(3, 388);
            this._edgeStatusLabel.Name = "_edgeStatusLabel";
            this._edgeStatusLabel.Size = new System.Drawing.Size(225, 22);
            this._edgeStatusLabel.TabIndex = 24;
            this._edgeStatusLabel.Text = "Draw ROI, then Detect";
            this._edgeStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _edgeResultsGrid
            //
            this._edgeResultsGrid.AllowUserToAddRows = false;
            this._edgeResultsGrid.AllowUserToDeleteRows = false;
            this._edgeResultsGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this._edgeResultsGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this._edgeResultsGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.edgeIndexColumn,
            this.edgeRowColumn,
            this.edgeColumnColumn,
            this.edgeAmplitudeColumn,
            this.edgeDistanceColumn});
            this.edgeTableLayout.SetColumnSpan(this._edgeResultsGrid, 3);
            this._edgeResultsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._edgeResultsGrid.Location = new System.Drawing.Point(3, 509);
            this._edgeResultsGrid.Name = "_edgeResultsGrid";
            this._edgeResultsGrid.ReadOnly = true;
            this._edgeResultsGrid.RowHeadersVisible = false;
            this._edgeResultsGrid.Size = new System.Drawing.Size(225, 49);
            this._edgeResultsGrid.TabIndex = 27;
            // 
            // edgeIndexColumn
            // 
            this.edgeIndexColumn.HeaderText = "#";
            this.edgeIndexColumn.Name = "edgeIndexColumn";
            this.edgeIndexColumn.ReadOnly = true;
            // 
            // edgeRowColumn
            // 
            this.edgeRowColumn.HeaderText = "Row";
            this.edgeRowColumn.Name = "edgeRowColumn";
            this.edgeRowColumn.ReadOnly = true;
            // 
            // edgeColumnColumn
            // 
            this.edgeColumnColumn.HeaderText = "Column";
            this.edgeColumnColumn.Name = "edgeColumnColumn";
            this.edgeColumnColumn.ReadOnly = true;
            // 
            // edgeAmplitudeColumn
            // 
            this.edgeAmplitudeColumn.HeaderText = "Amp";
            this.edgeAmplitudeColumn.Name = "edgeAmplitudeColumn";
            this.edgeAmplitudeColumn.ReadOnly = true;
            // 
            // edgeDistanceColumn
            // 
            this.edgeDistanceColumn.HeaderText = "Dist";
            this.edgeDistanceColumn.Name = "edgeDistanceColumn";
            this.edgeDistanceColumn.ReadOnly = true;
            // 
            // measurementTabPage
            // 
            this.measurementTabPage.Controls.Add(this.measurementBox);
            this.measurementTabPage.Location = new System.Drawing.Point(4, 22);
            this.measurementTabPage.Name = "measurementTabPage";
            this.measurementTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.measurementTabPage.Size = new System.Drawing.Size(253, 608);
            this.measurementTabPage.TabIndex = 2;
            this.measurementTabPage.Text = "③ 量測";
            this.measurementTabPage.UseVisualStyleBackColor = true;
            // 
            // measurementBox
            // 
            this.measurementBox.Controls.Add(this.measurementTableLayout);
            this.measurementBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.measurementBox.Location = new System.Drawing.Point(3, 3);
            this.measurementBox.Name = "measurementBox";
            this.measurementBox.Padding = new System.Windows.Forms.Padding(8, 18, 8, 8);
            this.measurementBox.Size = new System.Drawing.Size(247, 602);
            this.measurementBox.TabIndex = 0;
            this.measurementBox.TabStop = false;
            this.measurementBox.Text = "量測設定與結果";
            // 
            // measurementTableLayout
            // 
            this.measurementTableLayout.ColumnCount = 2;
            this.measurementTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 42F));
            this.measurementTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 58F));
            this.measurementTableLayout.Controls.Add(this.measurementPixelXLabel, 0, 0);
            this.measurementTableLayout.Controls.Add(this.measurementPixelSizeXNumeric, 1, 0);
            this.measurementTableLayout.Controls.Add(this.measurementPixelYLabel, 0, 1);
            this.measurementTableLayout.Controls.Add(this.measurementPixelSizeYNumeric, 1, 1);
            this.measurementTableLayout.Controls.Add(this.measureResultLabel, 0, 2);
            this.measurementTableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.measurementTableLayout.Location = new System.Drawing.Point(8, 33);
            this.measurementTableLayout.Name = "measurementTableLayout";
            // 刪除 Distance/Angle 獨立量測後只剩三列：pixel size X / Y 與結果訊息。
            // pixel size 不屬於「距離量測」功能，它是所有量測在配方未指定校正檔時的
            // 後備來源（ResolvePixelSize），故隨群組保留。
            this.measurementTableLayout.RowCount = 3;
            this.measurementTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.measurementTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.measurementTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.measurementTableLayout.Size = new System.Drawing.Size(231, 561);
            this.measurementTableLayout.TabIndex = 0;
            // 
            // measurementPixelXLabel
            // 
            this.measurementPixelXLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.measurementPixelXLabel.Location = new System.Drawing.Point(3, 26);
            this.measurementPixelXLabel.Name = "measurementPixelXLabel";
            this.measurementPixelXLabel.Size = new System.Drawing.Size(91, 26);
            this.measurementPixelXLabel.TabIndex = 2;
            this.measurementPixelXLabel.Text = "Pixel Size X (µm):";
            this.measurementPixelXLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // measurementPixelSizeXNumeric
            // 
            this.measurementPixelSizeXNumeric.DecimalPlaces = 2;
            this.measurementPixelSizeXNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this.measurementPixelSizeXNumeric.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.measurementPixelSizeXNumeric.Location = new System.Drawing.Point(100, 29);
            this.measurementPixelSizeXNumeric.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.measurementPixelSizeXNumeric.Name = "measurementPixelSizeXNumeric";
            this.measurementPixelSizeXNumeric.Size = new System.Drawing.Size(128, 22);
            this.measurementPixelSizeXNumeric.TabIndex = 3;
            this.measurementPixelSizeXNumeric.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // measurementPixelYLabel
            // 
            this.measurementPixelYLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.measurementPixelYLabel.Location = new System.Drawing.Point(3, 52);
            this.measurementPixelYLabel.Name = "measurementPixelYLabel";
            this.measurementPixelYLabel.Size = new System.Drawing.Size(91, 26);
            this.measurementPixelYLabel.TabIndex = 4;
            this.measurementPixelYLabel.Text = "Pixel Size Y (µm):";
            this.measurementPixelYLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // measurementPixelSizeYNumeric
            // 
            this.measurementPixelSizeYNumeric.DecimalPlaces = 2;
            this.measurementPixelSizeYNumeric.Dock = System.Windows.Forms.DockStyle.Fill;
            this.measurementPixelSizeYNumeric.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.measurementPixelSizeYNumeric.Location = new System.Drawing.Point(100, 55);
            this.measurementPixelSizeYNumeric.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.measurementPixelSizeYNumeric.Name = "measurementPixelSizeYNumeric";
            this.measurementPixelSizeYNumeric.Size = new System.Drawing.Size(128, 22);
            this.measurementPixelSizeYNumeric.TabIndex = 5;
            this.measurementPixelSizeYNumeric.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // measureResultLabel
            // 
            this.measurementTableLayout.SetColumnSpan(this.measureResultLabel, 2);
            this.measureResultLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.measureResultLabel.Location = new System.Drawing.Point(3, 386);
            this.measureResultLabel.Name = "measureResultLabel";
            this.measureResultLabel.Size = new System.Drawing.Size(225, 175);
            this.measureResultLabel.TabIndex = 11;
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.progressLabel,
            this.coordLabel,
            this.imageSizeLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 626);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1100, 22);
            this.statusStrip.TabIndex = 1;
            // 
            // progressLabel
            // 
            this.progressLabel.Name = "progressLabel";
            this.progressLabel.Size = new System.Drawing.Size(948, 17);
            this.progressLabel.Spring = true;
            this.progressLabel.Text = "Ready";
            this.progressLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // coordLabel
            // 
            this.coordLabel.Name = "coordLabel";
            this.coordLabel.Size = new System.Drawing.Size(79, 17);
            this.coordLabel.Text = "Row: -  Col: -";
            // 
            // imageSizeLabel
            // 
            this.imageSizeLabel.Name = "imageSizeLabel";
            this.imageSizeLabel.Size = new System.Drawing.Size(58, 17);
            this.imageSizeLabel.Text = "Size: - x -";
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            // 選 8.25pt 而非慣用的 9pt：右側面板已無寬度餘裕，9pt 會造成多處截字
            // （Pixel Size X (µm):、Start deg、EdgesSubPix 垂直被切）。Segoe UI 8.25pt 與原本的
            // Microsoft Sans Serif 8.25pt 行高相同（13px）、寬度僅多 3~7%，是能安全替換的尺寸。
            // 注意：上面的 AutoScaleMode.Font 不會在執行期依新字型重排版面（AutoScaleDimensions
            // 描述的是「設計當下」的字型度量），且 MainWindow.cs 的 NormalizeTemplateMatchingLayout()
            // 會在 OnLoad 用寫死座標重排 templateMatching 區 → 字型造成的截字只能逐點修版面。
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.ClientSize = new System.Drawing.Size(1100, 648);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.mainTableLayout);
            this.MinimumSize = new System.Drawing.Size(900, 500);
            this.Name = "MainWindow";
            this.Text = "Flash Measurement System - Template Matching";
            this.mainTableLayout.ResumeLayout(false);
            this.imageHostPanel.ResumeLayout(false);
            this.resultBannerPanel.ResumeLayout(false);
            this.rightPanel.ResumeLayout(false);
            this.featureTabControl.ResumeLayout(false);
            this.inspectionTabPage.ResumeLayout(false);
            this.templateMatchingBox.ResumeLayout(false);
            this.templateMatchingBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.minScoreNumeric)).EndInit();
            this.templateCreationBox.ResumeLayout(false);
            this.templateCreationBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pyramidNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.angleExtentNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.angleStartNumeric)).EndInit();
            this.imageQualityBox.ResumeLayout(false);
            this.edgeDetectionTabPage.ResumeLayout(false);
            this._edgeDetectionBox.ResumeLayout(false);
            this.edgeTableLayout.ResumeLayout(false);
            this.edgeAlgorithmPanel.ResumeLayout(false);
            this.edgeAlgorithmPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._edgeSigmaNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._edgeThresholdNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._edgeRoiWidthNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._edgeScanLengthNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._edgeAngleNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcCenterRowNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcCenterColNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcRadiusNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcAnnulusNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcAngleStartNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._arcAngleExtentNumeric)).EndInit();
            this.arcMeasurePanel.ResumeLayout(false);
            this.edgeButtonPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._edgeResultsGrid)).EndInit();
            this.measurementTabPage.ResumeLayout(false);
            this.measurementBox.ResumeLayout(false);
            this.measurementTableLayout.ResumeLayout(false);
            this.measurementTableLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.measurementPixelSizeXNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.measurementPixelSizeYNumeric)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel mainTableLayout;
        private HalconDotNet.HWindowControl hWindowControl;
        private System.Windows.Forms.Panel rightPanel;
        private System.Windows.Forms.TabControl featureTabControl;
        private System.Windows.Forms.TabPage inspectionTabPage;
        private System.Windows.Forms.TabPage edgeDetectionTabPage;
        private System.Windows.Forms.GroupBox imageQualityBox;
        private System.Windows.Forms.TabPage measurementTabPage;
        private System.Windows.Forms.TabPage recipeTabPage;
        private System.Windows.Forms.GroupBox measurementBox;
        private System.Windows.Forms.NumericUpDown measurementPixelSizeXNumeric;
        private System.Windows.Forms.NumericUpDown measurementPixelSizeYNumeric;
        private System.Windows.Forms.Label measureResultLabel;
        private System.Windows.Forms.Panel imageHostPanel;
        private System.Windows.Forms.Panel resultBannerPanel;
        private System.Windows.Forms.Label resultBannerLabel;
        private System.Windows.Forms.Label emptyStateGuideLabel;
        private System.Windows.Forms.TableLayoutPanel measurementTableLayout;
        private System.Windows.Forms.Label measurementPixelXLabel;
        private System.Windows.Forms.Label measurementPixelYLabel;
        private System.Windows.Forms.Button runIqcButton;
        private System.Windows.Forms.GroupBox _edgeDetectionBox;
        private System.Windows.Forms.TableLayoutPanel edgeTableLayout;
        private System.Windows.Forms.Label edgeAlgorithmLabel;
        private System.Windows.Forms.FlowLayoutPanel edgeAlgorithmPanel;
        private System.Windows.Forms.RadioButton _edgeMeasurePosRadio;
        private System.Windows.Forms.RadioButton _edgeSubPixRadio;
        private System.Windows.Forms.Label edgeSigmaLabel;
        private System.Windows.Forms.NumericUpDown _edgeSigmaNumeric;
        private System.Windows.Forms.Label edgeThresholdLabel;
        private System.Windows.Forms.NumericUpDown _edgeThresholdNumeric;
        private System.Windows.Forms.Label edgePolarityLabel;
        private System.Windows.Forms.ComboBox _edgePolarityCombo;
        private System.Windows.Forms.Label edgeSelectorLabel;
        private System.Windows.Forms.ComboBox _edgeSelectorCombo;
        private System.Windows.Forms.Label edgeSubpixelLabel;
        private System.Windows.Forms.ComboBox _edgeSubpixelMethodCombo;
        private System.Windows.Forms.Label edgeRoiWidthLabel;
        private System.Windows.Forms.NumericUpDown _edgeRoiWidthNumeric;
        private System.Windows.Forms.Label edgeScanLengthLabel;
        private System.Windows.Forms.NumericUpDown _edgeScanLengthNumeric;
        private System.Windows.Forms.Label edgeMeasureModeLabel;
        private System.Windows.Forms.ComboBox _edgeMeasureModeCombo;
        private System.Windows.Forms.Label edgeInterpolationLabel;
        private System.Windows.Forms.ComboBox _edgeInterpolationCombo;
        private System.Windows.Forms.Label edgeAngleLabel;
        private System.Windows.Forms.NumericUpDown _edgeAngleNumeric;
        private System.Windows.Forms.CheckBox _edgeDrawRoiCheck;
        private System.Windows.Forms.TableLayoutPanel edgeButtonPanel;
        private System.Windows.Forms.Button _runEdgeDetectionButton;
        private System.Windows.Forms.Button _clearEdgeDetectionButton;
        private System.Windows.Forms.TableLayoutPanel arcMeasurePanel;
        private System.Windows.Forms.Label arcMeasureHeader;
        private System.Windows.Forms.Label arcCenterRowLabel;
        private System.Windows.Forms.NumericUpDown _arcCenterRowNumeric;
        private System.Windows.Forms.Label arcCenterColLabel;
        private System.Windows.Forms.NumericUpDown _arcCenterColNumeric;
        private System.Windows.Forms.Label arcRadiusLabel;
        private System.Windows.Forms.NumericUpDown _arcRadiusNumeric;
        private System.Windows.Forms.Label arcAngleStartLabel;
        private System.Windows.Forms.NumericUpDown _arcAngleStartNumeric;
        private System.Windows.Forms.Label arcAngleExtentLabel;
        private System.Windows.Forms.NumericUpDown _arcAngleExtentNumeric;
        private System.Windows.Forms.Label arcAnnulusLabel;
        private System.Windows.Forms.NumericUpDown _arcAnnulusNumeric;
        private System.Windows.Forms.Button detectArcButton;
        private System.Windows.Forms.CheckBox _arcEditCheck;
        private System.Windows.Forms.CheckBox _sectorDrawCheck;
        private System.Windows.Forms.Label _edgeStatusLabel;
        private System.Windows.Forms.DataGridView _edgeResultsGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn edgeIndexColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn edgeRowColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn edgeColumnColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn edgeAmplitudeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn edgeDistanceColumn;
        private System.Windows.Forms.Label iqcResultLabel;
        private System.Windows.Forms.GroupBox templateCreationBox;
        private System.Windows.Forms.Button roiClearButton;
        private System.Windows.Forms.CheckBox roiModeCheck;
        private System.Windows.Forms.Button createTemplateButton;
        private System.Windows.Forms.Button loadRefImageButton;
        private System.Windows.Forms.Label pyramidLabel;
        private System.Windows.Forms.NumericUpDown pyramidNumeric;
        private System.Windows.Forms.Label angleExtentLabel;
        private System.Windows.Forms.NumericUpDown angleExtentNumeric;
        private System.Windows.Forms.Label angleStartLabel;
        private System.Windows.Forms.NumericUpDown angleStartNumeric;
        private System.Windows.Forms.GroupBox templateMatchingBox;
        private System.Windows.Forms.TextBox matchResultTextBox;
        private System.Windows.Forms.Button runMatchingButton;
        private System.Windows.Forms.Label minScoreLabel;
        private System.Windows.Forms.NumericUpDown minScoreNumeric;
        private System.Windows.Forms.ComboBox templateFileCombo;
        private System.Windows.Forms.Label templateLabel;
        private System.Windows.Forms.Button loadTestImageButton;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel progressLabel;
        private System.Windows.Forms.ToolStripStatusLabel coordLabel;
        private System.Windows.Forms.ToolStripStatusLabel imageSizeLabel;
    }
}

