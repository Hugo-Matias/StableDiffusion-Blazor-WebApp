using BlazorWebApp.Data;
using BlazorWebApp.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var maxBufferSize = 100 * 1024 * 1024;

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddServerSideBlazor().AddHubOptions(opt => { opt.MaximumReceiveMessageSize = maxBufferSize; });

builder.Services.AddHttpClient<SDAPIService>();
builder.Services.AddDbContextFactory<AppDbContext>(opt => opt.UseSqlite("Data Source=BlazorWebApp.db"));

builder.Services.AddSingleton<AppState>();
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<PromptButtonService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<IOService>();

builder.Services.AddScoped<JavascriptService>();
builder.Services.AddScoped<RandomService>();

builder.Services.AddTransient<MagickService>();
builder.Services.AddTransient<ParsingService>();

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

app.UseRouting();

app.MapBlazorHub();

app.MapFallbackToPage("/_Host");

app.Run();
