using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using iviewer.Services;
using iviewer.Helpers;

namespace iviewer
{
    public partial class VideoGenerator : Form
    {
        private readonly VideoGenerationService _generationService;
        private readonly VideoExportService _exportService;
        private readonly VideoMetadataService _metadataService;
        private readonly FileManagementService _fileService;
        private readonly UIUpdateService _uiService;

        private List<string> _rowImagePaths = new List<string>();
        private List<string> _perPromptVideoPaths = new List<string>();
        private List<string> _rowWorkflows = new List<string>();
        private List<VideoClipInfo> _clipInfos = new List<VideoClipInfo>();
        private List<string> _tempFiles = new List<string>();

        // Stream references for video players
        private FileStream _fullStream;
        private FileStream _perPromptStream;

        private enum GenerationStatus
        {
            Idle,
            Generating,
            Generated,
            Modified
        }

        private GenerationStatus _generationStatus = GenerationStatus.Idle;

        public VideoGenerator()
        {
            InitializeComponent();
       
            var config = new VideoGenerationConfig();
            _generationService = new VideoGenerationService(config);
            _exportService = new VideoExportService(config);
            _metadataService = new VideoMetadataService();
            _fileService = new FileManagementService(config.TempDir);
            _uiService = new UIUpdateService();

            InitializeGrid();
            SetupEventHandlers();
            InitializeVideoPlayers();
        }

        private void SetupEventHandlers()
        {
            tabControl.SelectedIndexChanged += OnTabChanged;
            dgvPrompts.CellClick += OnGridCellClick;
            dgvPrompts.CellValueChanged += OnGridValueChanged;
            dgvPrompts.EditingControlShowing += OnGridEditingControlShowing;
        }

        #region Generation Tab Methods

        private void InitializeGrid()
        {
            GridInitializer.Initialize(dgvPrompts);
            AddNewRow();
        }

        private void AddNewRow()
        {
            var rowData = new VideoRowData
            {
                Prompt = GetDefaultOrPreviousPrompt(),
                ImagePath = "",
                VideoPath = "",
                WorkflowJson = ""
            };

            int rowIndex = dgvPrompts.Rows.Add();
            PopulateGridRow(rowIndex, rowData);

            _rowImagePaths.Add("");
            _perPromptVideoPaths.Add("");
            _rowWorkflows.Add("");

            UpdateUI();
        }

        private string GetDefaultOrPreviousPrompt()
        {
            if (dgvPrompts.Rows.Count > 0)
            {
                return dgvPrompts.Rows[dgvPrompts.Rows.Count - 1].Cells["colPrompt"].Value?.ToString() ?? "";
            }
            return "A woman posing for a photoshoot. She smiles and sways her body. The camera remains static throughout. The lighting remains static throughout. The background remains static throughout.";
        }

        private void PopulateGridRow(int rowIndex, VideoRowData data)
        {
            var row = dgvPrompts.Rows[rowIndex];
            row.Cells["colPrompt"].Value = data.Prompt;
            row.Cells["colLora"].Value = "Select LoRA";
            row.Cells["colGenerate"].Value = "Generate";

            if (!string.IsNullOrEmpty(data.ImagePath))
            {
                row.Cells["colImage"].Value = ImageHelper.CreateThumbnail(data.ImagePath, 160, 160);
            }

            dgvPrompts.CurrentCell = dgvPrompts.Rows[rowIndex].Cells["colPrompt"];
            dgvPrompts.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
        }

