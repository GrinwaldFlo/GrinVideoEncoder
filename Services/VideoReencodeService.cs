using GrinVideoEncoder.Models;
using System.Diagnostics;
using System.Text;

namespace GrinVideoEncoder.Services;

public class VideoReencodeService(VideoProcessorService videoProcessor, IAppSettings settings, LogMain log, CommunicationService comm) : BackgroundService
{
	public const string MP4_EXT = ".mp4";

	private bool IsReady()
	{
		return !string.IsNullOrEmpty(settings.IndexerPath) && Directory.Exists(settings.IndexerPath);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await Task.Delay(5000, stoppingToken);

		await ResetProcessStatus();

		comm.AskTreatFiles = await VideoDbContext.CountVideosWithStatusAsync(CompressionStatus.ToProcess) > 0;

		while (!stoppingToken.IsCancellationRequested)
		{
			if (!comm.Status.IsRunning.Value && comm.AskTreatFiles && IsReady())
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

			if (ShouldShutdown())
			{
				log.Information("Scheduled shutdown time reached, shutting down the computer");
				comm.Status.Status.OnNext("Shutting down...");
				ExecuteShutdown();
				comm.AskClose = true;
			}
			if (comm.AskClose)
			{
				Environment.Exit(0);
			}

			await Task.Delay(1000, stoppingToken);
		}
	}

	private async Task ResetProcessStatus()
	{
		var processingVideos = await VideoDbContext.GetVideosWithStatusAsync(CompressionStatus.Processing);
		if (processingVideos.Count > 0)
		{
			log.Warning("{NbVideos} where in processing mode, re-set to ToProcess mode", processingVideos.Count);
			await VideoDbContext.SetStatusAsync(processingVideos.Select(x => x.Id), CompressionStatus.ToProcess);
		}
	}

