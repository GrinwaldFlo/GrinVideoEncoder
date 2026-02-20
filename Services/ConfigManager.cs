using System.Text.Json;

namespace GrinVideoEncoder.Services;

public static class ConfigManager
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true
	};

	public static List<string> GetAvailableConfigs()
	{
		var roamingRoot = AppSettings.GetRoamingRoot();
		if (!Directory.Exists(roamingRoot))
			return [];

		return Directory.GetDirectories(roamingRoot)
			.Select(Path.GetFileName)
			.Where(name => !string.IsNullOrEmpty(name) && File.Exists(Path.Combine(roamingRoot, name!, "config.json")))
			.Cast<string>()
			.ToList();
	}

	public static AppSettings LoadConfig(string configName)
	{
		var roamingRoot = AppSettings.GetRoamingRoot();
		var configPath = Path.Combine(roamingRoot, configName, "config.json");

		if (!File.Exists(configPath))
			throw new FileNotFoundException($"Configuration '{configName}' not found at {configPath}");

		var json = File.ReadAllText(configPath);
		var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
			?? throw new InvalidOperationException($"Failed to deserialize configuration '{configName}'");

		settings.ConfigName = configName;
		settings.InitializePaths();
		return settings;
	}

	public static void SaveConfig(AppSettings settings)
	{
		var dir = Path.GetDirectoryName(settings.ConfigFilePath)!;
		Directory.CreateDirectory(dir);

		var json = JsonSerializer.Serialize(settings, JsonOptions);
		File.WriteAllText(settings.ConfigFilePath, json);
	}

	public static AppSettings CreateConfig(string configName)
	{
		var settings = new AppSettings { ConfigName = configName };
		settings.InitializePaths();

		SaveConfig(settings);
		EnsureDirectories(settings);

		return settings;
	}

	public static void EnsureDirectories(AppSettings settings)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(settings.ConfigFilePath)!);
		Directory.CreateDirectory(settings.WorkPath);
		Directory.CreateDirectory(settings.InputPath);
		Directory.CreateDirectory(settings.OutputPath);
		Directory.CreateDirectory(settings.ProcessingPath);
		Directory.CreateDirectory(settings.FailedPath);
		Directory.CreateDirectory(settings.TempPath);
		Directory.CreateDirectory(settings.TrashPath);
		Directory.CreateDirectory(settings.LogPath);
	}
}
