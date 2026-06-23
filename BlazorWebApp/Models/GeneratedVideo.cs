namespace BlazorWebApp.Models
{
    /// <summary>
    /// Represents a generated video output from ComfyUI
    /// </summary>
    public class GeneratedVideo
    {
        /// <summary>
        /// Database ID if persisted (matches Image.Id)
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The video file data as base64 string (for smaller files) or file path
        /// </summary>
        public string VideoData { get; set; }

        /// <summary>
        /// Path to the saved video file
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Filename of the video
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Video width in pixels
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Video height in pixels
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Number of frames in the video
        /// </summary>
        public int FrameCount { get; set; }

        /// <summary>
        /// Frame rate (fps)
        /// </summary>
        public int FrameRate { get; set; }

        /// <summary>
        /// Duration in seconds
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// The prompt used for generation
        /// </summary>
        public string Prompt { get; set; }

        /// <summary>
        /// The negative prompt used for generation
        /// </summary>
        public string NegativePrompt { get; set; }

        /// <summary>
        /// Seed used for generation
        /// </summary>
        public long Seed { get; set; }

        /// <summary>
        /// Steps used for generation
        /// </summary>
        public int Steps { get; set; }

        /// <summary>
        /// CFG Scale used for generation
        /// </summary>
        public float CfgScale { get; set; }

        /// <summary>
        /// Sampler name used for generation
        /// </summary>
        public string Sampler { get; set; }

        /// <summary>
        /// Model name used for generation
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// Timestamp when the video was generated
        /// </summary>
        public DateTime DateCreated { get; set; } = DateTime.Now;

        /// <summary>
        /// Whether the video is marked as favorite
        /// </summary>
        public bool Favorite { get; set; }

        /// <summary>
        /// Rating score (0-3)
        /// </summary>
        public int Score { get; set; }
    }

    /// <summary>
    /// Container for multiple generated videos
    /// </summary>
    public class GeneratedVideos
    {
        public List<GeneratedVideo> Videos { get; set; } = new();
    }
}
