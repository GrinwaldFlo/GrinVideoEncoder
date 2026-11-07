using GrinVideoEncoder;
using GrinVideoEncoder.Data;
using GrinVideoEncoder.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// Test classes using the static <see cref="VideoDbContext.DbPath"/> must not run in parallel.
/// </summary>
[CollectionDefinition(NAME, DisableParallelization = true)]
public class DatabaseCollection
{
	public const string NAME = "Database";
}

/// <summary>
/// Temporary folder with a migrated database set as <see cref="VideoDbContext.DbPath"/>, and settings whose work folders are inside it.
/// </summary>
public abstract class TestDatabase : IDisposable
{
	protected TestDatabase(string name)
	{
		TempDir = Path.Combine(Path.GetTempPath(), $"GrinTests_{name}_{Guid.NewGuid():N}");
		Directory.CreateDirectory(TempDir);

		Settings = new AppSettings { ConfigName = "Test", WorkPath = Path.Combine(TempDir, "work") };

		VideoDbContext.SetPath(Path.Combine(TempDir, "test.db"));
		using var context = new VideoDbContext();
		context.Database.Migrate();
	}

	protected string TempDir { get; }
	protected AppSettings Settings { get; }

	public void Dispose()
	{
		SqliteConnection.ClearAllPools();
		if (Directory.Exists(TempDir))
			Directory.Delete(TempDir, true);
		GC.SuppressFinalize(this);
	}

	protected static async Task AddVideosAsync(params VideoFile[] videos)
	{
		await using var context = new VideoDbContext();
		context.VideoFiles.AddRange(videos);
		await context.SaveChangesAsync();
	}

	protected static async Task<List<VideoFile>> GetAllVideosAsync()
	{
		await using var context = new VideoDbContext();
		return await context.VideoFiles.AsNoTracking().ToListAsync();
	}

	protected static async Task<VideoFile> GetVideoAsync(Guid id)
	{
		await using var context = new VideoDbContext();
		return await context.VideoFiles.AsNoTracking().SingleAsync(v => v.Id == id);
	}
}
