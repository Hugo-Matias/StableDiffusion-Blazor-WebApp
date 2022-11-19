using BlazorWebApp.Data;
using BlazorWebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient<SDAPIService>();
builder.Services.AddScoped<JSConsole>();
builder.Services.AddSingleton<IOService>();
builder.Services.AddSingleton<PromptButtonService>();
builder.Services.AddSingleton<RandomService>();
builder.Services.AddSingleton<ImageState>();
builder.Services.AddSingleton<AppState>();

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
