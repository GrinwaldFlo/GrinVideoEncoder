var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration(builder.Configuration)
	.CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddHostedService<MainBackgroundService>();
builder.Services.AddTransient<VideoProcessorService>();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
}

// Create required folders
var folders = builder.Configuration.GetSection("Folders");
foreach (var folder in folders.GetChildren())
{
	Directory.CreateDirectory(folder.Value!);
}

app.UseStaticFiles();
app.MapRazorPages()
	.WithStaticAssets();
await app.RunAsync();