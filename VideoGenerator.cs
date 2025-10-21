using FFMpegCore;
using iviewer.Helpers;
using iviewer.Services;
using iviewer.Video;
using System.Data;

namespace iviewer
{
	public partial class VideoGenerator : Form
	{
		private readonly VideoGenerationService _generationService;
		private readonly FileManagementService _fileService;

		private Guid _videoGenerationStatePK;
		private VideoGenerationState _videoGenerationState;
		private bool _isEditing = false;
		private bool _isExported = false;

		public Guid VideoGenerationStatePK => _videoGenerationStatePK;

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
		private List<ClipGenerationState> _playAllVideos = new List<ClipGenerationState>();
		private List<ClipControl> _videoClipControls = new List<ClipControl>(); // Track video buttons for highlighting
		private Button _currentlyHighlightedButton = null;

		private bool _updatingRowStatus = false;

		public VideoGenerator(Guid? videoGenerationStatePK = null)
		{
			InitializeComponent();
			this.StartPosition = FormStartPosition.CenterScreen;

			var pk = videoGenerationStatePK.HasValue && videoGenerationStatePK.Value != Guid.Empty ? videoGenerationStatePK.Value : Guid.NewGuid();
			_videoGenerationStatePK = pk;

			_generationService = new VideoGenerationService();
			_fileService = new FileManagementService();

			//InitializeGrid(videoGenerationStatePK == null);
			InitializeGrid();
			SetupEventHandlers();
			InitializeVideoPlayers();
			UpdateClipStates();
		}

		private void SetupEventHandlers()
		{
			tabControl.SelectedIndexChanged += OnTabChanged;
			dgvPrompts.CellClick += OnGridCellClick;
			dgvPrompts.CellValueChanged += OnGridValueChanged;
			dgvPrompts.EditingControlShowing += OnGridEditingControlShowing;

			EventBus.ClipStatusChanged += OnClipStatusChanged;
			EventBus.VideoStatusChanged += OnVideoStatusChanged;

			// Grid events for edit mode
			dgvPrompts.CellBeginEdit += (s, e) => _isEditing = true;
			dgvPrompts.CellEndEdit += (s, e) => _isEditing = false;
		}

		#region Generation Tab Methods

		private void InitializeGrid()
		{
			GridInitializer.Initialize(dgvPrompts);

			//if (addNewRow)
			//{
			//    UpdateClipStates();
			//    AddNewRow();
			//}
			//else
			//{
			//    // Set initial status
			//    UpdateClipStates();
			//    UpdateUI();
			//}
		}

		private void AddNewRow()
		{
			int insertIndex;
			if (dgvPrompts.SelectedRows.Count > 0)
			{
				insertIndex = dgvPrompts.SelectedRows[0].Index + 1;
			}
			else
			{
				insertIndex = dgvPrompts.Rows.Count; // Add at end if no selection
			}

			UpdateClipStates();

			var clipGenerationState = ClipGenerationState.New();
			clipGenerationState.Prompt = GetDefaultOrPreviousPrompt();

			// Insert new row in grid
			dgvPrompts.Rows.Insert(insertIndex);

			dgvPrompts.Rows[insertIndex].Tag = clipGenerationState;
			PopulateGridRow(insertIndex);

			_videoGenerationState.ClipGenerationStates.Add(clipGenerationState);

			// Set initial status
			UpdateRowGenerationStatus(insertIndex);
			UpdateClipStates();
			UpdateUI();

			// Select the new row
			dgvPrompts.ClearSelection();
			dgvPrompts.Rows[insertIndex].Selected = true;
			dgvPrompts.CurrentCell = dgvPrompts.Rows[insertIndex].Cells["colPrompt"];
		}

		void OnMoveUp()
		{
			int rowIndex;
			if (dgvPrompts.SelectedRows.Count > 0)
			{
				rowIndex = dgvPrompts.SelectedRows[0].Index;
			}
			else
			{
				return;
			}

			if (rowIndex == 0)
			{
				return;
			}

			UpdateClipStateFromRow(rowIndex);
			UpdateClipStateFromRow(rowIndex - 1);

			var clip1 = RowClipState(rowIndex);
			var clip2 = RowClipState(rowIndex - 1);

			clip1.OrderIndex = rowIndex - 1;
			clip2.OrderIndex = rowIndex;

			dgvPrompts.Rows[clip1.OrderIndex].Tag = clip1;
			dgvPrompts.Rows[clip2.OrderIndex].Tag = clip2;

			PopulateGridRow(rowIndex - 1);
			PopulateGridRow(rowIndex);

			_videoGenerationState.Save();

			UpdateUI();

			dgvPrompts.ClearSelection();
			dgvPrompts.Rows[rowIndex - 1].Selected = true;
			dgvPrompts.CurrentCell = dgvPrompts.Rows[rowIndex - 1].Cells["colPrompt"];
		}

