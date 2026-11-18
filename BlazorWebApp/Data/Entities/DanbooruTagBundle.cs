namespace BlazorWebApp.Data.Entities
{
    /// <summary>
    /// Tag bundle for a saved Danbooru post, serialized as JSON via ValueConverter.
    /// Mirrors the five tag categories returned by the Danbooru API.
    /// </summary>
    public class DanbooruTagBundle
    {
        /// <summary>General tags (media type, quality, techniques, etc.)</summary>
        public List<string> General { get; set; } = new();

        /// <summary>Artist tags.</summary>
        public List<string> Artist { get; set; } = new();

        /// <summary>Character tags.</summary>
        public List<string> Character { get; set; } = new();

        /// <summary>Copyright tags (franchise, series, etc.)</summary>
        public List<string> Copyright { get; set; } = new();

        /// <summary>Meta tags (source, artist name, etc.)</summary>
        public List<string> Meta { get; set; } = new();
    }
}
