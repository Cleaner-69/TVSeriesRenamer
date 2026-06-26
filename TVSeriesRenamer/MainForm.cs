using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TVSeriesRenamer
{
    public partial class MainForm : Form
    {
        private enum OverwriteDecision
        {
            Ask,
            YesToAll,
            NoToAll,
            Cancel
        }

        private readonly ToolTip toolTip = new ToolTip();
        private readonly DataGridView dgvPreview = new DataGridView();
        private readonly Label lblPreviewSummary = new Label();

        private const string CurrentVersion = "v1.6";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/Cleaner-69/TVSeriesRenamer/releases/latest";
        private const string GitHubUserAgent = "TVSeriesRenamer";

        private static readonly HashSet<string> SupportedVideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v"
        };

        private string apiKey = "";
        private string apiToken = "";
        private bool isApiKeyVisible = false;
        private bool suppressSeriesNameTextChanged = false;
        private bool suppressFileSelectionChanged = false;
        private bool suppressNamingSettingsEvents = false;

        private readonly string settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TVSeriesRenamer"
        );

        private string SettingsFilePath => Path.Combine(settingsDirectory, "appsettings.json");
        private string LogFilePath => Path.Combine(settingsDirectory, "rename_log.txt");

        private readonly List<FileQueueItem> loadedFiles = new List<FileQueueItem>();
        private readonly List<RenamePreviewItem> previewItems = new List<RenamePreviewItem>();
        private readonly Stack<List<RenamePreviewItem>> undoStack = new Stack<List<RenamePreviewItem>>();

        private readonly List<SeriesSearchResult> seriesResults = new List<SeriesSearchResult>();
        private int selectedSeriesId = 0;
        private string selectedSeriesName = "";

        private readonly Dictionary<string, string> episodeTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public MainForm()
        {
            InitializeComponent();

            txtApiKey.UseSystemPasswordChar = true;
            btnToggleApiKey.Text = "Show Key";

            LoadApiKey();
            SetupToolTips();
            SetupDragDrop();
            SetupPreviewGrid();

            Text = $"TV Series Renamer {CurrentVersion}";
            lblVersion.Text = "Version 1.6";

            _ = CheckForUpdatesAsync();

            UpdateActionButtons();
            UpdateFileStatus();
        }

        public class FileQueueItem
        {
            public string FilePath { get; set; } = "";
            public override string ToString() => Path.GetFileName(FilePath);
        }

        public class RenamePreviewItem
        {
            public string OriginalPath { get; set; } = "";
            public string NewPath { get; set; } = "";
            public string Status { get; set; } = "";
            public string Message { get; set; } = "";
        }

        private class CompletedRenameOperation
        {
            public string OriginalPath { get; set; } = "";
            public string NewPath { get; set; } = "";
            public string BackupPath { get; set; } = "";
        }

        private class RenameOperationPlan
        {
            public List<RenamePreviewItem> ReadyItems { get; set; } = new List<RenamePreviewItem>();
            public List<string> BlockingIssues { get; set; } = new List<string>();
            public List<RenamePreviewItem> ExistingTargetItems { get; set; } = new List<RenamePreviewItem>();
        }

        public class SeriesSearchResult
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(Type)
                    ? $"{Name} [ID {Id}]"
                    : $"{Name} ({Type}) [ID {Id}]";
            }
        }

        public class SeriesDetectionResult
        {
            public string SuggestedSeriesName { get; set; } = "";
            public int MatchingFileCount { get; set; }
            public int TotalFileCount { get; set; }
            public bool IsConfident { get; set; }
        }

        public class AppSettings
        {
            public string ApiKey { get; set; } = "";
            public string NamingFormat { get; set; } = "S01E01";
            public string CustomNamingPattern { get; set; } = "{series} - {code} - {title}";
            public string LastOutputFolder { get; set; } = "";
        }

        private void SetupToolTips()
        {
            toolTip.SetToolTip(btnAddFiles, "Add one or more video files to the work list");
            toolTip.SetToolTip(btnAddFolder, "Add supported video files from a folder to the work list");
            toolTip.SetToolTip(btnRemoveSelected, "Remove selected files from the work list");
            toolTip.SetToolTip(btnClearFiles, "Clear all loaded files and preview data");
            toolTip.SetToolTip(btnChooseOutputFolder, "Choose where renamed files must be moved to");
            toolTip.SetToolTip(btnMatchFiles, "Build rename preview for selected files");
            toolTip.SetToolTip(btnRenameSelected, "Rename and move matched files to the output folder");
            toolTip.SetToolTip(btnUndo, "Undo the last successful rename/move operation");
            toolTip.SetToolTip(btnFetchSeries, "Search TVDB and fetch episode data for the selected series");
            toolTip.SetToolTip(btnSaveApiKey, "Save the TVDB API key");
            toolTip.SetToolTip(btnToggleApiKey, "Show or hide the TVDB API key");
            toolTip.SetToolTip(btnApiHelp, "Open TVDB API information page to get or manage your API key");
            toolTip.SetToolTip(btnOpenLog, "Open the rename log file");
            toolTip.SetToolTip(lstOriginalFiles, "Original files. Select the files to match and rename.");
            toolTip.SetToolTip(lstPreviewNew, "Generated new file names or match reasons.");
            toolTip.SetToolTip(dgvPreview, "Rename preview queue with status, original file, target file, and message.");
            toolTip.SetToolTip(lblDetectedSeries, "Detected series suggestion based on loaded file names");
            toolTip.SetToolTip(chkForceRename, "Override wrong-series safety checks for the current match operation");
            toolTip.SetToolTip(cmbNamingFormat, "Choose how the episode code is written in generated file names");
            toolTip.SetToolTip(txtCustomPattern, "Custom file name pattern. Supported tokens: {series}, {season}, {season00}, {episode}, {episode00}, {code}, {title}");
            toolTip.SetToolTip(txtOutputFolder, "Last output folder is saved and restored automatically when valid");
            toolTip.SetToolTip(lblPreviewSummary, "Preview summary: ready, warning, and error counts for the current match result");
        }

        private void SetupPreviewGrid()
        {
            labelOriginalPreview.Visible = false;
            labelNewNames.Visible = false;
            lstPreviewOriginal.Visible = false;
            lstPreviewNew.Visible = false;

            dgvPreview.Name = "dgvPreview";
            dgvPreview.Location = new Point(16, 24);
            dgvPreview.Size = new Size(1148, 219);
            dgvPreview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvPreview.ReadOnly = true;
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.AllowUserToResizeRows = false;
            dgvPreview.MultiSelect = true;
            dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPreview.RowHeadersVisible = false;
            dgvPreview.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPreview.BackgroundColor = SystemColors.Window;
            dgvPreview.BorderStyle = BorderStyle.FixedSingle;
            dgvPreview.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPreview.ShowCellToolTips = true;
            dgvPreview.CellDoubleClick += dgvPreview_CellDoubleClick;

            dgvPreview.Columns.Clear();
            dgvPreview.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 14, SortMode = DataGridViewColumnSortMode.Automatic });
            dgvPreview.Columns.Add(new DataGridViewTextBoxColumn { Name = "OriginalFile", HeaderText = "Original File", FillWeight = 34, SortMode = DataGridViewColumnSortMode.Automatic });
            dgvPreview.Columns.Add(new DataGridViewTextBoxColumn { Name = "NewFile", HeaderText = "New File", FillWeight = 34, SortMode = DataGridViewColumnSortMode.Automatic });
            dgvPreview.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "Message", FillWeight = 28, SortMode = DataGridViewColumnSortMode.Automatic });

            if (!groupPreview.Controls.Contains(dgvPreview))
                groupPreview.Controls.Add(dgvPreview);

            dgvPreview.BringToFront();
        }

        private void ClearPreviewGrid()
        {
            dgvPreview.Rows.Clear();
            SetPreviewSummary("Preview: none", Color.DimGray);
        }

        private void SetPreviewSummary(string text, Color colour)
        {
            lblPreviewSummary.Text = text;
            lblPreviewSummary.ForeColor = colour;
        }

        private void UpdatePreviewSummary()
        {
            if (previewItems.Count == 0)
            {
                SetPreviewSummary("Preview: none", Color.DimGray);
                return;
            }

            int readyCount = previewItems.Count(item => item.Status == "OK");
            int errorCount = previewItems.Count(item => item.Status == "ERROR");
            int warningCount = previewItems.Count - readyCount - errorCount;

            Color summaryColour = errorCount > 0
                ? Color.Firebrick
                : warningCount > 0 ? Color.DarkOrange : Color.Green;

            SetPreviewSummary($"Preview: {readyCount} ready | {warningCount} warning | {errorCount} error", summaryColour);
        }

        private void AddPreviewGridRow(string status, string originalFile, string newFile, string message)
        {
            int rowIndex = dgvPreview.Rows.Add(status, originalFile, newFile, message);
            DataGridViewRow row = dgvPreview.Rows[rowIndex];

            string tooltip = string.IsNullOrWhiteSpace(newFile)
                ? $"{status}: {message}"
                : $"{status}: {newFile} - {message}";

            // Apply tooltip to all cells (correct approach)
            foreach (DataGridViewCell cell in row.Cells)
            {
                cell.ToolTipText = tooltip;
            }

            switch (status.ToUpperInvariant())
            {
                case "OK":
                    row.DefaultCellStyle.ForeColor = Color.Green;
                    row.DefaultCellStyle.BackColor = Color.Honeydew;
                    break;
                case "OVERWRITE":
                    row.DefaultCellStyle.ForeColor = Color.DarkOrange;
                    row.DefaultCellStyle.BackColor = Color.FloralWhite;
                    break;
                case "PENDING":
                    row.DefaultCellStyle.ForeColor = Color.DimGray;
                    row.DefaultCellStyle.BackColor = Color.WhiteSmoke;
                    break;
                case "SKIP":
                case "NO MATCH":
                case "NO TVDB TITLE":
                case "WRONG SERIES":
                    row.DefaultCellStyle.ForeColor = Color.DarkGoldenrod;
                    row.DefaultCellStyle.BackColor = Color.LemonChiffon;
                    break;
                case "ERROR":
                    row.DefaultCellStyle.ForeColor = Color.Firebrick;
                    row.DefaultCellStyle.BackColor = Color.MistyRose;
                    break;
            }
        }

        private string GetPreviewDisplayStatus(RenamePreviewItem previewItem)
        {
            if (previewItem.Status == "OK" && previewItem.Message.StartsWith("Overwrite", StringComparison.OrdinalIgnoreCase))
                return "OVERWRITE";

            return previewItem.Status;
        }

        private string GetPreviewDisplayNewFile(RenamePreviewItem previewItem)
        {
            return string.IsNullOrWhiteSpace(previewItem.NewPath) ? "" : Path.GetFileName(previewItem.NewPath);
        }


        private void dgvPreview_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= previewItems.Count)
                return;

            RenamePreviewItem item = previewItems[e.RowIndex];
            string pathToOpen = File.Exists(item.OriginalPath) ? item.OriginalPath : item.NewPath;
            if (string.IsNullOrWhiteSpace(pathToOpen) || !File.Exists(pathToOpen))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select, \"{pathToOpen}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file location.\n\n{ex.Message}", "Open Location Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetupDragDrop()
        {
            AllowDrop = true;
            lstOriginalFiles.AllowDrop = true;
            DragEnter += FileDragEnter;
            DragDrop += FileDragDrop;
            lstOriginalFiles.DragEnter += FileDragEnter;
            lstOriginalFiles.DragDrop += FileDragDrop;
        }

        private void FileDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void FileDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            if (e.Data.GetData(DataFormats.FileDrop) is string[] droppedPaths)
                AddPathsToWorkList(droppedPaths, selectAddedItems: true);
        }

        private void UpdateActionButtons()
        {
            bool hasApiKey = !string.IsNullOrWhiteSpace(txtApiKey.Text) || !string.IsNullOrWhiteSpace(apiKey);
            bool hasSeriesName = !string.IsNullOrWhiteSpace(txtSeriesName.Text);
            bool hasFiles = loadedFiles.Count > 0;
            bool hasSelectedFiles = lstOriginalFiles.SelectedItems.Count > 0;
            bool hasOutputFolder = Directory.Exists(txtOutputFolder.Text);
            bool hasSeriesSelected = selectedSeriesId != 0 && !string.IsNullOrWhiteSpace(selectedSeriesName);
            bool hasEpisodeTitles = episodeTitles.Count > 0;
            bool hasRenamablePreview = previewItems.Any(item => item.Status == "OK");

            btnFetchSeries.Enabled = hasApiKey && hasSeriesName;
            btnRemoveSelected.Enabled = hasSelectedFiles;
            btnClearFiles.Enabled = hasFiles;
            btnMatchFiles.Enabled = hasFiles && hasSelectedFiles && hasOutputFolder && hasSeriesSelected && hasEpisodeTitles;
            btnRenameSelected.Enabled = hasRenamablePreview;
            btnUndo.Enabled = undoStack.Count > 0;
        }

        private void UpdateFileStatus()
        {
            lblFileStatus.Text = $"{loadedFiles.Count} file(s) loaded | {lstOriginalFiles.SelectedItems.Count} selected | {previewItems.Count} matched";
        }

        private void ClearPreview()
        {
            previewItems.Clear();
            lstPreviewOriginal.Items.Clear();
            lstPreviewNew.Items.Clear();
            ClearPreviewGrid();
            UpdateActionButtons();
            UpdateFileStatus();
        }

        private bool IsSupportedVideoFile(string filePath)
        {
            return SupportedVideoExtensions.Contains(Path.GetExtension(filePath));
        }

        private string GetSupportedExtensionsFilter()
        {
            return "Video Files|*.mkv;*.mp4;*.avi;*.mov;*.wmv;*.m4v|All Files|*.*";
        }

        private string GetSupportedExtensionsText()
        {
            return string.Join(", ", SupportedVideoExtensions.OrderBy(extension => extension));
        }

        private void AddPathsToWorkList(IEnumerable<string> paths, bool selectAddedItems)
        {
            ClearPreview();

            List<string> candidateFiles = new List<string>();

            foreach (string path in paths)
            {
                if (File.Exists(path))
                    candidateFiles.Add(path);
                else if (Directory.Exists(path))
                    candidateFiles.AddRange(Directory.GetFiles(path).Where(IsSupportedVideoFile));
            }

            int addedCount = 0;
            List<int> addedIndexes = new List<int>();

            foreach (string file in candidateFiles.Where(IsSupportedVideoFile))
            {
                if (loadedFiles.Any(item => string.Equals(item.FilePath, file, StringComparison.OrdinalIgnoreCase)))
                    continue;

                FileQueueItem queueItem = new FileQueueItem { FilePath = file };
                loadedFiles.Add(queueItem);
                int index = lstOriginalFiles.Items.Add(queueItem);
                addedIndexes.Add(index);
                addedCount++;
            }

            if (selectAddedItems)
            {
                lstOriginalFiles.ClearSelected();
                foreach (int index in addedIndexes)
                    lstOriginalFiles.SetSelected(index, true);
            }

            if (addedCount == 0)
            {
                MessageBox.Show(
                    $"No new supported video files were added.\n\nSupported file types: {GetSupportedExtensionsText()}",
                    "No Files Added",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            UpdateActionButtons();
            UpdateFileStatus();

            if (addedCount > 0)
                _ = AutoDetectAndSearchFromFilesAsync();
        }

        private List<FileQueueItem> GetSelectedFileQueueItems()
        {
            return lstOriginalFiles.SelectedItems.OfType<FileQueueItem>().ToList();
        }
        private List<FileQueueItem> GetFilesMatchingSeriesName(string seriesName)
        {
            List<FileQueueItem> matchingFiles = new List<FileQueueItem>();

            if (string.IsNullOrWhiteSpace(seriesName))
                return matchingFiles;

            string normalisedSeriesName = NormaliseSeriesName(seriesName);
            if (string.IsNullOrWhiteSpace(normalisedSeriesName))
                return matchingFiles;

            foreach (FileQueueItem queueItem in loadedFiles)
            {
                string candidateSeriesName = ExtractSeriesSearchCandidate(queueItem.FilePath);
                string normalisedCandidateSeriesName = NormaliseSeriesName(candidateSeriesName);

                if (string.IsNullOrWhiteSpace(normalisedCandidateSeriesName))
                    continue;

                bool isMatch = normalisedCandidateSeriesName.Contains(normalisedSeriesName, StringComparison.OrdinalIgnoreCase)
                    || normalisedSeriesName.Contains(normalisedCandidateSeriesName, StringComparison.OrdinalIgnoreCase);

                if (isMatch)
                    matchingFiles.Add(queueItem);
            }

            return matchingFiles;
        }

        private int SelectFilesMatchingSeriesName(string seriesName)
        {
            List<FileQueueItem> matchingFiles = GetFilesMatchingSeriesName(seriesName);

            suppressFileSelectionChanged = true;
            lstOriginalFiles.ClearSelected();

            for (int index = 0; index < lstOriginalFiles.Items.Count; index++)
            {
                FileQueueItem? listItem = lstOriginalFiles.Items[index] as FileQueueItem;
                if (listItem == null)
                    continue;

                if (matchingFiles.Any(item => string.Equals(item.FilePath, listItem.FilePath, StringComparison.OrdinalIgnoreCase)))
                    lstOriginalFiles.SetSelected(index, true);
            }

            suppressFileSelectionChanged = false;

            PopulateSelectedOriginalFilesPreview();
            UpdateActionButtons();
            UpdateFileStatus();

            return matchingFiles.Count;
        }

        private void PopulateSelectedOriginalFilesPreview()
        {
            if (previewItems.Count > 0)
                return;

            lstPreviewOriginal.Items.Clear();
            lstPreviewNew.Items.Clear();
            ClearPreviewGrid();

            foreach (FileQueueItem queueItem in GetSelectedFileQueueItems())
            {
                string originalFile = Path.GetFileName(queueItem.FilePath);
                string message = !Directory.Exists(txtOutputFolder.Text)
                    ? "Choose output folder first"
                    : "Pending match - click Match Selected Files";

                lstPreviewOriginal.Items.Add(originalFile);
                lstPreviewNew.Items.Add(message);
                AddPreviewGridRow("PENDING", originalFile, "", message);
            }
        }

        private SeriesDetectionResult DetectSeriesFromLoadedFiles()
        {
            SeriesDetectionResult result = new SeriesDetectionResult { TotalFileCount = loadedFiles.Count };

            if (loadedFiles.Count == 0)
                return result;

            Dictionary<string, (string DisplayName, int Count)> candidates = new Dictionary<string, (string DisplayName, int Count)>(StringComparer.OrdinalIgnoreCase);

            foreach (FileQueueItem file in loadedFiles)
            {
                string candidate = ExtractSeriesSearchCandidate(file.FilePath);
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                string normalised = NormaliseSeriesName(candidate);
                if (string.IsNullOrWhiteSpace(normalised))
                    continue;

                if (!candidates.ContainsKey(normalised))
                    candidates[normalised] = (candidate, 0);

                candidates[normalised] = (candidates[normalised].DisplayName, candidates[normalised].Count + 1);
            }

            if (candidates.Count == 0)
                return result;

            var best = candidates.OrderByDescending(candidate => candidate.Value.Count).ThenBy(candidate => candidate.Value.DisplayName).First();
            result.SuggestedSeriesName = best.Value.DisplayName;
            result.MatchingFileCount = best.Value.Count;

            if (loadedFiles.Count == 1)
                result.IsConfident = true;
            else
                result.IsConfident = ((decimal)result.MatchingFileCount / loadedFiles.Count) >= 0.60m;

            return result;
        }

        private string ExtractSeriesSearchCandidate(string filePath)
        {
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            Match episodeMatch = Regex.Match(nameWithoutExtension, @"S\d{1,2}E\d{1,2}|\b\d{1,2}x\d{1,2}\b", RegexOptions.IgnoreCase);

            if (!episodeMatch.Success)
                return "";

            string candidate = nameWithoutExtension.Substring(0, episodeMatch.Index);
            candidate = Regex.Replace(candidate, @"[\._]+", " ");
            candidate = Regex.Replace(candidate, @"\s+-\s*$", " ");
            candidate = Regex.Replace(candidate, @"\s+", " ").Trim();
            candidate = candidate.Trim('-', ' ', '.');

            return candidate.Length < 3 ? "" : candidate;
        }

        private async Task AutoDetectAndSearchFromFilesAsync()
        {
            SeriesDetectionResult detection = DetectSeriesFromLoadedFiles();

            if (string.IsNullOrWhiteSpace(detection.SuggestedSeriesName))
            {
                lblDetectedSeries.Text = "Detected series: Not detected from loaded files";
                lblDetectedSeries.ForeColor = Color.DarkOrange;
                UpdateActionButtons();
                return;
            }

            lblDetectedSeries.Text = detection.IsConfident
                ? $"Detected series: {detection.SuggestedSeriesName} ({detection.MatchingFileCount}/{detection.TotalFileCount} files)"
                : $"Detected series: {detection.SuggestedSeriesName} ({detection.MatchingFileCount}/{detection.TotalFileCount} files, review suggested match)";
            lblDetectedSeries.ForeColor = detection.IsConfident ? Color.Green : Color.DarkOrange;

            suppressSeriesNameTextChanged = true;
            txtSeriesName.Text = detection.SuggestedSeriesName;
            suppressSeriesNameTextChanged = false;

            selectedSeriesId = 0;
            selectedSeriesName = "";
            episodeTitles.Clear();
            seriesResults.Clear();
            lstSeriesResults.Items.Clear();
            lblSelectedSeries.Text = "No series selected";
            lblSelectedSeries.ForeColor = Color.DimGray;
            ClearPreview();

            if (!detection.IsConfident)
            {
                UpdateActionButtons();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtApiKey.Text) && string.IsNullOrWhiteSpace(apiKey))
            {
                UpdateActionButtons();
                return;
            }

            bool authenticated = await AuthenticateTVDB();
            if (!authenticated)
            {
                UpdateActionButtons();
                return;
            }

            await SearchSeries(detection.SuggestedSeriesName);
            AutoSelectBestSeriesResult(detection.SuggestedSeriesName);
            UpdateActionButtons();
        }

        private void AutoSelectBestSeriesResult(string detectedSeriesName)
        {
            if (lstSeriesResults.Items.Count == 0)
                return;

            SeriesSearchResult? bestResult = null;
            int bestScore = 0;

            foreach (SeriesSearchResult result in lstSeriesResults.Items.OfType<SeriesSearchResult>())
            {
                int score = ScoreSeriesCandidate(detectedSeriesName, result.Name);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestResult = result;
                }
            }

            if (bestResult != null && bestScore >= 80)
                lstSeriesResults.SelectedItem = bestResult;
        }

        private int ScoreSeriesCandidate(string detectedSeriesName, string tvdbSeriesName)
        {
            string detected = NormaliseSeriesName(detectedSeriesName);
            string tvdb = NormaliseSeriesName(tvdbSeriesName);

            if (string.IsNullOrWhiteSpace(detected) || string.IsNullOrWhiteSpace(tvdb))
                return 0;

            if (string.Equals(detected, tvdb, StringComparison.OrdinalIgnoreCase))
                return 100;

            if (detected.Contains(tvdb) || tvdb.Contains(detected))
                return 90;

            string[] detectedWords = detected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string[] tvdbWords = tvdb.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int matchingWords = detectedWords.Count(word => tvdbWords.Contains(word));

            if (matchingWords == 0)
                return 0;

            return (int)Math.Round((decimal)matchingWords / Math.Max(detectedWords.Length, tvdbWords.Length) * 80m);
        }

        private void btnAddFiles_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select video files";
                dialog.Filter = GetSupportedExtensionsFilter();
                dialog.Multiselect = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                    AddPathsToWorkList(dialog.FileNames, selectAddedItems: true);
            }
        }

        private void btnAddFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select folder containing video files";
                if (dialog.ShowDialog() == DialogResult.OK)
                    AddPathsToWorkList(new[] { dialog.SelectedPath }, selectAddedItems: false);
            }
        }

        private void btnRemoveSelected_Click(object sender, EventArgs e)
        {
            foreach (FileQueueItem item in GetSelectedFileQueueItems())
            {
                loadedFiles.Remove(item);
                lstOriginalFiles.Items.Remove(item);
            }

            ClearPreview();
            UpdateActionButtons();
            UpdateFileStatus();
        }

        private void btnClearFiles_Click(object sender, EventArgs e)
        {
            loadedFiles.Clear();
            lstOriginalFiles.Items.Clear();
            lblDetectedSeries.Text = "Detected series: None";
            lblDetectedSeries.ForeColor = Color.DimGray;
            ClearPreview();
            UpdateActionButtons();
            UpdateFileStatus();
        }

        private void lstOriginalFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressFileSelectionChanged)
                return;

            ClearPreview();
            PopulateSelectedOriginalFilesPreview();
            UpdateActionButtons();
            UpdateFileStatus();
        }

        private void btnChooseOutputFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select output folder for renamed files";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtOutputFolder.Text = dialog.SelectedPath;
                    SaveSettings();
                    ClearPreview();

                    if (!string.IsNullOrWhiteSpace(selectedSeriesName) &&
                        selectedSeriesId != 0 &&
                        lstOriginalFiles.SelectedItems.Count > 0)
                    {
                        PopulateSelectedOriginalFilesPreview();
                    }

                    UpdateActionButtons();
                    UpdateFileStatus();
                }
            }
        }

        private async void btnFetchSeries_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSeriesName.Text))
            {
                MessageBox.Show("Please enter a series name first.");
                return;
            }

            bool authenticated = await AuthenticateTVDB();
            if (!authenticated)
                return;

            await SearchSeries(txtSeriesName.Text);
            UpdateActionButtons();
        }

        private async void lstSeriesResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstSeriesResults.SelectedItem is SeriesSearchResult selected)
            {
                selectedSeriesId = selected.Id;
                selectedSeriesName = selected.Name;
                lblSelectedSeries.Text = $"Selected: {selectedSeriesName} [ID {selectedSeriesId}]";
                lblSelectedSeries.ForeColor = Color.Green;

                bool loaded = await FetchEpisodesForSelectedSeries();
                if (!loaded)
                {
                    lblSelectedSeries.Text = $"Selected: {selectedSeriesName} [ID {selectedSeriesId}] - no episode data loaded";
                    lblSelectedSeries.ForeColor = Color.DarkOrange;
                }

                ClearPreview();
                int selectedFileCount = SelectFilesMatchingSeriesName(selectedSeriesName);
                if (selectedFileCount > 0)
                {
                    lblDetectedSeries.Text = $"Detected series: {selectedSeriesName} ({selectedFileCount}/{loadedFiles.Count} files selected)";
                    lblDetectedSeries.ForeColor = Color.Green;
                }

                UpdateActionButtons();
                UpdateFileStatus();
            }
        }

        private void txtSeriesName_TextChanged(object sender, EventArgs e)
        {
            if (suppressSeriesNameTextChanged)
            {
                UpdateActionButtons();
                return;
            }

            selectedSeriesId = 0;
            selectedSeriesName = "";
            episodeTitles.Clear();
            seriesResults.Clear();
            lstSeriesResults.Items.Clear();
            lblSelectedSeries.Text = "No series selected";
            lblSelectedSeries.ForeColor = Color.DimGray;
            ClearPreview();
            UpdateActionButtons();
        }

        private void btnMatchFiles_Click(object sender, EventArgs e)
        {
            BuildMatchPreview();
        }

        private void btnRenameSelected_Click(object sender, EventArgs e)
        {
            ApplyRenameAndMove();
        }

        private void btnUndo_Click(object sender, EventArgs e)
        {
            UndoLastOperation();
        }

        private void btnSaveApiKey_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtApiKey.Text))
            {
                MessageBox.Show("Please enter a valid API key.");
                return;
            }

            apiKey = txtApiKey.Text.Trim();
            apiToken = "";
            SaveApiKey();
            MessageBox.Show("API key saved.");
            UpdateActionButtons();
        }

        private void btnToggleApiKey_Click(object sender, EventArgs e)
        {
            isApiKeyVisible = !isApiKeyVisible;
            txtApiKey.UseSystemPasswordChar = !isApiKeyVisible;
            btnToggleApiKey.Text = isApiKeyVisible ? "Hide Key" : "Show Key";
        }

        private void btnApiHelp_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://thetvdb.com/api-information",
                UseShellExecute = true
            });
        }

        private void btnOpenLog_Click(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(settingsDirectory);

                if (!File.Exists(LogFilePath))
                {
                    MessageBox.Show(
                        "The rename log file does not exist yet.\n\nA log file will be created after the first rename or undo action.",
                        "Log File Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    return;
                }

                Process.Start(new ProcessStartInfo { FileName = LogFilePath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open the log file.\n\n{ex.Message}", "Open Log Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cmbNamingFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool isCustom = string.Equals(GetSelectedNamingFormat(), "Custom", StringComparison.OrdinalIgnoreCase);
            txtCustomPattern.Enabled = isCustom;

            if (suppressNamingSettingsEvents)
                return;

            SaveSettings();
            ClearPreview();
            PopulateSelectedOriginalFilesPreview();
            UpdateActionButtons();
            UpdateFileStatus();
        }

        private void txtCustomPattern_TextChanged(object sender, EventArgs e)
        {
            if (suppressNamingSettingsEvents)
                return;

            SaveSettings();
            ClearPreview();
            PopulateSelectedOriginalFilesPreview();
            UpdateActionButtons();
            UpdateFileStatus();
        }

        private void BuildMatchPreview()
        {
            previewItems.Clear();
            lstPreviewOriginal.Items.Clear();
            lstPreviewNew.Items.Clear();
            ClearPreviewGrid();

            if (!Directory.Exists(txtOutputFolder.Text))
            {
                MessageBox.Show("Please choose a valid output folder first.");
                UpdateActionButtons();
                return;
            }

            if (selectedSeriesId == 0 || string.IsNullOrWhiteSpace(selectedSeriesName))
            {
                MessageBox.Show("Please search for and select a series first.");
                UpdateActionButtons();
                return;
            }

            if (episodeTitles.Count == 0)
            {
                MessageBox.Show("No episode data is loaded for the selected series. Please fetch and select the series again.");
                UpdateActionButtons();
                return;
            }

            List<FileQueueItem> selectedItems = GetSelectedFileQueueItems();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more files to match.");
                UpdateActionButtons();
                return;
            }

            foreach (FileQueueItem queueItem in selectedItems)
            {
                RenamePreviewItem previewItem = BuildPreviewItem(queueItem.FilePath);
                previewItems.Add(previewItem);

                string originalFile = Path.GetFileName(queueItem.FilePath);
                string displayStatus = GetPreviewDisplayStatus(previewItem);
                string displayNewFile = GetPreviewDisplayNewFile(previewItem);

                lstPreviewOriginal.Items.Add(originalFile);
                lstPreviewNew.Items.Add(
                    previewItem.Status == "OK"
                        ? $"{displayStatus} | {displayNewFile}"
                        : $"{displayStatus} | {previewItem.Message}"
                );
                AddPreviewGridRow(displayStatus, originalFile, displayNewFile, previewItem.Message);
            }

            UpdatePreviewSummary();
            AutoSizePreviewGridColumns();
            UpdateActionButtons();
            UpdateFileStatus();
        }

        private void AutoSizePreviewGridColumns()
        {
            if (dgvPreview.Rows.Count == 0)
                return;

            dgvPreview.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
            foreach (DataGridViewColumn column in dgvPreview.Columns)
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private RenamePreviewItem BuildPreviewItem(string filePath)
        {
            string originalName = Path.GetFileName(filePath);

            if (!File.Exists(filePath))
                return new RenamePreviewItem { OriginalPath = filePath, Status = "ERROR", Message = "Source file not found" };

            if (!IsSupportedVideoFile(filePath))
                return new RenamePreviewItem { OriginalPath = filePath, Status = "SKIP", Message = "Unsupported file type" };

            if (!chkForceRename.Checked && IsLikelyWrongSeries(originalName, selectedSeriesName))
                return new RenamePreviewItem { OriginalPath = filePath, Status = "WRONG SERIES", Message = $"Selected series: {selectedSeriesName}" };

            if (!TryExtractEpisodeCode(originalName, out int seasonNumber, out int episodeNumber, out string episodeCode))
                return new RenamePreviewItem { OriginalPath = filePath, Status = "NO MATCH", Message = "No SxxExx or 1x01 episode code found" };

            if (!episodeTitles.TryGetValue(episodeCode, out string? episodeTitle) || string.IsNullOrWhiteSpace(episodeTitle))
                return new RenamePreviewItem { OriginalPath = filePath, Status = "NO TVDB TITLE", Message = episodeCode };

            string extension = Path.GetExtension(filePath);
            if (!TryBuildNewFileName(seasonNumber, episodeNumber, episodeCode, episodeTitle, extension, out string newName, out string namingError))
                return new RenamePreviewItem { OriginalPath = filePath, Status = "ERROR", Message = namingError };

            string newPath = Path.Combine(txtOutputFolder.Text, newName);

            if (File.Exists(newPath))
            {
                if (chkForceRename.Checked)
                {
                    return new RenamePreviewItem
                    {
                        OriginalPath = filePath,
                        NewPath = newPath,
                        Status = "OK",
                        Message = "Overwrite enabled (existing file will be replaced)"
                    };
                }

                return new RenamePreviewItem
                {
                    OriginalPath = filePath,
                    NewPath = newPath,
                    Status = "SKIP",
                    Message = "Target already exists"
                };
            }

            return new RenamePreviewItem { OriginalPath = filePath, NewPath = newPath, Status = "OK", Message = "Ready" };
        }

        private async void ApplyRenameAndMove()
        {
            RenameOperationPlan plan = BuildRenameOperationPlan();
            if (plan.ReadyItems.Count == 0)
            {
                MessageBox.Show("There are no matched files ready to rename.", "Nothing to Rename", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateActionButtons();
                return;
            }

            if (plan.BlockingIssues.Count > 0)
            {
                string issueText = string.Join("\n", plan.BlockingIssues.Take(12));
                if (plan.BlockingIssues.Count > 12)
                    issueText += $"\n...and {plan.BlockingIssues.Count - 12} more issue(s).";

                MessageBox.Show(
                    $"Rename pre-flight validation failed. No files were moved.\n\n{issueText}",
                    "Rename Blocked",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                UpdateActionButtons();
                return;
            }

            OverwriteDecision overwriteDecision = ResolveOverwriteDecision(plan.ExistingTargetItems.Count);
            if (overwriteDecision == OverwriteDecision.Cancel)
            {
                MessageBox.Show("Rename operation cancelled before any files were moved.", "Rename Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateActionButtons();
                return;
            }

            List<RenamePreviewItem> executionItems = plan.ReadyItems;
            int skippedCount = 0;
            if (overwriteDecision == OverwriteDecision.NoToAll)
            {
                executionItems = plan.ReadyItems
                    .Where(item => !File.Exists(item.NewPath))
                    .ToList();
                skippedCount = plan.ReadyItems.Count - executionItems.Count;
            }

            if (executionItems.Count == 0)
            {
                MessageBox.Show("All ready items were skipped because target files already exist.", "Rename Skipped", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateActionButtons();
                return;
            }

            int successCount = 0;
            int errorCount = 0;
            int rollbackSuccessCount = 0;
            int rollbackErrorCount = 0;
            string batchId = DateTime.Now.ToString("yyyyMMddHHmmss");
            List<CompletedRenameOperation> completedOperations = new List<CompletedRenameOperation>();
            List<string> logEntries = new List<string>();

            Directory.CreateDirectory(settingsDirectory);
            Directory.CreateDirectory(txtOutputFolder.Text);
            logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | BATCH START | {batchId} | Items: {executionItems.Count} | Existing targets: {plan.ExistingTargetItems.Count} | Overwrite: {overwriteDecision}");

            foreach (RenamePreviewItem item in executionItems)
            {
                bool moved = TryRenameItemWithRollbackTracking(
                    item,
                    overwriteDecision == OverwriteDecision.YesToAll,
                    completedOperations,
                    logEntries,
                    out string errorMessage
                );

                if (moved)
                {
                    successCount++;
                    continue;
                }

                errorCount++;
                logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | BATCH ERROR | {batchId} | Stopping batch after failure | {errorMessage}");

                if (completedOperations.Count > 0)
                {
                    DialogResult rollbackPrompt = MessageBox.Show(
                        "A rename operation failed after some files were already moved.\n\nDo you want to rollback the successful moves from this batch?",
                        "Rollback Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );

                    if (rollbackPrompt == DialogResult.Yes)
                    {
                        RollbackCompletedOperations(completedOperations, logEntries, out rollbackSuccessCount, out rollbackErrorCount);
                        successCount = Math.Max(0, successCount - rollbackSuccessCount);
                    }
                }
                break;
            }

            if (rollbackSuccessCount == 0 && completedOperations.Count > 0)
            {
                DeleteRollbackBackups(completedOperations, logEntries);
                List<RenamePreviewItem> successfulRenames = completedOperations
                    .Select(operation => new RenamePreviewItem
                    {
                        OriginalPath = operation.OriginalPath,
                        NewPath = operation.NewPath,
                        Status = "RENAMED",
                        Message = "Moved and renamed"
                    })
                    .ToList();

                undoStack.Push(successfulRenames);
                RemoveSuccessfulFilesFromWorkList(successfulRenames);
            }

            logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | BATCH END | {batchId} | Success: {successCount} | Skipped: {skippedCount} | Errors: {errorCount} | RolledBack: {rollbackSuccessCount} | RollbackErrors: {rollbackErrorCount}");
            File.AppendAllLines(LogFilePath, logEntries);

            ClearPreview();
            MessageBox.Show(
                $"Rename and move completed.\n\nMoved/Renamed: {successCount}\nSkipped: {skippedCount}\nErrors: {errorCount}\nRolled back: {rollbackSuccessCount}\nRollback errors: {rollbackErrorCount}\n\nLog file:\n{LogFilePath}",
                "Rename Result",
                MessageBoxButtons.OK,
                errorCount > 0 || rollbackErrorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information
            );

            if (loadedFiles.Count > 0)
                await AutoDetectAndSearchFromFilesAsync();

            UpdateActionButtons();
            UpdateFileStatus();
        }

        private RenameOperationPlan BuildRenameOperationPlan()
        {
            RenameOperationPlan plan = new RenameOperationPlan
            {
                ReadyItems = previewItems.Where(item => item.Status == "OK").ToList()
            };

            if (!Directory.Exists(txtOutputFolder.Text))
                plan.BlockingIssues.Add("Output folder does not exist.");

            foreach (RenamePreviewItem item in plan.ReadyItems)
            {
                string originalFile = string.IsNullOrWhiteSpace(item.OriginalPath) ? "<blank source>" : Path.GetFileName(item.OriginalPath);

                if (string.IsNullOrWhiteSpace(item.OriginalPath))
                    plan.BlockingIssues.Add("A ready item has a blank source path.");
                else if (!File.Exists(item.OriginalPath))
                    plan.BlockingIssues.Add($"Source file not found: {originalFile}");

                if (string.IsNullOrWhiteSpace(item.NewPath))
                {
                    plan.BlockingIssues.Add($"Target path is blank for: {originalFile}");
                    continue;
                }

                string? targetDirectory = Path.GetDirectoryName(item.NewPath);
                if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
                    plan.BlockingIssues.Add($"Target folder does not exist for: {originalFile}");

                if (!string.IsNullOrWhiteSpace(item.OriginalPath) &&
                    string.Equals(item.OriginalPath, item.NewPath, StringComparison.OrdinalIgnoreCase))
                {
                    plan.BlockingIssues.Add($"Source and target are the same: {originalFile}");
                }

                if (File.Exists(item.NewPath))
                    plan.ExistingTargetItems.Add(item);
            }

            var duplicateTargets = plan.ReadyItems
                .Where(item => !string.IsNullOrWhiteSpace(item.NewPath))
                .GroupBy(item => item.NewPath, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .ToList();

            foreach (var duplicateTarget in duplicateTargets)
            {
                plan.BlockingIssues.Add($"Duplicate target in batch: {Path.GetFileName(duplicateTarget.Key)} ({duplicateTarget.Count()} items)");
            }

            return plan;
        }

        private OverwriteDecision ResolveOverwriteDecision(int existingTargetCount)
        {
            if (existingTargetCount == 0)
                return OverwriteDecision.YesToAll;

            DialogResult result = MessageBox.Show(
                $"{existingTargetCount} target file(s) already exist.\n\nYes = overwrite all existing targets\nNo = skip all existing targets\nCancel = stop before making changes",
                "Overwrite Confirmation",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
                return OverwriteDecision.YesToAll;
            if (result == DialogResult.No)
                return OverwriteDecision.NoToAll;
            return OverwriteDecision.Cancel;
        }

        private bool TryRenameItemWithRollbackTracking(
            RenamePreviewItem item,
            bool overwriteExisting,
            List<CompletedRenameOperation> completedOperations,
            List<string> logEntries,
            out string errorMessage)
        {
            errorMessage = "";
            string backupPath = "";

            try
            {
                if (File.Exists(item.NewPath))
                {
                    if (!overwriteExisting)
                    {
                        errorMessage = "Target exists and overwrite was not approved.";
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | SKIP | Target exists | {item.NewPath}");
                        return false;
                    }

                    backupPath = BuildRollbackBackupPath(item.NewPath);
                    File.Move(item.NewPath, backupPath);
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | BACKUP | Existing target moved to rollback backup | {item.NewPath} -> {backupPath}");
                }

                File.Move(item.OriginalPath, item.NewPath);
                completedOperations.Add(new CompletedRenameOperation
                {
                    OriginalPath = item.OriginalPath,
                    NewPath = item.NewPath,
                    BackupPath = backupPath
                });
                logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | RENAMED | {item.OriginalPath} -> {item.NewPath}");
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ERROR | {item.OriginalPath} -> {item.NewPath} | {ex.Message}");

                if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath) && !File.Exists(item.NewPath))
                {
                    try
                    {
                        File.Move(backupPath, item.NewPath);
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | RESTORE BACKUP | {backupPath} -> {item.NewPath}");
                    }
                    catch (Exception restoreEx)
                    {
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | RESTORE BACKUP ERROR | {backupPath} -> {item.NewPath} | {restoreEx.Message}");
                    }
                }
                return false;
            }
        }

        private string BuildRollbackBackupPath(string targetPath)
        {
            string directory = Path.GetDirectoryName(targetPath) ?? txtOutputFolder.Text;
            string fileName = Path.GetFileName(targetPath);
            string backupPath;
            int attempt = 0;

            do
            {
                attempt++;
                backupPath = Path.Combine(directory, $".{fileName}.rollback-{DateTime.Now:yyyyMMddHHmmss}-{attempt}.bak");
            }
            while (File.Exists(backupPath));

            return backupPath;
        }

        private void RollbackCompletedOperations(
            List<CompletedRenameOperation> completedOperations,
            List<string> logEntries,
            out int rollbackSuccessCount,
            out int rollbackErrorCount)
        {
            rollbackSuccessCount = 0;
            rollbackErrorCount = 0;
            logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ROLLBACK START | Items: {completedOperations.Count}");

            foreach (CompletedRenameOperation operation in completedOperations.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(operation.NewPath) && !File.Exists(operation.OriginalPath))
                    {
                        File.Move(operation.NewPath, operation.OriginalPath);
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ROLLBACK MOVE | {operation.NewPath} -> {operation.OriginalPath}");
                    }

                    if (!string.IsNullOrWhiteSpace(operation.BackupPath) && File.Exists(operation.BackupPath) && !File.Exists(operation.NewPath))
                    {
                        File.Move(operation.BackupPath, operation.NewPath);
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ROLLBACK RESTORE TARGET | {operation.BackupPath} -> {operation.NewPath}");
                    }

                    rollbackSuccessCount++;
                }
                catch (Exception ex)
                {
                    rollbackErrorCount++;
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ROLLBACK ERROR | {operation.NewPath} -> {operation.OriginalPath} | {ex.Message}");
                }
            }

            logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ROLLBACK END | Restored: {rollbackSuccessCount} | Errors: {rollbackErrorCount}");
        }

        private void DeleteRollbackBackups(List<CompletedRenameOperation> completedOperations, List<string> logEntries)
        {
            foreach (CompletedRenameOperation operation in completedOperations)
            {
                if (string.IsNullOrWhiteSpace(operation.BackupPath) || !File.Exists(operation.BackupPath))
                    continue;

                try
                {
                    File.Delete(operation.BackupPath);
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | BACKUP DELETE | {operation.BackupPath}");
                }
                catch (Exception ex)
                {
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | BACKUP DELETE ERROR | {operation.BackupPath} | {ex.Message}");
                }
            }
        }

        private void RemoveSuccessfulFilesFromWorkList(List<RenamePreviewItem> successfulRenames)
        {
            foreach (RenamePreviewItem rename in successfulRenames)
            {
                FileQueueItem? queueItem = loadedFiles.FirstOrDefault(item => string.Equals(item.FilePath, rename.OriginalPath, StringComparison.OrdinalIgnoreCase));
                if (queueItem != null)
                {
                    loadedFiles.Remove(queueItem);
                    lstOriginalFiles.Items.Remove(queueItem);
                }
            }
        }

        private void UndoLastOperation()
        {
            if (undoStack.Count == 0)
            {
                MessageBox.Show("Nothing to undo.");
                UpdateActionButtons();
                return;
            }

            List<RenamePreviewItem> lastOperation = undoStack.Pop();
            int successCount = 0;
            int skippedCount = 0;
            int errorCount = 0;
            List<string> logEntries = new List<string>();
            Directory.CreateDirectory(settingsDirectory);

            foreach (RenamePreviewItem item in lastOperation)
            {
                try
                {
                    if (!File.Exists(item.NewPath))
                    {
                        skippedCount++;
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | UNDO SKIPPED | Source not found | {item.NewPath}");
                        continue;
                    }

                    if (File.Exists(item.OriginalPath))
                    {
                        skippedCount++;
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | UNDO SKIPPED | Target already exists | {item.OriginalPath}");
                        continue;
                    }

                    File.Move(item.NewPath, item.OriginalPath);
                    successCount++;
                    AddPathsToWorkList(new[] { item.OriginalPath }, selectAddedItems: false);
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | UNDO | {item.NewPath} -> {item.OriginalPath}");
                }
                catch (Exception ex)
                {
                    errorCount++;
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | UNDO ERROR | {item.NewPath} -> {item.OriginalPath} | {ex.Message}");
                }
            }

            if (logEntries.Count > 0)
                File.AppendAllLines(LogFilePath, logEntries);

            ClearPreview();

            MessageBox.Show(
                $"Undo completed.\n\nRestored: {successCount}\nSkipped: {skippedCount}\nErrors: {errorCount}\n\nLog file:\n{LogFilePath}",
                "Undo Result",
                MessageBoxButtons.OK,
                errorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information
            );

            UpdateActionButtons();
            UpdateFileStatus();
        }


        private bool IsLikelyWrongSeries(string fileName, string selectedSeriesName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(selectedSeriesName))
                return false;

            string candidateSeriesName = ExtractSeriesCandidateFromFileName(fileName);
            if (string.IsNullOrWhiteSpace(candidateSeriesName))
                return false;

            string normalisedCandidate = NormaliseSeriesName(candidateSeriesName);
            string normalisedSelected = NormaliseSeriesName(selectedSeriesName);

            if (string.IsNullOrWhiteSpace(normalisedCandidate) || string.IsNullOrWhiteSpace(normalisedSelected))
                return false;

            return !(normalisedCandidate.Contains(normalisedSelected) || normalisedSelected.Contains(normalisedCandidate));
        }

        private string ExtractSeriesCandidateFromFileName(string fileName)
        {
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            Match episodeMatch = Regex.Match(nameWithoutExtension, @"S\d{1,2}E\d{1,2}|\b\d{1,2}x\d{1,2}\b", RegexOptions.IgnoreCase);
            if (!episodeMatch.Success)
                return "";

            string candidate = nameWithoutExtension.Substring(0, episodeMatch.Index);
            candidate = Regex.Replace(candidate, @"[\._\-]+", " ");
            candidate = Regex.Replace(candidate, @"\b(19|20)\d{2}\b", " ");
            candidate = Regex.Replace(candidate, @"\s+", " ").Trim();

            return candidate.Length < 3 ? "" : candidate;
        }

        private string NormaliseSeriesName(string value)
        {
            string normalised = value.ToLowerInvariant();
            normalised = Regex.Replace(normalised, @"\b(19|20)\d{2}\b", " ");
            normalised = Regex.Replace(normalised, @"[^a-z0-9]+", " ");
            normalised = Regex.Replace(normalised, @"\s+", " ").Trim();
            return normalised;
        }

        private bool TryExtractEpisodeCode(string fileName, out int seasonNumber, out int episodeNumber, out string episodeCode)
        {
            seasonNumber = 0;
            episodeNumber = 0;
            episodeCode = "";

            Match match = Regex.Match(fileName, @"S(\d+)E(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
                match = Regex.Match(fileName, @"\b(\d{1,2})x(\d{1,2})\b", RegexOptions.IgnoreCase);

            if (!match.Success)
                return false;

            seasonNumber = int.Parse(match.Groups[1].Value);
            episodeNumber = int.Parse(match.Groups[2].Value);
            episodeCode = $"S{seasonNumber:D2}E{episodeNumber:D2}";
            return true;
        }

        private string GetSelectedNamingFormat()
        {
            return cmbNamingFormat.SelectedItem?.ToString() ?? "S01E01";
        }

        private bool TryBuildNewFileName(int seasonNumber, int episodeNumber, string episodeCode, string episodeTitle, string extension, out string newName, out string errorMessage)
        {
            newName = "";
            errorMessage = "";

            string selectedFormat = GetSelectedNamingFormat();
            string safeSeriesName = MakeSafeFileName(selectedSeriesName);
            string safeEpisodeTitle = MakeSafeFileName(episodeTitle);
            string episodeTag;

            if (string.Equals(selectedFormat, "1x01", StringComparison.OrdinalIgnoreCase))
            {
                episodeTag = $"{seasonNumber}x{episodeNumber:D2}";
                newName = $"{safeSeriesName} - {episodeTag} - {safeEpisodeTitle}{extension}";
                return true;
            }

            if (string.Equals(selectedFormat, "Custom", StringComparison.OrdinalIgnoreCase))
            {
                string pattern = txtCustomPattern.Text.Trim();
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    errorMessage = "Custom naming pattern is blank";
                    return false;
                }

                string customName = pattern
                    .Replace("{series}", safeSeriesName)
                    .Replace("{season}", seasonNumber.ToString())
                    .Replace("{season00}", seasonNumber.ToString("D2"))
                    .Replace("{episode}", episodeNumber.ToString())
                    .Replace("{episode00}", episodeNumber.ToString("D2"))
                    .Replace("{code}", episodeCode)
                    .Replace("{title}", safeEpisodeTitle);

                customName = MakeSafeFileName(customName);
                if (string.IsNullOrWhiteSpace(customName))
                {
                    errorMessage = "Custom naming pattern produced a blank file name";
                    return false;
                }

                newName = $"{customName}{extension}";
                return true;
            }

            episodeTag = episodeCode;
            newName = $"{safeSeriesName} - {episodeTag} - {safeEpisodeTitle}{extension}";
            return true;
        }

        private string MakeSafeFileName(string value)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidChar, '-');
            return value.Trim();
        }

        private void SaveApiKey()
        {
            SaveSettings();
        }

        private void SaveSettings()
        {
            Directory.CreateDirectory(settingsDirectory);
            apiKey = txtApiKey.Text.Trim();
            AppSettings settings = new AppSettings
            {
                ApiKey = apiKey,
                NamingFormat = GetSelectedNamingFormat(),
                CustomNamingPattern = txtCustomPattern.Text.Trim(),
                LastOutputFolder = txtOutputFolder.Text.Trim()
            };
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }

        private void LoadApiKey()
        {
            if (!File.Exists(SettingsFilePath))
            {
                ApplyNamingSettings("S01E01", "{series} - {code} - {title}");
                return;
            }

            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings == null)
                {
                    ApplyNamingSettings("S01E01", "{series} - {code} - {title}");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    apiKey = settings.ApiKey;
                    txtApiKey.Text = apiKey;
                }

                ApplyNamingSettings(settings.NamingFormat, settings.CustomNamingPattern);
                ApplyOutputFolderSetting(settings.LastOutputFolder);
            }
            catch
            {
                ApplyNamingSettings("S01E01", "{series} - {code} - {title}");
                MessageBox.Show("Could not load saved application settings.");
            }
        }

        private void ApplyNamingSettings(string namingFormat, string customNamingPattern)
        {
            suppressNamingSettingsEvents = true;

            string format = string.IsNullOrWhiteSpace(namingFormat) ? "S01E01" : namingFormat.Trim();
            if (!cmbNamingFormat.Items.Contains(format))
                format = "S01E01";

            cmbNamingFormat.SelectedItem = format;
            txtCustomPattern.Text = string.IsNullOrWhiteSpace(customNamingPattern)
                ? "{series} - {code} - {title}"
                : customNamingPattern.Trim();
            txtCustomPattern.Enabled = string.Equals(format, "Custom", StringComparison.OrdinalIgnoreCase);

            suppressNamingSettingsEvents = false;
        }

        private void ApplyOutputFolderSetting(string lastOutputFolder)
        {
            if (string.IsNullOrWhiteSpace(lastOutputFolder))
                return;

            if (Directory.Exists(lastOutputFolder))
            {
                txtOutputFolder.Text = lastOutputFolder;
            }
        }

        private string GetJsonString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement property) ? property.GetString() ?? "" : "";
        }

        private int GetJsonInt(JsonElement element, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (element.TryGetProperty(propertyName, out JsonElement property))
                {
                    if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int numberValue))
                        return numberValue;

                    if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out int stringValue))
                        return stringValue;
                }
            }
            return 0;
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(GitHubUserAgent);
                    HttpResponseMessage response = await client.GetAsync(LatestReleaseApiUrl);
                    if (!response.IsSuccessStatusCode)
                        return;

                    string responseString = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(responseString))
                    {
                        string latestVersion = GetJsonString(doc.RootElement, "tag_name");
                        string releaseUrl = GetJsonString(doc.RootElement, "html_url");

                        if (string.IsNullOrWhiteSpace(latestVersion) || string.IsNullOrWhiteSpace(releaseUrl))
                            return;

                        if (!IsNewerVersion(latestVersion, CurrentVersion))
                            return;

                        DialogResult result = MessageBox.Show(
                            $"A newer version of TV Series Renamer is available.\n\nCurrent version: {CurrentVersion}\nLatest version: {latestVersion}\n\nDo you want to open the download page?",
                            "Update Available",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information
                        );

                        if (result == DialogResult.Yes)
                            Process.Start(new ProcessStartInfo { FileName = releaseUrl, UseShellExecute = true });
                    }
                }
            }
            catch
            {
                // Silent failure by design. Update checks must never block the app.
            }
        }

        private bool IsNewerVersion(string latestVersionText, string currentVersionText)
        {
            Version? latestVersion = ParseVersionTag(latestVersionText);
            Version? currentVersion = ParseVersionTag(currentVersionText);
            if (latestVersion == null || currentVersion == null)
                return false;
            return latestVersion.CompareTo(currentVersion) > 0;
        }

        private Version? ParseVersionTag(string versionText)
        {
            if (string.IsNullOrWhiteSpace(versionText))
                return null;

            string cleanedVersion = versionText.Trim();
            if (cleanedVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                cleanedVersion = cleanedVersion.Substring(1);

            Match match = Regex.Match(cleanedVersion, @"^\d+(\.\d+){0,3}");
            if (!match.Success)
                return null;

            return Version.TryParse(match.Value, out Version? parsedVersion) ? parsedVersion : null;
        }

        private async Task<bool> AuthenticateTVDB()
        {
            if (!string.IsNullOrWhiteSpace(txtApiKey.Text))
                apiKey = txtApiKey.Text.Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("Please enter and save your API key first.");
                return false;
            }

            using (HttpClient client = new HttpClient())
            {
                var requestBody = new { apikey = apiKey };
                string json = JsonSerializer.Serialize(requestBody);
                HttpResponseMessage response = await client.PostAsync(
                    "https://api4.thetvdb.com/v4/login",
                    new StringContent(json, Encoding.UTF8, "application/json")
                );

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Failed to authenticate with TVDB. Please check your API key.");
                    return false;
                }

                string responseString = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(responseString))
                {
                    apiToken = doc.RootElement.GetProperty("data").GetProperty("token").GetString() ?? "";
                }

                return !string.IsNullOrWhiteSpace(apiToken);
            }
        }

        private async Task SearchSeries(string seriesName)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
                string encodedSeriesName = Uri.EscapeDataString(seriesName);
                string url = $"https://api4.thetvdb.com/v4/search?query={encodedSeriesName}";
                HttpResponseMessage response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Series search failed.");
                    return;
                }

                string responseString = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(responseString))
                {
                    if (!doc.RootElement.TryGetProperty("data", out JsonElement results))
                    {
                        MessageBox.Show("No search results returned.");
                        return;
                    }

                    seriesResults.Clear();
                    lstSeriesResults.Items.Clear();
                    selectedSeriesId = 0;
                    selectedSeriesName = "";
                    episodeTitles.Clear();
                    lblSelectedSeries.Text = "No series selected";
                    lblSelectedSeries.ForeColor = Color.DimGray;

                    foreach (JsonElement item in results.EnumerateArray())
                    {
                        string name = GetJsonString(item, "name");
                        string type = GetJsonString(item, "type");
                        int id = GetJsonInt(item, "tvdb_id", "id");

                        if (string.IsNullOrWhiteSpace(name) || id == 0)
                            continue;

                        SeriesSearchResult result = new SeriesSearchResult { Id = id, Name = name, Type = type };
                        seriesResults.Add(result);
                        lstSeriesResults.Items.Add(result);
                    }

                    if (lstSeriesResults.Items.Count == 0)
                        MessageBox.Show("No usable series results found.");
                }
            }
        }

        private async Task<bool> FetchEpisodesForSelectedSeries()
        {
            if (selectedSeriesId == 0)
            {
                MessageBox.Show("Please select a series first.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(apiToken))
            {
                bool authenticated = await AuthenticateTVDB();
                if (!authenticated)
                    return false;
            }

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
                string url = $"https://api4.thetvdb.com/v4/series/{selectedSeriesId}/episodes/default";
                HttpResponseMessage response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Failed to fetch episode data from TVDB.");
                    return false;
                }

                string responseString = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(responseString))
                {
                    if (!doc.RootElement.TryGetProperty("data", out JsonElement data))
                    {
                        MessageBox.Show("No episode data returned.");
                        return false;
                    }

                    JsonElement episodes;
                    if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("episodes", out JsonElement episodeArray))
                        episodes = episodeArray;
                    else if (data.ValueKind == JsonValueKind.Array)
                        episodes = data;
                    else
                    {
                        MessageBox.Show("Episode data format was not recognised.");
                        return false;
                    }

                    episodeTitles.Clear();
                    foreach (JsonElement episode in episodes.EnumerateArray())
                    {
                        int season = GetJsonInt(episode, "seasonNumber", "airedSeason", "season");
                        int number = GetJsonInt(episode, "number", "airedEpisodeNumber", "episodeNumber");
                        string title = GetJsonString(episode, "name");

                        if (season == 0 || number == 0 || string.IsNullOrWhiteSpace(title))
                            continue;

                        string episodeCode = $"S{season:D2}E{number:D2}";
                        if (!episodeTitles.ContainsKey(episodeCode))
                            episodeTitles.Add(episodeCode, title);
                    }
                }
            }

            MessageBox.Show($"Loaded {episodeTitles.Count} episode titles for {selectedSeriesName}.");
            return episodeTitles.Count > 0;
        }
    }
}
