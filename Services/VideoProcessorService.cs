using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using static GrinVideoEncoder.Utils.GpuDetector;

namespace GrinVideoEncoder.Services;

public class VideoProcessorService(IAppSettings settings)
{
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
		catch (Exception ex)
		{
			Log.Warning(ex, "Failed to get media info for {FilePath}", filePath);
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

	public async Task ProcessVideo(string filePath, CommunicationService communication)
	{
		var token = communication.VideoProcessToken.Token;
		communication.Status.Filename = filePath;
		if (!ReadyToProcess)
			return;

		communication.Status.Status = "Processing";
		communication.Status.IsRunning = true;

		var gpuType = GpuDetector.DetectGpuVendor();
		FileNamer filename = new(settings, filePath);
		try
		{
			await PrepareProcessing(filename);

			var mediaInfo = await FFmpeg.GetMediaInfo(filename.ProcessingPath, token);
			var videoStream = mediaInfo.VideoStreams.FirstOrDefault()
				?? throw new Exception("No video stream found");

			if (!settings.ForceCpu && gpuType is GpuDetector.GpuVendor.Nvidia or GpuDetector.GpuVendor.AMD)
			{
				try
				{
					await ProcessWithGpu(videoStream, mediaInfo.AudioStreams, filename.TempPath, gpuType, token);
				}
				catch (Exception ex) when (ex.Message.Contains("encoder") || ex.Message.Contains("GPU"))
				{
					Log.Warning("{GpuType} GPU encoding failed. Falling back to CPU encoding. Error: {ErrorMessage}", gpuType, ex.Message);
					videoStream = mediaInfo.VideoStreams.First();
					await ProcessWithCpu(videoStream, mediaInfo.AudioStreams, filename, token);
				}
			}
			else
			{
				await ProcessWithCpu(videoStream, mediaInfo.AudioStreams, filename, token);
			}

			FinalizeProcessing(filename);
			communication.Status.Status = "Done";
		}
		catch (OperationCanceledException)
		{
			HandleProcessingError(filename);
			communication.Status.Status = $"Cancelled";
			Log.Warning("Encoding cancelled {InputPath}", filename.InputPath);
		}
		catch (Exception ex)
		{
			HandleProcessingError(filename, ex);
			communication.Status.Status = $"Failed";
		}
		finally
		{
			communication.Status.IsRunning = false;
		}
	}

	private static void FinalizeProcessing(FileNamer filename)
	{
		File.Move(filename.TempPath, filename.OutputPath);
		File.Move(filename.ProcessingPath, filename.TrashPath);
		Log.Information("Successfully processed {FinalPath}", filename.OutputPath);
	}

	private static void HandleProcessingError(FileNamer file, Exception? ex = null)
	{
		if (ex != null)
			Log.Error(ex, "Processing error");
		if (File.Exists(file.TempPath))
			File.Delete(file.TempPath);
		if (File.Exists(file.TempFirstPassPath))
			File.Delete(file.TempFirstPassPath);
		if (File.Exists(file.ProcessingPath))
		{
			File.Move(file.ProcessingPath, file.FailedPath, true);
		}
	}

	private static async Task PrepareProcessing(FileNamer file)
	{
		Log.Information("Waiting {FilePath} is ready", file.InputPath);
		await WaitForFile(file.InputPath, CancellationToken.None);

		File.Move(file.InputPath, file.ProcessingPath);
		Log.Information("Started processing {ProcessingPath}", file.ProcessingPath);
	}

	private async Task ProcessWithCpu(IVideoStream videoStream, IEnumerable<IAudioStream> audioStreams, FileNamer file, CancellationToken token)
	{
		videoStream.SetCodec(VideoCodec.hevc);

		var conversion = FFmpeg.Conversions.New()
			.AddStream(videoStream)
			.AddParameter($"-crf {settings.QualityLevel}")
			.AddParameter("-preset medium");

		foreach (var audioStream in audioStreams)
		{
			conversion.AddStream(audioStream);
		}

		conversion.SetOutput(file.TempPath);
		conversion.OnDataReceived += (sender, args) => Log.Information("FFmpeg [CPU]: {Data}", args.Data);
		await conversion.Start(token);
	}

	private async Task ProcessWithGpu(IVideoStream videoStream, IEnumerable<IAudioStream> audioStreams, string outputPath,
				GpuVendor gpuType, CancellationToken token)
	{
		var conversion = FFmpeg.Conversions.New()
			.AddStream(videoStream);

		// Add GPU-specific parameters for constant quality encoding
		switch (gpuType)
		{
			case GpuVendor.Nvidia:
				conversion
					.AddParameter("-c:v hevc_nvenc")
					.AddParameter("-preset p7")
					.AddParameter("-rc vbr")
					.AddParameter($"-cq {settings.QualityLevel}")
					.AddParameter("-rc-lookahead 32")
					.AddParameter("-spatial-aq 1")
					.AddParameter("-temporal-aq 1")
					.AddParameter("-gpu 0");
				break;

			case GpuVendor.AMD:
				conversion
					.AddParameter("-c:v hevc_amf")
					.AddParameter("-quality quality")
					.AddParameter("-rc cqp")
					.AddParameter($"-qp_i {settings.QualityLevel}")
					.AddParameter($"-qp_p {settings.QualityLevel + 2}")
					.AddParameter($"-qp_b {settings.QualityLevel + 4}");
				break;

			default:
				throw new ArgumentException("Unsupported GPU type");
		}

		conversion.SetOutput(outputPath);

		foreach (var audioStream in audioStreams)
		{
			conversion.AddStream(audioStream);
		}

		conversion.OnDataReceived += (sender, args) => Log.Information("FFmpeg [{GpuType} GPU]: {Data}", gpuType, args.Data);
		await conversion.Start(token);
	}

	/// <summary>
	/// Waits for the file to be ready for reading.
	/// </summary>
	/// <param name="filePath"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	private static async Task WaitForFile(string filePath, CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			try
			{
				using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
				if (stream.Length > 0)
					return;
			}
			catch
			{
				// Do nothing
			}

			await Task.Delay(5000, token);
		}
	}

	/// <summary>
	/// Downloads FFmpeg if not exists
	/// </summary>
	/// <returns></returns>
	public async Task FfmpegDownload()
	{
		string ffmpegPath = Path.Combine(settings.TempPath, "ffmpeg");
		if (!Directory.Exists(ffmpegPath))
		{
			Directory.CreateDirectory(ffmpegPath);
		}
		FFmpeg.SetExecutablesPath(ffmpegPath);
		await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegPath);

		string exePath = Path.Combine(ffmpegPath, "ffmpeg.exe");

		try
		{
			using var process = new System.Diagnostics.Process();
			process.StartInfo.FileName = exePath;
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

			Log.Information("FFmpeg version: {Version}", output.Split('\n').FirstOrDefault()?.Trim());
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "FFmpeg is not working properly. Application will close.");
			Environment.Exit(1);
		}
	}
}