using NReco.VideoInfo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace iviewer
{
	public class VideoMetadataParser
	{
		public static Dictionary<string, object?> ParseVideoMetadata(string text)
		{
			// Extract workflow (optional)
			var workflowMatch = Regex.Match(text, @"workflow: (\{.*?\})\nSource:", RegexOptions.Singleline | RegexOptions.Multiline);
			Dictionary<string, object>? workflow = null;
			if (workflowMatch.Success)
			{
				var workflowStr = workflowMatch.Groups[1].Value;
				workflowStr = Regex.Replace(workflowStr, @"'((?:\\.|[^'\\])*?)'", m => '"' + m.Groups[1].Value.Replace("\"", "\\\"") + '"');
				try
				{
					workflow = JsonSerializer.Deserialize<Dictionary<string, object>>(workflowStr);
				}
				catch { } // Ignore parse errors
			}

			// Extract Source (optional input image)
			var sourceMatch = Regex.Match(text, @"Source: (.*)");
			string source = sourceMatch.Success ? sourceMatch.Groups[1].Value.Trim() : string.Empty;

			// Extract nodes section (after Source)
			int nodesStart = text.IndexOf("3: "); // Assumes starts with node 3; adjust if varies
			string nodesText = nodesStart != -1 ? text.Substring(nodesStart).Trim() : string.Empty;

			// Parse nodes into dictionary with balanced braces regex
			var nodes = new Dictionary<string, Dictionary<string, JsonElement>>();
			var nodeMatches = Regex.Matches(nodesText, @"(\d+): (\{(?>[^{}]|(?<o>\{)|(?<-o>\}))*(?(o)(?!))\})", RegexOptions.Singleline);
			foreach (Match match in nodeMatches)
			{
				string key = match.Groups[1].Value;
				string valueStr = match.Groups[2].Value;
				// Convert Python dict str to valid JSON: Replace single-quoted strings with double-quoted, escaping inner "
				valueStr = Regex.Replace(valueStr, @"'((?:\\.|[^'\\])*?)'", m => '"' + m.Groups[1].Value.Replace("\"", "\\\"") + '"');
				// Handle nan in is_changed
				valueStr = Regex.Replace(valueStr, @"""is_changed"":\s*nan", "\"is_changed\": null");
				try
				{
					var nodeDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(valueStr);
					if (nodeDict != null)
					{
						nodes[key] = nodeDict;
					}
				}
				catch { } // Skip malformed nodes
			}

			// Initialize extracted values
			string prompt = string.Empty;
			string negativePrompt = string.Empty;
			long? seed = null;
			string model = string.Empty;
			double? cfgScale = null;
			int? steps = null;
			string sampler = string.Empty;
			int? clipSkip = null;
			int? width = null;
			int? height = null;
			var parametersList = new List<string>();

			// Loop through nodes and extract relevant data
			foreach (var kvp in nodes)
			{
				string nodeId = kvp.Key;
				var node = kvp.Value;
				string classType = node.ContainsKey("class_type") ? node["class_type"].GetString() ?? string.Empty : string.Empty;
				Dictionary<string, JsonElement>? inputs = null;
				if (node.ContainsKey("inputs"))
				{
					string inputsJson = node["inputs"].GetRawText();
					inputs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(inputsJson);
				}
				List<JsonElement>? widgets = null;
				if (node.ContainsKey("widgets_values"))
				{
					string widgetsJson = node["widgets_values"].GetRawText();
					widgets = JsonSerializer.Deserialize<List<JsonElement>>(widgetsJson);
				}
				Dictionary<string, JsonElement>? meta = null;
				if (node.ContainsKey("_meta"))
				{
					string metaJson = node["_meta"].GetRawText();
					meta = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metaJson);
				}
				string metaTitle = meta?.ContainsKey("title") == true ? meta["title"].GetString() ?? string.Empty : string.Empty;

				if (classType == "CLIPTextEncode")
				{
					string myText = inputs?.ContainsKey("text") == true ? inputs["text"].GetString() ?? string.Empty : (widgets?.Count > 0 ? widgets[0].GetString() ?? string.Empty : string.Empty);
					if (metaTitle.Contains("Positive"))
					{
						prompt = myText;
					}
					else if (metaTitle.Contains("Negative"))
					{
						negativePrompt = myText;
					}
				}
				else if (classType == "KSampler")
				{
					seed = inputs?.ContainsKey("seed") == true ? inputs["seed"].GetInt64() : (widgets?.Count > 0 ? widgets[0].GetInt64() : null);
					steps = inputs?.ContainsKey("steps") == true ? inputs["steps"].GetInt32() : (widgets?.Count > 2 ? widgets[2].GetInt32() : null);
					cfgScale = inputs?.ContainsKey("cfg") == true ? inputs["cfg"].GetDouble() : (widgets?.Count > 3 ? widgets[3].GetDouble() : null);
					sampler = inputs?.ContainsKey("sampler_name") == true ? inputs["sampler_name"].GetString() ?? string.Empty : (widgets?.Count > 4 ? widgets[4].GetString() ?? string.Empty : string.Empty);
					string scheduler = inputs?.ContainsKey("scheduler") == true ? inputs["scheduler"].GetString() ?? string.Empty : (widgets?.Count > 5 ? widgets[5].GetString() ?? string.Empty : string.Empty);
					double? denoise = inputs?.ContainsKey("denoise") == true ? inputs["denoise"].GetDouble() : (widgets?.Count > 6 ? widgets[6].GetDouble() : null);

					if (steps.HasValue) parametersList.Add($"Steps: {steps.Value}");
					if (!string.IsNullOrEmpty(sampler)) parametersList.Add($"Sampler: {sampler}");
					if (!string.IsNullOrEmpty(scheduler)) parametersList.Add($"Schedule type: {scheduler}");
					if (cfgScale.HasValue) parametersList.Add($"CFG scale: {cfgScale.Value}");
					if (seed.HasValue) parametersList.Add($"Seed: {seed.Value}");
					if (denoise.HasValue) parametersList.Add($"Denoising strength: {denoise.Value}");
				}
				else if (classType == "UNETLoader")
				{
					string unetName = inputs?.ContainsKey("unet_name") == true ? inputs["unet_name"].GetString() ?? string.Empty : (widgets?.Count > 0 ? widgets[0].GetString() ?? string.Empty : string.Empty);
					if (!string.IsNullOrEmpty(unetName))
					{
						model = unetName;
						parametersList.Add($"Model: {model}");
					}
				}
				else if (classType == "LoraLoaderModelOnly")
				{
					string loraName = inputs?.ContainsKey("lora_name") == true ? inputs["lora_name"].GetString() ?? string.Empty : (widgets?.Count > 0 ? widgets[0].GetString() ?? string.Empty : string.Empty);
					double strength = inputs?.ContainsKey("strength_model") == true ? inputs["strength_model"].GetDouble() : (widgets?.Count > 1 ? widgets[1].GetDouble() : 1.0);
					if (!string.IsNullOrEmpty(loraName))
					{
						parametersList.Add($"LoRA hashes: \"{loraName}\": {strength}");
					}
				}
				else if (classType == "WanImageToVideo")
				{
					width = inputs?.ContainsKey("width") == true ? inputs["width"].GetInt32() : (widgets?.Count > 0 ? widgets[0].GetInt32() : null);
					height = inputs?.ContainsKey("height") == true ? inputs["height"].GetInt32() : (widgets?.Count > 1 ? widgets[1].GetInt32() : null);
					int? length = inputs?.ContainsKey("length") == true ? inputs["length"].GetInt32() : (widgets?.Count > 2 ? widgets[2].GetInt32() : null);
					int? batchSize = inputs?.ContainsKey("batch_size") == true ? inputs["batch_size"].GetInt32() : (widgets?.Count > 3 ? widgets[3].GetInt32() : null);

					if (width.HasValue && height.HasValue) parametersList.Add($"Size: {width.Value}x{height.Value}");
					if (length.HasValue) parametersList.Add($"Length: {length.Value}");
					if (batchSize.HasValue) parametersList.Add($"Batch size: {batchSize.Value}");
				}
				else if (classType == "ModelSamplingSD3")
				{
					double? shift = inputs?.ContainsKey("shift") == true ? inputs["shift"].GetDouble() : (widgets?.Count > 0 ? widgets[0].GetDouble() : null);
					if (shift.HasValue) parametersList.Add($"Shift: {shift.Value}");
				}
				else if (classType == "CLIPLoader")
				{
					string clipName = inputs?.ContainsKey("clip_name") == true ? inputs["clip_name"].GetString() ?? string.Empty : (widgets?.Count > 0 ? widgets[0].GetString() ?? string.Empty : string.Empty);
					if (!string.IsNullOrEmpty(clipName)) parametersList.Add($"CLIP Model: {clipName}");
				}
				else if (classType == "VAELoader")
				{
					string vaeName = inputs?.ContainsKey("vae_name") == true ? inputs["vae_name"].GetString() ?? string.Empty : (widgets?.Count > 0 ? widgets[0].GetString() ?? string.Empty : string.Empty);
					if (!string.IsNullOrEmpty(vaeName)) parametersList.Add($"VAE: {vaeName}");
				}
				else if (classType == "CLIPVisionLoader")
				{
					string clipVisionName = inputs?.ContainsKey("clip_name") == true ? inputs["clip_name"].GetString() ?? string.Empty : (widgets?.Count > 0 ? widgets[0].GetString() ?? string.Empty : string.Empty);
					if (!string.IsNullOrEmpty(clipVisionName)) parametersList.Add($"CLIP Vision Model: {clipVisionName}");
				}
				// Extend here for other node types if your workflows vary
			}

			// Extract arbitrary tags from simple "key: value" lines in the text, excluding those starting with '{'
			var tags = new Dictionary<string, List<string>>();
			var tagMatches = Regex.Matches(text, @"^(\w+):\s*(?!\{)(.*)$", RegexOptions.Multiline);
			foreach (Match match in tagMatches)
			{
				if (!match.Value.Contains("{"))
				{
					string tagType = match.Groups[1].Value.Trim().ToLower(); // Normalize to lower for consistency
					string tagName = match.Groups[2].Value.Trim();
					if (!string.IsNullOrEmpty(tagName) && !new[] { "pk", "prompt", "model", "parameters", "source" }.Contains(tagType.ToLower()))
					{
						if (!tags.ContainsKey(tagType))
						{
							tags[tagType] = new List<string>();
						}
						tags[tagType].Add(tagName);
					}
				}
			}

			// Format Parameters as comma-separated string (exclude prompts)
			string parameters = string.Join(", ", parametersList);

			// Assemble and return the dictionary
			return new Dictionary<string, object?>
			{
				{ "Prompt", prompt },
				{ "NegativePrompt", negativePrompt },
				{ "Parameters", parameters },
				{ "Seed", seed },
				{ "Model", model },
				{ "CFGScale", cfgScale },
				{ "Steps", steps },
				{ "Sampler", sampler },
				{ "ClipSkip", clipSkip },
				{ "Width", width },
				{ "Height", height },
				{ "Tags", tags }  // Dictionary<string, List<string>> for tags
			};
		}

		public static (int Width, int Height, int Fps, int Length) GetVideoInfo(string filePath)
		{
			var ffProbe = new FFProbe();
			var mediaInfo = ffProbe.GetMediaInfo(filePath);

			// Find the first video stream (MP4 typically has one primary video stream)
			var videoStream = mediaInfo.Streams.FirstOrDefault(s => s.CodecType == "video");
			if (videoStream == null)
			{
				throw new Exception("No video stream found in the file.");
			}

			int width = videoStream.Width;
			int height = videoStream.Height;
			double fps = videoStream.FrameRate; // This is the average frame rate; use RFrameRate for rational if needed
			int length = (int)mediaInfo.Duration.TotalSeconds;

			return (width, height, (int)fps, length);
		}
	}
}