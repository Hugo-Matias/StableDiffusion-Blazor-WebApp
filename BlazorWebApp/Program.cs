using BlazorWebApp.Data;
using BlazorWebApp.Services;
using BlazorWebApp.Services.Templating;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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
builder.Services.AddSingleton<IOrchestratorService, OrchestratorService>();
// Image service - interface-only (IImageService.Progress has setter for WebSocket updates)
builder.Services.AddSingleton<IImageService, ImageService>();

builder.Services.AddSingleton<CsvService>();
// Progress service - interface-only
builder.Services.AddSingleton<IProgressService, ProgressService>();

// Resources service - interface-only for testability
builder.Services.AddSingleton<IResourcesService, ResourcesService>();

// Router service - interface-only
builder.Services.AddSingleton<IRouterService, RouterService>();

// Workflow service - interface-only
builder.Services.AddSingleton<WorkflowTemplateParser>();
builder.Services.AddSingleton<IFragmentSchemaService, FragmentSchemaService>();
builder.Services.AddSingleton<FragmentConditionValidator>();
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();

// Workflow state persistence service (per-workflow saved parameters)
builder.Services.AddSingleton<IWorkflowStateService, WorkflowStateService>();

// Component registry for fragment-to-component mapping
builder.Services.AddSingleton<IComponentRegistry, ComponentRegistry>();

// Generation parameter service for dynamic parameter management
builder.Services.AddScoped<IGenerationParameterService, GenerationParameterService>();

builder.Services.AddSingleton<DynamicPromptsService>();

// Cache service - interface-only for testability
builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddSingleton<ThemeService>();

// Asset resolution service for workflow models
builder.Services.AddScoped<IAssetResolverService, AssetResolverService>();

builder.Services.AddScoped<JavascriptService>();
builder.Services.AddSingleton<OllamaService>();

// Wildcard service for prompt wildcard management
builder.Services.AddSingleton<IWildcardService, WildcardService>();

// Template cache service for compiled Scriban templates
builder.Services.AddSingleton<ITemplateCacheService, TemplateCacheService>();

// Fluid template service for Liquid template rendering (replaces Scriban for fragments)
builder.Services.AddSingleton<IFluidTemplateService, FluidTemplateService>();

// Workflow validation service for startup template validation
builder.Services.AddSingleton<IWorkflowValidationService, WorkflowValidationService>();

// Info service for contextual help/shortcuts across the app
builder.Services.AddSingleton<IInfoService, InfoService>();

// Tokenizer service for accurate token counting
builder.Services.AddSingleton<ITokenizerService, TokenizerService>();

// MagickService - transient, injected by concrete type where needed
builder.Services.AddTransient<MagickService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Pre-compile and validate workflow templates at startup
{
    var templateCacheService = app.Services.GetRequiredService<ITemplateCacheService>();
    var workflowPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
    
    // Pre-compile all templates (always, for performance)
    var templatesCompiled = templateCacheService.PrecompileAll(Path.Combine(workflowPath, "Templates"));
    var fragmentsCompiled = templateCacheService.PrecompileAll(Path.Combine(workflowPath, "Fragments"));
    
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Pre-compiled {Templates} workflow templates and {Fragments} fragments", 
        templatesCompiled, fragmentsCompiled);

    // Validate templates in Development mode only
    if (app.Environment.IsDevelopment())
    {
        var validationService = app.Services.GetRequiredService<IWorkflowValidationService>();
        var validationResult = validationService.ValidateAllTemplates();
        
        if (!validationResult.IsValid)
        {
            logger.LogWarning(
                "Workflow template validation found {ErrorCount} errors. Check logs for details.",
                validationResult.Errors.Count);
        }
        
        // Validate fragment conditions
        var conditionGenerator = new FragmentConditionGenerator(workflowPath, 
            app.Services.GetRequiredService<ILogger<FragmentConditionGenerator>>());
        var conditionReport = conditionGenerator.GenerateAndValidate();
        
        if (conditionReport.HasIssues)
        {
            logger.LogWarning("Fragment condition validation found {IssueCount} issues:\n{Report}",
                conditionReport.TotalIssues,
                conditionReport.GenerateReport());
        }
        else
        {
            logger.LogInformation("? All fragment conditions are valid");
        }
        
        // Log compilation errors from cache service
        var compilationErrors = templateCacheService.CompilationErrors;
        if (compilationErrors.Count > 0)
        {
            logger.LogWarning("Template compilation found {Count} errors:", compilationErrors.Count);
            foreach (var error in compilationErrors)
            {
                var location = error.Line.HasValue ? $":{error.Line}" : "";
                logger.LogWarning("  {Path}{Location}: {Message}", error.FilePath, location, error.Message);
            }
        }
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
