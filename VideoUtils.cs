using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using iviewer.Services;
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
	internal class VideoUtils
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

        static void ConcatenateVideoClips(List<string> paths, string outputPath)
        {
            ConcatenateVideoClips(paths, outputPath);
        }

        internal static async Task ConcatenateVideoClipsAsync(List<string> paths, string outputPath)
        {
            // Re-encode all inputs to normalize PTS before concatenating
            var normalizedPaths = new List<string>();
            string tempDir = VideoGenerationConfig.TempFileDir;

            try
            {
                var firstInfo = await FFProbe.AnalyseAsync(paths[0]);
                double fps = firstInfo.PrimaryVideoStream.FrameRate;

                // Normalize each video (reset PTS, ensure consistent encoding)
                for (int i = 0; i < paths.Count; i++)
                {
                    string normalizedPath = Path.Combine(tempDir, $"normalized_{i}_{Guid.NewGuid()}.mp4");

                    await FFMpegArguments
                        .FromFileInput(paths[i])
                        .OutputToFile(normalizedPath, true, options => options
                            .WithCustomArgument("-vf \"setpts=PTS-STARTPTS\"")
                            .WithVideoCodec("libx264")
                            .WithConstantRateFactor(18)
                            .WithCustomArgument($"-r {fps}")
                            .WithCustomArgument("-pix_fmt yuv420p")
                            .WithCustomArgument("-an")
                            .WithCustomArgument("-y"))
                        .ProcessAsynchronously();

                    normalizedPaths.Add(normalizedPath);
                }

                // Now concatenate the normalized videos
                string listFile = Path.Combine(tempDir, Guid.NewGuid().ToString() + ".txt");
                using (var writer = new StreamWriter(listFile))
                {
                    foreach (var path in normalizedPaths)
                    {
                        writer.WriteLine($"file '{Path.GetFullPath(path).Replace("\\", "/")}'");
                    }
                }

                await FFMpegArguments
                    .FromFileInput(listFile, false, inputOptions => inputOptions
                        .ForceFormat("concat")
                        .WithCustomArgument("-safe 0"))
                    .OutputToFile(outputPath, true, options => options
                        .WithVideoCodec("copy") // Can safely copy now
                        .WithCustomArgument("-an")
                        .WithCustomArgument("-y"))
                    .ProcessAsynchronously();

                File.Delete(listFile);
            }
            finally
            {
                // Cleanup normalized temp files
                foreach (var path in normalizedPaths)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }

                foreach (var path in paths)
                {
                    if (File.Exists(path))
                        File.Delete(path);
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
                            newFps = fps * 4;
                        }
                        else if (fps < 20 && interpolate)
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
                //Debug.WriteLine($"Transition clip: {mediaInfo.VideoStreams[0].FrameRate} fps, {mediaInfo.VideoStreams[0].Duration} duration");

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

        internal static async Task<string> AdjustSpeedAsync(string inputPath, double speed, bool highQuality = true)
        {
            // Snap to 'good' values
            if (speed >= 0.65 && speed <= 0.7)
            {
                speed = 0.667;
            }
            else if (speed > 0.4 && speed < 0.6)
            {
                speed = 0.5;
            }

            // Probe original FPS (assume consistent across clips)
            var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
            double originalFps = mediaInfo.PrimaryVideoStream.FrameRate;
            double originalDuration = mediaInfo.PrimaryVideoStream.Duration.TotalSeconds;

            // Set uniform FPS (high for export quality)
            double uniformFps = originalFps;
            var fpsMultiplier = 1;

            if (highQuality && originalFps <= 30)
            { 
                uniformFps = originalFps * 2;
                fpsMultiplier = 2;
            }

            if (uniformFps == originalFps && (Math.Abs(speed - 1.0) < 0.001))
            {
                return inputPath;
            }

            // Create temp output path
            string tempOutput = "";

            if (highQuality)
            {
                var service = new VideoGenerationService();
                tempOutput = await service.InterpolateAndAdjustSpeedAsync(inputPath, fpsMultiplier, speed);
            }
            else
            {
                tempOutput = Path.Combine(VideoGenerationConfig.TempFileDir, "speed_" + Guid.NewGuid().ToString() + ".mp4");

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
            }

            //mediaInfo = FFProbe.Analyse(tempOutput);
            //Debug.WriteLine($"Speed changed by {speed}. Fps from {originalFps} to {info.PrimaryVideoStream.FrameRate}. Duration from {originalDuration} to {info.PrimaryVideoStream.Duration.TotalSeconds}");

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

        internal static async Task<string> ExtractFrameAtAsync(string videoPath, int frameIndex, string outputDir, double fps)
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
    }
}
