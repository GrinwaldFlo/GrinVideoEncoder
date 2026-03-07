using GrinVideoEncoder;

namespace Tests;

public class AppSettingsTests
{
	[Fact]
	public void InitializePaths_SetsWorkPath()
	{
		var settings = new AppSettings { ConfigName = "TestConfig" };

		settings.InitializePaths();

		Assert.False(string.IsNullOrEmpty(settings.WorkPath));
		Assert.Contains("TestConfig", settings.WorkPath);
	}

	[Fact]
	public void InputPath_AfterInitialize_EndsWithInput()
	{
		var settings = new AppSettings { ConfigName = "TestConfig" };
		settings.InitializePaths();

		Assert.EndsWith("Input", settings.InputPath);
	}

	[Fact]
	public void TrashPath_AfterInitialize_EndsWithTrash()
	{
		var settings = new AppSettings { ConfigName = "TestConfig" };
		settings.InitializePaths();

		Assert.EndsWith("Trash", settings.TrashPath);
	}

	[Fact]
	public void ProcessingPath_AfterInitialize_EndsWithProcessing()
	{
		var settings = new AppSettings { ConfigName = "TestConfig" };
		settings.InitializePaths();

		Assert.EndsWith("Processing", settings.ProcessingPath);
	}

	[Fact]
	public void OutputPath_AfterInitialize_EndsWithOutput()
	{
		var settings = new AppSettings { ConfigName = "TestConfig" };
		settings.InitializePaths();

		Assert.EndsWith("Output", settings.OutputPath);
	}

	[Fact]
	public void FailedPath_AfterInitialize_EndsWithFailed()
	{
		var settings = new AppSettings { ConfigName = "TestConfig" };
		settings.InitializePaths();

		Assert.EndsWith("Failed", settings.FailedPath);
	}

	[Fact]
	public void TempPath_AfterInitialize_EndsWithTemp()
	{
		var settings = new AppSettings { ConfigName = "TestConfig" };
		settings.InitializePaths();

		Assert.EndsWith("Temp", settings.TempPath);
	}

	[Fact]
	public void LogPath_AfterInitialize_EndsWithLog()
	{
		var settings = new AppSettings { ConfigName = "TestConfig" };
		settings.InitializePaths();

		Assert.EndsWith("Log", settings.LogPath);
	}

	[Fact]
	public void ConfigFilePath_ContainsConfigNameAndJson()
	{
		var settings = new AppSettings { ConfigName = "MyConfig" };

		Assert.Contains("MyConfig", settings.ConfigFilePath);
		Assert.EndsWith("config.json", settings.ConfigFilePath);
	}

	[Fact]
	public void DatabasePath_ContainsConfigName()
	{
		var settings = new AppSettings { ConfigName = "MyConfig" };

		Assert.Contains("MyConfig", settings.DatabasePath);
		Assert.EndsWith("videoindex.db", settings.DatabasePath);
	}

	[Fact]
	public void GetRoamingRoot_ReturnsNonEmptyString()
	{
		string root = AppSettings.GetRoamingRoot();

		Assert.False(string.IsNullOrEmpty(root));
		Assert.Contains("GrinVideoEncoder", root);
	}

	[Fact]
	public void DefaultValues_AreCorrect()
	{
		var settings = new AppSettings();

		Assert.False(settings.ForceCpu);
		Assert.Equal(23, settings.QualityLevel);
		Assert.Equal(string.Empty, settings.IndexerPath);
		Assert.Equal(500, settings.EncodingThreshold);
		Assert.Equal(10, settings.MinFileSizeMb);
		Assert.Equal(1, settings.MinFileAgeH);
		Assert.Null(settings.VideosGridSettings);
	}

	[Fact]
	public void VideoExtensions_DefaultContainsCommonFormats()
	{
		var settings = new AppSettings();

		Assert.Contains(".mp4", settings.VideoExtensions);
		Assert.Contains(".mkv", settings.VideoExtensions);
		Assert.Contains(".avi", settings.VideoExtensions);
		Assert.Contains(".mov", settings.VideoExtensions);
		Assert.Contains(".wmv", settings.VideoExtensions);
		Assert.Contains(".flv", settings.VideoExtensions);
		Assert.Contains(".webm", settings.VideoExtensions);
	}

	[Fact]
	public void IgnoreFolders_DefaultIsEmpty()
	{
		var settings = new AppSettings();

		Assert.Empty(settings.IgnoreFolders);
	}

	[Fact]
	public void AllSubPaths_AreChildrenOfWorkPath()
	{
		var settings = new AppSettings { ConfigName = "TestConfig" };
		settings.InitializePaths();

		Assert.StartsWith(settings.WorkPath, settings.InputPath);
		Assert.StartsWith(settings.WorkPath, settings.TrashPath);
		Assert.StartsWith(settings.WorkPath, settings.ProcessingPath);
		Assert.StartsWith(settings.WorkPath, settings.OutputPath);
		Assert.StartsWith(settings.WorkPath, settings.FailedPath);
		Assert.StartsWith(settings.WorkPath, settings.TempPath);
		Assert.StartsWith(settings.WorkPath, settings.LogPath);
	}
}