		void OnMoveDown()
		{
			int rowIndex;
			if (dgvPrompts.SelectedRows.Count > 0)
			{
				rowIndex = dgvPrompts.SelectedRows[0].Index;
			}
			else
			{
				return;
			}

			if (rowIndex >= dgvPrompts.RowCount - 1)
			{
				return;
			}

			UpdateClipStateFromRow(rowIndex);
			UpdateClipStateFromRow(rowIndex + 1);

			var clip1 = RowClipState(rowIndex);
			var clip2 = RowClipState(rowIndex + 1);

			clip1.OrderIndex = rowIndex + 1;
			clip2.OrderIndex = rowIndex;

			dgvPrompts.Rows[clip1.OrderIndex].Tag = clip1;
			dgvPrompts.Rows[clip2.OrderIndex].Tag = clip2;

			PopulateGridRow(rowIndex);
			PopulateGridRow(rowIndex + 1);

			_videoGenerationState.Save();

			UpdateUI();

			dgvPrompts.ClearSelection();
			dgvPrompts.Rows[rowIndex + 1].Selected = true;
			dgvPrompts.CurrentCell = dgvPrompts.Rows[rowIndex + 1].Cells["colPrompt"];
		}

		private string GetDefaultOrPreviousPrompt()
		{
			if (dgvPrompts.SelectedRows.Count > 0)
			{
				return dgvPrompts.SelectedRows[0].Cells["colPrompt"].Value?.ToString() ?? "";
			}
			//else if (dgvPrompts.RowCount > 1)
			//{
			//    return dgvPrompts.Rows[dgvPrompts.RowCount - 1].Cells["colPrompt"].Value?.ToString() ?? "";
			//}

			return "A woman posing for a photoshoot. She smiles and sways her body. The camera remains static throughout. The lighting remains consistent throughout. The background remains static throughout.";
		}

