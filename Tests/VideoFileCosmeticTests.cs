using GrinVideoEncoder.Models;

namespace Tests;

public class VideoFileCosmeticTests
{
	[Theory]
	[InlineData(null, "-")]
	[InlineData(0L, "0 B")]
	[InlineData(512L, "512 B")]
	[InlineData(1024L, "1 KB")]
	[InlineData(1536L, "1.5 KB")]
	[InlineData(1_048_576L, "1 MB")]
	[InlineData(1_572_864L, "1.5 MB")]
	[InlineData(1_073_741_824L, "1 GB")]
	[InlineData(1_099_511_627_776L, "1 TB")]
	public void FormatBytes_ReturnsExpected(long? bytes, string expected)
	{
		Assert.Equal(expected, VideoFile.FormatBytes(bytes));
	}

	[Fact]
	public void FormatBytes_LargeValue_FormatsAsGB()
	{
		long bytes = 5_368_709_120L; // 5 GB
		string result = VideoFile.FormatBytes(bytes);
		Assert.Equal("5 GB", result);
	}

	[Fact]
	public void FileSizeOriginalFormatted_ReturnsFormattedString()
	{
		var video = new VideoFile { FileSizeOriginal = 1_048_576 };

		Assert.Equal("1 MB", video.FileSizeOriginalFormatted);
	}

	[Fact]
	public void FileSizeCompressedFormatted_WhenNull_ReturnsDash()
	{
		var video = new VideoFile { FileSizeCompressed = null };

		Assert.Equal("-", video.FileSizeCompressedFormatted);
	}

	[Fact]
	public void FileSizeCompressedFormatted_WhenSet_ReturnsFormattedString()
	{
		var video = new VideoFile { FileSizeCompressed = 524_288 };

		Assert.Equal("512 KB", video.FileSizeCompressedFormatted);
	}

	[Fact]
	public void DurationFormatted_WhenNull_ReturnsDash()
	{
		var video = new VideoFile { DurationSeconds = null };

		Assert.Equal("-", video.DurationFormatted);
	}

	[Fact]
	public void DurationFormatted_LessThanOneHour_ReturnsMinutesAndSeconds()
	{
		var video = new VideoFile { DurationSeconds = 125 }; // 2:05

		Assert.Equal("2:05", video.DurationFormatted);
	}

	[Fact]
	public void DurationFormatted_ExactlyOneHour_ReturnsHourFormat()
	{
		var video = new VideoFile { DurationSeconds = 3600 };

		Assert.Equal("1:00:00", video.DurationFormatted);
	}

	[Fact]
	public void DurationFormatted_MultipleHours_ReturnsFullFormat()
	{
		var video = new VideoFile { DurationSeconds = 7384 }; // 2:03:04

		Assert.Equal("2:03:04", video.DurationFormatted);
	}

	[Fact]
	public void DurationFormatted_ZeroSeconds_ReturnsZero()
	{
		var video = new VideoFile { DurationSeconds = 0 };

		Assert.Equal("0:00", video.DurationFormatted);
	}

	[Fact]
	public void DurationFormatted_LessThanOneMinute_ReturnsSecondsOnly()
	{
		var video = new VideoFile { DurationSeconds = 45 };

		Assert.Equal("0:45", video.DurationFormatted);
	}

	[Fact]
	public void Resolution_WhenBothSet_ReturnsFormatted()
	{
		var video = new VideoFile { Width = 1920, Height = 1080 };

		Assert.Equal("1920×1080", video.Resolution);
	}

	[Fact]
	public void Resolution_WhenWidthNull_ReturnsDash()
	{
		var video = new VideoFile { Width = null, Height = 1080 };

		Assert.Equal("-", video.Resolution);
	}

	[Fact]
	public void Resolution_WhenHeightNull_ReturnsDash()
	{
		var video = new VideoFile { Width = 1920, Height = null };

		Assert.Equal("-", video.Resolution);
	}

	[Fact]
	public void Resolution_WhenBothNull_ReturnsDash()
	{
		var video = new VideoFile { Width = null, Height = null };

		Assert.Equal("-", video.Resolution);
	}

	[Theory]
	[InlineData(CompressionStatus.Original, "#6c757d")]
	[InlineData(CompressionStatus.Compressed, "#28a745")]
	[InlineData(CompressionStatus.FailedToCompress, "#dc3545")]
	[InlineData(CompressionStatus.Bigger, "#ffc107")]
	[InlineData(CompressionStatus.Removed, "#6c757d")]
	[InlineData(CompressionStatus.ToProcess, "#007bff")]
	[InlineData(CompressionStatus.Processing, "#17a2b8")]
	[InlineData(CompressionStatus.Kept, "#20c997")]
	public void StatusColor_ReturnsCorrectColor(CompressionStatus status, string expectedColor)
	{
		var video = new VideoFile { Status = status };

		Assert.Equal(expectedColor, video.StatusColor);
	}

