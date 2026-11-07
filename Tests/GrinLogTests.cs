using GrinVideoEncoder;
using GrinVideoEncoder.Services;

namespace Tests;

public class GrinLogTests : IDisposable
{
	private readonly string _tempDir;
	private readonly LogMain _log;

	public GrinLogTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "GrinTests_Log_" + Guid.NewGuid().ToString("N"));
		_log = new LogMain(new AppSettings { WorkPath = _tempDir });
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Text of the line, without the date prefix.
	/// </summary>
	private string LastText() => _log.LastLine.Value[(_log.LastLine.Value.IndexOf(" - ", StringComparison.Ordinal) + 3)..];

	[Fact]
	public void Information_ReplacesNamedPlaceholders()
	{
		_log.Information("Indexed {Filename} in {Folder}", "a.mp4", "/videos");

		Assert.Equal("Indexed a.mp4 in /videos", LastText());
	}

	[Fact]
	public void Information_PlaceholderWithFormat_KeepsTheFormat()
	{
		_log.Information("Size {Size:F2} MB", 1.5);

		Assert.Equal($"Size {1.5:F2} MB", LastText());
	}

	[Fact]
	public void Information_NoArguments_KeepsTheTextAsIs()
	{
		_log.Information("{\"json\": true}");

		Assert.Equal("{\"json\": true}", LastText());
	}

	[Fact]
	public void Information_ArgumentContainingBraces_IsKept()
	{
		_log.Information("File {Path}", "/videos/{2020}/a.mp4");

		Assert.Equal("File /videos/{2020}/a.mp4", LastText());
	}

	/// <summary>
	/// Serilog accepts a template with more placeholders than arguments, so the history must not throw either.
	/// </summary>
	[Fact]
	public void Information_MissingArgument_DoesNotThrow()
	{
		_log.Information("{First} and {Second}", "one");

		Assert.Contains("one", LastText());
	}

	[Fact]
	public void Error_WithException_PrefixesTheMessage()
	{
		_log.Error(new InvalidOperationException("boom"), "Failed {File}", "a.mp4");

		Assert.Equal("boom Failed a.mp4", LastText());
	}

	[Fact]
	public void History_KeepsTheLast5000Lines()
	{
		for (int i = 0; i < 5010; i++)
			_log.Information("Line {Number}", i);

		var history = _log.GetHistory();
		Assert.Equal(5000, history.Count);
		Assert.EndsWith("Line 10", history[0]);
		Assert.EndsWith("Line 5009", history[^1]);
	}

	/// <summary>
	/// The background services and FFmpeg write at the same time, while the UI reads the history.
	/// </summary>
	[Fact]
	public async Task History_ConcurrentWritesAndReads_LoseNothing()
	{
		const int writers = 8;
		const int linesPerWriter = 500;

		var reader = Task.Run(() =>
		{
			for (int i = 0; i < 200; i++)
				_ = _log.GetHistory().Count;
		});
		await Task.WhenAll(Enumerable.Range(0, writers)
			.Select(w => Task.Run(() =>
			{
				for (int i = 0; i < linesPerWriter; i++)
					_log.Information("Writer {Writer} line {Line}", w, i);
			}))
			.Append(reader));

		Assert.Equal(writers * linesPerWriter, _log.GetHistory().Count);
	}
}
