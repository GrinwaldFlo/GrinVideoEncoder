using GrinVideoEncoder.Data;
using GrinVideoEncoder.Services;
using Microsoft.EntityFrameworkCore;
using Radzen;

namespace GrinVideoEncoder.Startup;

public static class ServiceRegistration
{
	public static void AddApplicationServices(this WebApplicationBuilder builder, AppSettings appSettings)
	{
		builder.WebHost.UseUrls($"http://localhost:{appSettings.Port}");

		builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
		builder.Logging.AddFilter("Microsoft.AspNetCore.Watch", LogLevel.Warning);
		builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
		builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

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
		builder.Services.AddSingleton<MaintenanceService>();
		builder.Services.AddRadzenComponents();

		builder.Services.AddRazorComponents()
			.AddInteractiveServerComponents();
	}
}
