using GrinVideoEncoder.Components.Pages;
using GrinVideoEncoder.Models;
using Serilog.Sinks.SystemConsole.Themes;
using System.Diagnostics;
using Xabe.FFmpeg;
using static GrinVideoEncoder.Utils.GpuDetector;

namespace GrinVideoEncoder.Utils;

public partial class VideoEncoder(int qualityLevel, LogFfmpeg log, BehaviorSubject<double?> encodingPercent, CancellationToken appCt)
{
	private int _errorCounter = 0;
	private string _failReason = string.Empty;

	public enum ResultReason
	{
		None,
		GPU,
		Canceled,
		FfmpegFail,
		Exception,
		Misconfiguration,
		BadVideo
	}

	public record struct EncodingResult(bool Success, string ErrorMessage, ResultReason Reason)
	{
		internal readonly string Dump() => System.Text.Json.JsonSerializer.Serialize(this);
	}

	public async Task<EncodingResult> EncodeVideoAsync(string inputFilename, string outputFilename)
	{
		try
		{
			var mediaInfo = await FFmpeg.GetMediaInfo(inputFilename, appCt);
			var gpuType = GpuDetector.DetectGpuVendor();

			if (gpuType is GpuDetector.GpuVendor.Nvidia or GpuDetector.GpuVendor.AMD or GpuDetector.GpuVendor.Vaapi)
			{
				try
				{
					return await ProcessWithGpu(mediaInfo, outputFilename, gpuType, appCt);
				}
				catch (Exception ex) when (ex.Message.Contains("encoder") || ex.Message.Contains("GPU"))
				{
					log.Warning("{GpuType} GPU encoding failed. Error: {ErrorMessage}", gpuType, ex.Message);

					return new EncodingResult(false, $"{gpuType} GPU encoding failed. Error: {ex.Message}", ResultReason.GPU);
				}
			}
			else
			{
				return new EncodingResult(false, "No GPU found", ResultReason.GPU);
			}
		}
		catch (Exception ex)
		{
			return new EncodingResult(false, ex.Message, ResultReason.Exception);
		}
	}

	/// <summary>
	/// Xabe can report success when FFmpeg fails right after starting, because it does not wait for all
	/// of FFmpeg's output before checking for errors. So a successful result must have produced a file.
	/// </summary>
	internal static bool HasOutputFile(string outputPath)
	{
		var file = new FileInfo(outputPath);
		return file.Exists && file.Length > 0;
	}

	/// <summary>
	/// Error message of a failed conversion. Never serialize the exception: System.Text.Json throws on its TargetSite,
	/// which hides the FFmpeg error that Xabe puts in the message.
	/// </summary>
	internal static string DescribeFailure(Exception ex, string failReason) =>
		string.IsNullOrEmpty(failReason) ? ex.Message : $"{ex.Message} - {failReason}";

	/// <summary>
	/// Output parameters for hevc_vaapi. Never add "-tag:v hvc1" here: the muxer then drops the parameter sets
	/// the driver writes in the stream and keeps only the initial ones, which do not match. Every frame of the
	/// video decodes as garbage, while FFmpeg reports success and duration, resolution and FPS stay correct.
	/// </summary>
	/// <param name="tenBit">True to encode in 10 bits (Main 10 profile), see <see cref="GetVaapiOutputBitDepth"/>.</param>
	internal static string[] GetVaapiOutputParameters(int qualityLevel, bool tenBit) =>
	[
		$"-vf format={GetVaapiPixelFormat(tenBit)},hwupload",
		"-c:v hevc_vaapi",
		"-rc_mode CQP",
		$"-global_quality {qualityLevel}",
		"-g 60",
	];

	/// <summary>
	/// Pixel format uploaded to the GPU: hevc_vaapi encodes in the bit depth of what it receives.
	/// </summary>
	internal static string GetVaapiPixelFormat(bool tenBit) => tenBit ? "p010" : "nv12";

	/// <summary>
	/// Bit depth hevc_vaapi produces: 10 bits for a source above 8 bits if the GPU supports Main 10, else 8 bits.
	/// A 12-bit source is reduced to 10 bits, since HEVC Main 10 is the highest profile used.
	/// </summary>
	internal static int GetVaapiOutputBitDepth(int sourceBitDepth, bool supportsMain10) =>
		sourceBitDepth > 8 && supportsMain10 ? 10 : 8;

	/// <summary>
	/// Bit depth the encoding of a source must produce, or null if this GPU path does not guarantee one.
	/// Only VA-API is checked: the Windows encoders are not validated for high bit depth.
	/// </summary>
	public static int? GetExpectedOutputBitDepth(int sourceBitDepth) =>
		GpuDetector.DetectGpuVendor() == GpuVendor.Vaapi
			? GetVaapiOutputBitDepth(sourceBitDepth, GpuDetector.VaapiSupportsMain10)
			: null;

	internal static TimeSpan? ParseFfmpegToTimeSpan(string? log)
	{
		if (string.IsNullOrEmpty(log))
			return null;

		var timeMatch = FindTimeSpandRegex().Match(log);
		if (timeMatch.Success &&
			int.TryParse(timeMatch.Groups[1].Value, out int hours) &&
			int.TryParse(timeMatch.Groups[2].Value, out int minutes) &&
			double.TryParse(timeMatch.Groups[3].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double seconds))
		{
			int wholeSeconds = (int)seconds;
			int milliseconds = (int)((seconds - wholeSeconds) * 1000);
			return new TimeSpan(0, hours, minutes, wholeSeconds, milliseconds);
		}
		return null;
	}

