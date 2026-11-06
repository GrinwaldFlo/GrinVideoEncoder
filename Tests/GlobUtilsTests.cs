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

	[Theory]
	[InlineData("other")]
	[InlineData("..")]
	public void GetTrashDirectory_VideoOutsideRoot_Throws(string outside)
	{
		string root = Path.Combine(_tempDir, "videos");
		string trash = Path.Combine(_tempDir, "Trash");
		string videoDirectory = outside == ".." ? _tempDir : Path.Combine(_tempDir, outside, "a");

		Assert.Throws<InvalidOperationException>(() => GlobUtils.GetTrashDirectory(trash, root, videoDirectory));
	}

	[Fact]
	public void RebaseDirectory_SubfolderOfOldRoot_MovesToNewRoot()
	{
		string oldRoot = Path.Combine(_tempDir, "old");
		string newRoot = Path.Combine(_tempDir, "new");

		Assert.Equal(Path.Combine(newRoot, "a", "b"), GlobUtils.RebaseDirectory(oldRoot, newRoot, Path.Combine(oldRoot, "a", "b")));
	}

	[Fact]
	public void RebaseDirectory_OldRootItself_ReturnsNewRootWithoutTrailingSeparator()
	{
		string oldRoot = Path.Combine(_tempDir, "old");
		string newRoot = Path.Combine(_tempDir, "new");

		Assert.Equal(newRoot, GlobUtils.RebaseDirectory(oldRoot + Path.DirectorySeparatorChar, newRoot + Path.DirectorySeparatorChar, oldRoot));
	}

	[Fact]
	public void RebaseDirectory_SiblingWithSamePrefix_ReturnsNull()
	{
		string oldRoot = Path.Combine(_tempDir, "old");

		Assert.Null(GlobUtils.RebaseDirectory(oldRoot, Path.Combine(_tempDir, "new"), oldRoot + "2"));
	}

	[Theory]
	[InlineData(@"V:\Series", @"V:\Series\Kimmy\S1")]
	[InlineData(@"V:\Series", @"v:\series\Kimmy\S1")]
	[InlineData(@"V:\Series\", @"V:\Series\Kimmy/S1")]
	[InlineData(@"V:\", @"V:\Kimmy\S1")]
	[InlineData(@"\\nas\videos", @"\\NAS\Videos\Kimmy\S1")]
	public void RebaseDirectory_WindowsPath_MovesToNewRootIgnoringCase(string oldRoot, string directory)
	{
		string newRoot = Path.Combine(_tempDir, "new");

		Assert.Equal(Path.Combine(newRoot, "Kimmy", "S1"), GlobUtils.RebaseDirectory(oldRoot, newRoot, directory));
	}

	[Fact]
	public void RebaseDirectory_WindowsPathIsRoot_ReturnsNewRoot()
	{
		string newRoot = Path.Combine(_tempDir, "new");

		Assert.Equal(newRoot, GlobUtils.RebaseDirectory(@"V:\Series", newRoot, @"v:\SERIES\"));
	}

	[Theory]
	[InlineData(@"V:\Series2\Kimmy")]
	[InlineData(@"W:\Series\Kimmy")]
	[InlineData(@"V:\Series\..\Other")]
	[InlineData(@"V:\")]
	public void RebaseDirectory_WindowsPathOutsideOldRoot_ReturnsNull(string directory)
	{
		Assert.Null(GlobUtils.RebaseDirectory(@"V:\Series", Path.Combine(_tempDir, "new"), directory));
	}

	[Theory]
	[InlineData(@"V:\Series", true)]
	[InlineData(@"v:/Series", true)]
	[InlineData("V:", true)]
	[InlineData(@"\\nas\videos", true)]
	[InlineData("/home/user/videos", false)]
	[InlineData("videos", false)]
	[InlineData("V:videos", false)]
	public void IsWindowsStylePath_DetectsDriveAndUncPaths(string path, bool expected)
	{
		Assert.Equal(expected, GlobUtils.IsWindowsStylePath(path));
	}

	[Theory]
	[InlineData("a", true)]
	[InlineData("", true)]
	[InlineData("..", false)]
	public void IsInsideDirectory_ReturnsWhetherFolderIsUnderRoot(string relative, bool expected)
	{
		string root = Path.Combine(_tempDir, "videos");

		Assert.Equal(expected, GlobUtils.IsInsideDirectory(root, Path.Combine(root, relative)));
	}

	[Fact]
	public void GetTrashDirectory_SiblingWithSamePrefix_Throws()
	{
		string root = Path.Combine(_tempDir, "videos");
		string trash = Path.Combine(_tempDir, "Trash");

		Assert.Throws<InvalidOperationException>(() => GlobUtils.GetTrashDirectory(trash, root, root + "2"));
	}

	[Fact]
	public void GetTrashDirectory_SubfolderStartingWithDots_KeepsStructure()
	{
		string root = Path.Combine(_tempDir, "videos");
		string trash = Path.Combine(_tempDir, "Trash");

		Assert.Equal(Path.Combine(trash, "..hidden"), GlobUtils.GetTrashDirectory(trash, root, Path.Combine(root, "..hidden")));
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
