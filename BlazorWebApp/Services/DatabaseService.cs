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
        public int PageSize { get; set; }

        public DatabaseService(IDbContextFactory<AppDbContext> factory, SDAPIService api)
        {
            _factory = factory;
            _api = api;
            PageSize = 5;

            InitializeDatabase();
            PopulateModes();
            PopulateSamplers();
        }

        public async Task InitializeDatabase()
        {
            using var context = _factory.CreateDbContext();
            await context.Database.EnsureCreatedAsync();
            await context.Database.MigrateAsync();
        }

        public IAsyncEnumerable<Folder> GetFolders()
        {
            using var context = _factory.CreateDbContext();
            return context.Folders.AsAsyncEnumerable();
        }

        public async Task<List<Folder>> GetFolders(string name)
        {
            using var context = _factory.CreateDbContext();
            if (string.IsNullOrWhiteSpace(name)) return await context.Folders.ToListAsync();
            else return await context.Folders.Where(f => f.Name.Contains(name, StringComparison.InvariantCultureIgnoreCase)).ToListAsync();
        }

        public async Task<List<Project>> GetProjects(int folderId)
        {
            using var context = _factory.CreateDbContext();
            if (folderId <= 0) return await context.Projects.OrderBy(p => p.CreationTime).ToListAsync();
            else return await context.Projects.Where(p => p.FolderId == folderId).OrderBy(p => p.CreationTime).ToListAsync();
        }

        public async Task<Project> GetProject(int id)
        {
            using var context = _factory.CreateDbContext();
            return await context.Projects.SingleOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Project> GetProject(string name)
        {
            using var context = _factory.CreateDbContext();
            return await context.Projects.FirstOrDefaultAsync(p => p.Name == name);
        }

        public async Task<Project> GetLatestProject()
        {
            using var context = _factory.CreateDbContext();
            return await context.Projects.OrderBy(p => p.Id).LastOrDefaultAsync();
        }

        public async Task<Project> CreateProject(Project project)
        {
            using var context = _factory.CreateDbContext();
            var result = await context.Projects.AddAsync(project);
            await context.SaveChangesAsync();
            return result.Entity;
        }

        public async Task<Project> UpdateProject(int projectId, Project data)
        {
            using var context = _factory.CreateDbContext();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (!string.IsNullOrWhiteSpace(data.Name))
                project.Name = data.Name;
            if (!string.IsNullOrWhiteSpace(data.SampleImagePath))
                project.SampleImagePath = data.SampleImagePath;
            if (data.FolderId != null && data.FolderId >= 0 && context.Folders.Any(f => f.Id == data.FolderId))
                project.FolderId = data.FolderId;
            await context.SaveChangesAsync();
            return project;
        }

        /// <summary>
        /// Deletes Project from the database
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns>First Project on the projects table, if any.</returns>
        public async Task<Project?> DeleteProject(int projectId)
        {
            using var context = _factory.CreateDbContext();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            context.Projects.Remove(project);
            await context.SaveChangesAsync();
            return await context.Projects.FirstOrDefaultAsync();
        }

        public async Task RenameProject(int projectId, string name)
        {
            using var context = _factory.CreateDbContext();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            project.Name = name;
            await context.SaveChangesAsync();
        }

        public async Task SetProjectCover(int projectId, string imagePath)
        {
            using var context = _factory.CreateDbContext();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            project.SampleImagePath = imagePath;
            await context.SaveChangesAsync();
        }

        public async Task SetProjectFolder(int projectId, int folderId)
        {
            using var context = _factory.CreateDbContext();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            project.FolderId = folderId;
            await context.SaveChangesAsync();
        }

        public async Task<Image> AddImage(Image image)
        {
            using var context = _factory.CreateDbContext();
            if (context.Images != null)
            {
                var result = await context.Images.AddAsync(image);
                if (result != null) { await context.SaveChangesAsync(); }
            }
            return await context.Images.FirstOrDefaultAsync(i => i.Path == image.Path);
        }

        public async Task<ImagesDto> GetImages(int page)
        {
            using var context = _factory.CreateDbContext();
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

        public async Task<ImagesDto> GetImages(int page, int projectId)
        {
            using var context = _factory.CreateDbContext();
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

        public async Task<ImagesDto> GetSortedImages(int page, int projectId, GallerySettingsModel settings)
        {
            using var context = _factory.CreateDbContext();
            if (context.Images == null) return null;

            var query = context.Images.Where(i => i.ProjectId == projectId);
            if (!string.IsNullOrWhiteSpace(settings.SearchPrompt))
                query = query.Where(i => i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()));
            if (!string.IsNullOrWhiteSpace(settings.SearchNegativePrompt))
                query = query.Where(i => i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower()));
            if (settings.IsFavoritesOnly) query = query.Where(i => i.Favorite);

            List<int> modes = new();
            if (settings.IsModeTxt2Img) modes.Add(1);
            if (settings.IsModeImg2Img) modes.Add(2);
            if (settings.IsModeUpscale) modes.Add(3);
            query = query.Where(i => modes.Contains(i.ModeId));

            switch (settings.OrderBy)
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
            if (settings.OrderDescending) images.Reverse();

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
            using var context = _factory.CreateDbContext();
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project != null) return project.SampleImagePath;
            else return string.Empty;
        }

        public async Task<Image> GetRandomFavorite(int projectId)
        {
            using var context = _factory.CreateDbContext();
            if (context.Images == null) return null;
            return await context.Images.Where(i => i.ProjectId == projectId && i.Favorite).OrderBy(o => EF.Functions.Random()).FirstOrDefaultAsync();
        }

        public async Task<Image> UpdateImage(Image image)
        {
            using var context = _factory.CreateDbContext();
            var response = context.Update(image);
            await context.SaveChangesAsync();
            return response.Entity;
        }

        public async Task<Image> DeleteImage(Image image)
        {
            using var context = _factory.CreateDbContext();
            var response = context.Remove(image);
            await context.SaveChangesAsync();
            return response.Entity;
        }

        public async Task<string> GetSampler(int id)
        {
            if (id == 0) return string.Empty;
            using var context = _factory.CreateDbContext();
            var sampler = await context.Samplers.FirstOrDefaultAsync(s => s.Id == id);
            return sampler.Name;
        }

        public async Task<int> GetSampler(string samplerName)
        {
            using var context = _factory.CreateDbContext();
            var sampler = await context.Samplers.FirstOrDefaultAsync(s => s.Name == samplerName);
            return sampler.Id;
        }

        private async Task PopulateSamplers()
        {
            var samplers = await _api.GetSamplers();
            using var context = _factory.CreateDbContext();
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
            using var context = _factory.CreateDbContext();
            var modeEntity = await context.Modes.FirstOrDefaultAsync(m => m.Type == mode);
            return modeEntity.Id;
        }

        private async void PopulateModes()
        {
            using var context = _factory.CreateDbContext();
            if (context.Modes.Count() == 0)
            {
                foreach (var mode in (ModeType[])Enum.GetValues(typeof(ModeType)))
                {
                    await context.Modes.AddAsync(new Mode { Type = mode });
                }
            }
            await context.SaveChangesAsync();
        }
    }
}
