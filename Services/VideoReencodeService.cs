using GrinVideoEncoder.Models;

namespace GrinVideoEncoder.Services;

public class VideoReencodeService(VideoProcessorService videoProcessor, IAppSettings settings, LogMain log, CommunicationService comm) : BackgroundService
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

		var processingVideos = await VideoDbContext.GetVideosWithStatusAsync(CompressionStatus.Processing);
		if (processingVideos.Count > 0)
		{
			log.Warning("{NbVideos} where in processing mode, re-set to ToProcess mode", processingVideos.Count);
			await VideoDbContext.SetStatusAsync(processingVideos.Select(x => x.Id), CompressionStatus.ToProcess);
		}

		comm.AskTreatFiles = (await VideoDbContext.CountVideosWithStatusAsync(CompressionStatus.ToProcess)) > 0;

		while (!stoppingToken.IsCancellationRequested)
		{
			if (!comm.Status.IsRunning.Value && comm.AskTreatFiles)
			{
				comm.PreventSleep = true;
				await using var context = new VideoDbContext();
				var video = await context.PopNextVideoToProcess();

				if (video != null)
				{
					comm.Status.IsRunning.OnNext(true);
					comm.Status.Filename.OnNext(video.FullPath);
					await ReencodeVideoAsync(video);
					await context.SaveChangesAsync(stoppingToken);
					comm.Status.IsRunning.OnNext(false);

					if (comm.VideoProcessToken.IsCancellationRequested)
					{
						var videoToCancel = await VideoDbContext.GetVideosWithStatusAsync(CompressionStatus.ToProcess);
						await VideoDbContext.SetStatusAsync(videoToCancel.Select(x => x.Id), CompressionStatus.Original);
						comm.Status.Status.OnNext("Task canceled");
						log.Information("Encoding canceled");
					}
				}
				else
				{
					comm.AskTreatFiles = false;
					comm.PreventSleep = false;
				}
			}

			Thread.Sleep(1000);
		}
	}

	private async Task ReencodeVideoAsync(VideoFile video)
	{
		if (comm.VideoProcessToken.Token.IsCancellationRequested)
			return;
		bool success = false;
		string tempDir = Path.GetFullPath(Path.Combine(settings.ProcessingPath, Guid.NewGuid().ToString()));
		string tempInputPath = Path.Combine(tempDir, video.Filename);
		string tempOutputPath = Path.Combine(tempDir, "compressed_" + video.Filename);
		tempOutputPath = Path.ChangeExtension(tempOutputPath, MP4_EXT);

		try
		{
			Directory.CreateDirectory(tempDir);
			comm.Status.Status.OnNext($"Copying {video.FileSizeOriginalFormatted} ...");
			File.Copy(video.FullPath, tempInputPath, true);
			comm.Status.Status.OnNext("Encoding...");
		}
		catch (Exception ex)
		{
			video.Status = CompressionStatus.FailedToCompress;
			comm.Status.Status.OnNext("Error");
			log.Error("Prepare video failed", ex, video);
			Directory.Delete(tempDir, true);
			return;
		}
		
		try
		{
			await videoProcessor.EncodeVideo(tempInputPath, tempOutputPath, comm.VideoProcessToken.Token);
			success = true;
		}
		catch (TaskCanceledException)
		{
			video.Status = CompressionStatus.Original;
			log.Warning("Encoding canceled", video);
			Directory.Delete(tempDir, true);
			return;
		}
		catch (OperationCanceledException)
		{
			video.Status = CompressionStatus.Original;
			log.Warning("Encoding canceled", video);
			Directory.Delete(tempDir, true);
			return;
		}
		catch (Exception ex)
		{
			comm.Status.Status.OnNext("Error");
			log.Error("Encoding failed", ex, video);
		}

		// Step 3: Check DurationSeconds and TotalPixels
		var originalInfo = await VideoProcessorService.GetMediaInfo(tempInputPath);
		var compressedInfo = await VideoProcessorService.GetMediaInfo(tempOutputPath);
		double? originalDuration = originalInfo?.Duration.TotalSeconds;
		double? compressedDuration = compressedInfo?.Duration.TotalSeconds;
		double? durationDiff = originalDuration != null && compressedDuration != null ? Math.Abs(originalDuration.Value - compressedDuration.Value) : null;
		long? originalPixels = video.TotalPixels;
		long? compressedPixels = compressedInfo?.VideoStreams.FirstOrDefault()?.Width * compressedInfo?.VideoStreams.FirstOrDefault()?.Height;
		double? originalFps = originalInfo?.VideoStreams.FirstOrDefault()?.Framerate;
		double? compressedFps = compressedInfo?.VideoStreams.FirstOrDefault()?.Framerate;

		if (!success)
		{
			log.Error("Failed to encode");
		}
		else if (durationDiff == null || originalFps == null || compressedFps == null || originalPixels == null || compressedPixels == null)
		{
			log.Error("Failed to read media info");
		}
		else if (durationDiff > 0.1)
		{
			log.Error("Duration difference is too big {Duration:F2} [s]", durationDiff);
		}
		else if (originalPixels != compressedPixels)
		{
			log.Error("Resolution has changed {Resolution1} / {Resolution2}", originalPixels, compressedPixels);
		}
		else if (Math.Abs(originalFps.Value - compressedFps.Value) > 0.1)
		{
			log.Error("FPS has changed {FPS1:F2} / {FPS2:F2}", originalFps.Value, compressedFps.Value);
		}
		else if (new FileInfo(tempInputPath).Length < new FileInfo(tempOutputPath).Length)
		{
			video.Status = CompressionStatus.Bigger;
			log.Warning("New video is bigger than original {Video} {OriginalSize:F0} [MB] <  {NewSize:F0} [MB]", video.FullPath, new FileInfo(tempInputPath).Length / (1024 * 1024.0), new FileInfo(tempOutputPath).Length / (1024 * 1024.0));
		}
		else
		{
			try
			{
				string indexerPath = Path.GetFullPath(settings.IndexerPath);
				string videoDirpath = Path.GetFullPath(video.DirectoryPath);
				string relativePath = videoDirpath[indexerPath.Length..].TrimStart(Path.DirectorySeparatorChar);

				// Step 4: Move original to trash (keep directory structure)
				string trashDir = Path.GetFullPath(Path.Combine(settings.TrashPath, relativePath));
				Directory.CreateDirectory(trashDir);
				string trashPath = Path.Combine(trashDir, video.Filename);
				comm.Status.Status.OnNext("Encode success, moving files...");
				File.Move(tempInputPath, trashPath, true);
				File.Delete(video.FullPath);
				video.Filename = Path.ChangeExtension(video.Filename, MP4_EXT);
				// Step 5: Move new file to original location
				File.Copy(tempOutputPath, video.FullPath, true);
				/// Temp dir is removed only if everything went well
				Directory.Delete(tempDir, true);
				// Step 6: Update indexation data
				video.FileSizeCompressed = new FileInfo(video.FullPath).Length;
				video.Status = CompressionStatus.Compressed;
				video.LastModified = DateTime.Now;
				log.Information("New video successfully encoded {Video}, reduced of {CompressionFactor:F1}%", video.FullPath, video.CompressionFactor);
				comm.Status.Status.OnNext("Done");
			}
			catch (Exception ex)
			{
				video.Status = CompressionStatus.FailedToCompress;
				comm.Status.Status.OnNext("Error");
				log.Error("Encoding failed", ex, video);
			}
		}

		// Meaning a check has failed
		if (video.Status == CompressionStatus.Processing)
		{
			video.Status = CompressionStatus.FailedToCompress;
			Directory.Delete(tempDir, true);
		}
	}
}