	[System.Text.RegularExpressions.GeneratedRegex(@"time=(\d{2}):(\d{2}):(\d{2}\.\d{2})")]
	private static partial System.Text.RegularExpressions.Regex FindTimeSpandRegex();

	internal string FailReason => _failReason;

	internal void OnNewDataReceived(GpuVendor gpuType, TimeSpan totalTime, string? data, CancellationTokenSource videoCts)
	{
		var curTimespan = ParseFfmpegToTimeSpan(data);
		if (curTimespan == null || totalTime <= TimeSpan.Zero)
		{
			encodingPercent.OnNext(null);
		}
		else
		{
			encodingPercent.OnNext(curTimespan.Value.TotalSeconds / totalTime.TotalSeconds * 100);
		}
		log.Information("FFmpeg [{GpuType} GPU]: {Data}", gpuType, data);

		if (data != null
			&& ((data.Contains("STSC entry", StringComparison.OrdinalIgnoreCase) && data.Contains("is invalid", StringComparison.OrdinalIgnoreCase))
			||
			data.Contains("Error number"))
			)
		{
			_errorCounter++;

			if (_errorCounter > 100)
			{
				_failReason = $"FFmpeg conversion error detected: {data}";
				log.Error("FFmpeg conversion error detected: {Data}", data);
				videoCts.Cancel();
			}
		}
	}

	private async Task<EncodingResult> ProcessWithGpu(IMediaInfo? mediaInfo, string outputPath,
				GpuVendor gpuType, CancellationToken applicationCt)
	{
		if (mediaInfo == null)
			return new EncodingResult(false, "Failed to get media info", ResultReason.BadVideo);
		var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
		if (videoStream == null)
			return new EncodingResult(false, "No video stream found", ResultReason.BadVideo);
		var audioStreams = mediaInfo.AudioStreams;
		var subtitleStreams = mediaInfo.SubtitleStreams;

		if (!outputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
			return new EncodingResult(false, "Please provide an mp4 file", ResultReason.Misconfiguration);

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
					.AddParameter($"-cq {qualityLevel}")
					.AddParameter("-rc-lookahead 32")
					.AddParameter("-spatial-aq 1")
					.AddParameter("-temporal-aq 1")
					.AddParameter("-g 60")
					.AddParameter("-keyint_min 30")
					.AddParameter("-gpu 0");
				break;

			case GpuVendor.AMD:
				conversion
					.AddParameter("-c:v hevc_amf")
					.AddParameter("-rc cqp")
					.AddParameter($"-qp_i {qualityLevel}")
					.AddParameter($"-qp_p {qualityLevel}")
					.AddParameter("-g 60")
					.AddParameter("-keyint_min 30")
					.AddParameter("-pix_fmt yuv420p")
					.AddParameter("-tag:v hvc1");
				break;

			case GpuVendor.Vaapi:
				int sourceBitDepth = mediaInfo.GetBitDepth();
				bool tenBit = GetVaapiOutputBitDepth(sourceBitDepth, GpuDetector.VaapiSupportsMain10) == 10;
				if (sourceBitDepth > 8 && !tenBit)
				{
					log.Warning("{BitDepth}-bit source encoded to 8-bit: the GPU does not support HEVC Main 10", sourceBitDepth);
				}
				conversion.AddParameter($"-vaapi_device {GpuDetector.VaapiDevice}", ParameterPosition.PreInput);
				foreach (string parameter in GetVaapiOutputParameters(qualityLevel, tenBit))
				{
					conversion.AddParameter(parameter);
				}
				break;

			default:
				return new EncodingResult(false, "Unsupported GPU type", ResultReason.GPU);
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

		CancellationTokenSource videoCts = new();
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(applicationCt, videoCts.Token);
		var combinedToken = linkedCts.Token;

		_errorCounter = 0;
		_failReason = string.Empty;
		void handler(object sender, DataReceivedEventArgs args)
		{
			OnNewDataReceived(gpuType, mediaInfo.Duration, args.Data, videoCts);
		}

		conversion.OnDataReceived += handler;
		try
		{
			await conversion.Start(combinedToken);
		}
		catch (TaskCanceledException)
		{
			return string.IsNullOrEmpty(_failReason)
				? new EncodingResult(false, "Task canceled", ResultReason.Canceled)
				: new EncodingResult(false, _failReason, ResultReason.Exception);
		}
		catch (OperationCanceledException)
		{
			return string.IsNullOrEmpty(_failReason)
				? new EncodingResult(false, "Task canceled", ResultReason.Canceled)
				: new EncodingResult(false, _failReason, ResultReason.Exception);
		}
		catch (Exception ex)
		{
			log.Error(ex, "FFmpeg ({GpuType}) conversion failed", gpuType);
			return new EncodingResult(false, DescribeFailure(ex, _failReason), ResultReason.Exception);
		}
		finally
		{
			conversion.OnDataReceived -= handler;
		}

		return HasOutputFile(outputPath)
			? new EncodingResult(true, string.Empty, ResultReason.None)
			: new EncodingResult(false, $"FFmpeg ({gpuType}) did not produce an output file", ResultReason.FfmpegFail);
	}
}