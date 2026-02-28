using GrinVideoEncoder.Models;

namespace Tests;

public class VideoFileComputedPropertiesTests
{
	[Fact]
	public void CompressionFactor_WithValidSizes_ReturnsCorrectPercentage()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 1000,
			FileSizeCompressed = 500
		};

		Assert.Equal(100.0, video.CompressionFactor);
	}

	[Fact]
	public void CompressionFactor_CompressedIsNull_ReturnsNull()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 1000,
			FileSizeCompressed = null
		};

		Assert.Null(video.CompressionFactor);
	}

	[Fact]
	public void CompressionFactor_OriginalIsZero_ReturnsNull()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 0,
			FileSizeCompressed = 500
		};

		Assert.Null(video.CompressionFactor);
	}

	[Fact]
	public void CompressionFactor_SameSizes_ReturnsZero()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 1000,
			FileSizeCompressed = 1000
		};

		Assert.Equal(0.0, video.CompressionFactor);
	}

	[Fact]
	public void CompressionFactor_CompressedLargerThanOriginal_ReturnsNegative()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 500,
			FileSizeCompressed = 1000
		};

		Assert.True(video.CompressionFactor < 0);
	}

	[Fact]
	public void TotalPixels_WithValidDimensions_ReturnsProduct()
	{
		var video = new VideoFile { Width = 1920, Height = 1080 };

		Assert.Equal(1920L * 1080, video.TotalPixels);
	}

	[Fact]
	public void TotalPixels_WidthIsNull_ReturnsNull()
	{
		var video = new VideoFile { Width = null, Height = 1080 };

		Assert.Null(video.TotalPixels);
	}

	[Fact]
	public void TotalPixels_HeightIsNull_ReturnsNull()
	{
		var video = new VideoFile { Width = 1920, Height = null };

		Assert.Null(video.TotalPixels);
	}

	[Fact]
	public void TotalPixels_WidthIsZero_ReturnsNull()
	{
		var video = new VideoFile { Width = 0, Height = 1080 };

		Assert.Null(video.TotalPixels);
	}

	[Fact]
	public void TotalPixels_HeightIsZero_ReturnsNull()
	{
		var video = new VideoFile { Width = 1920, Height = 0 };

		Assert.Null(video.TotalPixels);
	}

	[Fact]
	public void QualityRatioOriginal_WithAllValues_ReturnsExpected()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			DurationSeconds = 100,
			Fps = 30.0,
			Width = 1920,
			Height = 1080
		};

		// (10_000_000 / 100 / (1920*1080)) * 1000.0 * (30.0 / 30.0)
		double expected = 10_000_000.0 / 100.0 / (1920.0 * 1080.0) * 1000.0 * (30.0 / 30.0);
		Assert.Equal(expected, video.QualityRatioOriginal!.Value, 6);
	}

	[Fact]
	public void QualityRatioOriginal_DurationIsNull_ReturnsNull()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			DurationSeconds = null,
			Fps = 30.0,
			Width = 1920,
			Height = 1080
		};

		Assert.Null(video.QualityRatioOriginal);
	}

	[Fact]
	public void QualityRatioOriginal_DurationIsZero_ReturnsNull()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			DurationSeconds = 0,
			Fps = 30.0,
			Width = 1920,
			Height = 1080
		};

		Assert.Null(video.QualityRatioOriginal);
	}

	[Fact]
	public void QualityRatioOriginal_FpsIsNull_ReturnsNull()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			DurationSeconds = 100,
			Fps = null,
			Width = 1920,
			Height = 1080
		};

		Assert.Null(video.QualityRatioOriginal);
	}

	[Fact]
	public void QualityRatioOriginal_DimensionsNull_ReturnsNull()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			DurationSeconds = 100,
			Fps = 30.0,
			Width = null,
			Height = null
		};

		Assert.Null(video.QualityRatioOriginal);
	}

	[Fact]
	public void QualityRatioCompressed_WithAllValues_ReturnsExpected()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			FileSizeCompressed = 5_000_000,
			DurationSeconds = 100,
			Fps = 30.0,
			Width = 1920,
			Height = 1080
		};

		double expected = 5_000_000.0 / 100.0 / (1920.0 * 1080.0) * 1000.0 * (30.0 / 30.0);
		Assert.Equal(expected, video.QualityRatioCompressed!.Value, 6);
	}

	[Fact]
	public void QualityRatioCompressed_CompressedIsNull_ReturnsNull()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			FileSizeCompressed = null,
			DurationSeconds = 100,
			Fps = 30.0,
			Width = 1920,
			Height = 1080
		};

		Assert.Null(video.QualityRatioCompressed);
	}

	[Fact]
	public void QualityRatio_WhenCompressed_ReturnsCompressedRatio()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			FileSizeCompressed = 5_000_000,
			DurationSeconds = 100,
			Fps = 30.0,
			Width = 1920,
			Height = 1080
		};

		Assert.Equal(video.QualityRatioCompressed, video.QualityRatio);
	}

	[Fact]
	public void QualityRatio_WhenNotCompressed_ReturnsOriginalRatio()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			FileSizeCompressed = null,
			DurationSeconds = 100,
			Fps = 30.0,
			Width = 1920,
			Height = 1080
		};

		Assert.Equal(video.QualityRatioOriginal, video.QualityRatio);
	}

	[Fact]
	public void QualityRatioOriginal_HigherFps_ReturnsLowerRatio()
	{
		var video30 = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			DurationSeconds = 100,
			Fps = 30.0,
			Width = 1920,
			Height = 1080
		};

		var video60 = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			DurationSeconds = 100,
			Fps = 60.0,
			Width = 1920,
			Height = 1080
		};

		Assert.True(video60.QualityRatioOriginal < video30.QualityRatioOriginal);
	}

	[Fact]
	public void FullPath_CombinesDirectoryAndFilename()
	{
		var video = new VideoFile
		{
			DirectoryPath = Path.Combine("C:", "Videos"),
			Filename = "test.mp4"
		};

		string expected = Path.GetFullPath(Path.Combine("C:", "Videos", "test.mp4"));
		Assert.Equal(expected, video.FullPath);
	}

	[Fact]
	public void DefaultValues_AreCorrect()
	{
		var video = new VideoFile();

		Assert.Equal(string.Empty, video.DirectoryPath);
		Assert.Equal(string.Empty, video.Filename);
		Assert.Equal(CompressionStatus.Original, video.Status);
		Assert.Equal(SanityStatus.Unknown, video.Sanity);
		Assert.NotEqual(Guid.Empty, video.Id);
	}

	[Fact]
	public void CompressionFactor_SmallCompression_ReturnsSmallPercentage()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 1000,
			FileSizeCompressed = 990
		};

		double expected = ((1000.0 / 990.0) - 1) * 100.0;
		Assert.Equal(expected, video.CompressionFactor!.Value, 6);
	}

	[Fact]
	public void TotalPixels_4K_ReturnsCorrectValue()
	{
		var video = new VideoFile { Width = 3840, Height = 2160 };

		Assert.Equal(3840L * 2160, video.TotalPixels);
	}

	[Fact]
	public void TotalPixels_NegativeWidth_ReturnsNull()
	{
		var video = new VideoFile { Width = -1, Height = 1080 };

		Assert.Null(video.TotalPixels);
	}

	[Fact]
	public void TotalPixels_NegativeHeight_ReturnsNull()
	{
		var video = new VideoFile { Width = 1920, Height = -1 };

		Assert.Null(video.TotalPixels);
	}

	[Fact]
	public void QualityRatioOriginal_VerySmallFile_ReturnsSmallRatio()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 1,
			DurationSeconds = 1000,
			Fps = 30.0,
			Width = 1920,
			Height = 1080
		};

		Assert.NotNull(video.QualityRatioOriginal);
		Assert.True(video.QualityRatioOriginal < 1.0);
	}

	[Fact]
	public void Id_TwoInstances_AreDifferent()
	{
		var video1 = new VideoFile();
		var video2 = new VideoFile();

		Assert.NotEqual(video1.Id, video2.Id);
	}

	[Fact]
	public void IndexedAt_DefaultIsRecentUtc()
	{
		var before = DateTime.UtcNow.AddSeconds(-1);
		var video = new VideoFile();
		var after = DateTime.UtcNow.AddSeconds(1);

		Assert.InRange(video.IndexedAt, before, after);
	}

	[Fact]
	public void EncodingErrorMessage_DefaultIsNull()
	{
		var video = new VideoFile();

		Assert.Null(video.EncodingErrorMessage);
	}

	[Fact]
	public void EncodingErrorMessage_CanBeSet()
	{
		var video = new VideoFile { EncodingErrorMessage = "Duration mismatch" };

		Assert.Equal("Duration mismatch", video.EncodingErrorMessage);
	}

	[Theory]
	[InlineData(CompressionStatus.Original)]
	[InlineData(CompressionStatus.Compressed)]
	[InlineData(CompressionStatus.FailedToCompress)]
	[InlineData(CompressionStatus.Bigger)]
	[InlineData(CompressionStatus.Removed)]
	[InlineData(CompressionStatus.ToProcess)]
	[InlineData(CompressionStatus.Processing)]
	[InlineData(CompressionStatus.Kept)]
	public void Status_AllEnumValues_CanBeAssigned(CompressionStatus status)
	{
		var video = new VideoFile { Status = status };

		Assert.Equal(status, video.Status);
	}

	[Theory]
	[InlineData(SanityStatus.Unknown)]
	[InlineData(SanityStatus.OriginalOk)]
	[InlineData(SanityStatus.OriginalKo)]
	[InlineData(SanityStatus.EncodedOk)]
	[InlineData(SanityStatus.EncodedKo)]
	public void Sanity_AllEnumValues_CanBeAssigned(SanityStatus sanity)
	{
		var video = new VideoFile { Sanity = sanity };

		Assert.Equal(sanity, video.Sanity);
	}

	[Fact]
	public void QualityRatioCompressed_DurationZero_ReturnsNull()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			FileSizeCompressed = 5_000_000,
			DurationSeconds = 0,
			Fps = 30.0,
			Width = 1920,
			Height = 1080
		};

		Assert.Null(video.QualityRatioCompressed);
	}

	[Fact]
	public void QualityRatioCompressed_FpsNull_ReturnsNull()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			FileSizeCompressed = 5_000_000,
			DurationSeconds = 100,
			Fps = null,
			Width = 1920,
			Height = 1080
		};

		Assert.Null(video.QualityRatioCompressed);
	}

	[Fact]
	public void QualityRatioCompressed_DimensionsZero_ReturnsNull()
	{
		var video = new VideoFile
		{
			FileSizeOriginal = 10_000_000,
			FileSizeCompressed = 5_000_000,
			DurationSeconds = 100,
			Fps = 30.0,
			Width = 0,
			Height = 0
		};

		Assert.Null(video.QualityRatioCompressed);
	}
}
