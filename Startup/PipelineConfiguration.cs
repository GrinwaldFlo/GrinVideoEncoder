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
		await videoProcessor.FfmpegDownload();

		var logMain = scope.ServiceProvider.GetRequiredService<LogMain>();
		await VideoDbContext.EnableWalModeAsync();
		await VideoDbContext.CheckpointWalAsync(logMain);
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
				Process.Start(new ProcessStartInfo(firstUrl) { UseShellExecute = true });
			}
		});
	}
}
