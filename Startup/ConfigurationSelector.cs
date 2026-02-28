using GrinVideoEncoder.Data;
using GrinVideoEncoder.Services;

namespace GrinVideoEncoder.Startup;

public static class ConfigurationSelector
{
	public static AppSettings SelectAndLoadConfig(string[] args)
	{
		string? configName = GetConfigFromArgs(args);
		var availableConfigs = ConfigManager.GetAvailableConfigs();

		configName ??= PromptForConfig(availableConfigs);

		if (configName is null)
			Environment.Exit(1);

		var appSettings = LoadOrCreateConfig(configName, availableConfigs);

		ConfigManager.EnsureDirectories(appSettings);
		VideoDbContext.SetPath(appSettings.DatabasePath);
		EnsureValidPort(appSettings);

		return appSettings;
	}

	private static string? GetConfigFromArgs(string[] args)
	{
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] == "--config" && i + 1 < args.Length)
				return args[i + 1];
		}

		return null;
	}

	private static string? PromptForConfig(List<string> availableConfigs)
	{
		if (availableConfigs.Count == 0)
		{
			Console.Write("No configurations found. Enter a name for the new configuration: ");
			string? name = Console.ReadLine()?.Trim();
			if (string.IsNullOrWhiteSpace(name))
			{
				Console.WriteLine("Configuration name cannot be empty.");
				return null;
			}

			return name;
		}

		Console.WriteLine("Available configurations:");
		for (int i = 0; i < availableConfigs.Count; i++)
			Console.WriteLine($"  [{i + 1}] {availableConfigs[i]}");

		Console.WriteLine($"  [0] Create new configuration");
		Console.Write("Select a configuration: ");
		string? input = Console.ReadLine()?.Trim();

		if (int.TryParse(input, out int choice) && choice >= 0 && choice <= availableConfigs.Count)
		{
			if (choice == 0)
			{
				Console.Write("Enter a name for the new configuration: ");
				string? name = Console.ReadLine()?.Trim();
				if (string.IsNullOrWhiteSpace(name))
				{
					Console.WriteLine("Configuration name cannot be empty.");
					return null;
				}

				return name;
			}

			return availableConfigs[choice - 1];
		}

		Console.WriteLine("Invalid selection.");
		return null;
	}

	private static AppSettings LoadOrCreateConfig(string configName, List<string> availableConfigs)
	{
		if (availableConfigs.Contains(configName, StringComparer.OrdinalIgnoreCase))
		{
			var settings = ConfigManager.LoadConfig(configName);
			Console.WriteLine($"Loaded configuration: {configName}");
			return settings;
		}

		var newSettings = ConfigManager.CreateConfig(configName);
		Console.WriteLine($"Created new configuration: {configName}");
		return newSettings;
	}

	private static void EnsureValidPort(AppSettings settings)
	{
		const int defaultPort = 14563;
		if (settings.Port < 1 || settings.Port > 65535)
		{
			Console.WriteLine($"Port {settings.Port} is out of range (1-65535). Using default port {defaultPort}.");
			settings.Port = defaultPort;
		}
	}
}
