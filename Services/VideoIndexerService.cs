using System.Linq;
using System.Linq.Expressions;
using GrinVideoEncoder.Data;
using GrinVideoEncoder.Models;
using GrinVideoEncoder.Utils;
using Microsoft.EntityFrameworkCore;
using Xabe.FFmpeg;

namespace GrinVideoEncoder.Services;

public class VideoIndexerService : BackgroundService
{
	private readonly IAppSettings _settings;
	private readonly LogMain _log;
	private readonly CommunicationService _communication;
	private readonly string _dbPath;

	public VideoIndexerService(IAppSettings settings, LogMain log, CommunicationService communication)
	{
		_settings = settings;
		_log = log;
		_communication = communication;
		_dbPath = _settings.DatabasePath;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_communication.AskReIndex = true;
		await InitializeDatabase();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				if (_communication.AskReIndex)
				{
					_communication.AskReIndex = false;
					await RunIndexing(stoppingToken);
				}

				await Task.Delay(1000, stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}
	}

	internal async Task RunIndexing(CancellationToken stoppingToken)
	{
		if (string.IsNullOrEmpty(_settings.IndexerPath))
		{
			_log.Warning("IndexerPath is not configured. Please update settings.");
			return;
		}

		if (!Directory.Exists(_settings.IndexerPath))
		{
			_log.Warning("IndexerPath '{IndexerPath}' does not exist. Video indexer service will not run.", _settings.IndexerPath);
			return;
		}

		await CleanIgnored(stoppingToken);
		await IndexExistingFiles(stoppingToken);
		await UpdateMissingStatus(stoppingToken);
		await UpdateMissingBitDepth(stoppingToken);
	}

	/// <summary>
	/// Get all video in the database where <see cref="VideoFile.Sanity"/> is <see cref="SanityStatus.Unknown"/>
	/// Use the function <see cref="MediaInfoExtensions.VideoIsSane(Xabe.FFmpeg.IMediaInfo)"/> to check if the video is sane.
	/// If <see cref="VideoFile.Status"/> is: <see cref="CompressionStatus.Compressed"/>
	/// => Use the flag <see cref="SanityStatus.EncodedKo"/> or <see cref="SanityStatus.EncodedKo"/>
	/// => Otherwize use the flag <see cref="SanityStatus.OriginalOk"/> or <see cref="SanityStatus.OriginalKo"/>
	/// </summary>
	/// <param name="stoppingToken"></param>
	/// <returns></returns>
	private Task UpdateMissingStatus(CancellationToken stoppingToken) =>
		UpdateMissingMediaInfo("sanity status", v => v.Sanity == SanityStatus.Unknown, (videoFile, mediaInfo) =>
		{
			bool isSane = mediaInfo?.VideoIsSane() is true;
			videoFile.Sanity = videoFile.Status == CompressionStatus.Compressed
				? (isSane ? SanityStatus.EncodedOk : SanityStatus.EncodedKo)
				: (isSane ? SanityStatus.OriginalOk : SanityStatus.OriginalKo);
		}, stoppingToken);

	/// <summary>
	/// Reads the bit depth of the videos indexed before it was stored.
	/// </summary>
	private Task UpdateMissingBitDepth(CancellationToken stoppingToken) =>
		UpdateMissingMediaInfo("bit depth", v => v.BitDepth == null,
			(videoFile, mediaInfo) => videoFile.BitDepth = mediaInfo?.GetBitDepth() ?? VideoFile.BIT_DEPTH_UNKNOWN,
			stoppingToken);

