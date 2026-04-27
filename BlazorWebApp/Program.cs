using BlazorWebApp.Data;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
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

// Scheduler job persistence
builder.Services.AddSingleton<BlazorWebApp.Scheduler.Persistence.IJobRepository, BlazorWebApp.Scheduler.Persistence.JobRepository>();

// Danbooru library persistence
builder.Services.AddSingleton<BlazorWebApp.Data.Repositories.ISavedDanbooruMediaRepository, BlazorWebApp.Data.Repositories.SavedDanbooruMediaRepository>();

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

// Image "Send To" service - context-aware routing of images/parameters to available workflows
builder.Services.AddScoped<IImageSendToService, ImageSendToService>();

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
    FileProvider = new PhysicalFileProvider(builder.Configuration["OutputDir"]),
    RequestPath = "/image"
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(builder.Configuration["ResourcesPath"]),
    RequestPath = "/files/resources"
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(builder.Configuration["ResourcePreviewsPath"]),
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
