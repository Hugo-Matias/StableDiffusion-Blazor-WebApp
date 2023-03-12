using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class IOService
    {
        private readonly IConfiguration _configuration;
        private readonly string[] _imagePaths = new string[3] { "ImagesPathLocal", "ImagesPathCloud", "ImagesPathVault" };

        public IOService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GetJsonAsString(string path) => new string(File.ReadAllText(path));

        public IOrderedEnumerable<FileInfo>? GetOrderedFiles(string path)
        {
            if (!Directory.Exists(path)) return null;

            try
            {
                var directory = new DirectoryInfo(path);

                // Order by name is important to get txt/img file pairs
                var files = directory.GetFiles().OrderBy(f => f.Name);
                return files;
            }
            catch (Exception e)
            {
                throw new Exception($"Couldn't read files from directory: {path}\n", e);
            }
        }

        public IEnumerable<FileInfo> GetFilesRecursive(string path, string? ignorePath = null, List<string>? extensionsBlacklist = null, List<string>? extensionsWhitelist = null)
        {
            void GetFiles(DirectoryInfo dir, ref List<FileInfo> files)
            {
                foreach (var subdir in dir.GetDirectories())
                {
                    GetFiles(subdir, ref files);
                }
                var query = dir.GetFiles().AsQueryable();
                if (extensionsBlacklist != null)
                {
                    query = query.Where(f => !extensionsBlacklist.Contains(f.Extension));
                }
                if (extensionsWhitelist != null)
                {
                    query = query.Where(f => extensionsWhitelist.Contains(f.Extension));
                }
                query = query.OrderBy(f => f.Name);
                files.AddRange(query.ToList());
            }

            if (!Directory.Exists(path)) return Array.Empty<FileInfo>();
            try
            {
                List<FileInfo> files = new();
                if (string.IsNullOrWhiteSpace(ignorePath) || !path.Contains(ignorePath, StringComparison.InvariantCultureIgnoreCase))
                {
                    var directory = new DirectoryInfo(path);
                    GetFiles(directory, ref files);
                }
                return files;
            }
            catch (Exception e)
            {
                throw new Exception($"Couldn't read files from directory: {path}\n", e);
            }
        }

        public async Task<List<ImageInfo>?> GetImages(string path)
        {
            if (!Directory.Exists(path)) return null;

            var files = GetOrderedFiles(path);
            var images = new List<ImageInfo>();
            var currentFileName = string.Empty;
            ImageInfo currentImage = null;

            foreach (var file in files)
            {
                var fileName = file.Name.Replace(file.Extension, "");

                if (currentFileName != fileName)
                {
                    currentFileName = fileName;
                    currentImage = new();
                }

                if (file.Extension == ".txt")
                {
                    currentImage.InfoString = LoadTextLines(file.FullName);
                }
                else
                {
                    try
                    {
                        currentImage.Image = await GetBase64FromFileAsync(file.FullName);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Couldn't get file: {file.Name}\n", e);
                    }
                }

                if (currentImage.Image != null && currentImage.InfoString != null)
                    images.Add(currentImage);
            }

            return images;
        }

        public string GetImageStaticFile(string path)
        {
            foreach (var imagePath in _imagePaths)
            {
                var normalizedPath = Parser.NormalizePath(path);
                var normalizedImagePath = Parser.NormalizePath(_configuration[imagePath]);
                if (normalizedPath.Contains(normalizedImagePath))
                {
                    var imageFile = normalizedPath.Replace(normalizedImagePath, "").Replace(@"\", "/");
                    return imagePath switch
                    {
                        "ImagesPathLocal" => "/image/local" + imageFile,
                        "ImagesPathCloud" => "/image/cloud" + imageFile,
                        "ImagesPathVault" => "/image/vault" + imageFile,
                        _ => string.Empty
                    };
                }
            }
            return string.Empty;
        }

        public string GetResourceImagePath(string type, string filename)
        {
            var previewPath = $"{type}/{Path.GetFileNameWithoutExtension(filename)}";
            if (File.Exists(Path.Join(_configuration["ResourcePreviewsPath"], previewPath + ".png"))) return $"./files/resource_previews/{previewPath}.png";
            else if (File.Exists(Path.Join(_configuration["ResourcePreviewsPath"], previewPath + ".jpg"))) return $"./files/resource_previews/{previewPath}.jpg";
            else return string.Empty;
        }

        public string GetBase64FromFile(string path)
        {
            if (!File.Exists(path)) return string.Empty;

            var bytes = File.ReadAllBytes(path);

            return Convert.ToBase64String(bytes);
        }

        public async Task<string?> GetBase64FromFileAsync(string path)
        {
            if (!File.Exists(path)) return null;

            //var bytes = await GetByteArray(path);
            var bytes = await File.ReadAllBytesAsync(path);

            return Convert.ToBase64String(bytes);
        }

        public int GetFileIndex(string path, Outdir dir)
        {
            FileInfo? lastFile = GetLastSavedFile(path);
            int fileIndex;

            if (lastFile != null)
            {
                var pattern = string.Empty;

                switch (dir)
                {
                    case Outdir.Txt2ImgSamples:
                    case Outdir.Img2ImgSamples:
                        pattern = @"(\d+)-";
                        break;
                    case Outdir.Txt2ImgGrid:
                    case Outdir.Img2ImgGrid:
                        pattern = @"-(\d+)";
                        break;
                    case Outdir.Extras:
                        pattern = @"(\d+)";
                        break;
                }
                fileIndex = int.Parse(Regex.Match(lastFile.Name, pattern).Groups[1].Value);
            }
            else
            {
                fileIndex = 0;
            }

            return fileIndex;
        }

        private FileInfo? GetLastSavedFile(string path) => GetOrderedFiles(path).LastOrDefault();

        public string[]? LoadTextLines(string path)
        {
            if (!File.Exists(path)) return null;

            return File.ReadAllLines(path);
        }

        public string? LoadText(string path)
        {
            if (!File.Exists(path)) return null;

            return File.ReadAllText(path);
        }

        public void SaveText(string path, string content, bool overwrite = true)
        {
            if (File.Exists(path) && !overwrite) return;
            CheckDirectory(path);
            File.WriteAllText(path, content);
        }

        public async Task SaveFileToDisk(string path, byte[] data)
        {
            CheckDirectory(path);
            await File.WriteAllBytesAsync(path, data);
        }

        private void CheckDirectory(string path)
        {
            path = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(path))
                Directory.CreateDirectory(path);
        }

        public DirectoryInfo CreateDirectory(string path) => Directory.CreateDirectory(path);
    }
}
