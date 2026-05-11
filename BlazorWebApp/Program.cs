using BlazorWebApp.Data;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using BlazorWebApp.Services.Cleanup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.StaticFiles;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
var maxBufferSize = 100 * 1024 * 1024;
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor().AddHubOptions(opt => { opt.MaximumReceiveMessageSize = maxBufferSize; });

builder.Services.AddMudServices(opt =>
{
    opt.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomEnd;
    opt.SnackbarConfiguration.PreventDuplicates = false;
});

builder.Services.AddHttpClient<ComfyUIService>();
// Register the interface to resolve to the same ComfyUIService instance
builder.Services.AddSingleton<IComfyUIService>(sp => sp.GetRequiredService<ComfyUIService>());
builder.Services.AddHttpClient<CivitaiService>();
builder.Services.AddScoped<ICivitaiResourceImageService, CivitaiResourceImageService>();
builder.Services.Configure<BlazorWebApp.Models.DanbooruOptions>(builder.Configuration.GetSection(BlazorWebApp.Models.DanbooruOptions.SectionName));
builder.Services.AddHttpClient<DanbooruService>();
builder.Services.AddDbContextFactory<AppDbContext>(opt => { opt.UseSqlite("Data Source=BlazorWebApp.db"); opt.EnableSensitiveDataLogging(); });

// Creates a singleton of the WS service to inject with DI and use it as the hosted service as well
builder.Services.AddSingleton<ComfyUIWebsocketService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ComfyUIWebsocketService>());
builder.Services.AddSingleton<ComfyUIEventBus>();
builder.Services.AddSingleton<IEventService, EventService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();

// Core data services - interface-only
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IIOService, IOService>();

// State management service
builder.Services.AddSingleton<IStateService, StateService>();

// Backend orchestration service (ComfyUI)
builder.Services.AddSingleton<IBackendService, BackendService>();

// Model and asset management service
builder.Services.AddSingleton<IModelService, ModelService>();

// Gallery management service (folders, projects, image selection)
builder.Services.AddSingleton<IGalleryService, GalleryService>();

// Session management service (canvas, image editor, videos) - singleton with circuit isolation
builder.Services.AddSingleton<ISessionService, SessionService>();

// Orchestrator service - interface-only (no consumers need concrete type)
builder.Services.AddScoped<IOrchestratorService, OrchestratorService>();
// Image service - interface-only (IImageService.Progress has setter for WebSocket updates)
builder.Services.AddSingleton<IImageService, ImageService>();

builder.Services.AddSingleton<CsvService>();
// Progress service - interface-only
builder.Services.AddSingleton<IProgressService, ProgressService>();

// Resources service - interface-only for testability
builder.Services.AddSingleton<IResourcesService, ResourcesService>();

// Resource cache service - in-memory cache with event-based invalidation
builder.Services.AddSingleton<IResourceCacheService, ResourceCacheService>();

// Resource filter service - filters assets/LoRAs by workflow compatibility
builder.Services.AddSingleton<IResourceFilterService, ResourceFilterService>();

// Resource filter state service - per-circuit filter settings (persists across page navigations, resets on workflow change)
builder.Services.AddScoped<IResourceFilterStateService, ResourceFilterStateService>();

// Router service - interface-only
builder.Services.AddSingleton<IRouterService, RouterService>();

// Workflow service - uses C# IWorkflowBuilder implementations only
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();

// Workflow state persistence service (per-workflow saved parameters)
builder.Services.AddSingleton<IWorkflowStateService, WorkflowStateService>();

// Generate-page named workflow state presets
builder.Services.AddScoped<IGenerateStatePresetService, GenerateStatePresetService>();

// Scheduler job persistence
builder.Services.AddSingleton<BlazorWebApp.Scheduler.Persistence.IJobRepository, BlazorWebApp.Scheduler.Persistence.JobRepository>();

// Danbooru library persistence
builder.Services.AddSingleton<BlazorWebApp.Data.Repositories.ISavedDanbooruMediaRepository, BlazorWebApp.Data.Repositories.SavedDanbooruMediaRepository>();

