using System.Linq;
using GrinVideoEncoder.Data;
using GrinVideoEncoder.Models;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Services;

public class VideoIndexerService : BackgroundService
{
	private readonly IAppSettings _settings;
	private readonly LogMain _log;
	private readonly string _dbPath;

	private long MinFileSizeBytes => _settings.MinFileSizeMB * 1024L * 1024L;

	public VideoIndexerService(IAppSettings settings, LogMain log)
	{
		_settings = settings;
		_log = log;
		_dbPath = _settings.DatabasePath;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (string.IsNullOrEmpty(_settings.IndexerPath))
		{
			_log.Warning("IndexerPath is not configured. Video indexer service will not run.");
			return;
		}

		if (!Directory.Exists(_settings.IndexerPath))
		{
			_log.Warning("IndexerPath '{IndexerPath}' does not exist. Video indexer service will not run.", _settings.IndexerPath);
			return;
		}

		await InitializeDatabase();
		await CleanIgnored(stoppingToken);
		await IndexExistingFiles(stoppingToken);

		while (!stoppingToken.IsCancellationRequested)
		{
			await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
		}
	}

	private async Task CleanIgnored(CancellationToken stoppingToken)
	{
		if (_settings.IgnoreFolders is null || _settings.IgnoreFolders.Count == 0)
			return;

		await using var context = new VideoDbContext();

		// Create a copy to avoid modification issues during iteration
		var ignoreFolders = _settings.IgnoreFolders.ToList();
		int totalRemoved = 0;

		foreach (string? folder in ignoreFolders)
		{
			if (string.IsNullOrEmpty(folder) || folder.Length < 3)
			{
				_log.Warning("Ignore directory {Folder} is not valide", folder);
				continue;
			}

			string containsPattern = $"{Path.DirectorySeparatorChar}{folder}{Path.DirectorySeparatorChar}";
			string endsWithPattern = $"{Path.DirectorySeparatorChar}{folder}";

			int fileRemoved = await context.VideoFiles
				.Where(v => v.DirectoryPath.Contains(containsPattern) || v.DirectoryPath.EndsWith(endsWithPattern))
				.ExecuteDeleteAsync(stoppingToken);
			totalRemoved += fileRemoved;
			if(fileRemoved > 0)
				_log.Information("{Count} files removed from database because it containts ignore directory {Directory}", fileRemoved, folder);
		}
		if(totalRemoved > 0)
			_log.Information("{Count} files removed from database because it is ignored in directory list", totalRemoved);
	}

	private async Task InitializeDatabase()
	{
		await using var context = new VideoDbContext();
		await context.Database.EnsureCreatedAsync();
		await VideoDbContext.EnableWalModeAsync();
		_log.Information("Video indexer database initialized at {DatabasePath}", _dbPath);
	}

	private async Task IndexExistingFiles(CancellationToken stoppingToken)
	{
		_log.Information("Scanning for existing video files in {IndexerPath}", _settings.IndexerPath);

		var directories = new Stack<string>();
		directories.Push(_settings.IndexerPath);
		int indexedCount = 0;
		var foundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		while (directories.Count > 0)
		{
			if (stoppingToken.IsCancellationRequested)
				break;

			string currentDir = directories.Pop();
			try
			{
				foreach (string filePath in Directory.EnumerateFiles(currentDir))
				{
					if (stoppingToken.IsCancellationRequested)
						break;

					if (IsEligibleVideoFile(filePath))
					{
						foundFiles.Add(filePath);
						await IndexFile(filePath);
						indexedCount++;
					}
				}

				foreach (string? subDir in Directory.EnumerateDirectories(currentDir).Where(IsEligibleFolder))
				{
					directories.Push(subDir);
				}
			}
			catch (UnauthorizedAccessException)
			{
				_log.Warning("Skipping directory due to access denied: {DirectoryPath}", currentDir);
			}
			catch (Exception ex)
			{
				_log.Error(ex, "Error scanning directory: {DirectoryPath}", currentDir);
			}
		}

		_log.Information("Initial indexing complete. Processed {Count} eligible files.", indexedCount);

		// Clean up database entries for files that no longer exist
		if (!stoppingToken.IsCancellationRequested)
			await CleanMissingFiles(foundFiles, stoppingToken);

		// Checkpoint WAL after scanning to commit all indexed files to the main database
		await VideoDbContext.CheckpointWalAsync( _log);
	}

