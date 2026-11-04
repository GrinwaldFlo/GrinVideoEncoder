using GrinVideoEncoder.Startup;

if (!await EncoderRequirements.EnsureMetAsync())
	return 1;

var appSettings = ConfigurationSelector.SelectAndLoadConfig(args);

var builder = WebApplication.CreateBuilder(args);
builder.AddApplicationServices(appSettings);

var app = builder.Build();
app.ConfigurePipeline();
await app.InitializeDatabaseAsync();
app.RegisterStartupBanner();

await app.RunAsync();
return 0;
