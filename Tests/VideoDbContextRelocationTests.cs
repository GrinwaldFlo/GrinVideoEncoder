using GrinVideoEncoder.Data;
using GrinVideoEncoder.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Uses a temporary database through the static <see cref="VideoDbContext.DbPath"/>, which no other test class sets.
/// </summary>
public class VideoDbContextRelocationTests : IDisposable
{
	private readonly string _tempDir;
	private readonly string _oldRoot;
	private readonly string _newRoot;

	public VideoDbContextRelocationTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "GrinTests_Relocation_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
		_oldRoot = Path.Combine(_tempDir, "old");
		_newRoot = Path.Combine(_tempDir, "new");

		VideoDbContext.SetPath(Path.Combine(_tempDir, "test.db"));
		using var context = new VideoDbContext();
		context.Database.Migrate();
	}

	public void Dispose()
	{
		SqliteConnection.ClearAllPools();
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);
		GC.SuppressFinalize(this);
	}

	private static async Task AddVideosAsync(params VideoFile[] videos)
	{
		await using var context = new VideoDbContext();
		context.VideoFiles.AddRange(videos);
		await context.SaveChangesAsync();
	}

	private static async Task<Dictionary<string, string>> GetDirectoriesByFilenameAsync()
	{
		await using var context = new VideoDbContext();
		return await context.VideoFiles.ToDictionaryAsync(v => v.Filename, v => v.DirectoryPath);
	}

	[Fact]
	public async Task RelocateDirectoriesAsync_MovesVideosInsideOldRootOnly()
	{
		string outside = Path.Combine(_tempDir, "old2");
		await AddVideosAsync(
			new VideoFile { DirectoryPath = _oldRoot, Filename = "root.mp4" },
			new VideoFile { DirectoryPath = Path.Combine(_oldRoot, "a", "b"), Filename = "deep.mp4" },
			new VideoFile { DirectoryPath = Path.Combine(_oldRoot, "a", "b"), Filename = "deep2.mp4" },
			new VideoFile { DirectoryPath = outside, Filename = "outside.mp4" });

		int updated = await VideoDbContext.RelocateDirectoriesAsync(_oldRoot, _newRoot);

		var directories = await GetDirectoriesByFilenameAsync();
		Assert.Equal(3, updated);
		Assert.Equal(_newRoot, directories["root.mp4"]);
		Assert.Equal(Path.Combine(_newRoot, "a", "b"), directories["deep.mp4"]);
		Assert.Equal(Path.Combine(_newRoot, "a", "b"), directories["deep2.mp4"]);
		Assert.Equal(outside, directories["outside.mp4"]);
	}

	[Fact]
	public async Task RelocateDirectoriesAsync_OldRootWithTrailingSeparator_MovesVideos()
	{
		await AddVideosAsync(new VideoFile { DirectoryPath = Path.Combine(_oldRoot, "a"), Filename = "v.mp4" });

		int updated = await VideoDbContext.RelocateDirectoriesAsync(_oldRoot + Path.DirectorySeparatorChar, _newRoot + Path.DirectorySeparatorChar);

		Assert.Equal(1, updated);
		Assert.Equal(Path.Combine(_newRoot, "a"), (await GetDirectoriesByFilenameAsync())["v.mp4"]);
	}

	[Fact]
	public async Task RelocateDirectoriesAsync_WindowsPaths_MovesVideosIgnoringCase()
	{
		await AddVideosAsync(
			new VideoFile { DirectoryPath = @"V:\Series\Kimmy\S1", Filename = "upper.mp4" },
			new VideoFile { DirectoryPath = @"v:\series\Kimmy\S1", Filename = "lower.mp4" },
			new VideoFile { DirectoryPath = @"V:\Movies", Filename = "outside.mp4" });

		int updated = await VideoDbContext.RelocateDirectoriesAsync(@"V:\Series", _newRoot);

		var directories = await GetDirectoriesByFilenameAsync();
		Assert.Equal(2, updated);
		Assert.Equal(Path.Combine(_newRoot, "Kimmy", "S1"), directories["upper.mp4"]);
		Assert.Equal(Path.Combine(_newRoot, "Kimmy", "S1"), directories["lower.mp4"]);
		Assert.Equal(@"V:\Movies", directories["outside.mp4"]);
	}

	[Fact]
	public async Task GetRandomVideosAsync_LeavesOutRemovedAndLimitsCount()
	{
		await AddVideosAsync(
			new VideoFile { DirectoryPath = _oldRoot, Filename = "1.mp4" },
			new VideoFile { DirectoryPath = _oldRoot, Filename = "2.mp4" },
			new VideoFile { DirectoryPath = _oldRoot, Filename = "3.mp4" },
			new VideoFile { DirectoryPath = _oldRoot, Filename = "removed.mp4", Status = CompressionStatus.Removed });

		var all = await VideoDbContext.GetRandomVideosAsync(10);
		var limited = await VideoDbContext.GetRandomVideosAsync(2);

		Assert.Equal(3, all.Count);
		Assert.DoesNotContain(all, v => v.Filename == "removed.mp4");
		Assert.Equal(2, limited.Count);
	}
}
