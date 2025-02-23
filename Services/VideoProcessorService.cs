﻿using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using static GrinVideoEncoder.Utils.GpuDetector;

namespace GrinVideoEncoder.Services;

public class VideoProcessorService(IAppSettings settings)
{
	public bool ReadyToProcess { get; private set; } = true;

	public async Task ProcessVideo(string filePath, CancellationToken token)
	{
		if (!ReadyToProcess)
			return;

		await FfmpegDownload();

		var gpuType = GpuDetector.DetectGpuVendor();
		FileNamer filename = new(settings, filePath);
		try
		{
			await PrepareProcessing(filename);

			var mediaInfo = await FFmpeg.GetMediaInfo(filename.ProcessingPath, token);
			var videoStream = mediaInfo.VideoStreams.FirstOrDefault()
				?? throw new Exception("No video stream found");

			if (gpuType is GpuDetector.GpuVendor.Nvidia or GpuDetector.GpuVendor.AMD)
			{
				try
				{
					await ProcessWithGpu(videoStream, mediaInfo.AudioStreams, filename.TempPath, gpuType, token);
				}
				catch (Exception ex) when (ex.Message.Contains("encoder") || ex.Message.Contains("GPU"))
				{
					Log.Warning("{GpuType} GPU encoding failed. Falling back to CPU encoding. Error: {ErrorMessage}", gpuType, ex.Message);
					await ProcessWithCpu(videoStream, mediaInfo.AudioStreams, filename, token);
				}
			}
			else
			{
				await ProcessWithCpu(videoStream, mediaInfo.AudioStreams, filename, token);
			}

			FinalizeProcessing(filename);
		}
		catch (Exception ex)
		{
			HandleProcessingError(ex, filename);
		}
		finally
		{
			// WIP
		}
	}

	private static void FinalizeProcessing(FileNamer filename)
	{
		File.Move(filename.TempPath, filename.OutputPath);
		File.Move(filename.ProcessingPath, filename.TrashPath);
		Log.Information("Successfully processed {FinalPath}", filename.OutputPath);
	}

	private static void HandleProcessingError(Exception ex, FileNamer file)
	{
		Log.Error(ex, "Processing error");
		if (File.Exists(file.TempPath))
			File.Delete(file.TempPath);
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

	private static async Task ProcessWithCpu(IVideoStream videoStream, IEnumerable<IAudioStream> audioStreams, FileNamer file, CancellationToken token)
	{
		videoStream.SetCodec(VideoCodec.hevc)
				  .SetBitrate(3000000);

		await RunFirstPass(videoStream, file, token);
		await RunSecondPass(videoStream, audioStreams, file, token);
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

	private static async Task RunFirstPass(IVideoStream videoStream, FileNamer file, CancellationToken token)
	{
		var conversion = FFmpeg.Conversions.New()
			.AddStream(videoStream)
			.AddParameter($"-pass 1")
			.AddParameter($"-passlogfile \"{file.StatFileName}\"")
			.AddParameter("-an")
			.SetOutput(file.TempFirstPassPath);

		conversion.OnDataReceived += (sender, args) => Log.Information("FFmpeg [Pass1]: {Data}", args.Data);
		var result = await conversion.Start(token);

		if (File.Exists(file.TempFirstPassPath))
			File.Delete(file.TempFirstPassPath);

		if (result.Duration.TotalSeconds < 10)
			throw new Exception($"First pass failed: {result}");
	}

	private static async Task RunSecondPass(IVideoStream videoStream, IEnumerable<IAudioStream> audioStreams, FileNamer file, CancellationToken token)
	{
		var conversion = FFmpeg.Conversions.New()
			.AddStream(videoStream)
			.AddParameter($"-pass 2")
			.AddParameter($"-passlogfile \"{file.StatFileName}\"")
			.SetOutput(file.TempPath);

		foreach (var audioStream in audioStreams)
		{
			conversion.AddStream(audioStream);
		}

		conversion.OnDataReceived += (sender, args) => Log.Information("FFmpeg [Pass2]: {Data}", args.Data);
		var result = await conversion.Start(token);

		if (result.Duration.TotalSeconds < 10)
			throw new Exception($"Second pass failed: {result}");
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
	private async Task FfmpegDownload()
	{
		string ffmpegPath = Path.Combine(settings.TempPath, "ffmpeg");
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
}