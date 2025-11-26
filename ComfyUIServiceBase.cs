using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;

namespace iviewer.Services
{
	public abstract class ComfyUIServiceBase
	{
		protected readonly HttpClient _httpClient;

		protected ComfyUIServiceBase()
		{
			_httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:8000") };
		}

		protected async Task<string> UploadImageAsync(string imagePath)
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
				Debug.WriteLine($"Error uploading image: {ex.Message}");
				return null;
			}
		}

		protected async Task<string> ExecuteWorkflowAsync(string workflowJson, string outputDir)
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

		protected async Task<string> PollForCompletionAsync(string promptId, string outputDir)
		{
			var startTime = DateTime.Now;

			try
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
						return FindGeneratedOutput(outputDir);
					}
					else if (status?["status_str"]?.ToString() == "error" || HasExecutionError(status?["messages"]))
					{
						return string.Empty;
					}

					await Task.Delay(1000);
				}
			}
			finally
			{
				// Clean up input directory
				CleanupComfyInputDirectory(startTime);
			}
		}

		protected void CleanupComfyInputDirectory(DateTime startTime)
		{
			try
			{
				foreach (var file in Directory.GetFiles(VideoGenerationConfig.ComfyInputDir))
				{
					try
					{
						if (new FileInfo(file).CreationTime <= startTime)
						{
							File.Delete(file);
						}
					}
					catch { }
				}

				foreach (var dir in Directory.GetDirectories(VideoGenerationConfig.ComfyInputDir))
				{
					try
					{
						if (new DirectoryInfo(dir).CreationTime <= startTime)
						{
							Directory.Delete(dir, true);
						}
					}
					catch { }
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error cleaning up input directory: {ex.Message}");
			}
		}

		protected bool HasExecutionError(JToken messagesToken)
		{
			if (messagesToken is JArray messages)
			{
				return messages.Any(msg => msg[0]?.ToString() == "execution_error");
			}
			return false;
		}

		protected string FindGeneratedOutput(string outputDir)
		{
			// Look for most recent file (image or video)
			var tempFile = Directory.GetFiles(VideoGenerationConfig.ComfyOutputDir, "*.*")
				.Where(f => new[] { ".mp4", ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(f).ToLower()))
				.Select(f => new { Path = f, Time = File.GetCreationTime(f) })
				.OrderByDescending(x => x.Time)
				.FirstOrDefault();

			if (tempFile == null)
			{
				throw new Exception("No output found in temp directory after completion");
			}

			var extension = Path.GetExtension(tempFile.Path).ToLower();
			var prefix = extension == ".mp4" ? "video"
				: new[] { ".png", ".jpg", ".jpeg" }.Contains(extension) ? "image"
				: "output";

			string uniqueName = $"{prefix}_{Guid.NewGuid()}{extension}";
			string finalPath = Path.Combine(outputDir, uniqueName);
			File.Move(tempFile.Path, finalPath);

			// Delete any associated files
			var baseName = Path.GetFileNameWithoutExtension(tempFile.Path);
			var associatedFiles = Directory.GetFiles(Path.GetDirectoryName(tempFile.Path), baseName + ".*");
			foreach (var assocFile in associatedFiles.Where(f => f != tempFile.Path))
			{
				try { File.Delete(assocFile); } catch { }
			}

			return Path.GetFullPath(finalPath);
		}

		public void Dispose()
		{
			_httpClient?.Dispose();
		}
	}
}