// Gallery cleanup persistence
builder.Services.AddSingleton<BlazorWebApp.Data.Repositories.ICleanupRepository, BlazorWebApp.Data.Repositories.CleanupRepository>();
builder.Services.AddSingleton<ICleanupPromptIndexService, CleanupPromptIndexService>();
builder.Services.AddSingleton<ICleanupImageHashService, CleanupImageHashService>();
builder.Services.AddSingleton<ICleanupIndexingService, CleanupIndexingService>();
builder.Services.Configure<CleanupEmbeddingOptions>(builder.Configuration.GetSection(CleanupEmbeddingOptions.SectionName));
builder.Services.AddSingleton<ICleanupEmbeddingModelMetadataService, CleanupEmbeddingModelMetadataService>();
builder.Services.AddSingleton<ICleanupEmbeddingVectorCodec, CleanupEmbeddingVectorCodec>();
builder.Services.AddSingleton<ICleanupEmbeddingImagePreprocessor, CleanupEmbeddingImagePreprocessor>();
builder.Services.AddSingleton<ICleanupEmbeddingRuntime, CleanupEmbeddingRuntime>();
builder.Services.AddSingleton<IImageEmbeddingService, OnnxImageEmbeddingService>();
builder.Services.AddSingleton<ICleanupEmbeddingIndexingService, CleanupEmbeddingIndexingService>();
builder.Services.Configure<CleanupScoringOptions>(builder.Configuration.GetSection(CleanupScoringOptions.SectionName));
builder.Services.AddSingleton<ICleanupScoringModelMetadataService, CleanupScoringModelMetadataService>();
builder.Services.AddSingleton<IImageScoringService, OnnxImageScoringService>();
builder.Services.AddSingleton<ICleanupScoreIndexingService, CleanupScoreIndexingService>();
builder.Services.Configure<CleanupIndexingQueueOptions>(builder.Configuration.GetSection(CleanupIndexingQueueOptions.SectionName));
builder.Services.AddSingleton<ICleanupIndexingQueue, CleanupIndexingQueue>();
builder.Services.AddHostedService(sp => (CleanupIndexingQueue)sp.GetRequiredService<ICleanupIndexingQueue>());
builder.Services.AddSingleton<ICleanupGroupingService, CleanupGroupingService>();
builder.Services.Configure<CleanupGroupExplanationOptions>(builder.Configuration.GetSection(CleanupGroupExplanationOptions.SectionName));
builder.Services.AddScoped<ICleanupGroupExplanationService, CleanupGroupExplanationService>();

// Danbooru library service - plain HttpClient for CDN downloads (no auth headers needed)
builder.Services.AddHttpClient<IDanbooruLibraryService, DanbooruLibraryService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BlazorWebApp/1.0");
});
builder.Services.AddScoped<BlazorWebApp.Scheduler.Persistence.ISchedulerDraftStore, BlazorWebApp.Scheduler.Persistence.SchedulerDraftStore>();

// Scheduler variation engine
builder.Services.AddScoped<BlazorWebApp.Scheduler.Engine.IVariationMaterializer, BlazorWebApp.Scheduler.Engine.VariationMaterializer>();
builder.Services.AddScoped<BlazorWebApp.Scheduler.Engine.IVariationSequencer, BlazorWebApp.Scheduler.Engine.VariationSequencer>();

// Scheduler execution engine
builder.Services.AddScoped<BlazorWebApp.Scheduler.Engine.IParameterApplier, BlazorWebApp.Scheduler.Engine.ParameterApplier>();
builder.Services.AddScoped<BlazorWebApp.Scheduler.Engine.IDirectiveExecutor, BlazorWebApp.Scheduler.Engine.DirectiveExecutor>();
builder.Services.AddScoped<BlazorWebApp.Scheduler.Engine.IJobGenerationRunner, BlazorWebApp.Scheduler.Engine.ImageServiceJobGenerationRunner>();
builder.Services.AddScoped<BlazorWebApp.Scheduler.ISchedulerService, BlazorWebApp.Scheduler.SchedulerService>();

// Scheduler snapshot buffer (Generate -> Editor handoff)
builder.Services.AddScoped<BlazorWebApp.Scheduler.IScheduleSnapshotService, BlazorWebApp.Scheduler.ScheduleSnapshotService>();

