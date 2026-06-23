namespace BlazorWebApp.Models
{
    /// <summary>
    /// Configuration for Danbooru API credentials and library storage path.
    /// Maps to the "Danbooru" section in appsettings.json.
    /// </summary>
    public class DanbooruOptions
    {
        /// <summary>
        /// Configuration section name in appsettings.json
        /// </summary>
        public const string SectionName = "Danbooru";

        /// <summary>
        /// Danbooru API login username.
        /// </summary>
        public string Login { get; set; } = string.Empty;

        /// <summary>
        /// Danbooru API key for authentication.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Root directory where saved Danbooru media files are stored.
        /// Media is organized into subfolders by rating and score bucket.
        /// </summary>
        public string SavedMediaPath { get; set; } = string.Empty;
    }
}
