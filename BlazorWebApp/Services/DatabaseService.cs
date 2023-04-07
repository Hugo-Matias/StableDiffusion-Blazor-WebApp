using BlazorWebApp.Data;
using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Services
{
    public class DatabaseService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly SDAPIService _api;
        private readonly IConfiguration _configuration;

        public int PageSize { get; set; }

        public DatabaseService(IDbContextFactory<AppDbContext> factory, SDAPIService api, IConfiguration configuration)
        {
            _factory = factory;
            _api = api;
            _configuration = configuration;
            PageSize = 5;

            InitializeDatabase();
            PopulateModes();
            PopulateSamplers();
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
            return await context.Folders.ToListAsync();
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
            if (folderId <= 0) return await context.Projects.OrderBy(p => p.CreationTime).ToListAsync();
            else return await context.Projects.Where(p => p.FolderId == folderId).OrderBy(p => p.CreationTime).ToListAsync();
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

        public async Task<Image> AddImage(Image image)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.Images != null)
            {
                var result = await context.Images.AddAsync(image);
                if (result != null) { await context.SaveChangesAsync(); }
            }
            return await context.Images.FirstOrDefaultAsync(i => i.Path == image.Path);
        }

        public async Task<List<Image>> GetImages(List<int> imageIds)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.Images.Where(i => imageIds.Contains(i.Id)).ToListAsync();
        }

        public async Task<ImagesDto> GetPagedImages(int page)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.Images == null) return null;
            var pageCount = Math.Ceiling(context.Images.Count() / (float)PageSize);
            var images = await context.Images
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
            query = query.Where(i => modes.Contains(i.ModeId));

            switch (state.OrderBy)
            {
                case GalleryOrderBy.Date:
                    query = query.OrderBy(i => i.DateCreated);
                    break;
                case GalleryOrderBy.Sampler:
                    query = query.OrderBy(i => i.SamplerId);
                    break;
                case GalleryOrderBy.Seed:
                    query = query.OrderBy(i => i.Seed);
                    break;
                case GalleryOrderBy.Steps:
                    query = query.OrderBy(i => i.Steps);
                    break;
                case GalleryOrderBy.CfgScale:
                    query = query.OrderBy(i => i.CfgScale);
                    break;
                case GalleryOrderBy.Width:
                    query = query.OrderBy(i => i.Width);
                    break;
                case GalleryOrderBy.Height:
                    query = query.OrderBy(i => i.Height);
                    break;
                case GalleryOrderBy.Favorite:
                    query = query.OrderBy(i => i.Favorite);
                    break;
                case GalleryOrderBy.Mode:
                    query = query.OrderBy(i => i.ModeId);
                    break;
                case GalleryOrderBy.Denoising:
                    query = query.OrderBy(i => i.DenoisingStrength);
                    break;
            }

            var images = await query.ToListAsync();
            if (state.OrderDescending) images.Reverse();

            var pageCount = Math.Ceiling(images.Count / (float)PageSize);

            return new ImagesDto
            {
                Images = images.Skip((page - 1) * PageSize).Take(PageSize).ToList(),
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

        public async Task<Image> GetRandomFavorite(int projectId)
        {
            using var context = await _factory.CreateDbContextAsync();
            if (context.Images == null) return null;
            return await context.Images.Where(i => i.ProjectId == projectId && i.Favorite).OrderBy(o => EF.Functions.Random()).FirstOrDefaultAsync();
        }

        public async Task<Image> UpdateImage(Image image)
        {
            using var context = await _factory.CreateDbContextAsync();
            var response = context.Update(image);
            await context.SaveChangesAsync();
            return response.Entity;
        }

        public async Task<Image> DeleteImage(Image image)
        {
            using var context = await _factory.CreateDbContextAsync();
            var response = context.Remove(image);
            await context.SaveChangesAsync();
            return response.Entity;
        }

        public async Task<string> GetSampler(int id)
        {
            if (id == 0) return string.Empty;
            using var context = await _factory.CreateDbContextAsync();
            var sampler = await context.Samplers.FirstOrDefaultAsync(s => s.Id == id);
            return sampler.Name;
        }

        public async Task<int> GetSampler(string samplerName)
        {
            using var context = await _factory.CreateDbContextAsync();
            var sampler = await context.Samplers.FirstOrDefaultAsync(s => s.Name == samplerName);
            return sampler.Id;
        }

        private async Task PopulateSamplers()
        {
            var samplers = await _api.GetSamplers();
            using var context = await _factory.CreateDbContextAsync();
            if (context.Samplers.Count() == 0 || context.Samplers.Count() < samplers.Count)
                foreach (var sampler in samplers)
                {
                    var currentSampler = context.Samplers.SingleOrDefault(s => s.Name == sampler.Name);
                    if (currentSampler == null)
                        await context.Samplers.AddAsync(new Data.Entities.Sampler { Name = sampler.Name });
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
            if (context.Modes.Count() == 0)
            {
                foreach (var mode in (ModeType[])Enum.GetValues(typeof(ModeType)))
                {
                    await context.Modes.AddAsync(new Mode { Type = mode });
                }
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
            entity.Title = prompt.Title;
            entity.Positive = prompt.Positive;
            entity.Negative = prompt.Negative;
            entity.IsFavorite = prompt.IsFavorite;
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

        public async Task<Resource> GetResourceById(int id)
        {
            using var context = _factory.CreateDbContext();
            return await context.Resources.Include(r => r.Type).Include(r => r.SubType).FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<Resource> GetResourceByCivitaiModelId(int civitaiId)
        {
            using var context = _factory.CreateDbContext();
            return await context.Resources.Include(r => r.Type).Include(r => r.SubType).FirstOrDefaultAsync(r => r.CivitaiModelId == civitaiId);
        }

        public async Task<Resource> GetResourceByFilename(string filename)
        {
            using var context = _factory.CreateDbContext();
            return await context.Resources.Include(r => r.Type).Include(r => r.SubType).FirstOrDefaultAsync(r => r.Filename == filename);
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
            var state = await context.States.Where(s => s.Version == int.Parse(_configuration["StateVersion"])).FirstOrDefaultAsync(s => s.Id == id);
            if (state == null && id == 1)
            {
                state = await context.States.Where(s => s.Version == int.Parse(_configuration["StateVersion"])).FirstOrDefaultAsync();
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

    }
}
