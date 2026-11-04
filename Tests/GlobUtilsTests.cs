using GrinVideoEncoder.Utils;

namespace Tests;

public class GlobUtilsTests : IDisposable
{
	private readonly string _tempDir;

	public GlobUtilsTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "GrinTests_GlobUtils_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void GetTrashDirectory_VideoInRootWithTrailingSeparator_ReturnsTrashRoot()
	{
		string root = Path.Combine(_tempDir, "videos");
		string trash = Path.Combine(_tempDir, "Trash");

		Assert.Equal(trash, GlobUtils.GetTrashDirectory(trash, root + Path.DirectorySeparatorChar, root));
	}

	[Fact]
	public void GetTrashDirectory_VideoInRootWithoutTrailingSeparator_ReturnsTrashRoot()
	{
		string root = Path.Combine(_tempDir, "videos");
		string trash = Path.Combine(_tempDir, "Trash");

		Assert.Equal(trash, GlobUtils.GetTrashDirectory(trash, root, root));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void GetTrashDirectory_VideoInSubfolder_KeepsStructure(bool trailingSeparator)
	{
		string root = Path.Combine(_tempDir, "videos");
		string trash = Path.Combine(_tempDir, "Trash");
		string indexerPath = trailingSeparator ? root + Path.DirectorySeparatorChar : root;

		string result = GlobUtils.GetTrashDirectory(trash, indexerPath, Path.Combine(root, "a", "b"));

		Assert.Equal(Path.Combine(trash, "a", "b"), result);
	}

	[Fact]
	public void FormatFileSizeMb_MissingFile_ReturnsNotAvailable()
	{
		Assert.Equal("n/a", GlobUtils.FormatFileSizeMb(Path.Combine(_tempDir, "missing.mp4")));
	}

	[Fact]
	public void FormatFileSizeMb_ExistingFile_ReturnsSize()
	{
		string filePath = Path.Combine(_tempDir, "video.mp4");
		File.WriteAllBytes(filePath, new byte[1024 * 1024]);

		Assert.Equal((1.0).ToString("F2") + " MB", GlobUtils.FormatFileSizeMb(filePath));
	}

	[Fact]
	public void FindExecutableInPath_UnknownName_ReturnsNull()
	{
		Assert.Null(GlobUtils.FindExecutableInPath("grin-executable-that-does-not-exist"));
	}

	[Fact]
	public void EnsureUniqueFilename_NoConflict_ReturnsSamePath()
	{
		string filePath = Path.Combine(_tempDir, "video.mp4");

		string result = GlobUtils.EnsureUniqueFilename(filePath);

		Assert.Equal(filePath, result);
	}

	[Fact]
	public void EnsureUniqueFilename_OneConflict_AppendsSuffix()
	{
		string filePath = Path.Combine(_tempDir, "video.mp4");
		File.WriteAllText(filePath, "dummy");

		string result = GlobUtils.EnsureUniqueFilename(filePath);

		Assert.Equal(Path.Combine(_tempDir, "video (1).mp4"), result);
	}

	[Fact]
	public void EnsureUniqueFilename_MultipleConflicts_IncrementsCounter()
	{
		string filePath = Path.Combine(_tempDir, "video.mp4");
		File.WriteAllText(filePath, "dummy");
		File.WriteAllText(Path.Combine(_tempDir, "video (1).mp4"), "dummy");
		File.WriteAllText(Path.Combine(_tempDir, "video (2).mp4"), "dummy");

		string result = GlobUtils.EnsureUniqueFilename(filePath);

		Assert.Equal(Path.Combine(_tempDir, "video (3).mp4"), result);
	}

	[Fact]
	public void EnsureUniqueFilename_PreservesExtension()
	{
		string filePath = Path.Combine(_tempDir, "movie.mkv");
		File.WriteAllText(filePath, "dummy");

		string result = GlobUtils.EnsureUniqueFilename(filePath);

		Assert.EndsWith(".mkv", result);
	}

	[Fact]
	public void EnsureUniqueFilename_CreatesDirectoryIfMissing()
	{
		string subDir = Path.Combine(_tempDir, "newsubdir");
		string filePath = Path.Combine(subDir, "video.mp4");

		string result = GlobUtils.EnsureUniqueFilename(filePath);

		Assert.True(Directory.Exists(subDir));
		Assert.Equal(filePath, result);
	}

	[Fact]
	public void IsFileReady_ExistingFileWithContent_ReturnsTrue()
	{
		string filePath = Path.Combine(_tempDir, "ready.txt");
		File.WriteAllText(filePath, "some content");

		Assert.True(GlobUtils.IsFileReady(filePath));
	}

	[Fact]
	public void IsFileReady_NonExistentFile_ReturnsFalse()
	{
		string filePath = Path.Combine(_tempDir, "nonexistent.txt");

		Assert.False(GlobUtils.IsFileReady(filePath));
	}

	[Fact]
	public void IsFileReady_EmptyFile_ReturnsFalse()
	{
		string filePath = Path.Combine(_tempDir, "empty.txt");
		File.WriteAllText(filePath, "");

		Assert.False(GlobUtils.IsFileReady(filePath));
	}

	[Fact]
	public void IsFileReady_LockedFile_ReturnsFalse()
	{
		string filePath = Path.Combine(_tempDir, "locked.txt");
		File.WriteAllText(filePath, "content");

		using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

		Assert.False(GlobUtils.IsFileReady(filePath));
	}

	[Fact]
	public void EnsureUniqueFilename_FileWithoutExtension_HandlesCorrectly()
	{
		string filePath = Path.Combine(_tempDir, "README");
		File.WriteAllText(filePath, "dummy");

		string result = GlobUtils.EnsureUniqueFilename(filePath);

		Assert.Equal(Path.Combine(_tempDir, "README (1)"), result);
	}
}