	private async Task ReencodeVideoAsync(VideoFile video)
	{
		if (comm.VideoProcessToken.Token.IsCancellationRequested)
			return;
		bool success;
		string tempDir = Path.GetFullPath(Path.Combine(settings.ProcessingPath, Guid.NewGuid().ToString()));
		string tempInputPath = Path.Combine(tempDir, video.Filename);
		string tempOutputPath = Path.Combine(tempDir, "compressed_" + video.Filename);
		string tempOrigineOutputPath = Path.Combine(video.DirectoryPath, $"{video.Filename}-GrinVideoEncoder.tmp");
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
			video.Status = CompressionStatus.FailedToCompress;
			comm.Status.Status.OnNext("Error");
			log.Error("Encoding failed", ex, video);
			await File.WriteAllTextAsync(Path.Combine(tempDir, "error.txt"), $"{ex}");
			return;
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
		var originalCreationTime = new FileInfo(video.FullPath).CreationTime;
		var originalLastWriteTime = new FileInfo(video.FullPath).LastWriteTime;
		double fpsTol = 2;
		bool hasFpsDiff = Math.Abs((originalFps ?? 0) - (compressedFps ?? 0)) > fpsTol;
		(double min, double max) = hasFpsDiff ? await videoProcessor.GetDiffFps(tempInputPath) : (0, 0);

		if (!success)
		{
			WriteError($"{video.FullPath} | Failed to encode");
		}
		else if (durationDiff == null || originalFps == null || compressedFps == null || originalPixels == null || compressedPixels == null)
		{
			WriteError($"{video.FullPath} | Failed to read media info");
		}
		else if (durationDiff > 0.5)
		{
			WriteError($"{video.FullPath} | Duration difference is too big {durationDiff:F2} [s]");
		}
		else if (originalPixels != compressedPixels)
		{
			WriteError($"{video.FullPath} | Resolution has changed {originalPixels} / {compressedPixels}");
		}
		else if (hasFpsDiff && max - min < fpsTol)
		{
			WriteError($"{video.FullPath} | FPS has changed {originalFps.Value:F2} / {compressedFps.Value:F2}");
		}
		else if (new FileInfo(tempInputPath).Length < new FileInfo(tempOutputPath).Length)
		{
			video.Status = CompressionStatus.Bigger;
			log.Warning("New video is bigger than original {Video} {OriginalSize:F0} [MB] <  {NewSize:F0} [MB]", video.FullPath, new FileInfo(tempInputPath).Length / (1024 * 1024.0), new FileInfo(tempOutputPath).Length / (1024 * 1024.0));
			Directory.Delete(tempDir, true);
		}
		else
		{
			try
			{
				if (max - min > fpsTol)
				{
					log.Warning("{video} has originally a FPS between {min:F2} and {max:F2}", video.FullPath, min, max);
				}
				string indexerPath = Path.GetFullPath(settings.IndexerPath);
				string videoDirpath = Path.GetFullPath(video.DirectoryPath);
				string relativePath = videoDirpath[indexerPath.Length..].TrimStart(Path.DirectorySeparatorChar);
				string trashDir = Path.GetFullPath(Path.Combine(settings.TrashPath, relativePath));

				// Prepare trash
				string trashPath = Path.Combine(trashDir, video.Filename);

				comm.Status.Status.OnNext("Encode success, moving files...");

				// Copy video to original location
				File.Copy(tempOutputPath, tempOrigineOutputPath, true);
				File.Delete(video.FullPath);
				video.Filename = Path.ChangeExtension(video.Filename, MP4_EXT);
				File.Move(tempOrigineOutputPath, video.FullPath, true);
				RestoreDate(video.FullPath, originalCreationTime, originalLastWriteTime);

				// Trash and cleanup
				Directory.CreateDirectory(trashDir);
				File.Move(tempInputPath, trashPath, true);
				Directory.Delete(tempDir, true);

				var newMediaInfo = await VideoProcessorService.GetMediaInfo(video.FullPath);
				video.Sanity = newMediaInfo?.VideoIsSane() is true ? SanityStatus.EncodedOk : SanityStatus.EncodedKo;
				video.FileSizeCompressed = new FileInfo(video.FullPath).Length;
				video.Status = CompressionStatus.Compressed;
				video.LastModified = DateTime.Now;
				log.Information("New video successfully encoded {Video}, reduced of {CompressionFactor:F1}%", video.FullPath, video.CompressionFactor ?? 0d);
				comm.Status.Status.OnNext("Done");
			}
			catch (Exception ex)
			{
				video.Status = CompressionStatus.FailedToCompress;
				comm.Status.Status.OnNext("Error");
				log.Error("Encoding failed", ex, video);
			}
		}

		void WriteError(string message)
		{
			video.Status = CompressionStatus.FailedToCompress;
			comm.Status.Status.OnNext("Error");

			log.Error(message);

			StringBuilder text = new(message);
			text.AppendLine();
			text.AppendLine("╔════════════════════════════════════════════════════════════════════════════════════╗");
			text.AppendLine("║                           DETAILED COMPARISON TABLE                                ║");
			text.AppendLine("╚════════════════════════════════════════════════════════════════════════════════════╝");
			text.AppendLine();

			// Comparison table
			text.AppendLine($"{"Property",-25} | {"Original",-30} | {"Compressed",-30}");
			text.AppendLine(new string('─', 90));

			text.AppendLine($"{"Duration",-25} | {originalInfo?.Duration.TotalSeconds:F4} s {"",-20} | {compressedInfo?.Duration.TotalSeconds:F4} s");
			text.AppendLine($"{"Resolution",-25} | {originalInfo?.VideoStreams.FirstOrDefault()?.Width}x{originalInfo?.VideoStreams.FirstOrDefault()?.Height} {"",-18} | {compressedInfo?.VideoStreams.FirstOrDefault()?.Width}x{compressedInfo?.VideoStreams.FirstOrDefault()?.Height}");
			text.AppendLine($"{"FPS",-25} | {originalInfo?.VideoStreams.FirstOrDefault()?.Framerate:F3} {"",-25} | {compressedInfo?.VideoStreams.FirstOrDefault()?.Framerate:F3}");
			text.AppendLine($"{"Total Pixels",-25} | {(originalInfo?.VideoStreams.FirstOrDefault()?.Width ?? 0) * (originalInfo?.VideoStreams.FirstOrDefault()?.Height ?? 0)} {"",-22} | {(compressedInfo?.VideoStreams.FirstOrDefault()?.Width ?? 0) * (compressedInfo?.VideoStreams.FirstOrDefault()?.Height ?? 0)}");
			text.AppendLine($"{"Video Bitrate",-25} | {originalInfo?.VideoStreams.FirstOrDefault()?.Bitrate} {"",-19} | {compressedInfo?.VideoStreams.FirstOrDefault()?.Bitrate}");
			text.AppendLine($"{"Audio Bitrate",-25} | {originalInfo?.AudioStreams.FirstOrDefault()?.Bitrate} {"",-19} | {compressedInfo?.AudioStreams.FirstOrDefault()?.Bitrate}");
			text.AppendLine($"{"Sample Rate",-25} | {originalInfo?.AudioStreams.FirstOrDefault()?.SampleRate} Hz {"",-19} | {compressedInfo?.AudioStreams.FirstOrDefault()?.SampleRate} Hz");
			text.AppendLine($"{"File Size",-25} | {new FileInfo(tempInputPath).Length / (1024 * 1024.0):F2} MB {"",-18} | {new FileInfo(tempOutputPath).Length / (1024 * 1024.0):F2} MB");

			video.EncodingErrorMessage = message;
			File.WriteAllText(Path.Combine(tempDir, "error.txt"), text.ToString());
		}
	}

	private static void RestoreDate(string fullPath, DateTime originalCreationTime, DateTime originalLastWriteTime)
	{
		_ = new FileInfo(fullPath)
		{
			CreationTime = originalCreationTime,
			LastWriteTime = originalLastWriteTime
		};
	}

	private bool ShouldShutdown()
	{
		return (comm.ScheduledShutdownEnabled && DateTime.Now >= comm.ScheduledShutdownTime) || (!comm.AskTreatFiles && comm.ShutdownWhenDone);
	}

	private static void ExecuteShutdown()
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = "shutdown",
			Arguments = "/s /t 60 /c \"GrinVideoEncoder scheduled shutdown\"",
			CreateNoWindow = true,
			UseShellExecute = false
		});
	}
}