	private async Task CleanMissingFiles(HashSet<string> foundFiles, CancellationToken stoppingToken)
	{
		await using var context = new VideoDbContext();

		var dbFiles = await context.VideoFiles
			.Where(v => v.DirectoryPath.StartsWith(_settings.IndexerPath))
			.Select(v => new { v.Id, FullPath = v.DirectoryPath + Path.DirectorySeparatorChar + v.Filename })
			.ToListAsync(stoppingToken);

		var missingFileIds = dbFiles
			.Where(f => !foundFiles.Contains(f.FullPath))
			.Select(f => f.Id)
			.ToList();

		if (missingFileIds.Count > 0)
		{
			int removed = await context.VideoFiles
				.Where(v => missingFileIds.Contains(v.Id))
				.ExecuteDeleteAsync(stoppingToken);

			_log.Information("Removed {Count} database entries for files that no longer exist", removed);
		}
	}

	private bool IsEligibleFolder(string fullpath)
	{
		DirectoryInfo dir = new(fullpath);
		if (_settings.IgnoreFolders.Contains(dir.Name, StringComparer.OrdinalIgnoreCase))
			return false;
		return true;
	}

	private async Task IndexFile(string filePath)
	{
		try
		{
			var fileInfo = new FileInfo(filePath);
			if (!fileInfo.Exists)
				return;

			await using var context = new VideoDbContext();

			var existingFile = await context.VideoFiles
				.FirstOrDefaultAsync(v => v.DirectoryPath == fileInfo.DirectoryName && v.Filename == fileInfo.Name);

			if (existingFile != null)
			{
				return;
			}

			var mediaInfo = await VideoProcessorService.GetMediaInfo(filePath);
			var videoStream = mediaInfo?.VideoStreams.FirstOrDefault();

			var videoFile = new VideoFile
			{
				DirectoryPath = fileInfo.DirectoryName ?? string.Empty,
				Filename = fileInfo.Name,
				FileSizeOriginal = fileInfo.Length,
				FileSizeCompressed = null,
				DurationSeconds = (long?)mediaInfo?.Duration.TotalSeconds,
				Width = videoStream?.Width,
				Height = videoStream?.Height,
				LastModified = fileInfo.LastWriteTimeUtc,
				Fps =videoStream?.Framerate
			};

			context.VideoFiles.Add(videoFile);
			await context.SaveChangesAsync();

			_log.Information("Indexed video: {Filename} ({Size:F2} MB, {Duration}, {Width}x{Height}, {Framerate} FPS)",
				fileInfo.Name,
				fileInfo.Length / (1024.0 * 1024.0),
				mediaInfo?.Duration ?? new TimeSpan(),
				videoStream?.Width ?? 0,
				videoStream?.Height ?? 0,
				videoStream?.Framerate ?? 0);
		}
		catch (Exception ex)
		{
			_log.Error(ex, "Failed to index file: {FilePath}", filePath);
		}
	}

	private async Task UpdateFileIndex(string filePath)
	{
		try
		{
			var fileInfo = new FileInfo(filePath);
			if (!fileInfo.Exists)
				return;

			await using var context = new VideoDbContext();

			var existingFile = await context.VideoFiles
				.FirstOrDefaultAsync(v => v.DirectoryPath == fileInfo.DirectoryName && v.Filename == fileInfo.Name);

			if (existingFile == null)
			{
				await IndexFile(filePath);
				return;
			}

			if (existingFile.LastModified >= fileInfo.LastWriteTimeUtc)
				return;

			var mediaInfo = await VideoProcessorService.GetMediaInfo(filePath);
			var videoStream = mediaInfo?.VideoStreams.FirstOrDefault();

			existingFile.FileSizeOriginal = fileInfo.Length;
			existingFile.DurationSeconds = (long?)mediaInfo?.Duration.TotalSeconds;
			existingFile.Width = videoStream?.Width;
			existingFile.Height = videoStream?.Height;
			existingFile.LastModified = fileInfo.LastWriteTimeUtc;
			existingFile.IndexedAt = DateTime.UtcNow;

			await context.SaveChangesAsync();

			_log.Information("Updated index for: {Filename}", fileInfo.Name);
		}
		catch (Exception ex)
		{
			_log.Error(ex, "Failed to update index for file: {FilePath}", filePath);
		}
	}

	private bool IsEligibleVideoFile(string filePath)
	{
		try
		{
			string extension = Path.GetExtension(filePath).ToLowerInvariant();
			if (!_settings.VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
				return false;

			var fileInfo = new FileInfo(filePath);
			return fileInfo.Exists && fileInfo.Length >= MinFileSizeBytes;
		}
		catch
		{
			return false;
		}
	}
}
