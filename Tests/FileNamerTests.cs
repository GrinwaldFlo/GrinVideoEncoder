using GrinVideoEncoder;
using GrinVideoEncoder.Utils;

namespace Tests;

public class FileNamerTests : IDisposable
{
	private readonly string _tempDir;
	private readonly AppSettings _settings;

	public FileNamerTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "GrinTests_FileNamer_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);

		_settings = new AppSettings { ConfigName = "TestConfig" };
		_settings.InitializePaths();

		// Override WorkPath to use temp dir via reflection or just create subdirs in temp
		// Since AppSettings paths are derived from WorkPath, we'll create matching directories
		Directory.CreateDirectory(_settings.FailedPath);
		Directory.CreateDirectory(_settings.ProcessingPath);
		Directory.CreateDirectory(_settings.OutputPath);
		Directory.CreateDirectory(_settings.TempPath);
		Directory.CreateDirectory(_settings.TrashPath);
		Directory.CreateDirectory(Path.Combine(_settings.LogPath, "Stats"));
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);

		// Clean up the settings directories
		if (Directory.Exists(_settings.WorkPath))
			Directory.Delete(_settings.WorkPath, true);

		GC.SuppressFinalize(this);
	}

	[Fact]
	public void NewFileName_AppendsRecodedSuffix()
	{
		string inputPath = Path.Combine(_tempDir, "myvideo.avi");
		File.WriteAllText(inputPath, "dummy");

		var namer = new FileNamer(_settings, inputPath);

		Assert.Equal("myvideo_recoded.mp4", namer.NewFileName);
	}

	[Fact]
	public void InputPath_MatchesOriginal()
	{
		string inputPath = Path.Combine(_tempDir, "test.mp4");
		File.WriteAllText(inputPath, "dummy");

		var namer = new FileNamer(_settings, inputPath);

		Assert.Equal(inputPath, namer.InputPath);
	}

	[Fact]
	public void FailedPath_ContainsOriginalFilename()
	{
		string inputPath = Path.Combine(_tempDir, "movie.mkv");
		File.WriteAllText(inputPath, "dummy");

		var namer = new FileNamer(_settings, inputPath);

		Assert.Contains("movie.mkv", namer.FailedPath);
		Assert.StartsWith(_settings.FailedPath, namer.FailedPath);
	}

	[Fact]
	public void ProcessingPath_ContainsOriginalFilename()
	{
		string inputPath = Path.Combine(_tempDir, "clip.mp4");
		File.WriteAllText(inputPath, "dummy");

		var namer = new FileNamer(_settings, inputPath);

		Assert.Contains("clip.mp4", namer.ProcessingPath);
		Assert.StartsWith(_settings.ProcessingPath, namer.ProcessingPath);
	}

	[Fact]
	public void OutputPath_ContainsRecodedName()
	{
		string inputPath = Path.Combine(_tempDir, "source.avi");
		File.WriteAllText(inputPath, "dummy");

		var namer = new FileNamer(_settings, inputPath);

		Assert.Contains("source_recoded.mp4", namer.OutputPath);
		Assert.StartsWith(_settings.OutputPath, namer.OutputPath);
	}

	[Fact]
	public void TempPath_ContainsOriginalFilename()
	{
		string inputPath = Path.Combine(_tempDir, "raw.mp4");
		File.WriteAllText(inputPath, "dummy");

		var namer = new FileNamer(_settings, inputPath);

		Assert.Contains("raw.mp4", namer.TempPath);
		Assert.StartsWith(_settings.TempPath, namer.TempPath);
	}

	[Fact]
	public void TempFirstPassPath_ContainsFirstPassSuffix()
	{
		string inputPath = Path.Combine(_tempDir, "video.mp4");
		File.WriteAllText(inputPath, "dummy");

		var namer = new FileNamer(_settings, inputPath);

		Assert.Contains("video_firstPass.mp4", namer.TempFirstPassPath);
	}

	[Fact]
	public void TrashPath_ContainsOriginalFilename()
	{
		string inputPath = Path.Combine(_tempDir, "old.mkv");
		File.WriteAllText(inputPath, "dummy");

		var namer = new FileNamer(_settings, inputPath);

		Assert.Contains("old.mkv", namer.TrashPath);
		Assert.StartsWith(_settings.TrashPath, namer.TrashPath);
	}

	[Fact]
	public void StatFileName_ContainsStatLogSuffix()
	{
		string inputPath = Path.Combine(_tempDir, "encode.mp4");
		File.WriteAllText(inputPath, "dummy");

		var namer = new FileNamer(_settings, inputPath);

		Assert.Contains("encode_stat.log", namer.StatFileName);
	}

	[Fact]
	public void NewFilePrefix_IsCorrectConstant()
	{
		Assert.Equal("_recoded.mp4", FileNamer.NEW_FILE_PREFIX);
	}
}
