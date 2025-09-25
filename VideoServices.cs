using FFMpegCore;
using iviewer.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iviewer.Services
{
    public class VideoGenerationService
    {
        private readonly VideoGenerationConfig _config;
        private readonly HttpClient _httpClient;

        public VideoGenerationService(VideoGenerationConfig config)
        {
            _config = config;
            _httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8000") };

            // Ensure directories exist
            Directory.CreateDirectory(_config.OutputDir);
            Directory.CreateDirectory(_config.TempDir);
        }

        public async Task<string> GenerateVideoAsync(VideoRowData rowData, int rowIndex)
        {
            string selectedWorkflow = string.IsNullOrEmpty(rowData.EndImagePath)
                ? _config.WorkflowPath
                : _config.WorkflowPathLast;

            string workflowJson = await PrepareWorkflowAsync(selectedWorkflow, rowData);
            string videoPath = await ExecuteWorkflowAsync(workflowJson, rowData, rowIndex);

            if (!string.IsNullOrEmpty(videoPath))
            {
                rowData.WorkflowJson = workflowJson; // Store for metadata extraction
            }

            return videoPath;
        }

        public async Task GenerateAllVideosAsync(
            List<VideoRowData> allRowData,
            List<string> rowImagePaths,
            List<string> perPromptVideoPaths,
            List<string> rowWorkflows,
            Action<int, string> onRowStatusUpdate,
            Action<int> onProgressUpdate,
            Action<int, string> onRowImageUpdate = null)
        {
            string prevVideoPath = null;

            for (int i = 0; i < allRowData.Count; i++)
            {
                var rowData = allRowData[i];

                // Chain videos: extract last frame if no start image
                if (string.IsNullOrEmpty(rowData.ImagePath) && !string.IsNullOrEmpty(prevVideoPath))
                {
                    string extractedFrame = VideoUtils.ExtractLastFrame(prevVideoPath, _config.TempDir);
                    if (!string.IsNullOrEmpty(extractedFrame))
                    {
                        rowData.ImagePath = extractedFrame;
                        rowImagePaths[i] = extractedFrame;

                        onRowImageUpdate?.Invoke(i, extractedFrame);
                    }
                }

                if (!string.IsNullOrEmpty(rowData.ImagePath) && !string.IsNullOrEmpty(rowData.Prompt))
                {
                    onRowStatusUpdate(i, "Generating...");

                    try
                    {
                        string videoPath = await GenerateVideoAsync(rowData, i);
                        if (!string.IsNullOrEmpty(videoPath))
                        {
                            perPromptVideoPaths[i] = videoPath;
                            rowWorkflows[i] = rowData.WorkflowJson;
                            prevVideoPath = videoPath;
                            onRowStatusUpdate(i, "Generated");
                        }
                        else
                        {
                            onRowStatusUpdate(i, "Generate");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error generating video for row {i}: {ex.Message}");
                        onRowStatusUpdate(i, "Generate");
                    }
                }

                int progress = (int)((i + 1) / (double)allRowData.Count * 100);
                onProgressUpdate(progress);
            }
        }

        private async Task<string> PrepareWorkflowAsync(string workflowPath, VideoRowData rowData)
        {
            string workflowJson = await File.ReadAllTextAsync(workflowPath);
            var (width, height) = ResolutionCalculator.Calculate(rowData.ImagePath, rowData.EndImagePath);

            // Upload images and get server filenames
            string startFilename = await UploadImageAsync(rowData.ImagePath);
            workflowJson = workflowJson.Replace("{START_IMAGE}", startFilename);

            if (!string.IsNullOrEmpty(rowData.EndImagePath))
            {
                string endFilename = await UploadImageAsync(rowData.EndImagePath);
                workflowJson = workflowJson.Replace("{END_IMAGE}", endFilename);
            }

            // Replace placeholders
            return workflowJson
                .Replace("{PROMPT}", rowData.Prompt)
                .Replace("{WIDTH}", width.ToString())
                .Replace("{HEIGHT}", height.ToString());
        }

        private async Task<string> UploadImageAsync(string imagePath)
        {
            string filename = Path.GetFileName(imagePath);
            string contentType = Path.GetExtension(imagePath).ToLower() == ".png" ? "image/png" : "image/jpeg";

            using var fileStream = File.OpenRead(imagePath);
            using var multipartContent = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            multipartContent.Add(fileContent, "image", filename);

            var response = await _httpClient.PostAsync("/upload/image", multipartContent);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Upload failed for {filename}: {responseBody}");
            }

            var result = JObject.Parse(responseBody);
            return result["name"]?.ToString() ?? filename;
        }

        private async Task<string> ExecuteWorkflowAsync(string workflowJson, VideoRowData rowData, int rowIndex)
        {
            var workflow = JObject.Parse(workflowJson);
            var payload = new JObject { ["prompt"] = workflow };

            var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/prompt", content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Workflow execution failed: {responseBody}");
            }

            var result = JObject.Parse(responseBody);
            string promptId = result["prompt_id"]?.ToString();

            if (string.IsNullOrEmpty(promptId))
            {
                throw new Exception("No prompt_id in response");
            }

            return await PollForCompletionAsync(promptId);
        }

        private async Task<string> PollForCompletionAsync(string promptId)
        {
            while (true)
            {
                var statusResponse = await _httpClient.GetAsync($"/history/{promptId}");
                if (!statusResponse.IsSuccessStatusCode)
                {
                    await Task.Delay(1000);
                    continue;
                }

                string statusBody = await statusResponse.Content.ReadAsStringAsync();
                var statusJson = JObject.Parse(statusBody ?? "{}");

                if (statusJson[promptId]?["status"]?["completed"]?.Value<bool>() == true)
                {
                    return FindGeneratedVideo();
                }

                await Task.Delay(1000);
            }
        }

        private string FindGeneratedVideo()
        {
            var tempVideos = Directory.GetFiles(_config.TempDir, "*.mp4")
                .Select(f => new { Path = f, Time = File.GetCreationTime(f) })
                .OrderByDescending(x => x.Time)
                .FirstOrDefault();

            if (tempVideos == null)
            {
                throw new Exception("No video found in temp directory after completion");
            }

            string uniqueName = $"video_row_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            string finalPath = Path.Combine(_config.OutputDir, uniqueName);
            File.Move(tempVideos.Path, finalPath);

            return Path.GetFullPath(finalPath);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class VideoExportService
    {
        private readonly VideoGenerationConfig _config;

        public VideoExportService(VideoGenerationConfig config)
        {
            _config = config;
        }

        public async Task<string> ExportVideosAsync(VideoExportData exportData, Action<int> onProgress)
        {
            Directory.CreateDirectory(_config.ExportDir);

            string filename = exportData.ExportTimestamp.ToString("yyyyMMdd-HHmmss");
            string stitchedPath = Path.Combine(_config.TempDir, filename + "_stitched.mp4");
            string tempPath = Path.Combine(_config.TempDir, filename + ".mp4");
            string exportPath = Path.Combine(_config.ExportDir, filename + ".mp4");
            string metaPath = Path.Combine(_config.ExportDir, filename + ".json");

            try
            {
                onProgress(25);
                bool success = await VideoUtils.StitchVideosWithTransitionsAsync(
                    exportData.VideoPaths, 
                    stitchedPath,
                    exportData.TransitionType,
                    exportData.TransitionDurations);

                if (!success)
                {
                    throw new Exception("Video stitching failed");
                }

                onProgress(50);
                success = await VideoUtils.UpscaleAndInterpolateVideoAsync(stitchedPath, tempPath);

                if (!success)
                {
                    throw new Exception("Video processing failed");
                }

                onProgress(75);
                await ExportMetadataAsync(exportData.ClipInfos, metaPath);

                File.Copy(tempPath, exportPath, overwrite: true);
                onProgress(100);

                // Cleanup temp files
                File.Delete(stitchedPath);
                File.Delete(tempPath);

                return exportPath;
            }
            catch
            {
                // Cleanup on failure
                if (File.Exists(stitchedPath)) File.Delete(stitchedPath);
                if (File.Exists(tempPath)) File.Delete(tempPath);
                throw;
            }
        }

        private async Task ExportMetadataAsync(List<VideoClipInfo> clipInfos, string metaPath)
        {
            var source = clipInfos.FirstOrDefault(c => Guid.TryParse(c.Source, out _))?.Source;

            var jsonData = new
            {
                Source = Guid.TryParse(source, out _) ? Guid.Parse(source) : Guid.NewGuid(),
                ClipInfos = clipInfos
            };

            string json = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
            await File.WriteAllTextAsync(metaPath, json);
        }
    }

    public class VideoMetadataService
    {
        public VideoClipInfo ExtractClipInfo(VideoRowData rowData, string resolution, int rowIndex)
        {
            var info = new VideoClipInfo
            {
                Path = rowData.VideoPath,
                Prompt = rowData.Prompt,
                Resolution = resolution,
                Duration = VideoUtils.GetVideoDuration(rowData.VideoPath),
                RowIndex = rowIndex,
                Source = Path.GetFileNameWithoutExtension(rowData.ImagePath)
            };

            if (!string.IsNullOrEmpty(rowData.WorkflowJson))
            {
                PopulateFromWorkflow(info, rowData.WorkflowJson, rowData.Prompt);
            }

            return info;
        }

        private void PopulateFromWorkflow(VideoClipInfo info, string workflowJson, string runtimePrompt)
        {
            try
            {
                var workflow = JObject.Parse(workflowJson);

                // Extract various metadata from workflow JSON
                ExtractPromptsFromWorkflow(info, workflow, runtimePrompt);
                ExtractSamplingParameters(info, workflow);
                ExtractModelInfo(info, workflow);
                ExtractAdditionalParameters(info, workflow);
            }
            catch (Exception)
            {
                // Use defaults if parsing fails
                SetDefaultValues(info);
            }
        }

        private void ExtractPromptsFromWorkflow(VideoClipInfo info, JObject workflow, string runtimePrompt)
        {
            var clipEncodes = workflow.Properties()
                .Select(p => p.Value as JObject)
                .Where(n => n?["class_type"]?.ToString() == "CLIPTextEncode");

            var positiveNode = clipEncodes.FirstOrDefault(n =>
                n["inputs"]["text"].ToString().Contains("{PROMPT}"));

            if (positiveNode != null)
            {
                info.Prompt = positiveNode["inputs"]["text"].ToString().Replace("{PROMPT}", runtimePrompt);
            }

            var negativeNode = clipEncodes.FirstOrDefault(n =>
                !n["inputs"]["text"].ToString().Contains("{PROMPT}"));

            if (negativeNode != null)
            {
                info.NegativePrompt = negativeNode["inputs"]["text"].ToString();
            }
        }

        private void ExtractSamplingParameters(VideoClipInfo info, JObject workflow)
        {
            var ksamplers = workflow.Properties()
                .Select(p => p.Value as JObject)
                .Where(n => n?["class_type"]?.ToString() == "KSamplerAdvanced");

            if (ksamplers.Any())
            {
                var primaryKS = ksamplers.First()["inputs"];
                info.Seed = int.TryParse(primaryKS["noise_seed"]?.ToString(), out int seed) ? seed : null;
                info.CFGScale = float.TryParse(primaryKS["cfg"]?.ToString(), out float cfg) ? cfg : 1.0f;
                info.Steps = int.TryParse(primaryKS["steps"]?.ToString(), out int steps) ? steps : 20;
                info.Sampler = primaryKS["sampler_name"]?.ToString() ?? "euler_ancestral";
                info.Parameters["Scheduler"] = primaryKS["scheduler"]?.ToString() ?? "simple";
            }
        }

        private void ExtractModelInfo(VideoClipInfo info, JObject workflow)
        {
            var models = workflow.Properties()
                .Select(p => p.Value as JObject)
                .Where(n => n?["class_type"]?.ToString() == "UNETLoader")
                .Select(n => n["inputs"]["unet_name"]?.ToString())
                .Where(name => !string.IsNullOrEmpty(name));

            if (models.Any())
            {
                info.Model = string.Join(", ", models);
            }

            // Extract LoRAs
            var loras = workflow.Properties()
                .Select(p => p.Value as JObject)
                .Where(n => n?["class_type"]?.ToString() == "LoraLoaderModelOnly")
                .Select(n => n["inputs"]["lora_name"]?.ToString())
                .Where(name => !string.IsNullOrEmpty(name));

            if (loras.Any())
            {
                info.Parameters["LoRAs"] = string.Join(", ", loras);
            }

            // Extract VAE
            var vaes = workflow.Properties()
                .Select(p => p.Value as JObject)
                .Where(n => n?["class_type"]?.ToString() == "VAELoader")
                .Select(n => n["inputs"]["vae_name"]?.ToString())
                .Where(name => !string.IsNullOrEmpty(name));

            if (vaes.Any())
            {
                info.Parameters["VAE"] = vaes.First();
            }
        }

        private void ExtractAdditionalParameters(VideoClipInfo info, JObject workflow)
        {
            // Extract workflow type
            var nodeTypes = workflow.Properties()
                .Select(p => p.Value as JObject)
                .Select(n => n?["class_type"]?.ToString())
                .Where(type => !string.IsNullOrEmpty(type));

            if (nodeTypes.Contains("WanImageToVideo"))
            {
                info.Parameters["Type"] = "i2v";
            }
            else if (nodeTypes.Contains("WanFirstLastFrameToVideo"))
            {
                info.Parameters["Type"] = "flf";
            }
            else
            {
                info.Parameters["Type"] = "unknown";
            }

            // Extract FPS from CreateVideo nodes
            var createVideos = workflow.Properties()
                .Select(p => p.Value as JObject)
                .Where(n => n?["class_type"]?.ToString() == "CreateVideo");

            if (createVideos.Any())
            {
                info.Parameters["FPS"] = createVideos.First()["inputs"]["fps"]?.ToString() ?? "16";
            }

            // Extract shift from ModelSamplingSD3
            var modelSamplings = workflow.Properties()
                .Select(p => p.Value as JObject)
                .Where(n => n?["class_type"]?.ToString() == "ModelSamplingSD3");

            if (modelSamplings.Any())
            {
                info.Parameters["Shift"] = modelSamplings.First()["inputs"]["shift"]?.ToString() ?? "5";
            }
        }

        private void SetDefaultValues(VideoClipInfo info)
        {
            info.NegativePrompt = "default negative";
            info.Seed = null;
            info.Model = "default";
            info.CFGScale = 1.0f;
            info.Steps = 20;
            info.Sampler = "euler_ancestral";
            info.ClipSkip = 1;

            // Ensure Parameters is initialized
            if (info.Parameters == null)
                info.Parameters = new Dictionary<string, string>();

            info.Parameters["Type"] = "unknown";
        }
    }

    public class FileManagementService
    {
        private readonly string _tempDir;

        public FileManagementService(string tempDir)
        {
            _tempDir = tempDir;
            Directory.CreateDirectory(_tempDir);
        }

        public void CleanupTempFiles(List<string> tempFiles)
        {
            foreach (string file in tempFiles.Where(File.Exists))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception)
                {
                    // Ignore cleanup failures
                }
            }
        }

        public string GetTempFilePath(string extension = ".tmp")
        {
            return Path.Combine(_tempDir, Path.GetRandomFileName() + extension);
        }

        public string TempDir => _tempDir;
    }

    public class UIUpdateService
    {
        public void UpdateProgressBar(ProgressBar progressBar, int percentage)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action(() => progressBar.Value = Math.Min(percentage, 100)));
            }
            else
            {
                progressBar.Value = Math.Min(percentage, 100);
            }
        }

        public void UpdateLabel(Label label, string text)
        {
            if (label.InvokeRequired)
            {
                label.Invoke(new Action(() => label.Text = text));
            }
            else
            {
                label.Text = text;
            }
        }
    }
}

