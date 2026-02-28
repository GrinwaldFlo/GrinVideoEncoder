using GrinVideoEncoder.Services;

namespace Tests;

public class VideoProcessorServiceTests
{
	[Fact]
	public void ParseFfmpegToTimeSpan_ValidTimeString_ReturnsParsedTimeSpan()
	{
		string log = "frame=  123 fps= 30 q=28.0 size=   1024kB time=00:01:30.50 bitrate= 2000kbps";

		var result = VideoProcessorService.ParseFfmpegToTimeSpan(log);

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

		var result = VideoProcessorService.ParseFfmpegToTimeSpan(log);

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

		var result = VideoProcessorService.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(TimeSpan.Zero, result.Value);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_NullInput_ReturnsNull()
	{
		var result = VideoProcessorService.ParseFfmpegToTimeSpan(null);

		Assert.Null(result);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_EmptyString_ReturnsNull()
	{
		var result = VideoProcessorService.ParseFfmpegToTimeSpan("");

		Assert.Null(result);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_NoTimeInString_ReturnsNull()
	{
		string log = "frame=  123 fps= 30 q=28.0 size=   1024kB bitrate= 2000kbps";

		var result = VideoProcessorService.ParseFfmpegToTimeSpan(log);

		Assert.Null(result);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_InvalidFormat_ReturnsNull()
	{
		string log = "time=invalid";

		var result = VideoProcessorService.ParseFfmpegToTimeSpan(log);

		Assert.Null(result);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_FractionalSeconds_HandlesCorrectly()
	{
		string log = "time=00:00:10.75";

		var result = VideoProcessorService.ParseFfmpegToTimeSpan(log);

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

		var result = VideoProcessorService.ParseFfmpegToTimeSpan(log);

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

		var result = VideoProcessorService.ParseFfmpegToTimeSpan(log);

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

		var result = VideoProcessorService.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(5400.0, result.Value.TotalSeconds);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_MultipleTimeOccurrences_ParsesFirst()
	{
		string log = "time=00:01:00.00 time=00:02:00.00";

		var result = VideoProcessorService.ParseFfmpegToTimeSpan(log);

		Assert.NotNull(result);
		Assert.Equal(1, result.Value.Minutes);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_WhitespaceOnly_ReturnsNull()
	{
		var result = VideoProcessorService.ParseFfmpegToTimeSpan("   ");

		Assert.Null(result);
	}

	[Fact]
	public void ParseFfmpegToTimeSpan_PartialTimeFormat_ReturnsNull()
	{
		var result = VideoProcessorService.ParseFfmpegToTimeSpan("time=00:01");

		Assert.Null(result);
	}
}
