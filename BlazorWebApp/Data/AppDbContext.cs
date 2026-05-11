using BlazorWebApp.Data.Converters;
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

        private static string SerializeLoraList(List<Lora> list)
            => JsonSerializer.Serialize(list ?? new List<Lora>());

        private static List<Lora> DeepCopyLoraList(List<Lora> list)
            => list == null ? new List<Lora>() : JsonSerializer.Deserialize<List<Lora>>(JsonSerializer.Serialize(list)) ?? new List<Lora>();

        private static List<Lora> DeserializeLoraList(string v)
            => string.IsNullOrWhiteSpace(v) ? new List<Lora>() : JsonSerializer.Deserialize<List<Lora>>(v) ?? new List<Lora>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Restrict EF Core to only scan types in the Data.Entities namespace
            // This prevents EF from trying to map model/DTO classes as entities
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Explicitly ignore all non-entity types that EF Core might discover through navigation properties
            // These are Models/DTOs that should not be mapped to database tables

            // Workflow-related models
            modelBuilder.Ignore<Workflow>();

            // State and generation models
            modelBuilder.Ignore<Lora>();
            modelBuilder.Ignore<AppState>();
            modelBuilder.Ignore<GeneratedVideo>();
            modelBuilder.Ignore<GeneratedVideos>();

            // GenerationParameters and related types
            modelBuilder.Ignore<GenerationParameters>();
            modelBuilder.Ignore<FragmentParameters>();
            modelBuilder.Ignore<SourceAsset>();

            // Uses Json serialization to store List<string>, the converter and comparer keep the domain class unclutered.
            var listStringConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null));

            var listStringComparer = new ValueComparer<List<string>>(
                (c1, c2) => new HashSet<string>(c1!).SetEquals(new HashSet<string>(c2!)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()
                );

            // Custom JSON options with our converters to preserve value types
            var generationParamsJsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new GenerationParametersJsonConverter() }
            };

            var opt = new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            var stateConverter = new ValueConverter<AppState, string>(v => JsonSerializer.Serialize(v, opt), v => JsonSerializer.Deserialize<AppState>(v, opt));
            var generationParamsConverter = new ValueConverter<GenerationParameters, string>(
                v => JsonSerializer.Serialize(v, generationParamsJsonOptions),
                v => JsonSerializer.Deserialize<GenerationParameters>(v, generationParamsJsonOptions) ?? new GenerationParameters());
            var generateStatePresetBodyConverter = new ValueConverter<GenerateStatePresetBody, string>(
                v => JsonSerializer.Serialize(v ?? new GenerateStatePresetBody(), generationParamsJsonOptions),
                v => string.IsNullOrWhiteSpace(v)
                    ? new GenerateStatePresetBody()
                    : JsonSerializer.Deserialize<GenerateStatePresetBody>(v, generationParamsJsonOptions) ?? new GenerateStatePresetBody());
            var listIntConverter = new ValueConverter<List<int>, string>(v => JsonSerializer.Serialize(v, opt), v => JsonSerializer.Deserialize<List<int>>(v, opt));
            var listIntComparer = new ValueComparer<List<int>>((c1, c2) => c1.SequenceEqual(c2), c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())), c => c.ToList());
            var loraListConverter = new ValueConverter<List<Lora>, string>(
                v => SerializeLoraList(v),
                v => DeserializeLoraList(v)
            );

            var loraListComparer = new ValueComparer<List<Lora>>(
                (c1, c2) => SerializeLoraList(c1) == SerializeLoraList(c2),
                c => SerializeLoraList(c).GetHashCode(),
                c => DeepCopyLoraList(c)
            );

            modelBuilder.Entity<Project>()
                .HasMany(p => p.Images)
                .WithOne()
                .HasForeignKey(i => i.ProjectId);

            modelBuilder.Entity<Mode>()
                .HasMany(m => m.Images)
                .WithOne()
                .HasForeignKey(i => i.ModeId);

            modelBuilder.Entity<Selection>().HasMany(s => s.Images).WithMany(i => i.Selections);

            modelBuilder.Entity<Image>().HasOne(i => i.Model).WithMany().HasForeignKey(nameof(Image.ResourceId));

            modelBuilder.Entity<CleanupImageIndex>()
                .HasOne(index => index.Image)
                .WithMany()
                .HasForeignKey(index => index.ImageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CleanupImageIndex>()
                .HasIndex(index => index.ImageId)
                .IsUnique();

            modelBuilder.Entity<CleanupImageIndex>()
                .HasIndex(index => index.ProjectId);

            modelBuilder.Entity<CleanupImageIndex>()
                .HasIndex(index => index.Status);

            modelBuilder.Entity<CleanupImageIndex>()
                .HasIndex(index => index.ExactHash);

            modelBuilder.Entity<CleanupImageIndex>()
                .HasIndex(index => index.PerceptualHash);

            modelBuilder.Entity<CleanupImageIndex>()
                .HasIndex(index => index.PromptFingerprint);

            modelBuilder.Entity<CleanupImageIndex>()
                .HasIndex(index => index.WorkflowId);

            modelBuilder.Entity<CleanupImageIndex>()
                .Property(index => index.Status)
                .HasConversion<string>();

            modelBuilder.Entity<CleanupImageEmbedding>()
                .HasOne(embedding => embedding.Image)
                .WithMany()
                .HasForeignKey(embedding => embedding.ImageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CleanupImageEmbedding>()
                .HasIndex(embedding => embedding.ImageId);

            modelBuilder.Entity<CleanupImageEmbedding>()
                .HasIndex(embedding => new { embedding.ImageId, embedding.ModelKey, embedding.ModelHash })
                .IsUnique();

            modelBuilder.Entity<CleanupImageEmbedding>()
                .Property(embedding => embedding.Status)
                .HasConversion<string>();

            modelBuilder.Entity<CleanupGroupRun>()
                .HasIndex(run => run.ProjectId);

            modelBuilder.Entity<CleanupGroupRun>()
                .HasIndex(run => run.Strategy);

            modelBuilder.Entity<CleanupGroupRun>()
                .HasIndex(run => run.Status);

            modelBuilder.Entity<CleanupGroupRun>()
                .Property(run => run.Strategy)
                .HasConversion<string>();

            modelBuilder.Entity<CleanupGroupRun>()
                .Property(run => run.Status)
                .HasConversion<string>();

            modelBuilder.Entity<CleanupGroup>()
                .HasOne(group => group.Run)
                .WithMany(run => run.Groups)
                .HasForeignKey(group => group.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CleanupGroup>()
                .HasOne(group => group.RepresentativeImage)
                .WithMany()
                .HasForeignKey(group => group.RepresentativeImageId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CleanupGroup>()
                .HasIndex(group => group.RunId);

            modelBuilder.Entity<CleanupGroup>()
                .HasIndex(group => new { group.RunId, group.GroupKey })
                .IsUnique();

            modelBuilder.Entity<CleanupGroup>()
                .HasIndex(group => group.RepresentativeImageId);

            modelBuilder.Entity<CleanupGroup>()
                .Property(group => group.Strategy)
                .HasConversion<string>();

            modelBuilder.Entity<CleanupGroupMember>()
                .HasOne(member => member.Group)
                .WithMany(group => group.Members)
                .HasForeignKey(member => member.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CleanupGroupMember>()
                .HasOne(member => member.Image)
                .WithMany()
                .HasForeignKey(member => member.ImageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CleanupGroupMember>()
                .HasIndex(member => member.GroupId);

            modelBuilder.Entity<CleanupGroupMember>()
                .HasIndex(member => member.ImageId);

            modelBuilder.Entity<CleanupGroupMember>()
                .HasIndex(member => new { member.GroupId, member.ImageId })
                .IsUnique();

            modelBuilder.Entity<CleanupGroupMember>()
                .Property(member => member.Role)
                .HasConversion<string>();

            modelBuilder.Entity<CleanupGroupMember>()
                .Property(member => member.SuggestedAction)
                .HasConversion<string>();

            modelBuilder.Entity<Folder>().HasIndex(f => f.Name).IsUnique();
            modelBuilder.Entity<Resource>().HasIndex(t => t.Filename).IsUnique();
            modelBuilder.Entity<ResourceType>().HasIndex(t => t.Name).IsUnique();
            modelBuilder.Entity<ResourceSubType>().HasIndex(t => t.Name).IsUnique();
            modelBuilder.Entity<ResourceImage>().HasIndex(t => t.Hash).IsUnique();
            modelBuilder.Entity<Resource>().Property(nameof(Resource.Tags)).HasConversion(listStringConverter, listStringComparer);
            modelBuilder.Entity<Resource>().Property(nameof(Resource.TriggerWords)).HasConversion(listStringConverter, listStringComparer);
            modelBuilder.Entity<ResourceImage>().Property(nameof(ResourceImage.Tags)).HasConversion(listStringConverter, listStringComparer);
            modelBuilder.Entity<ResourceTemplate>().Property(nameof(ResourceTemplate.ResourceIds)).HasConversion(listIntConverter, listIntComparer);
            modelBuilder.Entity<Prompt>().Property(p => p.Loras).HasConversion(loraListConverter, loraListComparer);
            modelBuilder.Entity<Prompt>().Property(p => p.Tags).HasConversion(listStringConverter, listStringComparer);
            modelBuilder.Entity<State>().Property(nameof(State.AppState)).HasConversion(stateConverter);
            modelBuilder.Entity<State>().Property(nameof(State.GenerationParameters)).HasConversion(generationParamsConverter);

            modelBuilder.Entity<GenerateStatePreset>()
                .HasIndex(p => p.WorkflowId);

            modelBuilder.Entity<GenerateStatePreset>()
                .Property(p => p.Body)
                .HasConversion(generateStatePresetBodyConverter);

            // Wildcard entity configuration
            modelBuilder.Entity<WildcardCollection>()
                .HasMany(c => c.Entries)
                .WithOne(e => e.Collection)
                .HasForeignKey(e => e.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WildcardCollection>()
                .HasIndex(c => c.Name);

            modelBuilder.Entity<WildcardCollection>()
                .HasIndex(c => c.Category);

            modelBuilder.Entity<WildcardEntry>()
                .HasIndex(e => e.CollectionId);

            modelBuilder.Entity<WildcardEntry>()
                .HasIndex(e => e.SortOrder);

            // WorkflowState entity configuration
            modelBuilder.Entity<WorkflowState>()
                .HasIndex(ws => ws.WorkflowId)
                .IsUnique();

            modelBuilder.Entity<WorkflowState>()
                .Property(nameof(WorkflowState.Parameters))
                .HasConversion(generationParamsConverter);

            // JobEntity (Scheduler): full Job stored as JSON in Body, denormalized columns for listing.
            var jobBodyConverter = new ValueConverter<Scheduler.Models.Job, string>(
                v => JsonSerializer.Serialize(v, Scheduler.SchedulerJsonOptions.Compact),
                v => JsonSerializer.Deserialize<Scheduler.Models.Job>(v, Scheduler.SchedulerJsonOptions.Compact)
                     ?? new Scheduler.Models.Job());

            modelBuilder.Entity<JobEntity>()
                .HasIndex(j => j.JobId)
                .IsUnique();

            modelBuilder.Entity<JobEntity>()
                .HasIndex(j => j.Status);

            modelBuilder.Entity<JobEntity>()
                .Property(j => j.Body)
                .HasConversion(jobBodyConverter);

            modelBuilder.Entity<JobEntity>()
                .Property(j => j.Status)
                .HasConversion<string>();

            // SchedulerDraft (single-row): holds the unsaved Editor draft between sessions.
            var schedulerDraftBodyConverter = new ValueConverter<Scheduler.Models.Job, string>(
                v => JsonSerializer.Serialize(v, Scheduler.SchedulerJsonOptions.Compact),
                v => JsonSerializer.Deserialize<Scheduler.Models.Job>(v, Scheduler.SchedulerJsonOptions.Compact)
                     ?? new Scheduler.Models.Job());

            modelBuilder.Entity<SchedulerDraft>()
                .Property(d => d.Body)
                .HasConversion(schedulerDraftBodyConverter);

            // SavedDanbooruMedia: tag bundle stored as JSON, unique index on DanbooruPostId.
            modelBuilder.Entity<SavedDanbooruMedia>()
                .HasIndex(m => m.DanbooruPostId)
                .IsUnique();

            modelBuilder.Entity<SavedDanbooruMedia>()
                .Property(m => m.TagsBundle)
                .HasConversion(new Converters.DanbooruTagBundleConverter());

            // Workshop entities - relationships
            modelBuilder.Entity<PromptWorkshopNode>()
                .HasOne(n => n.Session)
                .WithMany(s => s.Nodes)
                .HasForeignKey(n => n.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PromptWorkshopNode>()
                .HasOne(n => n.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(n => n.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Odditarium - JSON-backed body, standalone table (no FK constraint due to SQLite migration limits).
            // SessionId is a soft reference to PromptWorkshopSession when the game runs from Workshop context.
            var odditariumBodyConverter = new ValueConverter<OdditariumBody, string>(
                v => JsonSerializer.Serialize(v ?? new OdditariumBody(), OdditariumJsonOptions.Compact),
                v => string.IsNullOrWhiteSpace(v)
                    ? new OdditariumBody()
                    : JsonSerializer.Deserialize<OdditariumBody>(v, OdditariumJsonOptions.Compact) ?? new OdditariumBody());

            modelBuilder.Entity<OdditariumSession>()
                .Property(w => w.Body)
                .HasConversion(odditariumBodyConverter);

            // No FK constraint - SessionId is a soft reference only.
        }

        public DbSet<Image> Images { get; set; }
        public DbSet<CleanupImageIndex> CleanupImageIndexes { get; set; }
        public DbSet<CleanupImageEmbedding> CleanupImageEmbeddings { get; set; }
        public DbSet<CleanupGroupRun> CleanupGroupRuns { get; set; }
        public DbSet<CleanupGroup> CleanupGroups { get; set; }
        public DbSet<CleanupGroupMember> CleanupGroupMembers { get; set; }
        public DbSet<GenerateStatePreset> GenerateStatePresets { get; set; }
        public DbSet<SavedDanbooruMedia> SavedDanbooruMedia { get; set; }
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
        public DbSet<Selection> Selections { get; set; }
        public DbSet<WildcardCollection> WildcardCollections { get; set; }
        public DbSet<WildcardEntry> WildcardEntries { get; set; }
        public DbSet<SystemPromptTemplate> SystemPromptTemplates { get; set; }
        public DbSet<WorkflowState> WorkflowStates { get; set; }
        public DbSet<JobEntity> Jobs { get; set; }
        public DbSet<SchedulerDraft> SchedulerDrafts { get; set; }
        public DbSet<PromptWorkshopSession> PromptWorkshopSessions { get; set; }
        public DbSet<PromptWorkshopNode> PromptWorkshopNodes { get; set; }
        public DbSet<OdditariumSession> OdditariumSessions { get; set; }
    }
}
