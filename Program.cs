using GrinVideoEncoder;
using GrinVideoEncoder.Components;
using GrinVideoEncoder.Data;
using GrinVideoEncoder.Services;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;
using Radzen;

// --- Configuration selection ---
string? configName = null;

for (int i = 0; i < args.Length; i++)
{
	if (args[i] == "--config" && i + 1 < args.Length)
	{
		configName = args[i + 1];
		break;
	}
}

var availableConfigs = ConfigManager.GetAvailableConfigs();

if (configName is null)
{
	if (availableConfigs.Count == 0)
	{
		Console.Write("No configurations found. Enter a name for the new configuration: ");
		configName = Console.ReadLine()?.Trim();
		if (string.IsNullOrWhiteSpace(configName))
		{
			Console.WriteLine("Configuration name cannot be empty.");
			return;
		}
	}
	else
	{
		Console.WriteLine("Available configurations:");
		for (int i = 0; i < availableConfigs.Count; i++)
			Console.WriteLine($"  [{i + 1}] {availableConfigs[i]}");

		Console.WriteLine($"  [0] Create new configuration");
		Console.Write("Select a configuration: ");
		var input = Console.ReadLine()?.Trim();

		if (int.TryParse(input, out int choice) && choice >= 0 && choice <= availableConfigs.Count)
		{
			if (choice == 0)
			{
				Console.Write("Enter a name for the new configuration: ");
				configName = Console.ReadLine()?.Trim();
				if (string.IsNullOrWhiteSpace(configName))
				{
					Console.WriteLine("Configuration name cannot be empty.");
					return;
				}
			}
			else
			{
				configName = availableConfigs[choice - 1];
			}
		}
		else
		{
			Console.WriteLine("Invalid selection.");
			return;
		}
	}
}

// Load or create the configuration
AppSettings appSettings;
if (availableConfigs.Contains(configName, StringComparer.OrdinalIgnoreCase))
{
	appSettings = ConfigManager.LoadConfig(configName);
	Console.WriteLine($"Loaded configuration: {configName}");
}
else
{
	appSettings = ConfigManager.CreateConfig(configName);
	Console.WriteLine($"Created new configuration: {configName}");
}

ConfigManager.EnsureDirectories(appSettings);
VideoDbContext.SetPath(appSettings.DatabasePath);

// --- Build the web application ---
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<VideoDbContext>(options =>
	options.UseSqlite($"Data Source={appSettings.DatabasePath};Cache=Shared"));

builder.Services.AddSingleton<IAppSettings>(appSettings);
builder.Services.AddSingleton(appSettings);
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
	var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
	await db.Database.MigrateAsync();

	var videoProcessor = scope.ServiceProvider.GetRequiredService<VideoProcessorService>();
	await videoProcessor.FfmpegDownload();

	var logMain = scope.ServiceProvider.GetRequiredService<LogMain>();
	await VideoDbContext.EnableWalModeAsync();
	await VideoDbContext.CheckpointWalAsync(logMain);
}

StaticWebAssetsLoader.UseStaticWebAssets(app.Environment, app.Configuration);

await app.RunAsync();
