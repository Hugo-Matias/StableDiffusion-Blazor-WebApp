using BlazorWebApp.Data;
using BlazorWebApp.Services;
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

// Core data services
// DatabaseService needs dual registration: components use concrete type, services use interface
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<IDatabaseService>(sp => sp.GetRequiredService<DatabaseService>());
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

// Core application services
builder.Services.AddSingleton<ManagerService>();

// Image service - dual registration for concrete and interface access
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<IImageService>(sp => sp.GetRequiredService<ImageService>());

builder.Services.AddSingleton<CsvService>();

// Progress service - dual registration: ProgressContainer uses concrete for OnUpdate event
builder.Services.AddSingleton<ProgressService>();
builder.Services.AddSingleton<IProgressService>(sp => sp.GetRequiredService<ProgressService>());

builder.Services.AddSingleton<ResourcesService>();

// Router service - interface-only (no concrete type injection needed)
builder.Services.AddSingleton<IRouterService, RouterService>();

// Workflow service - interface-only (no concrete type injection needed)
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();

builder.Services.AddSingleton<DynamicPromptsService>();
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<ThemeService>();

// Asset resolution service for workflow models
builder.Services.AddScoped<IAssetResolverService, AssetResolverService>();

builder.Services.AddScoped<JavascriptService>();
builder.Services.AddScoped<OllamaService>();

// MagickService - transient, injected by concrete type where needed
builder.Services.AddTransient<MagickService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

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

var tagUsageService = app.Services.GetRequiredService<CacheService>();
_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(TimeSpan.FromMinutes(30));
        await tagUsageService.RefreshTagCache();
    }
});
