using GrinVideoEncoder.Utils;
using Microsoft.VisualBasic;
using System.Diagnostics;
using System.Management;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using static GrinVideoEncoder.Utils.GpuDetector;

namespace GrinVideoEncoder.Services;

public class VideoProcessorService(IAppSettings settings, LogFfmpeg log, CommunicationService comm)
{
	private static readonly TimeSpan _fileReadyPollDelay = TimeSpan.FromSeconds(5);

	public bool ReadyToProcess { get; private set; } = true;

	/// <summary>
	/// Gets the full media information for a video file using FFprobe.
	/// </summary>
	/// <param name="filePath">Path to the video file.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>The media information including duration, streams, and dimensions.</returns>
	public static async Task<IMediaInfo?> GetMediaInfo(string filePath, CancellationToken token = default)
	{
		try
		{
			return await FFmpeg.GetMediaInfo(filePath, token);
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Gets the duration of a video file using FFprobe.
	/// </summary>
	/// <param name="filePath">Path to the video file.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>The duration of the video.</returns>
	public static async Task<TimeSpan?> GetVideoDuration(string filePath, CancellationToken token = default)
	{
		var mediaInfo = await GetMediaInfo(filePath, token);
		return mediaInfo?.Duration;
	}

	/// <summary>
	/// Makes FFmpeg ready and verifies it runs. Downloads it if not exists, except on Linux where
	/// <see cref="Startup.EncoderRequirements"/> already selected the system FFmpeg: the downloadable build has no VA-API.
	/// </summary>
	/// <returns></returns>
	public async Task EnsureFfmpegReady()
	{
		if (!OperatingSystem.IsLinux())
		{
			string ffmpegPath = Path.Combine(settings.TempPath, "ffmpeg");
			if (!Directory.Exists(ffmpegPath))
			{
				Directory.CreateDirectory(ffmpegPath);
			}
			FFmpeg.SetExecutablesPath(ffmpegPath);
			await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegPath);
		}

		try
		{
			using var process = new System.Diagnostics.Process();
			process.StartInfo.FileName = GlobUtils.GetFfmpegExecutablePath();
			process.StartInfo.Arguments = "-version";
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.CreateNoWindow = true;
			process.Start();
			string output = await process.StandardOutput.ReadToEndAsync();
			await process.WaitForExitAsync();

			if (process.ExitCode != 0)
			{
				throw new Exception($"FFmpeg exited with code {process.ExitCode}");
			}

			log.Information("FFmpeg version: {Version}", output.Split('\n').FirstOrDefault()?.Trim());
		}
		catch (Exception ex)
		{
			log.Fatal(ex, "FFmpeg is not working properly. Application will close.");
			Environment.Exit(1);
		}
	}

	/// <summary>
	/// Analyse the video to find the max and min FPS during the video.
	/// </summary>
	/// <param name="videoPath">Path to the video file to analyze.</param>
	/// <returns>The absolute FPS difference</returns>
	public static async Task<(double min, double max)> GetDiffFps(string videoPath)
	{
		var result = await ProcessRunner.RunAsync(GlobUtils.GetFfprobeExecutablePath(),
			["-v", "error", "-select_streams", "v:0", "-show_entries", "frame=best_effort_timestamp_time", "-of", "csv=p=0", videoPath]);

		if (result.ExitCode != 0)
		{
			throw new InvalidOperationException($"FFprobe exited with code {result.ExitCode}. Error: {result.StandardError}");
		}

		return ComputeFpsRange(result.StandardOutput);
	}

	/// <summary>
	/// Computes the min and max instantaneous FPS from the frame timestamps printed by ffprobe, one per line.
	/// </summary>
	internal static (double min, double max) ComputeFpsRange(string ffprobeOutput)
	{
		var timestamps = ffprobeOutput.Replace(",", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Select(line => double.TryParse(line.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ts) ? ts : (double?)null)
			.Where(ts => ts.HasValue)
			.Select(ts => ts!.Value)
			.ToList();

		if (timestamps.Count < 2)
		{
			return (0.0, 0.0);
		}

		// Calculate instantaneous FPS between consecutive frames
		var fpsList = new List<double>();
		for (int i = 1; i < timestamps.Count; i++)
		{
			double timeDiff = timestamps[i] - timestamps[i - 1];
			if (timeDiff > 0)
			{
				double fps = 1.0 / timeDiff;
				fpsList.Add(fps);
			}
		}

		if (fpsList.Count == 0)
		{
			return (0.0, 0.0);
		}

		double maxFps = fpsList.Max();
		double minFps = fpsList.Min();

		return (minFps, maxFps);
	}

	public async Task ProcessVideo(string filePath, CommunicationService communication)
	{
		communication.Status.Filename.OnNext(filePath);
		if (!ReadyToProcess)
			return;

		communication.Status.Status.OnNext("Processing");
		communication.Status.IsRunning.OnNext(true);

		comm.PreventSleep = true;

		FileNamer filename = new(settings, filePath);
		try
		{
			bool prepareSuccess = await PrepareProcessing(filename);

			if (!prepareSuccess)
				return;

			var encoder = new VideoEncoder(settings.QualityLevel, log, comm.Status.EncodingPercent, communication.VideoProcessToken.Token);

			var encodeResult = await encoder.EncodeVideoAsync(filename.ProcessingPath, filename.TempPath);

			if (encodeResult.Success)
			{
				FinalizeProcessing(filename);
				communication.Status.Status.OnNext("Done");
			}
			else if (encodeResult.Reason == VideoEncoder.ResultReason.Canceled)
			{
				HandleProcessingError(filename);
				communication.Status.Status.OnNext($"Cancelled");
				log.Warning("Encoding cancelled {InputPath}", filename.InputPath);
			}
			else
			{
				HandleProcessingError(filename, encodeResult.ErrorMessage);
				communication.Status.Status.OnNext($"Failed");
			}
		}
		finally
		{
			comm.PreventSleep = false;
			communication.Status.IsRunning.OnNext(false);
		}
	}

	/// <summary>
	/// Waits until nothing writes to the file anymore. A file lock alone is not enough: on Linux, locks are advisory
	/// and a copy in progress holds none, so the size and last write time must also stay the same for one poll delay.
	/// </summary>
	/// <returns>True when the file is ready, false if it disappeared or the wait was canceled.</returns>
	internal static async Task<bool> WaitForFile(string filePath, TimeSpan pollDelay, CancellationToken token)
	{
		FileSnapshot? previous = null;
		while (!token.IsCancellationRequested)
		{
			var current = FileSnapshot.TryRead(filePath);
			if (current is null)
				return false;

			if (current == previous && GlobUtils.IsFileReady(filePath))
				return true;

			previous = current;
			await Task.Delay(pollDelay, token);
		}
		return false;
	}

	private void FinalizeProcessing(FileNamer filename)
	{
		File.Move(filename.TempPath, filename.OutputPath);
		File.Move(filename.ProcessingPath, filename.TrashPath);
		log.Information("Successfully processed {FinalPath}", filename.OutputPath);
	}

	private void HandleProcessingError(FileNamer file, string errorMessage = "")
	{
		if (!string.IsNullOrEmpty(errorMessage))
			log.Error(errorMessage, "Processing error");
		if (File.Exists(file.TempPath))
			File.Delete(file.TempPath);
		if (File.Exists(file.TempFirstPassPath))
			File.Delete(file.TempFirstPassPath);
		if (File.Exists(file.ProcessingPath))
		{
			File.Move(file.ProcessingPath, file.FailedPath, true);
		}
	}

	private async Task<bool> PrepareProcessing(FileNamer file)
	{
		try
		{
			log.Information("Waiting {FilePath} is ready", file.InputPath);
			if (!await WaitForFile(file.InputPath, _fileReadyPollDelay, CancellationToken.None))
			{
				log.Warning("{FilePath} disappeared before it could be processed", file.InputPath);
				return false;
			}

			File.Move(file.InputPath, file.ProcessingPath);
			log.Information("Started processing {ProcessingPath}", file.ProcessingPath);

			return true;
		}
		catch (Exception ex)
		{
			log.Error(ex, file.Dump());
		}
		return false;
	}
}