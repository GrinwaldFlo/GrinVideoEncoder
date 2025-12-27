using GrinVideoEncoder.Data;
using GrinVideoEncoder.Models;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Services;

public class VideoIndexerService : BackgroundService
{
	private readonly IAppSettings _settings;
	private readonly VideoProcessorService _videoProcessor;
	private readonly string _dbPath;
	private FileSystemWatcher? _watcher;

	private long MinFileSizeBytes => _settings.MinFileSizeMB * 1024L * 1024L;

	public VideoIndexerService(IAppSettings settings, VideoProcessorService videoProcessor)
	{
		_settings = settings;
		_videoProcessor = videoProcessor;
		_dbPath = _settings.DatabasePath;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (string.IsNullOrEmpty(_settings.IndexerPath))
		{
			Log.Warning("IndexerPath is not configured. Video indexer service will not run.");
			return;
		}

		if (!Directory.Exists(_settings.IndexerPath))
		{
			Log.Warning("IndexerPath '{IndexerPath}' does not exist. Video indexer service will not run.", _settings.IndexerPath);
			return;
		}

		await InitializeDatabase();
		await IndexExistingFiles(stoppingToken);
		StartFileWatcher();

		Log.Information("Video indexer watching for changes in {IndexerPath}", _settings.IndexerPath);

		while (!stoppingToken.IsCancellationRequested)
		{
			await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
		}

		_watcher?.Dispose();
	}

	private async Task InitializeDatabase()
	{
		await using var context = new VideoIndexerDbContext(_dbPath);
		await context.Database.EnsureCreatedAsync();
		Log.Information("Video indexer database initialized at {DatabasePath}", _dbPath);
	}

	private async Task IndexExistingFiles(CancellationToken stoppingToken)
	{
		Log.Information("Scanning for existing video files in {IndexerPath}", _settings.IndexerPath);

		var directories = new Stack<string>();
		directories.Push(_settings.IndexerPath);
		var indexedCount = 0;

		while (directories.Count > 0)
		{
			if (stoppingToken.IsCancellationRequested)
				break;

			var currentDir = directories.Pop();

			try
			{
				foreach (var filePath in Directory.EnumerateFiles(currentDir))
				{
					if (stoppingToken.IsCancellationRequested)
						break;

					if (IsEligibleVideoFile(filePath))
					{
						await IndexFile(filePath);
						indexedCount++;
					}
				}

				foreach (var subDir in Directory.EnumerateDirectories(currentDir))
				{
					directories.Push(subDir);
				}
			}
			catch (UnauthorizedAccessException)
			{
				Log.Warning("Skipping directory due to access denied: {DirectoryPath}", currentDir);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error scanning directory: {DirectoryPath}", currentDir);
			}
		}

		Log.Information("Initial indexing complete. Processed {Count} eligible files.", indexedCount);
	}

	private void StartFileWatcher()
	{
		_watcher = new FileSystemWatcher(_settings.IndexerPath)
		{
			EnableRaisingEvents = true,
			IncludeSubdirectories = true,
			NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite
		};

		_watcher.Created += async (sender, e) => await OnFileCreated(e.FullPath);
		_watcher.Changed += async (sender, e) => await OnFileChanged(e.FullPath);
		_watcher.Deleted += async (sender, e) => await OnFileDeleted(e.FullPath);
		_watcher.Renamed += async (sender, e) => await OnFileRenamed(e.OldFullPath, e.FullPath);
	}

	private async Task OnFileCreated(string filePath)
	{
		if (!IsEligibleVideoFile(filePath))
			return;

		await Task.Delay(1000); // Wait for file to be fully written
		await IndexFile(filePath);
	}

	private async Task OnFileChanged(string filePath)
	{
		if (!IsEligibleVideoFile(filePath))
			return;

		await UpdateFileIndex(filePath);
	}

	private async Task OnFileDeleted(string filePath)
	{
		await RemoveFromIndex(filePath);
	}

	private async Task OnFileRenamed(string oldPath, string newPath)
	{
		await RemoveFromIndex(oldPath);

		if (IsEligibleVideoFile(newPath))
		{
			await IndexFile(newPath);
		}
	}

	private async Task IndexFile(string filePath)
	{
		try
		{
			var fileInfo = new FileInfo(filePath);
			if (!fileInfo.Exists)
				return;

			await using var context = new VideoIndexerDbContext(_dbPath);

			var existingFile = await context.VideoFiles
				.FirstOrDefaultAsync(v => v.DirectoryPath == fileInfo.DirectoryName && v.Filename == fileInfo.Name);

			if (existingFile != null)
			{
				Log.Debug("File already indexed: {FilePath}", filePath);
				return;
			}

			var duration = await VideoProcessorService.GetVideoDuration(filePath);

			var videoFile = new VideoFile
			{
				DirectoryPath = fileInfo.DirectoryName ?? string.Empty,
				Filename = fileInfo.Name,
				FileSizeOriginal = fileInfo.Length,
				FileSizeCompressed = null,
				DurationSeconds = (long?)duration?.TotalSeconds,
				LastModified = fileInfo.LastWriteTimeUtc
			};

			context.VideoFiles.Add(videoFile);
			await context.SaveChangesAsync();

			Log.Information("Indexed video: {Filename} ({Size:F2} MB, {Duration})",
				fileInfo.Name,
				fileInfo.Length / (1024.0 * 1024.0),
				duration);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to index file: {FilePath}", filePath);
		}
	}

	private async Task UpdateFileIndex(string filePath)
	{
		try
		{
			var fileInfo = new FileInfo(filePath);
			if (!fileInfo.Exists)
				return;

			await using var context = new VideoIndexerDbContext(_dbPath);

			var existingFile = await context.VideoFiles
				.FirstOrDefaultAsync(v => v.DirectoryPath == fileInfo.DirectoryName && v.Filename == fileInfo.Name);

			if (existingFile == null)
			{
				await IndexFile(filePath);
				return;
			}

			if (existingFile.LastModified >= fileInfo.LastWriteTimeUtc)
				return;

			var duration = await VideoProcessorService.GetVideoDuration(filePath);

			existingFile.FileSizeOriginal = fileInfo.Length;
			existingFile.DurationSeconds = (long?)duration?.TotalSeconds;
			existingFile.LastModified = fileInfo.LastWriteTimeUtc;
			existingFile.IndexedAt = DateTime.UtcNow;

			await context.SaveChangesAsync();

			Log.Information("Updated index for: {Filename}", fileInfo.Name);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to update index for file: {FilePath}", filePath);
		}
	}

	private async Task RemoveFromIndex(string filePath)
	{
		try
		{
			var fileInfo = new FileInfo(filePath);

			await using var context = new VideoIndexerDbContext(_dbPath);

			var existingFile = await context.VideoFiles
				.FirstOrDefaultAsync(v => v.DirectoryPath == fileInfo.DirectoryName && v.Filename == fileInfo.Name);

			if (existingFile == null)
				return;

			context.VideoFiles.Remove(existingFile);
			await context.SaveChangesAsync();

			Log.Information("Removed from index: {Filename}", fileInfo.Name);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to remove file from index: {FilePath}", filePath);
		}
	}

	private bool IsEligibleVideoFile(string filePath)
	{
		try
		{
			var extension = Path.GetExtension(filePath).ToLowerInvariant();
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