        private async void OnGenerateSingleClick(int rowIndex)
        {
            var rowData = GetRowData(rowIndex);
            if (!ValidateRowData(rowData))
            {
                MessageBox.Show("Select an image and enter a prompt for this row.");
                return;
            }

            UpdateRowStatus(rowIndex, "Generating...");

            try
            {
                string videoPath = await _generationService.GenerateVideoAsync(rowData, rowIndex);

                if (!string.IsNullOrEmpty(videoPath))
                {
                    _perPromptVideoPaths[rowIndex] = videoPath;
                    _rowWorkflows[rowIndex] = rowData.WorkflowJson;
                    UpdateRowStatus(rowIndex, "Generated");
                    RefreshPreviewIfOpen();
                }
                else
                {
                    UpdateRowStatus(rowIndex, "Generate");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Generation failed: {ex.Message}");
                UpdateRowStatus(rowIndex, "Generate");
            }

            UpdateUI();
        }

        private async void OnGenerateAllClick()
        {
            if (!ValidateGenerateAll()) return;

            ResetAllRowStatuses();

            try
            {
                await _generationService.GenerateAllVideosAsync(
                    GetAllRowData(),
                    _rowImagePaths,
                    _perPromptVideoPaths,
                    _rowWorkflows,
                    OnRowStatusUpdate,
                    OnProgressUpdate);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Batch generation failed: {ex.Message}");
            }

            UpdateUI();
        }

        private VideoRowData GetRowData(int rowIndex)
        {
            var row = dgvPrompts.Rows[rowIndex];
            return new VideoRowData
            {
                Prompt = row.Cells["colPrompt"].Value?.ToString() ?? "",
                ImagePath = _rowImagePaths[rowIndex],
                EndImagePath = GetEndImagePath(rowIndex),
                VideoPath = _perPromptVideoPaths[rowIndex],
                WorkflowJson = _rowWorkflows[rowIndex]
            };
        }

        private string GetEndImagePath(int rowIndex)
        {
            return rowIndex + 1 < _rowImagePaths.Count && !string.IsNullOrEmpty(_rowImagePaths[rowIndex + 1])
                ? _rowImagePaths[rowIndex + 1]
                : null;
        }

        private void DeleteSelectedRow()
        {
            if (dgvPrompts.SelectedRows.Count > 0)
            {
                int rowIndex = dgvPrompts.SelectedRows[0].Index;

                // Confirm deletion if video exists
                string videoPath = _perPromptVideoPaths[rowIndex];
                if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                {
                    var result = MessageBox.Show(
                        "Delete associated video file?",
                        "Confirm Deletion",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        File.Delete(videoPath);
                    }
                }

                // Remove from collections
                dgvPrompts.Rows.RemoveAt(rowIndex);
                _rowImagePaths.RemoveAt(rowIndex);
                _perPromptVideoPaths.RemoveAt(rowIndex);
                _rowWorkflows.RemoveAt(rowIndex);

                UpdateUI();
            }
        }

        private void ExtractLastFrameFromSelected()
        {
            if (dgvPrompts.SelectedRows.Count == 0) return;

            int rowIndex = dgvPrompts.SelectedRows[0].Index;
            string videoPath = _perPromptVideoPaths[rowIndex];

            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath)) return;

            try
            {
                string framePath = VideoUtils.ExtractLastFrame(videoPath, _fileService.TempDir);
                if (!string.IsNullOrEmpty(framePath))
                {
                    // Determine target row (next row if exists, otherwise current)
                    int targetRow = rowIndex + 1 < _rowImagePaths.Count ? rowIndex + 1 : rowIndex;

                    _rowImagePaths[targetRow] = framePath;
                    _tempFiles.Add(framePath);

                    // Update thumbnail in grid
                    var thumbnail = ImageHelper.CreateThumbnail(framePath, 160, 160);
                    dgvPrompts.Rows[targetRow].Cells["colImage"].Value = thumbnail;

                    UpdateUI();
                    MessageBox.Show($"Last frame extracted to row {targetRow + 1}.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to extract last frame: {ex.Message}");
            }
        }

        #endregion

        #region Preview Tab Methods

        private void LoadPreviewTabs()
        {
            var validVideos = _perPromptVideoPaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList();

            // Load Full Video tab
            string fullVideoPath = validVideos.LastOrDefault();
            if (!string.IsNullOrEmpty(fullVideoPath))
            {
                LoadVideoInPlayer(videoPlayerFull, fullVideoPath, ref _fullStream);
            }

            // Load Per-Prompt Videos tab
            LoadPerPromptVideosTab(validVideos);
        }

