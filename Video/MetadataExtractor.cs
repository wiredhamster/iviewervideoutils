using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace iviewer
{
    // First, define classes to match your JSON structure
    public class VideoMetadata
    {
        public string Path { get; set; }
        public string Prompt { get; set; }
        public string NegativePrompt { get; set; }
        public int? Seed { get; set; }
        public string Model { get; set; }
        public double CFGScale { get; set; }
        public int Steps { get; set; }
        public string Sampler { get; set; }
        public int ClipSkip { get; set; }
        public string Source { get; set; }
        public string Resolution { get; set; }
        public double Duration { get; set; }
        public int RowIndex { get; set; }
        public Parameters Parameters { get; set; }
    }

    public class Parameters
    {
        public string Workflow { get; set; }
        public string Type { get; set; }
        public string LoRAs { get; set; }
        public string VAE { get; set; }
        public string Scheduler { get; set; }
        public string FPS { get; set; }
        public string Shift { get; set; }
    }

    public class VideoMetadataWrapper
    {
        public Guid Source { get; set; }
        public List<VideoClipInfo> ClipInfos { get; set; } = new List<VideoClipInfo>();
    }

    public static class MetadataExtractor
    {
		public static List<Guid> GetDistinctSources(string jsonData)
		{
			var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

			var sources = metadata.ClipInfos
				.Where(m => !string.IsNullOrWhiteSpace(m.Source) && Guid.TryParse(m.Source, out _))
				.Select(m => Guid.Parse(m.Source.Trim()))
				.Distinct()
				.ToList();

            var result = new List<Guid>();
            if (metadata.Source != null)
            {
                result.Add(metadata.Source);
            }

            result.AddRange(sources);
            return result;
        }

        /// <summary>
        /// Extract distinct prompts from video metadata
        /// </summary>
        /// <param name="jsonData">JSON string containing video metadata array</param>
        /// <returns>List of distinct prompts</returns>
        public static List<string> GetDistinctPrompts(string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            return metadata.ClipInfos
                .Where(m => !string.IsNullOrWhiteSpace(m.Prompt))
                .Select(m => m.Prompt.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

		public static List<string> GetDistinctNegativePrompts(string jsonData)
		{
            var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            return metadata.ClipInfos
                .Where(m => !string.IsNullOrWhiteSpace(m.NegativePrompt))
				.Select(m => m.NegativePrompt.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		/// <summary>
		/// Extract distinct models from video metadata
		/// </summary>
		public static List<string> GetDistinctModels(string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            return metadata.ClipInfos
                .Where(m => !string.IsNullOrWhiteSpace(m.Model))
                .Select(m => m.Model.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Extract distinct CFG scales from video metadata
        /// </summary>
        public static List<double> GetDistinctCFGScales(string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            return metadata.ClipInfos
                .Select(m => (double)m.CFGScale)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        /// <summary>
        /// Extract distinct step counts from video metadata
        /// </summary>
        public static List<int> GetDistinctSteps(string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            return metadata.ClipInfos
                .Select(m => m.Steps)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        /// <summary>
        /// Extract distinct samplers from video metadata
        /// </summary>
        public static List<string> GetDistinctSamplers(string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            return metadata.ClipInfos
                .Where(m => !string.IsNullOrWhiteSpace(m.Sampler))
                .Select(m => m.Sampler.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Extract distinct resolutions from video metadata
        /// </summary>
        public static List<string> GetDistinctResolutions(string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            return metadata.ClipInfos
                .Where(m => !string.IsNullOrWhiteSpace(m.Resolution))
                .Select(m => m.Resolution.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Extract distinct parameter types from video metadata
        /// </summary>
        public static List<string> GetDistinctParameterTypes(string jsonData)
        {
            var wrapper = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            return wrapper.ClipInfos
                .Where(m => m.Parameters != null && m.Parameters.ContainsKey("Type") && !string.IsNullOrWhiteSpace(m.Parameters["Type"]))
                .Select(m => m.Parameters["Type"].Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Get all distinct values for a specific property using reflection
        /// </summary>
        public static List<T> GetDistinctValues<T>(string jsonData, string propertyName)
        {
            var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            var property = typeof(VideoMetadata).GetProperty(propertyName);

            if (property == null)
                throw new ArgumentException($"Property '{propertyName}' not found");

            return metadata.ClipInfos
                .Select(m => property.GetValue(m))
                .Where(value => value != null)
                .Cast<T>()
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Get comprehensive summary of all distinct values
        /// </summary>
        public static MetadataSummary GetMetadataSummary(string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            return new MetadataSummary
            {
                DistinctSources = GetDistinctSources(jsonData),
				DistinctPrompts = GetDistinctPrompts(jsonData),
                DistinctNegativePrompts = GetDistinctNegativePrompts(jsonData),
                DistinctModels = GetDistinctModels(jsonData),
                DistinctCFGScales = GetDistinctCFGScales(jsonData),
                DistinctSteps = GetDistinctSteps(jsonData),
                DistinctSamplers = GetDistinctSamplers(jsonData),
                DistinctResolutions = GetDistinctResolutions(jsonData),
                DistinctParameterTypes = GetDistinctParameterTypes(jsonData),
                TotalVideos = metadata.ClipInfos.Count
            };
        }

        /// <summary>
        /// Extract individual models from comma-separated model strings
        /// </summary>
        public static List<string> GetDistinctIndividualModels(string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<VideoMetadataWrapper>(jsonData);

            return metadata.ClipInfos
                .Where(m => !string.IsNullOrWhiteSpace(m.Model))
                .SelectMany(m => m.Model.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(model => model.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public class MetadataSummary
    {
        public List<Guid> DistinctSources { get; set; }
		public List<string> DistinctPrompts { get; set; }
        public List<string> DistinctNegativePrompts { get; set; }
		public List<string> DistinctModels { get; set; }
        public List<double> DistinctCFGScales { get; set; }
        public List<int> DistinctSteps { get; set; }
        public List<string> DistinctSamplers { get; set; }
        public List<string> DistinctResolutions { get; set; }
        public List<string> DistinctParameterTypes { get; set; }
        public int TotalVideos { get; set; }

        public override string ToString()
        {
            return $@"Metadata Summary:
Total Videos: {TotalVideos}
Distinct Sources: {DistinctSources.Count}
Distinct Prompts: {DistinctPrompts.Count}
Distinct Models: {DistinctModels.Count}
Distinct CFG Scales: {string.Join(", ", DistinctCFGScales)}
Distinct Steps: {string.Join(", ", DistinctSteps)}
Distinct Samplers: {string.Join(", ", DistinctSamplers)}
Distinct Resolutions: {string.Join(", ", DistinctResolutions)}
Distinct Parameter Types: {string.Join(", ", DistinctParameterTypes)}";
        }
    }
}