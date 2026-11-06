using GrinVideoEncoder.Models;
using GrinVideoEncoder.Utils;

namespace Tests;

public class MediaInfoExtensionsTests
{
	[Theory]
	[InlineData("yuv420p", 8)]
	[InlineData("yuvj420p", 8)]
	[InlineData("yuv444p", 8)]
	[InlineData("nv12", 8)]
	[InlineData("gray", 8)]
	[InlineData("rgb24", 8)]
	[InlineData("rgb565le", 8)]
	[InlineData("bgr444be", 8)]
	[InlineData("yuv420p10le", 10)]
	[InlineData("yuv422p10be", 10)]
	[InlineData("YUV420P10LE", 10)]
	[InlineData("p010le", 10)]
	[InlineData("p210le", 10)]
	[InlineData("y210le", 10)]
	[InlineData("x2rgb10le", 10)]
	[InlineData("gray10le", 10)]
	[InlineData("yuv420p9le", 9)]
	[InlineData("rgb555be", 8)]
	[InlineData("yuv444p12le", 12)]
	[InlineData("p012le", 12)]
	[InlineData("yuv420p16le", 16)]
	[InlineData("p016le", 16)]
	[InlineData("rgb48le", 16)]
	[InlineData("rgba64be", 16)]
	public void ParseBitDepth_KnownFormats(string pixelFormat, int expected)
	{
		Assert.Equal(expected, MediaInfoExtensions.ParseBitDepth(pixelFormat));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	public void ParseBitDepth_MissingFormat_ReturnsUnknown(string? pixelFormat)
	{
		Assert.Equal(VideoFile.BIT_DEPTH_UNKNOWN, MediaInfoExtensions.ParseBitDepth(pixelFormat));
	}
}
