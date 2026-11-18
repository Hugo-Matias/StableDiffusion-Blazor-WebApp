namespace BlazorWebApp.Data.Dtos
{
    /// <summary>
    /// Represents a caption source — one CSV file under Data/Captions/.
    /// </summary>
    public class CaptionSource
    {
        /// <summary>
        /// Source name (filename without extension), used as the lookup key.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Display name for UI (first letter capitalized).
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Number of caption rows in this source.
        /// </summary>
        public int RowCount { get; set; }
    }
}
