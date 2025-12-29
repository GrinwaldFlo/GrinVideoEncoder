using GrinVideoEncoder;
using GrinVideoEncoder.Data; // Add this using directive
using Microsoft.AspNetCore.Hosting.Server; // Add this using directive
using Microsoft.AspNetCore.Hosting.Server.Features;

var builder = WebApplication.CreateBuilder(args);
var appSettings = builder.Configuration.GetSection("Settings").Get<AppSettings>() ?? throw new Exception("Failed to load Application Settings");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.File(Path.Combine(appSettings.LogPath, "GrinVideoEncoder.log"), rollingInterval: RollingInterval.Day)
    .WriteTo.Console()
    .CreateLogger();

var ffmpegLogger = new LoggerConfiguration()
	.MinimumLevel.Information()
	.WriteTo.File(Path.Combine(appSettings.LogPath, "Ffmpeg.log"), rollingInterval: RollingInterval.Day)
	.CreateLogger();

builder.Services.AddSingleton<Serilog.ILogger>(ffmpegLogger);

builder.Host.UseSerilog();

Log.Information("Starting application");

builder.Services.AddSingleton<IAppSettings>(appSettings);

// Add services
builder.Services.AddScoped<VideoIndexerDbContext>(provider => 
{
    var settings = provider.GetRequiredService<IAppSettings>();
    return new VideoIndexerDbContext(settings.DatabasePath);
});

builder.Services.AddHostedService<MainBackgroundService>();
builder.Services.AddHostedService<VideoIndexerService>();
builder.Services.AddHostedService<VideoReencodeService>();
builder.Services.AddTransient<VideoProcessorService>();
builder.Services.AddSingleton<CommunicationService>();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.MapRazorPages()
	.WithStaticAssets();

app.Lifetime.ApplicationStarted.Register(() =>
{
	var server = app.Services.GetRequiredService<IServer>(); // Get the IServer instance
	var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses; // Use the IServer instance to get the addresses
	if (addresses != null)
	{
		foreach (string address in addresses)
		{
			Log.Information("Application is listening on address: {Address}", address);
		}
	}
});

using (var scope = app.Services.CreateScope())
{
	var videoProcessor = scope.ServiceProvider.GetRequiredService<VideoProcessorService>();
	await videoProcessor.FfmpegDownload();
}

// Checkpoint WAL at startup to commit any pending changes from previous runs
await VideoIndexerDbContext.CheckpointWalAsync(appSettings.DatabasePath);

await app.RunAsync();
