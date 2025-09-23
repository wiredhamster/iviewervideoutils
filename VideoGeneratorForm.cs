using iviewer; // Your namespace for VideoPlayerControl
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iviewer
{
    public partial class VideoGeneratorForm : Form
    {
        private readonly string outputDir = @"C:\Users\sysadmin\Documents\ComfyUI\output\iviewer";
        private readonly string tempDir; // Temp subdir for raw outputs
        private string workflowPath = @"C:\xfer\Simplified - Export API.json"; // I2V
        private string workflowPathLast = @"C:\xfer\iViewer Wan22 flf - Export API.json"; // First/last

        private List<string> perPromptVideoPaths = new List<string>(); // Track generated videos		
        private List<string> rowImagePaths = new List<string>(); // Start images
        List<string> rowWorkflows = new List<string>();

        private Label lblResolution;
        private Button btnAddRow;
        private Button btnDeleteRow;
        private Button btnGenerateAll;
        private Button btnExtractLast; // Manual frame extract
        private Button btnPreview; // Preview button
        private DataGridView dgvPrompts;
        private DataGridViewImageColumn colImage;
        private DataGridViewTextBoxColumn colPrompt;
        private DataGridViewButtonColumn colLora;
        private DataGridViewButtonColumn colGenerate;
        private ProgressBar pbProgress;
        private VideoPlayerControl videoPlayer;

        public VideoGeneratorForm()
        {
            tempDir = Path.Combine(outputDir, "temp");
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(tempDir);
            InitializeComponent();
            InitializeGrid();
        }

        private void InitializeComponent()
        {
            lblResolution = new Label();
            btnAddRow = new Button();
            btnDeleteRow = new Button();
            btnGenerateAll = new Button();
            btnExtractLast = new Button();
            btnPreview = new Button();
            dgvPrompts = new DataGridView();
            colImage = new DataGridViewImageColumn();
            colPrompt = new DataGridViewTextBoxColumn();
            colLora = new DataGridViewButtonColumn();
            colGenerate = new DataGridViewButtonColumn();
            pbProgress = new ProgressBar();
            ((System.ComponentModel.ISupportInitialize)dgvPrompts).BeginInit();
            SuspendLayout();
            // 
            // lblResolution
            // 
            lblResolution.AutoSize = true;
            lblResolution.Location = new Point(12, 9);
            lblResolution.Name = "lblResolution";
            lblResolution.Size = new Size(29, 15);
            lblResolution.TabIndex = 0;
            lblResolution.Text = "N/A";
            // 
            // btnAddRow
            // 
            btnAddRow.Location = new Point(118, 5);
            btnAddRow.Name = "btnAddRow";
            btnAddRow.Size = new Size(75, 23);
            btnAddRow.TabIndex = 1;
            btnAddRow.Text = "+ Add Row";
            btnAddRow.UseVisualStyleBackColor = true;
            btnAddRow.Click += BtnAddRow_Click;
            // 
            // btnDeleteRow
            // 
            btnDeleteRow.Location = new Point(199, 5);
            btnDeleteRow.Name = "btnDeleteRow";
            btnDeleteRow.Size = new Size(84, 23);
            btnDeleteRow.TabIndex = 2;
            btnDeleteRow.Text = "- Delete Row";
            btnDeleteRow.UseVisualStyleBackColor = true;
            btnDeleteRow.Click += BtnDeleteRow_Click;
            // 
            // btnGenerateAll
            // 
            btnGenerateAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnGenerateAll.Location = new Point(500, 5); // Adjusted
            btnGenerateAll.Name = "btnGenerateAll";
            btnGenerateAll.Size = new Size(100, 23);
            btnGenerateAll.TabIndex = 3;
            btnGenerateAll.Text = "Generate All";
            btnGenerateAll.UseVisualStyleBackColor = true;
            btnGenerateAll.Click += BtnGenerateAll_Click;
            // 
            // btnExtractLast
            // 
            btnExtractLast.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnExtractLast.Location = new Point(606, 5);
            btnExtractLast.Name = "btnExtractLast";
            btnExtractLast.Size = new Size(90, 23);
            btnExtractLast.TabIndex = 4;
            btnExtractLast.Text = "Extract Last";
            btnExtractLast.UseVisualStyleBackColor = true;
            btnExtractLast.Enabled = false;
            btnExtractLast.Click += BtnExtractLast_Click;
            // 
            // btnPreview
            // 
            btnPreview.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnPreview.Enabled = true;
            btnPreview.Location = new Point(702, 5);
            btnPreview.Name = "btnPreview";
            btnPreview.Size = new Size(80, 23);
            btnPreview.TabIndex = 5;
            btnPreview.Text = "Preview";
            btnPreview.UseVisualStyleBackColor = true;
            btnPreview.Click += btnPreview_Click;
            // 
            // dgvPrompts
            // 
            dgvPrompts.AllowUserToAddRows = false;
            dgvPrompts.AllowUserToDeleteRows = false;
            dgvPrompts.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvPrompts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPrompts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPrompts.Columns.AddRange(new DataGridViewColumn[] { colImage, colPrompt, colLora, colGenerate });
            dgvPrompts.Location = new Point(3, 35);
            dgvPrompts.Name = "dgvPrompts";
            dgvPrompts.RowHeadersVisible = false;
            dgvPrompts.RowTemplate.Height = 60;
            dgvPrompts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPrompts.Size = new Size(778, 502);
            dgvPrompts.TabIndex = 6;
            // 
            // colImage
            // 
            colImage.HeaderText = "";
            colImage.Name = "colImage";
            colImage.Width = 150;
            // 
            // colPrompt
            // 
            colPrompt.HeaderText = "";
            colPrompt.Name = "colPrompt";
            colPrompt.Width = 300;
            // 
            // colLora
            // 
            colLora.HeaderText = "";
            colLora.Name = "colLora";
            colLora.Text = "Select LoRA";
            colLora.UseColumnTextForButtonValue = true;
            colLora.Width = 100;
            // 
            // colGenerate
            // 
            colGenerate.HeaderText = "";
            colGenerate.Name = "colGenerate";
            colGenerate.Text = "Generate";
            colGenerate.Width = 100;
            // 
            // pbProgress
            // 
            pbProgress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pbProgress.Location = new Point(3, 537);
            pbProgress.Name = "pbProgress";
            pbProgress.Size = new Size(778, 23);
            pbProgress.TabIndex = 7;
            // 
            // VideoGeneratorForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(784, 561);
            Controls.Add(pbProgress);
            Controls.Add(dgvPrompts);
            Controls.Add(btnPreview);
            Controls.Add(btnExtractLast);
            Controls.Add(btnGenerateAll);
            Controls.Add(btnDeleteRow);
            Controls.Add(btnAddRow);
            Controls.Add(lblResolution);
            Name = "VideoGeneratorForm";
            Text = "Video Generator";
            ((System.ComponentModel.ISupportInitialize)dgvPrompts).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private void InitializeGrid()
        {
            // Define columns if not in Designer (or verify they exist)
            if (dgvPrompts.Columns["colImage"] == null) dgvPrompts.Columns.Add(new DataGridViewImageColumn { Name = "colImage", HeaderText = "", Width = 150, ImageLayout = DataGridViewImageCellLayout.Zoom });
            if (dgvPrompts.Columns["colPrompt"] == null)
            {
                colPrompt = new DataGridViewTextBoxColumn { Name = "colPrompt", HeaderText = "", Width = 300 };
                // Multi-line support
                colPrompt.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                colPrompt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
                dgvPrompts.Columns.Add(colPrompt);
            }
            if (dgvPrompts.Columns["colLora"] == null) dgvPrompts.Columns.Add(new DataGridViewButtonColumn { Name = "colLora", HeaderText = "", Text = "Select LoRA", Width = 100, UseColumnTextForButtonValue = true });
            if (dgvPrompts.Columns["colGenerate"] == null) dgvPrompts.Columns.Add(new DataGridViewButtonColumn { Name = "colGenerate", HeaderText = "", Text = "Generate", Width = 100, UseColumnTextForButtonValue = true });

            dgvPrompts.RowHeadersVisible = false;
            dgvPrompts.AllowUserToAddRows = false;
            dgvPrompts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPrompts.CellClick += DgvPrompts_CellClick;
            dgvPrompts.CellValueChanged += DgvPrompts_CellValueChanged; // Track changes for button state
            dgvPrompts.EditingControlShowing += DgvPrompts_EditingControlShowing; // For multi-line editing
            // Enable row auto-sizing for wrapped text (with max height cap)
            dgvPrompts.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

            // Default row
            AddNewRow();
        }

        private void DgvPrompts_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox tb && dgvPrompts.CurrentCell.ColumnIndex == dgvPrompts.Columns["colPrompt"].Index)
            {
                tb.Multiline = true;
                tb.AcceptsReturn = true;
                tb.WordWrap = true;
            }
        }

        private void AddNewRow()
        {
            int rowIndex = dgvPrompts.Rows.Add(); // Adds empty row
            dgvPrompts.Rows[rowIndex].Cells["colPrompt"].Value = "A woman posing for a photoshoot. She smiles and sways her body."; // Default prompt
            dgvPrompts.Rows[rowIndex].Cells["colLora"].Value = "Select LoRA";
            dgvPrompts.Rows[rowIndex].Cells["colGenerate"].Value = "Generate";
            rowImagePaths.Add(""); // Empty path for new row
            perPromptVideoPaths.Add(""); // Empty video for new row
            rowWorkflows.Add(""); // Empty workflow for new row
            dgvPrompts.CurrentCell = dgvPrompts.Rows[rowIndex].Cells["colPrompt"]; // Focus prompt
            UpdatePreviewButton(); // Check if preview enabled
            UpdateExtractButton();
        }

        private void DgvPrompts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return; // Ignore header clicks

            int colIndex = e.ColumnIndex;
            DataGridViewColumn imageCol = dgvPrompts.Columns["colImage"];
            DataGridViewColumn loraCol = dgvPrompts.Columns["colLora"];
            DataGridViewColumn generateCol = dgvPrompts.Columns["colGenerate"];

            if (colIndex == imageCol.Index)
            {
                // Image selection logic (as before)
                using (var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        using (var img = System.Drawing.Image.FromFile(ofd.FileName))
                        {
                            var thumb = new Bitmap(100, 100);
                            using (var g = Graphics.FromImage(thumb))
                            {
                                g.DrawImage(img, new Rectangle(0, 0, 100, 100), new Rectangle(0, 0, img.Width, img.Height), GraphicsUnit.Pixel);
                            }
                            dgvPrompts.Rows[e.RowIndex].Cells[imageCol.Name].Value = thumb;
                        }
                        rowImagePaths[e.RowIndex] = ofd.FileName;
                        UpdateResolutionFromFirstImage();
                    }
                }
            }
            else if (colIndex == loraCol.Index)
            {
                // LoRA placeholder
                MessageBox.Show("LoRA selector coming in Stage 3.");
            }
            else if (colIndex == generateCol.Index)
            {
                // Single row generation
                GenerateSingleRow(e.RowIndex);
            }
        }

        // Track row changes to reset button state
        private void DgvPrompts_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvPrompts.Rows[e.RowIndex];
            if (row.Cells["colGenerate"].Value?.ToString() == "Generated")
            {
                row.Cells["colGenerate"].Value = "Generate"; // Revert if changed
            }
            UpdatePreviewButton();
            UpdateExtractButton();
        }

        private void UpdatePreviewButton()
        {
            //btnPreview.Enabled = perPromptVideoPaths.Any(p => !string.IsNullOrEmpty(p) && File.Exists(p));
        }

        private void UpdateExtractButton()
        {
            btnExtractLast.Enabled = dgvPrompts.SelectedRows.Count > 0 && perPromptVideoPaths.Any(p => !string.IsNullOrEmpty(p) && File.Exists(p));
        }

        private void UpdateResolutionFromFirstImage()
        {
            if (dgvPrompts.Rows.Count == 0 || string.IsNullOrEmpty(rowImagePaths[0]))
            {
                lblResolution.Text = "N/A";
                return;
            }

            string firstPath = rowImagePaths[0];
            var targetWidth = 0;
            var targetHeight = 0;
            (targetWidth, targetHeight) = CalculateResolution(firstPath);
            lblResolution.Text = $"{targetWidth}x{targetHeight}";
        }

        private async void GenerateSingleRow(int rowIndex)
        {
            string imagePath = rowImagePaths[rowIndex];
            string prompt = dgvPrompts.Rows[rowIndex].Cells["colPrompt"].Value?.ToString() ?? "";

            if (string.IsNullOrEmpty(imagePath) || string.IsNullOrEmpty(prompt))
            {
                MessageBox.Show("Select an image and enter a prompt for this row.");
                return;
            }

            // Button state (text only)
            var generateCell = dgvPrompts.Rows[rowIndex].Cells["colGenerate"];
            generateCell.Value = "Generating...";
            dgvPrompts.Refresh(); // Immediate UI update

            // First/last: Use next row's start as end if exists
            string endImagePath = null;
            if (rowIndex + 1 < rowImagePaths.Count && !string.IsNullOrEmpty(rowImagePaths[rowIndex + 1]))
            {
                endImagePath = rowImagePaths[rowIndex + 1];
            }

            string videoPath = await GenerateSingleVideo(prompt, imagePath, endImagePath, rowIndex);
            if (!string.IsNullOrEmpty(videoPath))
            {
                perPromptVideoPaths[rowIndex] = videoPath;
                generateCell.Value = "Generated";
                UpdatePreviewButton();
                UpdateExtractButton();
                // Refresh preview if open (pass via event or check if form open)
                if (Application.OpenForms.OfType<VideoPreviewForm>().Any())
                {
                    // TODO: Reload last video in preview or broadcast event
                }
            }
            else
            {
                generateCell.Value = "Generate"; // Fail: Revert
            }
        }

        private void BtnExtractLast_Click(object sender, EventArgs e)
        {
            if (dgvPrompts.SelectedRows.Count == 0) return;
            int rowIndex = dgvPrompts.SelectedRows[0].Index;
            string videoPath = perPromptVideoPaths[rowIndex];
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath)) return;

            // Extract to next row's start if exists, else current
            int targetRow = rowIndex + 1 < rowImagePaths.Count ? rowIndex + 1 : rowIndex;
            string framePath = ExtractLastFrame(videoPath);
            if (!string.IsNullOrEmpty(framePath))
            {
                rowImagePaths[targetRow] = framePath;
                // Refresh thumb
                using (var img = System.Drawing.Image.FromFile(framePath))
                {
                    var thumb = new Bitmap(100, 100);
                    using (var g = Graphics.FromImage(thumb))
                    {
                        g.DrawImage(img, new Rectangle(0, 0, 100, 100), new Rectangle(0, 0, img.Width, img.Height), GraphicsUnit.Pixel);
                    }
                    dgvPrompts.Rows[targetRow].Cells["colImage"].Value = thumb;
                }
                if (targetRow == 0) UpdateResolutionFromFirstImage();
                MessageBox.Show($"Last frame extracted to row {targetRow + 1}.");
            }
        }

        private string ExtractLastFrame(string videoPath)
        {
            return VideoUtils.ExtractLastFrame(videoPath);
        }

        private async void BtnGenerateAll_Click(object sender, EventArgs e)
        {
            if (dgvPrompts.Rows.Count == 0)
            {
                MessageBox.Show("Add at least one prompt.");
                return;
            }

            // Clear existing videos and reset buttons
            perPromptVideoPaths.Clear();
            perPromptVideoPaths = new List<string>(new string[dgvPrompts.Rows.Count]);
            foreach (DataGridViewRow row in dgvPrompts.Rows)
            {
                row.Cells["colGenerate"].Value = "Generate";
            }

            string prevVideoPath = null; // For chaining
            int completed = 0;
            for (int i = 0; i < dgvPrompts.Rows.Count; i++)
            {
                DataGridViewRow row = dgvPrompts.Rows[i];
                string imagePath = rowImagePaths[i];
                string prompt = row.Cells["colPrompt"].Value?.ToString() ?? "";

                // Chaining: If no start image, extract from prev video
                if (string.IsNullOrEmpty(imagePath) && !string.IsNullOrEmpty(prevVideoPath))
                {
                    imagePath = ExtractLastFrame(prevVideoPath);
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        rowImagePaths[i] = imagePath;
                        // Refresh thumb for start col
                        using (var img = System.Drawing.Image.FromFile(imagePath))
                        {
                            var thumb = new Bitmap(100, 100);
                            using (var g = Graphics.FromImage(thumb))
                            {
                                g.DrawImage(img, new Rectangle(0, 0, 100, 100), new Rectangle(0, 0, img.Width, img.Height), GraphicsUnit.Pixel);
                            }
                            row.Cells["colImage"].Value = thumb;
                        }
                        if (i == 0) UpdateResolutionFromFirstImage();
                    }
                }

                // First/last: Use next row's start as end if exists
                string endImagePath = null;
                if (i + 1 < rowImagePaths.Count && !string.IsNullOrEmpty(rowImagePaths[i + 1]))
                {
                    endImagePath = rowImagePaths[i + 1];
                }

                if (!string.IsNullOrEmpty(imagePath) && !string.IsNullOrEmpty(prompt))
                {
                    row.Cells["colGenerate"].Value = "Generating...";
                    dgvPrompts.Refresh();

                    string videoPath = await GenerateSingleVideo(prompt, imagePath, endImagePath, i);
                    if (!string.IsNullOrEmpty(videoPath))
                    {
                        perPromptVideoPaths[i] = videoPath;
                        prevVideoPath = videoPath; // For next chain
                        row.Cells["colGenerate"].Value = "Generated";
                    }
                    else
                    {
                        row.Cells["colGenerate"].Value = "Generate";
                    }
                    completed++;
                }
            }

            pbProgress.Value = 100;
            UpdatePreviewButton();
            UpdateExtractButton();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            OpenPreviewPopup();
        }

        private void BtnAddRow_Click(object sender, EventArgs e)
        {
            AddNewRow();
        }

        private void BtnDeleteRow_Click(object sender, EventArgs e)
        {
            if (dgvPrompts.SelectedRows.Count > 0)
            {
                int rowIndex = dgvPrompts.SelectedRows[0].Index;
                string videoPath = perPromptVideoPaths[rowIndex];
                if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                {
                    if (MessageBox.Show("Delete associated video file?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        File.Delete(videoPath);
                    }
                }
                dgvPrompts.Rows.RemoveAt(rowIndex);
                rowImagePaths.RemoveAt(rowIndex);
                perPromptVideoPaths.RemoveAt(rowIndex);
            }
            UpdateResolutionFromFirstImage();
            UpdatePreviewButton();
            UpdateExtractButton();
        }

        private void OpenPreviewPopup()
        {
            // Filter non-empty video paths for preview
            var validVideos = perPromptVideoPaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList();
            if (validVideos.Count == 0)
            {
                // Show the preview form without any videos for debugging.
                var debugForm = new VideoPreviewForm();
                debugForm.Show();
                return;
            }

            var clipInfos = new List<VideoClipInfo>();
            for (int i = 0; i < dgvPrompts.Rows.Count; i++)
            {
                if (!string.IsNullOrEmpty(perPromptVideoPaths[i]) && File.Exists(perPromptVideoPaths[i]))
                {
                    var row = dgvPrompts.Rows[i];
                    string prompt = row.Cells["colPrompt"].Value?.ToString() ?? string.Empty;
                    string imagePath = rowImagePaths[i];
                    string workflowJson = rowWorkflows[i];

                    var info = ExtractClipInfo(prompt, imagePath, workflowJson);
                    info.Path = perPromptVideoPaths[i];
                    info.Resolution = lblResolution.Text; // Or calc per-video
                    info.Duration = VideoUtils.GetVideoDuration(perPromptVideoPaths[i]);
                    info.RowIndex = i;

                    clipInfos.Add(info);
                }
            }

            string fullVideoPath = validVideos.Count > 0 ? validVideos.Last() : null; // Last as "full" for now

            // Pass callback for regen
            Action<int> regenCallback = (rowIndex) =>
            {
                dgvPrompts.ClearSelection();
                dgvPrompts.Rows[rowIndex].Selected = true;
                dgvPrompts.CurrentCell = dgvPrompts.Rows[rowIndex].Cells["colGenerate"];
                GenerateSingleRow(rowIndex);
            };

            var previewForm = new VideoPreviewForm(fullVideoPath, validVideos, clipInfos, regenCallback);
            previewForm.Show();
        }

        private VideoClipInfo ExtractClipInfo(string runtimePrompt, string runtimeImagePath, string workflowJson)
        {
            var info = new VideoClipInfo();
            info.Prompt = runtimePrompt; // Runtime, not from template
            info.NegativePrompt = ""; // Extract from template

            try
            {
                // Positive/Negative Prompt (from template, but positive has placeholder—replace with runtime)
                var workflow = JObject.Parse(workflowJson);
                var clipEncodes = workflow.Properties().Select(p => p.Value as JObject).Where(n => n?["class_type"].ToString() == "CLIPTextEncode");
                var positiveNode = clipEncodes.FirstOrDefault(n => n["inputs"]["text"].ToString().Contains("{PROMPT}"));
                if (positiveNode != null)
                {
                    info.Prompt = positiveNode["inputs"]["text"].ToString().Replace("{PROMPT}", runtimePrompt); // Full with runtime
                }
                var negativeNode = clipEncodes.FirstOrDefault(n => !n["inputs"]["text"].ToString().Contains("{PROMPT}"));
                if (negativeNode != null)
                {
                    info.NegativePrompt = negativeNode["inputs"]["text"].ToString();
                }

                // Seed (first KSamplerAdvanced)
                var ksamplers = workflow.Properties().Select(p => p.Value as JObject).Where(n => n?["class_type"].ToString() == "KSamplerAdvanced");
                if (ksamplers.Any())
                {
                    info.Seed = int.TryParse(ksamplers.First()["inputs"]["noise_seed"].ToString(), out int seed) ? seed : null;
                }

                // Model (UNETLoader unet_name, join if multiple)
                var models = workflow.Properties().Select(p => p.Value as JObject).Where(n => n?["class_type"].ToString() == "UNETLoader").Select(n => n["inputs"]["unet_name"].ToString());
                if (models.Any())
                {
                    info.Model = string.Join(", ", models);
                }

                // CFGScale, Steps, Sampler (first KSamplerAdvanced)
                if (ksamplers.Any())
                {
                    var primaryKS = ksamplers.First()["inputs"];
                    info.CFGScale = float.TryParse(primaryKS["cfg"].ToString(), out float cfg) ? cfg : 1.0f;
                    info.Steps = int.TryParse(primaryKS["steps"].ToString(), out int steps) ? steps : 20;
                    info.Sampler = primaryKS["sampler_name"].ToString();
                }

                // ClipSkip (default, not in template)
                info.ClipSkip = 1;

                // Source (GUID from runtime imagePath)
                string filename = Path.GetFileNameWithoutExtension(runtimeImagePath);
                if (Guid.TryParse(filename, out Guid guid))
                {
                    info.Source = guid.ToString();
                }

                // Parameters
                info.Parameters["Workflow"] = Path.GetFileName(workflowPath);

                // Type
                var types = workflow.Properties().Select(p => p.Value as JObject).Select(n => n?["class_type"].ToString());
                if (types.Contains("WanImageToVideo"))
                {
                    info.Parameters["Type"] = "i2v";
                }
                else if (types.Contains("WanFirstLastFrameToVideo"))
                {
                    info.Parameters["Type"] = "flf";
                }
                else
                {
                    info.Parameters["Type"] = "unknown";
                }

                // LoRAs (from LoraLoaderModelOnly, join)
                var loras = workflow.Properties().Select(p => p.Value as JObject).Where(n => n?["class_type"].ToString() == "LoraLoaderModelOnly").Select(n => n["inputs"]["lora_name"].ToString());
                if (loras.Any())
                {
                    info.Parameters["LoRAs"] = string.Join(", ", loras);
                }

                // VAE (from VAELoader)
                var vaes = workflow.Properties().Select(p => p.Value as JObject).Where(n => n?["class_type"].ToString() == "VAELoader").Select(n => n["inputs"]["vae_name"].ToString());
                if (vaes.Any())
                {
                    info.Parameters["VAE"] = vaes.First();
                }

                // Scheduler (from first KSamplerAdvanced)
                if (ksamplers.Any())
                {
                    info.Parameters["Scheduler"] = ksamplers.First()["inputs"]["scheduler"].ToString();
                }

                // FPS (from CreateVideo)
                var createVideos = workflow.Properties().Select(p => p.Value as JObject).Where(n => n?["class_type"].ToString() == "CreateVideo");
                if (createVideos.Any())
                {
                    info.Parameters["FPS"] = createVideos.First()["inputs"]["fps"].ToString();
                }

                // Add more common params as needed (e.g., shift from ModelSamplingSD3)
                var modelSamplings = workflow.Properties().Select(p => p.Value as JObject).Where(n => n?["class_type"].ToString() == "ModelSamplingSD3");
                if (modelSamplings.Any())
                {
                    info.Parameters["Shift"] = modelSamplings.First()["inputs"]["shift"].ToString();
                }

                return info;
            }
            catch (Exception ex)
            {
                // Fallback defaults on error
                return new VideoClipInfo { Prompt = runtimePrompt, NegativePrompt = "default negative", Seed = null, Model = "default", CFGScale = 1.0f, Steps = 20, Sampler = "euler_ancestral", ClipSkip = 1, Source = "", Parameters = new Dictionary<string, string> { { "Type", "unknown" } } };
            }
        }

        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<string> RunWorkflowAsync(string workflowPath)
        {
            if (!File.Exists(workflowPath))
                throw new FileNotFoundException("Workflow JSON file not found", workflowPath);

            string rawJson = await File.ReadAllTextAsync(workflowPath);

            string wrappedJson = rawJson;

            using var content = new StringContent(wrappedJson, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await httpClient.PostAsync("http://127.0.0.1:8000/prompt", content);

            string body = await response.Content.ReadAsStringAsync();
            Debug.WriteLine(body);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        private async Task<string> GenerateSingleVideo(string prompt, string imagePath, string endImagePath = null, int rowIndex = 0)
        {
            pbProgress.Value = 0;

            // Select workflow
            string selectedWorkflow = string.IsNullOrEmpty(endImagePath) ? workflowPath : workflowPathLast;
            string workflowJson = File.ReadAllText(selectedWorkflow);

            // Calculate resolution (average if end)
            var width = 0;
            var height = 0;
            (width, height) = CalculateResolution(imagePath, endImagePath);

            // Replacements
            workflowJson = workflowJson.Replace("{PROMPT}", prompt)
                                       .Replace("{START_IMAGE}", Path.GetFileName(imagePath))
                                       .Replace("{WIDTH}", width.ToString())
                                       .Replace("{HEIGHT}", height.ToString());
            if (!string.IsNullOrEmpty(endImagePath))
            {
                workflowJson = workflowJson.Replace("{END_IMAGE}", Path.GetFileName(endImagePath));
            }

            using (var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8000") })
            {
                try
                {
                    // Multipart upload for start image
                    string startFilename = await UploadImageMultipart(client, imagePath);
                    workflowJson = workflowJson.Replace(Path.GetFileName(imagePath), startFilename);
                    Debug.WriteLine($"Uploaded start image as: {startFilename}");

                    // Multipart upload for end image if provided
                    if (!string.IsNullOrEmpty(endImagePath))
                    {
                        string endFilename = await UploadImageMultipart(client, endImagePath);
                        workflowJson = workflowJson.Replace(Path.GetFileName(endImagePath), endFilename);
                        Debug.WriteLine($"Uploaded end image as: {endFilename}");
                    }

                    // Parse workflow
                    var workflow = JObject.Parse(workflowJson);

                    // Save processed debug
                    string debugPath = Path.Combine(Path.GetDirectoryName(selectedWorkflow) ?? "", "debug_api_processed.json");
                    File.WriteAllText(debugPath, workflow.ToString(Formatting.Indented));
                    Debug.WriteLine($"Processed API debug saved to: {debugPath}");

                    // Wrap for API
                    var payload = new JObject { ["prompt"] = workflow };
                    Debug.WriteLine("API Payload Snippet: " + payload.ToString(Formatting.None).Substring(0, 300) + "...");

                    // Send
                    var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("/prompt", content);
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Workflow Response: {responseBody}");

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Prompt failed: {responseBody}");
                    }

                    var result = JObject.Parse(responseBody);
                    var promptId = result["prompt_id"]?.ToString();
                    if (string.IsNullOrEmpty(promptId)) throw new Exception("No prompt_id in response.");

                    rowWorkflows[rowIndex] = workflowJson;

                    // Poll for completion
                    while (true)
                    {
                        var statusResponse = await client.GetAsync($"/history/{promptId}");
                        if (!statusResponse.IsSuccessStatusCode)
                        {
                            Debug.WriteLine($"Status fetch failed: {await statusResponse.Content.ReadAsStringAsync()}");
                            await Task.Delay(1000);
                            continue;
                        }
                        string statusBody = await statusResponse.Content.ReadAsStringAsync();
                        var statusJson = JObject.Parse(statusBody ?? "{}");
                        if (statusJson[promptId]?["status"]?["completed"]?.Value<bool>() == true)
                        {
                            pbProgress.Value = 100;

                            // Find newest MP4 in temp dir
                            var tempVideos = Directory.GetFiles(tempDir, "*.mp4")
                                .Select(f => new { Path = f, Time = File.GetCreationTime(f) })
                                .OrderByDescending(x => x.Time)
                                .FirstOrDefault();
                            if (tempVideos == null)
                            {
                                throw new Exception("No video found in temp dir after completion.");
                            }

                            // Move to main dir with unique name
                            string uniqueName = $"video_row_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                            string finalPath = Path.Combine(outputDir, uniqueName);
                            File.Move(tempVideos.Path, finalPath);

                            // Normalize path
                            finalPath = Path.GetFullPath(finalPath).Replace(@"\\", @"\");
                            finalPath = finalPath.Replace("\\\\", "\\");
                            finalPath = finalPath.Replace("\\", "/");

                            Debug.WriteLine($"Moved video from temp to: {finalPath}");
                            return finalPath;
                        }
                        pbProgress.Value = Math.Min(pbProgress.Value + 10, 90);
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error generating video: {ex.Message}\n\nCheck VS Output > Debug for logs.");
                    return null;
                }
            }
        }

        private (int Width, int Height) CalculateResolution(string startImagePath, string endImagePath = null)
        {
            double totalAspect = 0;
            int count = 0;

            using (var startImg = System.Drawing.Image.FromFile(startImagePath))
            {
                totalAspect += (double)startImg.Width / startImg.Height;
                count++;
            }

            if (!string.IsNullOrEmpty(endImagePath))
            {
                using (var endImg = System.Drawing.Image.FromFile(endImagePath))
                {
                    totalAspect += (double)endImg.Width / endImg.Height;
                    count++;
                }
            }

            double aspect = totalAspect / count;
            int targetPixels = 720 * 512;
            double sqrtPixels = Math.Sqrt(targetPixels * aspect);
            int targetWidth = (int)Math.Round(sqrtPixels / 16.0) * 16; // Snap to nearest multiple of 16
            double targetHeightDouble = targetWidth / aspect;
            int targetHeight = (int)Math.Round(targetHeightDouble / 16.0) * 16; // Snap to nearest multiple of 16

            return (targetWidth, targetHeight);
        }

        // Updated multipart upload helper
        private async Task<string> UploadImageMultipart(HttpClient client, string imagePath)
        {
            string filename = Path.GetFileName(imagePath);
            string contentType = Path.GetExtension(imagePath).ToLower() == ".png" ? "image/png" : "image/jpeg";

            using (var fileStream = File.OpenRead(imagePath))
            {
                var multipartContent = new MultipartFormDataContent();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                multipartContent.Add(fileContent, "image", filename); // Key "image" is required

                var uploadResponse = await client.PostAsync("/upload/image", multipartContent);
                string uploadBody = await uploadResponse.Content.ReadAsStringAsync();
                Debug.WriteLine($"Upload Response for {filename}: {uploadBody}");

                if (!uploadResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Upload failed for {filename}: {uploadBody} (Status: {uploadResponse.StatusCode})");
                }

                var uploadResult = JObject.Parse(uploadBody);
                return uploadResult["name"]?.ToString() ?? filename; // ComfyUI returns the saved name
            }
        }
    }
}