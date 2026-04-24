using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Interface for file system I/O operations.
    /// Provides methods for file and directory management, image handling, and metadata operations.
    /// </summary>
    public interface IIOService
    {
        // File Operations
        void MoveFile(string sourcePath, string destinationPath);
        void DeleteFile(FileInfo file);
        void DeleteFile(string path);
        string GetJsonAsString(string path);
        string? LoadText(string path);
        string[]? LoadTextLines(string path);
        void SaveText(string path, string content, bool overwrite = true);
        Task SaveFileToDisk(string path, byte[] data);

        // Directory Operations
        DirectoryInfo CreateDirectory(string path);
        void DeleteFolder(DirectoryInfo dir, bool isRecursive);
        DirectoryInfo? GetFolderByName(string path, string folderName);

        // File Discovery
        FileInfo? GetFileByName(string path, string fileName);
        IOrderedEnumerable<FileInfo>? GetOrderedFiles(string path);
        IEnumerable<FileInfo> GetFilesByName(string path, string name);
        IEnumerable<FileInfo> GetFilesRecursive(string path, string? ignorePath = null, List<string>? extensionsBlacklist = null, List<string>? extensionsWhitelist = null);

        // Image Operations
        Task<List<ImageInfo>?> GetImages(string path);
        string GetImageStaticFile(string path);
        string GetResourceImagePath(string type, string filename);
        string GetBase64FromFile(string path);
        Task<string?> GetBase64FromFileAsync(string path);
        /// <summary>
        /// Resolve a web request path (e.g. /files/danbooru/...) to a filesystem path.
        /// Returns the original path if it is already a filesystem path.
        /// </summary>
        string ResolveFilePath(string path);

        // Index/Pattern Operations
        int GetFileIndex(string path, Outdir dir);

        // Metadata Operations
        Task<string> ReadMetadata(string path);
    }
}
