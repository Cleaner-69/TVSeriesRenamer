namespace TVSeriesRenamer
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            groupApi = new GroupBox();
            btnOpenLog = new Button();
            lblVersion = new Label();
            btnApiHelp = new Button();
            btnToggleApiKey = new Button();
            btnSaveApiKey = new Button();
            txtApiKey = new TextBox();
            labelApiKey = new Label();
            groupSeries = new GroupBox();
            lblSelectedSeries = new Label();
            lstSeriesResults = new ListBox();
            btnFetchSeries = new Button();
            txtSeriesName = new TextBox();
            labelSeriesName = new Label();
            groupFiles = new GroupBox();
            lblFileStatus = new Label();
            lstOriginalFiles = new ListBox();
            btnClearFiles = new Button();
            btnRemoveSelected = new Button();
            btnAddFolder = new Button();
            btnAddFiles = new Button();
            groupOutput = new GroupBox();
            btnChooseOutputFolder = new Button();
            txtOutputFolder = new TextBox();
            labelOutputFolder = new Label();
            groupPreview = new GroupBox();
            chkForceRename = new CheckBox();
            btnUndo = new Button();
            btnRenameSelected = new Button();
            btnMatchFiles = new Button();
            labelNewNames = new Label();
            labelOriginalPreview = new Label();
            lstPreviewNew = new ListBox();
            lstPreviewOriginal = new ListBox();
            groupApi.SuspendLayout();
            groupSeries.SuspendLayout();
            groupFiles.SuspendLayout();
            groupOutput.SuspendLayout();
            groupPreview.SuspendLayout();
            SuspendLayout();
            // 
            // groupApi
            // 
            groupApi.Controls.Add(btnOpenLog);
            groupApi.Controls.Add(lblVersion);
            groupApi.Controls.Add(btnApiHelp);
            groupApi.Controls.Add(btnToggleApiKey);
            groupApi.Controls.Add(btnSaveApiKey);
            groupApi.Controls.Add(txtApiKey);
            groupApi.Controls.Add(labelApiKey);
            groupApi.Location = new Point(8, 8);
            groupApi.Name = "groupApi";
            groupApi.Size = new Size(1180, 70);
            groupApi.TabIndex = 0;
            groupApi.TabStop = false;
            groupApi.Text = "TVDB Authentication";
            // 
            // btnOpenLog
            // 
            btnOpenLog.Location = new Point(940, 26);
            btnOpenLog.Name = "btnOpenLog";
            btnOpenLog.Size = new Size(90, 25);
            btnOpenLog.TabIndex = 6;
            btnOpenLog.Text = "Log File";
            btnOpenLog.UseVisualStyleBackColor = true;
            btnOpenLog.Click += btnOpenLog_Click;
            // 
            // lblVersion
            // 
            lblVersion.AutoSize = true;
            lblVersion.Location = new Point(1096, 31);
            lblVersion.Name = "lblVersion";
            lblVersion.Size = new Size(63, 15);
            lblVersion.TabIndex = 5;
            lblVersion.Text = "Version 1.3";
            // 
            // btnApiHelp
            // 
            btnApiHelp.Location = new Point(844, 26);
            btnApiHelp.Name = "btnApiHelp";
            btnApiHelp.Size = new Size(90, 25);
            btnApiHelp.TabIndex = 4;
            btnApiHelp.Text = "Get API Key";
            btnApiHelp.UseVisualStyleBackColor = true;
            btnApiHelp.Click += btnApiHelp_Click;
            // 
            // btnToggleApiKey
            // 
            btnToggleApiKey.Location = new Point(748, 26);
            btnToggleApiKey.Name = "btnToggleApiKey";
            btnToggleApiKey.Size = new Size(90, 25);
            btnToggleApiKey.TabIndex = 3;
            btnToggleApiKey.Text = "Show Key";
            btnToggleApiKey.UseVisualStyleBackColor = true;
            btnToggleApiKey.Click += btnToggleApiKey_Click;
            // 
            // btnSaveApiKey
            // 
            btnSaveApiKey.Location = new Point(652, 26);
            btnSaveApiKey.Name = "btnSaveApiKey";
            btnSaveApiKey.Size = new Size(90, 25);
            btnSaveApiKey.TabIndex = 2;
            btnSaveApiKey.Text = "Save Key";
            btnSaveApiKey.UseVisualStyleBackColor = true;
            btnSaveApiKey.Click += btnSaveApiKey_Click;
            // 
            // txtApiKey
            // 
            txtApiKey.Location = new Point(98, 27);
            txtApiKey.Name = "txtApiKey";
            txtApiKey.Size = new Size(548, 23);
            txtApiKey.TabIndex = 1;
            // 
            // labelApiKey
            // 
            labelApiKey.AutoSize = true;
            labelApiKey.Location = new Point(16, 31);
            labelApiKey.Name = "labelApiKey";
            labelApiKey.Size = new Size(50, 15);
            labelApiKey.TabIndex = 0;
            labelApiKey.Text = "API Key:";
            // 
            // groupSeries
            // 
            groupSeries.Controls.Add(lblSelectedSeries);
            groupSeries.Controls.Add(lstSeriesResults);
            groupSeries.Controls.Add(btnFetchSeries);
            groupSeries.Controls.Add(txtSeriesName);
            groupSeries.Controls.Add(labelSeriesName);
            groupSeries.Location = new Point(8, 84);
            groupSeries.Name = "groupSeries";
            groupSeries.Size = new Size(1180, 150);
            groupSeries.TabIndex = 1;
            groupSeries.TabStop = false;
            groupSeries.Text = "Series";
            // 
            // lblSelectedSeries
            // 
            lblSelectedSeries.AutoSize = true;
            lblSelectedSeries.ForeColor = Color.DimGray;
            lblSelectedSeries.Location = new Point(98, 55);
            lblSelectedSeries.Name = "lblSelectedSeries";
            lblSelectedSeries.Size = new Size(99, 15);
            lblSelectedSeries.TabIndex = 4;
            lblSelectedSeries.Text = "No series selected";
            // 
            // lstSeriesResults
            // 
            lstSeriesResults.FormattingEnabled = true;
            lstSeriesResults.ItemHeight = 15;
            lstSeriesResults.Location = new Point(16, 76);
            lstSeriesResults.Name = "lstSeriesResults";
            lstSeriesResults.Size = new Size(1148, 64);
            lstSeriesResults.TabIndex = 3;
            lstSeriesResults.SelectedIndexChanged += lstSeriesResults_SelectedIndexChanged;
            // 
            // btnFetchSeries
            // 
            btnFetchSeries.Location = new Point(1046, 24);
            btnFetchSeries.Name = "btnFetchSeries";
            btnFetchSeries.Size = new Size(118, 25);
            btnFetchSeries.TabIndex = 2;
            btnFetchSeries.Text = "Search TVDB";
            btnFetchSeries.UseVisualStyleBackColor = true;
            btnFetchSeries.Click += btnFetchSeries_Click;
            // 
            // txtSeriesName
            // 
            txtSeriesName.Location = new Point(98, 25);
            txtSeriesName.Name = "txtSeriesName";
            txtSeriesName.Size = new Size(942, 23);
            txtSeriesName.TabIndex = 1;
            txtSeriesName.TextChanged += txtSeriesName_TextChanged;
            // 
            // labelSeriesName
            // 
            labelSeriesName.AutoSize = true;
            labelSeriesName.Location = new Point(16, 29);
            labelSeriesName.Name = "labelSeriesName";
            labelSeriesName.Size = new Size(75, 15);
            labelSeriesName.TabIndex = 0;
            labelSeriesName.Text = "Series Name:";
            // 
            // groupFiles
            // 
            groupFiles.Controls.Add(lblFileStatus);
            groupFiles.Controls.Add(lstOriginalFiles);
            groupFiles.Controls.Add(btnClearFiles);
            groupFiles.Controls.Add(btnRemoveSelected);
            groupFiles.Controls.Add(btnAddFolder);
            groupFiles.Controls.Add(btnAddFiles);
            groupFiles.Location = new Point(8, 240);
            groupFiles.Name = "groupFiles";
            groupFiles.Size = new Size(1180, 220);
            groupFiles.TabIndex = 2;
            groupFiles.TabStop = false;
            groupFiles.Text = "Files";
            // 
            // lblFileStatus
            // 
            lblFileStatus.AutoSize = true;
            lblFileStatus.Location = new Point(16, 194);
            lblFileStatus.Name = "lblFileStatus";
            lblFileStatus.Size = new Size(197, 15);
            lblFileStatus.TabIndex = 5;
            lblFileStatus.Text = "0 file(s) loaded | 0 selected | 0 matched";
            // 
            // lstOriginalFiles
            // 
            lstOriginalFiles.FormattingEnabled = true;
            lstOriginalFiles.HorizontalScrollbar = true;
            lstOriginalFiles.ItemHeight = 15;
            lstOriginalFiles.Location = new Point(16, 58);
            lstOriginalFiles.Name = "lstOriginalFiles";
            lstOriginalFiles.SelectionMode = SelectionMode.MultiExtended;
            lstOriginalFiles.Size = new Size(1148, 124);
            lstOriginalFiles.TabIndex = 4;
            lstOriginalFiles.SelectedIndexChanged += lstOriginalFiles_SelectedIndexChanged;
            // 
            // btnClearFiles
            // 
            btnClearFiles.Location = new Point(319, 24);
            btnClearFiles.Name = "btnClearFiles";
            btnClearFiles.Size = new Size(90, 25);
            btnClearFiles.TabIndex = 3;
            btnClearFiles.Text = "Clear All";
            btnClearFiles.UseVisualStyleBackColor = true;
            btnClearFiles.Click += btnClearFiles_Click;
            // 
            // btnRemoveSelected
            // 
            btnRemoveSelected.Location = new Point(207, 24);
            btnRemoveSelected.Name = "btnRemoveSelected";
            btnRemoveSelected.Size = new Size(106, 25);
            btnRemoveSelected.TabIndex = 2;
            btnRemoveSelected.Text = "Remove Selected";
            btnRemoveSelected.UseVisualStyleBackColor = true;
            btnRemoveSelected.Click += btnRemoveSelected_Click;
            // 
            // btnAddFolder
            // 
            btnAddFolder.Location = new Point(111, 24);
            btnAddFolder.Name = "btnAddFolder";
            btnAddFolder.Size = new Size(90, 25);
            btnAddFolder.TabIndex = 1;
            btnAddFolder.Text = "Add Folder";
            btnAddFolder.UseVisualStyleBackColor = true;
            btnAddFolder.Click += btnAddFolder_Click;
            // 
            // btnAddFiles
            // 
            btnAddFiles.Location = new Point(16, 24);
            btnAddFiles.Name = "btnAddFiles";
            btnAddFiles.Size = new Size(90, 25);
            btnAddFiles.TabIndex = 0;
            btnAddFiles.Text = "Add Files";
            btnAddFiles.UseVisualStyleBackColor = true;
            btnAddFiles.Click += btnAddFiles_Click;
            // 
            // groupOutput
            // 
            groupOutput.Controls.Add(btnChooseOutputFolder);
            groupOutput.Controls.Add(txtOutputFolder);
            groupOutput.Controls.Add(labelOutputFolder);
            groupOutput.Location = new Point(8, 466);
            groupOutput.Name = "groupOutput";
            groupOutput.Size = new Size(1180, 70);
            groupOutput.TabIndex = 3;
            groupOutput.TabStop = false;
            groupOutput.Text = "Output";
            // 
            // btnChooseOutputFolder
            // 
            btnChooseOutputFolder.Location = new Point(1046, 26);
            btnChooseOutputFolder.Name = "btnChooseOutputFolder";
            btnChooseOutputFolder.Size = new Size(118, 25);
            btnChooseOutputFolder.TabIndex = 2;
            btnChooseOutputFolder.Text = "Choose Folder";
            btnChooseOutputFolder.UseVisualStyleBackColor = true;
            btnChooseOutputFolder.Click += btnChooseOutputFolder_Click;
            // 
            // txtOutputFolder
            // 
            txtOutputFolder.Location = new Point(110, 27);
            txtOutputFolder.Name = "txtOutputFolder";
            txtOutputFolder.ReadOnly = true;
            txtOutputFolder.Size = new Size(930, 23);
            txtOutputFolder.TabIndex = 1;
            // 
            // labelOutputFolder
            // 
            labelOutputFolder.AutoSize = true;
            labelOutputFolder.Location = new Point(16, 31);
            labelOutputFolder.Name = "labelOutputFolder";
            labelOutputFolder.Size = new Size(84, 15);
            labelOutputFolder.TabIndex = 0;
            labelOutputFolder.Text = "Output Folder:";
            // 
            // groupPreview
            // 
            groupPreview.Controls.Add(chkForceRename);
            groupPreview.Controls.Add(btnUndo);
            groupPreview.Controls.Add(btnRenameSelected);
            groupPreview.Controls.Add(btnMatchFiles);
            groupPreview.Controls.Add(labelNewNames);
            groupPreview.Controls.Add(labelOriginalPreview);
            groupPreview.Controls.Add(lstPreviewNew);
            groupPreview.Controls.Add(lstPreviewOriginal);
            groupPreview.Location = new Point(8, 542);
            groupPreview.Name = "groupPreview";
            groupPreview.Size = new Size(1180, 290);
            groupPreview.TabIndex = 4;
            groupPreview.TabStop = false;
            groupPreview.Text = "Match Preview";
            // 
            // chkForceRename
            // 
            chkForceRename.AutoSize = true;
            chkForceRename.Location = new Point(664, 255);
            chkForceRename.Name = "chkForceRename";
            chkForceRename.Size = new Size(234, 19);
            chkForceRename.TabIndex = 7;
            chkForceRename.Text = "Force Rename (ignore series mismatch)";
            chkForceRename.UseVisualStyleBackColor = true;
            // 
            // btnUndo
            // 
            btnUndo.Location = new Point(307, 250);
            btnUndo.Name = "btnUndo";
            btnUndo.Size = new Size(120, 25);
            btnUndo.TabIndex = 6;
            btnUndo.Text = "Undo Last Move";
            btnUndo.UseVisualStyleBackColor = true;
            btnUndo.Click += btnUndo_Click;
            // 
            // btnRenameSelected
            // 
            btnRenameSelected.Location = new Point(173, 250);
            btnRenameSelected.Name = "btnRenameSelected";
            btnRenameSelected.Size = new Size(128, 25);
            btnRenameSelected.TabIndex = 5;
            btnRenameSelected.Text = "Rename && Move";
            btnRenameSelected.UseVisualStyleBackColor = true;
            btnRenameSelected.Click += btnRenameSelected_Click;
            // 
            // btnMatchFiles
            // 
            btnMatchFiles.Location = new Point(16, 250);
            btnMatchFiles.Name = "btnMatchFiles";
            btnMatchFiles.Size = new Size(151, 25);
            btnMatchFiles.TabIndex = 4;
            btnMatchFiles.Text = "Match Selected Files";
            btnMatchFiles.UseVisualStyleBackColor = true;
            btnMatchFiles.Click += btnMatchFiles_Click;
            // 
            // labelNewNames
            // 
            labelNewNames.AutoSize = true;
            labelNewNames.Location = new Point(598, 24);
            labelNewNames.Name = "labelNewNames";
            labelNewNames.Size = new Size(70, 15);
            labelNewNames.TabIndex = 3;
            labelNewNames.Text = "New Names";
            // 
            // labelOriginalPreview
            // 
            labelOriginalPreview.AutoSize = true;
            labelOriginalPreview.Location = new Point(16, 24);
            labelOriginalPreview.Name = "labelOriginalPreview";
            labelOriginalPreview.Size = new Size(74, 15);
            labelOriginalPreview.TabIndex = 2;
            labelOriginalPreview.Text = "Original Files";
            // 
            // lstPreviewNew
            // 
            lstPreviewNew.FormattingEnabled = true;
            lstPreviewNew.HorizontalScrollbar = true;
            lstPreviewNew.ItemHeight = 15;
            lstPreviewNew.Location = new Point(598, 44);
            lstPreviewNew.Name = "lstPreviewNew";
            lstPreviewNew.Size = new Size(566, 199);
            lstPreviewNew.TabIndex = 1;
            // 
            // lstPreviewOriginal
            // 
            lstPreviewOriginal.FormattingEnabled = true;
            lstPreviewOriginal.HorizontalScrollbar = true;
            lstPreviewOriginal.ItemHeight = 15;
            lstPreviewOriginal.Location = new Point(16, 44);
            lstPreviewOriginal.Name = "lstPreviewOriginal";
            lstPreviewOriginal.Size = new Size(566, 199);
            lstPreviewOriginal.TabIndex = 0;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1196, 841);
            Controls.Add(groupPreview);
            Controls.Add(groupOutput);
            Controls.Add(groupFiles);
            Controls.Add(groupSeries);
            Controls.Add(groupApi);
            MinimumSize = new Size(1212, 880);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "TV Series Renamer v1.3";
            groupApi.ResumeLayout(false);
            groupApi.PerformLayout();
            groupSeries.ResumeLayout(false);
            groupSeries.PerformLayout();
            groupFiles.ResumeLayout(false);
            groupFiles.PerformLayout();
            groupOutput.ResumeLayout(false);
            groupOutput.PerformLayout();
            groupPreview.ResumeLayout(false);
            groupPreview.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private GroupBox groupApi;
        private Button btnOpenLog;
        private Label lblVersion;
        private Button btnApiHelp;
        private Button btnToggleApiKey;
        private Button btnSaveApiKey;
        private TextBox txtApiKey;
        private Label labelApiKey;
        private GroupBox groupSeries;
        private Label lblSelectedSeries;
        private ListBox lstSeriesResults;
        private Button btnFetchSeries;
        private TextBox txtSeriesName;
        private Label labelSeriesName;
        private GroupBox groupFiles;
        private Label lblFileStatus;
        private ListBox lstOriginalFiles;
        private Button btnClearFiles;
        private Button btnRemoveSelected;
        private Button btnAddFolder;
        private Button btnAddFiles;
        private GroupBox groupOutput;
        private Button btnChooseOutputFolder;
        private TextBox txtOutputFolder;
        private Label labelOutputFolder;
        private GroupBox groupPreview;
        private CheckBox chkForceRename;
        private Button btnUndo;
        private Button btnRenameSelected;
        private Button btnMatchFiles;
        private Label labelNewNames;
        private Label labelOriginalPreview;
        private ListBox lstPreviewNew;
        private ListBox lstPreviewOriginal;
    }
}
