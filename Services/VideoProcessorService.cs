using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace GrinVideoEncoder.Services;

public class VideoProcessorService
{
	private readonly string _processingPath = string.Empty;
	private readonly string _outputPath = string.Empty;
	private readonly string _tempPath = string.Empty;
	private readonly string _failedPath = string.Empty;

	public bool ReadyToProcess { get; private set; } = false;

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

	public async Task ProcessVideo(string filePath, CancellationToken token)
	{
		if (!ReadyToProcess)
			return;

		string? processingPath = null;
		string? outputPath = null;
		try
		{
			//if (!GlobUtils.IsFileReady(filePath))
			//	return;
			Log.Information($"Waiting {filePath} is ready");

			await WaitForFile(filePath, token);

			processingPath = Path.Combine(_processingPath, Path.GetFileName(filePath));
			File.Move(filePath, processingPath);

			Log.Information($"Started processing {processingPath}");

			string outputFileName = Path.GetFileNameWithoutExtension(filePath) + "_encoded.mp4";
			outputPath = Path.Combine(_tempPath, outputFileName);

			// Get media info
			var mediaInfo = await FFmpeg.GetMediaInfo(processingPath, token);
			var videoStream = (mediaInfo.VideoStreams.FirstOrDefault()?.SetCodec(VideoCodec.hevc)) ?? throw new Exception("No video stream found");

			// First pass
			var conversion1 = FFmpeg.Conversions.New()
				.AddStream(videoStream)
				.AddParameter("-b:v 3M")
				.AddParameter("-x265-params pass=1:stats=stats.log")
				.SetOutput(Path.Combine(_tempPath, "NUL")); // Output to null

			conversion1.OnDataReceived += (sender, args) => Log.Information($"FFmpeg [Pass1]: {args.Data}");
			var result1 = await conversion1.Start(token);

			if (result1.Duration.TotalSeconds < 10) throw new Exception($"First pass failed: {result1}");

			// Second pass
			var conversion2 = FFmpeg.Conversions.New()
				.AddStream(videoStream)
				.AddParameter("-b:v 3M")
				.AddParameter("-x265-params pass=2:stats=stats.log")
				.SetOutput(outputPath);

			conversion2.OnDataReceived += (sender, args) => Log.Information($"FFmpeg [Pass2]: {args.Data}");
			var result2 = await conversion2.Start(token);

			if (result2.Duration.TotalSeconds < 10) throw new Exception($"Second pass failed: {result2}");

			// Move to output
			string finalPath = Path.Combine(_outputPath, outputFileName);
			File.Move(outputPath, finalPath);
			Log.Information($"Successfully processed {finalPath}");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Processing error");
			if (File.Exists(outputPath)) File.Delete(outputPath);
			if (processingPath != null && File.Exists(processingPath))
			{
				string failedPath = Path.Combine(_failedPath, Path.GetFileName(processingPath));
				File.Move(processingPath, failedPath, true);
			}
		}
		finally
		{
			CleanupTempFiles();
		}
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

	public async Task FfmpegDownload()
	{
		// Download FFmpeg if not exists
		string ffmpegPath = Path.Combine(_tempPath, "ffmpeg");
		if (!Directory.Exists(ffmpegPath))
		{
			Directory.CreateDirectory(ffmpegPath);
		}
		FFmpeg.SetExecutablesPath(ffmpegPath);

		if (!File.Exists(Path.Combine(ffmpegPath, "ffmpeg")))
		{
			await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegPath);
		}
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
			catch { }

			await Task.Delay(5000);
		}
	}
}
