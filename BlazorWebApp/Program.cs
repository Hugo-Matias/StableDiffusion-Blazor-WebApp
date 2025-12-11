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

builder.Services.AddHttpClient<SDAPIService>();
builder.Services.AddHttpClient<ComfyUIService>();
builder.Services.AddHttpClient<CivitaiService>();
builder.Services.AddHttpClient<DanbooruService>();
builder.Services.AddDbContextFactory<AppDbContext>(opt => { opt.UseSqlite("Data Source=BlazorWebApp.db"); opt.EnableSensitiveDataLogging(); });

// Creates a singleton of the WS service to inject with DI and use it as the hosted service as well
builder.Services.AddSingleton<ComfyUIWebsocketService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ComfyUIWebsocketService>());
builder.Services.AddSingleton<ComfyUIEventBus>();

// Event aggregation service for typed events
builder.Services.AddSingleton<IEventService, EventService>();

// Settings management service
builder.Services.AddSingleton<ISettingsService, SettingsService>();

// State management service
builder.Services.AddSingleton<IStateDatabaseService>(sp => 
    new StateDatabaseServiceAdapter(sp.GetRequiredService<DatabaseService>()));
builder.Services.AddSingleton<IStateService, StateService>();

builder.Services.AddSingleton<ManagerService>();
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<IOService>();
builder.Services.AddSingleton<CsvService>();
builder.Services.AddSingleton<ProgressService>();
builder.Services.AddSingleton<ResourcesService>();
builder.Services.AddSingleton<RouterService>();
builder.Services.AddSingleton<WorkflowService>();
builder.Services.AddSingleton<DynamicPromptsService>();
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<ThemeService>();
// Asset resolution service for workflow models
builder.Services.AddScoped<IAssetResolverService, AssetResolverService>();

builder.Services.AddScoped<JavascriptService>();
builder.Services.AddScoped<OllamaService>();

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
