using System.Diagnostics;
using GrinVideoEncoder.Components;
using GrinVideoEncoder.Data;
using GrinVideoEncoder.Services;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Startup;

public static class PipelineConfiguration
{
	public static void ConfigurePipeline(this WebApplication app)
	{
		if (!app.Environment.IsDevelopment())
		{
			app.UseExceptionHandler("/Error", createScopeForErrors: true);
		}

		app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
		app.UseAntiforgery();

		app.MapStaticAssets();
		app.MapRazorComponents<App>()
			.AddInteractiveServerRenderMode();
	}

	public static async Task InitializeDatabaseAsync(this WebApplication app)
	{
		using var scope = app.Services.CreateScope();

		var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();
		await db.Database.MigrateAsync();

		var videoProcessor = scope.ServiceProvider.GetRequiredService<VideoProcessorService>();
		await videoProcessor.EnsureFfmpegReady();

		var logMain = scope.ServiceProvider.GetRequiredService<LogMain>();
		await VideoDbContext.EnableWalModeAsync();
		await VideoDbContext.CheckpointWalAsync(logMain);
	}

	/// <summary>
	/// Asks for the new location of the indexed folder if it was moved, and saves it.
	/// </summary>
	/// <returns>False if the folder is missing and no new location was given, so the application must close.</returns>
	public static async Task<bool> EnsureIndexerPathAsync(this WebApplication app)
	{
		var settings = app.Services.GetRequiredService<AppSettings>();
		var check = new IndexerPathCheck(Console.In, Console.Out, Directory.Exists, File.Exists,
			VideoDbContext.GetRandomVideosAsync, VideoDbContext.RelocateDirectoriesAsync);

		string? indexerPath = await check.RunAsync(settings.IndexerPath);
		if (indexerPath is null)
			return false;

		if (indexerPath != settings.IndexerPath)
		{
			settings.IndexerPath = indexerPath;
			ConfigManager.SaveConfig(settings);
		}

		return true;
	}

	public static void RegisterStartupBanner(this WebApplication app)
	{
		StaticWebAssetsLoader.UseStaticWebAssets(app.Environment, app.Configuration);

		app.Lifetime.ApplicationStarted.Register(() =>
		{
			var urls = app.Urls;

			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine();
			Console.WriteLine("  ╔═══════════════════════════════════╗");
			Console.WriteLine("  ║                                   ║");
			Console.WriteLine("  ║    ██████  ██████  ██ ███    ██   ║");
			Console.WriteLine("  ║   ██       ██   ██ ██ ████   ██   ║");
			Console.WriteLine("  ║   ██   ███ ██████  ██ ██ ██  ██   ║");
			Console.WriteLine("  ║   ██    ██ ██   ██ ██ ██  ██ ██   ║");
			Console.WriteLine("  ║    ██████  ██   ██ ██ ██   ████   ║");
			Console.WriteLine("  ║                                   ║");
			Console.WriteLine("  ║       V I D E O   E N C O D E R   ║");
			Console.WriteLine("  ║                                   ║");
			Console.WriteLine("  ╚═══════════════════════════════════╝");
			Console.ResetColor();
			Console.WriteLine();

			foreach (var url in urls)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.Write(" -> Listening on: ");
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine(url);
			}

			Console.ResetColor();
			Console.WriteLine();

			var firstUrl = urls.FirstOrDefault();
			if (firstUrl is not null)
			{
				try
				{
					Process.Start(new ProcessStartInfo(firstUrl) { UseShellExecute = true });
				}
				catch (Exception ex)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"Failed to open browser for URL '{firstUrl}': {ex.Message}");
					Console.ResetColor();
				}
			}
		});
	}
}
