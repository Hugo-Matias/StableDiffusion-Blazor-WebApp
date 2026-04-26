using BlazorWebApp.Data;
using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IComfyUIService _capi;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseService> _logger;
        private readonly OllamaService _ollamaService;
        private readonly IIOService _io;

        public int PageSize { get; set; }

        public DatabaseService(IDbContextFactory<AppDbContext> factory, IComfyUIService capi, IConfiguration configuration, ILogger<DatabaseService> logger, OllamaService ollamaService, IIOService io)
        {
            _factory = factory;
            _capi = capi;
            _configuration = configuration;
            _logger = logger;
            _ollamaService = ollamaService;
            _io = io;
            PageSize = 5;

            InitializeDatabase();
            PopulateModes();
            PopulateSamplers();
            SeedDefaultSystemPromptTemplates();
            SeedResourceTypes();
        }

        public async Task InitializeDatabase()
        {
            using var context = await _factory.CreateDbContextAsync();
            await context.Database.EnsureCreatedAsync();
            await context.Database.MigrateAsync();
        }

        public async Task<List<Folder>> GetFolders()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Folders
                .OrderBy(f => f.SortOrder)
                .ToListAsync();
        }

        public async Task<List<Folder>> GetFolders(string name)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (string.IsNullOrWhiteSpace(name)) return await context.Folders.ToListAsync();
            else return await context.Folders.Where(f => f.Name.Contains(name, StringComparison.InvariantCultureIgnoreCase)).ToListAsync();
        }

        public async Task<Folder> GetFolder(string name)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Folders.FirstOrDefaultAsync(f => f.Name.Equals(name));
        }

        public async Task<Folder> GetFolder(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Folders.FirstOrDefaultAsync(f => f.Id.Equals(id));
        }

        public async Task<List<Project>> GetProjects(int folderId = 0)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (folderId <= 0) return await context.Projects.OrderByDescending(p => p.CreationTime).ToListAsync();
            else return await context.Projects.Where(p => p.FolderId == folderId).OrderByDescending(p => p.CreationTime).ToListAsync();
        }

        public async Task<Project> GetProject(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Projects.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Project> GetProject(string name)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Projects.FirstOrDefaultAsync(p => p.Name == name);
        }

        public async Task<Project> GetLatestProject()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Projects.OrderBy(p => p.Id).LastOrDefaultAsync();
        }

        public async Task<List<Project>?> GetLastUsedProjects(int amount, int[] ignoreIds)
        {
            using var context = await _factory.CreateDbContextAsync();
            var usedProjects = await context.Images.Select(i => i.ProjectId).Distinct().ToListAsync();
            if (usedProjects.Count < amount) return null;
            var ids = new List<int>();
            foreach (var id in context.Images.OrderByDescending(i => i.Id).Select(i => i.ProjectId))
            {
                if (!ids.Contains(id) && !ignoreIds.Contains(id)) ids.Add(id);
                if (ids.Count >= amount) break;
            }
            var projects = new List<Project>();
            foreach (var id in ids)
            {
                projects.Add(await GetProject(id));
            }
            return projects;
        }

        public async Task<Folder> CreateFolder(Folder folder)
        {
            using var context = await _factory.CreateDbContextAsync();
            var folderEntity = await context.Folders.AddAsync(folder);
            await context.SaveChangesAsync();
            return folderEntity.Entity;
        }

        public async Task<Project> CreateProject(Project project)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (project.Folder != null && !string.IsNullOrWhiteSpace(project.Folder.Name))
            {
                var folder = await GetFolder(project.Folder.Name);
                if (folder != null)
                {
                    project.FolderId = folder.Id;
                    project.Folder = null;
                }
            }
            var result = await context.Projects.AddAsync(project);
            await context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<Project> UpdateProject(int projectId, Project data)
        {
            using var context = await _factory.CreateDbContextAsync();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (!string.IsNullOrWhiteSpace(data.Name))
                project.Name = data.Name;
            if (!string.IsNullOrWhiteSpace(data.SampleImagePath))
                project.SampleImagePath = data.SampleImagePath;
            if (data.Folder != null && !string.IsNullOrWhiteSpace(data.Folder.Name))
            {
                var folder = await GetFolder(data.Folder.Name);
                if (folder != null) project.FolderId = folder.Id;
                else project.Folder = data.Folder;
            }
            else if (data.Folder == null && project.FolderId > 0) project.FolderId = null;
            await context.SaveChangesAsync();
            return project;
        }

        public async Task<Project> UpdateProject(Project project)
        {
            using var context = await _factory.CreateDbContextAsync();
            var entity = context.Projects.Update(project);
            await context.SaveChangesAsync();
            return entity.Entity;
        }

        public async Task DeleteFolder(int folderId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var folder = await context.Folders.FirstOrDefaultAsync(f => f.Id == folderId);
            if (context.Projects.Any(p => p.FolderId == folderId))
            {
                var projects = context.Projects.Where(p => p.FolderId == folderId);
                foreach (var project in projects)
                {
                    project.FolderId = null;
                }
            }
            context.Folders.Remove(folder);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Deletes Project from the database
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns>First Project on the projects table, if any.</returns>
        public async Task<Project?> DeleteProject(int projectId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            context.Projects.Remove(project);
            await context.SaveChangesAsync();
            return await context.Projects.FirstOrDefaultAsync();
        }

        public async Task RenameProject(int projectId, string name)
        {
            using var context = await _factory.CreateDbContextAsync();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            project.Name = name;
            await context.SaveChangesAsync();
        }

        public async Task SetProjectCover(int projectId, string imagePath)
        {
            using var context = await _factory.CreateDbContextAsync();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            project.SampleImagePath = imagePath;
            await context.SaveChangesAsync();
        }

        public async Task SetProjectFolder(int projectId, int folderId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            project.FolderId = folderId;
            await context.SaveChangesAsync();
        }

        public async Task<List<Selection>> GetSelections()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Selections.Include(s => s.Images).ToListAsync();
        }

        public async Task<List<Selection>> GetSelectionsByName(string name)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Selections.Where(s => s.Name.ToLower() == name.ToLower()).ToListAsync();
        }

        public async Task<Selection> GetSelection(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Selections.Include(s => s.Images).FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task CreateSelection(Selection selection)
        {
            using var context = await _factory.CreateDbContextAsync();
            var images = await context.Images.Where(i => selection.Images.Select(i => i.Id).Contains(i.Id)).ToListAsync();
            selection.Images = images;
            await context.Selections.AddAsync(selection);
            await context.SaveChangesAsync();
        }

        public async Task UpdateSelection(int selectionId, List<int> imageIds)
        {
            using var context = await _factory.CreateDbContextAsync();
            var entity = await context.Selections.Include(s => s.Images).FirstOrDefaultAsync(s => s.Id == selectionId);
            var images = await context.Images.Where(i => imageIds.Contains(i.Id)).ToListAsync();
            entity.Images.Clear();
            foreach (var image in images)
            {
                entity.Images.Add(image);
            }
            await context.SaveChangesAsync();
        }

        public async Task<Selection?> DeleteSelection(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var selection = await context.Selections.FirstOrDefaultAsync(s => s.Id == id);
            context.Selections.Remove(selection);
            await context.SaveChangesAsync();
            return await context.Selections.FirstOrDefaultAsync();
        }

        public async Task<Image> AddImage(Image image)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.Images != null)
            {
                if (image.Model != null)
                {
                    var resource = await context.Resources.FirstOrDefaultAsync(r => r.Id == image.Model.Id);

                    if (resource != null)
                    {
                        image.ResourceId = resource.Id;
                        image.Model = resource;
                    }
                }
                var result = await context.Images.AddAsync(image);
                if (result != null) { await context.SaveChangesAsync(); }
            }
            return await context.Images.FirstOrDefaultAsync(i => i.Path == image.Path);
        }

        public async Task<List<Image>> GetImages(List<int> imageIds)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Images.Include(i => i.Model).Where(i => imageIds.Contains(i.Id)).ToListAsync();
        }

        public async Task<Image?> GetImageById(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Images.Include(i => i.Model).FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<ImagesDto> GetPagedImages(int page)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.Images == null) return null;
            var pageCount = Math.Ceiling(context.Images.Count() / (float)PageSize);
            var images = await context.Images
                .Include(i => i.Model)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
            return new ImagesDto
            {
                Images = images,
                CurrentPage = page,
                PageCount = (int)pageCount,
                HasNext = (int)pageCount > 1 && page < (int)pageCount,
                HasPrev = page > 1
            };
        }

        public async Task<ImagesDto> GetPagedImages(int page, int projectId)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.Images == null) return null;
            var pageCount = Math.Ceiling(context.Images.Count(i => i.ProjectId == projectId) / (float)PageSize);
            var images = await context.Images
                .Include(i => i.Model)
                .Where(i => i.ProjectId == projectId)
                .OrderByDescending(i => i.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
            return new ImagesDto
            {
                Images = images,
                CurrentPage = page,
                PageCount = (int)pageCount,
                HasNext = (int)pageCount > 1 && page < (int)pageCount,
                HasPrev = page > 1
            };
        }

        public async Task<ImagesDto> GetPagedImages(int page, List<int> imageIds)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.Images == null) return null;
            var pageCount = Math.Ceiling(context.Images.Count(i => imageIds.Contains(i.Id)) / (float)PageSize);
            var images = await context.Images
                .Include(i => i.Model)
                .Where(i => imageIds.Contains(i.Id))
                .OrderByDescending(i => i.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
            return new ImagesDto
            {
                Images = images,
                CurrentPage = page,
                PageCount = (int)pageCount,
                HasNext = (int)pageCount > 1 && page < (int)pageCount,
                HasPrev = page > 1
            };
        }

        public async Task<ImagesDto> GetSortedImages(int page, int projectId, AppStateGallery state)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.Images == null) return null;

            var query = context.Images.Where(i => i.ProjectId == projectId);
            if (!string.IsNullOrWhiteSpace(state.Prompt))
                query = query.Where(i => i.Prompt.ToLower().Contains(state.Prompt.ToLower()));
            if (!string.IsNullOrWhiteSpace(state.NegativePrompt))
                query = query.Where(i => i.NegativePrompt.ToLower().Contains(state.NegativePrompt.ToLower()));
            if (state.IsFavoritesOnly) query = query.Where(i => i.Favorite);
            if (state.FilterByDateRange) query = query.Where(i => i.DateCreated >= state.DateRange.Start && i.DateCreated <= state.DateRange.End.Value.AddDays(1));

            List<int> modes = new();
            if (state.IsModeTxt2Img) modes.Add(1);
            if (state.IsModeImg2Img) modes.Add(2);
            if (state.IsModeUpscale) modes.Add(3);
            if (state.IsModeImg2Vid) modes.Add(4);

            // Only filter by mode if at least one mode is selected, otherwise show all
            if (modes.Count > 0)
                query = query.Where(i => modes.Contains(i.ModeId));

            if (state.IsScore)
                query = query.Where(i => i.Score == state.Score);

            switch (state.OrderBy)
            {
                case GalleryOrderBy.Date:
                    query = state.OrderDescending ? query.OrderByDescending(i => i.DateCreated) : query.OrderBy(i => i.DateCreated);
                    break;
                case GalleryOrderBy.Sampler:
                    query = state.OrderDescending ? query.OrderByDescending(i => i.SamplerId) : query.OrderBy(i => i.SamplerId);
                    break;
                case GalleryOrderBy.Seed:
                    query = state.OrderDescending ? query.OrderByDescending(i => i.Seed) : query.OrderBy(i => i.Seed);
                    break;
                case GalleryOrderBy.Steps:
                    query = state.OrderDescending ? query.OrderByDescending(i => i.Steps) : query.OrderBy(i => i.Steps);
                    break;
                case GalleryOrderBy.CfgScale:
                    query = state.OrderDescending ? query.OrderByDescending(i => i.CfgScale) : query.OrderBy(i => i.CfgScale);
                    break;
                case GalleryOrderBy.Width:
                    query = state.OrderDescending ? query.OrderByDescending(i => i.Width) : query.OrderBy(i => i.Width);
                    break;
                case GalleryOrderBy.Height:
                    query = state.OrderDescending ? query.OrderByDescending(i => i.Height) : query.OrderBy(i => i.Height);
                    break;
                case GalleryOrderBy.Favorite:
                    query = state.OrderDescending ? query.OrderByDescending(i => i.Favorite) : query.OrderBy(i => i.Favorite);
                    break;
                case GalleryOrderBy.Mode:
                    query = state.OrderDescending ? query.OrderByDescending(i => i.ModeId) : query.OrderBy(i => i.ModeId);
                    break;
                case GalleryOrderBy.Denoising:
                    query = state.OrderDescending ? query.OrderByDescending(i => i.DenoisingStrength) : query.OrderBy(i => i.DenoisingStrength);
                    break;
                case GalleryOrderBy.Random:
                    // For consistent random ordering across pagination, use a deterministic approach
                    // SQLite can handle: ORDER BY (Id * seed) % prime
                    // This produces consistent ordering for the same seed
                    var seed = state.RandomSeed ?? 1;
                    // Use modulo with the seed - simple arithmetic that SQLite can translate
                    // The multiplication spreads IDs, modulo wraps them, creating pseudo-random order
                    query = query.OrderBy(i => (i.Id * seed) % 1000003);
                    break;
            }

            var totalCount = await query.CountAsync();
            var pageCount = Math.Ceiling(totalCount / (float)PageSize);

            var images = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Include(i => i.Model)
                .AsNoTracking()
                .ToListAsync();

            return new ImagesDto
            {
                Images = images,
                CurrentPage = page,
                PageCount = (int)pageCount,
                HasNext = (int)pageCount > 1 && page < (int)pageCount,
                HasPrev = page > 1
            };
        }

        public async Task<string> GetSampleImage(int projectId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project != null) return project.SampleImagePath;
            else return string.Empty;
        }

        public async Task<ImagesDto> GetRandomImages(int amount)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.Images == null) return null;
            var images = await context.Images.Include(i => i.Model).OrderBy(i => EF.Functions.Random()).Take(amount).ToListAsync();
            return new ImagesDto
            {
                Images = images,
                CurrentPage = 1,
                PageCount = 1,
                HasNext = false,
                HasPrev = false
            };
        }

        public async Task<Image> GetRandomFavorite(int projectId)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.Images == null) return null;
            return await context.Images.Include(i => i.Model).Where(i => i.ProjectId == projectId && i.Favorite).OrderBy(o => EF.Functions.Random()).FirstOrDefaultAsync();
        }

        public async Task<List<Image>> GetRecentImagesWithPrompts(int limit = 10000)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Images
                .Where(i => !string.IsNullOrEmpty(i.Prompt))
                .OrderByDescending(i => i.DateCreated)
                .Take(limit)
                .Select(i => new Image
                {
                    Id = i.Id,
                    Prompt = i.Prompt,
                    NegativePrompt = i.NegativePrompt
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Image> UpdateImage(Image image)
        {
            using var context = await _factory.CreateDbContextAsync();
            var imageEntity = context.Images.Include(i => i.Model).FirstOrDefault(i => i.Id == image.Id);

            if (imageEntity != null)
            {
                context.Entry(imageEntity).CurrentValues.SetValues(image);

                if (image.Model != null)
                {
                    var resource = await context.Resources.FirstOrDefaultAsync(r => r.Id == image.Model.Id);
                    if (resource != null)
                    {
                        imageEntity.ResourceId = resource.Id;
                        imageEntity.Model = resource;
                    }
                }
            }
            else
            {
                imageEntity.ResourceId = null;
                imageEntity.Model = null;
            }

            await context.SaveChangesAsync();
            return imageEntity;
        }

        public async Task UpdateImages(List<Image> images)
        {
            using var context = await _factory.CreateDbContextAsync();
            foreach (var image in images)
            {
                var imageEntity = await context.Images.Include(i => i.Model).FirstOrDefaultAsync(i => i.Id == image.Id);
                if (imageEntity != null)
                {
                    context.Entry(imageEntity).CurrentValues.SetValues(image);
                    if (image.Model != null)
                    {
                        var resource = await context.Resources.FirstOrDefaultAsync(r => r.Id == image.Model.Id);
                        if (resource != null)
                        {
                            imageEntity.ResourceId = resource.Id;
                            imageEntity.Model = resource;
                        }
                    }
                }
                else
                {
                    imageEntity.ResourceId = null;
                    imageEntity.Model = null;
                }
            }
            await context.SaveChangesAsync();
        }

        public async Task<Image> DeleteImage(Image image)
        {
            using var context = await _factory.CreateDbContextAsync();
            var response = context.Remove(image);
            await context.SaveChangesAsync();

            // Delete the physical file after the entity is removed so a DB failure does
            // not leave the record orphaned while the file is already gone.
            if (!string.IsNullOrWhiteSpace(image.Path))
            {
                try { _io.DeleteFile(image.Path); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "DeleteImage: entity {Id} removed from DB but file '{Path}' could not be deleted.",
                        image.Id, image.Path);
                }
            }

            return response.Entity;
        }

        public async Task<string> GetSampler(int id)
        {
            if (id == 0) return string.Empty;
            using var context = await _factory.CreateDbContextAsync();
            var sampler = await context.Samplers.FirstOrDefaultAsync(s => s.Id == id);
            return sampler != null ? sampler.Name : string.Empty;
        }

        public async Task<int> GetSamplerIdByName(string samplerName)
        {
            if (string.IsNullOrWhiteSpace(samplerName)) return 0;
            using var context = await _factory.CreateDbContextAsync();
            var sampler = await context.Samplers.FirstOrDefaultAsync(s => s.Name.ToLower() == samplerName.ToLower());
            return sampler != null ? sampler.Id : -1;
        }

        private async Task PopulateSamplers()
        {
            var samplerNames = new List<string>();
            try
            {
                samplerNames = await _capi.GetNodeInputOptionsAsync("KSampler", "sampler_name");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not retrieve samplers from ComfyUI backend.");
            }

            if (samplerNames.Count == 0)
            {
                _logger.LogWarning("No samplers retrieved, backend may not be available.");
                return;
            }

            using var context = await _factory.CreateDbContextAsync();
            foreach (var name in samplerNames)
            {
                var currentSampler = context.Samplers.SingleOrDefault(s => s.Name.ToLower() == name.ToLower());
                if (currentSampler == null)
                    await context.Samplers.AddAsync(new Data.Entities.Sampler { Name = name });
            }
            await context.SaveChangesAsync();
        }

        public async Task<int> GetMode(ModeType mode)
        {
            using var context = await _factory.CreateDbContextAsync();
            var modeEntity = await context.Modes.FirstOrDefaultAsync(m => m.Type == mode);
            return modeEntity.Id;
        }

        public async Task<ModeType> GetMode(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var modeEntity = await context.Modes.FirstOrDefaultAsync(m => m.Id == id);
            return modeEntity.Type;
        }

        private async void PopulateModes()
        {
            using var context = await _factory.CreateDbContextAsync();
            foreach (var mode in (ModeType[])Enum.GetValues(typeof(ModeType)))
            {
                var record = await context.Modes.FirstOrDefaultAsync(o => o.Type == mode);
                if (record == null)
                    await context.Modes.AddAsync(new Mode { Type = mode });
            }
            await context.SaveChangesAsync();
        }

        private async void SeedResourceTypes()
        {
            var requiredTypes = new[] { "Checkpoint", "Diffusion", "TextualInversion", "Hypernetwork", "LORA", "LoCon", "VAE" };
            using var context = await _factory.CreateDbContextAsync();
            foreach (var typeName in requiredTypes)
            {
                var exists = await context.ResourceTypes.AnyAsync(t => t.Name == typeName);
                if (!exists)
                    await context.ResourceTypes.AddAsync(new ResourceType { Name = typeName });
            }
            await context.SaveChangesAsync();
        }

        public async Task<List<Prompt>> GetPrompts(string positive = "", string negative = "")
        {
            using var context = await _factory.CreateDbContextAsync();
            var query = context.Prompts.AsQueryable();
            if (!string.IsNullOrWhiteSpace(positive)) query = query.Where(p => p.Positive.ToLower().Contains(positive.ToLower()));
            if (!string.IsNullOrWhiteSpace(negative)) query = query.Where(p => p.Negative.ToLower().Contains(negative.ToLower()));
            return query.OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Title).ToList();
        }

        public async Task<List<Prompt>> GetPromptsByCategory(string? category)
        {
            using var context = await _factory.CreateDbContextAsync();
            var query = context.Prompts.AsQueryable();
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category);
            return await query.OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Title).ToListAsync();
        }

        public async Task<List<Prompt>> GetPromptsByTags(List<string> tags)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (tags == null || !tags.Any())
                return await context.Prompts.OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Title).ToListAsync();

            var query = context.Prompts.AsQueryable();
            foreach (var tag in tags)
            {
                var tagLower = tag.ToLower();
                query = query.Where(p => p.Tags != null && p.Tags.Any(t => t.ToLower().Contains(tagLower)));
            }
            return await query.OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Title).ToListAsync();
        }

        public async Task<List<Prompt>> GetPinnedPrompts()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Prompts
                .Where(p => p.IsPinned)
                .OrderBy(p => p.SortOrder)
                .ToListAsync();
        }

        public async Task<List<Prompt>> GetFavoritePrompts()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Prompts
                .Where(p => p.IsFavorite)
                .OrderBy(p => p.Title)
                .ToListAsync();
        }

        public async Task<List<string>> GetAllPromptCategories()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Prompts
                .Where(p => !string.IsNullOrWhiteSpace(p.Category))
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<List<string>> GetAllPromptTags()
        {
            using var context = await _factory.CreateDbContextAsync();
            var allPrompts = await context.Prompts
                .Where(p => p.Tags != null && p.Tags.Any())
                .Select(p => p.Tags!)
                .ToListAsync();

            return allPrompts
                .SelectMany(tags => tags)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        public async Task UpdatePromptUsage(int promptId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var prompt = await context.Prompts.FirstOrDefaultAsync(p => p.Id == promptId);
            if (prompt != null)
            {
                prompt.UsageCount++;
                prompt.LastUsedAt = DateTime.Now;
                await context.SaveChangesAsync();
            }
        }

        public async Task<List<Prompt>> SearchPromptsWithKeywords(string query)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (string.IsNullOrWhiteSpace(query))
                return await context.Prompts.OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Title).ToListAsync();

            // Load all prompts into memory first to avoid EF Core translation issues with complex LINQ
            var allPrompts = await context.Prompts.ToListAsync();

            // Split query into keywords
            var keywords = query.ToLower().Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            // Filter in memory where we can use complex LINQ operations
            var results = allPrompts
                .Where(p => keywords.Any(k =>
                    (p.Title != null && p.Title.ToLower().Contains(k)) ||
                    (p.Positive != null && p.Positive.ToLower().Contains(k)) ||
                    (p.Negative != null && p.Negative.ToLower().Contains(k)) ||
                    (p.Category != null && p.Category.ToLower().Contains(k)) ||
                    (p.Tags != null && p.Tags.Any(t => t.ToLower().Contains(k)))))
                .OrderByDescending(p => p.IsFavorite)
                .ThenBy(p => p.Title)
                .ToList();

            return results;
        }

        public async Task CreatePrompt(Prompt prompt)
        {
            using var context = await _factory.CreateDbContextAsync();
            context.Prompts.Add(prompt);
            await context.SaveChangesAsync();
        }

        public async Task UpdatePrompt(PromptResource prompt)
        {
            using var context = await _factory.CreateDbContextAsync();
            var entity = await context.Prompts.FirstOrDefaultAsync(p => p.Id == prompt.Id);
            if (entity == null) return;

            entity.Title = prompt.Title;
            entity.Positive = prompt.Positive;
            entity.Negative = prompt.Negative;
            entity.IsFavorite = prompt.IsFavorite;
            entity.Loras = prompt.Loras;
            entity.Category = prompt.Category;
            entity.Tags = prompt.Tags;
            entity.IsPinned = prompt.IsPinned;
            entity.SortOrder = prompt.SortOrder;
            await context.SaveChangesAsync();
        }

        public async Task DeletePrompt(int id)
        {
            using var context = _factory.CreateDbContext();
            var entity = context.Prompts.FirstOrDefault(p => p.Id == id);
            if (entity != null) context.Prompts.Remove(entity);
            await context.SaveChangesAsync();
        }

        public async Task<List<Resource>> GetResources()
        {
            using var context = _factory.CreateDbContext();
            return await context.Resources.Include(r => r.Type).Include(r => r.SubType).ToListAsync();
        }

        public async Task<List<Resource>> GetResources(int typeId)
        {
            using var context = _factory.CreateDbContext();
            return await context.Resources.Where(r => r.Type.Id == typeId).Include(r => r.Type).Include(r => r.SubType).ToListAsync();
        }

        public async Task<List<Resource>> GetResources(int? typeId = null, string? baseModel = null)
        {
            using var context = _factory.CreateDbContext();
            var query = context.Resources.Include(r => r.Type).Include(r => r.SubType).AsQueryable();

            if (typeId.HasValue)
            {
                query = query.Where(r => r.Type.Id == typeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(baseModel))
            {
                query = query.Where(r => r.BaseModel == baseModel);
            }

            return await query.ToListAsync();
        }

        public async Task<Resource> GetResourceById(int id)
        {
            using var context = _factory.CreateDbContext();
            return await context.Resources.Include(r => r.Type).Include(r => r.SubType).FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<List<string>> GetDistinctBaseModels()
        {
            using var context = _factory.CreateDbContext();
            return await context.Resources
                .Where(r => r.BaseModel != null)
                .Select(r => r.BaseModel)
                .Distinct()
                .OrderBy(b => b)
                .ToListAsync();
        }

        public async Task<List<string>> GetDistinctBaseModels(int typeId)
        {
            using var context = _factory.CreateDbContext();
            return await context.Resources
                .Where(r => r.BaseModel != null && r.Type.Id == typeId)
                .Select(r => r.BaseModel)
                .Distinct()
                .OrderBy(b => b)
                .ToListAsync();
        }

        public async Task<Resource> GetResourceByCivitaiModelId(int civitaiId)
        {
            using var context = _factory.CreateDbContext();
            return await context.Resources.Include(r => r.Type).Include(r => r.SubType).FirstOrDefaultAsync(r => r.CivitaiModelId == civitaiId);
        }

        public async Task<Resource> GetResourceByFilename(string filename)
        {
            using var context = _factory.CreateDbContext();
            filename = Path.GetFileName(filename);
            return await context.Resources.Include(r => r.Type).Include(r => r.SubType).FirstOrDefaultAsync(r => r.Filename == filename || r.Filename.Contains(filename));
        }

        public async Task<List<ResourceType>> GetResourceTypes(bool ordered)
        {
            using var context = _factory.CreateDbContext();
            if (ordered) return await context.ResourceTypes.OrderBy(t => t.Name).ToListAsync();
            return await context.ResourceTypes.ToListAsync();
        }

        public async Task<IEnumerable<ResourceSubType>> GetResourceSubTypes(bool ordered)
        {
            using var context = _factory.CreateDbContext();
            if (ordered) return await context.ResourceSubTypes.OrderBy(t => t.Name).ToListAsync();
            return await context.ResourceSubTypes.ToArrayAsync();
        }

        public async Task<IEnumerable<ResourceSubType>> GetResourceSubTypes(string name, bool ordered)
        {
            using var context = _factory.CreateDbContext();
            var query = context.ResourceSubTypes.AsQueryable();
            if (!string.IsNullOrEmpty(name)) query = query.Where(t => t.Name.ToLower().Contains(name.ToLower()));
            if (ordered) query = query.OrderBy(t => t.Name);
            return await query.ToArrayAsync();
        }
        public async Task<IEnumerable<string>> GetResourceSubtypeNames(string name)
        {
            IEnumerable<ResourceSubType> types = string.IsNullOrWhiteSpace(name) ? await GetResourceSubTypes(ordered: true) : await GetResourceSubTypes(name, ordered: true);
            return types.Select(t => t.Name).ToArray();
        }

        /// <summary>
        /// Creates a new database record for a Resource entity.
        /// </summary>
        /// <param name="resource"></param>
        /// <returns>True, in case of successfully adding the new Resource.<br/>False, if the resource's Filename is already registered in the database.</returns>
        public async Task<bool> CreateResource(Resource resource)
        {
            using var context = _factory.CreateDbContext();
            var exists = await context.Resources.AnyAsync(r => r.Filename == resource.Filename);
            if (exists) return false;
            var type = await context.ResourceTypes.FirstOrDefaultAsync(t => t.Name.Equals(resource.Type.Name));
            if (type != null) resource.Type = type;
            if (resource.SubType != null)
            {
                var subtype = await context.ResourceSubTypes.FirstOrDefaultAsync(t => t.Name.Equals(resource.SubType.Name));
                if (subtype != null) resource.SubType = subtype;
            }
            context.Resources.Add(resource);
            await context.SaveChangesAsync();
            return true;
        }
        public async Task<Resource> UpdateResource(Resource resource)
        {
            using var context = await _factory.CreateDbContextAsync();
            var response = context.Update(resource);
            await context.SaveChangesAsync();
            return response.Entity;
        }

        public async Task UpdateResourceLoadedDate(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var resource = await context.Resources.FirstOrDefaultAsync(r => r.Id == id);
            resource.LastLoadedDate = DateTime.Now;
            await context.SaveChangesAsync();
        }

        public async Task DeleteResource(int resourceId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var resource = await context.Resources.FirstOrDefaultAsync(r => r.Id == resourceId);
            if (resource != null) context.Resources.Remove(resource);
            await context.SaveChangesAsync();
        }

        public async Task ToggleResourceState(int resourceId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var resource = await context.Resources.FirstOrDefaultAsync(r => r.Id == resourceId);
            if (resource != null) resource.IsEnabled = !resource.IsEnabled;
            await context.SaveChangesAsync();
        }

        public async Task<bool> CheckResourceExistsByFilename(string filename)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Resources.AnyAsync(r => r.Filename == filename);
        }

        public async Task<bool> CheckResourceExistsByModelId(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Resources.AnyAsync(r => r.CivitaiModelId == id);
        }

        public async Task<bool> CheckResourceExistsByModelVersionId(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Resources.AnyAsync(r => r.CivitaiModelVersionId == id);
        }

        public async Task<bool> CreateResourceImage(ResourceImage image)
        {
            using var context = await _factory.CreateDbContextAsync();
            var exists = await context.ResourceImages.AnyAsync(i => i.Hash == image.Hash);
            if (exists) return false;
            context.ResourceImages.Add(image);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ResourceImage>> GetResourceImages(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            return context.ResourceImages.Where(i => i.CivitaiModelVersionID == id).ToList();
        }

        public async Task<ImagesDto> GetRandomResourceImages(int amount)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.ResourceImages == null) return null;
            var resourceImages = await context.ResourceImages.OrderBy(i => EF.Functions.Random()).Take(amount).ToListAsync();
            var images = new List<Image>();
            foreach (var resourceImage in resourceImages)
            {
                var image = new Image(resourceImage)
                {
                    SamplerId = await GetSamplerIdByName(resourceImage.Sampler)
                };
                images.Add(image);
            }
            return new ImagesDto
            {
                Images = images,
                CurrentPage = 1,
                PageCount = 1,
                HasNext = false,
                HasPrev = false
            };
        }

        public async Task<int> ResourceImageByModelVersionIdCount(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.ResourceImages.CountAsync(i => i.CivitaiModelVersionID == id);
        }

        public async Task DeleteResourceImage(int civitaiModelVersionId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var images = context.ResourceImages.Where(i => i.CivitaiModelVersionID == civitaiModelVersionId);
            foreach (var image in images)
            {
                context.ResourceImages.Remove(image);
            }
            await context.SaveChangesAsync();
        }

        public async Task<State> CreateState(State state)
        {
            using var context = await _factory.CreateDbContextAsync();
            var entity = context.States.Add(state);
            await context.SaveChangesAsync();
            return entity.Entity;
        }

        public async Task<List<State>> GetStates(int stateVersion)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.States.Where(s => s.Version == stateVersion).ToListAsync();
        }

        public async Task<State?> GetState(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var stateVersion = int.Parse(_configuration["StateVersion"]);

            var state = await context.States
                .Where(s => s.Version == stateVersion)
                .FirstOrDefaultAsync(s => s.Id == id);

            // Fallback: If ID 1 requested (AutoSave) and not found, get latest AutoSave for current version
            if (state == null && id == 1)
            {
                state = await context.States
                    .Where(s => s.Version == stateVersion && s.Title == "AutoSave")
                    .OrderByDescending(s => s.CreationDate)
                    .FirstOrDefaultAsync();
            }

            return state;
        }

        public async Task<State> UpdateState(State state)
        {
            using var context = await _factory.CreateDbContextAsync();
            var response = context.Update(state);
            await context.SaveChangesAsync();
            return response.Entity;
        }

        public async Task DeleteState(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var state = await context.States.FirstOrDefaultAsync(r => r.Id == id);
            if (state != null) context.States.Remove(state);
            await context.SaveChangesAsync();
        }

        public async Task<ResourceTemplate> CreateResourceTemplate(ResourceTemplate template)
        {
            using var context = await _factory.CreateDbContextAsync();
            var entity = context.ResourceTemplates.Add(template);
            await context.SaveChangesAsync();
            return entity.Entity;
        }

        public async Task<List<int>> GetActiveResourceIds()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Resources.Where(r => r.IsEnabled).Select(r => r.Id).ToListAsync();
        }

        public async Task<List<ResourceTemplate>> GetResourceTemplates()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.ResourceTemplates.ToListAsync();
        }

        public async Task<ResourceTemplate?> GetResourceTemplate(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var state = await context.ResourceTemplates.FirstOrDefaultAsync(s => s.Id == id);
            return state;
        }

        public async Task<ResourceTemplate> UpdateResourceTemplate(ResourceTemplate template)
        {
            using var context = await _factory.CreateDbContextAsync();
            var response = context.Update(template);
            await context.SaveChangesAsync();
            return response.Entity;
        }

        public async Task DeleteResourceTemplate(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var template = await context.ResourceTemplates.FirstOrDefaultAsync(r => r.Id == id);
            if (template != null) context.ResourceTemplates.Remove(template);
            await context.SaveChangesAsync();
        }

        public async Task UpdateFolderSortOrder(int folderId, int newSortOrder)
        {
            using var context = await _factory.CreateDbContextAsync();
            var folder = await context.Folders.FindAsync(folderId);
            if (folder != null)
            {
                folder.SortOrder = newSortOrder;
                await context.SaveChangesAsync();
            }
        }

        #region Wildcard Operations

        // Wildcard Collection Operations
        public async Task<List<WildcardCollection>> GetAllWildcardCollections()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.WildcardCollections
                .Include(c => c.Entries)
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<WildcardCollection?> GetWildcardCollectionById(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.WildcardCollections
                .Include(c => c.Entries.OrderBy(e => e.SortOrder))
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<WildcardCollection?> GetWildcardCollectionByName(string name)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.WildcardCollections
                .Include(c => c.Entries.OrderBy(e => e.SortOrder))
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
        }

        public async Task<List<WildcardCollection>> GetWildcardCollectionsByCategory(string? category)
        {
            using var context = await _factory.CreateDbContextAsync();
            var query = context.WildcardCollections.Include(c => c.Entries).AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(c => c.Category == category);

            return await query
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<List<string>> GetAllWildcardCategories()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.WildcardCollections
                .Where(c => !string.IsNullOrWhiteSpace(c.Category))
                .Select(c => c.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<WildcardCollection> CreateWildcardCollection(WildcardCollection collection)
        {
            using var context = await _factory.CreateDbContextAsync();
            collection.CreatedAt = DateTime.Now;
            collection.UpdatedAt = DateTime.Now;
            var entity = await context.WildcardCollections.AddAsync(collection);
            await context.SaveChangesAsync();
            return entity.Entity;
        }

        public async Task<WildcardCollection> UpdateWildcardCollection(WildcardCollection collection)
        {
            using var context = await _factory.CreateDbContextAsync();
            var existing = await context.WildcardCollections.FindAsync(collection.Id);
            if (existing == null) return null;

            existing.Name = collection.Name;
            existing.Description = collection.Description;
            existing.Category = collection.Category;
            existing.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();
            return existing;
        }

        public async Task UpdateWildcardCollectionUsage(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var collection = await context.WildcardCollections.FindAsync(id);
            if (collection != null)
            {
                collection.UsageCount++;
                collection.UpdatedAt = DateTime.Now;
                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteWildcardCollection(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var collection = await context.WildcardCollections.FindAsync(id);
            if (collection != null)
            {
                context.WildcardCollections.Remove(collection);
                await context.SaveChangesAsync();
            }
        }

        // Wildcard Entry Operations
        public async Task<List<WildcardEntry>> GetEntriesByCollectionId(int collectionId)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.WildcardEntries
                .Where(e => e.CollectionId == collectionId)
                .OrderBy(e => e.SortOrder)
                .ToListAsync();
        }

        public async Task<WildcardEntry> CreateWildcardEntry(WildcardEntry entry)
        {
            using var context = await _factory.CreateDbContextAsync();
            var entity = await context.WildcardEntries.AddAsync(entry);
            await context.SaveChangesAsync();

            // Update collection timestamp
            var collection = await context.WildcardCollections.FindAsync(entry.CollectionId);
            if (collection != null)
            {
                collection.UpdatedAt = DateTime.Now;
                await context.SaveChangesAsync();
            }

            return entity.Entity;
        }

        public async Task<WildcardEntry> UpdateWildcardEntry(WildcardEntry entry)
        {
            using var context = await _factory.CreateDbContextAsync();
            var existing = await context.WildcardEntries.FindAsync(entry.Id);
            if (existing == null) return null;

            existing.Value = entry.Value;
            existing.Weight = entry.Weight;
            existing.SortOrder = entry.SortOrder;

            await context.SaveChangesAsync();

            // Update collection timestamp
            var collection = await context.WildcardCollections.FindAsync(existing.CollectionId);
            if (collection != null)
            {
                collection.UpdatedAt = DateTime.Now;
                await context.SaveChangesAsync();
            }

            return existing;
        }

        public async Task DeleteWildcardEntry(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var entry = await context.WildcardEntries.FindAsync(id);
            if (entry != null)
            {
                var collectionId = entry.CollectionId;
                context.WildcardEntries.Remove(entry);
                await context.SaveChangesAsync();

                // Update collection timestamp
                var collection = await context.WildcardCollections.FindAsync(collectionId);
                if (collection != null)
                {
                    collection.UpdatedAt = DateTime.Now;
                    await context.SaveChangesAsync();
                }
            }
        }

        #endregion

        #region System Prompt Template Operations

        private async void SeedDefaultSystemPromptTemplates()
        {
            using var context = await _factory.CreateDbContextAsync();

            // Get default templates from OllamaService
            var defaultTemplates = _ollamaService.GetDefaultTemplates();

            // Per-name check: only seed templates that don't exist yet (by Name)
            var existingNames = await context.SystemPromptTemplates
                .Where(t => t.IsDefault)
                .Select(t => t.Name)
                .ToListAsync();
            var toAdd = defaultTemplates.Where(t => !existingNames.Contains(t.Name)).ToList();

            if (toAdd.Count == 0)
                return; // All defaults already seeded

            foreach (var template in toAdd)
            {
                template.CreatedAt = DateTime.UtcNow;
                template.UpdatedAt = DateTime.UtcNow;
            }

            await context.SystemPromptTemplates.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} new default system prompt templates from OllamaService", toAdd.Count);
        }

        public async Task<List<SystemPromptTemplate>> GetSystemPromptTemplates()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.SystemPromptTemplates
                .OrderBy(t => t.IsDefault ? 0 : 1)
                .ThenBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<SystemPromptTemplate?> GetSystemPromptTemplate(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.SystemPromptTemplates.FindAsync(id);
        }

        public async Task<bool> CreateSystemPromptTemplate(SystemPromptTemplate template)
        {
            using var context = await _factory.CreateDbContextAsync();
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;
            await context.SystemPromptTemplates.AddAsync(template);
            return await context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateSystemPromptTemplate(SystemPromptTemplate template)
        {
            using var context = await _factory.CreateDbContextAsync();
            template.UpdatedAt = DateTime.UtcNow;
            context.SystemPromptTemplates.Update(template);
            return await context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteSystemPromptTemplate(int id)
        {
            using var context = await _factory.CreateDbContextAsync();
            var template = await context.SystemPromptTemplates.FindAsync(id);
            if (template == null || template.IsDefault) return false;

            context.SystemPromptTemplates.Remove(template);
            return await context.SaveChangesAsync() > 0;
        }

        #endregion
    }
}
