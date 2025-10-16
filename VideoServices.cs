using CefSharp.Internals;
using FFMpegCore;
using FFMpegCore.Enums;
using iviewer;
using iviewer.Helpers;
using iviewer.Services;
using iviewer.Video;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace iviewer.Services
{
    internal class VideoGenerationService
    {
        private readonly HttpClient _httpClient;

        public VideoGenerationService()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8000") };

            // Ensure directories exist
            Directory.CreateDirectory(VideoGenerationConfig.ComfyOutputDir);
            Directory.CreateDirectory(VideoGenerationConfig.TempFileDir);
            Directory.CreateDirectory(VideoGenerationConfig.WorkingDir);
        }

        public void QueueClips(VideoGenerationState state)
        {
            state.Save();
            state.ResetClips();

            foreach (var clip in state.ClipGenerationStates)
            {
                if (clip.Status == "Queuing")
                {
                    clip.Status = "Queued";
                    clip.Save();

                    //EventBus.RaiseClipQueued(clip.PK);
                    EventBus.RaiseClipStatusChanged(clip.PK, clip.VideoGenerationStatePK, "Queued");
                }
            }

            if (state.ClipGenerationStates.Any(c => c.Status == "Failed"))
            {
                state.Status = "Failed";
            }
            else if (state.ClipGenerationStates.Any(c => c.Status == "Generating"))
            {
                state.Status = "Generating";
            }
            else if (state.ClipGenerationStates.Any(c => c.Status == "Queued"))
            {
                state.Status = "Queued";
            }
            else if (state.ClipGenerationStates.Any(c => c.Status == "Generated"))
            {
                state.Status = "Generated";
            }
            else
            {
                state.Status = "Unknown";
            }

            if (state.HasChanges)
            {
                state.Save();
                EventBus.RaiseVideoStatusChanged(state.PK, state.Status);
            }
        }

        public async Task<string> GenerateVideoAsync(ClipGenerationState clip)
        {
            var video = VideoGenerationState.Load(clip.VideoGenerationStatePK);
            var endImagePath = "";

            if (clip.ImagePath == "" || clip.Prompt == "")
            {
                clip.Status = "Failed";
                return "";
            }

            clip.VideoPath = "";

            var thisClip = false;
            foreach (var item in video.ClipGenerationStates.OrderBy(c => c.OrderIndex))
            {
                if (thisClip)
                {
                    endImagePath = item.ImagePath;
                    break;
                }

                if (item.PK == clip.PK)
                {
                    thisClip = true;
                }
            }

            string selectedWorkflow = string.IsNullOrEmpty(endImagePath)
                ? VideoGenerationConfig.I2vWorkflowPath
                : VideoGenerationConfig.FlfWorkflowPath;

            string workflowJson = await PrepareWorkflowAsync(selectedWorkflow, clip.Prompt, clip.ImagePath, endImagePath, video.Width, video.Height);

            clip.Status = "Generating";
            clip.Save();

            EventBus.RaiseClipStatusChanged(clip.PK, video.PK, clip.Status);

            var videoPath = await ExecuteWorkflowAsync(workflowJson, clip.TempDir); //, rowData, rowIndex);

            if (!string.IsNullOrEmpty(videoPath))
            {
                clip.WorkflowJson = workflowJson; // Store for metadata extraction
                clip.WorkflowPath = selectedWorkflow;
                clip.VideoPath = videoPath;
            }

            clip.Save();

            return videoPath;
        }

        public async Task<string> InterpolateAndAdjustSpeedAsync(string inputVideo, int fpsMultiplier, double speedFactor)
        {
            var tempFiles = new List<string>();

            var info = await FFProbe.AnalyseAsync(inputVideo);
            var inputFps = info.PrimaryVideoStream.FrameRate;

            double targetFps = inputFps * fpsMultiplier; // TODO: Why would this be different

            int rifeMultiplier = fpsMultiplier;

            if (speedFactor < 1.0) // Slow down
            {
                rifeMultiplier = (int)Math.Ceiling(fpsMultiplier / speedFactor);
                if (rifeMultiplier < 1) rifeMultiplier = 1;
                if (rifeMultiplier > 4) rifeMultiplier = 4; // Clamp for quality/speed
            }
            else if (speedFactor > 1.0) // Speed up
            {
                rifeMultiplier = speedFactor < fpsMultiplier ? fpsMultiplier : 1;
            }

            var interpolated = "";
            double interpolatedFps = inputFps * rifeMultiplier;

            if (rifeMultiplier == 1 && interpolatedFps == inputFps)
            {
                interpolated = inputVideo;
            }
            else
            {
                interpolated = await InterpolateVideoAsync(inputVideo, rifeMultiplier, interpolatedFps);
            }

            // Calculate PTS multiplier to go from interpolatedFps to targetFps
            //double ptsMultiplier = interpolatedFps / (speedFactor * targetFps);
            double ptsMultiplier = 1 / speedFactor;

            var outputVideo = interpolated;

            if (Math.Abs(ptsMultiplier - 1) > 0.001)
            {
                outputVideo = Path.Combine(Path.GetDirectoryName(inputVideo), $"speed_adjusted_{Guid.NewGuid()}.mp4");

                await FFMpegArguments
                        .FromFileInput(inputVideo)
                        .OutputToFile(outputVideo, true, options =>
                        {
                            options.WithVideoCodec("libx264");
                            options.WithConstantRateFactor(23);
                            options.WithCustomArgument($"-vf \"setpts={ptsMultiplier:F6}*PTS\"");
                            options.WithCustomArgument($"-r {targetFps}");
                            options.WithCustomArgument("-pix_fmt yuv420p");
                            options.WithCustomArgument("-an"); // No audio
                        })
                        .ProcessAsynchronously();
            }

            // Now delete the intermediate file.
            if (interpolated != inputVideo && interpolated != outputVideo)
            {
                File.Delete(interpolated);
            }

            //var mediaInfo = FFProbe.Analyse(outputVideo);

            return outputVideo;
        }

        async Task<string> InterpolateVideoAsync(string inputVideo, int rifeMultiplier, double targetFps)
        {
            string templatePath = VideoGenerationConfig.InterpolateWorkflowPath;
            string workflowJson = await File.ReadAllTextAsync(templatePath);
            var uploadedVideo = await UploadVideoAsync(inputVideo);

            // String replacements
            workflowJson = workflowJson.Replace("{RIFE_MULTIPLIER}", rifeMultiplier.ToString())
                                       .Replace("{TARGET_FPS}", targetFps.ToString("F1")) // 1 decimal for FPS
                                       .Replace("{INPUT_VIDEO}", uploadedVideo);

            // Validate JSON
            try
            {
                JObject.Parse(workflowJson);
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid JSON after replacements: {ex.Message}");
            }

            var videoPath = await ExecuteWorkflowAsync(workflowJson, Path.GetDirectoryName(inputVideo));

            //var mediaInfo = FFProbe.Analyse(videoPath);

            return videoPath;
        }

        async Task<string> UpscaleVideoAsync(string inputVideo)
        {
            string templatePath = VideoGenerationConfig.UpscaleWorkflowPath;
            string workflowJson = await File.ReadAllTextAsync(templatePath);
            var uploadedVideo = await UploadVideoAsync(inputVideo);

            // String replacements
            workflowJson = workflowJson.Replace("{INPUT_VIDEO}", uploadedVideo);

            // Validate JSON
            try
            {
                JObject.Parse(workflowJson);
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid JSON after replacements: {ex.Message}");
            }

            var videoPath = await ExecuteWorkflowAsync(workflowJson, Path.GetDirectoryName(inputVideo));

            //var mediaInfo = FFProbe.Analyse(videoPath);

            return videoPath;
        }


        private async Task<string> PrepareWorkflowAsync(string workflowPath, string prompt, string imagePath, string endImagePath, int width, int height)
        {
            string workflowJson = await File.ReadAllTextAsync(workflowPath);

            if (width == 0 || height == 0)
            {
                (width, height) = ResolutionCalculator.Calculate(imagePath);
            }

            // Upload images and get server filenames
            string startFilename = await UploadImageAsync(imagePath);
            workflowJson = workflowJson.Replace("{START_IMAGE}", startFilename);

            if (!string.IsNullOrEmpty(endImagePath))
            {
                string endFilename = await UploadImageAsync(endImagePath);
                workflowJson = workflowJson.Replace("{END_IMAGE}", endFilename);
            }

            // Replace placeholders
            return workflowJson
                .Replace("{PROMPT}", prompt)
                .Replace("{WIDTH}", width.ToString())
                .Replace("{HEIGHT}", height.ToString());
        }

        private async Task<string> UploadImageAsync(string imagePath)
        {
            try
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
            catch (Exception ex)
            {
                return null;
            }
        }

        private async Task<string> UploadVideoAsync(string videoPath)
        {
            try
            {
                string filename = Path.GetFileName(videoPath);

                var extension = Path.GetExtension(videoPath).ToLower();
                string contentType = extension == ".mp4" ? "video/mp4" :
                    extension == ".mov" ? "video/mov"
                    : "video/quicktime";

                using var fileStream = File.OpenRead(videoPath);
                using var multipartContent = new MultipartFormDataContent();

                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                multipartContent.Add(fileContent, "image", filename); // Key "video" for VHS extension

                var response = await _httpClient.PostAsync("/upload/image", multipartContent);
                string responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Upload failed for {filename}: {responseBody}");
                }

                var result = JObject.Parse(responseBody);
                return result["name"]?.ToString() ?? filename;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private async Task<string> ExecuteWorkflowAsync(string workflowJson, string outputDir)
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

            return await PollForCompletionAsync(promptId, outputDir);
        }

        private async Task<string> PollForCompletionAsync(string promptId, string outputDir)
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
                var promptData = statusJson[promptId];
                if (promptData == null)
                {
                    await Task.Delay(1000);
                    continue;
                }

                var status = promptData["status"];
                if (status?["completed"]?.Value<bool>() == true)
                {
                    return FindGeneratedVideo(outputDir);
                }
                else if (status?["status_str"]?.ToString() == "error" || HasExecutionError(status?["messages"]))
                {
                    return string.Empty; // Failure detected
                }

                await Task.Delay(1000);
            }
        }

        private bool HasExecutionError(JToken messagesToken)
        {
            if (messagesToken is JArray messages)
            {
                return messages.Any(msg => msg[0]?.ToString() == "execution_error");
            }
            return false;
        }

        // TODO: This should include the file pattern to look for
        // And should also be able to deal with images
        private string FindGeneratedVideo(string outputDir)
        {
            var tempVideos = Directory.GetFiles(VideoGenerationConfig.ComfyOutputDir, "*.mp4")
                .Select(f => new { Path = f, Time = File.GetCreationTime(f) })
                .OrderByDescending(x => x.Time)
                .FirstOrDefault();

            if (tempVideos == null)
            {
                throw new Exception("No video found in temp directory after completion");
            }

            string uniqueName = $"video_{Guid.NewGuid().ToString()}.mp4";
            string finalPath = Path.Combine(outputDir, uniqueName);
            File.Move(tempVideos.Path, finalPath);

            // Delete any associated .png file.
            var pngFile = Path.Combine(Path.GetDirectoryName(tempVideos.Path), Path.GetFileNameWithoutExtension(tempVideos.Path) + ".png");
            if (File.Exists(pngFile))
            {
                File.Delete(pngFile);
            }

            return Path.GetFullPath(finalPath);
        }

        internal async Task<string> ExportVideo(VideoGenerationState video)
        {
            var path = await StitchVideos(video, true);
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            return path;
        }

        internal async Task<string> StitchVideos(VideoGenerationState video, bool highQuality)
        {
            try
            {
                var clipStates = video.ClipGenerationStates
                    .Where(r => !string.IsNullOrEmpty(r.VideoPath) && File.Exists(r.VideoPath))
                    .OrderBy(c => c.OrderIndex)
                    .ToList();
                if (!clipStates.Any())
                {
                    return "";
                }

                var tempFiles = new List<string>(); // For cleanup
                var processedClips = new List<string>();

                // Create junction segments
                for (int i = 0; i < clipStates.Count; i++)
                {
                    if (i < clipStates.Count - 1)
                    {
                        var currentState = clipStates[i];
                        var videoClip = clipStates[i].VideoPath;
                        string processedClip = await ProcessClipForJunction(videoClip, currentState, highQuality, tempFiles);
                        if (processedClip != videoClip)
                        {
                            tempFiles.Add(processedClip);
                        }

                        string transitionSegment = await GenerateTransitionSegment(processedClip, clipStates[i + 1].VideoPath, currentState, highQuality, tempFiles);
                        if (!string.IsNullOrEmpty(transitionSegment))
                        {
                            tempFiles.Add(transitionSegment);

                            var joinedClip = Path.Combine(video.WorkingDir, "joined_" + Guid.NewGuid().ToString() + ".mp4");
                            await VideoUtils.ConcatenateVideoClipsAsync(new List<string> { processedClip, transitionSegment }, joinedClip);

                            tempFiles.Add(joinedClip);

                            processedClips.Add(joinedClip);
                        }
                        else
                        {
                            processedClips.Add(processedClip);
                        }
                    }
                    else
                    {
                        // Last clip
                        // On balance it is better to ignore Drop Frames for the last clip. Otherwise we have to automatically handle deciding if drop frames are genuinely required, or just the default values
                        processedClips.Add(clipStates[i].VideoPath);
                    }
                }

                // Upscale clips
                if (highQuality)
                {
                    var hqClips = new List<string>();
                    foreach (var clipPath in processedClips)
                    {
                        var hqPath = await UpscaleVideoAsync(clipPath);
                        hqClips.Add(hqPath);
                        tempFiles.Add(hqPath);
                    }

                    processedClips = hqClips;
                }

                // Process clips for speed adjustments
                var adjustedPaths = new List<string>();
                for (var i = 0; i < processedClips.Count; i++)
                {
                    var clip = processedClips[i];
                    var clipSpeed = clipStates[i].ClipSpeed;

                    string adjusted = await VideoUtils.AdjustSpeedAsync(clip, clipSpeed, highQuality);
                    adjustedPaths.Add(adjusted);
                    if (adjusted != clip)
                    {
                        tempFiles.Add(adjusted);
                    }
                }

                processedClips = adjustedPaths;

                var outputPath = Path.Combine(video.WorkingDir, $"Stitched_{DateTime.Now.Ticks}.mp4");
                await VideoUtils.ConcatenateVideoClipsAsync(processedClips, outputPath);

                //var mediaInfo = FFProbe.Analyse(outputPath);

                if (highQuality)
                {
                    // Let's check the video.
                    //throw new Exception("Didnt work");
                }

                // Cleanup temps
                foreach (var temp in tempFiles.Where(File.Exists))
                {
                    try { File.Delete(temp); } catch { }
                }

                return outputPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in video stitching: {ex.Message}");
                return "";
            }
        }

        // Helper: Process (trim) clip based on next junction (e.g., drop frames for Interpolate)
        private static async Task<string> ProcessClipForJunction(string path, ClipGenerationState clipState, bool highQuality, List<string> tempFiles)
        {
            // TODO: we should default dropFrames = 1 for transition type 'None'.
        if (!clipState.TransitionType.Equals("Interpolate") || clipState.TransitionDropFrames <= 0)
        {
            return path; // No processing needed
        }

        // Probe to get duration and frame count
        var mediaInfo = await FFProbe.AnalyseAsync(path);
        double durationSec = mediaInfo.Duration.TotalSeconds;
        double inputFps = mediaInfo.PrimaryVideoStream.FrameRate;
        int totalFrames = (int)(durationSec * inputFps) + 1;

        if (clipState.TransitionDropFrames >= totalFrames)
        {
            throw new ArgumentException($"Drop frames exceed total frames for clip: {path}");
        }

        // Calculate trim duration
        double trimDurationSec = durationSec - ((clipState.TransitionDropFrames + 1) / inputFps);

        // Trim to temp file
        string trimmedClip = Path.Combine(clipState.WorkingDir, $"trimmed_{Guid.NewGuid()}.mp4");
        var trimArgs = FFMpegArguments
            .FromFileInput(path)
            .OutputToFile(trimmedClip, true, options =>
            {
                options.WithCustomArgument($"-t {trimDurationSec}");
                options.WithVideoCodec("libx264");
                options.WithCustomArgument("-an");
                options.WithFramerate(inputFps);
            });
        await trimArgs.ProcessAsynchronously();
        tempFiles.Add(trimmedClip);

        return trimmedClip;
    }

    // Helper: Generate transition segment based on type
    private static async Task<string> GenerateTransitionSegment(
        string prevClip,
        string nextClip,
        ClipGenerationState clipState,
        bool highQuality,
        List<string> tempFiles)
    {
        if (clipState.TransitionType.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty; // No segment
        }

        // Probe to get duration and frame count
        var mediaInfo = await FFProbe.AnalyseAsync(prevClip);
        double durationSec = mediaInfo.Duration.TotalSeconds;
        double inputFps = mediaInfo.PrimaryVideoStream.FrameRate;
        int totalFrames = (int)(durationSec * inputFps);

        string transitionClip = Path.Combine(clipState.WorkingDir, $"transition_{Guid.NewGuid()}.mp4");

        if (clipState.TransitionType.Equals("Fade", StringComparison.OrdinalIgnoreCase))
        {
            // Use xfade to create a short transition segment
            // TODO: This doesn't work. And I don't think it's the right approach to create a short transition segment. But I don't know how to stitch together otherwise.
            double offset = clipState.TransitionDuration; // Assuming prev end overlaps with next start
            var fadeArgs = FFMpegArguments
                .FromFileInput(prevClip)
                .AddFileInput(nextClip)
                .OutputToFile(transitionClip, true, options =>
                {
                    options.WithCustomArgument($"-filter_complex \"[0:v][1:v]xfade=transition=fade:duration={clipState.TransitionDuration}:offset={offset},settb=1/1000000\"");
                    options.WithVideoCodec("libx264");
                    options.WithCustomArgument("-an");
                    options.WithFramerate(inputFps);
                    options.WithCustomArgument($"-t {clipState.TransitionDuration * 2}"); // Approx length of transition
                });
            await fadeArgs.ProcessAsynchronously();
        }
        else if (clipState.TransitionType.Equals("Interpolate", StringComparison.OrdinalIgnoreCase))
        {
            int numInterpFrames = clipState.TransitionAddFrames;
            int interpFactor = numInterpFrames + 1; // If we want 2 interpolation frames then we need the increase the number of frames by 3.

            // Get total frames for prev and next (approximate via duration * fps)
            var prevMediaInfo = await FFProbe.AnalyseAsync(prevClip);
            var prevStream = prevMediaInfo.PrimaryVideoStream;
            if (prevStream == null) return string.Empty;

            double approxPrevTotalFrames = prevMediaInfo.Duration.TotalSeconds * prevStream.FrameRate;
            int totalPrevFrames = (int)Math.Ceiling(approxPrevTotalFrames);

            var nextMediaInfo = await FFProbe.AnalyseAsync(nextClip);
            var nextStream = nextMediaInfo.PrimaryVideoStream;
            if (nextStream == null) return string.Empty;

            double approxNextTotalFrames = nextMediaInfo.Duration.TotalSeconds * nextStream.FrameRate;
            int totalNextFrames = (int)Math.Ceiling(approxNextTotalFrames);

            if (totalPrevFrames < interpFactor || totalNextFrames < interpFactor)
            {
                // Not enough frames in clips; handle gracefully (e.g., fall back to simple concat or log error)
                return string.Empty;
            }

            string extractDir = Path.GetDirectoryName(prevClip); // Reuse prevClip dir for extracts

            // Extract the 4 keyframes. Note that it is a zero based index.
            int frame1Index = totalPrevFrames - numInterpFrames - 2; // numInterpFrames before the end
            string frame1 = await VideoUtils.ExtractFrameAtAsync(prevClip, frame1Index, extractDir, inputFps);
            tempFiles.Add(frame1);

            string frame2 = await VideoUtils.ExtractFrameAtAsync(prevClip, totalPrevFrames - 1, extractDir, inputFps); // Last frame
            tempFiles.Add(frame2);

            string frame3 = await VideoUtils.ExtractFrameAtAsync(nextClip, 0, extractDir, inputFps); // First frame
            tempFiles.Add(frame3);

            int frame4Index = numInterpFrames + 1;
            string frame4 = await VideoUtils.ExtractFrameAtAsync(nextClip, frame4Index, extractDir, inputFps);
            tempFiles.Add(frame4);

            // Build file list for concat (4 frames)
            string fileListPath = Path.Combine(extractDir, $"filelist_{DateTime.Now.Ticks}.txt");
            var fileListContent = new List<string>
                    {
                        $"file '{frame1.Replace("\\", "/")}'",
                        $"file '{frame2.Replace("\\", "/")}'",
                        $"file '{frame3.Replace("\\", "/")}'",
                        $"file '{frame4.Replace("\\", "/")}'"
                    };

            await File.WriteAllLinesAsync(fileListPath, fileListContent);
            tempFiles.Add(fileListPath);

            // Calculate bridge FPS for slower spacing over longer duration (allows more interpolation blending before trimming middle)
            // Desired transition duration (output after trim)
            var bridgeFps = (double)inputFps / interpFactor;
            var numInputFrames = 4;
            var bridgeDuration = (double)numInputFrames / bridgeFps; // 4s for your test (even spacing, last frame held for full 1/bridgeFps)
            string bridgeVideo = Path.Combine(clipState.WorkingDir, $"bridge_{Guid.NewGuid()}.mp4");
                   
            await FFMpegArguments
                .FromFileInput(fileListPath, false, inputOptions =>
                {
                    inputOptions.ForceFormat("concat");
                    inputOptions.WithCustomArgument("-safe 0");
                })
                .OutputToFile(bridgeVideo, true, options =>
                {
                    options.WithVideoCodec("libx264");
                    options.WithConstantRateFactor(23);
                    options.WithCustomArgument($"-r {bridgeFps}"); // Output framerate for even spacing
                    options.WithCustomArgument($"-vf \"setpts=N/{bridgeFps}/TB,fps={bridgeFps}\""); // Retime for precise hold times (total ~{bridgeDuration}s)
                    options.WithCustomArgument("-pix_fmt yuv420p");
                    //options.WithCustomArgument("-vframes " + numInputFrames); // Exact input frame count
                })
                .ProcessAsynchronously();

            tempFiles.Add(bridgeVideo);

            //mediaInfo = await FFProbe.AnalyseAsync(bridgeVideo);

            var interpolatedClip = "";

            if (highQuality)
            {
                // Use Comfy UI workflow
                var service = new VideoGenerationService();
                interpolatedClip = await service.InterpolateVideoAsync(bridgeVideo, interpFactor, inputFps);

                if (string.IsNullOrEmpty(interpolatedClip))
                {
                    throw new Exception("Error interpolating video");
                }
            }
            else
            {
                interpolatedClip = Path.Combine(clipState.WorkingDir, $"interpolated_{Guid.NewGuid()}.mp4");

                // Interpolate to inputFps (preserves duration ~bridgeDuration)
                await FFMpegArguments
                    .FromFileInput(bridgeVideo)
                    .OutputToFile(interpolatedClip, true, options =>
                    {
                        options.WithCustomArgument("-v verbose");
                        options.WithCustomArgument($"-vf minterpolate=fps={inputFps}:mi_mode=mci:mc_mode=aobmc:vsbmc=1:scd=none");
                        options.WithVideoCodec("libx264");
                        options.WithConstantRateFactor(23);
                        options.WithCustomArgument("-pix_fmt yuv420p");
                        options.WithCustomArgument("-an");
                    })
                    .ProcessAsynchronously();
            }

            //mediaInfo = await FFProbe.AnalyseAsync(interpolatedClip);

            tempFiles.Add(interpolatedClip);

            // Trim interpolated clip
            mediaInfo = await FFProbe.AnalyseAsync(interpolatedClip);
            var interpStream = mediaInfo.PrimaryVideoStream;
            double approxInterpTotalFrames = mediaInfo.Duration.TotalSeconds * interpStream.FrameRate;
            int totalInterpFrames = (int)Math.Ceiling(approxInterpTotalFrames);
            string finalTransitionClip = transitionClip; // Default to interpolated if no trim needed

            if (totalInterpFrames > numInterpFrames)
            {
                int startFrame = 2 + numInterpFrames; // 2 key frames plus the interp frames in between
                double startTime = (double)startFrame / inputFps;
                double trimDuration = (double)numInterpFrames / inputFps;

                finalTransitionClip = Path.Combine(clipState.WorkingDir, $"transition_trimmed_{Guid.NewGuid()}.mp4");
                await FFMpegArguments
                    .FromFileInput(interpolatedClip, false, inputOptions =>
                    {
                        inputOptions.WithCustomArgument($"-ss {startTime}"); // Seek via custom -ss on input (fast approx)
                    })
                    .OutputToFile(finalTransitionClip, true, options =>
                    {
                        options.WithDuration(TimeSpan.FromSeconds(trimDuration)); // Exact duration enforcement
                        options.WithVideoCodec("libx264"); // Key fix: Re-encode for precise -t (no copy imprecision)
                        options.WithConstantRateFactor(23);
                        options.WithCustomArgument($"-r {inputFps}"); // Lock to exact framerate
                        options.WithCustomArgument("-pix_fmt yuv420p");
                        options.WithCustomArgument("-an");
                    })
                    .ProcessAsynchronously();

                tempFiles.Add(finalTransitionClip);
            }

            // Update mediaInfo and set transitionClip
            transitionClip = finalTransitionClip;

            //mediaInfo = await FFProbe.AnalyseAsync(transitionClip);
        }

        return transitionClip;
    }

    internal async Task DeleteAndCleanUp(VideoGenerationState videoGenerationState)
        {
            videoGenerationState.Save();

            Directory.Delete(videoGenerationState.TempDir, true);

            var tempFiles = new List<string>();
            foreach (var file in videoGenerationState.TempFiles.Split(','))
            {
                if (!string.IsNullOrEmpty(file) && File.Exists(file))
                {
                    tempFiles.Add(file);
                }
            }

            FileManagementService.CleanupTempFiles(tempFiles);

            var clipPKs = new HashSet<Guid>();
            var pk = videoGenerationState.PK;

            if (videoGenerationState != null)
            {
                videoGenerationState.ClipGenerationStates.ForEach(c => clipPKs.Add(c.PK));

                videoGenerationState.Delete();
                videoGenerationState = null;
            }

            EventBus.RaiseVideoDeleted(pk, clipPKs);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    internal class VideoMetadataService
    {
        public VideoClipInfo ExtractClipInfo(ClipGenerationState clipState)
        {
            var mediaInfo = FFProbe.Analyse(clipState.VideoPath);

            var info = new VideoClipInfo
            {
                Path = clipState.VideoPath,
                Prompt = clipState.Prompt,
                Resolution = $"{mediaInfo.PrimaryVideoStream.Width}x{mediaInfo.PrimaryVideoStream.Height}",
                Duration = mediaInfo.Duration.TotalSeconds,
                RowIndex = clipState.OrderIndex,
                Source = Path.GetFileNameWithoutExtension(clipState.ImagePath)
            };

            if (!string.IsNullOrEmpty(clipState.WorkflowJson))
            {
                PopulateFromWorkflow(info, clipState.WorkflowJson, clipState.Prompt);
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
        public static void CleanupTempFiles(List<string> tempFiles)
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

        public static string GetTempFilePath(string extension = ".tmp")
        {
            return Path.Combine(VideoGenerationConfig.TempFileDir, Path.GetRandomFileName() + extension);
        }
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

            // Queue column
            var colQueue = new DataGridViewButtonColumn
            {
                Name = "colQueue",
                HeaderText = "Action",
                Text = "Queue",
                Width = 100,
                UseColumnTextForButtonValue = false
            };
            grid.Columns.Add(colQueue);

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
        public static Bitmap CreateThumbnail(string imagePath, int? width, int height)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    return null;
                }
                using var originalImage = System.Drawing.Image.FromFile(imagePath);

                // Calculate thumbnail dimensions
                float sourceAspect = (float)originalImage.Width / originalImage.Height;
                int thumbWidth = width ?? (int)(height * sourceAspect); // Use provided width or calculate based on height
                int thumbHeight = height;

                var thumbnail = new Bitmap(thumbWidth, thumbHeight);
                using var graphics = Graphics.FromImage(thumbnail);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                if (width.HasValue)
                {
                    // Width provided: Fit to box with letterboxing if needed
                    float destAspect = (float)width.Value / height;
                    int drawWidth, drawHeight, drawX, drawY;
                    if (sourceAspect > destAspect)
                    {
                        // Source wider - fit to width
                        drawWidth = width.Value;
                        drawHeight = (int)(width.Value / sourceAspect);
                        drawX = 0;
                        drawY = (height - drawHeight) / 2;
                    }
                    else
                    {
                        // Source taller - fit to height
                        drawHeight = height;
                        drawWidth = (int)(height * sourceAspect);
                        drawY = 0;
                        drawX = (width.Value - drawWidth) / 2;
                    }
                    graphics.Clear(Color.White); // Set background color
                    graphics.DrawImage(originalImage, new Rectangle(drawX, drawY, drawWidth, drawHeight));
                }
                else
                {
                    // Width not provided: Scale to height, maintain aspect
                    graphics.DrawImage(originalImage, new Rectangle(0, 0, thumbWidth, thumbHeight));
                }

                return thumbnail;
            }
            catch (Exception ex)
            {
                // Return a placeholder thumbnail on error
                int thumbWidth = width ?? height; // Square fallback if width null
                var errorThumbnail = new Bitmap(thumbWidth, height);
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
        public static (int Width, int Height) Calculate(string imagePath)
        {
            try
            {
                double aspectRatio = 0;
                int count = 0;
                var startingWidth = 0;
                var startingHeight = 0;

                if (File.Exists(imagePath))
                {
                    using (var startImg = System.Drawing.Image.FromFile(imagePath))
                    {
                        startingWidth = startImg.Width;
                        startingHeight = startImg.Height;
                        aspectRatio = (double)startImg.Width / startImg.Height;
                    }

                    int targetPixels = 720 * 512;
                    double sqrtPixels = Math.Sqrt(targetPixels * aspectRatio);

                    int targetWidth = (int)Math.Ceiling(sqrtPixels / 16.0) * 16;
                    int targetHeight = (int)Math.Ceiling((targetWidth / aspectRatio) / 16.0) * 16;

                    Debug.Print($"Resolution: {startingWidth}x{startingHeight} => {targetWidth}x{targetHeight}");

                    return (targetWidth, targetHeight);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error calculating resolution: " + ex.Message);
            }

            return (0, 0);
        }
    }
}