// Scheduler editor draft state (in-memory + debounced persistence for unsaved work)
builder.Services.AddScoped<BlazorWebApp.Scheduler.ISchedulerEditorState, BlazorWebApp.Scheduler.SchedulerEditorState>();

// Component registry for fragment-to-component mapping
builder.Services.AddSingleton<IComponentRegistry, ComponentRegistry>();

// Generation parameter service for dynamic parameter management
builder.Services.AddScoped<IGenerationParameterService, GenerationParameterService>();

// Media "Send To" service - context-aware routing of source media/parameters to available workflows
builder.Services.AddScoped<IMediaSendToService, MediaSendToService>();

// Prompt "Send To" service - apply Workshop prompts to a target workflow
builder.Services.AddScoped<IPromptSendToService, PromptSendToService>();

builder.Services.AddSingleton<DynamicPromptsService>();

// Cache service - interface-only for testability
builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddSingleton<ThemeService>();

// Asset resolution service for workflow models
builder.Services.AddScoped<IAssetResolverService, AssetResolverService>();

builder.Services.AddScoped<JavascriptService>();
builder.Services.AddSingleton<OllamaService>();

// Tag prompt builder service (two-pass Danbooru tag resolution)
builder.Services.AddScoped<TagPromptService>();

// Phase 12 - Image-to-Prompt orchestrator (vision-language models)
builder.Services.AddScoped<VLModelService>();

// Wildcard service for prompt wildcard management
builder.Services.AddSingleton<IWildcardService, WildcardService>();

// Phase 10 - Wildcard Forge (LLM x Wildcards) services
builder.Services.AddSingleton<BlazorWebApp.Services.WildcardForge.WildcardForgeKnowledge>();
builder.Services.AddScoped<BlazorWebApp.Services.WildcardForge.PromptComposer>();
builder.Services.AddScoped<BlazorWebApp.Services.WildcardForge.IWildcardForgeService, BlazorWebApp.Services.WildcardForge.WildcardForgeService>();

// Info service for contextual help/shortcuts across the app
builder.Services.AddSingleton<IInfoService, InfoService>();

// Tokenizer service for accurate token counting
builder.Services.AddSingleton<ITokenizerService, TokenizerService>();

// Artist browser service for Anima style gallery artist tags
builder.Services.AddSingleton<IArtistBrowserService, ArtistBrowserService>();

// MagickService - transient, injected by concrete type where needed
builder.Services.AddTransient<MagickService>();

// Workshop service – prompt workshop session/node tree orchestration
builder.Services.AddSingleton<IWorkshopService, WorkshopService>();

// Odditarium - freestyle prompt game service (replaces WorkshopWizardService)
builder.Services.AddSingleton<IOdditariumService, OdditariumService>();

// Prompt history service - session-scoped undo/redo for Generate page prompts
builder.Services.AddScoped<IPromptHistoryService, PromptHistoryService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Validate C# workflow builders at startup in Development mode
if (app.Environment.IsDevelopment())
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var workflowService = app.Services.GetRequiredService<IWorkflowService>();
    var workflows = workflowService.GetWorkflows();

    logger.LogInformation("Discovered {Count} C# workflow builder(s)", workflows.Count);

    foreach (var workflow in workflows)
    {
        logger.LogInformation("  - {Title} ({Base}/{Mode})", workflow.Title, workflow.Base, workflow.Mode);
    }

    if (workflows.Count == 0)
    {
        logger.LogWarning("No C# workflow builders found! Users will have no available workflows.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(builder.Configuration["OutputDir"]!),
    RequestPath = "/image"
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(builder.Configuration["ResourcesPath"]!),
    RequestPath = "/files/resources"
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(builder.Configuration["ResourcePreviewsPath"]!),
    RequestPath = "/files/resource_previews"
});

var danbooruOptions = app.Services.GetRequiredService<IOptions<DanbooruOptions>>().Value;
var danbooruLogger = app.Services.GetRequiredService<ILogger<Program>>();
if (string.IsNullOrWhiteSpace(danbooruOptions.SavedMediaPath))
{
    danbooruLogger.LogWarning("Danbooru SavedMediaPath is not configured. Library save/view will not work.");
}
else if (!Directory.Exists(danbooruOptions.SavedMediaPath))
{
    danbooruLogger.LogWarning("Danbooru SavedMediaPath '{Path}' does not exist. Library save/view will not work.", danbooruOptions.SavedMediaPath);
}
else
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(danbooruOptions.SavedMediaPath),
        RequestPath = "/files/danbooru"
    });
}

