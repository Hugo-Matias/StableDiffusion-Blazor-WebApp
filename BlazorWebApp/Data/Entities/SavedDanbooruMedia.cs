using BlazorWebApp.Data.Dtos;

namespace BlazorWebApp.Data.Entities
{
    /// <summary>
    /// Database row for a Danbooru post saved to the local library.
    /// Scalar columns denormalize the fields used for filtering/sorting;
    /// the full tag bundle is stored as JSON in <see cref="TagsBundle"/>.
    /// </summary>
    public class SavedDanbooruMedia
    {
        /// <summary>Database primary key (auto-increment).</summary>
        public int Id { get; set; }

        /// <summary>
        /// Stable domain identifier matching the Danbooru post ID. Indexed and unique.
        /// </summary>
        public int DanbooruPostId { get; set; }

        /// <summary>Denormalized rating value (general, sensitive, questionable, explicit, none).</summary>
        public string Rating { get; set; } = string.Empty;

        /// <summary>Denormalized score value, used for folder bucketing and filtering.</summary>
        public int Score { get; set; }

        /// <summary>Image/video width in pixels.</summary>
        public int Width { get; set; }

        /// <summary>Image/video height in pixels.</summary>
        public int Height { get; set; }

        /// <summary>File extension (jpg, png, mp4, etc.).</summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>Whether this media is a video.</summary>
        public bool IsVideo { get; set; }

        /// <summary>Original Danbooru CDN URL (<c>file_url</c>).</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Large sample URL (<c>large_file_url</c>).</summary>
        public string SampleUrl { get; set; } = string.Empty;

        /// <summary>Preview URL (<c>preview_file_url</c>).</summary>
        public string PreviewUrl { get; set; } = string.Empty;

        /// <summary>
        /// Relative path within <c>SavedMediaPath</c> where the file was saved
        /// (e.g., <c>explicit/score_300/123456.jpg</c>).
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Creation timestamp (UTC).</summary>
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Full tag bundle serialized via <see cref="DanbooruTagBundleConverter"/>.
        /// </summary>
        public DanbooruTagBundle TagsBundle { get; set; } = new();

        public SavedDanbooruMedia() { }

        /// <summary>
        /// Constructs a new entity from a <see cref="DanbooruPost"/> DTO.
        /// </summary>
        public SavedDanbooruMedia(DanbooruPost post)
        {
            DanbooruPostId = post.Id;
            Rating = post.Rating ?? string.Empty;
            Score = post.Score;
            Width = post.Width;
            Height = post.Height;
            Extension = post.Extension ?? string.Empty;
            IsVideo = post.IsVideo;
            Url = post.Url ?? string.Empty;
            SampleUrl = post.SampleUrl ?? string.Empty;
            PreviewUrl = post.PreviewUrl ?? string.Empty;

            TagsBundle = new DanbooruTagBundle
            {
                General = post.TagsGeneral ?? new List<string>(),
                Artist = post.TagsArtist ?? new List<string>(),
                Character = post.TagsCharacter ?? new List<string>(),
                Copyright = post.TagsCopyright ?? new List<string>(),
                Meta = post.TagsMeta ?? new List<string>(),
            };
        }
    }
}