	/// <summary>
	/// Reads the media info of every video matching <paramref name="filter"/> and lets <paramref name="update"/> fill the missing value.
	/// </summary>
	/// <param name="what">Name of the value, for the log.</param>
	private async Task UpdateMissingMediaInfo(string what, Expression<Func<VideoFile, bool>> filter,
		Action<VideoFile, IMediaInfo?> update, CancellationToken stoppingToken)
	{
		await using var context = new VideoDbContext();

		var files = await context.VideoFiles
			.Where(filter)
			.ToListAsync(stoppingToken);

		if (files.Count == 0)
			return;

		_log.Information("Updating {What} for {Count} files", what, files.Count);

		uint cnt = 0;
		foreach (var videoFile in files)
		{
			if (stoppingToken.IsCancellationRequested)
				break;

			try
			{
				var mediaInfo = await VideoProcessorService.GetMediaInfo(videoFile.FullPath, stoppingToken);
				update(videoFile, mediaInfo);
			}
			catch (Exception ex)
			{
				_log.Error(ex, "Failed to update {What} for {FilePath}", what, videoFile.FullPath);
			}

			if (cnt++ % 200 == 0)
			{
				await context.SaveChangesAsync(stoppingToken);
				_log.Information("Updating {What} {Count}/{Total} files", what, cnt, files.Count);
			}
		}

		await context.SaveChangesAsync(stoppingToken);
		_log.Information("{What} update complete", what);
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
			if (fileRemoved > 0)
				_log.Information("{Count} files removed from database because it containts ignore directory {Directory}", fileRemoved, folder);
		}
		if (totalRemoved > 0)
			_log.Information("{Count} files removed from database because it is ignored in directory list", totalRemoved);
	}

	private async Task InitializeDatabase()
	{
		await using var context = new VideoDbContext();
		await context.Database.EnsureCreatedAsync();
		await VideoDbContext.EnableWalModeAsync();
		_log.Information("Video indexer database initialized at {DatabasePath}", _dbPath);
	}

	private HashSet<string> _filesInDb = [];

	private async Task LoadCurrentFilesHashSetAsync()
	{
		await using var context = new VideoDbContext();
		_filesInDb = await context.VideoFiles.Select(v => v.FullPath.ToLower()).ToHashSetAsync();
	}

	private async Task IndexExistingFiles(CancellationToken stoppingToken)
	{
		_log.Information("Scanning for existing video files in {IndexerPath}", _settings.IndexerPath);

		var directories = new Stack<string>();
		directories.Push(_settings.IndexerPath);
		int indexedCount = 0;
		var foundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		// Their videos were not seen, which does not mean they were deleted
		var unreadDirectories = new List<string>();
		await LoadCurrentFilesHashSetAsync();

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
				unreadDirectories.Add(currentDir);
				_log.Warning("Skipping directory due to access denied: {DirectoryPath}", currentDir);
			}
			catch (Exception ex)
			{
				unreadDirectories.Add(currentDir);
				_log.Error(ex, "Error scanning directory: {DirectoryPath}", currentDir);
			}
		}

		_log.Information("Initial indexing complete. Processed {Count} eligible files.", indexedCount);

		// Clean up database entries for files that no longer exist
		if (!stoppingToken.IsCancellationRequested)
			await CleanMissingFiles(foundFiles, unreadDirectories, stoppingToken);

		// Checkpoint WAL after scanning to commit all indexed files to the main database
		await VideoDbContext.CheckpointWalAsync(_log);
	}

	/// <summary>
	/// Removes the videos of the indexed folder that the scan did not find.
	/// </summary>
	/// <param name="unreadDirectories">Folders the scan could not read: their videos are kept.</param>
	private async Task CleanMissingFiles(HashSet<string> foundFiles, List<string> unreadDirectories, CancellationToken stoppingToken)
	{
		await using var context = new VideoDbContext();

		// A text prefix is not enough: /videos2 starts like /videos but is not inside it
		var dbFiles = await context.VideoFiles
			.Select(v => new { v.Id, v.DirectoryPath, v.Filename })
			.ToListAsync(stoppingToken);

		var missingFileIds = dbFiles
			.Where(f => GlobUtils.IsInsideDirectory(_settings.IndexerPath, f.DirectoryPath)
				&& !unreadDirectories.Any(dir => GlobUtils.IsInsideDirectory(dir, f.DirectoryPath))
				&& !foundFiles.Contains(Path.Combine(f.DirectoryPath, f.Filename)))
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
		return !_settings.IgnoreFolders.Contains(dir.Name, StringComparer.OrdinalIgnoreCase);
	}

	private async Task IndexFile(string filePath)
	{
		try
		{
			if (_filesInDb.Contains(filePath.ToLower()))
				return;

			var fileInfo = new FileInfo(filePath);
			if (!fileInfo.Exists)
				return;

			await using var context = new VideoDbContext();

			var existingFile = await context.VideoFiles
				.FirstOrDefaultAsync(v => v.DirectoryPath == fileInfo.DirectoryName && v.Filename == fileInfo.Name);

			if (existingFile != null)
				return;

			// Detect moved file: same filename and size but at a different path where the old file no longer exists
			var movedFile = await DetectMovedFile(context, fileInfo);
			if (movedFile != null)
			{
				string oldPath = movedFile.FullPath;
				movedFile.DirectoryPath = fileInfo.DirectoryName ?? string.Empty;
				movedFile.LastModified = fileInfo.LastWriteTimeUtc;
				await context.SaveChangesAsync();

				_filesInDb.Remove(oldPath.ToLower());
				_filesInDb.Add(filePath.ToLower());

				_log.Information("Detected moved video: {Filename} from {OldPath} to {NewPath}",
					fileInfo.Name, oldPath, movedFile.FullPath);
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
				Fps = videoStream?.Framerate,
				BitDepth = mediaInfo?.GetBitDepth() ?? VideoFile.BIT_DEPTH_UNKNOWN,
				Sanity = mediaInfo?.VideoIsSane() is true ? SanityStatus.OriginalOk : SanityStatus.OriginalKo
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

	/// <summary>
	/// Detects if a file was moved from another directory by looking for an existing database entry
	/// with the same filename and file size where the original file no longer exists on disk.
	/// Returns the matched entry if exactly one candidate is found, otherwise null.
	/// </summary>
	private static async Task<VideoFile?> DetectMovedFile(VideoDbContext context, FileInfo fileInfo)
	{
		var candidates = await context.VideoFiles
			.Where(v => v.Filename == fileInfo.Name
				&& (v.FileSizeOriginal == fileInfo.Length || v.FileSizeCompressed == fileInfo.Length)
				&& v.DirectoryPath != fileInfo.DirectoryName)
			.ToListAsync();

		if (candidates.Count == 0)
			return null;

		// Keep only candidates whose original file no longer exists on disk
		var movedCandidates = candidates
			.Where(v => !File.Exists(v.FullPath))
			.ToList();

		// Only treat as a move if there is exactly one unambiguous match
		return movedCandidates.Count == 1 ? movedCandidates[0] : null;
	}

	private bool IsEligibleVideoFile(string filePath)
	{
		try
		{
			string extension = Path.GetExtension(filePath).ToLowerInvariant();
			if (!_settings.VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
				return false;
			var fileInfo = new FileInfo(filePath);
			return fileInfo.Exists;
		}
		catch
		{
			return false;
		}
	}
}