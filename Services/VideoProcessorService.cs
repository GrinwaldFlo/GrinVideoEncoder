using GrinVideoEncoder.Utils;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using static GrinVideoEncoder.Utils.GpuDetector;

namespace GrinVideoEncoder.Services;

public class VideoProcessorService(IAppSettings settings, LogFfmpeg log)
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

	public async Task ProcessVideo(string filePath, CommunicationService communication)
	{
		var token = communication.VideoProcessToken.Token;
		communication.Status.Filename.OnNext(filePath);
		if (!ReadyToProcess)
			return;

		communication.Status.Status.OnNext("Processing");
		communication.Status.IsRunning.OnNext(true);
		
		// Prevent system from sleeping during encoding
		PowerManagement.PreventSleep();

		FileNamer filename = new(settings, filePath);
		try
		{
			await PrepareProcessing(filename);

			await EncodeVideo(filename.ProcessingPath, filename.TempPath, token);

			FinalizeProcessing(filename);
			communication.Status.Status.OnNext("Done");
		}
		catch (OperationCanceledException)
		{
			HandleProcessingError(filename);
			communication.Status.Status.OnNext($"Cancelled");
			log.Warning("Encoding cancelled {InputPath}", filename.InputPath);
		}
		catch (Exception ex)
		{
			HandleProcessingError(filename, ex);
			communication.Status.Status.OnNext($"Failed");
		}
		finally
		{
			// Allow system to sleep again after processing completes
			PowerManagement.AllowSleep();
			communication.Status.IsRunning.OnNext(false);
		}
	}

	private void FinalizeProcessing(FileNamer filename)
	{
		File.Move(filename.TempPath, filename.OutputPath);
		File.Move(filename.ProcessingPath, filename.TrashPath);
		log.Information("Successfully processed {FinalPath}", filename.OutputPath);
	}

	private void HandleProcessingError(FileNamer file, Exception? ex = null)
	{
		if (ex != null)
			log.Error(ex, "Processing error");
		if (File.Exists(file.TempPath))
			File.Delete(file.TempPath);
		if (File.Exists(file.TempFirstPassPath))
			File.Delete(file.TempFirstPassPath);
		if (File.Exists(file.ProcessingPath))
		{
			File.Move(file.ProcessingPath, file.FailedPath, true);
		}
	}

	public async Task EncodeVideo(string inputFilename, string outputFilename, CancellationToken token = default)
	{
		var gpuType = GpuDetector.DetectGpuVendor();
		var mediaInfo = await FFmpeg.GetMediaInfo(inputFilename, token);
		var videoStream = mediaInfo.VideoStreams.FirstOrDefault()
			?? throw new Exception("No video stream found");

		if (!settings.ForceCpu && gpuType is GpuDetector.GpuVendor.Nvidia or GpuDetector.GpuVendor.AMD)
		{
			try
			{
				await ProcessWithGpu(videoStream, mediaInfo.AudioStreams, mediaInfo.SubtitleStreams, outputFilename, gpuType, token);
			}
			catch (Exception ex) when (ex.Message.Contains("encoder") || ex.Message.Contains("GPU"))
			{
				log.Warning("{GpuType} GPU encoding failed. Falling back to CPU encoding. Error: {ErrorMessage}", gpuType, ex.Message);
				throw new Exception("No GPU found");
				//videoStream = mediaInfo.VideoStreams.First();
				//await ProcessWithCpu(videoStream, mediaInfo.AudioStreams, outputFilename, token);
			}
		}
		else
		{
			throw new Exception("No GPU found");
			//await ProcessWithCpu(videoStream, mediaInfo.AudioStreams, outputFilename, token);
		}
	}

	private async Task PrepareProcessing(FileNamer file)
	{
		log.Information("Waiting {FilePath} is ready", file.InputPath);
		await WaitForFile(file.InputPath, CancellationToken.None);

		File.Move(file.InputPath, file.ProcessingPath);
		log.Information("Started processing {ProcessingPath}", file.ProcessingPath);
	}

	private async Task ProcessWithCpu(IVideoStream videoStream, IEnumerable<IAudioStream> audioStreams, string output, CancellationToken token)
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

		conversion.SetOutput(output);
		conversion.OnDataReceived += (sender, args) => log.Information("FFmpeg [CPU]: {Data}", args.Data);
		await conversion.Start(token);
	}

	private async Task ProcessWithGpu(IVideoStream videoStream, IEnumerable<IAudioStream> audioStreams, IEnumerable<ISubtitleStream> subtitleStreams, string outputPath,
				GpuVendor gpuType, CancellationToken token)
	{
		if (!outputPath.EndsWith(".mp4"))
			throw new Exception("Please provide an mp4 file");

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
					.AddParameter("-rc cqp")
					.AddParameter($"-qp_i {settings.QualityLevel}")
					.AddParameter($"-qp_p {settings.QualityLevel}")
					.AddParameter("-pix_fmt yuv420p")
					.AddParameter("-tag:v hvc1");
				break;

			default:
				throw new ArgumentException("Unsupported GPU type");
		}

		// Process Audio
		foreach (var audioStream in audioStreams)
		{
			conversion.AddStream(audioStream);
		}

		// Process Subtitles
		if (subtitleStreams != null && subtitleStreams.Any())
		{
			foreach (var subStream in subtitleStreams)
			{
				conversion.AddStream(subStream);
			}
			// Force conversion to MP4-compatible subtitle format
			conversion.AddParameter("-c:s mov_text");
		}

		conversion.SetOutput(outputPath);

		conversion.OnDataReceived += (sender, args) => log.Information("FFmpeg [{GpuType} GPU]: {Data}", gpuType, args.Data);
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
			if (IsFileAvailable(filePath))
				return;

			await Task.Delay(5000, token);
		}
	}

	public static bool IsFileAvailable(string filePath)
	{
		try
		{
			using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
			if (stream.Length > 0)
				return true;
		}
		catch
		{
			// Do nothing
		}
		return false;
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

			log.Information("FFmpeg version: {Version}", output.Split('\n').FirstOrDefault()?.Trim());
		}
		catch (Exception ex)
		{
			log.Fatal(ex, "FFmpeg is not working properly. Application will close.");
			Environment.Exit(1);
		}
	}
}