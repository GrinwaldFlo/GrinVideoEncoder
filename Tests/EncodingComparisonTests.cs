using GrinVideoEncoder.Services;

namespace Tests;

public class EncodingComparisonTests : IDisposable
{
	private static readonly (double min, double max) _noFpsScan = (0, 0);

	private readonly string _tempDir;

	public EncodingComparisonTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "GrinTests_Comparison_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// A 1080p 30 FPS 8-bit encoding that matches its original.
	/// </summary>
	private static EncodingComparison Matching() => new(
		OriginalDuration: 60, EncodedDuration: 60,
		OriginalPixels: 1920 * 1080, EncodedPixels: 1920 * 1080,
		OriginalFps: 30, EncodedFps: 30,
		OriginalBitDepth: 8, EncodedBitDepth: 8, ExpectedBitDepth: 8);

	[Fact]
	public void FindProblem_Matching_ReturnsNull()
	{
		Assert.Null(Matching().FindProblem(_noFpsScan));
	}

	[Theory]
	[InlineData(0.4, true)]
	[InlineData(0.6, false)]
	public void FindProblem_DurationDifference_AllowsHalfASecond(double difference, bool accepted)
	{
		var comparison = Matching() with { EncodedDuration = 60 + difference };

		string? problem = comparison.FindProblem(_noFpsScan);

		Assert.Equal(accepted, problem is null);
		if (!accepted)
			Assert.Contains("Duration", problem);
	}

	[Fact]
	public void FindProblem_ShorterEncoding_IsAProblem()
	{
		Assert.Contains("Duration", (Matching() with { EncodedDuration = 50 }).FindProblem(_noFpsScan));
	}

	[Fact]
	public void FindProblem_ResolutionChanged_IsAProblem()
	{
		Assert.Contains("Resolution", (Matching() with { EncodedPixels = 1280 * 720 }).FindProblem(_noFpsScan));
	}

	[Theory]
	[InlineData(nameof(EncodingComparison.OriginalDuration))]
	[InlineData(nameof(EncodingComparison.EncodedDuration))]
	[InlineData(nameof(EncodingComparison.OriginalPixels))]
	[InlineData(nameof(EncodingComparison.EncodedPixels))]
	[InlineData(nameof(EncodingComparison.OriginalFps))]
	[InlineData(nameof(EncodingComparison.EncodedFps))]
	public void FindProblem_UnreadableValue_IsAProblem(string missing)
	{
		var comparison = missing switch
		{
			nameof(EncodingComparison.OriginalDuration) => Matching() with { OriginalDuration = null },
			nameof(EncodingComparison.EncodedDuration) => Matching() with { EncodedDuration = null },
			nameof(EncodingComparison.OriginalPixels) => Matching() with { OriginalPixels = null },
			nameof(EncodingComparison.EncodedPixels) => Matching() with { EncodedPixels = null },
			nameof(EncodingComparison.OriginalFps) => Matching() with { OriginalFps = null },
			_ => Matching() with { EncodedFps = null },
		};

		Assert.Contains("media info", comparison.FindProblem(_noFpsScan));
	}

	[Theory]
	[InlineData(31.9, false)]
	[InlineData(32.1, true)]
	public void NeedsFpsScan_OnlyAboveTolerance(double encodedFps, bool expected)
	{
		Assert.Equal(expected, (Matching() with { EncodedFps = encodedFps }).NeedsFpsScan);
	}

	[Fact]
	public void NeedsFpsScan_UnreadableFps_DoesNotScan()
	{
		Assert.False((Matching() with { EncodedFps = null }).NeedsFpsScan);
	}

	[Fact]
	public void FindProblem_FpsChangedOnConstantFrameRateOriginal_IsAProblem()
	{
		var comparison = Matching() with { EncodedFps = 60 };

		Assert.Contains("FPS", comparison.FindProblem((29.97, 30.0)));
	}

	/// <summary>
	/// The average FPS of a variable frame rate video can change with the encoding, so it is accepted.
	/// </summary>
	[Fact]
	public void FindProblem_FpsChangedOnVariableFrameRateOriginal_IsAccepted()
	{
		var comparison = Matching() with { EncodedFps = 60 };

		Assert.Null(comparison.FindProblem((24, 60)));
	}

	[Fact]
	public void FindProblem_FpsScanFailed_IsAProblem()
	{
		var comparison = Matching() with { EncodedFps = 60 };

		Assert.Contains("FPS", comparison.FindProblem(_noFpsScan));
	}

	[Fact]
	public void FindProblem_BitDepthReduced_IsAProblem()
	{
		var comparison = Matching() with { OriginalBitDepth = 10, EncodedBitDepth = 8, ExpectedBitDepth = 10 };

		Assert.Contains("Bit depth", comparison.FindProblem(_noFpsScan));
	}

	[Fact]
	public void FindProblem_UnknownEncodedBitDepth_IsAProblemWhenOneIsExpected()
	{
		Assert.Contains("Bit depth", (Matching() with { EncodedBitDepth = 0 }).FindProblem(_noFpsScan));
	}

	[Fact]
	public void FindProblem_NoExpectedBitDepth_DoesNotCheckIt()
	{
		var comparison = Matching() with { OriginalBitDepth = 10, EncodedBitDepth = 8, ExpectedBitDepth = null };

		Assert.Null(comparison.FindProblem(_noFpsScan));
	}

	[Fact]
	public void IsVariableFrameRate_OnlyAboveTolerance()
	{
		Assert.True(EncodingComparison.IsVariableFrameRate((24, 60)));
		Assert.False(EncodingComparison.IsVariableFrameRate((29.97, 30)));
	}

	[Fact]
	public void WouldOverwriteOtherFile_SamePath_ReturnsFalse()
	{
		string path = Path.Combine(_tempDir, "a.mp4");
		File.WriteAllText(path, "original");

		Assert.False(VideoReencodeService.WouldOverwriteOtherFile(path, path));
	}

	[Fact]
	public void WouldOverwriteOtherFile_NewPathFree_ReturnsFalse()
	{
		string original = Path.Combine(_tempDir, "a.mkv");
		File.WriteAllText(original, "original");

		Assert.False(VideoReencodeService.WouldOverwriteOtherFile(original, Path.Combine(_tempDir, "a.mp4")));
	}

	/// <summary>
	/// Re-encoding a.mkv writes a.mp4: an existing a.mp4 is another video that must not be replaced.
	/// </summary>
	[Fact]
	public void WouldOverwriteOtherFile_OtherFileAtNewPath_ReturnsTrue()
	{
		string original = Path.Combine(_tempDir, "a.mkv");
		string other = Path.Combine(_tempDir, "a.mp4");
		File.WriteAllText(original, "original");
		File.WriteAllText(other, "another video");

		Assert.True(VideoReencodeService.WouldOverwriteOtherFile(original, other));
	}

	[Fact]
	public void GetEncodedFilename_ChangesTheExtensionToMp4()
	{
		Assert.Equal("a.mp4", VideoReencodeService.GetEncodedFilename("a.MKV"));
		Assert.Equal("a.b.mp4", VideoReencodeService.GetEncodedFilename("a.b.avi"));
	}
}