	[Theory]
	[InlineData(CompressionStatus.Original, "Original")]
	[InlineData(CompressionStatus.Compressed, "Compressed")]
	[InlineData(CompressionStatus.FailedToCompress, "Failed")]
	[InlineData(CompressionStatus.Bigger, "Bigger")]
	[InlineData(CompressionStatus.Removed, "Removed")]
	[InlineData(CompressionStatus.ToProcess, "To Process")]
	[InlineData(CompressionStatus.Processing, "Processing")]
	[InlineData(CompressionStatus.Kept, "Kept")]
	public void StatusDisplayName_ReturnsCorrectName(CompressionStatus status, string expectedName)
	{
		var video = new VideoFile { Status = status };

		Assert.Equal(expectedName, video.StatusDisplayName);
	}

	[Theory]
	[InlineData(CompressionStatus.Original, "backup")]
	[InlineData(CompressionStatus.Compressed, "compress")]
	[InlineData(CompressionStatus.FailedToCompress, "error")]
	[InlineData(CompressionStatus.Bigger, "trending_up")]
	[InlineData(CompressionStatus.Removed, "delete")]
	[InlineData(CompressionStatus.ToProcess, "schedule")]
	[InlineData(CompressionStatus.Processing, "hourglass_bottom")]
	[InlineData(CompressionStatus.Kept, "check_circle")]
	public void StatusIcon_ReturnsCorrectIcon(CompressionStatus status, string expectedIcon)
	{
		var video = new VideoFile { Status = status };

		Assert.Equal(expectedIcon, video.StatusIcon);
	}

	[Theory]
	[InlineData(1L, "1 B")]
	[InlineData(999L, "999 B")]
	public void FormatBytes_SmallValues_FormatsAsBytes(long bytes, string expected)
	{
		Assert.Equal(expected, VideoFile.FormatBytes(bytes));
	}

	[Fact]
	public void FormatBytes_ExactlyOneTB_FormatCorrectly()
	{
		Assert.Equal("1 TB", VideoFile.FormatBytes(1_099_511_627_776L));
	}

	[Fact]
	public void FormatBytes_VeryLargeValue_FormatsAsTB()
	{
		long tenTB = 10_995_116_277_760L;
		string result = VideoFile.FormatBytes(tenTB);
		Assert.Equal("10 TB", result);
	}

	[Fact]
	public void DurationFormatted_ExactlyOneMinute_Formats()
	{
		var video = new VideoFile { DurationSeconds = 60 };

		Assert.Equal("1:00", video.DurationFormatted);
	}

	[Fact]
	public void DurationFormatted_LargeValue_HandlesCorrectly()
	{
		var video = new VideoFile { DurationSeconds = 86400 }; // 24 hours

		Assert.Equal("24:00:00", video.DurationFormatted);
	}

	[Fact]
	public void DurationFormatted_OneSecond_Formats()
	{
		var video = new VideoFile { DurationSeconds = 1 };

		Assert.Equal("0:01", video.DurationFormatted);
	}

	[Fact]
	public void DurationFormatted_59Minutes59Seconds_NoHourPrefix()
	{
		var video = new VideoFile { DurationSeconds = 3599 }; // 59:59

		Assert.Equal("59:59", video.DurationFormatted);
	}

	[Fact]
	public void Resolution_CommonResolutions_FormatCorrectly()
	{
		Assert.Equal("3840×2160", new VideoFile { Width = 3840, Height = 2160 }.Resolution);
		Assert.Equal("1280×720", new VideoFile { Width = 1280, Height = 720 }.Resolution);
		Assert.Equal("640×480", new VideoFile { Width = 640, Height = 480 }.Resolution);
	}

	[Fact]
	public void StatusColor_AllStatuses_AreValidHexColors()
	{
		foreach (CompressionStatus status in Enum.GetValues<CompressionStatus>())
		{
			var video = new VideoFile { Status = status };
			Assert.Matches(@"^#[0-9a-fA-F]{6}$", video.StatusColor);
		}
	}

	[Fact]
	public void StatusDisplayName_AllStatuses_AreNonEmpty()
	{
		foreach (CompressionStatus status in Enum.GetValues<CompressionStatus>())
		{
			var video = new VideoFile { Status = status };
			Assert.False(string.IsNullOrEmpty(video.StatusDisplayName));
		}
	}

	[Fact]
	public void StatusIcon_AllStatuses_AreNonEmpty()
	{
		foreach (CompressionStatus status in Enum.GetValues<CompressionStatus>())
		{
			var video = new VideoFile { Status = status };
			Assert.False(string.IsNullOrEmpty(video.StatusIcon));
		}
	}
}
