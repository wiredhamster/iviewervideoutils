using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace iviewer
{
    public class VideoClipInfo
    {
        public string Path { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string NegativePrompt { get; set; } = "";
        public int? Seed { get; set; }
        public string Model { get; set; } = "";
        public float CFGScale { get; set; } = 1.0f;
        public int Steps { get; set; } = 20;
        public string Sampler { get; set; } = "";
        public int ClipSkip { get; set; } = 1;
        public string Source { get; set; } = "";
        public string Resolution { get; set; } = "";
        public double Duration { get; set; }
        public int RowIndex { get; set; }

        // Initialize Parameters as empty dictionary to prevent deserialization issues
        [JsonProperty]
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        // Constructor to ensure Parameters is always initialized
        public VideoClipInfo()
        {
            Parameters = new Dictionary<string, string>();
        }

        // Copy constructor for cloning
        public VideoClipInfo(VideoClipInfo other)
        {
            Path = other.Path;
            Prompt = other.Prompt;
            NegativePrompt = other.NegativePrompt;
            Seed = other.Seed;
            Model = other.Model;
            CFGScale = other.CFGScale;
            Steps = other.Steps;
            Sampler = other.Sampler;
            ClipSkip = other.ClipSkip;
            Source = other.Source;
            Resolution = other.Resolution;
            Duration = other.Duration;
            RowIndex = other.RowIndex;
            Parameters = new Dictionary<string, string>(other.Parameters);
        }

        // Helper method to safely add parameters
        public void AddParameter(string key, string value)
        {
            Parameters ??= new Dictionary<string, string>();
            Parameters[key] = value ?? "";
        }

        // Helper method to safely get parameters
        public string GetParameter(string key, string defaultValue = "")
        {
            Parameters ??= new Dictionary<string, string>();
            return Parameters.TryGetValue(key, out string value) ? value : defaultValue;
        }
    }
}