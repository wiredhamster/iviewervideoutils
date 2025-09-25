using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Arguments;
using FFMpegCore.Pipes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

		public static void ProcessFolder(string folder, string action)
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

        public static async Task<bool> UpscaleAndInterpolateVideoAsync(string inputFile, string outputFile)
        {
            try
            {
                var width = 0;
                var height = 0;
                var fps = 0;
                var args = new StringBuilder();

                if (Path.GetExtension(inputFile).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    (width, height, fps, _) = VideoMetadataParser.GetVideoInfo(inputFile);
                    if (width > 0 && height > 0 && fps > 0)
                    {
                        var newHeight = height;
                        var newFps = fps;

                        // Calculate new height (upscale if <= 1080p)
                        if (height <= 1080)
                        {
                            newHeight = height * 2;
                        }

                        // Calculate new fps (interpolate if < 30fps)
                        if (fps < 15)
                        {
                            newFps = fps * 4;
                        }
                        else if (fps < 30)
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
                    var result = await FFMpegArguments
                        .FromFileInput(inputFile)
                        .OutputToFile(outputFile, true, options => options
                            .WithCustomArgument(ffmpegArgs))
                        .ProcessAsynchronously();

                    return result;
                }
                else
                {
                    // No processing required. Just copy the file.
                    await Task.Run(() => File.Copy(inputFile, outputFile, overwrite: true));
                    return true;
                }
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
        public static bool UpscaleAndInterpolateVideo(string inputFile, string outputFile)
        {
            return UpscaleAndInterpolateVideoAsync(inputFile, outputFile).Result;
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

        //     public static string ExtractLastFrame(string inputVideoPath)
        //     {
        //         if (!File.Exists(inputVideoPath))
        //         {
        //             throw new FileNotFoundException("Input video file not found.", inputVideoPath);
        //         }

        //var outputDirectory = Path.Combine(Path.GetDirectoryName(inputVideoPath), "temp_frames");
        //         if (!Directory.Exists(outputDirectory))
        //         {
        //             Directory.CreateDirectory(outputDirectory);
        //         }

        //         var filename = Path.GetFileNameWithoutExtension(inputVideoPath);
        //         string outputPath = Path.Combine(outputDirectory, $"{filename}_lastframe.png");

        //         try
        //         {
        //             // Analyse video duration
        //             var mediaInfo = FFProbe.Analyse(inputVideoPath);
        //             var duration = mediaInfo.Duration;

        //             // Seek a little before the end (avoid going past the last keyframe)
        //             var seekTime = duration - TimeSpan.FromSeconds(0.5);
        //             if (seekTime < TimeSpan.Zero)
        //                 seekTime = TimeSpan.Zero;

        //             FFMpegArguments
        //		.FromFileInput(inputVideoPath)
        //		.OutputToFile(outputPath, true, options => options
        //			.Seek(seekTime)                 
        //			.WithFrameOutputCount(1)
        //			.WithVideoCodec(VideoCodec.Png))
        //		.ProcessSynchronously();

        //             return outputPath;
        //         }
        //         catch (Exception ex)
        //         {
        //             // Log or handle error as needed (e.g., Debug.WriteLine(ex.Message));
        //             return null;
        //         }
        //     }

        public static string ExtractLastFrame(string inputVideoPath, string outputDirectory)
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

                return ExtractLastFrameWithFrameCount(inputVideoPath, outputPath);
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
        private static string ExtractLastFrameWithFrameCount(string inputVideoPath, string outputPath)
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
            List<string> inputVideoPaths,
            string outputPath,
            string transitionType,
            List<double> transitionDurations,
            bool deleteIntermediateFiles = true)
        {
            try
            {
                if (inputVideoPaths.Count == 1)
                {
                    File.Copy(inputVideoPaths[0], outputPath, true);
                    return true;
                }
                // Check if any transitions
                bool hasTransitions = transitionDurations?.Any(d => d > 0) == true;
                if (!hasTransitions || transitionDurations.Count != inputVideoPaths.Count - 1)
                {
                    // Fallback or error
                    return await StitchVideosAsync(inputVideoPaths, outputPath, deleteIntermediateFiles); // Your existing no-transition stitch
                }
                // Create output dir
                string outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);
                return await StitchWithUniformTransitions(inputVideoPaths, outputPath, transitionType, transitionDurations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stitching videos with transitions: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> StitchWithUniformTransitions(
            List<string> inputVideoPaths,
            string outputPath,
            string transitionType,
            List<double> transitionDurations)
        {
            try
            {
                var filterParts = new List<string>();
                var durations = new List<double>();
                double cumulativeOffset = 0;

                // Probe durations
                for (int i = 0; i < inputVideoPaths.Count; i++)
                {
                    var mediaInfo = await FFProbe.AnalyseAsync(inputVideoPaths[i]);
                    durations.Add(mediaInfo.Duration.TotalSeconds);
                }

                string currentLabel = "[0:v]";
                for (int i = 0; i < inputVideoPaths.Count - 1; i++)
                {
                    double duration = transitionDurations[i];
                    string nextLabel = $"[{i + 1}:v]";
                    string outputLabel = $"[v{i}]";

                    if (duration > 0)
                    {
                        // xfade with correct offset (end of current minus overlap)
                        double offset = cumulativeOffset + durations[i] - duration;
                        filterParts.Add($"{currentLabel}{nextLabel}xfade=transition={transitionType}:duration={duration}:offset={offset}{outputLabel}");
                    }
                    else
                    {
                        // No transition: concat
                        filterParts.Add($"{currentLabel}{nextLabel}concat=n=2:v=1:a=0{outputLabel}");
                    }

                    // Update cumulative (full current duration, as overlap is handled in filter)
                    cumulativeOffset += durations[i];
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

        public static async Task<bool> StitchVideosAsync(List<string> inputVideoPaths,
            string outputPath,
            bool deleteIntermediateFiles = true,
            bool trimLastFrame = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputPath))
                    throw new ArgumentException("Output path cannot be empty");

                // Check if all input files exist
                foreach (string path in inputVideoPaths)
                {
                    if (!File.Exists(path))
                        throw new FileNotFoundException($"Input file not found: {path}");
                }

                // Create output directory if it doesn't exist
                string outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                if (inputVideoPaths.Count == 1)
                {
                    File.Copy(inputVideoPaths[0], outputPath, true);
                    return true;
                }

                if (trimLastFrame)
                {
                    //Create temp trimmed clips (drop last frame)
                    var tempClips = new List<string>();
                    for (int i = 0; i < inputVideoPaths.Count - 1; i++) // Skip last (no trim)
                    {
                        var info = FFProbe.Analyse(inputVideoPaths[i]);
                        var fps = info.PrimaryVideoStream.FrameRate;
                        var duration = info.Duration.TotalSeconds - (1.0 / fps); // Trim ~1 frame
                        var tempClip = Path.GetTempFileName() + ".mp4";
                        await FFMpegArguments
                            .FromFileInput(inputVideoPaths[i])
                            .OutputToFile(tempClip, overwrite: true, options =>
                            {
                                options.WithDuration(TimeSpan.FromSeconds(duration)); // Trim end
                                options.CopyChannel(); // Fast copy, no re-encode
                            })
                            .ProcessAsynchronously();
                        tempClips.Add(tempClip);
                    }
                    tempClips.Add(inputVideoPaths.Last()); // Full last clip
                    inputVideoPaths = tempClips;
                }

                // Method 1: Using FFMpeg concat filter (recommended for same format/codec)
                var mediaInfos = inputVideoPaths.Select(path => FFProbe.Analyse(path)).ToList();

                // Check if all videos have the same resolution and codec
                bool sameSpecs = mediaInfos.All(info =>
                    info.PrimaryVideoStream.Width == mediaInfos[0].PrimaryVideoStream.Width &&
                    info.PrimaryVideoStream.Height == mediaInfos[0].PrimaryVideoStream.Height &&
                    info.PrimaryVideoStream.CodecName == mediaInfos[0].PrimaryVideoStream.CodecName);

                if (sameSpecs)
                {
                    // Use concat demuxer for identical specs (faster, no re-encoding)
                    return await ConcatWithDemuxer(inputVideoPaths, outputPath, deleteIntermediateFiles);
                }
                else
                {
                    // Why different specs?
                    foreach (var info in mediaInfos)
                    {
                        Debug.WriteLine($"{info.PrimaryVideoStream.Width}x{info.PrimaryVideoStream.Height} ({info.PrimaryVideoStream.CodecName})");
                    }

                    // Use concat filter for different specs (slower, requires re-encoding)
                    return await ConcatWithFilter(inputVideoPaths, outputPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stitching videos: {ex.Message}");
                return false;
            }
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

        /// <summary>
        /// Synchronous version of StitchVideosAsync
        /// </summary>
        public static bool StitchVideos(
            List<string> inputVideoPaths,
            string outputPath,
            bool deleteIntermediateFiles = true)
        {
            return StitchVideosAsync(inputVideoPaths, outputPath, deleteIntermediateFiles).Result;
        }
    }
}
