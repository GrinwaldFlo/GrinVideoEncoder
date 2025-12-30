using GrinVideoEncoder.Data;
using GrinVideoEncoder.Models;
using GrinVideoEncoder.Services;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Services;

public class VideoReencodeService : BackgroundService
{
	private readonly VideoProcessorService _videoProcessor;
	private readonly IAppSettings _settings;
	private readonly CommunicationService _communication;

	public VideoReencodeService(VideoProcessorService videoProcessor, IAppSettings settings, CommunicationService communication)
	{
		_videoProcessor = videoProcessor;
		_settings = settings;
		_communication = communication;
	}
	public const string MP4_EXT = ".mp4";
	private async Task ReencodeVideoAsync(VideoFile video)
	{
		if (_communication.VideoProcessToken.Token.IsCancellationRequested)
			return;
		// Step 1: Copy video to temp folder
		string tempDir = Path.GetFullPath(Path.Combine(_settings.ProcessingPath, Guid.NewGuid().ToString()));
		Directory.CreateDirectory(tempDir);
		string tempInputPath = Path.Combine(tempDir, video.Filename);
		File.Copy(video.FullPath, tempInputPath, true);

		// Step 2: Re-encode video in temp folder
		string tempOutputPath = Path.Combine(tempDir, "compressed_" + video.Filename);
		tempOutputPath = Path.ChangeExtension(tempOutputPath, MP4_EXT);

		bool success = false;
		try
		{
			await _videoProcessor.EncodeVideo(tempInputPath, tempOutputPath, _communication.VideoProcessToken.Token);
			success = true;
		}
		catch (TaskCanceledException)
		{
			Log.Warning("Encoding canceled", video);
			return;
		}
		catch (Exception ex)
		{
			Log.Error("Encoding failed", ex, video);
		}

		// Step 3: Check DurationSeconds and TotalPixels
		var originalInfo = await VideoProcessorService.GetMediaInfo(tempInputPath);
		var compressedInfo = await VideoProcessorService.GetMediaInfo(tempOutputPath);
		long? originalDuration = (long?)originalInfo?.Duration.TotalSeconds;
		long? compressedDuration = (long?)compressedInfo?.Duration.TotalSeconds;
		long? originalPixels = video.TotalPixels;
		long? compressedPixels = compressedInfo?.VideoStreams.FirstOrDefault()?.Width * compressedInfo?.VideoStreams.FirstOrDefault()?.Height;
		double? originalFps = originalInfo?.VideoStreams.FirstOrDefault()?.Framerate;
		double? compressedFps = compressedInfo?.VideoStreams.FirstOrDefault()?.Framerate;

		if (success && originalDuration != null && originalDuration == compressedDuration &&
			originalPixels != null && originalPixels == compressedPixels &&
			originalFps != null && compressedFps != null && Math.Abs(originalFps.Value - compressedFps.Value) < 0.1
			)
		{
			if (new FileInfo(tempInputPath).Length < new FileInfo(tempOutputPath).Length)
			{
				video.Status = CompressionStatus.Bigger;
				Log.Warning("New video is bigger than original {Video} {OriginalSize:F0} [MB] <  {NewSize:F0} [MB]", video.FullPath, new FileInfo(tempInputPath).Length / (1024*1024.0), new FileInfo(tempOutputPath).Length / (1024 * 1024.0));
			}
			else
			{
				string indexerPath = Path.GetFullPath(_settings.IndexerPath);
				string videoDirpath = Path.GetFullPath(video.DirectoryPath);
				string relativePath = videoDirpath[indexerPath.Length..].TrimStart(Path.DirectorySeparatorChar);

				// Step 4: Move original to trash (keep directory structure)
				string trashDir = Path.GetFullPath(Path.Combine(_settings.TrashPath, relativePath));
				Directory.CreateDirectory(trashDir);
				string trashPath = Path.Combine(trashDir, video.Filename);
				File.Move(tempInputPath, trashPath, true);
				File.Delete(video.FullPath);
				video.Filename = Path.ChangeExtension(video.Filename, MP4_EXT);
				// Step 5: Move new file to original location
				File.Move(tempOutputPath, video.FullPath, true);

				// Step 6: Update indexation data
				video.FileSizeCompressed = new FileInfo(video.FullPath).Length;
				video.Status = CompressionStatus.Compressed;
				Log.Information("New video successfully encoded {Video}, reduced of {CompressionFactor:F1}%", video.FullPath, video.CompressionFactor);
			}
		}
		else
		{
			video.Status = CompressionStatus.FailedToCompress;
		}

		Directory.Delete(tempDir, true);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await Task.Delay(5000, stoppingToken);
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

		while (!stoppingToken.IsCancellationRequested)
		{
			if (!_communication.Status.IsRunning && _communication.VideoToProcess.Count > 0)
			{
				await _communication.Status.SetIsRunningAsync(true);
				var nextId = _communication.VideoToProcess.Pop();
				await using var context = new VideoIndexerDbContext(_settings.DatabasePath);
				var video = await context.VideoFiles.FirstOrDefaultAsync(x => x.Id == nextId, cancellationToken: stoppingToken);
				if (video != null)
				{
					await _communication.Status.SetFilenameAsync(video.FullPath);
					await ReencodeVideoAsync(video);
				}
				await context.SaveChangesAsync();
				await _communication.Status.SetIsRunningAsync(false);

				if (_communication.VideoProcessToken.IsCancellationRequested)
				{
					_communication.VideoToProcess.Clear();
					Log.Information("Encoding canceled");
				}
			}

			Thread.Sleep(1000);
		}
	}

}
