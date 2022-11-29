using BlazorWebApp.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Data
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Project>()
				.HasMany(p => p.Images)
				.WithOne()
				.HasForeignKey(i => i.ProjectId);
		}

		public DbSet<Image> Images { get; set; }
		public DbSet<Sampler> Samplers { get; set; }
		public DbSet<Project> Projects { get; set; }
	}
}
