using GrinVideoEncoder.Models;

namespace Tests;

public class FolderMaintenanceInfoTests
{
	[Theory]
	[InlineData(0, "0 B")]
	[InlineData(500, "500 B")]
	[InlineData(1023, "1023 B")]
	public void SizeFormatted_Bytes_ReturnsCorrectFormat(long sizeBytes, string expected)
	{
		var info = new FolderMaintenanceInfo { SizeBytes = sizeBytes };

		Assert.Equal(expected, info.SizeFormatted);
	}

	[Theory]
	[InlineData(1024, "1.0 KB")]
	[InlineData(1536, "1.5 KB")]
	[InlineData(512_000, "500.0 KB")]
	public void SizeFormatted_Kilobytes_ReturnsCorrectFormat(long sizeBytes, string expected)
	{
		var info = new FolderMaintenanceInfo { SizeBytes = sizeBytes };

		Assert.Equal(expected, info.SizeFormatted);
	}

	[Theory]
	[InlineData(1_048_576, "1.0 MB")]
	[InlineData(10_485_760, "10.0 MB")]
	[InlineData(536_870_912, "512.0 MB")]
	public void SizeFormatted_Megabytes_ReturnsCorrectFormat(long sizeBytes, string expected)
	{
		var info = new FolderMaintenanceInfo { SizeBytes = sizeBytes };

		Assert.Equal(expected, info.SizeFormatted);
	}

	[Theory]
	[InlineData(1_073_741_824, "1.00 GB")]
	[InlineData(5_368_709_120, "5.00 GB")]
	public void SizeFormatted_Gigabytes_ReturnsCorrectFormat(long sizeBytes, string expected)
	{
		var info = new FolderMaintenanceInfo { SizeBytes = sizeBytes };

		Assert.Equal(expected, info.SizeFormatted);
	}

	[Fact]
	public void DefaultValues_AreCorrect()
	{
		var info = new FolderMaintenanceInfo();

		Assert.Equal(string.Empty, info.Name);
		Assert.Equal(string.Empty, info.Path);
		Assert.Equal(0, info.SizeBytes);
		Assert.Equal(0, info.FileCount);
		Assert.Empty(info.Files);
	}
}
