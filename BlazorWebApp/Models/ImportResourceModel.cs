using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Represents an untracked resource file detected during import scanning.
    /// </summary>
    public class ImportResourceModel
    {
        /// <summary>
        /// The physical file reference.
        /// </summary>
        public FileInfo File { get; set; }

        /// <summary>
        /// The filename only (without path).
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// The detected resource type based on parent directory.
        /// </summary>
        public ResourceType Type { get; set; }

        /// <summary>
        /// True if file is in main type path (active), false if in _storage (inactive).
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// File size in kilobytes.
        /// </summary>
        public double SizeKb { get; set; }

        /// <summary>
        /// Full path to the file for debugging/logging.
        /// </summary>
        public string DetectedPath { get; set; }

        /// <summary>
        /// Optional cover image path to use for this specific file.
        /// </summary>
        public string? CoverImagePath { get; set; }

        public ImportResourceModel()
        {
        }

        public ImportResourceModel(FileInfo file, ResourceType type, bool isEnabled)
        {
            File = file;
            Filename = file.Name;
            Type = type;
            IsEnabled = isEnabled;
            SizeKb = file.Length / 1024.0;
            DetectedPath = file.FullName;
        }
    }
}