namespace iviewer.Helpers
{
    public static class GridInitializer
    {
        public static void Initialize(DataGridView grid)
        {
            grid.Columns.Clear();

            // Image column
            var colImage = new DataGridViewImageColumn
            {
                Name = "colImage",
                HeaderText = "Image",
                Width = 150,
                ImageLayout = DataGridViewImageCellLayout.Zoom
            };
            grid.Columns.Add(colImage);

            // Prompt column
            var colPrompt = new DataGridViewTextBoxColumn
            {
                Name = "colPrompt",
                HeaderText = "Prompt",
                Width = 300
            };
            colPrompt.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            colPrompt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
            grid.Columns.Add(colPrompt);

            // LoRA column
            var colLora = new DataGridViewButtonColumn
            {
                Name = "colLora",
                HeaderText = "LoRA",
                Text = "Select LoRA",
                Width = 100,
                UseColumnTextForButtonValue = true
            };
            grid.Columns.Add(colLora);

            // Generate column
            var colGenerate = new DataGridViewButtonColumn
            {
                Name = "colGenerate",
                HeaderText = "Action",
                Text = "Generate",
                Width = 100,
                UseColumnTextForButtonValue = false
            };
            grid.Columns.Add(colGenerate);

            // Grid settings
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.RowTemplate.Height = 160;
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
        }
    }

