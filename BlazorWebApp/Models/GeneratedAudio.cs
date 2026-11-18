namespace BlazorWebApp.Models
{
    /// <summary>
    /// Represents a generated audio output from ComfyUI.
    /// </summary>
    public class GeneratedAudio
    {
        public int Id { get; set; }
        public string AudioData { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public double Duration { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string NegativePrompt { get; set; } = string.Empty;
        public long Seed { get; set; }
        public int Steps { get; set; }
        public float CfgScale { get; set; }
        public string Sampler { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public bool Favorite { get; set; }
        public int Score { get; set; }
    }

    /// <summary>
    /// Container for generated audio files.
    /// </summary>
    public class GeneratedAudios
    {
        public List<GeneratedAudio> Audios { get; set; } = new();
    }
}