using GrinVideoEncoder.Models;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Data;

public class VideoIndexerDbContext : DbContext
{
	private readonly string _dbPath;

	public VideoIndexerDbContext(string dbPath)
	{
		_dbPath = dbPath;
	}

	public DbSet<VideoFile> VideoFiles { get; set; } = null!;

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.UseSqlite($"Data Source={_dbPath}");
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<VideoFile>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.DirectoryPath, e.Filename }).IsUnique();
			entity.Property(e => e.DirectoryPath).IsRequired().HasMaxLength(1024);
			entity.Property(e => e.Filename).IsRequired().HasMaxLength(256);
			entity.Ignore(e => e.FullPath);
		});
	}
}
