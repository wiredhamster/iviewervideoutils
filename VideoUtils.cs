using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace iviewer
{
	public class VideoUtils
	{
		public static void ConfigureGlobalFFOptions()
		{
			GlobalFFOptions.Configure(options => options.BinaryFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg"));
		}

		public static async Task ProcessFolder(string folder, string action)
		{
			var inputPath = FileHandler.Instance.TranslatePath(folder);
			var outputPath = FileHandler.Instance.TranslatePath($"{folder}\\processed");

			var files = FileHandler.Instance.GetFiles(inputPath);
			foreach (var inputFile in files)
			{
				var outputFile = Path.Combine(outputPath, Path.GetFileName(inputFile));
				if (!FileHandler.Instance.Exists(outputFile))
				{
					try
					{
						bool result = false;
						switch (action.ToLower())
						{
							case "reverse":
								result = ReverseVideo(inputFile, outputFile);
								break;
							case "upscaleandinterpolate":
								result = UpscaleAndInterpolateVideo(inputFile, outputFile);
								break;
                            case "interpolate":
                                result = UpscaleAndInterpolateVideo(inputFile, outputFile, false, true);
                                break;
							case "extractframes":
								result = ExtractFrames(inputFile, outputPath);
								break;
						}

						if (result)
						{
							File.Delete(inputFile);
						}
					}
					catch (Exception ex)
					{
						FileHandler.WriteLog($"Error upscaling video {inputFile}. {ex.Message}");
					}
				}
			}
		}

		static bool ReverseVideo(string inputFile, string outputFile)
		{
			try
			{
				if (Path.GetExtension(inputFile).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
				{
					FFMpegArguments
					.FromFileInput(inputFile)
					.OutputToFile(outputFile, true, options => options
						.WithCustomArgument("-vf reverse -af areverse")) // Reverse video and audio
					.ProcessSynchronously();

					return true;
				}
				else
				{
					// Unsupported file type
					return false;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error: {ex.Message}");

				return false;
			}
		}

        public static async Task<bool> UpscaleAndInterpolateVideoAsync(string inputFile, string outputFile, bool upscale = true, bool interpolate = true)
        {
            try
            {
                var width = 0;
                var height = 0;
                var fps = 0;
                var args = new StringBuilder();
                var result = false;

                if (Path.GetExtension(inputFile).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    (width, height, fps, _) = VideoMetadataParser.GetVideoInfo(inputFile);
                    if (width > 0 && height > 0 && fps > 0)
                    {
                        var newHeight = height;
                        var newFps = fps;

                        // Calculate new height (upscale if <= 1080p)
                        if (height <= 1080 && upscale)
                        {
                            newHeight = height * 2;
                        }

                        // Calculate new fps (interpolate if < 30fps)
                        if (fps < 15 && interpolate)
                        {
                            newFps = fps * 3;
                        }
                        else if (fps < 30 && interpolate)
                        {
                            newFps = fps * 2;
                        }

                        // Build video filter arguments
                        if (newHeight != height)
                        {
                            args.Append($"scale=-1:{newHeight}");
                        }

                        if (newFps != fps)
                        {
                            if (args.Length > 0)
                            {
                                args.Append(",");
                            }
                            args.Append($"minterpolate=fps={newFps}");
                        }
                    }
                }
                else
                {
                    // Unsupported file type
                    return false;
                }

                if (args.Length > 0)
                {
                    // Process video with filters
                    var ffmpegArgs = $"-vf {args.ToString()}";
                    result = await FFMpegArguments
                        .FromFileInput(inputFile)
                        .OutputToFile(outputFile, true, options => options
                            .WithCustomArgument(ffmpegArgs))
                        .ProcessAsynchronously();
                }
                else
                {
                    // No processing required. Just copy the file.
                    await Task.Run(() => File.Copy(inputFile, outputFile, overwrite: true));
                }

                var mediaInfo = await FFProbe.AnalyseAsync(outputFile);
                Debug.WriteLine($"Transition clip: {mediaInfo.VideoStreams[0].FrameRate} fps, {mediaInfo.VideoStreams[0].Duration} duration");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing video: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Synchronous wrapper for UpscaleAndInterpolateVideoAsync
        /// </summary>
        /// <param name="inputFile">Path to the input video file</param>
        /// <param name="outputFile">Path to the output video file</param>
        /// <returns>True if processing succeeded, false otherwise</returns>
        public static bool UpscaleAndInterpolateVideo(string inputFile, string outputFile, bool upscale = true, bool interpolate = true)
        {
            return UpscaleAndInterpolateVideoAsync(inputFile, outputFile, upscale, interpolate).Result;
        }

        /// <summary>
        /// Extracts frames from an MP4 video file to a specified directory.
        /// </summary>
        /// <param name="inputVideoPath">The path to the input MP4 video file.</param>
        /// <param name="outputDirectory">The directory where extracted frames will be saved as PNG images.</param>
        /// <param name="frameRate">The frame rate for extraction (e.g., 1 for one frame per second). Use -1 to extract all frames.</param>
        static bool ExtractFrames(string inputVideoPath, string outputDirectory, double frameRate = -1.0)
		{
			if (!File.Exists(inputVideoPath))
			{
				throw new FileNotFoundException("Input video file not found.", inputVideoPath);
			}

			if (!Directory.Exists(outputDirectory))
			{
				Directory.CreateDirectory(outputDirectory);
			}

			var filename = Path.GetFileNameWithoutExtension(inputVideoPath);

			string outputPattern = Path.Combine(outputDirectory, filename + "_%05d.png"); // e.g., frame_00001.png

			FFMpegArguments
				.FromFileInput(inputVideoPath)
				.OutputToFile(outputPattern, false, options =>
				{
					if (frameRate > 0)
					{
						options.WithCustomArgument($"-r {frameRate}"); // Set extraction rate
					}
					options.WithVideoCodec(VideoCodec.Png); // Output as PNG
				})
				.ProcessSynchronously();

			return true;
		}

        private static async Task<string> AdjustSpeedAsync(string inputPath, double speed, bool highQuality = true)
        {
            if (Math.Abs(speed - 1.0) < 0.001) return inputPath; // No adjustment needed

            // Probe original FPS (assume consistent across clips)
            var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
            double originalFps = mediaInfo.PrimaryVideoStream.FrameRate;

            // Set uniform FPS (high for export quality)
            double uniformFps = originalFps;

            if (originalFps < 15 && highQuality)
            {
                uniformFps = originalFps * 6;
            }
            else if (originalFps < 30 && highQuality)
            {
                uniformFps = originalFps * 4;
            }

            // Create temp output path
            string tempOutput = Path.ChangeExtension(Path.GetTempFileName(), ".mp4");

            // Build custom filter string
            string vfArg = $"setpts=PTS/{speed}";
            double effectiveFps = originalFps * speed;

            if (speed <= 1.0)
            {
                // For slow/normal: setpts first (slows if <1), then interpolate up if needed
                vfArg += $",minterpolate=fps={uniformFps}";
            }
            else
            {
                // For fast: Interpolate to effective first, then setpts, then downsample if needed
                double interpFps = effectiveFps;
                vfArg = $"minterpolate=fps={interpFps},{vfArg}";
                effectiveFps = interpFps * speed;
                vfArg += $",fps={uniformFps}";
            }

            await FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile(tempOutput, true, opt =>
                {
                    if (!string.IsNullOrEmpty(vfArg))
                    {
                        opt.WithCustomArgument($"-vf \"{vfArg}\"");
                    }

                    // Force output FPS to uniform
                    opt.WithFramerate(uniformFps);

                    // No audio
                    opt.DisableChannel(Channel.Audio);
                })
                .ProcessAsynchronously();

            return tempOutput;
        }

        public static string ExtractLastFrame(string inputVideoPath, string outputDirectory, bool upscale = false)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(inputVideoPath))
                    throw new ArgumentException("Input video path cannot be empty");

                var filename = Path.GetFileNameWithoutExtension(inputVideoPath);
                string outputPath = Path.Combine(outputDirectory, $"{filename}_lastframe.png");

                if (string.IsNullOrWhiteSpace(outputPath))
                    throw new ArgumentException("Output path cannot be empty");

                if (!File.Exists(inputVideoPath))
                    throw new FileNotFoundException($"Input video file not found: {inputVideoPath}");

                // Create output directory if it doesn't exist
                string outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                return ExtractLastFrameWithFrameCount(inputVideoPath, outputPath, upscale);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting last frame: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Counts total frames and extracts the last one
        /// </summary>
        private static string ExtractLastFrameWithFrameCount(string inputVideoPath, string outputPath, bool upscale = false)
        {
            try
            {
                // Get video information
                var videoInfo = FFProbe.Analyse(inputVideoPath);
                var videoStream = videoInfo.PrimaryVideoStream;

                // Calculate total frames
                double fps = videoStream.FrameRate;
                double duration = videoInfo.Duration.TotalSeconds;
                long totalFrames = (long)(fps * duration);

                // Extract the last frame by frame number (0-indexed, so totalFrames-1)
                var success = FFMpegArguments
                    .FromFileInput(inputVideoPath)
                    .OutputToFile(outputPath, true, options => options
                        .WithCustomArgument($"-vf \"select=eq(n\\,{totalFrames - 1})\"") // Select specific frame
                        .WithCustomArgument("-vframes 1")   // Extract only 1 frame
                        .WithCustomArgument("-q:v 2")       // High quality
                        .WithVideoCodec("png"))             // PNG codec
                    .ProcessSynchronously();

                if (!success)
                {
                    return null;
                }

                if (upscale)
                {
                    outputPath = UpscaleImage(outputPath, Path.GetDirectoryName(outputPath), true);
                }

                return success ? outputPath : null;
            }
            catch
            {
                // Final fallback - use the original time-based approach
                var videoInfo = FFProbe.Analyse(inputVideoPath);
                double videoDuration = videoInfo.Duration.TotalSeconds;
                TimeSpan lastFrameTime = TimeSpan.FromSeconds(Math.Max(0, videoDuration - 0.033)); // One frame back at 30fps

                var success = FFMpeg.Snapshot(inputVideoPath, outputPath, captureTime: lastFrameTime);
                return success ? outputPath : null;
            }
        }

        public static string ExtractFirstFrame(string videoPath, string outputPath, bool upscale = false)
        {
            string tempPng = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetRandomFileName() + ".png");

            // Use FFMpegCore to extract the first frame
            FFMpegArguments.FromFileInput(videoPath)
                .OutputToFile(tempPng, true, options => options
                    .WithVideoCodec("png")
                    .Seek(TimeSpan.Zero)
                    .ForceFormat("image2")
                    .WithCustomArgument("-vframes 1")
                )
                .ProcessSynchronously();

            if (!File.Exists(tempPng))
            {
                throw new Exception("FFMpegCore failed to extract frame.");
            }

            if (upscale)
            {
                tempPng = UpscaleImage(tempPng, Path.GetDirectoryName(outputPath), true);
            }

            return tempPng;
        }


        static string UpscaleImage(string input, string outputPath, bool deleteInput = true)
        {
            string upscaledPath = Path.Combine(outputPath, $"{Path.GetFileNameWithoutExtension(input)}_upscaled.png");

            using (var image = SixLabors.ImageSharp.Image.Load(input))
            { 
                int newWidth = image.Width * 2;
                int newHeight = image.Height * 2;

                image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));  // Lanczos3 for high-quality sharp upscale; alternatives: Bicubic, Welch

                image.Save(upscaledPath);  // Save as PNG (or Jpeg for compression)
            }

            // Optional: Delete original extracted frame if not needed
            if (deleteInput)
            {
                File.Delete(input);
            }

            return upscaledPath;
        }

        // Helper to get duration (in seconds)
        public static double GetVideoDuration(string videoPath)
        {
            try
            {
                var mediaInfo = FFProbe.Analyse(videoPath);
                var duration = mediaInfo.Duration.TotalSeconds;

				return duration;
            }
            catch
            {
                return 0;
            }
        }

        public static async Task<bool> StitchVideosWithTransitionsAsync(
            List<VideoRowData> rowDatas,
            string outputPath,
            bool deleteIntermediateFiles = true,
            bool highQuality = true)
        {
            try
            {
                var validRows = rowDatas.Where(r => !string.IsNullOrEmpty(r.VideoPath) && File.Exists(r.VideoPath));
                if (!validRows.Any())
                {
                    return false;
                }
                else if (validRows.Count() == 1) 
                {
                    File.Copy(validRows.First().VideoPath, outputPath, true);
                    return true;
                }

                // Pre-process clips for speed adjustments
                var adjustedPaths = new List<string>();
                var tempFiles = new List<string>();
                foreach (var row in validRows)
                {
                    string adjusted = await AdjustSpeedAsync(row.VideoPath, row.ClipSpeed, highQuality);
                    adjustedPaths.Add(adjusted);
                    if (adjusted != row.VideoPath)
                    {
                        tempFiles.Add(adjusted);
                    }
                }

                // Create output dir
                string outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                // Optimization: If all durations=0 and specs match, use demuxer
                // TODO: This will crash if any video files are not valid.
                bool allZero = validRows.All(r => r.JunctionParameters.Type == TransitionType.None || (r.JunctionParameters.Type == TransitionType.Fade && r.JunctionParameters.DurationSeconds == 0.0));
                var mediaInfos = await Task.WhenAll(adjustedPaths.Select(p => FFProbe.AnalyseAsync(p)));
                bool sameSpecs = mediaInfos.All(info =>
                    info.PrimaryVideoStream.Width == mediaInfos[0].PrimaryVideoStream.Width &&
                    info.PrimaryVideoStream.Height == mediaInfos[0].PrimaryVideoStream.Height &&
                    info.PrimaryVideoStream.CodecName == mediaInfos[0].PrimaryVideoStream.CodecName);

                bool success;
                if (allZero && sameSpecs)
                {
                    success = await ConcatWithDemuxer(adjustedPaths, outputPath, false); // Don't delete yet
                }
                else
                {
                    success = await StitchVideos(adjustedPaths, outputPath, validRows.Select(r => r.JunctionParameters).ToList());
                }

                // Cleanup temps
                if (deleteIntermediateFiles && success)
                {
                    foreach (var temp in tempFiles.Where(File.Exists))
                    {
                        try { File.Delete(temp); } catch { }
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stitching videos with transitions: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> StitchVideosWithTransitionsAsync(
            List<string> inputVideoPaths,
            string outputPath,
            string transitionType,
            List<double> transitionDurations,
            List<double>? speeds = null,
            bool deleteIntermediateFiles = true,
            bool highQuality = true)
        {
            try
            {
                if (inputVideoPaths.Count == 1 && !string.IsNullOrEmpty(inputVideoPaths[0]) && File.Exists(inputVideoPaths[0]))
                {
                    File.Copy(inputVideoPaths[0], outputPath, true);
                    return true;
                }

                // Default speeds if not provided
                if (speeds == null || speeds.Count < inputVideoPaths.Count)
                {
                    speeds = Enumerable.Repeat(1.0, inputVideoPaths.Count).ToList();
                }

                // Pre-process clips for speed adjustments
                var adjustedPaths = new List<string>();
                var tempFiles = new List<string>();
                for (int i = 0; i < inputVideoPaths.Count; i++)
                {
                    string adjusted = await AdjustSpeedAsync(inputVideoPaths[i], speeds[i], highQuality);
                    adjustedPaths.Add(adjusted);
                    if (adjusted != inputVideoPaths[i])
                    {
                        tempFiles.Add(adjusted);
                    }
                }

                // Handle trimLastFrame on adjusted paths if transition duration is 0
                for (int i = 0; i < adjustedPaths.Count - 1; i++) // Skip last
                {
                    if (transitionDurations[i] == 0 && !string.IsNullOrEmpty(inputVideoPaths[0]) && File.Exists(inputVideoPaths[0]))
                    {
                        var info = await FFProbe.AnalyseAsync(adjustedPaths[i]);
                        double fps = info.PrimaryVideoStream.FrameRate;
                        double duration = info.Duration.TotalSeconds - (1.0 / fps);
                        string trimmedTemp = Path.ChangeExtension(Path.GetTempFileName(), ".mp4");
                        await FFMpegArguments
                            .FromFileInput(adjustedPaths[i])
                            .OutputToFile(trimmedTemp, true, options =>
                            {
                                options.WithDuration(TimeSpan.FromSeconds(duration));
                                options.CopyChannel();
                            })
                            .ProcessAsynchronously();
                        if (deleteIntermediateFiles && adjustedPaths[i] != inputVideoPaths[i])
                        {
                            tempFiles.Remove(adjustedPaths[i]); // Will delete original adjusted
                            File.Delete(adjustedPaths[i]);
                        }
                        adjustedPaths[i] = trimmedTemp;
                        tempFiles.Add(trimmedTemp);
                    }
                }

                // Pad transitionDurations with 0s if short/null
                if (transitionDurations == null)
                {
                    transitionDurations = new List<double>();
                }
                while (transitionDurations.Count < adjustedPaths.Count - 1)
                {
                    transitionDurations.Add(0.0);
                }

                // Create output dir
                string outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                // Optimization: If all durations=0 and specs match, use demuxer
                // TODO: This will crash if any video files are not valid.
                bool allZero = transitionDurations.All(d => d == 0.0);
                var mediaInfos = await Task.WhenAll(adjustedPaths.Select(p => FFProbe.AnalyseAsync(p)));
                bool sameSpecs = mediaInfos.All(info =>
                    info.PrimaryVideoStream.Width == mediaInfos[0].PrimaryVideoStream.Width &&
                    info.PrimaryVideoStream.Height == mediaInfos[0].PrimaryVideoStream.Height &&
                    info.PrimaryVideoStream.CodecName == mediaInfos[0].PrimaryVideoStream.CodecName);

                bool success;
                if (allZero && sameSpecs)
                {
                    success = await ConcatWithDemuxer(adjustedPaths, outputPath, false); // Don't delete yet
                }
                else
                {
                    success = await StitchWithTransitions(adjustedPaths, outputPath, transitionType, transitionDurations);
                }

                // Cleanup temps
                if (deleteIntermediateFiles && success)
                {
                    foreach (var temp in tempFiles.Where(File.Exists))
                    {
                        try { File.Delete(temp); } catch { }
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stitching videos with transitions: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> StitchWithTransitions(
            List<string> inputVideoPaths,
            string outputPath,
            string transitionType,
            List<double> transitionDurations)
        {
            try
            {
                var filterParts = new List<string>();
                var durations = new List<double>();
                // Probe durations and FPS
                var fpsList = new List<double>();
                for (int i = 0; i < inputVideoPaths.Count; i++)
                {
                    var mediaInfo = await FFProbe.AnalyseAsync(inputVideoPaths[i]);
                    durations.Add(mediaInfo.Duration.TotalSeconds);
                    fpsList.Add(mediaInfo.PrimaryVideoStream.FrameRate);
                }

                // Choose common FPS (max or fixed high value)
                double commonFps = Math.Max(60, fpsList.Max());

                // Add FPS normalization filters for each input with consistent timebase
                for (int i = 0; i < inputVideoPaths.Count; i++)
                {
                    filterParts.Add($"[{i}:v]fps={commonFps},settb=1/1000000[norm{i}]");
                }

                string currentLabel = "[norm0]";
                double cumulativeOffset = durations[0]; // Start with the first clip's duration
                for (int i = 0; i < inputVideoPaths.Count - 1; i++)
                {
                    double duration = transitionDurations[i];
                    string nextLabel = $"[norm{i + 1}]";
                    string outputLabel = $"[v{i}]";
                    double offset = cumulativeOffset - duration;
                    if (duration > 0)
                    {
                        // xfade with correct offset and set timebase on output
                        filterParts.Add($"{currentLabel}{nextLabel}xfade=transition={transitionType}:duration={duration}:offset={offset},settb=1/1000000{outputLabel}");
                    }
                    else
                    {
                        // No transition: concat with set timebase on output
                        filterParts.Add($"{currentLabel}{nextLabel}concat=n=2:v=1:a=0,settb=1/1000000{outputLabel}");
                    }

                    // Update cumulative (add next clip's net duration)
                    cumulativeOffset += durations[i + 1] - duration;
                    currentLabel = outputLabel;
                }

                // Build complete filter complex
                string filterComplex = string.Join(";", filterParts);

                // Create FFMpeg arguments with all inputs
                var ffmpegArgs = FFMpegArguments.FromFileInput(inputVideoPaths[0]);
                for (int i = 1; i < inputVideoPaths.Count; i++)
                {
                    ffmpegArgs = ffmpegArgs.AddFileInput(inputVideoPaths[i]);
                }

                // Execute
                var result = await ffmpegArgs
                    .OutputToFile(outputPath, true, options =>
                    {
                        options.WithCustomArgument($"-filter_complex \"{filterComplex}\"");
                        options.WithCustomArgument($"-map {currentLabel}");
                        options.WithVideoCodec("libx264");
                        options.WithCustomArgument("-an"); // No audio for now
                        options.WithCustomArgument("-y"); // Overwrite output
                        options.WithFramerate(commonFps); // Set output FPS to common
                    })
                    .ProcessAsynchronously();

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in uniform transition stitching: {ex.Message}");
                return false;
            }
        }


        // Enum for transition types
        public enum TransitionType
        {
            None,
            Interpolate,
            Fade
        }

        // Record for junction parameters (immutable, simple)
        public record JunctionParameters(
            TransitionType Type,
            double DurationSeconds = 0.0, // For Fade (overlap duration) or Interpolate (blend duration)
            int DropFrames = 0,           // For Interpolate: frames to drop from end of prev clip
            int InterpFrames = 0         // For Interpolate: number of interp frames to generate
        );

        private static async Task<bool> StitchVideos(
            List<string> inputVideoPaths,
            string outputPath,
            List<JunctionParameters> junctions)
        {
            if (junctions.Count < inputVideoPaths.Count - 1)
            {
                throw new ArgumentException("Junctions list must match the number of stitch points (inputs - 1)");
            }

            try
            {
                var tempFiles = new List<string>(); // For cleanup
                var segments = new List<string>();  // Build list of processed segments

                for (int i = 0; i < inputVideoPaths.Count; i++)
                {
                    string currentClip = inputVideoPaths[i];
                    string processedClip = await ProcessClipForJunction(currentClip, i < junctions.Count ? junctions[i] : null, tempFiles);
                    segments.Add(processedClip);

                    // If not last clip, add transition segment based on junction
                    if (i < inputVideoPaths.Count - 1)
                    {
                        var junction = junctions[i];
                        string transitionSegment = await GenerateTransitionSegment(processedClip, inputVideoPaths[i + 1], junction, tempFiles);
                        if (!string.IsNullOrEmpty(transitionSegment))
                        {
                            segments.Add(transitionSegment);
                        }
                    }
                }

                // Probe to get duration and frame count
                var mediaInfo = await FFProbe.AnalyseAsync(inputVideoPaths[0]);
                //double durationSec = mediaInfo.Duration.TotalSeconds;
                double inputFps = mediaInfo.PrimaryVideoStream.FrameRate;
                //int totalFrames = (int)(durationSec * inputFps) + 1;

                // Create temp concat list file
                string listFile = Path.GetTempFileName();
                using (var writer = new StreamWriter(listFile))
                {
                    foreach (var seg in segments)
                    {
                        // Use absolute paths and escape properly
                        writer.WriteLine($"file '{Path.GetFullPath(seg).Replace("\\", "/")}'");

                        mediaInfo = await FFProbe.AnalyseAsync(seg);
                        var dur = mediaInfo.Duration.TotalSeconds;
                        var fps = mediaInfo.PrimaryVideoStream.FrameRate;
                        var frames = (int)(dur * fps) + 1;
                        Debug.WriteLine($"Segment: {dur} seconds @ {fps} fps with {frames} frames");
                    }
                }

                // Concat using demuxer
                var concatArgs = FFMpegArguments
                    .FromFileInput(listFile, true, inputOptions => inputOptions
                        .WithCustomArgument("-f concat -safe 0"))
                    .OutputToFile(outputPath, true, options =>
                    {
                        options.WithVideoCodec("libx264");
                        options.WithCustomArgument("-an"); // No audio
                        options.WithCustomArgument("-y");
                        options.WithFramerate(inputFps);
                    });

                var result = await concatArgs.ProcessAsynchronously();

                // Cleanup list file
                File.Delete(listFile);

                //mediaInfo = await FFProbe.AnalyseAsync(outputPath);
                //durationSec = mediaInfo.Duration.TotalSeconds;
                //inputFps = mediaInfo.PrimaryVideoStream.FrameRate;
                //totalFrames = (int)(durationSec * inputFps) + 1;

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in video stitching: {ex.Message}");
                return false;
            }
        }

        // Helper: Process (trim) clip based on next junction (e.g., drop frames for Interpolate)
        private static async Task<string> ProcessClipForJunction(string clipPath, JunctionParameters? nextJunction, List<string> tempFiles)
        {
            if (nextJunction == null || nextJunction.Type != TransitionType.Interpolate || nextJunction.DropFrames <= 0)
            {
                return clipPath; // No processing needed
            }

            // Probe to get duration and frame count
            var mediaInfo = await FFProbe.AnalyseAsync(clipPath);
            double durationSec = mediaInfo.Duration.TotalSeconds;
            double inputFps = mediaInfo.PrimaryVideoStream.FrameRate;
            int totalFrames = (int)(durationSec * inputFps) + 1;

            if (nextJunction.DropFrames >= totalFrames)
            {
                throw new ArgumentException($"Drop frames exceed total frames for clip: {clipPath}");
            }

            // Calculate trim duration
            double trimDurationSec = durationSec - (nextJunction.DropFrames / inputFps);

            // Trim to temp file
            string trimmedClip = Path.Combine(Path.GetTempPath(), $"trimmed_{Guid.NewGuid()}.mp4");
            var trimArgs = FFMpegArguments
                .FromFileInput(clipPath)
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
            JunctionParameters junction,
            List<string> tempFiles)
        {
            if (junction.Type == TransitionType.None)
            {
                return string.Empty; // No segment
            }

            // Probe to get duration and frame count
            var mediaInfo = await FFProbe.AnalyseAsync(prevClip);
            double durationSec = mediaInfo.Duration.TotalSeconds;
            double inputFps = mediaInfo.PrimaryVideoStream.FrameRate;
            int totalFrames = (int)(durationSec * inputFps) + 1;

            string transitionClip = Path.Combine(Path.GetTempPath(), $"transition_{Guid.NewGuid()}.mp4");

            if (junction.Type == TransitionType.Fade)
            {
                // Use xfade to create a short transition segment
                double offset = junction.DurationSeconds; // Assuming prev end overlaps with next start
                var fadeArgs = FFMpegArguments
                    .FromFileInput(prevClip)
                    .AddFileInput(nextClip)
                    .OutputToFile(transitionClip, true, options =>
                    {
                        options.WithCustomArgument($"-filter_complex \"[0:v][1:v]xfade=transition=fade:duration={junction.DurationSeconds}:offset={offset},settb=1/1000000\"");
                        options.WithVideoCodec("libx264");
                        options.WithCustomArgument("-an");
                        options.WithFramerate(inputFps);
                        options.WithCustomArgument($"-t {junction.DurationSeconds * 2}"); // Approx length of transition
                    });
                await fadeArgs.ProcessAsynchronously();
            }
            else if (junction.Type == TransitionType.Interpolate)
            {
                //int numInterpFrames = junction.InterpFrames;
                //if (numInterpFrames <= 0) return string.Empty;

                //// Extract last frame of prev (post-trim)
                //string lastFrame = ExtractLastFrame(prevClip, Path.GetDirectoryName(prevClip));
                //tempFiles.Add(lastFrame);

                //// Extract first frame of next
                //string firstFrame = ExtractFirstFrame(nextClip, Path.GetDirectoryName(nextClip));
                //tempFiles.Add(firstFrame);

                //string fileListPath = Path.Combine(Path.GetDirectoryName(prevClip), $"filelist_{DateTime.Now.Ticks}.txt");

                //var bridgeFps = Math.Max(0.1, 2 * (double)inputFps / (double)(numInterpFrames + 1)); // Avoid zero/negative; +1 for inclusive frames

                //var fileListContent = new List<string>
                //{
                //    $"file '{lastFrame.Replace("\\", "/")}'",
                //    $"file '{firstFrame.Replace("\\", "/")}'"

                //};
                //await File.WriteAllLinesAsync(fileListPath, fileListContent);

                //tempFiles.Add(fileListPath);

                //string bridgeVideo = Path.Combine(Path.GetTempPath(), $"bridge_{Guid.NewGuid()}.mp4");
                //var numFrames = fileListContent.Count;

                //var result = await FFMpegArguments
                //    .FromFileInput(fileListPath, false, inputOptions =>
                //    {
                //        inputOptions.ForceFormat("concat");
                //        inputOptions.WithCustomArgument("-safe 0");
                //        // No input -framerate needed with -vf approach
                //    })
                //    .OutputToFile(bridgeVideo, true, options =>
                //    {
                //        options.WithVideoCodec("libx264");
                //        options.WithConstantRateFactor(23);
                //        options.WithCustomArgument($"-r {bridgeFps}"); // Reinforce output framerate
                //        options.WithCustomArgument($"-vf \"setpts=N/{bridgeFps}/TB,fps={bridgeFps}\""); // Key fix: Custom -vf for retiming
                //        options.WithCustomArgument("-pix_fmt yuv420p");
                //        options.WithCustomArgument("-vframes " + numFrames); // Limit to exact frame count
                //    })
                //    .ProcessAsynchronously();

                //mediaInfo = FFProbe.Analyse(bridgeVideo);

                //tempFiles.Add(bridgeVideo);

                //// Use minterpolate
                //result = await FFMpegArguments
                //    .FromFileInput(bridgeVideo)
                //    .OutputToFile(transitionClip, true, options =>
                //    {
                //        options.WithCustomArgument("-v verbose");
                //        options.WithCustomArgument($"-vf minterpolate=fps={inputFps}:mi_mode=mci:mc_mode=aobmc:vsbmc=1:scd=none");
                //        options.WithVideoCodec("libx264");
                //        options.WithConstantRateFactor(23);
                //        options.WithCustomArgument("-pix_fmt yuv420p");
                //        options.WithCustomArgument("-an");
                //    })
                //    .ProcessAsynchronously();

                //mediaInfo = FFProbe.Analyse(transitionClip);

                //tempFiles.Add(transitionClip);
                // Updated routine (replace your current logic; assumes numInterpFrames >= 4, else return empty as before)
                int numInterpFrames = junction.InterpFrames;
                if (numInterpFrames < 4) return string.Empty; // minterpolate requires at least 4 input frames

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

                if (totalPrevFrames < numInterpFrames + 1 || totalNextFrames < numInterpFrames + 1)
                {
                    // Not enough frames in clips; handle gracefully (e.g., fall back to simple concat or log error)
                    return string.Empty;
                }

                string extractDir = Path.GetDirectoryName(prevClip); // Reuse prevClip dir for extracts

                // Extract the 4 keyframes
                int frame1Index = totalPrevFrames - 1 - numInterpFrames; // numInterpFrames before the end
                string frame1 = await ExtractFrameAtAsync(prevClip, frame1Index, extractDir, inputFps);
                tempFiles.Add(frame1);

                string frame2 = await ExtractFrameAtAsync(prevClip, totalPrevFrames - 1, extractDir, inputFps); // Last frame
                tempFiles.Add(frame2);

                string frame3 = await ExtractFrameAtAsync(nextClip, 0, extractDir, inputFps); // First frame
                tempFiles.Add(frame3);

                int frame4Index = numInterpFrames; // numInterpFrames from the start
                string frame4 = await ExtractFrameAtAsync(nextClip, frame4Index, extractDir, inputFps);
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
                double desiredTransitionDuration = (double)numInterpFrames / inputFps; // 1s for your test
                                                                                       // Set bridgeFps low to extend input duration: numInputFrames / (desiredTransitionDuration * numInputFrames) = 1 / desiredTransitionDuration
                var bridgeFps = (double)inputFps / numInterpFrames; // 16/16=1 fps for your test
                var numInputFrames = 4;
                var bridgeDuration = (double)numInputFrames / bridgeFps; // 4s for your test (even spacing, last frame held for full 1/bridgeFps)
                string bridgeVideo = Path.Combine(Path.GetTempPath(), $"bridge_{Guid.NewGuid()}.mp4");

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
                        options.WithCustomArgument("-vframes " + numInputFrames); // Exact input frame count
                    })
                    .ProcessAsynchronously();

                tempFiles.Add(bridgeVideo);

                mediaInfo = await FFProbe.AnalyseAsync(bridgeVideo);

                // Interpolate to inputFps (preserves duration ~bridgeDuration)
                string interpolatedClip = Path.Combine(Path.GetTempPath(), $"interpolated_{Guid.NewGuid()}.mp4");
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

                tempFiles.Add(interpolatedClip);

                // Trim interpolated clip
                mediaInfo = await FFProbe.AnalyseAsync(interpolatedClip);
                var interpStream = mediaInfo.PrimaryVideoStream;
                double approxInterpTotalFrames = mediaInfo.Duration.TotalSeconds * interpStream.FrameRate;
                int totalInterpFrames = (int)Math.Ceiling(approxInterpTotalFrames);
                string finalTransitionClip = transitionClip; // Default to interpolated if no trim needed

                if (totalInterpFrames > numInterpFrames)
                {
                    int startFrame = (totalInterpFrames - numInterpFrames); // / 2; -- For some reason, the correct part of the clip to extract seems to be the second half, not the middle.
                    double startTime = (double)startFrame / inputFps;
                    double trimDuration = (double)numInterpFrames / inputFps;

                    finalTransitionClip = Path.Combine(Path.GetTempPath(), $"transition_trimmed_{Guid.NewGuid()}.mp4");
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

                mediaInfo = await FFProbe.AnalyseAsync(transitionClip);
            }

            return transitionClip;
        }

        static async Task<string> ExtractFrameAtAsync(string videoPath, int frameIndex, string outputDir, double fps)
        {
            var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
            var stream = mediaInfo.PrimaryVideoStream;
            if (stream == null)
            {
                throw new InvalidOperationException("No video stream found in the input file.");
            }

            // Approximate total frames if needed, but assume frameIndex is valid
            double approxTotalFrames = mediaInfo.Duration.TotalSeconds * stream.FrameRate;
            if (frameIndex < 0 || frameIndex >= approxTotalFrames)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex), $"Frame index {frameIndex} is out of range [0, {approxTotalFrames - 1}].");
            }

            string outputPath = Path.Combine(outputDir, $"extracted_frame_{frameIndex}_{DateTime.Now.Ticks}.png");
            await FFMpegArguments
                .FromFileInput(videoPath)
                .OutputToFile(outputPath, true, options =>
                {
                    options.WithCustomArgument($"-vf \"select=eq(n\\,{frameIndex})\"");
                    options.WithCustomArgument("-vframes 1");
                    options.ForceFormat("image2");
                    options.WithVideoCodec("png");
                })
                .ProcessAsynchronously();

            return outputPath;
        }

        private static async Task<bool> ConcatWithDemuxer(
        List<string> inputVideoPaths,
        string outputPath,
        bool deleteIntermediateFiles)
        {
            string tempListFile = Path.ChangeExtension(Path.GetTempFileName(), ".txt");

            try
            {
                // Create temporary file list for concat demuxer
                var fileListContent = inputVideoPaths.Select(path => $"file '{path.Replace("\\", "/")}'");
                await File.WriteAllLinesAsync(tempListFile, fileListContent);

                // Use FFMpegArguments with proper input format specification
                var result = await FFMpegArguments
                    .FromFileInput(tempListFile, verifyExists: false, options => options
                        .ForceFormat("concat")  // Specify concat format for input
                        .WithCustomArgument("-safe 0")) // Allow unsafe file paths
                    .OutputToFile(outputPath, true, options => options
                        .WithVideoCodec("copy")
                        .WithAudioCodec("copy"))
                    .ProcessAsynchronously();

                return result;
            }
            finally
            {
                if (deleteIntermediateFiles && File.Exists(tempListFile))
                    File.Delete(tempListFile);
            }
        }

        private static async Task<bool> ConcatWithFilter(List<string> inputVideoPaths, string outputPath)
        {
            // Analyze all videos to determine common properties
            var videoInfos = inputVideoPaths.Select(path => FFProbe.Analyse(path)).ToList();
            bool hasAudio = videoInfos.Any(info => info.AudioStreams.Any());

            // Find the maximum resolution to scale all videos to
            int maxWidth = videoInfos.Max(info => info.PrimaryVideoStream.Width);
            int maxHeight = videoInfos.Max(info => info.PrimaryVideoStream.Height);

            // Build filter complex string with scaling and concatenation
            var filterParts = new List<string>();

            // Add scaling filters for each input
            for (int i = 0; i < inputVideoPaths.Count; i++)
            {
                filterParts.Add($"[{i}:v]scale={maxWidth}:{maxHeight}[v{i}]");
            }

            // Add concat filter
            string concatInputs = string.Join("", Enumerable.Range(0, inputVideoPaths.Count).Select(i => $"[v{i}]"));
            string concatFilter = hasAudio ?
                $"{concatInputs}concat=n={inputVideoPaths.Count}:v=1:a=1[outv][outa]" :
                $"{concatInputs}concat=n={inputVideoPaths.Count}:v=1:a=0[outv]";
            filterParts.Add(concatFilter);

            string filterComplex = string.Join(";", filterParts);

            // Create FFMpegArguments starting with the first input
            var ffmpegArgs = FFMpegArguments.FromFileInput(inputVideoPaths[0]);

            // Add remaining inputs
            for (int i = 1; i < inputVideoPaths.Count; i++)
            {
                ffmpegArgs = ffmpegArgs.AddFileInput(inputVideoPaths[i]);
            }

            // Configure output based on audio presence
            var result = await ffmpegArgs
                .OutputToFile(outputPath, true, options =>
                {
                    options.WithCustomArgument($"-filter_complex \"{filterComplex}\"");

                    if (hasAudio)
                    {
                        options.WithCustomArgument("-map [outv] -map [outa]")
                               .WithVideoCodec("libx264")
                               .WithAudioCodec("aac");
                    }
                    else
                    {
                        options.WithCustomArgument("-map [outv]")
                               .WithVideoCodec("libx264")
                               .WithCustomArgument("-an"); // No audio
                    }
                })
                .ProcessAsynchronously();

            return result;
        }
    }
}
