using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace iviewer.Services
{
	public class ImageGenerationService : ComfyUIServiceBase
	{
		private const string LoraMatchPattern = @"<lora:([^:]+):(-?\d+(?:\.\d+)?)>";
		private const string SDXLWorkflowPath = @"C:\Users\sysadmin\Documents\ComfyUI\user\default\workflows\iviewer\SDXL - API.json";

		public ImageGenerationService() : base()
		{
			// Ensure directories exist
			Directory.CreateDirectory(VideoGenerationConfig.ComfyOutputDir);
			Directory.CreateDirectory(VideoGenerationConfig.TempFileDir);
			Directory.CreateDirectory(VideoGenerationConfig.WorkingDir);
		}

		public async Task<string> GenerateImageAsync(ImageGenerationRequest request)
		{
			try
			{
				// Validate inputs
				if (string.IsNullOrWhiteSpace(request.Prompt))
				{
					throw new ArgumentException("Prompt cannot be empty");
				}

				if (request.Width <= 0 || request.Height <= 0)
				{
					throw new ArgumentException("Width and Height must be positive");
				}

				// Prepare workflow
				string workflowJson = await PrepareWorkflowAsync(request);

				// Execute workflow
				string imagePath = await ExecuteWorkflowAsync(workflowJson, request.OutputDirectory ?? VideoGenerationConfig.TempFileDir);

				return imagePath;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error generating image: {ex.Message}");
				throw;
			}
		}

		private async Task<string> PrepareWorkflowAsync(ImageGenerationRequest request)
		{
			string workflowPath = SDXLWorkflowPath;

			if (!File.Exists(workflowPath))
			{
				throw new FileNotFoundException($"SDXL workflow not found: {workflowPath}");
			}

			string workflowJson = await File.ReadAllTextAsync(workflowPath);

			// Parse and process LoRAs from prompt
			(workflowJson, string cleanPrompt, var loras) = PrepareWorkflowLoras(workflowJson, request.Prompt);

			// Replace placeholders
			workflowJson = workflowJson
				.Replace("{PROMPT}", cleanPrompt)
				.Replace("{NEGATIVE_PROMPT}", request.NegativePrompt ?? "")
				.Replace("{WIDTH}", request.Width.ToString())
				.Replace("{HEIGHT}", request.Height.ToString())
				.Replace("{STEPS}", request.Steps.ToString())
				.Replace("{CFG_SCALE}", request.CfgScale.ToString("F1"))
				.Replace("{SEED}", request.Seed?.ToString() ?? "-1")
				.Replace("{MODEL}", request.Model ?? "sd_xl_base_1.0.safetensors");

			// Validate JSON
			try
			{
				JObject.Parse(workflowJson);
			}
			catch (Exception ex)
			{
				throw new Exception($"Invalid JSON after replacements: {ex.Message}");
			}

			return workflowJson;
		}

		private (string workflowJson, string cleanPrompt, List<LoraInfo> loras) PrepareWorkflowLoras(
			string workflowJson,
			string prompt)
		{
			var loras = new List<LoraInfo>();
			var loraIndex = 1;

			foreach (Match match in Regex.Matches(prompt, LoraMatchPattern))
			{
				if (loraIndex > 10) break; // Maximum 10 LoRAs

				string loraKey = match.Groups[1].Value;
				string weightStr = match.Groups[2].Value;

				if (!double.TryParse(weightStr, out double weight))
				{
					weight = 1.0;
				}

				var lora = Lora.LoadFromKey(loraKey);

				if (lora != null)
				{
					// For SDXL, we typically use the main LoRA file
					string loraName = !string.IsNullOrEmpty(lora.HighNoiseLora)
						? lora.HighNoiseLora
						: lora.LowNoiseLora;

					if (!string.IsNullOrEmpty(loraName))
					{
						workflowJson = workflowJson
							.Replace($"{{LORA{loraIndex}_NAME}}", loraName)
							.Replace($"{{LORA{loraIndex}_STRENGTH}}", weight.ToString("F2"));

						loras.Add(new LoraInfo
						{
							Name = loraName,
							Weight = weight,
							Index = loraIndex
						});

						loraIndex++;
					}
				}
			}

			// Fill remaining LoRA slots with defaults
			for (int i = loraIndex; i <= 10; i++)
			{
				workflowJson = workflowJson
					.Replace($"{{LORA{i}_NAME}}", "None")
					.Replace($"{{LORA{i}_STRENGTH}}", "0");
			}

			// Remove LoRA tags from prompt
			string cleanPrompt = Regex.Replace(prompt, LoraMatchPattern, "").Trim();
			cleanPrompt = Regex.Replace(cleanPrompt, @"\s+", " ");

			return (workflowJson, cleanPrompt, loras);
		}
	}

	public class ImageGenerationRequest
	{
		public string Prompt { get; set; }
		public string NegativePrompt { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public int Steps { get; set; } = 20;
		public double CfgScale { get; set; } = 7.0;
		public int? Seed { get; set; }
		public string Model { get; set; }
		public string OutputDirectory { get; set; }
	}

	public class LoraInfo
	{
		public string Name { get; set; }
		public double Weight { get; set; }
		public int Index { get; set; }
	}
}