app.UseRouting();

app.MapBlazorHub();

var sourcePreviewContentTypes = new FileExtensionContentTypeProvider();
app.MapGet("/api/source-preview", IResult (string? filename, IConfiguration configuration, ILogger<Program> log) =>
{
    if (string.IsNullOrWhiteSpace(filename))
    {
        return Results.BadRequest(new { error = "Missing filename." });
    }

    var normalized = filename.Replace('\\', '/');
    var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (Path.IsPathRooted(normalized) || segments.Any(segment => segment is "." or ".."))
    {
        return Results.BadRequest(new { error = "Invalid filename." });
    }

    var previewPath = ResolveSourcePreviewPath(configuration, normalized);
    if (previewPath is null)
    {
        log.LogWarning("Source preview file '{Filename}' was not found under ComfyUI:InputsPath.", normalized);
        return Results.NotFound(new { error = "Source file not found." });
    }

    var contentType = ResolveSourcePreviewContentType(sourcePreviewContentTypes, previewPath);
    var stream = new FileStream(
        previewPath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 64 * 1024,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
    return Results.File(stream, contentType, enableRangeProcessing: true);
});

// Direct multipart upload endpoint that streams large source files (videos in particular)
// to ComfyUI WITHOUT going through SignalR. The Generate page's video drag/drop control
// uses fetch() against this route so the circuit stays responsive while the file uploads.
// Returns the ComfyUI input filename which is then stored on SourceAsset.Filename.
app.MapPost("/api/upload-source", async (HttpRequest request, IComfyUIService comfy, ILogger<Program> log) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "Missing file." });
    }

    try
    {
        await using var stream = file.OpenReadStream();
        var filename = await comfy.UploadStreamAsync(stream, file.FileName, file.ContentType);
        return Results.Json(new { filename });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to forward source upload '{OriginalName}' ({Bytes} bytes) to ComfyUI", file.FileName, file.Length);
        return Results.StatusCode(StatusCodes.Status502BadGateway);
    }
})
.DisableAntiforgery()
// Allow up to 2 GB per source upload (videos). Kestrel's default request body limit is 30 MB
// and the form parser's multipart limit is 128 MB; both would reject most video clips
// before the handler ever runs.
.WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(2L * 1024 * 1024 * 1024))
.WithMetadata(new Microsoft.AspNetCore.Mvc.RequestFormLimitsAttribute { MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024 });

app.MapFallbackToPage("/_Host");

app.Run();

var tagUsageService = app.Services.GetRequiredService<ICacheService>();
_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(TimeSpan.FromMinutes(30));
        await tagUsageService.RefreshTagCache();
    }
});

static string? ResolveSourcePreviewPath(IConfiguration configuration, string normalizedFilename)
{
    var configuredInputsPath = configuration["ComfyUI:InputsPath"];
    if (string.IsNullOrWhiteSpace(configuredInputsPath))
    {
        return null;
    }

    var inputsRoot = Path.GetFullPath(configuredInputsPath);
    var candidatePaths = new[]
    {
        normalizedFilename,
        Path.Combine("input", normalizedFilename)
    };

    foreach (var candidatePath in candidatePaths)
    {
        var resolvedPath = Path.GetFullPath(Path.Combine(inputsRoot, candidatePath));
        if (IsPathUnderRoot(inputsRoot, resolvedPath) && File.Exists(resolvedPath))
        {
            return resolvedPath;
        }
    }

    return null;
}

static bool IsPathUnderRoot(string rootPath, string candidatePath)
{
    var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    return candidatePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
}

static string ResolveSourcePreviewContentType(FileExtensionContentTypeProvider provider, string path)
{
    if (provider.TryGetContentType(path, out var contentType))
    {
        return contentType;
    }

    return Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mov" => "video/quicktime",
        ".mkv" => "video/x-matroska",
        ".avi" => "video/x-msvideo",
        _ => "application/octet-stream"
    };
}
