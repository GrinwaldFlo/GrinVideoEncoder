using GrinVideoEncoder;
using GrinVideoEncoder.Components;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Radzen;

var builder = WebApplication.CreateBuilder(args);
var appSettings = builder.Configuration.GetSection("Settings").Get<AppSettings>() ?? throw new Exception("Failed to load Application Settings");

VideoDbContext.SetPath(appSettings.DatabasePath);

builder.Services.AddSingleton<IAppSettings>(appSettings);
builder.Services.AddSingleton<LogMain>();
builder.Services.AddSingleton<LogFfmpeg>();

builder.Services.AddHostedService<MainBackgroundService>();
builder.Services.AddHostedService<VideoIndexerService>();
builder.Services.AddHostedService<PreventSleep>();
builder.Services.AddHostedService<VideoReencodeService>();
builder.Services.AddTransient<VideoProcessorService>();
builder.Services.AddSingleton<CommunicationService>();
builder.Services.AddRadzenComponents();

builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
	var videoProcessor = scope.ServiceProvider.GetRequiredService<VideoProcessorService>();
	await videoProcessor.FfmpegDownload();

    var logMain = scope.ServiceProvider.GetRequiredService<LogMain>();
    await VideoDbContext.CheckpointWalAsync(logMain);
}

StaticWebAssetsLoader.UseStaticWebAssets(app.Environment, app.Configuration);

await app.RunAsync();
