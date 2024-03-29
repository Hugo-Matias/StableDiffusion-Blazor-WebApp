﻿using BlazorWebApp.Extensions;
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

        public void MoveFile(string sourcePath, string destinationPath)
        {
            var destFile = new FileInfo(destinationPath);
            if (!destFile.Directory.Exists) Directory.CreateDirectory(destFile.DirectoryName);
            File.Move(sourcePath, destinationPath, true);
        }

        public string GetJsonAsString(string path) => new string(File.ReadAllText(path));

        public DirectoryInfo? GetFolderByName(string path, string folderName)
        {
            var dir = Directory.GetDirectories(path, $"{folderName}*").FirstOrDefault();
            if (dir == null) return null;
            return new DirectoryInfo(dir);
        }

        public FileInfo? GetFileByName(string path, string fileName)
        {
            var file = Directory.GetFiles(path, $"{fileName}*").FirstOrDefault();
            if (file == null) return null;
            return new FileInfo(file);
        }

        public void DeleteFolder(DirectoryInfo dir, bool isRecursive) => dir.Delete(isRecursive);

        public void DeleteFile(FileInfo file) => file.Delete();

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

        public IEnumerable<FileInfo> GetFilesByName(string path, string name)
        {
            if (!Directory.Exists(path)) return Array.Empty<FileInfo>();
            try
            {
                var dir = new DirectoryInfo(path);
                return dir.GetFiles().Where(f => f.Name.Replace(f.Extension, "").Equals(name));
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
            if (path.StartsWith("/image/")) return path;
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
            var files = GetOrderedFiles(path).ToList();
            FileInfo? lastFile = null;
            if (files != null && files.Count > 0)
            {
                files.Reverse();
                var index = 1;
                while (lastFile == null && index <= files.Count)
                {
                    foreach (var file in files)
                    {
                        index++;
                        if (!file.Name.StartsWith("xyz_grid", StringComparison.InvariantCultureIgnoreCase))
                        {
                            lastFile = file;
                            break;
                        }
                    }
                }
            }
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

        public void DeleteFile(string path) => File.Delete(path);
    }
}