        private void LoadPerPromptVideosTab(List<string> validVideos)
        {
            var flowPanel = VideoPlayerHelper.CreateVideoButtonsPanel(
                validVideos,
                GenerateClipInfos(),
                OnVideoButtonClick,
                OnRegenButtonClick);

            // Clear and re-add controls to per-prompt tab
            tabPagePerPrompt.Controls.Clear();
            tabPagePerPrompt.Controls.Add(videoPlayerPerPrompt);
            tabPagePerPrompt.Controls.Add(btnExport);
            tabPagePerPrompt.Controls.Add(btnImport);
            tabPagePerPrompt.Controls.Add(flowPanel);
            videoPlayerPerPrompt.BringToFront();
            btnExport.BringToFront();
            btnImport.BringToFront();
        }

        private void LoadVideoInPlayer(VideoPlayerControl player, string videoPath, ref FileStream streamRef)
        {
            try
            {
                player.StopAndHide();

                // Dispose previous stream
                streamRef?.Dispose();
                streamRef = null;

                // Normalize path
                string normalizedPath = Path.GetFullPath(videoPath);
                normalizedPath = normalizedPath.Replace(@"\\", @"\");
                normalizedPath = normalizedPath.Replace("\\\\", "\\");
                normalizedPath = normalizedPath.Replace("\\", "/");

                // Create new stream and assign
                streamRef = new FileStream(normalizedPath, FileMode.Open, FileAccess.Read);
                player.VideoStream = streamRef;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading video {Path.GetFileName(videoPath)}: {ex.Message}\nPath: {videoPath}");
            }
        }

        private void OnVideoButtonClick(int videoIndex, string videoPath)
        {
            LoadVideoInPlayer(videoPlayerPerPrompt, videoPath, ref _perPromptStream);
        }

        private void OnRegenButtonClick(int rowIndex)
        {
            tabControl.SelectedIndex = 0; // Switch to generation tab
            SelectAndGenerateRow(rowIndex);
        }

        #endregion

        #region Export Methods

        private async void OnExportClick()
        {
            if (!ValidateExport()) return;

            SetExportButtonState(true, "Exporting...");

            try
            {
                var exportData = new VideoExportData
                {
                    VideoPaths = _perPromptVideoPaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList(),
                    ClipInfos = GenerateClipInfos(),
                    ExportTimestamp = DateTime.Now
                };

                string exportPath = await _exportService.ExportVideosAsync(exportData, OnExportProgressUpdate);
                MessageBox.Show($"Exported successfully to: {exportPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}");
            }
            finally
            {
                SetExportButtonState(false, "Export Stitched");
            }
        }

        private void ImportVideosForTesting()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "MP4 Videos|*.mp4",
                Multiselect = true,
                Title = "Select Videos to Import for Testing"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var imported = ofd.FileNames.ToList();

                // Add imported videos to the collections
                for (int i = 0; i < imported.Count; i++)
                {
                    _perPromptVideoPaths.Add(imported[i]);
                    _rowImagePaths.Add("");
                    _rowWorkflows.Add("");

                    // Add grid row
                    int rowIndex = dgvPrompts.Rows.Add();
                    dgvPrompts.Rows[rowIndex].Cells["colPrompt"].Value = "Imported Video";
                    dgvPrompts.Rows[rowIndex].Cells["colGenerate"].Value = "Imported";

                    // Add clip info for metadata
                    _clipInfos.Add(new VideoClipInfo
                    {
                        Path = imported[i],
                        Prompt = "Imported Video",
                        Resolution = "Unknown",
                        Duration = VideoUtils.GetVideoDuration(imported[i]),
                        RowIndex = -1 // Flag as imported
                    });
                }

                UpdateUI();
                MessageBox.Show($"{imported.Count} videos imported. Total videos: {_perPromptVideoPaths.Count(p => !string.IsNullOrEmpty(p))}");
            }
        }

