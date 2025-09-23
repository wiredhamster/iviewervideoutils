using Newtonsoft.Json;

namespace iviewer
{
    public partial class VideoPreviewForm : Form
    {
        private List<string> perPromptVideos;
        private List<VideoClipInfo> clipInfos;
        private Action<int> regenCallback;
        private FileStream fullStream;
        private FileStream perPromptStream;

        private string outputDir = @"C:\Users\sysadmin\Documents\ComfyUI\output\iviewer\exported";

        public VideoPreviewForm(string fullVideoPath, List<string> perPromptVideos, List<VideoClipInfo> clipInfos, Action<int> regenCallback)
        {
            this.perPromptVideos = perPromptVideos;
            this.clipInfos = clipInfos ?? new List<VideoClipInfo>();
            this.regenCallback = regenCallback;
            InitializeComponent();
            InitializeVideoPlayers();
            LoadFullVideo(fullVideoPath);
            LoadPerPromptVideos(perPromptVideos);
            tabPreview.SelectedIndexChanged += TabPreview_SelectedIndexChanged;
        }

        public VideoPreviewForm()
        {
            perPromptVideos = new List<string>();
            clipInfos = new List<VideoClipInfo>();
            InitializeComponent();
            InitializeVideoPlayers();
            tabPreview.SelectedIndexChanged += TabPreview_SelectedIndexChanged;
        }

