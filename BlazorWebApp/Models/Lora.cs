using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class Lora
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public float Strength { get; set; }
        public bool IsNegative { get; set; }
        public bool IsEnabled { get; set; }

        /// <summary>
        /// LoRA file path for high noise model in dual-model workflows (e.g., WAN 2.2).
        /// When set, creates a LoRA loader for the high noise model.
        /// </summary>
        public string HighPath { get; set; }

        /// <summary>
        /// LoRA file path for low noise model in dual-model workflows (e.g., WAN 2.2).
        /// When set, creates a LoRA loader for the low noise model.
        /// </summary>
        public string LowPath { get; set; }

        /// <summary>
        /// Indicates if this LoRA has a high noise model variant configured.
        /// </summary>
        [JsonIgnore]
        public bool HasHighPath => !string.IsNullOrWhiteSpace(HighPath);

        /// <summary>
        /// Indicates if this LoRA has a low noise model variant configured.
        /// </summary>
        [JsonIgnore]
        public bool HasLowPath => !string.IsNullOrWhiteSpace(LowPath);

        /// <summary>
        /// Indicates if this is a dual-model LoRA (has either high or low path set).
        /// </summary>
        [JsonIgnore]
        public bool IsDualModel => HasHighPath || HasLowPath;

        public Lora() { }
        public Lora(Lora clone)
        {
            Name = clone.Name;
            Path = clone.Path;
            Strength = clone.Strength;
            IsNegative = clone.IsNegative;
            IsEnabled = clone.IsEnabled;
            HighPath = clone.HighPath;
            LowPath = clone.LowPath;
        }
    }
}
