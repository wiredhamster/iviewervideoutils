using iviewer.Helpers;
using iviewer.Services;
using iviewer.Video;
using System.Data;

namespace iviewer
{
    public partial class VideoGenerator : Form
    {
        private readonly VideoGenerationConfig _config;
        private readonly VideoGenerationService _generationService;
        private readonly VideoExportService _exportService;
        private readonly VideoMetadataService _metadataService;
        private readonly FileManagementService _fileService;
        private readonly UIUpdateService _uiService;

        private Guid _videoGenerationStatePK;
        private VideoGenerationState _videoGenerationState;
        private bool _isEditing = false;
        private bool _isExported = false;

        public Guid VideoGenerationStatePK => _videoGenerationStatePK;

        private List<string> _rowImagePaths = new List<string>();
        private List<string> _perPromptVideoPaths = new List<string>();
        private List<string> _rowWorkflows = new List<string>();
        private List<VideoClipInfo> _clipInfos = new List<VideoClipInfo>();
        private string _previewVideoPath = null;
        private List<string> _tempFiles = new List<string>();

        private int _width = 0;
        private int _height = 0;

        // Stream references for video players
        private FileStream _fullStream;
        private FileStream _perPromptStream;

        // Play all videos
        private bool _isPlayingAll = false;
        private int _currentPlayAllIndex = 0;
        private List<string> _playAllVideos = new List<string>();
        private List<Button> _videoButtons = new List<Button>(); // Track video buttons for highlighting
        private Button _currentlyHighlightedButton = null;

        // Transition settings - modify these to experiment with different types
        public const string TRANSITION_TYPE = "fade"; // Options: "fade", "dissolve", "wipe", "slide"
        private const double DEFAULT_TRANSITION_DURATION = 0.1; // Default duration in seconds
        private const double DEFAULT_CLIP_SPEED = 1.0;
        private List<double> _transitionDurations = new List<double>();
        private List<double> _clipSpeeds = new List<double>();

        private enum GenerationStatus
        {
            Idle,
            Generating,
            Generated,
            Modified
        }

        private GenerationStatus _generationStatus = GenerationStatus.Idle;
        private bool _updatingRowStatus = false;

        public VideoGenerator()
        {
            InitializeComponent();

            _config = new VideoGenerationConfig();
            _generationService = new VideoGenerationService(_config);
            _exportService = new VideoExportService(_config);
            _metadataService = new VideoMetadataService();
            _fileService = new FileManagementService(_config);
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

            EventBus.ClipStatusChanged += OnClipStatusChanged;

            // Grid events for edit mode
            dgvPrompts.CellBeginEdit += (s, e) => _isEditing = true;
            dgvPrompts.CellEndEdit += (s, e) => _isEditing = false;
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

            int insertIndex;
            if (dgvPrompts.SelectedRows.Count > 0)
            {
                insertIndex = dgvPrompts.SelectedRows[0].Index + 1;
            }
            else
            {
                insertIndex = dgvPrompts.Rows.Count; // Add at end if no selection
            }

            // Insert new row in grid
            dgvPrompts.Rows.Insert(insertIndex);

            // Insert empty entries in lists at the correct position
            _rowImagePaths.Insert(insertIndex, "");
            _perPromptVideoPaths.Insert(insertIndex, "");
            _rowWorkflows.Insert(insertIndex, "");

            PopulateGridRow(insertIndex, rowData);

            if (_transitionDurations.Count >= insertIndex)
            {
                _transitionDurations.Insert(insertIndex, DEFAULT_TRANSITION_DURATION);
            }

            if (_clipSpeeds.Count >= insertIndex)
            {
                _clipSpeeds.Insert(insertIndex, DEFAULT_CLIP_SPEED);
            }

            // Set initial status
            UpdateRowGenerationStatus(insertIndex);
            UpdateState();
            UpdateUI();

            // Select the new row
            dgvPrompts.ClearSelection();
            dgvPrompts.Rows[insertIndex].Selected = true;
            dgvPrompts.CurrentCell = dgvPrompts.Rows[insertIndex].Cells["colPrompt"];
        }

        private string GetDefaultOrPreviousPrompt()
        {
            if (dgvPrompts.Rows.Count > 0)
            {
                return dgvPrompts.Rows[dgvPrompts.Rows.Count - 1].Cells["colPrompt"].Value?.ToString() ?? "";
            }
            return "A woman posing for a photoshoot. She smiles and sways her body. The camera remains static throughout. The lighting remains consistent throughout. The background remains static throughout.";
        }

