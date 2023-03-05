using BlazorWebApp.Models;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class IOService
    {
        private readonly IConfiguration _configuration;

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

        public IEnumerable<FileInfo> GetFilesRecursive(string path)
        {
            static void GetFiles(DirectoryInfo dir, ref List<FileInfo> files)
            {
                foreach (var subdir in dir.GetDirectories())
                {
                    GetFiles(subdir, ref files);
                }
                files.AddRange(dir.GetFiles().OrderBy(f => f.Name).ToList());
            }

            if (!Directory.Exists(path)) return Array.Empty<FileInfo>();
            try
            {
                List<FileInfo> files = new();
                var directory = new DirectoryInfo(path);
                GetFiles(directory, ref files);
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

        public async Task SaveFileToDisk(string path, byte[] data) => await File.WriteAllBytesAsync(path, data);

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

        public void SaveText(string path, string content) => File.WriteAllText(path, content);

        public DirectoryInfo CreateDirectory(string path) => Directory.CreateDirectory(path);
    }
}
