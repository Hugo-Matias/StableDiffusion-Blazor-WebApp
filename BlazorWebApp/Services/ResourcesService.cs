using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    public class ResourcesService
    {
        private readonly ManagerService _m;
        private readonly IOService _io;
        private readonly DatabaseService _db;
        private readonly IConfiguration _configuration;

        public ResourcesService(ManagerService manager, IOService io, DatabaseService db, IConfiguration configuration)
        {
            _m = manager;
            _io = io;
            _db = db;
            _configuration = configuration;
        }

        public async Task<List<LocalResource>> CreateLocalResourcesByType(int typeId)
        {
            List<LocalResource> resources = new();
            var entities = await _db.GetResources(typeId);
            if (entities != null && entities.Count > 0)
            {
                var type = entities.FirstOrDefault()!.Type.Name;
                if (type.Equals("vae", StringComparison.InvariantCultureIgnoreCase))
                {
                    resources.AddRange(new List<LocalResource> {
                        new() { Title = "None", Type = new() { Name = type } },
                        new() { Title = "Automatic", Type = new() { Name = type } } }
                    );
                }
                foreach (var entity in entities)
                {
                    if (resources.Any(r => r.Title == entity.Title))
                    {
                        var createdResource = resources.FirstOrDefault(r => r.Title == entity.Title);
                        var file = new LocalResourceFile(entity);
                        file.ImageSrc = _io.GetResourceImagePath(createdResource.Type.Name, file.Filename);
                        file.Title = Path.GetFileNameWithoutExtension(file.Filename);
                        file.IsEnabled = entity.IsEnabled;
                        createdResource.Files.Insert(0, file);
                        createdResource.ImageSrc = file.ImageSrc;
                        //var subtype = entity.SubType != null && !string.IsNullOrWhiteSpace(entity.SubType.Name) ? entity.SubType.Name : null;
                        //var localFile = await GetResourceFileInfo(entity.Type.Name, subtype, file);
                        //if (localFile == null)
                        //{
                        //    Console.ForegroundColor = ConsoleColor.Red;
                        //    await Console.Out.WriteLineAsync($"Missing File: {entity.Id}-{entity.Filename} ({entity.Type.Name}/{entity.SubType?.Name})");
                        //    Console.ResetColor();
                        //    continue;
                        //}
                        //else createdResource.CreatedDate = localFile.File.LastAccessTimeUtc;
                        if (entity.CreatedDate < createdResource.CreatedDate)
                        {
                            await Console.Out.WriteLineAsync($"Updating Resource Date: {entity.Title} ({entity.Type.Name}/{entity.SubType?.Name})");
                            entity.CreatedDate = createdResource.CreatedDate;
                            await _db.UpdateResource(entity);
                        }
                    }
                    else
                    {
                        resources.Add(await CreateLocalResourceByEntity(entity));
                    }
                }
            }
            return resources.OrderBy(r => r.Title).ToList();
        }

        public async Task<LocalResource> CreateLocalResourceByEntity(Resource entity)
        {
            var resource = new LocalResource(entity);
            var file = new LocalResourceFile(entity);
            file.ImageSrc = _io.GetResourceImagePath(resource.Type.Name, file.Filename);
            file.Title = Path.GetFileNameWithoutExtension(file.Filename);
            file.IsEnabled = entity.IsEnabled;
            resource.ImageSrc = file.ImageSrc;
            resource.Files = new() { file };
            if (entity.CreatedDate == DateTime.MinValue)
            {
                var subtype = entity.SubType != null && !string.IsNullOrWhiteSpace(entity.SubType.Name) ? entity.SubType.Name : null;
                var localFile = await GetResourceFileInfo(entity.Type.Name, subtype, file);
                if (localFile == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    await Console.Out.WriteLineAsync($"Missing File:  {entity.Id}-{entity.Filename} ({entity.Type.Name}/{entity.SubType?.Name})");
                    Console.ResetColor();
                    return resource;
                }
                entity.CreatedDate = localFile.File.LastWriteTimeUtc;
                await _db.UpdateResource(entity);
                await Console.Out.WriteLineAsync($"Updated CreatedDate for: {entity.Id}-{entity.Title} ({entity.Type.Name}/{entity.SubType?.Name})");
            }
            return resource;
        }

        public async Task<LocalResourceFile?> GetResourceFileInfo(string resourceType, string? resourceSubtype, LocalResourceFile file)
        {
            var comp = StringComparison.InvariantCultureIgnoreCase;
            var fileDir = _m.ResourceTypeDirectories.FirstOrDefault(f => f.Key.Equals(resourceType, comp)).Value;
            if (string.IsNullOrWhiteSpace(fileDir)) fileDir = Path.Combine(_configuration["ResourcesPath"], resourceType);
            if (!string.IsNullOrWhiteSpace(resourceSubtype)) fileDir = Path.Combine(fileDir, resourceSubtype);
            if (File.Exists(Path.Combine(fileDir, file.Filename)))
            {
                file.IsEnabled = true;
                file.File = new FileInfo(Path.Combine(fileDir, file.Filename));
            }
            else
            {
                file.IsEnabled = false;
                var storagePath = Path.Combine(_configuration["ResourcesPath"], "_storage", resourceType);
                if (!string.IsNullOrWhiteSpace(resourceSubtype)) storagePath = Path.Combine(storagePath, resourceSubtype);
                storagePath = Path.Combine(storagePath, file.Filename);
                if (File.Exists(storagePath)) file.File = new FileInfo(storagePath);
                else return null;
            }
            if (file.SizeKb <= 0 && file.CivitaiId != null && file.CivitaiId > 0)
            {
                file.SizeKb = file.File.Length / 1024;
                var entity = await _db.GetResourceById(file.ResourceId);
                entity.SizeKb = file.SizeKb;
                await _db.UpdateResource(entity);
            }
            return file;
        }

        public async Task LoadPrompt(LocalResourceFile file, string resourceType, ValueTuple<ModeType, bool> target)
        {
            var comp = StringComparison.InvariantCultureIgnoreCase;
            var filename = file.File.Name.Replace(file.File.Extension, "");
            var keyword = string.Empty;
            var triggerWords = string.Empty;
            var weight = _m.State.Resources.Weight;

            if (resourceType.Equals("TextualInversion", comp)) keyword = weight != 1 ? $", ({filename}:{weight})" : filename;
            else if (resourceType.Equals("Hypernetwork", comp)) keyword = $", <hypernet:{filename}:{weight}>";
            else if (resourceType.Equals("LORA", comp) || resourceType.Equals("LoCon", comp)) keyword = $", <lora:{filename}:{weight}>";

            if (_m.State.Resources.LoadTriggerWords && file.TriggerWords != null)
            {
                triggerWords = ", ";
                triggerWords += string.Join(", ", file.TriggerWords);
            }

            if (target.Item1 == ModeType.Txt2Img)
            {
                if (target.Item2 == true) _m.ParametersTxt2Img.Prompt += $"{keyword}{triggerWords}";
                else _m.ParametersTxt2Img.NegativePrompt += $", {keyword}{triggerWords}";
            }
            else if (target.Item1 == ModeType.Img2Img)
            {
                if (target.Item2 == true) _m.ParametersImg2Img.Prompt += $"{keyword}{triggerWords}";
                else _m.ParametersImg2Img.NegativePrompt += $"{keyword}{triggerWords}";
            }
        }

        public async Task UpdateResource(Resource resource, string directory, string filename, int resourceId, bool isEnabled)
        {
            var fileInfos = _io.GetFilesByName(directory, filename);
            var baseDestPath = isEnabled ? _m.ResourceTypeDirectories[resource.Type.Name] : Path.Combine(_configuration["ResourcesPath"], "_storage", resource.Type.Name);
            if (resource.SubType != null) baseDestPath = Path.Combine(baseDestPath, resource.SubType.Name);
            // Update other files linked to the model that must share the same name, ie. yaml configs or txt info
            foreach (var fileInfo in fileInfos)
            {
                var currentFile = new FileInfo(resource.Filename);
                var file = currentFile.Name.Replace(currentFile.Extension, fileInfo.Extension);
                var sourcePath = fileInfo.FullName;
                var destinationPath = Path.Combine(baseDestPath, file);
                _io.MoveFile(sourcePath, destinationPath);
            }
            // Update resource preview
            var resourceEntity = await _db.GetResourceById(resourceId);
            var previewFiles = _io.GetFilesByName(Path.Combine(_configuration["ResourcePreviewsPath"], resourceEntity.Type.Name), filename).ToArray();
            if (previewFiles != null)
            {
                //if (previewFiles.Length > 1) await Console.Out.WriteLineAsync("More than one resource preview found.");
                var previewFilename = new FileInfo(resource.Filename);
                var previewDestination = Path.Combine(_configuration["ResourcePreviewsPath"], resource.Type.Name, previewFilename.Name.Replace(previewFilename.Extension, previewFiles[0].Extension));
                _io.MoveFile(previewFiles[0].FullName, previewDestination);
            }
            await _db.UpdateResource(resource);
        }

        public async Task DeleteResource(Resource resource, bool deleteFiles, string directory, string filename)
        {
            if (deleteFiles)
            {
                var subtypeDir = Path.Combine(_configuration["ImagesPathVault"], "Saved", resource.Type.Name);
                var modelImagesDir = _io.GetFolderByName(subtypeDir, resource.CivitaiModelId.ToString());
                if (modelImagesDir != null)
                {
                    var versionImagesDir = _io.GetFolderByName(modelImagesDir.FullName, resource.CivitaiModelVersionId.ToString());
                    if (versionImagesDir != null) _io.DeleteFolder(versionImagesDir, true);
                    var versionImage = _io.GetFileByName(modelImagesDir.FullName, resource.CivitaiModelVersionId.ToString());
                    if (versionImage != null) _io.DeleteFile(versionImage);
                }

                var files = _io.GetFilesByName(directory, filename);
                foreach (var localFile in files)
                {
                    _io.DeleteFile(localFile.FullName);
                }
            }
            await _db.DeleteResource(resource.Id);
            await _db.DeleteResourceImage((int)resource.CivitaiModelVersionId);
        }

        public async Task ToggleResource(LocalResource resource, LocalResourceFile file)
        {
            if (_m.ResourceTypeDirectories == null) await _m.GetResourceTypeDirectories();
            string destPath = string.Empty;
            if (file.IsEnabled) destPath = Path.Combine(_configuration["ResourcesPath"], "_storage", resource.Type.Name);
            else destPath = _m.ResourceTypeDirectories.FirstOrDefault(p => p.Key.Equals(resource.Type.Name, StringComparison.InvariantCultureIgnoreCase)).Value;
            if (resource.SubType != null) destPath = Path.Combine(destPath, resource.SubType.Name);
            destPath = Path.Combine(destPath, file.Filename);
            _io.MoveFile(file.File.FullName, destPath);
            await _db.ToggleResourceState(file.ResourceId);
        }
    }
}
