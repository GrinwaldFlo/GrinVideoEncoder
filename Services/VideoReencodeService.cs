using GrinVideoEncoder.Models;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Services;

public class VideoReencodeService(VideoProcessorService videoProcessor, IAppSettings settings, LogMain log, CommunicationService communication) : BackgroundService
{
	public const string MP4_EXT = ".mp4";

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await Task.Delay(5000, stoppingToken);
		if (string.IsNullOrEmpty(settings.IndexerPath))
		{
			log.Warning("IndexerPath is not configured. Video indexer service will not run.");
			return;
		}

		if (!Directory.Exists(settings.IndexerPath))
		{
			log.Warning("IndexerPath '{IndexerPath}' does not exist. Video indexer service will not run.", settings.IndexerPath);
			return;
		}

		while (!stoppingToken.IsCancellationRequested)
		{
			if (!communication.Status.IsRunning.Value && communication.VideoToProcess.Count > 0)
			{
				communication.Status.IsRunning.OnNext(true);
				var nextId = communication.VideoToProcess.Pop();
				await using var context = new VideoDbContext();
				var video = await context.VideoFiles.FirstOrDefaultAsync(x => x.Id == nextId, cancellationToken: stoppingToken);
				if (video != null)
				{
					communication.Status.Filename.OnNext(video.FullPath);
					await ReencodeVideoAsync(video);
				}
				await context.SaveChangesAsync(stoppingToken);
				communication.Status.IsRunning.OnNext(false);

				if (communication.VideoProcessToken.IsCancellationRequested)
				{
					communication.VideoToProcess.Clear();
					communication.Status.Status.OnNext("Task canceled");
					log.Information("Encoding canceled");
				}
			}

			Thread.Sleep(1000);
		}
	}

	private async Task ReencodeVideoAsync(VideoFile video)
	{
		if (communication.VideoProcessToken.Token.IsCancellationRequested)
			return;

		// Prevent system from sleeping during encoding
		PowerManagement.PreventSleep();

		try
		{
			// Step 1: Copy video to temp folder
			string tempDir = Path.GetFullPath(Path.Combine(settings.ProcessingPath, Guid.NewGuid().ToString()));
			Directory.CreateDirectory(tempDir);
			communication.Status.Status.OnNext("Copying...");
			string tempInputPath = Path.Combine(tempDir, video.Filename);
			File.Copy(video.FullPath, tempInputPath, true);

			// Step 2: Re-encode video in temp folder
			string tempOutputPath = Path.Combine(tempDir, "compressed_" + video.Filename);
			tempOutputPath = Path.ChangeExtension(tempOutputPath, MP4_EXT);
			communication.Status.Status.OnNext("Encoding...");
			bool success = false;
			try
			{
				await videoProcessor.EncodeVideo(tempInputPath, tempOutputPath, communication.VideoProcessToken.Token);
				success = true;
			}
			catch (TaskCanceledException)
			{
				log.Warning("Encoding canceled", video);
				return;
			}
			catch (Exception ex)
			{
				communication.Status.Status.OnNext("Error");
				log.Error("Encoding failed", ex, video);
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
					log.Warning("New video is bigger than original {Video} {OriginalSize:F0} [MB] <  {NewSize:F0} [MB]", video.FullPath, new FileInfo(tempInputPath).Length / (1024 * 1024.0), new FileInfo(tempOutputPath).Length / (1024 * 1024.0));
				}
				else
				{
					string indexerPath = Path.GetFullPath(settings.IndexerPath);
					string videoDirpath = Path.GetFullPath(video.DirectoryPath);
					string relativePath = videoDirpath[indexerPath.Length..].TrimStart(Path.DirectorySeparatorChar);

					// Step 4: Move original to trash (keep directory structure)
					string trashDir = Path.GetFullPath(Path.Combine(settings.TrashPath, relativePath));
					Directory.CreateDirectory(trashDir);
					string trashPath = Path.Combine(trashDir, video.Filename);
					communication.Status.Status.OnNext("Encode success, moving files...");
					File.Move(tempInputPath, trashPath, true);
					File.Delete(video.FullPath);
					video.Filename = Path.ChangeExtension(video.Filename, MP4_EXT);
					// Step 5: Move new file to original location
					File.Move(tempOutputPath, video.FullPath, true);

					// Step 6: Update indexation data
					video.FileSizeCompressed = new FileInfo(video.FullPath).Length;
					video.Status = CompressionStatus.Compressed;
					video.LastModified = DateTime.Now;
					log.Information("New video successfully encoded {Video}, reduced of {CompressionFactor:F1}%", video.FullPath, video.CompressionFactor);
					communication.Status.Status.OnNext("Done");
				}
			}
			else
			{
				video.Status = CompressionStatus.FailedToCompress;
			}

			Directory.Delete(tempDir, true);
		}
		finally
		{
			// Allow system to sleep again after encoding completes
			PowerManagement.AllowSleep();
		}
	}
}