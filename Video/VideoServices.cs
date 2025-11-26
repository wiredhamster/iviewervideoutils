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
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace iviewer.Services
{
	internal class VideoGenerationService : ComfyUIServiceBase
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

			int frames = 81; // 101;

			var compatibility = GetLoraCompatibility(clip.Prompt);
			var selectedWorkflow = "";

			if (compatibility == "Wan 2.2 I2V")
			{
				selectedWorkflow = string.IsNullOrEmpty(endImagePath)
					? VideoGenerationConfig.I2vWorkflowPath
					: VideoGenerationConfig.FlfWorkflowPath;
			}
			else if (compatibility == "Wan 2.2 5B")
			{
				if (!string.IsNullOrEmpty(endImagePath))
				{
					clip.Status = "Failed";
					clip.Save();

					return "";
				}

				selectedWorkflow = VideoGenerationConfig.Wan22_5BWorkflowPath;
			}
			else
			{

				clip.Status = "Failed";
				clip.Save();

				return "";
			}

			string workflowJson = await PrepareWorkflowAsync(selectedWorkflow, clip.Prompt, clip.ImagePath, endImagePath, video.Width, video.Height, frames);

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


		private async Task<string> PrepareWorkflowAsync(string workflowPath, string prompt, string imagePath, string endImagePath, int width, int height, int frames)
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

			(workflowJson, prompt) = PrepareWorkflowLoras(workflowJson, prompt);

			// Replace placeholders
			return workflowJson
				.Replace("{PROMPT}", prompt)
				.Replace("{WIDTH}", width.ToString())
				.Replace("{HEIGHT}", height.ToString())
				.Replace("{FRAMES}", frames.ToString());
		}

		string GetLoraCompatibility(string prompt)
		{
			var compatibilityList = new List<string>();
			foreach (Match match in Regex.Matches(prompt, loraMatchPattern))
			{
				string key = match.Groups[1].Value;
				var lora = Lora.LoadFromKey(key);
				if (!compatibilityList.Contains(lora.Compatibility))
				{
					compatibilityList.Add(lora.Compatibility);
				}
			}

			var compatibility = "";
			if (compatibilityList.All(c => c.Contains("Wan 2.2 I2V")))
			{
				compatibility = "Wan 2.2 I2V";
			}
			else if (compatibilityList.All(c => c.Contains("Wan 2.2 5B")))
			{
				compatibility = "Wan 2.2 5B";
			}

			return compatibility;
		}

		(string workflowJson, string cleanPrompt) PrepareWorkflowLoras(string workflowJson, string prompt)
		{
			var index = 1;

			foreach (Match match in Regex.Matches(prompt, loraMatchPattern))
			{
				string key = match.Groups[1].Value;
				var lora = Lora.LoadFromKey(key);

				var strengthIndex = 1;

				if (lora.HighNoiseLora != "")
				{
					strengthIndex++;
					workflowJson = workflowJson
						.Replace($"{{LORA_HIGH{index}_NAME}}", lora.HighNoiseLora)
						.Replace($"{{LORA_HIGH{index}_STRENGTH}}", match.Groups[strengthIndex].ToString());
				}

				if (lora.LowNoiseLora != "")
				{
					strengthIndex++;
					workflowJson = workflowJson
						.Replace($"{{LORA_LOW{index}_NAME}}", lora.LowNoiseLora)
						.Replace($"{{LORA_LOW{index}_STRENGTH}}", match.Groups[strengthIndex].ToString());
				}
				index++;
			}

			// Remove all lora tags from the prompt
			string cleanPrompt = Regex.Replace(prompt, loraMatchPattern, "").Trim();

			// Clean up multiple spaces that might be left after removal
			cleanPrompt = Regex.Replace(cleanPrompt, @"\s+", " ");

			// We need to replace any remaining literals with default values to keep Comfy happy
			workflowJson = workflowJson
				.Replace("{LORA_HIGH1_NAME}", "Physics_WAN_v7.safetensors")
				.Replace("{LORA_HIGH1_STRENGTH}", "0")
				.Replace("{LORA_HIGH2_NAME}", "Physics_WAN_v7.safetensors")
				.Replace("{LORA_HIGH2_STRENGTH}", "0")
				.Replace("{LORA_HIGH3_NAME}", "Physics_WAN_v7.safetensors")
				.Replace("{LORA_HIGH3_STRENGTH}", "0")
				.Replace("{LORA_LOW1_NAME}", "Physics_WAN_v7.safetensors")
				.Replace("{LORA_LOW1_STRENGTH}", "0")
				.Replace("{LORA_LOW2_NAME}", "Physics_WAN_v7.safetensors")
				.Replace("{LORA_LOW2_STRENGTH}", "0")
				.Replace("{LORA_LOW3_NAME}", "Physics_WAN_v7.safetensors")
				.Replace("{LORA_LOW3_STRENGTH}", "0");

			return (workflowJson, cleanPrompt);
		}

		const string loraMatchPattern = @"<lora:([^:]+):(-?\d+(?:\.\d+)?)(?::(-?\d+(?:\.\d+)?))?>";

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

				// First pass: Generate all transitions and collect frame usage info
				var transitionInfos = new List<TransitionInfo>();
				for (int i = 0; i < clipStates.Count; i++)
				{
					var currentState = clipStates[i];
					var nextState = i == clipStates.Count - 1 ? clipStates[0] : clipStates[i + 1];

					TransitionInfo transitionInfo = await GenerateTransitionSegmentWithInfo(
						currentState, nextState, highQuality, tempFiles);

					transitionInfos.Add(transitionInfo);
				}

				// Second pass: Process clips with correct trimming from both sides
				for (int i = 0; i < clipStates.Count; i++)
				{
					var currentState = clipStates[i];

					// Get transition info for this clip
					var nextTransition = transitionInfos[i]; // Transition after this clip
					var prevTransitionIndex = i == 0 ? clipStates.Count - 1 : i - 1;
					var prevTransition = transitionInfos[prevTransitionIndex]; // Transition before this clip

					// Calculate total frames to drop from both ends
					int totalDropFirstFrames = currentState.TransitionDropFirstFrames + prevTransition.NextClipFramesUsed;
					int totalDropLastFrames = currentState.TransitionDropLastFrames + nextTransition.PrevClipFramesUsed;

					string processedClip = await ProcessClipForJunction(
						currentState, totalDropFirstFrames, totalDropLastFrames, highQuality, tempFiles);

					if (processedClip != currentState.VideoPath)
					{
						tempFiles.Add(processedClip);
					}

					// Join with the transition AFTER this clip
					if (!string.IsNullOrEmpty(nextTransition.TransitionSegment))
					{
						tempFiles.Add(nextTransition.TransitionSegment);
						var joinedClip = Path.Combine(video.WorkingDir, "joined_" + Guid.NewGuid().ToString() + ".mp4");
						await VideoUtils.ConcatenateVideoClipsAsync(
							new List<string> { processedClip, nextTransition.TransitionSegment }, joinedClip);
						tempFiles.Add(joinedClip);
						processedClips.Add(joinedClip);
					}
					else
					{
						processedClips.Add(processedClip);
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

		public class TransitionInfo
		{
			public string TransitionSegment { get; set; }
			public int PrevClipFramesUsed { get; set; }  // How many frames from end of prev clip are in transition
			public int NextClipFramesUsed { get; set; }  // How many frames from start of next clip are in transition
		}

		private static async Task<string> ProcessClipForJunction(
			ClipGenerationState clipState,
			int dropFirstFrames,
			int dropLastFrames,
			bool highQuality,
			List<string> tempFiles)
		{
			var path = clipState.VideoPath;

			if (dropLastFrames <= 0 && dropFirstFrames <= 0)
			{
				return path; // No processing needed
			}

			// Probe to get duration and frame count
			var mediaInfo = await FFProbe.AnalyseAsync(path);
			double durationSec = mediaInfo.Duration.TotalSeconds;
			double inputFps = mediaInfo.PrimaryVideoStream.FrameRate;
			int totalFrames = (int)(durationSec * inputFps) + 1;

			int totalDropFrames = dropFirstFrames + dropLastFrames;
			if (totalDropFrames >= totalFrames)
			{
				throw new ArgumentException($"Total drop frames ({totalDropFrames}) exceed total frames ({totalFrames}) for clip: {path}");
			}

			// Calculate start time and trim duration
			double startTimeSec = dropFirstFrames / inputFps;
			double endTimeSec = durationSec - ((dropLastFrames + 1) / inputFps);
			double trimDurationSec = endTimeSec - startTimeSec;

			// Trim to temp file
			string trimmedClip = Path.Combine(clipState.WorkingDir, $"trimmed_{Guid.NewGuid()}.mp4");
			var trimArgs = FFMpegArguments
				.FromFileInput(path)
				.OutputToFile(trimmedClip, true, options =>
				{
					if (dropFirstFrames > 0)
					{
						options.WithCustomArgument($"-ss {startTimeSec}");
					}
					options.WithCustomArgument($"-t {trimDurationSec}");
					options.WithVideoCodec("libx264");
					options.WithCustomArgument("-an");
					options.WithFramerate(inputFps);
				});

			await trimArgs.ProcessAsynchronously();
			tempFiles.Add(trimmedClip);

			return trimmedClip;
		}

		private static async Task<TransitionInfo> GenerateTransitionSegmentWithInfo(
			ClipGenerationState prevClip,
			ClipGenerationState nextClip,
			bool highQuality,
			List<string> tempFiles)
		{
			var result = new TransitionInfo
			{
				TransitionSegment = string.Empty,
				PrevClipFramesUsed = 0,
				NextClipFramesUsed = 0
			};

			if (prevClip.TransitionType.Equals("None", StringComparison.OrdinalIgnoreCase))
			{
				return result;
			}

			// Get clip info
			var prevMediaInfo = await FFProbe.AnalyseAsync(prevClip.VideoPath);
			var nextMediaInfo = await FFProbe.AnalyseAsync(nextClip.VideoPath);

			var prevStream = prevMediaInfo.PrimaryVideoStream;
			var nextStream = nextMediaInfo.PrimaryVideoStream;

			if (prevStream == null || nextStream == null)
			{
				return result;
			}

			double inputFps = prevStream.FrameRate;
			int totalPrevFrames = (int)Math.Ceiling(prevMediaInfo.Duration.TotalSeconds * inputFps);
			int totalNextFrames = (int)Math.Ceiling(nextMediaInfo.Duration.TotalSeconds * inputFps);

			string extractDir = Path.GetDirectoryName(prevClip.VideoPath);

			if (prevClip.TransitionType.Equals("Interpolate", StringComparison.OrdinalIgnoreCase))
			{
				result = await GenerateInterpolateTransition(prevClip, nextClip, highQuality, tempFiles,
					totalPrevFrames, totalNextFrames, inputFps);
			}
			else if (prevClip.TransitionType.Equals("Redirect", StringComparison.OrdinalIgnoreCase))
			{
				result = await GenerateRedirectTransition(prevClip, nextClip, highQuality, tempFiles,
					totalPrevFrames, totalNextFrames, inputFps);
			}
			else if (prevClip.TransitionType.Equals("Fade", StringComparison.OrdinalIgnoreCase))
			{
				result.TransitionSegment = await GenerateFadeTransition(prevClip, nextClip, inputFps, tempFiles);
			}

			return result;
		}

		private static async Task<string> GenerateFadeTransition(
			ClipGenerationState prevClip,
			ClipGenerationState nextClip,
			double inputFps,
			List<string> tempFiles)
		{
			string transitionClip = Path.Combine(prevClip.WorkingDir, $"transition_{Guid.NewGuid()}.mp4");
			double offset = prevClip.TransitionDuration;

			var fadeArgs = FFMpegArguments
				.FromFileInput(prevClip.VideoPath)
				.AddFileInput(nextClip.VideoPath)
				.OutputToFile(transitionClip, true, options =>
				{
					options.WithCustomArgument($"-filter_complex \"[0:v][1:v]xfade=transition=fade:duration={prevClip.TransitionDuration}:offset={offset},settb=1/1000000\"");
					options.WithVideoCodec("libx264");
					options.WithCustomArgument("-an");
					options.WithFramerate(inputFps);
					options.WithCustomArgument($"-t {prevClip.TransitionDuration * 2}");
				});

			await fadeArgs.ProcessAsynchronously();
			tempFiles.Add(transitionClip);

			return transitionClip;
		}

		private static async Task<TransitionInfo> GenerateInterpolateTransition(
			ClipGenerationState prevClip,
			ClipGenerationState nextClip,
			bool highQuality,
			List<string> tempFiles,
			int totalPrevFrames,
			int totalNextFrames,
			double inputFps)
		{
			var result = new TransitionInfo();

			var extractDir = prevClip.WorkingDir;

			int numInterpFrames = prevClip.TransitionAddFrames;
			int interpFactor = numInterpFrames + 1;

			// Calculate available frames AFTER respecting drop frames
			int availablePrevFrames = totalPrevFrames - prevClip.TransitionDropLastFrames;
			int availableNextFrames = totalNextFrames - nextClip.TransitionDropFirstFrames;

			int requiredFrames = interpFactor * 3;

			// Use 3 reference frames from each clip
			if (availablePrevFrames < requiredFrames || availableNextFrames < requiredFrames)
			{
				return result;
			}

			var frames = new List<string>();

			// Comment is wrong.
			// Extract 3 frames from END of prev clip (BEFORE the dropped frames)
			// If TransitionDropLastFrames = 1, and totalPrevFrames = 81, then we use frames 77, 78, 79 (indices 77, 78, 79)
			// Frame 80 (index 80) is dropped
			var prevIndices = new List<int>
	{
		availablePrevFrames - 3 * interpFactor,
		availablePrevFrames - 2 * interpFactor,
		availablePrevFrames - interpFactor
	};

			foreach (var idx in prevIndices)
			{
				string frame = await VideoUtils.ExtractFrameAtAsync(prevClip.VideoPath, idx, extractDir, inputFps);
				frames.Add(frame);
				tempFiles.Add(frame);
			}

			// Comment is wrong.
			// Extract 3 frames from START of next clip (AFTER the dropped frames)
			// If TransitionDropFirstFrames = 1, and totalNextFrames = 81, then we use frames 1, 2, 3 (indices 1, 2, 3)
			// Frame 0 (index 0) is dropped
			var nextIndices = new List<int>
	{
		nextClip.TransitionDropFirstFrames,
		nextClip.TransitionDropFirstFrames + 1 * interpFactor,
		nextClip.TransitionDropFirstFrames + 2 * interpFactor
	};

			foreach (var idx in nextIndices)
			{
				string frame = await VideoUtils.ExtractFrameAtAsync(nextClip.VideoPath, idx, extractDir, inputFps);
				frames.Add(frame);
				tempFiles.Add(frame);
			}

			result.PrevClipFramesUsed = numInterpFrames; // This is the number of frames used from the first clip, excluding DropLastFrames. It uses a multiplier of 2 because of how prevInices works compared to nextIndices.
			result.NextClipFramesUsed = 0; // This is the number of frames used from the second clip, excluding DropFirstFrames. (Interpolation is between frame 81 - interpFactor of clip 1 and frame 0 of clip 2).

			// Create bridge video and interpolate
			var interpolatedClip = await CreateInterpolatedClip(
				frames, prevClip, highQuality, tempFiles,
				interpFactor, inputFps);

			tempFiles.Add(interpolatedClip);

			var startFrame = interpFactor * 2;
			result.TransitionSegment = await TrimClip(interpolatedClip, startFrame, numInterpFrames, inputFps, prevClip.WorkingDir, tempFiles);

			return result;
		}

		private static async Task<TransitionInfo> GenerateRedirectTransition(
			ClipGenerationState prevClip,
			ClipGenerationState nextClip,
			bool highQuality,
			List<string> tempFiles,
			int totalPrevFrames,
			int totalNextFrames,
			double inputFps)
		{
			var result = new TransitionInfo();

			var extractDir = prevClip.WorkingDir;

			int addFrames = prevClip.TransitionAddFrames;
			int interpFactor = addFrames + 1;

			// Calculate available frames AFTER respecting drop frames
			int availablePrevFrames = totalPrevFrames - prevClip.TransitionDropLastFrames;
			int availableNextFrames = totalNextFrames - nextClip.TransitionDropFirstFrames;

			// Calculate frame positions with increasing gaps (within available frames)
			var prevFrameIndices = CalculateRedirectFrameIndices(availablePrevFrames, addFrames, false);
			var nextFrameIndices = CalculateRedirectFrameIndices(availableNextFrames, addFrames, true);

			// Adjust next indices to account for dropped first frames
			nextFrameIndices = nextFrameIndices.Select(idx => idx + nextClip.TransitionDropFirstFrames).ToList();

			// Validate we have enough frames
			if (prevFrameIndices.Any(idx => idx < 0 || idx >= totalPrevFrames) ||
				nextFrameIndices.Any(idx => idx < 0 || idx >= totalNextFrames))
			{
				return result;
			}

			var frames = new List<string>();

			// Extract frames from prev clip (these will be cut from the end of prev clip)
			foreach (var frameIndex in prevFrameIndices)
			{
				string frame = await VideoUtils.ExtractFrameAtAsync(prevClip.VideoPath, frameIndex, extractDir, inputFps);
				frames.Add(frame);
				tempFiles.Add(frame);
			}

			// Extract frames from next clip (these will be cut from the start of next clip)
			foreach (var frameIndex in nextFrameIndices)
			{
				string frame = await VideoUtils.ExtractFrameAtAsync(nextClip.VideoPath, frameIndex, extractDir, inputFps);
				frames.Add(frame);
				tempFiles.Add(frame);
			}

			// Create bridge video and interpolate - output will replace the extracted frames
			result.PrevClipFramesUsed = availablePrevFrames - prevFrameIndices.Skip(1).First();
			result.NextClipFramesUsed = nextFrameIndices.OrderByDescending(i => i).Skip(1).First() + 1;

			var interpolatedClip = await CreateInterpolatedClip(
				frames, prevClip, highQuality, tempFiles,
				interpFactor, inputFps);

			tempFiles.Add(interpolatedClip);

			//var info = FFProbe.Analyse(interpolatedClip);

			var startFrame = result.PrevClipFramesUsed;
			var numFrames = (prevFrameIndices.Count - 1) * interpFactor + (nextFrameIndices.Count - 1) * interpFactor;
			result.TransitionSegment = await TrimClip(interpolatedClip, startFrame, numFrames, inputFps, prevClip.WorkingDir, tempFiles);

			return result;
		}

		private static List<int> CalculateRedirectFrameIndices(int availableFrames, int addFrames, bool forward)
		{
			var indices = new List<int>();
			int position = forward ? 0 : availableFrames - 1;

			indices.Add(position);

			for (int gap = 1; gap <= addFrames; gap++)
			{
				if (forward)
				{
					position += gap;
				}
				else
				{
					position -= gap;
				}
				indices.Add(position);
			}

			return indices.OrderBy(i => i).ToList();
		}

		private static async Task<string> CreateInterpolatedClip(
			List<string> frames,
			ClipGenerationState clipState,
			bool highQuality,
			List<string> tempFiles,
			int interpFactor,
			double inputFps)
		{
			// Build file list for concat
			string fileListPath = Path.Combine(clipState.WorkingDir, $"filelist_{DateTime.Now.Ticks}.txt");
			var fileListContent = frames.Select(f => $"file '{f.Replace("\\", "/")}'");
			await File.WriteAllLinesAsync(fileListPath, fileListContent);
			tempFiles.Add(fileListPath);

			// Calculate bridge FPS
			double bridgeFps = inputFps / interpFactor;
			int numInputFrames = frames.Count;
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
					options.WithCustomArgument($"-r {bridgeFps}");
					options.WithCustomArgument($"-vf \"setpts=N/{bridgeFps}/TB,fps={bridgeFps}\"");
					options.WithCustomArgument("-pix_fmt yuv420p");
				})
				.ProcessAsynchronously();

			tempFiles.Add(bridgeVideo);

			//var info = FFProbe.Analyse(bridgeVideo);

			// Interpolate
			string interpolatedClip;

			if (highQuality)
			{
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

			tempFiles.Add(interpolatedClip);

			//var info = FFProbe.Analyse(interpolatedClip);

			return interpolatedClip;
		}

		private static async Task<string> TrimClip(
			string inputClip,
			int startFrame,
			int numFrames,
			double inputFps,
			string workingDir,
			List<string> tempFiles)
		{
			if (startFrame == 0 && numFrames <= 0)
			{
				return inputClip; // No trimming needed
			}

			double startTime = startFrame / inputFps;
			double trimDuration = numFrames / inputFps;

			string trimmedClip = Path.Combine(workingDir, $"trimmed_{Guid.NewGuid()}.mp4");

			await FFMpegArguments
				.FromFileInput(inputClip, false, inputOptions =>
				{
					if (startFrame > 0)
					{
						inputOptions.WithCustomArgument($"-ss {startTime}");
					}
				})
				.OutputToFile(trimmedClip, true, options =>
				{
					if (numFrames > 0)
					{
						options.WithDuration(TimeSpan.FromSeconds(trimDuration));
					}
					options.WithVideoCodec("libx264");
					options.WithConstantRateFactor(23);
					options.WithCustomArgument($"-r {inputFps}");
					options.WithCustomArgument("-pix_fmt yuv420p");
					options.WithCustomArgument("-an");
				})
				.ProcessAsynchronously();

			tempFiles.Add(trimmedClip);

			return trimmedClip;
		}

		internal async Task DeleteAndCleanUp(VideoGenerationState videoGenerationState)
		{
			videoGenerationState.Save();

			try
			{
				Directory.Delete(videoGenerationState.TempDir, true);
			}
			catch (Exception ex) { }

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

					//int targetPixels = 720 * 512;
					int targetPixels = 800 * 600;
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
