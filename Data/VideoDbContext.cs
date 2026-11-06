using System;
using GrinVideoEncoder.Models;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Data;

/// <summary>
/// Use :
/// dotnet ef migrations add <MigrationName> --output-dir Data/Migrations
/// </summary>
public class VideoDbContext : DbContext
{
	public VideoDbContext() { }
	public VideoDbContext(DbContextOptions<VideoDbContext> options) : base(options) { }

	public static string DbPath { get; private set; } = string.Empty;
	public DbSet<VideoFile> VideoFiles { get; set; } = null!;

	/// <summary>
	/// Checkpoints the WAL file to commit all pending changes to the main database file.
	/// This reduces the WAL file size and ensures data durability.
	/// </summary>
	public static async Task CheckpointWalAsync(LogMain log)
	{
		await using var context = new VideoDbContext();
		await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");
		log.Information("SQLite WAL checkpoint completed for {DatabasePath}", DbPath);
	}

	public static async Task<int> CountVideosWithStatusAsync(CompressionStatus status)
	{
		await using var context = new VideoDbContext();
		return await context.VideoFiles
			.AsNoTracking()
			.CountAsync(v => v.Status == status);
	}

	public static async Task<List<VideoFile>> GetVideosWithStatusAsync(CompressionStatus status)
	{
		await using var context = new VideoDbContext();
		return await context.VideoFiles
			.Where(v => v.Status == status)
			.AsNoTracking()
			.ToListAsync();
	}

	public static async Task<int> CountVideosAsync()
	{
		await using var context = new VideoDbContext();
		return await context.VideoFiles.CountAsync();
	}

	/// <param name="includeHighBitDepth">False to leave out the videos above 8 bits, see <see cref="VideoFile.IsHighBitDepth"/>.</param>
	public async Task<List<VideoFile>> GetVideosWithHighQualityRatioAsync(double threshold, double minSizeByte, DateTime maxLastModifiedTime, bool includeHighBitDepth)
	{
		return await VideoFiles
			.Where(v => v.Status == CompressionStatus.Original)
			.AsNoTracking()
			.AsAsyncEnumerable()
			.Where(v => v.QualityRatioOriginal.HasValue && v.QualityRatioOriginal > threshold && v.FileSizeOriginal > minSizeByte && v.LastModified < maxLastModifiedTime
				&& (includeHighBitDepth || !v.IsHighBitDepth))
			.ToListAsync();
	}

	internal static async Task ResetErrorAsync(Guid id)
	{
		await using var context = new VideoDbContext();
		var videoToReset = await context.VideoFiles.FirstOrDefaultAsync(x => x.Id == id);
		if (videoToReset != null)
		{
			videoToReset.Status = CompressionStatus.Original;
			await context.SaveChangesAsync();
		}
	}

	internal static async Task MarkKept(Guid id)
	{
		await using var context = new VideoDbContext();
		var videoToReset = await context.VideoFiles.FirstOrDefaultAsync(x => x.Id == id);
		if (videoToReset != null)
		{
			videoToReset.Status = CompressionStatus.Kept;
			await context.SaveChangesAsync();
		}
	}

	/// <summary>
	/// Picks random indexed videos, leaving out the ones marked <see cref="CompressionStatus.Removed"/>.
	/// </summary>
	internal static async Task<List<VideoFile>> GetRandomVideosAsync(int count)
	{
		await using var context = new VideoDbContext();
		return await context.VideoFiles
			.AsNoTracking()
			.Where(v => v.Status != CompressionStatus.Removed)
			.OrderBy(_ => EF.Functions.Random())
			.Take(count)
			.ToListAsync();
	}

	/// <summary>
	/// Moves every video inside <paramref name="oldRoot"/> to the same relative folder in <paramref name="newRoot"/>.
	/// Videos outside <paramref name="oldRoot"/> are left as they are.
	/// </summary>
	/// <returns>Number of videos updated.</returns>
	internal static async Task<int> RelocateDirectoriesAsync(string oldRoot, string newRoot)
	{
		await using var context = new VideoDbContext();
		var directories = await context.VideoFiles
			.Select(v => v.DirectoryPath)
			.Distinct()
			.ToListAsync();

		await using var transaction = await context.Database.BeginTransactionAsync();
		int updated = 0;
		foreach (string directory in directories)
		{
			string? newDirectory = GlobUtils.RebaseDirectory(oldRoot, newRoot, directory);
			if (newDirectory is null || newDirectory == directory)
				continue;

			updated += await context.VideoFiles
				.Where(v => v.DirectoryPath == directory)
				.ExecuteUpdateAsync(s => s.SetProperty(v => v.DirectoryPath, newDirectory));
		}

		await transaction.CommitAsync();
		return updated;
	}

	internal static void SetPath(string databasePath)
	{
		DbPath = databasePath;
	}

	internal static async Task<int> SetStatusAsync(IEnumerable<Guid> ids, CompressionStatus newStatus)
	{
		await using var context = new VideoDbContext();
		var videoToReset = await context.VideoFiles.Where(x => ids.Contains(x.Id)).ToListAsync();

		videoToReset.ForEach(x => x.Status = newStatus);
		return await context.SaveChangesAsync();
	}

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		if (!optionsBuilder.IsConfigured)
			optionsBuilder.UseSqlite($"Data Source={DbPath};Cache=Shared");
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<VideoFile>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.DirectoryPath).IsRequired().HasMaxLength(1024);
			entity.Property(e => e.Filename).IsRequired().HasMaxLength(256);
			entity.Ignore(e => e.FullPath);
		});
	}

	internal async Task<VideoFile?> PopNextVideoToProcess()
	{
		var video = await VideoFiles.FirstOrDefaultAsync(x => x.Status == CompressionStatus.ToProcess);
		if (video != null)
		{
			video.Status = CompressionStatus.Processing;
			await SaveChangesAsync();
		}
		return video;
	}

	public static async Task EnableWalModeAsync()
	{
		await using var context = new VideoDbContext();
		await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
	}
}