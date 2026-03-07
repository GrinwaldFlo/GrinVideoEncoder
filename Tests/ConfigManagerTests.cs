using GrinVideoEncoder;
using GrinVideoEncoder.Services;

namespace Tests;

public class ConfigManagerTests : IDisposable
{
	private readonly List<string> _createdConfigs = [];

	public void Dispose()
	{
		foreach (var configName in _createdConfigs)
		{
			string roamingRoot = AppSettings.GetRoamingRoot();
			string configDir = Path.Combine(roamingRoot, configName);
			if (Directory.Exists(configDir))
				Directory.Delete(configDir, true);

			string localRoot = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"GrinVideoEncoder", configName);
			if (Directory.Exists(localRoot))
				Directory.Delete(localRoot, true);
		}
		GC.SuppressFinalize(this);
	}

	private string UniqueConfigName()
	{
		string name = $"Test_{Guid.NewGuid():N}";
		_createdConfigs.Add(name);
		return name;
	}

	[Fact]
	public void SaveConfig_CreatesFile()
	{
		var configName = UniqueConfigName();
		var settings = new AppSettings { ConfigName = configName, QualityLevel = 18 };
		settings.InitializePaths();

		ConfigManager.SaveConfig(settings);

		Assert.True(File.Exists(settings.ConfigFilePath));
	}

	[Fact]
	public void LoadConfig_ReturnsCorrectSettings()
	{
		var configName = UniqueConfigName();
		var indexerPath = Path.Combine(Path.GetTempPath(), "Videos");
		var original = new AppSettings
		{
			ConfigName = configName,
			QualityLevel = 20,
			ForceCpu = true,
			IndexerPath = indexerPath,
			EncodingThreshold = 500,
			MinFileSizeMb = 50,
			MinFileAgeH = 24
		};
		original.InitializePaths();
		ConfigManager.SaveConfig(original);

		var loaded = ConfigManager.LoadConfig(configName);

		Assert.Equal(configName, loaded.ConfigName);
		Assert.Equal(20, loaded.QualityLevel);
		Assert.True(loaded.ForceCpu);
		Assert.Equal(Path.GetFullPath(indexerPath), loaded.IndexerPath);
		Assert.Equal(500, loaded.EncodingThreshold);
		Assert.Equal(50, loaded.MinFileSizeMb);
		Assert.Equal(24, loaded.MinFileAgeH);
	}

	[Fact]
	public void LoadConfig_NonExistentConfig_ThrowsFileNotFoundException()
	{
		Assert.Throws<FileNotFoundException>(() => ConfigManager.LoadConfig("NonExistentConfig_12345"));
	}

	[Fact]
	public void CreateConfig_CreatesConfigFileAndDirectories()
	{
		var configName = UniqueConfigName();

		var settings = ConfigManager.CreateConfig(configName);

		Assert.Equal(configName, settings.ConfigName);
		Assert.True(File.Exists(settings.ConfigFilePath));
		Assert.True(Directory.Exists(settings.WorkPath));
		Assert.True(Directory.Exists(settings.InputPath));
		Assert.True(Directory.Exists(settings.OutputPath));
		Assert.True(Directory.Exists(settings.ProcessingPath));
		Assert.True(Directory.Exists(settings.FailedPath));
		Assert.True(Directory.Exists(settings.TempPath));
		Assert.True(Directory.Exists(settings.TrashPath));
		Assert.True(Directory.Exists(settings.LogPath));
	}

	[Fact]
	public void SaveConfig_ThenLoadConfig_Roundtrip()
	{
		var configName = UniqueConfigName();
		var original = new AppSettings
		{
			ConfigName = configName,
			QualityLevel = 28,
			VideoExtensions = [".mp4", ".avi"],
			IgnoreFolders = ["temp", ".cache"]
		};
		original.InitializePaths();
		ConfigManager.SaveConfig(original);

		var loaded = ConfigManager.LoadConfig(configName);

		Assert.Equal(original.QualityLevel, loaded.QualityLevel);
		Assert.Equal(original.VideoExtensions, loaded.VideoExtensions);
		Assert.Equal(original.IgnoreFolders, loaded.IgnoreFolders);
	}

	[Fact]
	public void SaveConfig_OverwritesExisting()
	{
		var configName = UniqueConfigName();
		var settings = new AppSettings { ConfigName = configName, QualityLevel = 18 };
		settings.InitializePaths();
		ConfigManager.SaveConfig(settings);

		settings.QualityLevel = 30;
		ConfigManager.SaveConfig(settings);

		var loaded = ConfigManager.LoadConfig(configName);
		Assert.Equal(30, loaded.QualityLevel);
	}

	[Fact]
	public void EnsureDirectories_CreatesAllRequired()
	{
		var configName = UniqueConfigName();
		var settings = new AppSettings { ConfigName = configName };
		settings.InitializePaths();

		ConfigManager.EnsureDirectories(settings);

		Assert.True(Directory.Exists(settings.WorkPath));
		Assert.True(Directory.Exists(settings.InputPath));
		Assert.True(Directory.Exists(settings.OutputPath));
		Assert.True(Directory.Exists(settings.ProcessingPath));
		Assert.True(Directory.Exists(settings.FailedPath));
		Assert.True(Directory.Exists(settings.TempPath));
		Assert.True(Directory.Exists(settings.TrashPath));
		Assert.True(Directory.Exists(settings.LogPath));
	}

	[Fact]
	public void LoadConfig_InitializesWorkPath()
	{
		var configName = UniqueConfigName();
		var settings = ConfigManager.CreateConfig(configName);

		var loaded = ConfigManager.LoadConfig(configName);

		Assert.False(string.IsNullOrEmpty(loaded.WorkPath));
		Assert.Contains(configName, loaded.WorkPath);
	}

	[Fact]
	public void GetAvailableConfigs_IncludesCreatedConfig()
	{
		var configName = UniqueConfigName();
		ConfigManager.CreateConfig(configName);

		var configs = ConfigManager.GetAvailableConfigs();

		Assert.Contains(configName, configs);
	}
}
