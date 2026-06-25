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
        private ToolTip toolTip = new ToolTip();

        // ---- APPLICATION VERSION / UPDATE CHECK ----
        private const string CurrentVersion = "v1.1";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/Cleaner-69/TVSeriesRenamer/releases/latest";
        private const string GitHubUserAgent = "TVSeriesRenamer";

        // ---- SUPPORTED FILE TYPES ----
        // Phase 1 hardening: restrict rename preview and rename execution to common video file types.
        // This reduces noise and lowers the risk of accidentally processing unrelated files.
        private static readonly HashSet<string> SupportedVideoExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mkv",
                ".mp4",
                ".avi",
                ".mov",
                ".wmv",
                ".m4v"
            };

        // ---- TVDB CONFIG ----
        private string apiKey = "";
        private string apiToken = "";

        // ---- API KEY VISIBILITY ----
        private bool isApiKeyVisible = false;

        // ---- LOCAL SETTINGS / LOGGING ----
        private readonly string settingsDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TVSeriesRenamer"
            );

        private string SettingsFilePath =>
            Path.Combine(settingsDirectory, "appsettings.json");

        private string LogFilePath =>
            Path.Combine(settingsDirectory, "rename_log.txt");

        // ---- RENAME PREVIEW STATE ----
        private List<RenamePreviewItem> previewItems = new List<RenamePreviewItem>();

        // ---- UNDO STATE ----
        private Stack<List<RenamePreviewItem>> undoStack = new Stack<List<RenamePreviewItem>>();

        // ---- TVDB SERIES SEARCH STATE ----
        private List<SeriesSearchResult> seriesResults = new List<SeriesSearchResult>();
        private int selectedSeriesId = 0;
        private string selectedSeriesName = "";

        // ---- TVDB EPISODE LOOKUP ----
        // Key example: S01E01
        // Value example: Pilot
        private Dictionary<string, string> episodeTitles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public MainForm()
        {
            InitializeComponent();

            btnRename.Enabled = false;
            btnFetchSeries.Enabled = false;
            btnUndo.Enabled = false;

            txtApiKey.UseSystemPasswordChar = true;
            btnToggleApiKey.Text = "Show Key";

            SetupPreviewGrid();
            LoadApiKey();

            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 500;
            toolTip.ReshowDelay = 200;
            toolTip.ShowAlways = true;

            SetupToolTips();

            Text = $"TV Series Renamer {CurrentVersion}";
            _ = CheckForUpdatesAsync();
        }

        // ---- MODELS ----

        public class RenamePreviewItem
        {
            public string OriginalPath { get; set; } = "";
            public string NewPath { get; set; } = "";
            public string Status { get; set; } = "";
        }

        public class SeriesSearchResult
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";

            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(Type)
                    ? Name
                    : $"{Name} ({Type})";
            }
        }

        public class AppSettings
        {
            public string ApiKey { get; set; } = "";
        }

        // ---- GRID PREVIEW SETUP ----

        private void SetupPreviewGrid()
        {
            dgvPreview.Rows.Clear();
            dgvPreview.Columns.Clear();

            dgvPreview.Columns.Add("Status", "Status");
            dgvPreview.Columns.Add("Original", "Original File");
            dgvPreview.Columns.Add("New", "New File / Reason");

            dgvPreview.ReadOnly = true;
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.RowHeadersVisible = false;

            // Fixed row height to keep preview readable and stable.
            dgvPreview.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgvPreview.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dgvPreview.RowTemplate.Height = 22;

            dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPreview.MultiSelect = false;

            dgvPreview.Columns["Status"].FillWeight = 18;
            dgvPreview.Columns["Original"].FillWeight = 41;
            dgvPreview.Columns["New"].FillWeight = 41;

            foreach (DataGridViewColumn column in dgvPreview.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.Automatic;
            }

            dgvPreview.Columns["Status"].HeaderCell.ToolTipText = "Rename status";
            dgvPreview.Columns["Original"].HeaderCell.ToolTipText = "Original file";
            dgvPreview.Columns["New"].HeaderCell.ToolTipText = "New file or reason";
        }

        private void SetupToolTips()
        {
            toolTip.SetToolTip(btnSelectFolder, "Select folder with video files");
            toolTip.SetToolTip(btnPreview, "Generate preview for supported video files");
            toolTip.SetToolTip(btnRename, "Apply rename");
            toolTip.SetToolTip(btnUndo, "Undo last rename");
            toolTip.SetToolTip(btnFetchSeries, "Fetch series from TVDB");
            toolTip.SetToolTip(btnSaveApiKey, "Save the TVDB API key");
            toolTip.SetToolTip(btnToggleApiKey, "Show or hide the TVDB API key");
            toolTip.SetToolTip(txtFolderPath, "Folder path");
            toolTip.SetToolTip(txtSeriesName, "Series to search");
            toolTip.SetToolTip(txtApiKey, "TVDB API key");
            toolTip.SetToolTip(lstSeriesResults, "Select correct series");
            toolTip.SetToolTip(chkForceRename, "Override safety checks and rename all files in preview");
        }

        private void AddPreviewRow(string status, string originalName, string newNameOrReason)
        {
            int rowIndex = dgvPreview.Rows.Add(status, originalName, newNameOrReason);
            DataGridViewRow row = dgvPreview.Rows[rowIndex];

            switch (status)
            {
                case "OK":
                    row.DefaultCellStyle.BackColor = Color.LightGreen;
                    break;
                case "SKIP":
                    row.DefaultCellStyle.BackColor = Color.LightGray;
                    break;
                case "NO MATCH":
                    row.DefaultCellStyle.BackColor = Color.Khaki;
                    break;
                case "NO TVDB TITLE":
                    row.DefaultCellStyle.BackColor = Color.LightYellow;
                    break;
                case "WRONG SERIES":
                    row.DefaultCellStyle.BackColor = Color.Orange;
                    break;
                case "ERROR":
                    row.DefaultCellStyle.BackColor = Color.LightCoral;
                    break;
            }

            row.Cells["Original"].ToolTipText = originalName;
            row.Cells["New"].ToolTipText = newNameOrReason;
        }

        // ---- STATE MANAGEMENT ----

        private void ResetPreviewState(bool clearEpisodeTitles)
        {
            dgvPreview.Rows.Clear();
            previewItems.Clear();

            btnRename.Enabled = false;
            btnFetchSeries.Enabled = false;

            if (clearEpisodeTitles)
            {
                episodeTitles.Clear();
            }
        }

        private void ResetSeriesSelectionState()
        {
            selectedSeriesId = 0;
            selectedSeriesName = "";
            seriesResults.Clear();
            lstSeriesResults.Items.Clear();
            episodeTitles.Clear();
        }

        // ---- FILE FILTERING ----

        private bool IsSupportedVideoFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return SupportedVideoExtensions.Contains(extension);
        }

        private string[] GetSupportedVideoFilesFromSelectedFolder()
        {
            if (!Directory.Exists(txtFolderPath.Text))
            {
                return Array.Empty<string>();
            }

            return Directory
                .GetFiles(txtFolderPath.Text)
                .Where(IsSupportedVideoFile)
                .ToArray();
        }

        private string GetSupportedExtensionsText()
        {
            return string.Join(", ", SupportedVideoExtensions.OrderBy(extension => extension));
        }

        // ---- UI EVENTS ----

        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtFolderPath.Text = dialog.SelectedPath;

                    ResetPreviewState(clearEpisodeTitles: true);
                    ResetSeriesSelectionState();
                }
            }
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            BuildBasicPreview();
        }

        private void btnRename_Click(object sender, EventArgs e)
        {
            int successCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            List<RenamePreviewItem> successfulRenames = new List<RenamePreviewItem>();
            List<string> logEntries = new List<string>();

            Directory.CreateDirectory(settingsDirectory);

            foreach (RenamePreviewItem item in previewItems)
            {
                try
                {
                    if (!File.Exists(item.OriginalPath))
                    {
                        skippedCount++;
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | SKIP | Source not found | {item.OriginalPath}");
                        continue;
                    }

                    if (!IsSupportedVideoFile(item.OriginalPath))
                    {
                        skippedCount++;
                        logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | SKIP | Unsupported file type | {item.OriginalPath}");
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

                    successfulRenames.Add(new RenamePreviewItem
                    {
                        OriginalPath = item.OriginalPath,
                        NewPath = item.NewPath,
                        Status = "RENAMED"
                    });

                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | RENAMED | {item.OriginalPath} -> {item.NewPath}");
                }
                catch (Exception ex)
                {
                    errorCount++;
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ERROR | {item.OriginalPath} -> {item.NewPath} | {ex.Message}");
                }
            }

            if (logEntries.Count > 0)
            {
                File.AppendAllLines(LogFilePath, logEntries);
            }

            if (successfulRenames.Count > 0)
            {
                undoStack.Push(successfulRenames);
                btnUndo.Enabled = true;
            }

            MessageBox.Show(
                $"Rename completed.\n\n" +
                $"Renamed: {successCount}\n" +
                $"Skipped: {skippedCount}\n" +
                $"Errors: {errorCount}\n\n" +
                $"Log file:\n{LogFilePath}",
                "Rename Result",
                MessageBoxButtons.OK,
                errorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information
            );

            btnRename.Enabled = false;
            RefreshPreviewAfterOperation();
        }

        private void btnUndo_Click(object sender, EventArgs e)
        {
            if (undoStack.Count == 0)
            {
                MessageBox.Show("Nothing to undo.");
                btnUndo.Enabled = false;
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
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | UNDO | {item.NewPath} -> {item.OriginalPath}");
                }
                catch (Exception ex)
                {
                    errorCount++;
                    logEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | UNDO ERROR | {item.NewPath} -> {item.OriginalPath} | {ex.Message}");
                }
            }

            if (logEntries.Count > 0)
            {
                File.AppendAllLines(LogFilePath, logEntries);
            }

            MessageBox.Show(
                $"Undo completed.\n\n" +
                $"Restored: {successCount}\n" +
                $"Skipped: {skippedCount}\n" +
                $"Errors: {errorCount}\n\n" +
                $"Log file:\n{LogFilePath}",
                "Undo Result",
                MessageBoxButtons.OK,
                errorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information
            );

            btnUndo.Enabled = undoStack.Count > 0;
            RefreshPreviewAfterOperation();
        }

        private async void btnFetchSeries_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSeriesName.Text))
            {
                MessageBox.Show("Please enter a series name first.");
                return;
            }

            bool success = await AuthenticateTVDB();

            if (!success)
                return;

            await SearchSeries(txtSeriesName.Text);
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
        }

        private void btnToggleApiKey_Click(object sender, EventArgs e)
        {
            isApiKeyVisible = !isApiKeyVisible;
            txtApiKey.UseSystemPasswordChar = !isApiKeyVisible;
            btnToggleApiKey.Text = isApiKeyVisible ? "Hide Key" : "Show Key";
        }

        private async void lstSeriesResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstSeriesResults.SelectedItem is SeriesSearchResult selected)
            {
                selectedSeriesId = selected.Id;
                selectedSeriesName = selected.Name;

                MessageBox.Show($"Selected series: {selectedSeriesName}");

                bool loaded = await FetchEpisodesForSelectedSeries();

                if (loaded)
                {
                    BuildPreviewWithEpisodeTitles();
                }
            }
        }

        // ---- COMPATIBILITY WRAPPERS FOR DESIGNER AUTO-NAMING ----

        private void btnSelectFolder_Click_1(object sender, EventArgs e)
        {
            btnSelectFolder_Click(sender, e);
        }

        private void btnPreview_Click_1(object sender, EventArgs e)
        {
            btnPreview_Click(sender, e);
        }

        private void btnRename_Click_1(object sender, EventArgs e)
        {
            btnRename_Click(sender, e);
        }

        private void btnUndo_Click_1(object sender, EventArgs e)
        {
            btnUndo_Click(sender, e);
        }

        private void btnFetchSeries_Click_1(object sender, EventArgs e)
        {
            btnFetchSeries_Click(sender, e);
        }

        private void btnSaveApiKey_Click_1(object sender, EventArgs e)
        {
            btnSaveApiKey_Click(sender, e);
        }

        private void btnToggleApiKey_Click_1(object sender, EventArgs e)
        {
            btnToggleApiKey_Click(sender, e);
        }

        private void lstSeriesResults_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            lstSeriesResults_SelectedIndexChanged(sender, e);
        }

        // ---- SETTINGS LOAD / SAVE ----

        private void SaveApiKey()
        {
            Directory.CreateDirectory(settingsDirectory);

            AppSettings settings = new AppSettings
            {
                ApiKey = apiKey
            };

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

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

        // ---- PREVIEW REFRESH ----

        private void RefreshPreviewAfterOperation()
        {
            if (episodeTitles.Count > 0 && selectedSeriesId != 0)
            {
                BuildPreviewWithEpisodeTitles();
            }
            else
            {
                BuildBasicPreview();
            }
        }

        // ---- BASIC PREVIEW LOGIC ----

        private void BuildBasicPreview()
        {
            ResetPreviewState(clearEpisodeTitles: false);

            if (!Directory.Exists(txtFolderPath.Text))
            {
                MessageBox.Show("Please select a valid folder first.");
                return;
            }

            string[] files = GetSupportedVideoFilesFromSelectedFolder();

            if (files.Length == 0)
            {
                AddPreviewRow(
                    "SKIP",
                    "No supported video files found",
                    $"Supported file types: {GetSupportedExtensionsText()}"
                );
                return;
            }

            foreach (string file in files)
            {
                string originalName = Path.GetFileName(file);
                string? newName = GenerateNewName(originalName);

                if (newName == null)
                {
                    AddPreviewRow("NO MATCH", originalName, "");
                    continue;
                }

                string newPath = Path.Combine(txtFolderPath.Text, newName);

                if (File.Exists(newPath))
                {
                    AddPreviewRow("SKIP", originalName, "Already exists");
                    continue;
                }

                previewItems.Add(new RenamePreviewItem
                {
                    OriginalPath = file,
                    NewPath = newPath,
                    Status = "OK"
                });

                AddPreviewRow("OK", originalName, newName);
            }

            btnRename.Enabled = previewItems.Count > 0;
            btnFetchSeries.Enabled = previewItems.Count > 0;
        }

        // ---- ENRICHED PREVIEW USING TVDB EPISODE TITLES ----

        private void BuildPreviewWithEpisodeTitles()
        {
            ResetPreviewState(clearEpisodeTitles: false);

            if (!Directory.Exists(txtFolderPath.Text))
            {
                MessageBox.Show("Please select a valid folder first.");
                return;
            }

            string[] files = GetSupportedVideoFilesFromSelectedFolder();

            if (files.Length == 0)
            {
                AddPreviewRow(
                    "SKIP",
                    "No supported video files found",
                    $"Supported file types: {GetSupportedExtensionsText()}"
                );
                return;
            }

            foreach (string file in files)
            {
                string originalName = Path.GetFileName(file);

                if (!chkForceRename.Checked && IsLikelyWrongSeries(originalName, selectedSeriesName))
                {
                    AddPreviewRow("WRONG SERIES", originalName, $"Selected series: {selectedSeriesName}");
                    continue;
                }

                if (!TryExtractEpisodeCode(originalName, out int seasonNumber, out int episodeNumber, out string episodeCode))
                {
                    AddPreviewRow("NO MATCH", originalName, "");
                    continue;
                }

                if (!episodeTitles.TryGetValue(episodeCode, out string episodeTitle))
                {
                    AddPreviewRow("NO TVDB TITLE", originalName, episodeCode);
                    continue;
                }

                string safeSeriesName = MakeSafeFileName(selectedSeriesName);
                string safeEpisodeTitle = MakeSafeFileName(episodeTitle);
                string extension = Path.GetExtension(file);

                string newName = $"{safeSeriesName} - {episodeCode} - {safeEpisodeTitle}{extension}";
                string newPath = Path.Combine(txtFolderPath.Text, newName);

                if (File.Exists(newPath))
                {
                    AddPreviewRow("SKIP", originalName, "Already exists");
                    continue;
                }

                previewItems.Add(new RenamePreviewItem
                {
                    OriginalPath = file,
                    NewPath = newPath,
                    Status = "OK"
                });

                AddPreviewRow("OK", originalName, newName);
            }

            btnRename.Enabled = previewItems.Count > 0;
        }

        // ---- WRONG SERIES DETECTION ----

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

            if (normalisedCandidate.Contains(normalisedSelected) || normalisedSelected.Contains(normalisedCandidate))
                return false;

            return true;
        }

        private string ExtractSeriesCandidateFromFileName(string fileName)
        {
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            Match episodeMatch = Regex.Match(
                nameWithoutExtension,
                @"S\d{1,2}E\d{1,2}",
                RegexOptions.IgnoreCase
            );

            if (!episodeMatch.Success)
                return "";

            string candidate = nameWithoutExtension.Substring(0, episodeMatch.Index);

            candidate = Regex.Replace(candidate, @"[\._\-]+", " ");
            candidate = Regex.Replace(candidate, @"\b(19|20)\d{2}\b", " ");
            candidate = Regex.Replace(candidate, @"\s+", " ").Trim();

            if (candidate.Length < 3)
                return "";

            return candidate;
        }

        private string NormaliseSeriesName(string value)
        {
            string normalised = value.ToLowerInvariant();

            normalised = Regex.Replace(normalised, @"\b(19|20)\d{2}\b", " ");
            normalised = Regex.Replace(normalised, @"[^a-z0-9]+", " ");
            normalised = Regex.Replace(normalised, @"\s+", " ").Trim();

            return normalised;
        }

        // ---- RENAME LOGIC ----

        private string? GenerateNewName(string fileName)
        {
            if (TryExtractEpisodeCode(fileName, out int seasonNumber, out int episodeNumber, out string episodeCode))
            {
                return $"{episodeCode}{Path.GetExtension(fileName)}";
            }

            return null;
        }

        private bool TryExtractEpisodeCode(string fileName, out int seasonNumber, out int episodeNumber, out string episodeCode)
        {
            seasonNumber = 0;
            episodeNumber = 0;
            episodeCode = "";

            Match match = Regex.Match(
                fileName,
                @"S(\d+)E(\d+)",
                RegexOptions.IgnoreCase
            );

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
            {
                value = value.Replace(invalidChar, '-');
            }

            return value.Trim();
        }

        // ---- JSON HELPERS ----

        private string GetJsonString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement property))
            {
                return property.GetString() ?? "";
            }

            return "";
        }

        private int GetJsonInt(JsonElement element, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (element.TryGetProperty(propertyName, out JsonElement property))
                {
                    if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int numberValue))
                    {
                        return numberValue;
                    }

                    if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out int stringValue))
                    {
                        return stringValue;
                    }
                }
            }

            return 0;
        }

        // ---- GITHUB UPDATE CHECK ----

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
                            $"A newer version of TV Series Renamer is available.\n\n" +
                            $"Current version: {CurrentVersion}\n" +
                            $"Latest version: {latestVersion}\n\n" +
                            "Do you want to open the download page?",
                            "Update Available",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information
                        );

                        if (result == DialogResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = releaseUrl,
                                UseShellExecute = true
                            });
                        }
                    }
                }
            }
            catch
            {
                // Update checks must never block or break the app.
                // Silent failure is intentional.
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
            {
                cleanedVersion = cleanedVersion.Substring(1);
            }

            Match match = Regex.Match(cleanedVersion, @"^\d+(\.\d+){0,3}");

            if (!match.Success)
                return null;

            if (Version.TryParse(match.Value, out Version? parsedVersion))
                return parsedVersion;

            return null;
        }

        // ---- TVDB AUTHENTICATION ----

        private async Task<bool> AuthenticateTVDB()
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("Please enter and save your API key first.");
                return false;
            }

            using (HttpClient client = new HttpClient())
            {
                var requestBody = new
                {
                    apikey = apiKey
                };

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
                    apiToken = doc.RootElement
                                  .GetProperty("data")
                                  .GetProperty("token")
                                  .GetString() ?? "";
                }

                return !string.IsNullOrWhiteSpace(apiToken);
            }
        }

        // ---- TVDB SERIES SEARCH ----

        private async Task SearchSeries(string seriesName)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiToken);

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

                    foreach (JsonElement item in results.EnumerateArray())
                    {
                        string name = GetJsonString(item, "name");
                        string type = GetJsonString(item, "type");
                        int id = GetJsonInt(item, "tvdb_id", "id");

                        if (string.IsNullOrWhiteSpace(name) || id == 0)
                            continue;

                        SeriesSearchResult result = new SeriesSearchResult
                        {
                            Id = id,
                            Name = name,
                            Type = type
                        };

                        seriesResults.Add(result);
                        lstSeriesResults.Items.Add(result);
                    }

                    if (lstSeriesResults.Items.Count == 0)
                    {
                        MessageBox.Show("No usable series results found.");
                    }
                }
            }
        }

        // ---- TVDB EPISODE FETCH ----

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
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiToken);

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

                    if (data.ValueKind == JsonValueKind.Object &&
                        data.TryGetProperty("episodes", out JsonElement episodeArray))
                    {
                        episodes = episodeArray;
                    }
                    else if (data.ValueKind == JsonValueKind.Array)
                    {
                        episodes = data;
                    }
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
                        {
                            episodeTitles.Add(episodeCode, title);
                        }
                    }
                }
            }

            MessageBox.Show($"Loaded {episodeTitles.Count} episode titles for {selectedSeriesName}.");
            return episodeTitles.Count > 0;
        }

        private void btnApiHelp_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://thetvdb.com/api-information",
                UseShellExecute = true
            });
        }
    }
}