using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            var listStringConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null));

            var listStringComparer = new ValueComparer<List<string>>(
                (c1, c2) => new HashSet<string>(c1!).SetEquals(new HashSet<string>(c2!)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()
                );

            var opt = new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            var stateConverter = new ValueConverter<AppState, string>(v => JsonSerializer.Serialize(v, opt), v => JsonSerializer.Deserialize<AppState>(v, opt));
            var txt2imgConverter = new ValueConverter<Txt2ImgParameters, string>(v => JsonSerializer.Serialize(v, opt), v => JsonSerializer.Deserialize<Txt2ImgParameters>(v, opt));
            var img2imgConverter = new ValueConverter<Img2ImgParameters, string>(v => JsonSerializer.Serialize(v, opt), v => JsonSerializer.Deserialize<Img2ImgParameters>(v, opt));
            var upscaleConverter = new ValueConverter<UpscaleParameters, string>(v => JsonSerializer.Serialize(v, opt), v => JsonSerializer.Deserialize<UpscaleParameters>(v, opt));
            var listIntConverter = new ValueConverter<List<int>, string>(v => JsonSerializer.Serialize(v, opt), v => JsonSerializer.Deserialize<List<int>>(v, opt));
            var listIntComparer = new ValueComparer<List<int>>((c1, c2) => c1.SequenceEqual(c2), c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())), c => c.ToList());

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
            modelBuilder.Entity<Resource>().Property(nameof(Resource.Tags)).HasConversion(listStringConverter, listStringComparer);
            modelBuilder.Entity<Resource>().Property(nameof(Resource.TriggerWords)).HasConversion(listStringConverter, listStringComparer);
            modelBuilder.Entity<ResourceImage>().Property(nameof(ResourceImage.Tags)).HasConversion(listStringConverter, listStringComparer);
            modelBuilder.Entity<ResourceTemplate>().Property(nameof(ResourceTemplate.ResourceIds)).HasConversion(listIntConverter, listIntComparer);
            modelBuilder.Entity<State>().Property(nameof(State.AppState)).HasConversion(stateConverter);
            modelBuilder.Entity<State>().Property(nameof(State.Txt2ImgParameters)).HasConversion(txt2imgConverter);
            modelBuilder.Entity<State>().Property(nameof(State.Img2ImgParameters)).HasConversion(img2imgConverter);
            modelBuilder.Entity<State>().Property(nameof(State.UpscaleParameters)).HasConversion(upscaleConverter);
        }

        public DbSet<Image> Images { get; set; }
        public DbSet<Entities.Sampler> Samplers { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Mode> Modes { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Prompt> Prompts { get; set; }
        public DbSet<Resource> Resources { get; set; }
        public DbSet<ResourceType> ResourceTypes { get; set; }
        public DbSet<ResourceSubType> ResourceSubTypes { get; set; }
        public DbSet<ResourceImage> ResourceImages { get; set; }
        public DbSet<ResourceTemplate> ResourceTemplates { get; set; }
        public DbSet<State> States { get; set; }
    }
}