        private void TabPreview_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabPreview.SelectedIndex == 0) // Switched to Full
            {
                videoPlayerPerPrompt.StopAndHide();
                perPromptStream?.Dispose();
                perPromptStream = null;
            }
            else if (tabPreview.SelectedIndex == 1) // Switched to Per-Prompt
            {
                videoPlayerFull.StopAndHide();
                fullStream?.Dispose();
                fullStream = null;
            }
        }

        private void LoadFullVideo(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    // Normalize path
                    path = Path.GetFullPath(path).Replace(@"\\", @"\");
                    path = path.Replace("\\\\", "\\");
                    path = path.Replace("\\", "/");

                    fullStream = new FileStream(path, FileMode.Open);
                    videoPlayerFull.VideoStream = fullStream;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading {Path.GetFileName(path)}: {ex.Message}\nPath: {path}");
                }
            }
            else
            {
                videoPlayerFull.Text = "No full video available."; // Fallback
            }
        }

        private void LoadPerPromptVideos(List<string> paths)
        {
            // Stop full on load
            videoPlayerFull.StopAndHide();
            fullStream?.Dispose();
            fullStream = null;

            // Clear existing buttons
            tabPreview.TabPages[1].Controls.Remove(videoPlayerPerPrompt); // Temp remove

            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 200,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight
            };
            tabPreview.TabPages[1].Controls.Add(flowPanel);

            for (int i = 0; i < paths.Count; i++)
            {
                if (File.Exists(paths[i]))
                {
                    var info = clipInfos.Count > i ? clipInfos[i] : new VideoClipInfo(); // Safe access
                    int currentIndex = i; // Capture loop variable to avoid closure issue

                    var btnPlay = new Button
                    {
                        Text = $"Clip {i + 1}: {Path.GetFileNameWithoutExtension(paths[i])}",
                        Width = 200,
                        Height = 50,
                        Margin = new Padding(5),
                        Tag = i // Store index
                    };
                    btnPlay.Click += (s, e) =>
                    {
                        try
                        {
                            videoPlayerPerPrompt.StopAndHide();
                            perPromptStream?.Dispose();
                            string cleanPath = NormalizePath(paths[currentIndex]); // Use captured index
                            perPromptStream = new FileStream(cleanPath, FileMode.Open);
                            videoPlayerPerPrompt.VideoStream = perPromptStream;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error loading clip: {ex.Message}");
                        }
                    };

                    // Regen button (disabled for imports)
                    var btnRegen = new Button
                    {
                        Text = "Regen",
                        Width = 60,
                        Height = 50,
                        Margin = new Padding(5),
                        Tag = i,
                        Enabled = info.RowIndex >= 0 // Disable for imported (-1)
                    };
                    btnRegen.Click += (s, e) =>
                    {
                        if (info.RowIndex >= 0 && regenCallback != null)
                        {
                            this.Close(); // Back to generator
                            regenCallback(info.RowIndex);
                        }
                        else
                        {
                            MessageBox.Show("Regen not available for imported videos.");
                        }
                    };
                    flowPanel.Controls.Add(btnPlay);
                    flowPanel.Controls.Add(btnRegen);
                }
            }

            tabPreview.TabPages[1].Controls.Add(videoPlayerPerPrompt); // Re-add player above buttons
            videoPlayerPerPrompt.BringToFront();
        }

        private async void BtnExport_Click(object sender, EventArgs e)
        {
            if (perPromptVideos.Count == 0)
            {
                MessageBox.Show("No videos to export.");
                return;
            }

            using (var sfd = new SaveFileDialog { Filter = "MP4 Files|*.mp4", Title = "Export Stitched Video" })
            {
                if (sfd.ShowDialog() != DialogResult.OK) return;

                var exportDir = Path.GetDirectoryName(sfd.FileName) ?? outputDir; // Fallback
                Directory.CreateDirectory(exportDir);

                btnExport.Text = "Exporting...";
                btnExport.Enabled = false;

                try
                {
                    string stitchedPath = Path.Combine(Path.GetDirectoryName(sfd.FileName), Path.GetFileNameWithoutExtension(sfd.FileName) + "_stitched.mp4");
                    bool success = await VideoUtils.StitchVideosAsync(perPromptVideos, stitchedPath);
                    if (success)
                    {
                        success = await VideoUtils.UpscaleAndInterpolateVideoAsync(stitchedPath, sfd.FileName);

                        // New: Save metadata sidecar
                        string metaPath = Path.ChangeExtension(sfd.FileName, ".json");
                        File.WriteAllText(metaPath, Newtonsoft.Json.JsonConvert.SerializeObject(clipInfos, Formatting.Indented));

                        File.Delete(stitchedPath);

                        MessageBox.Show($"Exported to: {stitchedPath}\nMetadata: {metaPath}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}");
                }
                finally
                {
                    btnExport.Text = "Export Stitched";
                    btnExport.Enabled = true;
                }
            }
        }

        // Helper: Normalize path (same as before)
        private string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace(@"\\", @"\")
                                          .Replace("\\\\", "\\")
                                          .Replace("\\", "/");
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog
            {
                Filter = "MP4 Videos|*.mp4",
                Multiselect = true,
                Title = "Select Videos to Import for Testing"
            })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    // Append to existing or replace? Append for now
                    var imported = ofd.FileNames.ToList();
                    perPromptVideos.AddRange(imported);

                    // Add dummy metadata (no prompt/row for imports)
                    for (int i = 0; i < imported.Count; i++)
                    {
                        clipInfos.Add(new VideoClipInfo
                        {
                            Path = imported[i],
                            Prompt = "Imported Video", // Placeholder
                            Resolution = "Unknown",
                            Duration = VideoUtils.GetVideoDuration(imported[i]), // Optional probe
                            RowIndex = -1 // Flag as imported (no regen)
                        });
                    }

                    LoadPerPromptVideos(perPromptVideos); // Refresh tab
                    MessageBox.Show($"{imported.Count} videos imported. Total: {perPromptVideos.Count}");
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            videoPlayerFull?.StopAndHide();
            videoPlayerPerPrompt?.StopAndHide();
            fullStream?.Dispose();
            perPromptStream?.Dispose();
            base.OnFormClosing(e);
        }

        void InitializeVideoPlayers()
        {
            // Needs to be done here, as the designer deletes them if they are shown in design mode.
            videoPlayerFull = new VideoPlayerControl();
            videoPlayerPerPrompt = new VideoPlayerControl();

            // 
            // videoPlayerFull
            // 
            videoPlayerFull.Dock = DockStyle.Fill;
            videoPlayerFull.Location = new Point(3, 3);
            videoPlayerFull.Name = "videoPlayerFull";
            videoPlayerFull.Size = new Size(786, 566);
            videoPlayerFull.TabIndex = 0;
            // 
            // videoPlayerPerPrompt
            // 
            videoPlayerPerPrompt.Dock = DockStyle.Top;
            videoPlayerPerPrompt.Height = 400;
            videoPlayerPerPrompt.Location = new Point(3, 3);
            videoPlayerPerPrompt.Name = "videoPlayerPerPrompt";
            videoPlayerPerPrompt.Size = new Size(786, 400);
            videoPlayerPerPrompt.TabIndex = 0;
            videoPlayerPerPrompt.Loop = false;
        }
    }
}