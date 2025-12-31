using GrinVideoEncoder.Models;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Data;

public class VideoIndexerDbContext(string dbPath) : DbContext
{
	public DbSet<VideoFile> VideoFiles { get; set; } = null!;

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.UseSqlite($"Data Source={dbPath}");
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

	/// <summary>
	/// Checkpoints the WAL file to commit all pending changes to the main database file.
	/// This reduces the WAL file size and ensures data durability.
	/// </summary>
	public static async Task CheckpointWalAsync(string dbPath, LogMain log)
	{
		await using var context = new VideoIndexerDbContext(dbPath);
		await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");
		log.Information("SQLite WAL checkpoint completed for {DatabasePath}", dbPath);
	}

	public async Task<List<VideoFile>> GetVideosWithHighQualityRatioAsync(double threshold)
	{
		return await VideoFiles
			.Where(v => v.Status == CompressionStatus.Original)
			.AsAsyncEnumerable()
			.Where(v => v.QualityRatioOriginal.HasValue && v.QualityRatioOriginal > threshold)
			.ToListAsync();
	}
}
