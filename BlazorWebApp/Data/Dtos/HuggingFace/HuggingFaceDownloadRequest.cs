using BlazorWebApp.Services;

namespace BlazorWebApp.Data.Dtos.HuggingFace
{
    /// <summary>
    /// Describes a single HuggingFace download operation: which files to fetch,
    /// how to map them to local resources, and where to store them.
    /// All Type/SubType/BaseModel fields must be confirmed by the caller before calling
    /// <c>DownloadAsync</c> — the service will not validate mapping correctness.
    /// </summary>
    public class HuggingFaceDownloadRequest
    {
        /// <summary>Repository identity in "author/repo" format.</summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>Git revision / branch / tag to download from. Defaults to "main".</summary>
        public string Revision { get; set; } = "main";

        /// <summary>Files selected for download.</summary>
        public List<HuggingFaceFileSelection> Files { get; set; } = new();

        /// <summary>Confirmed resource type name (e.g. "Checkpoint", "LORA"). Required.</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>Confirmed resource sub-type name (e.g. "SafeTensor"). Use null or "none" for no sub-type.</summary>
        public string? SubTypeName { get; set; }

        /// <summary>User-confirmed base model string (e.g. "Flux", "SDXL 1.0").</summary>
        public string? BaseModel { get; set; }

        /// <summary>
        /// When true, repo-relative folder structure is preserved under the destination root.
        /// When false, all files are downloaded flat into the destination root.
        /// </summary>
        public bool KeepFolderStructure { get; set; }

        /// <summary>
        /// When true, the service will attempt to parse the repo README for a preview image.
        /// </summary>
        public bool DownloadPreview { get; set; } = true;

        /// <summary>
        /// Inferred mapping hints (from <see cref="HuggingFaceModelMapper.InferHints"/>),
        /// stored on the Resource for reference but not used to override confirmed mapping.
        /// </summary>
        public HuggingFaceMappingHints? InferredHints { get; set; }
    }

    /// <summary>A single file selected from a HuggingFace repository for download.</summary>
    public class HuggingFaceFileSelection
    {
        /// <summary>Repo-relative file path (e.g. "unet/model.safetensors").</summary>
        public string RepoFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Optional override for the local output filename (bare name only, no path).
        /// When null or empty the original filename from <see cref="RepoFilePath"/> is used.
        /// </summary>
        public string? OutputFilename { get; set; }

        /// <summary>File size in kilobytes, as reported by the tree/paths-info endpoint.</summary>
        public double SizeKb { get; set; }

        /// <summary>LFS SHA-256 or file SHA from the tree entry, when available.</summary>
        public string? Sha { get; set; }

        /// <summary>
        /// When true, a <c>Resource</c> DB row is created for this file.
        /// Set to false for companion files (configs, tokenizer JSON, etc.) that should
        /// be stored on disk but not tracked as addressable resources.
        /// </summary>
        public bool IsResourceFile { get; set; } = true;
    }

    // -------------------------------------------------------------------------
    // Result types
    // -------------------------------------------------------------------------

    /// <summary>Outcome status for <see cref="HuggingFaceDownloadRequest"/> execution.</summary>
    public enum HuggingFaceDownloadStatus
    {
        /// <summary>All files downloaded and all resource DB rows created.</summary>
        Success,

        /// <summary>At least one file downloaded successfully, but others failed or were skipped.</summary>
        PartialSuccess,

        /// <summary>All requested resource files already exist in the DB or on disk — nothing was written.</summary>
        AlreadyExists,

        /// <summary>Flat download was blocked because two or more files share the same bare filename.</summary>
        PathConflict,

        /// <summary>Download was cancelled before completion.</summary>
        Cancelled,

        /// <summary>One or more files failed; no files were downloaded successfully.</summary>
        Error,
    }

    /// <summary>Detailed result of a <see cref="HuggingFaceDownloadRequest"/> execution.</summary>
    public class HuggingFaceDownloadResult
    {
        public HuggingFaceDownloadStatus Status { get; set; }
        public List<string> DownloadedFiles { get; set; } = new();
        public List<string> SkippedFiles { get; set; } = new();
        public List<string> FailedFiles { get; set; } = new();
        public IReadOnlySet<string>? ConflictingFilenames { get; set; }
        public string? Message { get; set; }

        public static HuggingFaceDownloadResult Conflict(IReadOnlySet<string> names) =>
            new() { Status = HuggingFaceDownloadStatus.PathConflict, ConflictingFilenames = names, Message = $"Flat download blocked: {names.Count} filename collision(s): {string.Join(", ", names)}" };

        public static HuggingFaceDownloadResult Fail(string message) =>
            new() { Status = HuggingFaceDownloadStatus.Error, Message = message };
    }
}
