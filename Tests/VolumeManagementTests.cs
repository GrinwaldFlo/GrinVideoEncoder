using GrinVideoEncoder.Utils;

namespace Tests;

public class VolumeManagementTests : IDisposable
{
	private readonly string _tempDir;

	public VolumeManagementTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "GrinTests_Volume_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void GetDirectorySize_EmptyDirectory_ReturnsZero()
	{
		long size = VolumeManagent.GetDirectorySize(_tempDir);

		Assert.Equal(0, size);
	}

	[Fact]
	public void GetDirectorySize_WithFiles_ReturnsTotalSize()
	{
		File.WriteAllBytes(Path.Combine(_tempDir, "file1.bin"), new byte[1024]);
		File.WriteAllBytes(Path.Combine(_tempDir, "file2.bin"), new byte[2048]);

		long size = VolumeManagent.GetDirectorySize(_tempDir);

		Assert.Equal(3072, size);
	}

	[Fact]
	public void GetDirectorySize_WithNestedFiles_IncludesAll()
	{
		string subDir = Path.Combine(_tempDir, "sub");
		Directory.CreateDirectory(subDir);
		File.WriteAllBytes(Path.Combine(_tempDir, "root.bin"), new byte[100]);
		File.WriteAllBytes(Path.Combine(subDir, "nested.bin"), new byte[200]);

		long size = VolumeManagent.GetDirectorySize(_tempDir);

		Assert.Equal(300, size);
	}

	[Fact]
	public void GetFreeSpaceByte_NullOrEmpty_ReturnsZero()
	{
		Assert.Equal(0, VolumeManagent.GetFreeSpaceByte(""));
		Assert.Equal(0, VolumeManagent.GetFreeSpaceByte(null!));
	}

	[Fact]
	public void GetFreeSpaceByte_ValidPath_ReturnsPositiveValue()
	{
		long freeSpace = VolumeManagent.GetFreeSpaceByte(_tempDir);

		Assert.True(freeSpace > 0);
	}
}
