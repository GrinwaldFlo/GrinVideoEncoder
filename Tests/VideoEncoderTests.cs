using GrinVideoEncoder;
using GrinVideoEncoder.Services;
using GrinVideoEncoder.Utils;
using System.Reactive.Subjects;
using static GrinVideoEncoder.Utils.GpuDetector;

namespace Tests;

public class VideoEncoderTests : IDisposable
{
	private const string PROGRESS_LINE = "frame=  100 fps=30 q=28.0 size=1024kB time=00:00:30.00 bitrate=2000kbps";
	private const string ERROR_LINE = "Error number -22 occurred";

	private readonly string _tempDir;
	private readonly BehaviorSubject<double?> _percent = new(null);
	private readonly VideoEncoder _encoder;

	public VideoEncoderTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "GrinTests_Encoder_" + Guid.NewGuid().ToString("N"));
		_encoder = new VideoEncoder(23, new LogFfmpeg(new AppSettings { WorkPath = _tempDir }), _percent, CancellationToken.None);
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void OnNewDataReceived_ProgressLine_PublishesPercent()
	{
		using var cts = new CancellationTokenSource();

		_encoder.OnNewDataReceived(GpuVendor.Vaapi, TimeSpan.FromSeconds(60), PROGRESS_LINE, cts);

		Assert.Equal(50, _percent.Value!.Value, 3);
	}

	[Fact]
	public void OnNewDataReceived_LineWithoutTime_PublishesNull()
	{
		using var cts = new CancellationTokenSource();
		_percent.OnNext(10);

		_encoder.OnNewDataReceived(GpuVendor.Vaapi, TimeSpan.FromSeconds(60), "Stream mapping:", cts);

		Assert.Null(_percent.Value);
	}

	/// <summary>
	/// A video whose duration could not be read must not show an infinite progress.
	/// </summary>
	[Fact]
	public void OnNewDataReceived_ZeroDuration_PublishesNull()
	{
		using var cts = new CancellationTokenSource();

		_encoder.OnNewDataReceived(GpuVendor.Vaapi, TimeSpan.Zero, PROGRESS_LINE, cts);

		Assert.Null(_percent.Value);
	}

	[Fact]
	public void OnNewDataReceived_NullData_DoesNotThrow()
	{
		using var cts = new CancellationTokenSource();

		_encoder.OnNewDataReceived(GpuVendor.Vaapi, TimeSpan.FromSeconds(60), null, cts);

		Assert.False(cts.IsCancellationRequested);
	}

	[Fact]
	public void OnNewDataReceived_HundredErrors_DoesNotCancel()
	{
		using var cts = new CancellationTokenSource();

		for (int i = 0; i < 100; i++)
			_encoder.OnNewDataReceived(GpuVendor.Vaapi, TimeSpan.FromSeconds(60), ERROR_LINE, cts);

		Assert.False(cts.IsCancellationRequested);
		Assert.Empty(_encoder.FailReason);
	}

	[Theory]
	[InlineData(ERROR_LINE)]
	[InlineData("[mov] STSC entry 1 is INVALID (first=0 count=1 id=1)")]
	public void OnNewDataReceived_MoreThanHundredErrors_CancelsWithReason(string line)
	{
		using var cts = new CancellationTokenSource();

		for (int i = 0; i < 101; i++)
			_encoder.OnNewDataReceived(GpuVendor.Vaapi, TimeSpan.FromSeconds(60), line, cts);

		Assert.True(cts.IsCancellationRequested);
		Assert.Contains(line, _encoder.FailReason);
	}

	[Fact]
	public async Task EncodeVideoAsync_UnreadableInput_FailsWithoutThrowing()
	{
		Directory.CreateDirectory(_tempDir);
		string input = Path.Combine(_tempDir, "input.mp4");
		File.WriteAllText(input, "not a video");

		var result = await _encoder.EncodeVideoAsync(input, Path.Combine(_tempDir, "output.mp4"));

		Assert.False(result.Success);
		Assert.NotEqual(VideoEncoder.ResultReason.None, result.Reason);
		Assert.False(File.Exists(Path.Combine(_tempDir, "output.mp4")));
	}

	[Fact]
	public void DetectGpuVendor_Linux_IsVaapi()
	{
		if (!OperatingSystem.IsLinux())
			return;

		Assert.Equal(GpuVendor.Vaapi, DetectGpuVendor());
	}

	[Fact]
	public void GetExpectedOutputBitDepth_Linux_FollowsVaapi()
	{
		if (!OperatingSystem.IsLinux())
			return;

		Assert.Equal(8, VideoEncoder.GetExpectedOutputBitDepth(8));
		Assert.Equal(VideoEncoder.GetVaapiOutputBitDepth(10, VaapiSupportsMain10), VideoEncoder.GetExpectedOutputBitDepth(10));
	}
}
