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
		{
			// The video was already marked Processing: leaving it so would keep it out of the queue until a restart
			video.Status = CompressionStatus.Original;
			return;
		}

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
			log.Warning("Encoding canceled {FilePath}", video.FullPath);
			DeleteDirectory(tempDir);
			return;
		}
		catch (Exception ex)
		{
			video.Status = CompressionStatus.FailedToCompress;
			video.EncodingErrorMessage = ex.Message;
			comm.Status.Status.OnNext(STR_ERROR);
			log.Error(ex, "Prepare video failed {FilePath}", video.FullPath);
			DeleteDirectory(tempDir);
			return;
		}

		var encoder = new VideoEncoder(settings.QualityLevel, logFfmpeg, comm.Status.EncodingPercent, comm.VideoProcessToken.Token);

		var result = await encoder.EncodeVideoAsync(tempInputPath, tempOutputPath);

		if (result.Reason == VideoEncoder.ResultReason.Canceled)
		{
			video.Status = CompressionStatus.Original;
			comm.Status.Status.OnNext("Canceled");
			log.Warning("Encoding canceled {FilePath}", video.FullPath);
			DeleteDirectory(tempDir);
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
		var compressedStream = compressedInfo?.VideoStreams.FirstOrDefault();
		int originalBitDepth = originalInfo?.GetBitDepth() ?? VideoFile.BIT_DEPTH_UNKNOWN;
		int compressedBitDepth = compressedInfo?.GetBitDepth() ?? VideoFile.BIT_DEPTH_UNKNOWN;
		var comparison = new EncodingComparison(
			OriginalDuration: originalInfo?.Duration.TotalSeconds,
			EncodedDuration: compressedInfo?.Duration.TotalSeconds,
			OriginalPixels: video.TotalPixels,
			EncodedPixels: compressedStream?.Width * compressedStream?.Height,
			OriginalFps: originalInfo?.VideoStreams.FirstOrDefault()?.Framerate,
			EncodedFps: compressedStream?.Framerate,
			OriginalBitDepth: originalBitDepth,
			EncodedBitDepth: compressedBitDepth,
			ExpectedBitDepth: VideoEncoder.GetExpectedOutputBitDepth(originalBitDepth));
		var originalCreationTime = new FileInfo(video.FullPath).CreationTime;
		var originalLastWriteTime = new FileInfo(video.FullPath).LastWriteTime;
		var fpsRange = comparison.NeedsFpsScan ? await GetFpsRangeAsync(tempInputPath) : (min: 0d, max: 0d);
		string encodedPath = Path.Combine(video.DirectoryPath, GetEncodedFilename(video.Filename));
		string? problem = comparison.FindProblem(fpsRange);

		if (problem != null)
		{
			WriteError($"{video.FullPath} | {problem}");
		}
		else if (WouldOverwriteOtherFile(video.FullPath, encodedPath))
		{
			WriteError($"{video.FullPath} | {encodedPath} already exists and would be overwritten");
		}
		else if (new FileInfo(tempInputPath).Length < new FileInfo(tempOutputPath).Length)
		{
			video.Status = CompressionStatus.Bigger;
			comm.Status.Status.OnNext("Bigger than original, kept original");
			log.Warning("New video is bigger than original {Video} {OriginalSize:F0} [MB] <  {NewSize:F0} [MB]", video.FullPath, new FileInfo(tempInputPath).Length / (1024 * 1024.0), new FileInfo(tempOutputPath).Length / (1024 * 1024.0));
			DeleteDirectory(tempDir);
		}
		else
		{
			try
			{
				if (EncodingComparison.IsVariableFrameRate(fpsRange))
				{
					log.Warning("{video} has originally a FPS between {min:F2} and {max:F2}", video.FullPath, fpsRange.min, fpsRange.max);
				}
				string trashDir = GlobUtils.GetTrashDirectory(settings.TrashPath, settings.IndexerPath, video.DirectoryPath);

				// Prepare trash
				string trashPath = Path.Combine(trashDir, video.Filename);

				comm.Status.Status.OnNext("Encode success, moving files...");

				// Copy video to original location
				await GlobUtils.CopyFileWithProgressAsync(tempOutputPath, tempOrigineOutputPath, comm.Status.EncodingPercent, CancellationToken.None); // Do not allow to cancel while all is done
				File.Delete(video.FullPath);
				video.Filename = Path.GetFileName(encodedPath);
				File.Move(tempOrigineOutputPath, video.FullPath, true);
				RestoreDate(video.FullPath, originalCreationTime, originalLastWriteTime);

				// Trash and cleanup
				Directory.CreateDirectory(trashDir);
				File.Move(tempInputPath, trashPath, true);
				DeleteDirectory(tempDir);

				var newMediaInfo = await VideoProcessorService.GetMediaInfo(video.FullPath);
				video.Sanity = newMediaInfo?.VideoIsSane() is true ? SanityStatus.EncodedOk : SanityStatus.EncodedKo;
				video.BitDepth = newMediaInfo?.GetBitDepth() ?? VideoFile.BIT_DEPTH_UNKNOWN;
				video.FileSizeCompressed = new FileInfo(video.FullPath).Length;
				video.Status = CompressionStatus.Compressed;
				video.LastModified = DateTime.UtcNow;
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

	/// <summary>
	/// Name of the re-encoded video, which is always an mp4.
	/// </summary>
	internal static string GetEncodedFilename(string filename) => Path.ChangeExtension(filename, MP4_EXT);

	/// <summary>
	/// True if writing the re-encoded video to <paramref name="newPath"/> would replace another file than the original,
	/// such as an existing a.mp4 when re-encoding a.mkv.
	/// </summary>
	internal static bool WouldOverwriteOtherFile(string originalPath, string newPath)
	{
		var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		return !string.Equals(Path.GetFullPath(originalPath), Path.GetFullPath(newPath), comparison) && File.Exists(newPath);
	}

	private static void DeleteDirectory(string path)
	{
		if (Directory.Exists(path))
			Directory.Delete(path, true);
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