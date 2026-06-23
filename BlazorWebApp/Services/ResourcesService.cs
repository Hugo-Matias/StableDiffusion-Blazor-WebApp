using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    public class ResourcesService : IResourcesService
    {
        private readonly IStateService _state;
        private readonly IIOService _io;
        private readonly IDatabaseService _db;
        private readonly IConfiguration _configuration;
        private readonly IEventService _events;
        private readonly Dictionary<string, string> _resourceTypeDirectories;

        public ResourcesService(IStateService state, IIOService io, IDatabaseService db, IConfiguration configuration, IEventService events)
        {
            _state = state;
            _io = io;
            _db = db;
            _configuration = configuration;
            _events = events;

            // Build resource type directories from configuration
            var baseDir = _configuration["ResourcesPath"]!;
            _resourceTypeDirectories = new()
            {
                {"Checkpoint", Path.Combine(baseDir, "Checkpoint")},
                {"Diffusion", Path.Combine(baseDir, "Diffusion")},
                {"TextualInversion", Path.Combine(baseDir, "TextualInversion")},
                {"Hypernetwork", Path.Combine(baseDir, "Hypernetwork")},
                {"LORA", Path.Combine(baseDir, "LORA")},
                {"LoCon", Path.Combine(baseDir, "LORA")},
                {"VAE", Path.Combine(baseDir, "VAE")}
            };
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
                        var createdResource = resources.FirstOrDefault(r => r.Title == entity.Title)!;
                        var file = new LocalResourceFile(entity);
                        file.ImageSrc = _io.GetResourceImagePath(createdResource.Type.Name, file.Filename);
                        file.Title = Path.GetFileNameWithoutExtension(file.Filename);
                        file.IsEnabled = entity.IsEnabled;
                        createdResource.Files.Insert(0, file);
                        createdResource.ImageSrc = file.ImageSrc;
                        if (entity.CreatedDate > createdResource.CreatedDate) createdResource.CreatedDate = entity.CreatedDate;
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
            var fileDir = _resourceTypeDirectories.FirstOrDefault(f => f.Key.Equals(resourceType, comp)).Value;
            if (string.IsNullOrWhiteSpace(fileDir)) fileDir = Path.Combine(_configuration["ResourcesPath"]!, resourceType);
            if (!string.IsNullOrWhiteSpace(resourceSubtype)) fileDir = Path.Combine(fileDir, resourceSubtype);
            if (File.Exists(Path.Combine(fileDir, file.Filename)))
            {
                file.IsEnabled = true;
                file.File = new FileInfo(Path.Combine(fileDir, file.Filename));
            }
            else
            {
                file.IsEnabled = false;
                var storagePath = Path.Combine(_configuration["ResourcesPath"]!, "_storage", resourceType);
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
            var weight = _state.State.Resources.Weight;

            if (resourceType.Equals("TextualInversion", comp)) keyword = weight != 1 ? $", ({filename}:{weight})" : filename;
            else if (resourceType.Equals("Hypernetwork", comp)) keyword = $", <hypernet:{filename}:{weight}>";
            else if (resourceType.Equals("LORA", comp) || resourceType.Equals("LoCon", comp))
            {
                var resourcesPath = _configuration["ResourcesPath"]!;
                var loraBasePath = Path.Combine(resourcesPath, resourceType);
                var fullPath = file.File.FullName;
                var subPath = fullPath.StartsWith(loraBasePath, comp)
                    ? fullPath.Substring(loraBasePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    : file.File.Name;

                var newLora = new Lora { Name = filename, Path = subPath, Strength = weight, IsNegative = !target.Item2, IsEnabled = true };

                // Update GenerationParameters.Loras (primary)
                if (!_state.GenerationParameters.Loras.Any(l => l.Name.Equals(filename, comp)))
                {
                    _state.GenerationParameters.Loras.Add(new Lora(newLora));
                }
            }

            if (_state.State.Resources.LoadTriggerWords && file.TriggerWords != null)
            {
                triggerWords = ", ";
                triggerWords += string.Join(", ", file.TriggerWords);
            }

            // Update GenerationParameters prompts fragment (primary)
            var promptsFragment = _state.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts, FragmentKeys.Fragments.Prompts);
            var promptKey = target.Item2 ? FragmentKeys.Params.Positive : FragmentKeys.Params.Negative;
            var currentPrompt = promptsFragment.GetValue<string>(promptKey) ?? "";
            promptsFragment.SetValue(promptKey, currentPrompt + $"{triggerWords}{keyword}");
        }

        public async Task UpdateResource(Resource resource, string directory, string filename, int resourceId, bool isEnabled)
        {
            var fileInfos = _io.GetFilesByName(directory, filename);
            var baseDestPath = isEnabled ? _resourceTypeDirectories[resource.Type.Name] : Path.Combine(_configuration["ResourcesPath"]!, "_storage", resource.Type.Name);
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
            var previewFiles = _io.GetFilesByName(Path.Combine(_configuration["ResourcePreviewsPath"]!, resourceEntity.Type.Name), filename).ToArray();
            if (previewFiles != null && previewFiles.Length > 0)
            {
                //if (previewFiles.Length > 1) await Console.Out.WriteLineAsync("More than one resource preview found.");
                var previewFilename = new FileInfo(resource.Filename);
                var previewDestination = Path.Combine(_configuration["ResourcePreviewsPath"]!, resource.Type.Name, previewFilename.Name.Replace(previewFilename.Extension, previewFiles[0].Extension));
                _io.MoveFile(previewFiles[0].FullName, previewDestination);
            }
            await _db.UpdateResource(resource);
        }

        public async Task DeleteResource(Resource resource, bool deleteFiles, string directory, string filename)
        {
            if (deleteFiles)
            {
                var subtypeDir = Path.Combine(_configuration["OutputDir"]!, "Saved", resource.Type.Name);
                var modelImagesDir = _io.GetFolderByName(subtypeDir, resource.CivitaiModelId.ToString()!);
                if (modelImagesDir != null)
                {
                    var versionImagesDir = _io.GetFolderByName(modelImagesDir.FullName, resource.CivitaiModelVersionId.ToString()!);
                    if (versionImagesDir != null) _io.DeleteFolder(versionImagesDir, true);
                    var versionImage = _io.GetFileByName(modelImagesDir.FullName, resource.CivitaiModelVersionId.ToString()!);
                    if (versionImage != null) _io.DeleteFile(versionImage);
                }

                var files = _io.GetFilesByName(directory, filename);
                foreach (var localFile in files)
                {
                    _io.DeleteFile(localFile.FullName);
                }
            }
            await _db.DeleteResource(resource.Id);
            if (resource.CivitaiModelVersionId != null)
                await _db.DeleteResourceImage((int)resource.CivitaiModelVersionId);
            _events.Publish(new ResourcesChangedEventArgs("Delete"));
        }

        public async Task ToggleResource(LocalResource resource, LocalResourceFile file)
        {
            string destPath = string.Empty;
            if (file.IsEnabled) destPath = Path.Combine(_configuration["ResourcesPath"]!, "_storage", resource.Type.Name);
            else destPath = _resourceTypeDirectories.FirstOrDefault(p => p.Key.Equals(resource.Type.Name, StringComparison.InvariantCultureIgnoreCase)).Value;
            if (resource.SubType != null) destPath = Path.Combine(destPath, resource.SubType.Name);
            destPath = Path.Combine(destPath, file.Filename);
            _io.MoveFile(file.File.FullName, destPath);
            await _db.ToggleResourceState(file.ResourceId);
            _events.Publish(new ResourcesChangedEventArgs("Toggle"));
        }

        /// <summary>
        /// Scans known resource directories (enabled and _storage) for files not tracked in the database.
        /// </summary>
        /// <returns>List of untracked resource files with detected type and active/inactive state.</returns>
        public async Task<List<ImportResourceModel>> GetUntrackedResources()
        {
            var untrackedResources = new List<ImportResourceModel>();
            var validExtensions = new[] { ".safetensors", ".ckpt", ".pt" };
            var resourceTypes = await _db.GetResourceTypes(ordered: false);

            foreach (var typeDir in _resourceTypeDirectories)
            {
                var typeName = typeDir.Key;
                var enabledPath = typeDir.Value;
                var storagePath = Path.Combine(_configuration["ResourcesPath"]!, "_storage", typeName);

                var resourceType = resourceTypes.FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.InvariantCultureIgnoreCase));
                if (resourceType == null) continue;

                // Scan enabled (active) directory
                if (Directory.Exists(enabledPath))
                {
                    var files = _io.GetFilesRecursive(enabledPath, extensionsWhitelist: validExtensions.ToList());
                    foreach (var file in files)
                    {
                        var exists = await _db.CheckResourceExistsByFilename(file.Name);
                        if (!exists)
                        {
                            untrackedResources.Add(new ImportResourceModel(file, resourceType, isEnabled: true));
                        }
                    }
                }

                // Scan storage (inactive) directory
                if (Directory.Exists(storagePath))
                {
                    var files = _io.GetFilesRecursive(storagePath, extensionsWhitelist: validExtensions.ToList());
                    foreach (var file in files)
                    {
                        var exists = await _db.CheckResourceExistsByFilename(file.Name);
                        if (!exists)
                        {
                            untrackedResources.Add(new ImportResourceModel(file, resourceType, isEnabled: false));
                        }
                    }
                }
            }

            return untrackedResources.OrderBy(r => r.Type.Name).ThenBy(r => r.Filename).ToList();
        }

        /// <summary>
        /// Imports untracked resource files into the database with shared metadata defaults.
        /// </summary>
        /// <param name="filesToImport">List of untracked files to import.</param>
        /// <param name="sharedTitle">Shared title for all imports (optional - defaults to filename without extension).</param>
        /// <param name="sharedSubType">Shared sub-type for all imports (optional).</param>
        /// <param name="sharedBaseModel">Shared base model for all imports (optional).</param>
        /// <param name="sharedTriggerWords">Shared trigger words for all imports (optional).</param>
        /// <param name="coverImagePath">Path to cover image to copy for all imports (optional).</param>
        /// <returns>Number of successfully imported resources.</returns>
        public async Task<int> ImportResources(
            List<ImportResourceModel> filesToImport,
            string? sharedTitle = null,
            string? sharedSubType = null,
            string? sharedBaseModel = null,
            List<string>? sharedTriggerWords = null)
        {
            int importedCount = 0;

            foreach (var file in filesToImport)
            {
                try
                {
                    // Create Resource entity
                    var resource = new Resource
                    {
                        Title = !string.IsNullOrWhiteSpace(sharedTitle)
                            ? sharedTitle
                            : Path.GetFileNameWithoutExtension(file.Filename),
                        Filename = file.Filename,
                        Type = file.Type,
                        BaseModel = sharedBaseModel,
                        TriggerWords = sharedTriggerWords,
                        IsEnabled = file.IsEnabled,
                        SizeKb = file.SizeKb
                    };

                    // Add SubType if provided
                    if (!string.IsNullOrWhiteSpace(sharedSubType))
                    {
                        resource.SubType = new ResourceSubType { Name = sharedSubType };
                    }

                    // Copy cover image if provided for this specific file
                    if (!string.IsNullOrWhiteSpace(file.CoverImagePath) && File.Exists(file.CoverImagePath))
                    {
                        var previewDir = Path.Combine(_configuration["ResourcePreviewsPath"]!, file.Type.Name);
                        if (!Directory.Exists(previewDir))
                        {
                            Directory.CreateDirectory(previewDir);
                        }

                        var previewFileName = Path.GetFileNameWithoutExtension(file.Filename) + Path.GetExtension(file.CoverImagePath);
                        var previewPath = Path.Combine(previewDir, previewFileName);

                        File.Copy(file.CoverImagePath, previewPath, overwrite: true);
                    }

                    // Save to database
                    var success = await _db.CreateResource(resource);
                    if (success)
                    {
                        importedCount++;
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue with other imports
                    Console.WriteLine($"Failed to import {file.Filename}: {ex.Message}");
                }
            }

            // Publish event if any resources were imported
            if (importedCount > 0)
            {
                _events.Publish(new ResourcesChangedEventArgs($"Imported {importedCount} resource(s)"));
            }

            return importedCount;
        }
    }
}
