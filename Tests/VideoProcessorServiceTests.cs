using GrinVideoEncoder.Services;
using GrinVideoEncoder.Utils;

namespace Tests;

public class VideoProcessorServiceTests
{
	[Fact]
	public void GetVaapiOutputParameters_UsesEncoderAndQuality()
	{
		string[] parameters = VideoEncoder.GetVaapiOutputParameters(28);

		Assert.Contains("-c:v hevc_vaapi", parameters);
		Assert.Contains("-global_quality 28", parameters);
	}

	[Fact]
	public void GetVaapiOutputParameters_NeverTagsHvc1_BecauseItCorruptsTheVideo()
	{
		Assert.DoesNotContain(VideoEncoder.GetVaapiOutputParameters(28), parameter => parameter.Contains("hvc1"));
	}

	[Fact]
	public void HasOutputFile_MissingFile_ReturnsFalse()
	{
		Assert.False(VideoEncoder.HasOutputFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4")));
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(1, true)]
	public void HasOutputFile_ExistingFile_RequiresContent(int size, bool expected)
	{
		string filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4");
		File.WriteAllBytes(filePath, new byte[size]);
		try
		{
			Assert.Equal(expected, VideoEncoder.HasOutputFile(filePath));
		}
		finally
		{
			File.Delete(filePath);
		}
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_ValidTimeString_ReturnsParsedTimeSpan()
	{
		string log = "frame=  123 fps= 30 q=28.0 size=   1024kB time=00:01:30.50 bitrate= 2000kbps";

		var result = VideoEncoder.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(0, result.Value.Hours);
		Assert.Equal(1, result.Value.Minutes);
		Assert.Equal(30, result.Value.Seconds);
		Assert.Equal(500, result.Value.Milliseconds);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_HoursMinutesSeconds_ReturnsParsedTimeSpan()
	{
		string log = "frame= 5000 fps= 60 q=25.0 size=  50000kB time=02:15:45.00 bitrate= 5000kbps";

		var result = VideoEncoder.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(2, result.Value.Hours);
		Assert.Equal(15, result.Value.Minutes);
		Assert.Equal(45, result.Value.Seconds);
		Assert.Equal(0, result.Value.Milliseconds);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_ZeroTime_ReturnsZeroTimeSpan()
	{
		string log = "time=00:00:00.00";

		var result = VideoEncoder.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(TimeSpan.Zero, result.Value);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_NullInput_ReturnsNull()
	{
		var result = VideoEncoder.ParseFfmpegToTimeSpan(null);

		Assert.Null(result);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_EmptyString_ReturnsNull()
	{
		var result = VideoEncoder.ParseFfmpegToTimeSpan("");

		Assert.Null(result);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_NoTimeInString_ReturnsNull()
	{
		string log = "frame=  123 fps= 30 q=28.0 size=   1024kB bitrate= 2000kbps";

		var result = VideoEncoder.ParseFfmpegToTimeSpan(log);

		Assert.Null(result);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_InvalidFormat_ReturnsNull()
	{
		string log = "time=invalid";

		var result = VideoEncoder.ParseFfmpegToTimeSpan(log);

		Assert.Null(result);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_FractionalSeconds_HandlesCorrectly()
	{
		string log = "time=00:00:10.75";

		var result = VideoEncoder.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(0, result.Value.Hours);
		Assert.Equal(0, result.Value.Minutes);
		Assert.Equal(10, result.Value.Seconds);
		Assert.Equal(750, result.Value.Milliseconds);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_TypicalProgressLine_Parses()
	{
		string log = "frame= 1000 fps=120 q=23.0 Lsize=   25600kB time=00:05:33.24 bitrate= 630.2kbits/s speed=4.01x";

		var result = VideoEncoder.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(0, result.Value.Hours);
		Assert.Equal(5, result.Value.Minutes);
		Assert.Equal(33, result.Value.Seconds);
		Assert.Equal(240, result.Value.Milliseconds);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_LongDuration_Parses()
	{
		string log = "time=99:59:59.99";

		var result = VideoEncoder.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(99, (int)result.Value.TotalHours);
		Assert.Equal(59, result.Value.Minutes);
		Assert.Equal(59, result.Value.Seconds);
		Assert.Equal(990, result.Value.Milliseconds);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_TotalSeconds_IsAccurate()
	{
		string log = "time=01:30:00.00";

		var result = VideoEncoder.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(5400.0, result.Value.TotalSeconds);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_MultipleTimeOccurrences_ParsesFirst()
	{
		string log = "time=00:01:00.00 time=00:02:00.00";

		var result = VideoEncoder.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(1, result.Value.Minutes);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_WhitespaceOnly_ReturnsNull()
	{
		var result = VideoEncoder.ParseFfmpegToTimeSpan("   ");

		Assert.Null(result);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_PartialTimeFormat_ReturnsNull()
	{
		var result = VideoEncoder.ParseFfmpegToTimeSpan("time=00:01");

		Assert.Null(result);
	}

	[Fact]
	public void DescribeFailure_ThrownException_KeepsTheMessage()
	{
		// A thrown exception has a TargetSite, which made the former JSON serialization throw and hide the message
		Exception thrown;
		try
		{
			throw new InvalidOperationException("Conversion failed: [hevc_vaapi] No usable encoding profile found.");
		}
		catch (Exception ex)
		{
			thrown = ex;
		}

		Assert.Equal("Conversion failed: [hevc_vaapi] No usable encoding profile found.", VideoEncoder.DescribeFailure(thrown, string.Empty));
	}

	[Fact]
	public void DescribeFailure_WithFailReason_AppendsIt()
	{
		Assert.Equal("boom - too many errors", VideoEncoder.DescribeFailure(new Exception("boom"), "too many errors"));
	}

	[Fact]
	public void ComputeFpsRange_ConstantFrameRate_ReturnsSameMinAndMax()
	{
		(double min, double max) = VideoProcessorService.ComputeFpsRange("0.000000\n0.040000\n0.080000\n0.120000\n");

		Assert.Equal(25, min, 3);
		Assert.Equal(25, max, 3);
	}

	[Fact]
	public void ComputeFpsRange_VariableFrameRate_ReturnsRange()
	{
		// 25 FPS then 50 FPS, with ffprobe's trailing commas and an N/A line
		(double min, double max) = VideoProcessorService.ComputeFpsRange("0.000000,\n0.040000,\nN/A\n0.080000,\n0.100000,\n");

		Assert.Equal(25, min, 3);
		Assert.Equal(50, max, 3);
	}

	[Theory]
	[InlineData("")]
	[InlineData("0.000000\n")]
	[InlineData("1.0\n1.0\n")]
	public void ComputeFpsRange_NotEnoughFrames_ReturnsZero(string output)
	{
		Assert.Equal((0.0, 0.0), VideoProcessorService.ComputeFpsRange(output));
	}
}