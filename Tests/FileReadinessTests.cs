using GrinVideoEncoder.Services;
using GrinVideoEncoder.Utils;

namespace Tests;

public class FileReadinessTests : IDisposable
{
	private static readonly TimeSpan _pollDelay = TimeSpan.FromMilliseconds(100);
	private readonly string _tempDir;

	public FileReadinessTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "GrinTests_FileReadiness_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void FileSnapshot_MissingFile_ReturnsNull()
	{
		Assert.Null(FileSnapshot.TryRead(Path.Combine(_tempDir, "missing.mp4")));
	}

	[Fact]
	public void FileSnapshot_ChangesWhenFileGrows()
	{
		string filePath = Path.Combine(_tempDir, "growing.mp4");
		File.WriteAllText(filePath, "a");
		var before = FileSnapshot.TryRead(filePath);

		File.AppendAllText(filePath, "b");

		Assert.NotEqual(before, FileSnapshot.TryRead(filePath));
	}

	[Fact]
	public async Task WaitForFile_StableFile_ReturnsTrue()
	{
		string filePath = Path.Combine(_tempDir, "stable.mp4");
		File.WriteAllText(filePath, "content");

		Assert.True(await VideoProcessorService.WaitForFile(filePath, _pollDelay, CancellationToken.None));
	}

	[Fact]
	public async Task WaitForFile_MissingFile_ReturnsFalse()
	{
		Assert.False(await VideoProcessorService.WaitForFile(Path.Combine(_tempDir, "missing.mp4"), _pollDelay, CancellationToken.None));
	}

	[Fact]
	public async Task WaitForFile_FileStillWritten_WaitsUntilWritingStops()
	{
		string filePath = Path.Combine(_tempDir, "copying.mp4");
		File.WriteAllText(filePath, "start");

		// The writer holds no lock that blocks readers, like a copy on Linux
		var writer = Task.Run(async () =>
		{
			for (int i = 0; i < 30; i++)
			{
				await File.AppendAllTextAsync(filePath, new string('x', 100));
				await Task.Delay(_pollDelay / 10);
			}
		});

		Assert.True(await VideoProcessorService.WaitForFile(filePath, _pollDelay, CancellationToken.None));
		Assert.True(writer.IsCompleted);
		Assert.Equal(5 + (30 * 100), new FileInfo(filePath).Length);
	}

	[Fact]
	public async Task WaitForFile_Canceled_ReturnsFalseOrThrowsCanceled()
	{
		string filePath = Path.Combine(_tempDir, "locked.mp4");
		File.WriteAllText(filePath, "content");
		using var writer = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); // never ready while locked
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

		try
		{
			Assert.False(await VideoProcessorService.WaitForFile(filePath, _pollDelay, cts.Token));
		}
		catch (OperationCanceledException)
		{
			// Task.Delay throws when canceled during the wait, which is also a correct end
		}
	}

	/// <summary>
	/// An empty file must not block the drop folder forever: once nothing writes to it, encoding fails and it goes to Failed.
	/// </summary>
	[Fact]
	public async Task WaitForFile_StableEmptyFile_ReturnsTrue()
	{
		string filePath = Path.Combine(_tempDir, "empty.mp4");
		File.WriteAllText(filePath, string.Empty);
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		Assert.True(await VideoProcessorService.WaitForFile(filePath, _pollDelay, cts.Token));
	}
}