        private void PopulateGridRow(int rowIndex, VideoRowData data)
        {
            var row = dgvPrompts.Rows[rowIndex];
            row.Cells["colPrompt"].Value = data.Prompt;
            row.Cells["colLora"].Value = "Select LoRA";
            row.Cells["colQueue"].Value = "Queue";

            if (!string.IsNullOrEmpty(data.ImagePath))
            {
                row.Cells["colImage"].Value = ImageHelper.CreateThumbnail(data.ImagePath, null, 160);
            }
            else if (rowIndex > 0 && _perPromptVideoPaths[rowIndex - 1] != "")
            {
                string framePath = VideoUtils.ExtractLastFrame(_perPromptVideoPaths[rowIndex - 1], _fileService.TempDir);
                if (!string.IsNullOrEmpty(framePath) && File.Exists(framePath))
                {
                    _rowImagePaths[rowIndex] = framePath;
                    _tempFiles.Add(framePath);

                    // Update thumbnail in grid with error handling
                    try
                    {
                        var thumbnail = ImageHelper.CreateThumbnail(framePath, null, 160);
                        dgvPrompts.Rows[rowIndex].Cells["colImage"].Value = thumbnail;
                    }
                    catch (Exception thumbnailEx)
                    {
                        MessageBox.Show($"Frame extracted but thumbnail update failed: {thumbnailEx.Message}");
                    }
                }
            }

            dgvPrompts.CurrentCell = dgvPrompts.Rows[rowIndex].Cells["colPrompt"];
            dgvPrompts.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCells);
        }

        private async void OnQueueSingleClick(int rowIndex)
        {
            var rowData = GetRowData(rowIndex);
            if (!ValidateRowData(rowData))
            {
                MessageBox.Show("Select an image and enter a prompt for this row.");
                return;
            }

            UpdateRowStatus(rowIndex, "Queuing");

            var allRowData = GetAllRowData();
            if (_width == 0 || _height == 0)
            {
                (_width, _height) = ResolutionCalculator.Calculate(allRowData.First().ImagePath);
            }

            UpdateState();

            _generationService.QueueClips(_videoGenerationState);

            LoadState(_videoGenerationStatePK);
            UpdateUI();

            QueryStartQueue();
        }

        private async void OnQueueAllClick()
        {
            if (!ValidateGenerateAll()) return;

            try
            {
                var allRowData = GetAllRowData();
                if (_width == 0 || _height == 0)
                {
                    (_width, _height) = ResolutionCalculator.Calculate(allRowData.First().ImagePath);
                }

                for (var rowIndex = 0; rowIndex < dgvPrompts.RowCount; rowIndex++)
                {
                    var row = dgvPrompts.Rows[rowIndex];
                    if ((row.Cells["colQueue"].Value.Equals("Queue") || row.Cells["colQueue"].Value.Equals("Requeue")) && row.Cells["colPrompt"].Value != null)
                    {
                        UpdateRowStatus(rowIndex, "Queuing");
                    }
                }

                UpdateState();

                _generationService.QueueClips(_videoGenerationState);

                LoadState(_videoGenerationStatePK);
                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Batch generation failed: {ex.Message}");
            }

            UpdateUI();

            QueryStartQueue();
        }

