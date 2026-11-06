using GrinVideoEncoder.Models;
using System.Text;

namespace GrinVideoEncoder.Services;

public class VideoReencodeService(IAppSettings settings, LogMain log, LogFfmpeg logFfmpeg, CommunicationService comm) : BackgroundService
{
	public const string MP4_EXT = ".mp4";
	private const string STR_ERROR = "Error";

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
					try
					{
						await ReencodeVideoAsync(video);
					}
					catch (Exception ex)
					{
						// An unexpected error must fail this video only, not stop the service and the whole queue
						video.Status = CompressionStatus.FailedToCompress;
						video.EncodingErrorMessage = ex.Message;
						comm.Status.Status.OnNext(STR_ERROR);
						log.Error(ex, "Unexpected error while re-encoding {FilePath}", video.FullPath);
					}
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
				await ExecuteShutdownAsync();
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

		string tempDir = Path.GetFullPath(Path.Combine(settings.ProcessingPath, Guid.NewGuid().ToString()));
		string tempInputPath = Path.Combine(tempDir, video.Filename);
		string tempOutputPath = Path.Combine(tempDir, "compressed_" + video.Filename);
		string tempOrigineOutputPath = Path.Combine(video.DirectoryPath, $"{video.Filename}-GrinVideoEncoder.tmp");
		tempOutputPath = Path.ChangeExtension(tempOutputPath, MP4_EXT);

		try
		{
			Directory.CreateDirectory(tempDir);
			comm.Status.Status.OnNext($"Copying {video.FileSizeOriginalFormatted} ...");
			await GlobUtils.CopyFileWithProgressAsync(video.FullPath, tempInputPath, comm.Status.EncodingPercent, comm.VideoProcessToken.Token);
			comm.Status.Status.OnNext("Encoding...");
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
			comm.Status.Status.OnNext(STR_ERROR);
			log.Error("Prepare video failed", ex, video);
			Directory.Delete(tempDir, true);
			return;
		}

		var encoder = new VideoEncoder(settings.QualityLevel, logFfmpeg, comm.Status.EncodingPercent, comm.VideoProcessToken.Token);

		var result = await encoder.EncodeVideoAsync(tempInputPath, tempOutputPath);

		if (result.Reason == VideoEncoder.ResultReason.Canceled)
		{
			video.Status = CompressionStatus.Original;
			log.Warning("Encoding canceled", video);
			Directory.Delete(tempDir, true);
			return;
		}
		else if (!result.Success)
		{
			video.Status = CompressionStatus.FailedToCompress;
			comm.Status.Status.OnNext(STR_ERROR);
			video.EncodingErrorMessage = result.ErrorMessage;
			log.Error("Encoding failed {FilePath}: {ErrorMessage}", video.FullPath, result.ErrorMessage);
			await File.WriteAllTextAsync(Path.Combine(tempDir, "error.txt"), result.Dump());
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
		int originalBitDepth = originalInfo?.GetBitDepth() ?? VideoFile.BIT_DEPTH_UNKNOWN;
		int compressedBitDepth = compressedInfo?.GetBitDepth() ?? VideoFile.BIT_DEPTH_UNKNOWN;
		int? expectedBitDepth = VideoEncoder.GetExpectedOutputBitDepth(originalBitDepth);
		double fpsTol = 2;
		// Unreadable FPS is reported below as a media info failure, it must not trigger the slow full-file scan
		bool hasFpsDiff = originalFps != null && compressedFps != null && Math.Abs(originalFps.Value - compressedFps.Value) > fpsTol;
		(double min, double max) = hasFpsDiff ? await GetFpsRangeAsync(tempInputPath) : (0, 0);

		if (!result.Success)
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
		else if (expectedBitDepth != null && compressedBitDepth < expectedBitDepth)
		{
			WriteError($"{video.FullPath} | Bit depth has been reduced {originalBitDepth} / {compressedBitDepth}, expected {expectedBitDepth}");
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
				string trashDir = GlobUtils.GetTrashDirectory(settings.TrashPath, settings.IndexerPath, video.DirectoryPath);

				// Prepare trash
				string trashPath = Path.Combine(trashDir, video.Filename);

				comm.Status.Status.OnNext("Encode success, moving files...");

				// Copy video to original location
				await GlobUtils.CopyFileWithProgressAsync(tempOutputPath, tempOrigineOutputPath, comm.Status.EncodingPercent, CancellationToken.None); // Do not allow to cancel while all is done
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
				video.BitDepth = newMediaInfo?.GetBitDepth() ?? VideoFile.BIT_DEPTH_UNKNOWN;
				video.FileSizeCompressed = new FileInfo(video.FullPath).Length;
				video.Status = CompressionStatus.Compressed;
				video.LastModified = DateTime.Now;
				log.Information("New video successfully encoded {Video}, reduced of {CompressionFactor:F1}%", video.FullPath, Math.Round(video.CompressionFactor ?? 0d, 2));
				comm.Status.Status.OnNext("Done");
			}
			catch (Exception ex)
			{
				video.Status = CompressionStatus.FailedToCompress;
				comm.Status.Status.OnNext(STR_ERROR);
				video.EncodingErrorMessage = ex.Message;
				log.Error(ex, "Encoding failed {FilePath}", video.FullPath);
			}
		}