    public static class ImageHelper
    {
        public static Bitmap CreateThumbnail(string imagePath, int width, int height)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException($"Image file not found: {imagePath}");
                }

                using var originalImage = System.Drawing.Image.FromFile(imagePath);
                var thumbnail = new Bitmap(width, height);

                using var graphics = Graphics.FromImage(thumbnail);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                // Calculate aspect ratio to maintain proportions
                float sourceAspect = (float)originalImage.Width / originalImage.Height;
                float destAspect = (float)width / height;

                int drawWidth, drawHeight, drawX, drawY;

                if (sourceAspect > destAspect)
                {
                    // Source is wider - fit to width
                    drawWidth = width;
                    drawHeight = (int)(width / sourceAspect);
                    drawX = 0;
                    drawY = (height - drawHeight) / 2;
                }
                else
                {
                    // Source is taller - fit to height
                    drawHeight = height;
                    drawWidth = (int)(height * sourceAspect);
                    drawY = 0;
                    drawX = (width - drawWidth) / 2;
                }

                graphics.Clear(Color.White); // Set background color
                graphics.DrawImage(originalImage, new Rectangle(drawX, drawY, drawWidth, drawHeight));

                return thumbnail;
            }
            catch (Exception ex)
            {
                // Return a placeholder thumbnail on error
                var errorThumbnail = new Bitmap(width, height);
                using var graphics = Graphics.FromImage(errorThumbnail);
                graphics.Clear(Color.LightGray);
                graphics.DrawString("Error", SystemFonts.DefaultFont, Brushes.Red, 10, height / 2 - 10);
                Console.WriteLine($"Error creating thumbnail for {imagePath}: {ex.Message}");
                return errorThumbnail;
            }
        }
    }

    public static class ResolutionCalculator
    {
        public static (int Width, int Height) Calculate(string imagePath, string endImagePath = null)
        {
            double totalAspect = 0;
            int count = 0;
            var startingWidth = 0;
            var startingHeight = 0;

            using (var startImg = System.Drawing.Image.FromFile(imagePath))
            {
                startingWidth = startImg.Width;
                startingHeight = startImg.Height;
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

            double aspectRatio = totalAspect / count;
            int targetPixels = 720 * 512;
            double sqrtPixels = Math.Sqrt(targetPixels * aspectRatio);

            int targetWidth = (int)Math.Ceiling(sqrtPixels / 16.0) * 16;
            int targetHeight = (int)Math.Ceiling((targetWidth / aspectRatio) / 16.0) * 16;

            Debug.Print($"Resolution: {startingWidth}x{startingHeight} => {targetWidth}x{targetHeight}");

            return (targetWidth, targetHeight);
        }
    }

    public static class VideoPlayerHelper
    {
        public static FlowLayoutPanel CreateVideoButtonsPanel(
            List<string> videoPaths,
            List<VideoClipInfo> clipInfos,
            Action<int, string> onVideoClick,
            List<double> transitionDurations,
            Action<int, double> onTransitionDurationChanged,
            List<Button> buttonTracker = null)
        {
            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 400,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight
            };

            for (int i = 0; i < videoPaths.Count; i++)
            {
                if (!File.Exists(videoPaths[i])) continue;

                var info = clipInfos.Count > i ? clipInfos[i] : new VideoClipInfo();
                int currentIndex = i; // Capture for closure

                var btnPlay = new Button
                {
                    Text = $"Clip {i + 1}: {Path.GetFileNameWithoutExtension(videoPaths[i])}",
                    Width = 200,
                    Height = 50,
                    Margin = new Padding(5),
                    Tag = i
                };
                btnPlay.Click += (s, e) => onVideoClick(currentIndex, videoPaths[currentIndex]);

                flowPanel.Controls.Add(btnPlay);

                // Add to tracker if provided
                buttonTracker?.Add(btnPlay);

                // Add transition duration control (except for the last clip)
                if (i < videoPaths.Count - 1)
                {
                    var txtDuration = new TextBox
                    {
                        Size = new Size(40, 20),
                        Text = "0"
                    };

                    txtDuration.TextChanged += (s, e) => {
                        if (double.TryParse(txtDuration.Text, out double newDuration) && newDuration >= 0 && newDuration <= 5)
                        {
                            onTransitionDurationChanged(currentIndex, newDuration);
                            txtDuration.BackColor = Color.White;
                        }
                        else
                        {
                            txtDuration.BackColor = Color.LightPink;
                        }
                    };

                    flowPanel.Controls.Add(txtDuration);
                }
            }

            return flowPanel;
        }
    }
}