        private List<VideoClipInfo> GenerateClipInfos()
        {
            var clipInfos = new List<VideoClipInfo>();

            for (int i = 0; i < dgvPrompts.Rows.Count; i++)
            {
                if (!string.IsNullOrEmpty(_perPromptVideoPaths[i]) && File.Exists(_perPromptVideoPaths[i]))
                {
                    var clipInfo = _metadataService.ExtractClipInfo(
                        GetRowData(i),
                        lblResolution.Text,
                        i);

                    if (clipInfo.Prompt == "Imported Video")
                    {
                        clipInfo.RowIndex = -1;
                    }

                    clipInfos.Add(clipInfo);
                }
            }

            return clipInfos;
        }

        private void OnExportProgressUpdate(int percentage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => pbProgress.Value = percentage));
            }
            else
            {
                pbProgress.Value = percentage;
            }
        }

        #endregion

        #region UI Update Methods

        private void UpdateUI()
        {
            UpdateResolutionLabel();
            UpdateButtonStates();
            RefreshPreviewIfOpen();
        }

        private void UpdateResolutionLabel()
        {
            if (_rowImagePaths.Count > 0 && !string.IsNullOrEmpty(_rowImagePaths[0]))
            {
                var (width, height) = ResolutionCalculator.Calculate(_rowImagePaths[0]);
                lblResolution.Text = $"{width}x{height}";
            }
            else
            {
                lblResolution.Text = "N/A";
            }
        }

        private void UpdateButtonStates()
        {
            btnGenerateAll.Enabled = dgvPrompts.Rows.Count > 0;
            btnExtractLast.Enabled = HasGeneratedVideos() && dgvPrompts.SelectedRows.Count > 0;
            btnPreview.Enabled = HasGeneratedVideos();
            btnExport.Enabled = HasGeneratedVideos();
        }

        private bool HasGeneratedVideos()
        {
            return _perPromptVideoPaths.Any(p => !string.IsNullOrEmpty(p) && File.Exists(p));
        }

        private void UpdateRowStatus(int rowIndex, string status)
        {
            if (rowIndex >= 0 && rowIndex < dgvPrompts.Rows.Count)
            {
                dgvPrompts.Rows[rowIndex].Cells["colGenerate"].Value = status;
                dgvPrompts.Refresh();
            }
        }

        private void OnRowStatusUpdate(int rowIndex, string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateRowStatus(rowIndex, status)));
            }
            else
            {
                UpdateRowStatus(rowIndex, status);
            }
        }

        private void OnProgressUpdate(int percentage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => pbProgress.Value = percentage));
            }
            else
            {
                pbProgress.Value = percentage;
            }
        }

        #endregion

        #region Event Handlers

        private void OnTabChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedIndex == 0) // Generation tab
            {
                // Stop and dispose video players when switching to generation
                videoPlayerFull?.StopAndHide();
                videoPlayerPerPrompt?.StopAndHide();
                _fullStream?.Dispose();
                _fullStream = null;
                _perPromptStream?.Dispose();
                _perPromptStream = null;
            }
            else if (tabControl.SelectedIndex == 1 || tabControl.SelectedIndex == 2) // Preview tabs
            {
                LoadPreviewTabs();
            }
        }

        private void OnGridCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            string columnName = dgvPrompts.Columns[e.ColumnIndex].Name;

            switch (columnName)
            {
                case "colImage":
                    HandleImageSelection(e.RowIndex);
                    break;
                case "colLora":
                    HandleLoraSelection(e.RowIndex);
                    break;
                case "colGenerate":
                    OnGenerateSingleClick(e.RowIndex);
                    break;
            }
        }

        private void HandleImageSelection(int rowIndex)
        {
            using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _rowImagePaths[rowIndex] = ofd.FileName;
                dgvPrompts.Rows[rowIndex].Cells["colImage"].Value =
                    ImageHelper.CreateThumbnail(ofd.FileName, 160, 160);
                UpdateUI();
            }
        }

        private void HandleLoraSelection(int rowIndex)
        {
            MessageBox.Show("LoRA selector coming in Stage 3.");
        }

        private void OnGridValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                ResetRowStatusIfGenerated(e.RowIndex);
                UpdateUI();
            }
        }

        private void OnGridEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox tb && dgvPrompts.CurrentCell.ColumnIndex == dgvPrompts.Columns["colPrompt"].Index)
            {
                tb.Multiline = true;
                tb.AcceptsReturn = true;
                tb.WordWrap = true;
            }
        }

        #endregion

        #region Helper Methods

        private bool ValidateRowData(VideoRowData data)
        {
            return !string.IsNullOrEmpty(data.ImagePath) && !string.IsNullOrEmpty(data.Prompt);
        }

        private bool ValidateGenerateAll()
        {
            if (dgvPrompts.Rows.Count == 0)
            {
                MessageBox.Show("Add at least one prompt.");
                return false;
            }
            return true;
        }

        private bool ValidateExport()
        {
            if (!HasGeneratedVideos())
            {
                MessageBox.Show("No videos to export.");
                return false;
            }
            return true;
        }

        private void ResetAllRowStatuses()
        {
            foreach (DataGridViewRow row in dgvPrompts.Rows)
            {
                row.Cells["colGenerate"].Value = "Generate";
            }
            dgvPrompts.Refresh();
        }

        private void ResetRowStatusIfGenerated(int rowIndex)
        {
            var row = dgvPrompts.Rows[rowIndex];
            if (row.Cells["colGenerate"].Value?.ToString() == "Generated")
            {
                row.Cells["colGenerate"].Value = "Generate";
            }
        }

        private List<VideoRowData> GetAllRowData()
        {
            return Enumerable.Range(0, dgvPrompts.Rows.Count)
                           .Select(GetRowData)
                           .ToList();
        }

        private void SelectAndGenerateRow(int rowIndex)
        {
            dgvPrompts.ClearSelection();
            dgvPrompts.Rows[rowIndex].Selected = true;
            dgvPrompts.CurrentCell = dgvPrompts.Rows[rowIndex].Cells["colGenerate"];
            OnGenerateSingleClick(rowIndex);
        }

        private void RefreshPreviewIfOpen()
        {
            // Update preview tabs if they're currently visible
            if (tabControl.SelectedIndex == 1 || tabControl.SelectedIndex == 2)
            {
                LoadPreviewTabs();
            }
        }

        private void SetExportButtonState(bool isExporting, string text)
        {
            btnExport.Enabled = !isExporting;
            btnExport.Text = text;
        }

        #endregion

        #region Cleanup

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            var action = MessageBox.Show("Delete temporary files?", "Video Generator", MessageBoxButtons.YesNoCancel);

            if (action == DialogResult.Yes)
            {
                _fileService.CleanupTempFiles(_tempFiles);
            }
            else if (action == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            // Dispose video players and streams
            videoPlayerFull?.StopAndHide();
            videoPlayerPerPrompt?.StopAndHide();
            _fullStream?.Dispose();
            _perPromptStream?.Dispose();

            base.OnFormClosing(e);
        }

        #endregion
    }

    #region Data Classes

    public class VideoRowData
    {
        public string Prompt { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public string EndImagePath { get; set; } = "";
        public string VideoPath { get; set; } = "";
        public string WorkflowJson { get; set; } = "";
    }

    public class VideoExportData
    {
        public List<string> VideoPaths { get; set; } = new();
        public List<VideoClipInfo> ClipInfos { get; set; } = new();
        public DateTime ExportTimestamp { get; set; }
    }

    public class VideoGenerationConfig
    {
        public string OutputDir { get; set; } = @"C:\Users\sysadmin\Documents\ComfyUI\output\iviewer";
        public string TempDir => Path.Combine(OutputDir, "temp");
        public string WorkflowPath { get; set; } = @"C:\xfer\Simplified - Export API.json";
        public string WorkflowPathLast { get; set; } = @"C:\xfer\iViewer Wan22 flf - Export API.json";
        public string ExportDir { get; set; } = @"D:\Users\sysadmin\Archive\Visual Studio Projects\SD-Processed";
    }

    #endregion
}