		void WriteError(string message)
		{
			video.Status = CompressionStatus.FailedToCompress;
			comm.Status.Status.OnNext(STR_ERROR);

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
			text.AppendLine($"{"Bit Depth",-25} | {originalBitDepth,-30} | {compressedBitDepth}");
			text.AppendLine($"{"Video Bitrate",-25} | {originalInfo?.VideoStreams.FirstOrDefault()?.Bitrate} {"",-19} | {compressedInfo?.VideoStreams.FirstOrDefault()?.Bitrate}");
			text.AppendLine($"{"Audio Bitrate",-25} | {originalInfo?.AudioStreams.FirstOrDefault()?.Bitrate} {"",-19} | {compressedInfo?.AudioStreams.FirstOrDefault()?.Bitrate}");
			text.AppendLine($"{"Sample Rate",-25} | {originalInfo?.AudioStreams.FirstOrDefault()?.SampleRate} Hz {"",-19} | {compressedInfo?.AudioStreams.FirstOrDefault()?.SampleRate} Hz");
			text.AppendLine($"{"File Size",-25} | {GlobUtils.FormatFileSizeMb(tempInputPath)} {"",-18} | {GlobUtils.FormatFileSizeMb(tempOutputPath)}");

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

	/// <summary>
	/// Schedules the shutdown and closes the application. If the system refuses, the application stays open
	/// and the shutdown options are turned off, so it does not retry every second.
	/// </summary>
	private async Task ExecuteShutdownAsync()
	{
		log.Information("Shutdown condition met, shutting down the computer");
		comm.Status.Status.OnNext("Shutting down...");

		var result = await SystemShutdown.RequestAsync();
		if (result.ExitCode == 0)
		{
			comm.AskClose = true;
			return;
		}

		comm.ScheduledShutdownEnabled = false;
		comm.ShutdownWhenDone = false;
		comm.Status.Status.OnNext("Shutdown failed");
		log.Error("Shutdown failed with exit code {ExitCode}: {Output}", result.ExitCode, result.Output.Trim());
	}

	/// <summary>
	/// Scans the FPS of every frame. A failure is logged and returns an empty range, which fails the video as an FPS change.
	/// </summary>
	private async Task<(double min, double max)> GetFpsRangeAsync(string videoPath)
	{
		try
		{
			return await VideoProcessorService.GetDiffFps(videoPath);
		}
		catch (Exception ex)
		{
			log.Error(ex, "Failed to scan the FPS of {FilePath}", videoPath);
			return (0, 0);
		}
	}
}