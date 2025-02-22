using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using static GrinVideoEncoder.Utils.GpuDetector;

namespace GrinVideoEncoder.Services;

public class VideoProcessorService
{
	private readonly string _failedPath = string.Empty;
	private readonly string _outputPath = string.Empty;
	private readonly string _processingPath = string.Empty;
	private readonly string _tempPath = string.Empty;

	public VideoProcessorService(IConfiguration config)
	{
		try
		{
			_processingPath = config["Folders:Processing"] ?? throw new Exception("Folders:Processing not defined");
			_tempPath = config["Folders:Temp"] ?? throw new Exception("Folders:Temp not defined");
			_outputPath = config["Folders:Output"] ?? throw new Exception("Folders:Output not defined");
			_failedPath = config["Folders:Failed"] ?? throw new Exception("Folders:Failed not defined");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to initialize VideoProcessorService");
		}

		ReadyToProcess = true;
	}

	public bool ReadyToProcess { get; private set; } = false;

	/// <summary>
	/// Downloads FFmpeg if not exists
	/// </summary>
	/// <returns></returns>
	public async Task FfmpegDownload()
	{
		// Download FFmpeg if not exists
		string ffmpegPath = Path.Combine(_tempPath, "ffmpeg");
		if (!Directory.Exists(ffmpegPath))
		{
			Directory.CreateDirectory(ffmpegPath);
		}
		FFmpeg.SetExecutablesPath(ffmpegPath);

		if (!File.Exists(Path.Combine(ffmpegPath, "ffmpeg.exe")))
		{
			await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegPath);
		}
	}

	public async Task ProcessVideo(string filePath, CancellationToken token)
	{
		if (!ReadyToProcess)
			return;

		await FfmpegDownload();

		var gpuType = GpuDetector.DetectGpuVendor();

		string? processingPath = null;
		string? outputPath = null;
		string? statsPath = null;

		try
		{
			(processingPath, outputPath, statsPath) = await PrepareProcessing(filePath);

			var mediaInfo = await FFmpeg.GetMediaInfo(processingPath, token);
			var videoStream = mediaInfo.VideoStreams.FirstOrDefault()
				?? throw new Exception("No video stream found");

			if (gpuType is GpuDetector.GpuVendor.Nvidia or GpuDetector.GpuVendor.AMD)
			{
				try
				{
					await ProcessWithGpu(videoStream, mediaInfo.AudioStreams, outputPath, gpuType, token);
				}
				catch (Exception ex) when (ex.Message.Contains("encoder") || ex.Message.Contains("GPU"))
				{
					Log.Warning("{GpuType} GPU encoding failed. Falling back to CPU encoding. Error: {ErrorMessage}", gpuType, ex.Message);
					await ProcessWithCpu(videoStream, mediaInfo.AudioStreams, outputPath, statsPath, token);
				}
			}
			else
			{
				await ProcessWithCpu(videoStream, mediaInfo.AudioStreams, outputPath, statsPath, token);
			}

			FinalizeProcessing(outputPath);
		}
		catch (Exception ex)
		{
			HandleProcessingError(ex, processingPath, outputPath);
		}
		finally
		{
			Cleanup(statsPath);
		}
	}

	private static async Task ProcessWithGpu(IVideoStream videoStream, IEnumerable<IAudioStream> audioStreams, string outputPath,
		GpuVendor gpuType, CancellationToken token)
	{
		videoStream.SetBitrate(3000000);

		var conversion = FFmpeg.Conversions.New()
			.AddStream(videoStream);

		// Add GPU-specific parameters
		switch (gpuType)
		{
			case GpuVendor.Nvidia:
				conversion
					.AddParameter("-c:v hevc_nvenc")
					.AddParameter("-preset p7")
					.AddParameter("-rc-lookahead 32")
					.AddParameter("-spatial-aq 1")
					.AddParameter("-temporal-aq 1")
					.AddParameter("-gpu 0")
					.AddParameter("-b:v 3M");
				break;

			case GpuVendor.AMD:
				conversion
					.AddParameter("-c:v hevc_amf")
					.AddParameter("-quality quality")
					.AddParameter("-rc cqp")
					.AddParameter("-qp_i 18")
					.AddParameter("-qp_p 20")
					.AddParameter("-qp_b 24")
					.AddParameter("-b:v 3M");
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
		var result = await conversion.Start(token);

		if (result.Duration.TotalSeconds < 10)
			throw new Exception($"{gpuType} GPU encoding failed: {result}");
	}

	private static async Task RunSecondPass(
		IVideoStream videoStream,
		IEnumerable<IAudioStream> audioStreams,
		string outputPath,
		string statsPath,
		CancellationToken token)
	{
		var conversion = FFmpeg.Conversions.New()
			.AddStream(videoStream)
			.AddParameter($"-pass 2")
			.AddParameter($"-passlogfile \"{statsPath}\"")
			.SetOutput(outputPath);

		foreach (var audioStream in audioStreams)
		{
			conversion.AddStream(audioStream);
		}

		conversion.OnDataReceived += (sender, args) => Log.Information("FFmpeg [Pass2]: {Data}", args.Data);
		var result = await conversion.Start(token);

		if (result.Duration.TotalSeconds < 10)
			throw new Exception($"Second pass failed: {result}");
	}

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

	private void Cleanup(string? statsPath)
	{
		if (statsPath != null && File.Exists(statsPath))
			File.Delete(statsPath);
		CleanupTempFiles();
	}

	private void CleanupTempFiles()
	{
		try
		{
			string? tempFolder = _tempPath;
			foreach (string file in Directory.GetFiles(tempFolder))
			{
				File.Delete(file);
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Temp cleanup failed");
		}
	}

	private void FinalizeProcessing(string outputPath)
	{
		string finalPath = Path.Combine(_outputPath, Path.GetFileName(outputPath));
		File.Move(outputPath, finalPath);
		Log.Information("Successfully processed {FinalPath}", finalPath);
	}

	private void HandleProcessingError(Exception ex, string? processingPath, string? outputPath)
	{
		Log.Error(ex, "Processing error");
		if (outputPath != null && File.Exists(outputPath))
			File.Delete(outputPath);
		if (processingPath != null && File.Exists(processingPath))
		{
			string failedPath = Path.Combine(_failedPath, Path.GetFileName(processingPath));
			File.Move(processingPath, failedPath, true);
		}
	}

	private async Task<(string processingPath, string outputPath, string statsPath)> PrepareProcessing(string filePath)
	{
		Log.Information("Waiting {FilePath} is ready", filePath);
		await WaitForFile(filePath, CancellationToken.None);

		string processingPath = Path.Combine(_processingPath, Path.GetFileName(filePath));
		File.Move(filePath, processingPath);

		string outputFileName = Path.GetFileNameWithoutExtension(filePath) + "_encoded.mp4";
		string outputPath = Path.Combine(_tempPath, outputFileName);
		string statsPath = Path.Combine(_tempPath, "x265_stats.log");

		Log.Information("Started processing {ProcessingPath}", processingPath);

		return (processingPath, outputPath, statsPath);
	}

	private async Task ProcessWithCpu(
									IVideoStream videoStream,
		IEnumerable<IAudioStream> audioStreams,
		string outputPath,
		string statsPath,
		CancellationToken token)
	{
		videoStream.SetCodec(VideoCodec.hevc)
				  .SetBitrate(3000000);

		await RunFirstPass(videoStream, statsPath, token);
		await RunSecondPass(videoStream, audioStreams, outputPath, statsPath, token);
	}

	private async Task RunFirstPass(IVideoStream videoStream, string statsPath, CancellationToken token)
	{
		string nullOutputPath = Path.Combine(_tempPath, "null_output.mp4");

		var conversion = FFmpeg.Conversions.New()
			.AddStream(videoStream)
			.AddParameter($"-pass 1")
			.AddParameter($"-passlogfile \"{statsPath}\"")
			.AddParameter("-an")
			.SetOutput(nullOutputPath);

		conversion.OnDataReceived += (sender, args) => Log.Information("FFmpeg [Pass1]: {Data}", args.Data);
		var result = await conversion.Start(token);

		if (File.Exists(nullOutputPath))
			File.Delete(nullOutputPath);

		if (result.Duration.TotalSeconds < 10)
			throw new Exception($"First pass failed: {result}");
	}
}