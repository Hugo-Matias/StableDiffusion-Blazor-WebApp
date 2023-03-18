using BlazorWebApp.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace BlazorWebApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Uses Json serialization to store List<string>, the converter and comparer keep the domain class unclutered.
            // Doc: https://stackoverflow.com/a/52499249/12173765
            //      https://learn.microsoft.com/en-us/ef/core/modeling/value-comparers?tabs=ef5
            var converter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null));

            var comparer = new ValueComparer<List<string>>(
                (c1, c2) => new HashSet<string>(c1!).SetEquals(new HashSet<string>(c2!)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()
                );

            modelBuilder.Entity<Project>()
                .HasMany(p => p.Images)
                .WithOne()
                .HasForeignKey(i => i.ProjectId);

            modelBuilder.Entity<Mode>()
                .HasMany(m => m.Images)
                .WithOne()
                .HasForeignKey(i => i.ModeId);

            modelBuilder.Entity<Folder>().HasIndex(f => f.Name).IsUnique();
            modelBuilder.Entity<Resource>().HasIndex(t => t.Filename).IsUnique();
            modelBuilder.Entity<ResourceType>().HasIndex(t => t.Name).IsUnique();
            modelBuilder.Entity<ResourceSubType>().HasIndex(t => t.Name).IsUnique();
            modelBuilder.Entity<ResourceImage>().HasIndex(t => t.Hash).IsUnique();
            modelBuilder.Entity<Resource>().Property(nameof(Resource.Tags)).HasConversion(converter, comparer);
            modelBuilder.Entity<Resource>().Property(nameof(Resource.TriggerWords)).HasConversion(converter, comparer);
            modelBuilder.Entity<ResourceImage>().Property(nameof(ResourceImage.Tags)).HasConversion(converter, comparer);
        }

        public DbSet<Image> Images { get; set; }
        public DbSet<Sampler> Samplers { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Mode> Modes { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Prompt> Prompts { get; set; }
        public DbSet<Resource> Resources { get; set; }
        public DbSet<ResourceType> ResourceTypes { get; set; }
        public DbSet<ResourceSubType> ResourceSubTypes { get; set; }
        public DbSet<ResourceImage> ResourceImages { get; set; }
    }
}
