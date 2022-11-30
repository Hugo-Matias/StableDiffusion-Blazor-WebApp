using BlazorWebApp.Data;
using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Services
{
	public class DatabaseService
	{
		private readonly IDbContextFactory<AppDbContext> _factory;
		private readonly SDAPIService _api;
		private readonly int _pageSize;

		public DatabaseService(IDbContextFactory<AppDbContext> factory, SDAPIService api)
		{
			_factory = factory;
			_api = api;

			_pageSize = 10;

			using var context = _factory.CreateDbContext();

			if (context.Samplers.Count() == 0) { PopulateSamplers(); }
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

		public async Task AddImage(Image image)
		{
			using var context = _factory.CreateDbContext();

			if (context.Images != null)
			{
				var result = await context.Images.AddAsync(image);
				if (result != null) { await context.SaveChangesAsync(); }
			}
		}

		public async Task<ImagesDto> GetImages(int page)
		{
			using var context = _factory.CreateDbContext();

			if (context.Images == null) return null;

			var pageCount = Math.Ceiling(context.Images.Count() / (float)_pageSize);

			var images = await context.Images
				.Skip((page - 1) * _pageSize)
				.Take(_pageSize)
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

			var pageCount = Math.Ceiling(context.Images.Count(i => i.ProjectId == projectId) / (float)_pageSize);

			var images = await context.Images
				.Where(i => i.ProjectId == projectId)
				.Skip((page - 1) * _pageSize)
				.Take(_pageSize)
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

			foreach (var sampler in samplers)
			{
				await context.Samplers.AddAsync(new Sampler { Name = sampler.Name });
			}

			await context.SaveChangesAsync();
		}
	}
}