        void QueryStartQueue()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form as QueueForm != null)
                {
                    var queueForm = (QueueForm)form;
                    if (!queueForm.Running)
                    {
                        if (MessageBox.Show("Start Queue?", "iViewer", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            queueForm.Start();
                            break;
                        }
                    }
                }                
            }
        }

        private void OnRowImageUpdate(int rowIndex, string imagePath)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnRowImageUpdate(rowIndex, imagePath)));
                return;
            }

            try
            {
                if (rowIndex >= 0 && rowIndex < dgvPrompts.Rows.Count)
                {
                    // Create and set thumbnail
                    var thumbnail = ImageHelper.CreateThumbnail(imagePath, null, 160);
                    dgvPrompts.Rows[rowIndex].Cells["colImage"].Value = thumbnail;

                    // Refresh the grid to show the change
                    dgvPrompts.InvalidateRow(rowIndex);
                    dgvPrompts.Refresh();

                    // Also update the resolution label if this is the first image
                    if (rowIndex == 0 || string.IsNullOrEmpty(lblResolution.Text) || lblResolution.Text == "N/A")
                    {
                        (_width, _height) = ResolutionCalculator.Calculate(imagePath);
                        lblResolution.Text = $"{_width}x{_height}";
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't stop generation process
                Console.WriteLine($"Error updating thumbnail for row {rowIndex}: {ex.Message}");
            }
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

                // Remove clip
                var clip = _videoGenerationState?.ClipGenerationStates.FirstOrDefault(c => dgvPrompts.Rows[rowIndex].Tag != null && c.PK == Guid.Parse(dgvPrompts.Rows[rowIndex].Tag.ToString()));
                if (clip != null)
                {
                    _videoGenerationState.ClipGenerationStates.Remove(clip);
                    clip.Delete();
                }

                // Remove from collections
                dgvPrompts.Rows.RemoveAt(rowIndex);
                _rowImagePaths.RemoveAt(rowIndex);
                _perPromptVideoPaths.RemoveAt(rowIndex);
                _rowWorkflows.RemoveAt(rowIndex);

                // Remove transition duration (but keep at least one)
                if (_transitionDurations.Count > rowIndex)
                {
                    _transitionDurations.RemoveAt(rowIndex);
                }

                if (_clipSpeeds.Count > rowIndex)
                {
                    _clipSpeeds.RemoveAt(rowIndex);
                }

                EventBus.RaiseItemQueued(Guid.Empty);

                UpdateState();
                UpdateUI();
            }
        }

        void OnDeleteImage()
        {
            if (dgvPrompts.SelectedRows.Count == 0) return;

            int rowIndex = dgvPrompts.SelectedRows[0].Index;
            dgvPrompts.Rows[rowIndex].Cells["colImage"].Value = null;
            _rowImagePaths[rowIndex] = String.Empty;
        }

        private void ExtractLastFrameFromSelected()
        {
            if (dgvPrompts.SelectedRows.Count == 0) return;

            int rowIndex = dgvPrompts.SelectedRows[0].Index;
            string videoPath = _perPromptVideoPaths[rowIndex];

            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                MessageBox.Show("No video file found for selected row.");
                return;
            }

            try
            {
                string framePath = VideoUtils.ExtractLastFrame(videoPath, _fileService.TempDir);
                if (!string.IsNullOrEmpty(framePath) && File.Exists(framePath))
                {
                    // Determine target row (next row if exists, otherwise current)
                    int targetRow = rowIndex + 1 < _rowImagePaths.Count ? rowIndex + 1 : rowIndex;

                    _rowImagePaths[targetRow] = framePath;
                    _tempFiles.Add(framePath);

                    // Update thumbnail in grid with error handling
                    try
                    {
                        var thumbnail = ImageHelper.CreateThumbnail(framePath, null, 160);
                        dgvPrompts.Rows[targetRow].Cells["colImage"].Value = thumbnail;

                        // Force refresh of the specific row
                        dgvPrompts.InvalidateRow(targetRow);
                        dgvPrompts.Refresh();
                    }
                    catch (Exception thumbnailEx)
                    {
                        MessageBox.Show($"Frame extracted but thumbnail update failed: {thumbnailEx.Message}");
                    }

                    UpdateUI();
                    MessageBox.Show($"Last frame extracted to row {targetRow + 1}.");
                }
                else
                {
                    MessageBox.Show("Failed to extract frame or frame file not created.");
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
            // Load Full Video tab
            if (!string.IsNullOrEmpty(_previewVideoPath) && File.Exists(_previewVideoPath))
            {
                LoadVideoInPlayer(videoPlayerFull, _previewVideoPath, ref _fullStream);
            }

            // Load Per-Prompt Videos tab
            LoadPerPromptVideosTab();
        }

        private void LoadPerPromptVideosTab()
        {
            _videoButtons.Clear();

            EnsureTransitionDurationsPopulated();
            EnsureClipSpeedsPopulated();

            var flowPanel = VideoPlayerHelper.CreateVideoButtonsPanel(
                _perPromptVideoPaths,
                GenerateClipInfos(),
                OnVideoButtonClick,
                _transitionDurations,
                OnTransitionDurationChanged,
                _clipSpeeds,
                OnClipSpeedChanged,
                _videoButtons);

            // Configure flow panel to not overlap buttons
            flowPanel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            flowPanel.Location = new Point(3, tabPagePerPrompt.Height - 200);
            flowPanel.Size = new Size(986, 165);
            flowPanel.AutoScroll = true;

            // Clear existing dynamic controls but keep the buttons and video player
            var controlsToRemove = tabPagePerPrompt.Controls.OfType<Control>()
                .Where(c => c != videoPlayerPerPrompt && c != btnPreview && c != btnImport && c != btnPlayAll)
                .ToList();

            foreach (var control in controlsToRemove)
            {
                tabPagePerPrompt.Controls.Remove(control);
                control.Dispose();
            }

            // Add the flow panel
            tabPagePerPrompt.Controls.Add(flowPanel);
            videoPlayerPerPrompt.BringToFront();

            // Ensure buttons are visible and positioned correctly
            btnPreview.BringToFront();
            btnImport.BringToFront();
            btnPlayAll.BringToFront();

            // Adjust video player size to accommodate flow panel
            videoPlayerPerPrompt.Size = new Size(986, tabPagePerPrompt.Height - 235);
        }

        private void OnTransitionDurationChanged(int clipIndex, double duration)
        {
            if (clipIndex >= 0 && clipIndex < _transitionDurations.Count)
            {
                _transitionDurations[clipIndex] = duration;
            }
        }

        private void OnClipSpeedChanged(int clipIndex, double speed)
        {
            if (clipIndex >= 0 && clipIndex < _clipSpeeds.Count)
            {
                _clipSpeeds[clipIndex] = speed;
            }
        }

        private void EnsureTransitionDurationsPopulated()
        {
            // Ensure we have enough transition duration entries
            while (_transitionDurations.Count < _perPromptVideoPaths.Count)
            {
                _transitionDurations.Add(DEFAULT_TRANSITION_DURATION);
            }

            // Remove excess entries if we have too many
            while (_transitionDurations.Count > _perPromptVideoPaths.Count)
            {
                _transitionDurations.RemoveAt(_transitionDurations.Count - 1);
            }
        }

        private void EnsureClipSpeedsPopulated()
        {
            // Ensure we have enough clip speed entries
            while (_clipSpeeds.Count < _perPromptVideoPaths.Count)
            {
                _clipSpeeds.Add(DEFAULT_CLIP_SPEED);
            }

            // Remove excess entries if we have too many
            while (_clipSpeeds.Count > _perPromptVideoPaths.Count)
            {
                _clipSpeeds.RemoveAt(_clipSpeeds.Count - 1);
            }
        }

        private void LoadVideoInPlayer(VideoPlayerControl player, string videoPath, ref FileStream streamRef, double speed = 1)
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
                player.SetSpeed(speed);

                btnExport.BringToFront();
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading video {Path.GetFileName(videoPath)}: {ex.Message}\nPath: {videoPath}");
            }
        }

        private void OnVideoButtonClick(int videoIndex, string videoPath, double speed)
        {
            // If playing all, stop the sequence
            if (_isPlayingAll)
            {
                StopPlayAllSequence();
            }

            // Reset all highlights since user manually selected a video
            ResetAllVideoButtonHighlights();

            // Temporarily highlight the selected button
            if (videoIndex < _videoButtons.Count && _videoButtons[videoIndex] != null)
            {
                var button = _videoButtons[videoIndex];
                button.BackColor = Color.LightYellow;
                button.ForeColor = Color.DarkOrange;

                // Reset after a short delay
                Task.Delay(1500).ContinueWith(_ =>
                {
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() =>
                        {
                            if (!_isPlayingAll) // Only reset if not playing all
                            {
                                ResetAllVideoButtonHighlights();
                            }
                        }));
                    }
                });
            }

            LoadVideoInPlayer(videoPlayerPerPrompt, videoPath, ref _perPromptStream, speed);
        }

        #endregion

        #region Preview Methods

        private async void OnPreviewClick()
        {
            if (!ValidateExport()) return;

            SetPreviewButtonState(true, "Creating...");

            try
            {
                string previewFilename = $"preview_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                string previewPath = Path.Combine(_fileService.TempDir, previewFilename);

                // Create preview with transitions but without upscaling/interpolation
                bool success = await VideoUtils.StitchVideosWithTransitionsAsync(
                    _perPromptVideoPaths,
                    previewPath,
                    TRANSITION_TYPE,
                    _transitionDurations,
                    _clipSpeeds, 
                    true,
                    false);

                if (success)
                {
                    _previewVideoPath = previewPath;
                    _tempFiles.Add(previewPath); // Track for cleanup

                    // Switch to Full Video tab and load the preview
                    tabControl.SelectedIndex = 2; // Full Video tab
                    LoadVideoInPlayer(videoPlayerFull, previewPath, ref _fullStream);
                }
                else
                {
                    MessageBox.Show("Failed to create preview video.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Preview creation failed: {ex.Message}");
            }
            finally
            {
                SetPreviewButtonState(false, "Preview");
                SetExportButtonState(false, "Export");
            }
        }

        // Helper method for preview button state:
        private void SetPreviewButtonState(bool isProcessing, string text)
        {
            btnPreview.Enabled = !isProcessing;
            btnPreview.Text = text;
        }

        #endregion

        #region Export Methods

        private async void OnExportClick()
        {
            if (string.IsNullOrEmpty(_previewVideoPath) || !File.Exists(_previewVideoPath))
            {
                MessageBox.Show("No preview video available. Please create a preview first from the Per-Prompt Videos tab.");
                return;
            }

            SetExportButtonState(true, "Exporting...");
            _videoGenerationState.Status = "Exporting";
            _videoGenerationState.Save();

            EventBus.RaiseItemQueued(Guid.Empty);

            try
            {
                string filename = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string tempPath = Path.Combine(_fileService.TempDir, filename + ".mp4");
                string exportPath = Path.Combine(
                    _config.ExportDir,
                    filename + ".mp4");
                string metaPath = Path.Combine(
                    Path.GetDirectoryName(exportPath) ?? "",
                    filename + ".json");

                // Ensure export directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(exportPath) ?? "");

                // Apply upscaling and interpolation to the preview video
                bool success = await VideoUtils.UpscaleAndInterpolateVideoAsync(_previewVideoPath, tempPath);

                if (!success)
                {
                    throw new Exception("Video upscaling and interpolation failed");
                }

                // Export metadata
                await ExportMetadataAsync(GenerateClipInfos(), metaPath);

                // Copy to final location
                File.Copy(tempPath, exportPath, overwrite: true);

                // Cleanup temp file
                File.Delete(tempPath);

                _isExported = true;

                SetExportButtonState(false, "Exported");
                _videoGenerationState.Status = "Exported";
                _videoGenerationState.Save();

                EventBus.RaiseItemQueued(Guid.Empty);

                MessageBox.Show($"Video exported successfully to: {exportPath}");
            }
            catch (Exception ex)
            {
                SetExportButtonState(false, "Export");

                _videoGenerationState.Status = "Failed";
                _videoGenerationState.Save();

                EventBus.RaiseItemQueued(Guid.Empty);

                MessageBox.Show($"Export failed: {ex.Message}");
            }
        }

        private async Task ExportMetadataAsync(List<VideoClipInfo> clipInfos, string metaPath)
        {
            var jsonData = new
            {
                Source = Guid.NewGuid(),
                ClipInfos = clipInfos,
                TransitionType = TRANSITION_TYPE,
                TransitionDurations = _transitionDurations,
                ExportTimestamp = DateTime.Now
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);
            await File.WriteAllTextAsync(metaPath, json);
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
                    _transitionDurations.Add(DEFAULT_TRANSITION_DURATION);
                    _clipSpeeds.Add(DEFAULT_CLIP_SPEED);

                    // Add grid row
                    int rowIndex = dgvPrompts.Rows.Add();
                    dgvPrompts.Rows[rowIndex].Cells["colPrompt"].Value = "Imported Video";
                    dgvPrompts.Rows[rowIndex].Cells["colQueue"].Value = "Imported";

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
            try
            {
                UpdateResolutionLabel();
                UpdateButtonStates();
                RefreshAllRowStatuses();
                RefreshPreviewIfOpen();
            }
            catch (Exception e)
            {
            }
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
            btnQueueAll.Enabled = dgvPrompts.Rows.Count > 0;
            btnExtractLast.Enabled = HasGeneratedVideos() && dgvPrompts.SelectedRows.Count > 0;
            btnPreview.Enabled = HasGeneratedVideos();
            btnImport.Enabled = true;
            btnExport.Enabled = !string.IsNullOrEmpty(_previewVideoPath) && File.Exists(_previewVideoPath);
        }

        private bool HasGeneratedVideos()
        {
            return _perPromptVideoPaths.Any(p => !string.IsNullOrEmpty(p) && File.Exists(p));
        }

        // TODO: Update for Queued statuses
        private void UpdateRowStatus(int rowIndex, string status)
        {
            if (rowIndex >= 0 && rowIndex < dgvPrompts.Rows.Count)
            {
                var row = dgvPrompts.Rows[rowIndex];
                row.Cells["colQueue"].Value = status;

                // Apply color coding for generating status
                var generateCell = row.Cells["colQueue"];
                if (status == "Generating")
                {
                    generateCell.Style.BackColor = Color.LightBlue;
                    generateCell.Style.ForeColor = Color.DarkBlue;
                }

                dgvPrompts.Refresh();

                // After generation completes, update to proper status
                if (status == "Generated")
                {
                    UpdateRowGenerationStatus(rowIndex);
                }
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

        #region Data Refresh

        private void OnClipStatusChanged(Guid clipPK, string newStatus)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Guid, string>(OnClipStatusChanged), clipPK, newStatus);
                return;
            }

            if (_isEditing)
            {
                // Skip refresh during edit (or queue: store pending {clipPK, newStatus}, apply on _isEditing=false)
                return;
            }

            // Reload VideoGenerationState to get latest
            _videoGenerationState = VideoGenerationState.Load(VideoGenerationStatePK);

            foreach (DataGridViewRow row in dgvPrompts.Rows)
            {
                if (row.Tag is Guid rowPK && rowPK == clipPK)
                {
                    // Update status (e.g., "Generated"), video path if new, etc.
                    row.Cells["colQueue"].Value = newStatus;

                    var clip = DB.SelectSingle($"SELECT * FROM ClipGenerationStates WHERE PK = {DB.FormatDBValue(clipPK)}");
                    _perPromptVideoPaths[row.Index] = clip["VideoPath"].ToString();
                    _rowImagePaths[row.Index] = clip["ImagePath"].ToString();
                    var thumbnail = ImageHelper.CreateThumbnail(_rowImagePaths[row.Index], null, 160);
                    dgvPrompts.Rows[row.Index].Cells["colImage"].Value = thumbnail;

                    break;
                }
            }
            UpdateUI(); // Your method
        }

        #endregion

        #region Event Handlers

        private void OnTabChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedIndex == 0) // Generation tab
            {
                // Stop play all and reset highlights
                if (_isPlayingAll)
                {
                    StopPlayAllSequence();
                }

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
                case "colQueue":
                    OnQueueSingleClick(e.RowIndex);
                    break;
            }
        }

        private void HandleImageSelection(int rowIndex)
        {
            using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                // Check if this row was previously generated
                bool wasGenerated = dgvPrompts.Rows[rowIndex].Cells["colQueue"].Value?.ToString() == "Generated";

                _rowImagePaths[rowIndex] = ofd.FileName;
                dgvPrompts.Rows[rowIndex].Cells["colImage"].Value = ImageHelper.CreateThumbnail(ofd.FileName, null, 160);

                // If it was previously generated, mark for regeneration
                if (wasGenerated)
                {
                    _updatingRowStatus = true;
                    try
                    {
                        dgvPrompts.Rows[rowIndex].Cells["colQueue"].Value = "Requeue";
                        var generateCell = dgvPrompts.Rows[rowIndex].Cells["colQueue"];
                        generateCell.Style.BackColor = Color.LightYellow;
                        generateCell.Style.ForeColor = Color.DarkOrange;
                    }
                    finally
                    {
                        _updatingRowStatus = false;
                    }
                }

                UpdateButtonStates();
                UpdateResolutionLabel();
                RefreshPreviewIfOpen();
            }
        }

        private void HandleLoraSelection(int rowIndex)
        {
            MessageBox.Show("LoRA selector coming in Stage 3.");
        }

        private void OnGridValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && !_updatingRowStatus)
            {
                // Mark row as potentially modified if it was previously generated
                var currentStatus = dgvPrompts.Rows[e.RowIndex].Cells["colQueue"].Value?.ToString();
                if (currentStatus == "Generated")
                {
                    _updatingRowStatus = true;
                    try
                    {
                        dgvPrompts.Rows[e.RowIndex].Cells["colQueue"].Value = "Requeue";
                        var generateCell = dgvPrompts.Rows[e.RowIndex].Cells["colQueue"];
                        generateCell.Style.BackColor = Color.LightYellow;
                        generateCell.Style.ForeColor = Color.DarkOrange;
                    }
                    finally
                    {
                        _updatingRowStatus = false;
                    }
                }

                // Only update UI if we're not in the middle of updating row status
                UpdateButtonStates();
                UpdateResolutionLabel();
                RefreshPreviewIfOpen();
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
                row.Cells["colQueue"].Value = "Queue";
            }
            dgvPrompts.Refresh();
        }

        private List<VideoRowData> GetAllRowData()
        {
            return Enumerable.Range(0, dgvPrompts.Rows.Count)
                           .Select(GetRowData)
                           .ToList();
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

        // TODO: I feel like this should come from the ClipGenerationStatus now.
        private void UpdateRowGenerationStatus(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvPrompts.Rows.Count || _updatingRowStatus)
                return;

            _updatingRowStatus = true;
            try
            {
                var row = dgvPrompts.Rows[rowIndex];
                string currentStatus = row.Cells["colQueue"].Value?.ToString() ?? "Queue";

                // Check if video exists for this row
                bool hasVideo = rowIndex < _perPromptVideoPaths.Count &&
                               !string.IsNullOrEmpty(_perPromptVideoPaths[rowIndex]) &&
                               File.Exists(_perPromptVideoPaths[rowIndex]);

                // Check if row data is valid for generation
                bool hasValidData = rowIndex < _rowImagePaths.Count &&
                                   !string.IsNullOrEmpty(_rowImagePaths[rowIndex]) &&
                                   !string.IsNullOrWhiteSpace(row.Cells["colPrompt"].Value?.ToString());

                // Determine appropriate status
                string newStatus;
                if (currentStatus == "Generating")
                {
                    // Don't change if currently generating
                    return;
                }
                else if (currentStatus == "Queued")
                {
                    // Don't change if currently queued
                    return;
                }
                else if (!hasValidData)
                {
                    newStatus = "Queue";
                }
                else if (!hasVideo)
                {
                    newStatus = "Queue";
                }
                else
                {
                    // Has video and valid data - check if it's been modified
                    if (HasRowBeenModifiedSinceGeneration(rowIndex))
                    {
                        newStatus = "Requeue";
                    }
                    else
                    {
                        newStatus = "Generated";
                    }
                }

                // Only update if status has changed
                if (currentStatus != newStatus)
                {
                    // Update button text and color
                    row.Cells["colQueue"].Value = newStatus;

                    // Optional: Change button appearance based on status
                    var generateCell = row.Cells["colQueue"];
                    switch (newStatus)
                    {
                        case "Generated":
                            generateCell.Style.BackColor = Color.LightGreen;
                            generateCell.Style.ForeColor = Color.DarkGreen;
                            break;
                        case "Regenerate":
                            generateCell.Style.BackColor = Color.LightYellow;
                            generateCell.Style.ForeColor = Color.DarkOrange;
                            break;
                        case "Generate":
                        case "Queue":
                        default:
                            generateCell.Style.BackColor = Color.White;
                            generateCell.Style.ForeColor = Color.Black;
                            break;
                    }
                }
            }
            finally
            {
                _updatingRowStatus = false;
            }
        }

        private bool HasRowBeenModifiedSinceGeneration(int rowIndex)
        {
            // This is a simple implementation - you might want to store generation timestamps
            // or checksums for more accurate tracking

            if (rowIndex >= _perPromptVideoPaths.Count || string.IsNullOrEmpty(_perPromptVideoPaths[rowIndex]))
                return false;

            string videoPath = _perPromptVideoPaths[rowIndex];
            if (!File.Exists(videoPath))
                return true; // Video was deleted, so it's effectively modified

            // For now, we'll assume if the video exists and we're calling this method,
            // it might have been modified. In a more robust implementation, you'd track
            // the exact state when generation completed.
            return false;
        }

        private void RefreshAllRowStatuses()
        {
            for (int i = 0; i < dgvPrompts.Rows.Count; i++)
            {
                UpdateRowGenerationStatus(i);
            }
        }

        #endregion

        #region Play All Feature

        private void StartPlayAllSequence()
        {
            if (_isPlayingAll)
            {
                StopPlayAllSequence();
                return;
            }

            _playAllVideos = _perPromptVideoPaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList();

            if (_playAllVideos.Count == 0)
            {
                MessageBox.Show("No videos to play.");
                return;
            }

            _isPlayingAll = true;
            _currentPlayAllIndex = 0;
            btnPlayAll.Text = "Stop All";
            btnPlayAll.BackColor = Color.LightCoral;

            PlayNextVideoInSequence();
        }

        private void StopPlayAllSequence()
        {
            _isPlayingAll = false;

            btnPlayAll.Text = "Play All";
            btnPlayAll.BackColor = SystemColors.Control;

            // Reset all button highlights
            ResetAllVideoButtonHighlights();

            // Stop current video
            videoPlayerPerPrompt?.StopAndHide();
        }

        private async void PlayNextVideoInSequence()
        {
            if (!_isPlayingAll || _currentPlayAllIndex >= _playAllVideos.Count)
            {
                // Sequence complete
                StopPlayAllSequence();
                return;
            }

            string videoPath = _playAllVideos[_currentPlayAllIndex];

            try
            {
                // Update button to show current progress
                btnPlayAll.Text = $"Playing {_currentPlayAllIndex + 1}/{_playAllVideos.Count}";

                // Highlight the current video button
                HighlightCurrentVideoButton(_currentPlayAllIndex);

                // Load and play the video
                LoadVideoInPlayer(videoPlayerPerPrompt, videoPath, ref _perPromptStream, _clipSpeeds[_currentPlayAllIndex]);

                // Get video duration and wait for it to complete
                double videoDuration = VideoUtils.GetVideoDuration(videoPath) / _clipSpeeds[_currentPlayAllIndex];

                // Wait for video to complete (with small buffer)
                await Task.Delay(TimeSpan.FromSeconds(videoDuration + 0.2));

                // Move to next video if still playing all
                if (_isPlayingAll)
                {
                    _currentPlayAllIndex++;
                    PlayNextVideoInSequence();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing video {Path.GetFileName(videoPath)}: {ex.Message}");
                // Skip to next video
                if (_isPlayingAll)
                {
                    _currentPlayAllIndex++;
                    PlayNextVideoInSequence();
                }
            }
        }

        private void HighlightCurrentVideoButton(int videoIndex)
        {
            // Reset previous highlight
            ResetAllVideoButtonHighlights();

            // Find and highlight the current button
            if (videoIndex < _videoButtons.Count && _videoButtons[videoIndex] != null)
            {
                var button = _videoButtons[videoIndex];
                _currentlyHighlightedButton = button;

                // Store original colors if not already stored
                if (button.Tag == null)
                {
                    button.Tag = new ButtonColors
                    {
                        BackColor = button.BackColor,
                        ForeColor = button.ForeColor
                    };
                }

                // Apply highlight colors
                button.BackColor = Color.LightBlue;
                button.ForeColor = Color.DarkBlue;
                button.Font = new Font(button.Font, FontStyle.Bold);
            }
        }

        private void ResetAllVideoButtonHighlights()
        {
            foreach (var button in _videoButtons.Where(b => b != null))
            {
                // Restore original colors
                if (button.Tag is ButtonColors originalColors)
                {
                    button.BackColor = originalColors.BackColor;
                    button.ForeColor = originalColors.ForeColor;
                }
                else
                {
                    // Fallback to default colors
                    button.BackColor = SystemColors.Control;
                    button.ForeColor = SystemColors.ControlText;
                }

                button.Font = new Font(button.Font, FontStyle.Regular);
            }

            _currentlyHighlightedButton = null;
        }

        // Helper class to store original button colors
        private class ButtonColors
        {
            public Color BackColor { get; set; }
            public Color ForeColor { get; set; }
        }

        #endregion

        #region Load / Save state

        void UpdateState()
        {
            if (_videoGenerationState == null)
            {
                _videoGenerationState = VideoGenerationState.New();
                _videoGenerationState.Status = "Queue";
                _videoGenerationStatePK = _videoGenerationState.PK;
            }

            _videoGenerationState.ImagePath = _rowImagePaths.FirstOrDefault() ?? string.Empty; // First clip's image for display

            if (_videoGenerationState.Width == 0 && _videoGenerationState.Height == 0)
            {
                _videoGenerationState.Width = _width;
                _videoGenerationState.Height = _height;
            }

            _videoGenerationState.PreviewPath = _previewVideoPath;
            _videoGenerationState.TempFiles = string.Join(",", _tempFiles);

            for (int i = 0; i < dgvPrompts.Rows.Count; i++)
            {
                var clipState = _videoGenerationState.ClipGenerationStates.FirstOrDefault(c => dgvPrompts.Rows[i].Tag != null && c.PK == Guid.Parse(dgvPrompts.Rows[i].Tag.ToString()));
                if (clipState == null)
                {
                    clipState = ClipGenerationState.New();
                    dgvPrompts.Rows[i].Tag = clipState.PK;
                    _videoGenerationState.ClipGenerationStates.Add(clipState);
                }

                clipState.VideoGenerationStatePK = _videoGenerationStatePK;
                clipState.ImagePath = _rowImagePaths.ElementAtOrDefault(i) ?? string.Empty;
                clipState.VideoPath = _perPromptVideoPaths.ElementAtOrDefault(i) ?? string.Empty;
                clipState.Prompt = dgvPrompts.Rows[i].Cells["colPrompt"].Value?.ToString() ?? string.Empty;
                clipState.WorkflowPath = _rowWorkflows.ElementAtOrDefault(i) ?? string.Empty;
                clipState.Status = dgvPrompts.Rows[i].Cells["colQueue"].Value?.ToString() ?? string.Empty;
                clipState.OrderIndex = i;
            }
        }

        public void LoadState(Guid pk)
        {
            var state = VideoGenerationState.Load(pk);
            _videoGenerationStatePK = pk;

            if (state != null)
            {
                _videoGenerationState = state;
                _width = state.Width;
                _height = state.Height;
                _previewVideoPath = state.PreviewPath;
                _tempFiles = state.TempFiles.Split(',').ToList();

                // Clear grid/lists
                dgvPrompts.Rows.Clear();
                _rowImagePaths.Clear();
                _perPromptVideoPaths.Clear();
                _rowWorkflows.Clear();

                foreach (var clipState in state.ClipGenerationStates)
                {
                    int rowIndex = dgvPrompts.Rows.Add();

                    dgvPrompts.Rows[rowIndex].Tag = clipState.PK;
                    dgvPrompts.Rows[rowIndex].Cells["colPrompt"].Value = clipState.Prompt;
                    dgvPrompts.Rows[rowIndex].Cells["colLora"].Value = "Select LoRA";
                    dgvPrompts.Rows[rowIndex].Cells["colQueue"].Value = clipState.Status;

                    string imgPath = clipState.ImagePath;
                    _rowImagePaths.Add(File.Exists(imgPath) ? imgPath : string.Empty);
                    dgvPrompts.Rows[rowIndex].Cells["colImage"].Value = ImageHelper.CreateThumbnail(imgPath, null, 160);

                    string vidPath = clipState.VideoPath;
                    _perPromptVideoPaths.Add(File.Exists(vidPath) ? vidPath : string.Empty);

                    _rowWorkflows.Add(clipState.WorkflowJson);
                }
            }
        }

        #endregion

        #region Cleanup

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Dispose video players and streams
            videoPlayerFull?.StopAndHide();
            videoPlayerPerPrompt?.StopAndHide();
            _fullStream?.Dispose();
            _perPromptStream?.Dispose();

            EventBus.ClipStatusChanged -= OnClipStatusChanged;

            if (btnExport.Text == "Exporting...")
            {
                e.Cancel = true;
                return;
            }

            if (_videoGenerationState != null && _videoGenerationState.ClipGenerationStates.Any(c => c.Status == "Queued" || c.Status == "Generating"))
            {
                // Video is in the queue. Close without confirmation.
            }
            else
            {
                var action = _isExported ? DialogResult.Yes : MessageBox.Show("Discard?", "Video Generator", MessageBoxButtons.YesNoCancel);
                if (action == DialogResult.Yes)
                {
                    _fileService.CleanupTempFiles(_tempFiles);

                    foreach (var filePath in _rowImagePaths)
                    {
                        if (File.Exists(filePath))
                        {
                            try
                            {
                                File.Delete(filePath);
                            }
                            catch { }
                        }
                    }

                    foreach (var filePath in _perPromptVideoPaths)
                    {
                        if (File.Exists(filePath))
                        {
                            try
                            {
                                File.Delete(filePath);
                            }
                            catch { }
                        }
                    }

                    if (_videoGenerationState != null)
                    {
                        _videoGenerationState.Delete();
                        _videoGenerationState = null;
                    }

                    EventBus.RaiseItemQueued(Guid.Empty);
                }
                else if (action == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

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
        public string TransitionType { get; set; } = "fade"; // Single transition type for all clips
        public List<double> TransitionDurations { get; set; } = new(); // Duration per transition
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