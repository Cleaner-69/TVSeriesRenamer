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
        private readonly ToolTip toolTip = new ToolTip();

        private const string CurrentVersion = "v1.3";
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

            Text = $"TV Series Renamer {CurrentVersion}";
            lblVersion.Text = "Version 1.3";

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
            toolTip.SetToolTip(lblDetectedSeries, "Detected series suggestion based on loaded file names");
            toolTip.SetToolTip(chkForceRename, "Override wrong-series safety checks for the current match operation");
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
            Match episodeMatch = Regex.Match(nameWithoutExtension, @"S\d{1,2}E\d{1,2}", RegexOptions.IgnoreCase);

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
            ClearPreview();
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
                    ClearPreview();
                    UpdateActionButtons();
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
                UpdateActionButtons();
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

        private void BuildMatchPreview()
        {
            previewItems.Clear();
            lstPreviewOriginal.Items.Clear();
            lstPreviewNew.Items.Clear();

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
                lstPreviewOriginal.Items.Add(Path.GetFileName(queueItem.FilePath));
                lstPreviewNew.Items.Add(previewItem.Status == "OK" ? Path.GetFileName(previewItem.NewPath) : $"{previewItem.Status}: {previewItem.Message}");
            }

            UpdateActionButtons();
            UpdateFileStatus();
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
                return new RenamePreviewItem { OriginalPath = filePath, Status = "NO MATCH", Message = "No SxxExx episode code found" };

            if (!episodeTitles.TryGetValue(episodeCode, out string episodeTitle))
                return new RenamePreviewItem { OriginalPath = filePath, Status = "NO TVDB TITLE", Message = episodeCode };

            string safeSeriesName = MakeSafeFileName(selectedSeriesName);
            string safeEpisodeTitle = MakeSafeFileName(episodeTitle);
            string extension = Path.GetExtension(filePath);
            string newName = $"{safeSeriesName} - {episodeCode} - {safeEpisodeTitle}{extension}";
            string newPath = Path.Combine(txtOutputFolder.Text, newName);

            if (File.Exists(newPath))
                return new RenamePreviewItem { OriginalPath = filePath, NewPath = newPath, Status = "SKIP", Message = "Target already exists" };

            return new RenamePreviewItem { OriginalPath = filePath, NewPath = newPath, Status = "OK", Message = "Ready" };
        }

        private void ApplyRenameAndMove()
        {
            List<RenamePreviewItem> readyItems = previewItems.Where(item => item.Status == "OK").ToList();

            if (readyItems.Count == 0)
            {
                MessageBox.Show("There are no matched files ready to rename.");
                UpdateActionButtons();
                return;
            }

            int successCount = 0;
            int skippedCount = 0;
            int errorCount = 0;
            List<RenamePreviewItem> successfulRenames = new List<RenamePreviewItem>();
            List<string> logEntries = new List<string>();

            Directory.CreateDirectory(settingsDirectory);
            Directory.CreateDirectory(txtOutputFolder.Text);

            foreach (RenamePreviewItem item in readyItems)
            {
                try
                {
                    if (!File.Exists(item.OriginalPath))
                    {
                        skippedCount++;
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | SKIP | Source not found | {item.OriginalPath}");
                        continue;
                    }

                    if (File.Exists(item.NewPath))
                    {
                        skippedCount++;
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | SKIP | Target already exists | {item.NewPath}");
                        continue;
                    }

                    File.Move(item.OriginalPath, item.NewPath);
                    successCount++;
                    successfulRenames.Add(new RenamePreviewItem { OriginalPath = item.OriginalPath, NewPath = item.NewPath, Status = "RENAMED", Message = "Moved and renamed" });
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | RENAMED | {item.OriginalPath} -> {item.NewPath}");
                }
                catch (Exception ex)
                {
                    errorCount++;
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ERROR | {item.OriginalPath} -> {item.NewPath} | {ex.Message}");
                }
            }

            if (logEntries.Count > 0)
                File.AppendAllLines(LogFilePath, logEntries);

            if (successfulRenames.Count > 0)
                undoStack.Push(successfulRenames);

            RemoveSuccessfulFilesFromWorkList(successfulRenames);
            ClearPreview();

            MessageBox.Show(
                $"Rename and move completed.\n\nMoved/Renamed: {successCount}\nSkipped: {skippedCount}\nErrors: {errorCount}\n\nLog file:\n{LogFilePath}",
                "Rename Result",
                MessageBoxButtons.OK,
                errorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information
            );

            UpdateActionButtons();
            UpdateFileStatus();
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
            Match episodeMatch = Regex.Match(nameWithoutExtension, @"S\d{1,2}E\d{1,2}", RegexOptions.IgnoreCase);
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
                return false;

            seasonNumber = int.Parse(match.Groups[1].Value);
            episodeNumber = int.Parse(match.Groups[2].Value);
            episodeCode = $"S{seasonNumber:D2}E{episodeNumber:D2}";
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
            Directory.CreateDirectory(settingsDirectory);
            AppSettings settings = new AppSettings { ApiKey = apiKey };
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }

        private void LoadApiKey()
        {
            if (!File.Exists(SettingsFilePath))
                return;

            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings == null || string.IsNullOrWhiteSpace(settings.ApiKey))
                    return;

                apiKey = settings.ApiKey;
                txtApiKey.Text = apiKey;
            }
            catch
            {
                MessageBox.Show("Could not load saved API key settings.");
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
