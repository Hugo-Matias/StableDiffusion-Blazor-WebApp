namespace BlazorWebApp.Models
{
    /// <summary>
    /// Loader settings for an audio source (trim start, trim duration).
    /// Used by DramaBox and other audio-based workflows.
    /// </summary>
    public class AudioSourceOptions
    {
        /// <summary>
        /// Trim start offset in seconds.
        /// </summary>
        public double TrimStart { get; set; }

        /// <summary>
        /// Trim duration in seconds (0 = play to end).
        /// </summary>
        public double TrimDuration { get; set; }

        public AudioSourceOptions Clone()
        {
            return new AudioSourceOptions
            {
                TrimStart = TrimStart,
                TrimDuration = TrimDuration
            };
        }
    }
}
