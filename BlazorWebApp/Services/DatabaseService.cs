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

            PopulateModes();
            PopulateSamplers();
        }

        public async Task<List<Project>> GetProjects()
        {
            using var context = _factory.CreateDbContext();

            return await context.Projects.ToListAsync();
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

        public async Task CreateProject(Project project)
        {
            using var context = _factory.CreateDbContext();

            var result = await context.Projects.AddAsync(project);

            await context.SaveChangesAsync();
        }

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

            List<Image> images = new();

            switch (settings.OrderBy)
            {
                case GalleryOrderBy.Date:
                    images = await context.Images.Where(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())).OrderBy(i => i.DateCreated).ToListAsync();
                    break;
                case GalleryOrderBy.Sampler:
                    images = await context.Images.Where(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())).OrderBy(i => i.SamplerId).ToListAsync();
                    break;
                case GalleryOrderBy.Seed:
                    images = await context.Images.Where(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())).OrderBy(i => i.Seed).ToListAsync();
                    break;
                case GalleryOrderBy.Steps:
                    images = await context.Images.Where(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())).OrderBy(i => i.Steps).ToListAsync();
                    break;
                case GalleryOrderBy.CfgScale:
                    images = await context.Images.Where(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())).OrderBy(i => i.CfgScale).ToListAsync();
                    break;
                case GalleryOrderBy.Width:
                    images = await context.Images.Where(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())).OrderBy(i => i.Width).ToListAsync();
                    break;
                case GalleryOrderBy.Height:
                    images = await context.Images.Where(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())).OrderBy(i => i.Height).ToListAsync();
                    break;
                case GalleryOrderBy.Favorite:
                    images = await context.Images.Where(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())).OrderBy(i => i.Favorite).ToListAsync();
                    break;
                case GalleryOrderBy.Mode:
                    images = await context.Images.Where(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())).OrderBy(i => i.ModeId).ToListAsync();
                    break;
                case GalleryOrderBy.Denoising:
                    images = await context.Images.Where(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())).OrderBy(i => i.DenoisingStrength).ToListAsync();
                    break;
            }
            if (settings.OrderDirection == GalleryOrderDirection.Desc) images.Reverse();

            var pageCount = Math.Ceiling(context.Images.Count(i => i.ProjectId == projectId && i.Prompt.ToLower().Contains(settings.SearchPrompt.ToLower()) && i.NegativePrompt.ToLower().Contains(settings.SearchNegativePrompt.ToLower())) / (float)PageSize);

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
            return context.Images.Where(i => i.ProjectId == projectId && i.Favorite).OrderBy(o => EF.Functions.Random()).FirstOrDefault();
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

        public string GetSampler(int id)
        {
            using var context = _factory.CreateDbContext();

            return context.Samplers.SingleOrDefault(s => s.Id == id).Name;
        }

        public int GetSampler(string samplerName)
        {
            using var context = _factory.CreateDbContext();

            return context.Samplers.SingleOrDefault(s => s.Name == samplerName).Id;
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
                        await context.Samplers.AddAsync(new Sampler { Name = sampler.Name });
                }

            await context.SaveChangesAsync();
        }

        public int GetMode(ModeType mode)
        {
            using var context = _factory.CreateDbContext();

            return context.Modes.SingleOrDefault(m => m.Type == mode).Id;
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
