namespace TVSeriesRenamer
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnSelectFolder = new Button();
            txtFolderPath = new TextBox();
            btnPreview = new Button();
            btnRename = new Button();
            label1 = new Label();
            txtSeriesName = new TextBox();
            btnFetchSeries = new Button();
            lstSeriesResults = new ListBox();
            label2 = new Label();
            textBox1 = new TextBox();
            txtApiKey = new TextBox();
            btnSaveApiKey = new Button();
            btnToggleApiKey = new Button();
            btnUndo = new Button();
            dgvPreview = new DataGridView();
            chkForceRename = new CheckBox();
            lblVersion = new Label();
            ((System.ComponentModel.ISupportInitialize)dgvPreview).BeginInit();
            SuspendLayout();
            // 
            // btnSelectFolder
            // 
            btnSelectFolder.Location = new Point(3, 50);
            btnSelectFolder.Name = "btnSelectFolder";
            btnSelectFolder.Size = new Size(96, 38);
            btnSelectFolder.TabIndex = 0;
            btnSelectFolder.Text = "Select Folder";
            btnSelectFolder.UseVisualStyleBackColor = true;
            btnSelectFolder.Click += btnSelectFolder_Click;
            // 
            // txtFolderPath
            // 
            txtFolderPath.Location = new Point(106, 59);
            txtFolderPath.Name = "txtFolderPath";
            txtFolderPath.Size = new Size(665, 23);
            txtFolderPath.TabIndex = 1;
            // 
            // btnPreview
            // 
            btnPreview.Location = new Point(777, 58);
            btnPreview.Name = "btnPreview";
            btnPreview.Size = new Size(75, 23);
            btnPreview.TabIndex = 3;
            btnPreview.Text = "Preview";
            btnPreview.UseVisualStyleBackColor = true;
            btnPreview.Click += btnPreview_Click;
            // 
            // btnRename
            // 
            btnRename.Location = new Point(3, 339);
            btnRename.Name = "btnRename";
            btnRename.Size = new Size(95, 23);
            btnRename.TabIndex = 4;
            btnRename.Text = "Apply Rename";
            btnRename.UseVisualStyleBackColor = true;
            btnRename.Click += btnRename_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(4, 374);
            label1.Name = "label1";
            label1.Size = new Size(75, 15);
            label1.TabIndex = 5;
            label1.Text = "Series Name:";
            // 
            // txtSeriesName
            // 
            txtSeriesName.Location = new Point(85, 371);
            txtSeriesName.Name = "txtSeriesName";
            txtSeriesName.Size = new Size(158, 23);
            txtSeriesName.TabIndex = 6;
            // 
            // btnFetchSeries
            // 
            btnFetchSeries.Location = new Point(249, 371);
            btnFetchSeries.Name = "btnFetchSeries";
            btnFetchSeries.Size = new Size(124, 23);
            btnFetchSeries.TabIndex = 7;
            btnFetchSeries.Text = "Fetch Series Info";
            btnFetchSeries.UseVisualStyleBackColor = true;
            btnFetchSeries.Click += btnFetchSeries_Click;
            // 
            // lstSeriesResults
            // 
            lstSeriesResults.FormattingEnabled = true;
            lstSeriesResults.Location = new Point(4, 400);
            lstSeriesResults.Name = "lstSeriesResults";
            lstSeriesResults.Size = new Size(951, 124);
            lstSeriesResults.TabIndex = 8;
            lstSeriesResults.SelectedIndexChanged += lstSeriesResults_SelectedIndexChanged;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(4, 24);
            label2.Name = "label2";
            label2.Size = new Size(50, 15);
            label2.TabIndex = 9;
            label2.Text = "API Key:";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(0, 0);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(100, 23);
            textBox1.TabIndex = 0;
            // 
            // txtApiKey
            // 
            txtApiKey.Location = new Point(60, 21);
            txtApiKey.Name = "txtApiKey";
            txtApiKey.Size = new Size(483, 23);
            txtApiKey.TabIndex = 10;
            txtApiKey.UseSystemPasswordChar = true;
            // 
            // btnSaveApiKey
            // 
            btnSaveApiKey.Location = new Point(549, 21);
            btnSaveApiKey.Name = "btnSaveApiKey";
            btnSaveApiKey.Size = new Size(75, 23);
            btnSaveApiKey.TabIndex = 11;
            btnSaveApiKey.Text = "Save Key";
            btnSaveApiKey.UseVisualStyleBackColor = true;
            btnSaveApiKey.Click += btnSaveApiKey_Click;
            // 
            // btnToggleApiKey
            // 
            btnToggleApiKey.Location = new Point(630, 20);
            btnToggleApiKey.Name = "btnToggleApiKey";
            btnToggleApiKey.Size = new Size(75, 23);
            btnToggleApiKey.TabIndex = 12;
            btnToggleApiKey.Text = "Show Key";
            btnToggleApiKey.UseVisualStyleBackColor = true;
            btnToggleApiKey.Click += btnToggleApiKey_Click;
            // 
            // btnUndo
            // 
            btnUndo.Location = new Point(105, 339);
            btnUndo.Name = "btnUndo";
            btnUndo.Size = new Size(115, 23);
            btnUndo.TabIndex = 13;
            btnUndo.Text = "Undo Last Rename";
            btnUndo.UseVisualStyleBackColor = true;
            btnUndo.Click += btnUndo_Click;
            // 
            // dgvPreview
            // 
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPreview.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPreview.Location = new Point(3, 88);
            dgvPreview.Name = "dgvPreview";
            dgvPreview.ReadOnly = true;
            dgvPreview.RowHeadersVisible = false;
            dgvPreview.Size = new Size(950, 245);
            dgvPreview.TabIndex = 14;
            // 
            // chkForceRename
            // 
            chkForceRename.AutoSize = true;
            chkForceRename.Location = new Point(226, 342);
            chkForceRename.Name = "chkForceRename";
            chkForceRename.Size = new Size(234, 19);
            chkForceRename.TabIndex = 15;
            chkForceRename.Text = "Force Rename (ignore series mismatch)";
            chkForceRename.UseVisualStyleBackColor = true;
            // 
            // lblVersion
            // 
            lblVersion.AutoSize = true;
            lblVersion.Location = new Point(886, 21);
            lblVersion.Name = "lblVersion";
            lblVersion.Size = new Size(63, 15);
            lblVersion.TabIndex = 16;
            lblVersion.Text = "Version 1.1";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(961, 531);
            Controls.Add(lblVersion);
            Controls.Add(chkForceRename);
            Controls.Add(dgvPreview);
            Controls.Add(btnUndo);
            Controls.Add(btnToggleApiKey);
            Controls.Add(btnSaveApiKey);
            Controls.Add(txtApiKey);
            Controls.Add(label2);
            Controls.Add(lstSeriesResults);
            Controls.Add(btnFetchSeries);
            Controls.Add(txtSeriesName);
            Controls.Add(label1);
            Controls.Add(btnRename);
            Controls.Add(btnPreview);
            Controls.Add(txtFolderPath);
            Controls.Add(btnSelectFolder);
            Name = "MainForm";
            Text = "TV Series Renamer v1.1";
            ((System.ComponentModel.ISupportInitialize)dgvPreview).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnSelectFolder;
        private TextBox txtFolderPath;
        private Button btnPreview;
        private Button btnRename;
        private Label label1;
        private TextBox txtSeriesName;
        private Button btnFetchSeries;
        private ListBox lstSeriesResults;
        private Label label2;
        private TextBox textBox1;
        private TextBox txtApiKey;
        private Button btnSaveApiKey;
        private Button btnToggleApiKey;
        private Button btnUndo;
        private DataGridView dgvPreview;
        private CheckBox chkForceRename;
        private Label lblVersion;
    }
}