		private void PopulateGridRow(int rowIndex)
		{
			var row = dgvPrompts.Rows[rowIndex];
			var state = RowClipState(row);

			row.Cells["colPrompt"].Value = state.Prompt;
			row.Cells["colLora"].Value = "Select LoRA";
			row.Cells["colQueue"].Value = "Queue";

			if (!string.IsNullOrEmpty(state.ImagePath))
			{
				row.Cells["colImage"].Value = ImageHelper.CreateThumbnail(state.ImagePath, null, 160);
			}
			else if (rowIndex > 0 && RowClipState(rowIndex - 1).VideoPath != "")
			{
				string framePath = VideoUtils.ExtractLastFrame(RowClipState(rowIndex - 1).VideoPath, _videoGenerationState.TempDir, true);
				if (!string.IsNullOrEmpty(framePath) && File.Exists(framePath))
				{
					RowClipState(rowIndex).ImagePath = framePath;
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
			UpdateClipStates();

			//var rowData = RowData(rowIndex);
			//if (!ValidateRowData(rowData))
			if (!ValidateRow(dgvPrompts.Rows[rowIndex]))
			{
				MessageBox.Show("Select an image and enter a prompt for this row.");
				return;
			}

			UpdateRowStatus(rowIndex, "Queuing");

			var allRowData = VideoRows;
			if (_width == 0 || _height == 0)
			{
				(_width, _height) = ResolutionCalculator.Calculate(allRowData.First().ImagePath);
			}

			UpdateClipStates();

			_generationService.QueueClips(_videoGenerationState);

			LoadState(false);
			UpdateUI();

			QueryStartQueue();
		}

		private async void OnQueueAllClick()
		{
			if (!ValidateGenerateAll()) return;

			if (_videoGenerationState.ClipGenerationStates.Count != dgvPrompts.Rows.Count)
			{
				// Why??
			}

			try
			{
				var allRowData = VideoRows;
				if (_width == 0 || _height == 0)
				{
					(_width, _height) = ResolutionCalculator.Calculate(allRowData.First().ImagePath);
				}

				for (var rowIndex = 0; rowIndex < dgvPrompts.RowCount; rowIndex++)
				{
					var row = dgvPrompts.Rows[rowIndex];
					if (row.Cells["colQueue"].Value.Equals("Queue") && row.Cells["colPrompt"].Value != null)
					{
						UpdateRowStatus(rowIndex, "Queuing");
					}
				}

				UpdateClipStates();

				_generationService.QueueClips(_videoGenerationState);

				LoadState(false);
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

		private void DeleteSelectedRow()
		{
			if (dgvPrompts.SelectedRows.Count > 0)
			{
				int rowIndex = dgvPrompts.SelectedRows[0].Index;

				var clip = RowClipState(rowIndex);
				var pk = clip.PK;

				foreach (var videoClip in _videoGenerationState.ClipGenerationStates)
				{
					if (videoClip.PK == pk)
					{
						_videoGenerationState.ClipGenerationStates.Remove(videoClip);
						break;
					}
				}

				// Remove row
				dgvPrompts.Rows.RemoveAt(rowIndex);

				clip.Delete();
				_videoGenerationState.Save();

				EventBus.RaiseClipDeleted(pk, _videoGenerationStatePK);

				UpdateClipStates();
				UpdateUI();
			}
		}

		void OnDeleteImage()
		{
			if (dgvPrompts.SelectedRows.Count == 0) return;

			int rowIndex = dgvPrompts.SelectedRows[0].Index;
			dgvPrompts.Rows[rowIndex].Cells["colImage"].Value = null;

			var clip = RowClipState(rowIndex);
			clip.ImagePath = String.Empty;
			clip.Save();

			EventBus.RaiseClipStatusChanged(clip.PK, clip.VideoGenerationStatePK, clip.Status);
		}

		private void ExtractLastFrameFromSelected()
		{
			if (dgvPrompts.SelectedRows.Count == 0) return;

			int rowIndex = dgvPrompts.SelectedRows[0].Index;
			string videoPath = RowClipState(rowIndex).VideoPath;

			if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
			{
				MessageBox.Show("No video file found for selected row.");
				return;
			}

			try
			{
				string framePath = VideoUtils.ExtractLastFrame(videoPath, _videoGenerationState.TempDir, true);
				if (!string.IsNullOrEmpty(framePath) && File.Exists(framePath))
				{
					// Determine target row (next row if exists, otherwise current)
					int targetRow = rowIndex + 1 < dgvPrompts.Rows.Count ? rowIndex + 1 : rowIndex;

					RowClipState(targetRow).ImagePath = framePath;
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
				LoadVideoInPlayer(videoPlayerFull, _previewVideoPath, ref _fullStream, loop: true);
			}

			// Load Per-Prompt Videos tab
			LoadPerPromptVideosTab();
		}

		private void LoadPerPromptVideosTab()
		{
			_videoClipControls.Clear();

			var flowPanel = CreateVideoButtonsPanel(
				OnVideoButtonClick,
				OnTransitionChanged,
				_videoClipControls);

			// Configure flow panel to not overlap buttons
			flowPanel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
			flowPanel.Location = new Point(3, tabPagePerPrompt.Height - 395);
			flowPanel.Size = new Size(Width - 6, 395);
			flowPanel.AutoScroll = true;

			// Clear existing dynamic controls but keep the buttons and video player
			var controlsToRemove = tabPagePerPrompt.Controls.OfType<Control>()
				.Where(c => c != videoPlayerPerPrompt && c != btnPreview && c != btnImport && c != btnPlayAll && c != btnPlay2x)
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
			btnPlay2x.BringToFront();

			// Adjust video player size to accommodate flow panel
			videoPlayerPerPrompt.Size = new Size(btnPreview.Left - 10, tabPagePerPrompt.Height - 400);

			CheckStates();
		}

		FlowLayoutPanel CreateVideoButtonsPanel(
		   //List<VideoRowData> videoRows,
		   //List<VideoClipInfo> clipInfos,
		   Action<int, string, double> onVideoClick,
		   Action<int, double, string, double, int, int> onTransitionChanged,
		   List<ClipControl> clipControls = null)
		{
			var flowPanel = new FlowLayoutPanel
			{
				Name = "FlowPanel",
				Dock = DockStyle.Bottom,
				Height = 395,
				AutoScroll = true,
				FlowDirection = FlowDirection.LeftToRight
			};

			for (int i = 0; i < dgvPrompts.RowCount; i++)
			{
				var state = RowClipState(i);
				int currentIndex = i; // Capture for closure

				var clipControl = new ClipControl();
				clipControl.Enabled = false;
				clipControl.btnClip.Text = $"Clip {i + 1}";
				clipControl.cboEffect.Text = state.TransitionType;
				clipControl.txtAddFrames.Text = state.TransitionAddFrames.ToString();
				clipControl.txtDropFrames.Text = state.TransitionDropFrames.ToString();
				clipControl.txtLength.Text = state.TransitionDuration.ToString();
				clipControl.txtSpeed.Text = state.ClipSpeed.ToString();
				clipControl.Tag = i;

				clipControl.Enabled = File.Exists(state.VideoPath);

				flowPanel.Controls.Add(clipControl);

				// Add to tracker if provided
				clipControls?.Add(clipControl);

				// Common change handler
				EventHandler changeHandler = (sender, e) =>
				{
					bool isValid = true;

					// Validate speed (clip-level, but include since it's per row)
					if (!double.TryParse(clipControl.txtSpeed.Text, out double speed) || speed <= 0 || speed > 5)
					{
						clipControl.txtSpeed.BackColor = Color.LightPink;
						isValid = false;
					}
					else
					{
						clipControl.txtSpeed.BackColor = Color.White;
					}

					// Validate transition type
					string transitionType = clipControl.cboEffect.SelectedItem?.ToString() ?? "None";
					// Add any validation if needed, e.g., if (string.IsNullOrEmpty(transitionType)) isValid = false;

					// Validate duration
					if (!double.TryParse(clipControl.txtLength.Text, out double duration) || duration < 0 || duration > 5)
					{
						clipControl.txtLength.BackColor = Color.LightPink;
						isValid = false;
					}
					else
					{
						clipControl.txtLength.BackColor = Color.White;
					}

					// Validate drop frames
					if (!int.TryParse(clipControl.txtDropFrames.Text, out int dropFrames) || dropFrames < 0)
					{
						clipControl.txtDropFrames.BackColor = Color.LightPink;
						isValid = false;
					}
					else
					{
						clipControl.txtDropFrames.BackColor = Color.White;
					}

					// Validate add frames
					if (!int.TryParse(clipControl.txtAddFrames.Text, out int addFrames) || addFrames < 0)
					{
						clipControl.txtAddFrames.BackColor = Color.LightPink;
						isValid = false;
					}
					else
					{
						clipControl.txtAddFrames.BackColor = Color.White;
					}

					clipControl.EnableTransitionControls();

					if (isValid)
					{
						onTransitionChanged(currentIndex, speed, transitionType, duration, dropFrames, addFrames);
					}
				};

				// Wire up events
				clipControl.txtSpeed.TextChanged += changeHandler;
				clipControl.cboEffect.SelectedIndexChanged += changeHandler;
				clipControl.txtLength.TextChanged += changeHandler;
				clipControl.txtDropFrames.TextChanged += changeHandler;
				clipControl.txtAddFrames.TextChanged += changeHandler;

				if (i == dgvPrompts.RowCount - 1)
				{
					clipControl.LastClip = true;
				}

				if (File.Exists(state.VideoPath))
				{
					clipControl.btnClip.Click += (s, e) => onVideoClick(currentIndex, state.VideoPath, state.ClipSpeed);
					clipControl.Enabled = true;
				}
				else
				{
					clipControl.Enabled = false;
				}

				clipControl.EnableTransitionControls();
			}

			return flowPanel;
		}

		private void OnTransitionChanged(int clipIndex, double speed, string transitionType, double duration, int dropFrames, int addFrames)
		{
			if (clipIndex >= 0)
			{
				var state = RowClipState(clipIndex);
				state.ClipSpeed = speed;
				state.TransitionType = transitionType;
				state.TransitionDuration = duration;
				state.TransitionDropFrames = dropFrames;
				state.TransitionAddFrames = addFrames;
			}
		}

		private void LoadVideoInPlayer(VideoPlayerControl player, string videoPath, ref FileStream streamRef, double speed = 1, bool loop = false)
		{
			try
			{
				if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
				{
					return;
				}

				player.Play(videoPath, speed, loop);

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
			if (videoIndex < _videoClipControls.Count && _videoClipControls[videoIndex] != null)
			{
				var button = _videoClipControls[videoIndex];
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
				// Ensure states are set correctly
				CheckStates();

				_videoGenerationState.Save();

				_previewVideoPath = await _generationService.StitchVideos(_videoGenerationState, false);

				if (!string.IsNullOrEmpty(_previewVideoPath))
				{
					_videoGenerationState.PreviewPath = _previewVideoPath;
					_tempFiles.Add(_previewVideoPath); // Track for cleanup

					_videoGenerationState.Save();

					// Switch to Full Video tab and load the preview
					tabControl.SelectedIndex = 2; // Full Video tab
					LoadVideoInPlayer(videoPlayerFull, _previewVideoPath, ref _fullStream, loop: true);
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

		// Debug method to check states
		void CheckStates()
		{
			for (var i = 0; i < dgvPrompts.Rows.Count; i++)
			{
				var rowClip = RowClipState(i);
				var videoClip = _videoGenerationState.ClipGenerationStates.FirstOrDefault(c => c.OrderIndex == i);
				if (!object.ReferenceEquals(rowClip, videoClip))
				{
					// Why are they different?
					//throw new Exception("Row clip state != Video clip state");
				}

				var row = dgvPrompts.Rows[i];
				if ((row.Cells["colPrompt"].Value != null && row.Cells["colPrompt"].Value != rowClip.Prompt)
					|| (row.Cells["colQueue"].Value != "Requeue" && rowClip.Status != "Requeue" && !string.Equals(row.Cells["colQueue"].Value.ToString(), rowClip.Status, StringComparison.OrdinalIgnoreCase)))
				{
					//throw new Exception("Row != Clip");
				}

				if (tabControl.SelectedIndex == 1)
				{
					var flowPanel = tabPagePerPrompt.Controls.Find("FlowPanel", true)[0] as FlowLayoutPanel;
					var clipControl = flowPanel.Controls.OfType<ClipControl>().FirstOrDefault(c => (int)c.Tag == i);
					if (clipControl.txtAddFrames.Text != rowClip.TransitionAddFrames.ToString()
						|| clipControl.cboEffect.Text != rowClip.TransitionType.ToString()
						|| clipControl.txtDropFrames.Text != rowClip.TransitionDropFrames.ToString()
						|| clipControl.txtLength.Text != rowClip.TransitionDuration.ToString()
						|| clipControl.txtSpeed.Text != rowClip.ClipSpeed.ToString())
					{
						throw new Exception("ClipControl != Clip");
					}
				}
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
			_videoGenerationState.Status = "Export";
			_videoGenerationState.Save();

			EventBus.RaiseVideoStatusChanged(_videoGenerationStatePK, _videoGenerationState.Status);

			SetExportButtonState(true, "Queued");

			QueryStartQueue();

			Close();
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

					var clipGenerationState = ClipGenerationState.New();
					clipGenerationState.VideoPath = imported[i];
					clipGenerationState.Prompt = "Imported Video";

					_videoGenerationState.ClipGenerationStates.Add(clipGenerationState);

					dgvPrompts.Rows[rowIndex].Tag = clipGenerationState;
				}

				_videoGenerationState.Save();

				UpdateUI();

				CheckStates();

				MessageBox.Show($"{imported.Count} videos imported. Total videos: {VideoRows.Count()}");
			}
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
			if (!string.IsNullOrEmpty(RowClipState(0)?.ImagePath))
			{
				var (width, height) = ResolutionCalculator.Calculate(RowClipState(0).ImagePath);
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
			return VideoRows.Any(p => !string.IsNullOrEmpty(p.VideoPath) && File.Exists(p.VideoPath));
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

		private void OnVideoStatusChanged(Guid videoPK, string status)
		{
			if (videoPK != _videoGenerationStatePK)
			{
				return;
			}

			if (status == "Exported")
			{
				_isExported = true;
			}

			var exportButtonText = status;
			var enabled = true;
			if (status == "Export")
			{
				exportButtonText = "Queued";
				enabled = false;
			}
			else if (status == "Queued" || status == "Generating" || status == "Generated")
			{
				exportButtonText = "Export";
				enabled = false;
			}
			else if (status == "Generated")
			{
				exportButtonText = "Export";
				enabled = true;
			}

			SetExportButtonState(enabled, exportButtonText);
		}

		private void OnClipStatusChanged(Guid clipPK, Guid videoPK, string newStatus)
		{
			if (InvokeRequired)
			{
				Invoke(new Action<Guid, Guid, string>(OnClipStatusChanged), clipPK, videoPK, newStatus);
				return;
			}

			if (videoPK != _videoGenerationStatePK)
			{
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
				var clipState = RowClipState(row);

				if (clipState.PK == clipPK)
				{
					// Update status (e.g., "Generated"), video path if new, etc.
					row.Cells["colQueue"].Value = newStatus;

					// TODO: I assume we can reload like this to get latest
					clipState.LoadFromSql($"SELECT * FROM ClipGenerationStates WHERE PK = {DB.FormatDBValue(clipPK)}");

					var thumbnail = ImageHelper.CreateThumbnail(clipState.ImagePath, null, 160);
					dgvPrompts.Rows[row.Index].Cells["colImage"].Value = thumbnail;
					dgvPrompts.Rows[row.Index].Cells["colPrompt"].Value = clipState.Prompt;

					break;
				}
			}

			UpdateUI();
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
			var startDir = _videoGenerationState.ClipGenerationStates.Any(c => !string.IsNullOrEmpty(c.ImagePath))
				? _videoGenerationState.TempDir
				: VideoGenerationConfig.ImportDir;

			// Check if this row was previously generated
			bool wasGenerated = dgvPrompts.Rows[rowIndex].Cells["colQueue"].Value?.ToString() == "Generated";
			string filePath = "";

			using var ofd = new OpenFileDialog { Filter = "Media|*.png;*.jpg;*.jpeg;*.mp4", InitialDirectory = startDir };
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				if (ofd.FileName.StartsWith(_videoGenerationState.TempDir, StringComparison.OrdinalIgnoreCase))
				{
					filePath = ofd.FileName;
				}
				else
				{
					filePath = Path.Combine(_videoGenerationState.TempDir, Path.GetFileName(ofd.FileName));
					if (File.Exists(filePath))
					{
						filePath = Path.Combine(_videoGenerationState.TempDir, Guid.NewGuid().ToString() + Path.GetExtension(filePath));
					}

					File.Move(ofd.FileName, filePath);

					// Is there a metadata file? If so, we want to add it to temp files for deletion later.
					var metadataFile = Path.Combine(Path.GetDirectoryName(ofd.FileName), Path.GetFileNameWithoutExtension(ofd.FileName) + ".txt");
					if (File.Exists(metadataFile))
					{
						File.Move(metadataFile, Path.Combine(_videoGenerationState.TempDir, Path.GetFileName(metadataFile)));
					}
				}
			}
			else
			{
				return;
			}

			var imagePath = "";
			if (Path.GetExtension(filePath).ToLower() == ".mp4")
			{
				// Extract first frame for video
				imagePath = VideoUtils.ExtractFirstFrame(ofd.FileName, VideoGenerationConfig.TempFileDir, true);
				_tempFiles.Add(imagePath);
				RowClipState(rowIndex).VideoPath = ofd.FileName;
				wasGenerated = true;
			}
			else
			{
				imagePath = filePath;
			}

			dgvPrompts.Rows[rowIndex].Cells["colImage"].Value = ImageHelper.CreateThumbnail(imagePath, null, 160);
			RowClipState(rowIndex).ImagePath = imagePath;

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

		private bool ValidateRow(DataGridViewRow row)
		{
			var state = RowClipState(row);
			var isValid = !string.IsNullOrEmpty(state.ImagePath) && !string.IsNullOrEmpty(state.Prompt);
			if (!isValid)
			{
				var found = false;

				for (var i = dgvPrompts.Rows.Count - 1; i >= 0; i--)
				{
					row = dgvPrompts.Rows[i];

					if (found)
					{
						// Row is valid if the row above it is in the queue
						return dgvPrompts.Rows[i].Cells["colQueue"].Value.Equals("Queued") || dgvPrompts.Rows[i].Cells["colQueue"].Value.Equals("Generating");
					}
					else if (RowClipState(i).PK == state.PK)
					{
						found = true;
					}
				}
			}

			return isValid;
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
				bool hasVideo = File.Exists(RowClipState(rowIndex).VideoPath);

				// Check if row data is valid for generation
				bool hasValidData = rowIndex < dgvPrompts.Rows.Count &&
								   !string.IsNullOrEmpty(RowClipState(rowIndex).ImagePath) &&
								   !string.IsNullOrWhiteSpace(row.Cells["colPrompt"].Value?.ToString());

				// Determine appropriate status
				string newStatus;
				if (currentStatus == "Generating" || currentStatus == "Queued" || currentStatus == "Failed")
				{
					// Don't change
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
			string videoPath = RowClipState(rowIndex).VideoPath;
			if (!File.Exists(videoPath))
				return true; // Video was deleted, so it's effectively modified

			return RowClipState(rowIndex).HasChanges;
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

		private void StartPlayAllSequence(double speedFactor = 1.0)
		{
			if (_isPlayingAll)
			{
				StopPlayAllSequence();
				return;
			}

			if (!GeneratedRows.Any())
			{
				return;
			}

			_playAllVideos.Clear();
			foreach (DataGridViewRow row in dgvPrompts.Rows)
			{
				_playAllVideos.Add(RowClipState(row));
			}

			_isPlayingAll = true;
			_currentPlayAllIndex = 0;
			btnPlayAll.Text = "Stop All";
			btnPlayAll.BackColor = Color.LightCoral;

			PlayNextVideoInSequence(speedFactor, null);
		}

		private void StopPlayAllSequence()
		{
			_isPlayingAll = false;

			btnPlayAll.Text = "Play All";
			btnPlayAll.BackColor = SystemColors.Control;

			// Reset all button highlights
			ResetAllVideoButtonHighlights();

			// Stop current video
			//videoPlayerPerPrompt?.StopAndHide();
		}

		private async void PlayNextVideoInSequence(double speedFactor, Task<IMediaAnalysis> infoTask)
		{
			if (!_isPlayingAll || _currentPlayAllIndex >= _playAllVideos.Count)
			{
				// Sequence complete
				StopPlayAllSequence();
				return;
			}

			var clipState = _playAllVideos[_currentPlayAllIndex];

			try
			{
				if (File.Exists(clipState.VideoPath))
				{
					// Update button to show current progress
					btnPlayAll.Text = $"Playing {_currentPlayAllIndex + 1}/{_playAllVideos.Count}";

					// Highlight the current video button
					HighlightCurrentVideoButton(_currentPlayAllIndex);

					// Load and play the video
					LoadVideoInPlayer(videoPlayerPerPrompt, clipState.VideoPath, ref _perPromptStream, clipState.ClipSpeed * speedFactor);

					// Get video duration and wait for it to complete
					var info = (IMediaAnalysis)null;
					if (infoTask != null)
					{
						info = await infoTask;
					}
					else
					{
						info = FFProbe.Analyse(clipState.VideoPath);
					}

					var frameTime = 1.0 / info.PrimaryVideoStream.FrameRate;
					var frameAdjustment = Math.Max(0, (frameTime * clipState.TransitionDropFrames) + (frameTime * clipState.TransitionAddFrames));
					double videoDuration = (info.Duration.TotalSeconds - frameAdjustment) / (clipState.ClipSpeed * speedFactor);

					// Create task to get info for next video
					var nextClipState = _playAllVideos.Count > _currentPlayAllIndex + 1 ? _playAllVideos[_currentPlayAllIndex + 1] : null;
					if (nextClipState != null && File.Exists(nextClipState.VideoPath))
					{
						infoTask = FFProbe.AnalyseAsync(nextClipState.VideoPath);
					}

					await Task.Delay(TimeSpan.FromSeconds(videoDuration));
				}

				// Move to next video if still playing all
				if (_isPlayingAll)
				{
					_currentPlayAllIndex++;
					PlayNextVideoInSequence(speedFactor, infoTask);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error playing video {Path.GetFileName(clipState.VideoPath)}: {ex.Message}");
				// Skip to next video
				if (_isPlayingAll)
				{
					_currentPlayAllIndex++;
					PlayNextVideoInSequence(speedFactor, null);
				}
			}
		}

		private void HighlightCurrentVideoButton(int videoIndex)
		{
			// Reset previous highlight
			ResetAllVideoButtonHighlights();

			// Find and highlight the current button
			if (videoIndex < _videoClipControls.Count && _videoClipControls[videoIndex] != null)
			{
				var button = _videoClipControls[videoIndex].btnClip;
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
			foreach (var clipControl in _videoClipControls.Where(b => b != null))
			{
				var button = clipControl.btnClip;

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

		void UpdateClipStates()
		{
			if (_videoGenerationState == null)
			{
				if (_videoGenerationState == null || _videoGenerationState.PK != _videoGenerationStatePK)
				{
					_videoGenerationState = VideoGenerationState.Load(_videoGenerationStatePK);
					if (_videoGenerationState != null)
					{
						LoadState();
					}
				}

				if (_videoGenerationState == null)
				{
					_videoGenerationState = VideoGenerationState.New();
					_videoGenerationState.Status = "Queue";
					_videoGenerationStatePK = _videoGenerationState.PK;
					_videoGenerationState.Save();
				}
			}

			foreach (DataGridViewRow row in dgvPrompts.Rows)
			{
				if (RowClipState(row).ImagePath != "")
				{
					_videoGenerationState.ImagePath = RowClipState(row).ImagePath ?? string.Empty; // First clip's image for display
					break;
				}
			}

			if (_videoGenerationState.Width == 0 && _videoGenerationState.Height == 0)
			{
				_videoGenerationState.Width = _width;
				_videoGenerationState.Height = _height;
			}

			_videoGenerationState.PreviewPath = _previewVideoPath;
			_videoGenerationState.TempFiles = string.Join(",", _tempFiles);

			UpdateClipStatesFromRows();

			_videoGenerationState.Save();
		}

		void UpdateClipStatesFromRows()
		{
			for (int i = 0; i < dgvPrompts.Rows.Count; i++)
			{
				UpdateClipStateFromRow(i);
			}
		}

		private void UpdateClipStateFromRow(int i)
		{
			var clipState = RowClipState(i);

			clipState.VideoGenerationStatePK = _videoGenerationStatePK;
			clipState.Prompt = dgvPrompts.Rows[i].Cells["colPrompt"].Value?.ToString() ?? string.Empty;
			clipState.Status = dgvPrompts.Rows[i].Cells["colQueue"].Value?.ToString() ?? string.Empty;
			clipState.OrderIndex = i;

			clipState.Save();
		}

		public void LoadState(bool reloadRows = true)
		{
			if (_videoGenerationState == null)
			{
				// ??
			}

			_width = _videoGenerationState.Width;
			_height = _videoGenerationState.Height;
			_previewVideoPath = _videoGenerationState.PreviewPath;
			_tempFiles = _videoGenerationState.TempFiles.Split(',').ToList();

			// Clear grid/lists
			if (reloadRows)
			{
				dgvPrompts.Rows.Clear();

				VideoGenerationState.Load(_videoGenerationStatePK);

				for (var rowIndex = 0; rowIndex < _videoGenerationState.ClipGenerationStates.Count; rowIndex++)
				{
					var clipState = _videoGenerationState.ClipGenerationStates[rowIndex];

					string imgPath = clipState.ImagePath;
					string vidPath = clipState.VideoPath;

					_tempFiles.Add(imgPath);
					_tempFiles.Add(vidPath);

					dgvPrompts.Rows.Add();
					dgvPrompts.Rows[rowIndex].Tag = clipState;

					dgvPrompts.Rows[rowIndex].Cells["colImage"].Value = ImageHelper.CreateThumbnail(imgPath, null, 160);
					dgvPrompts.Rows[rowIndex].Cells["colPrompt"].Value = clipState.Prompt;
					dgvPrompts.Rows[rowIndex].Cells["colLora"].Value = "Select LoRA";
					dgvPrompts.Rows[rowIndex].Cells["colQueue"].Value = clipState.Status;
				}
			}

			CheckStates();
		}

		#endregion

		#region Cleanup

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (_isPlayingAll)
			{
				StopPlayAllSequence();
			}

			// Dispose video players and streams
			videoPlayerFull?.StopAndHide();
			videoPlayerPerPrompt?.StopAndHide();
			_fullStream?.Dispose();
			_perPromptStream?.Dispose();

			EventBus.ClipStatusChanged -= OnClipStatusChanged;
			EventBus.VideoStatusChanged -= OnVideoStatusChanged;

			if (btnExport.Text == "Exporting...")
			{
				e.Cancel = true;
				return;
			}

			if (_videoGenerationState.Status == "Export" || _videoGenerationState.Status == "Exporting")
			{
				// Just close without saving.
			}
			else if (_videoGenerationState.ClipGenerationStates.Any(c => c.Status == "Queued" || c.Status == "Generating"))
			{
				// Video is in the queue. Close without confirmation.
				// TODO: Not sure this works for RowData.
				// Also, what about saving if any row has changes?
				_videoGenerationState.Save();
			}
			else
			{
				var action = MessageBox.Show("Discard?", "Video Generator", MessageBoxButtons.YesNoCancel);
				if (action == DialogResult.Yes)
				{
					_generationService.DeleteAndCleanUp(_videoGenerationState);
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

		#region RowData

		ClipGenerationState RowClipState(int index) => dgvPrompts.RowCount > 0 ? (ClipGenerationState)dgvPrompts.Rows[index].Tag : null;

		ClipGenerationState RowClipState(DataGridViewRow row) => (ClipGenerationState)row.Tag;

		IEnumerable<ClipGenerationState> GeneratedRows
		{
			get
			{
				foreach (DataGridViewRow row in dgvPrompts.Rows)
				{
					if (File.Exists(RowClipState(row).VideoPath))
					{
						yield return RowClipState(row);
					}
				}
			}
		}

		IEnumerable<ClipGenerationState> VideoRows
		{
			get
			{
				foreach (DataGridViewRow row in dgvPrompts.Rows)
				{
					yield return RowClipState(row);
				}
			}
		}

		#endregion
	}

	#region Data Classes

	public static class VideoGenerationConfig
	{
		public static string ComfyOutputDir => @"C:\Users\sysadmin\Documents\ComfyUI\output\iviewer\temp_output";
		public static string TempFileDir => @"C:\Users\sysadmin\Documents\ComfyUI\output\iviewer\temp_files";
		public static string WorkingDir => @"C:\Users\sysadmin\Documents\ComfyUI\output\iviewer";
		public static string I2vWorkflowPath => @"C:\Users\sysadmin\Documents\ComfyUI\user\default\workflows\iviewer\Wan22 i2v - API.json";
		public static string I2vWorkflowFileStem => "comfyui_video";
		public static string FlfWorkflowPath => @"C:\Users\sysadmin\Documents\ComfyUI\user\default\workflows\iviewer\Wan22 flf - API.json";
		public static string FlfWorkflowFileStem => "comfyui_video";
		public static string InterpolateWorkflowPath => @"C:\Users\sysadmin\Documents\ComfyUI\user\default\workflows\iviewer\Interpolate - API.json";
		public static string InterpolateWorkflowFileStem => "interpolated_";
		public static string UpscaleWorkflowPath => @"C:\Users\sysadmin\Documents\ComfyUI\user\default\workflows\iviewer\Upscale - API.json";
		public static string UpscaleWorkflowFileStem => "upscaled_";
		public static string ImportDir => @"D:\Users\sysadmin\Archive\Visual Studio Projects\SD-Process";
		public static string ExportDir => @"D:\Users\sysadmin\Archive\Visual Studio Projects\SD-Processed";
	}

	#endregion
}