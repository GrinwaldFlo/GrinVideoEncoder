using GrinVideoEncoder.Data;
using GrinVideoEncoder.Models;
using GrinVideoEncoder.Services;

namespace Tests;

/// <summary>
/// Indexes small files that are not videos: their media info cannot be read, which the indexer must handle.
/// </summary>
[Collection(DatabaseCollection.NAME)]
public class VideoIndexerServiceTests : TestDatabase
{
	private readonly string _root;
	private readonly VideoIndexerService _indexer;

	public VideoIndexerServiceTests() : base("Indexer")
	{
		_root = Path.Combine(TempDir, "library");
		Directory.CreateDirectory(_root);
		Settings.IndexerPath = _root;
		_indexer = new VideoIndexerService(Settings, new LogMain(Settings), new CommunicationService());
	}

	private string CreateFile(string relativePath, int size = 10)
	{
		string path = Path.Combine(_root, relativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllBytes(path, new byte[size]);
		return path;
	}

	private Task IndexAsync() => _indexer.RunIndexing(CancellationToken.None);

	private static async Task<List<string>> GetIndexedPathsAsync() =>
		(await GetAllVideosAsync()).Select(v => v.FullPath).Order().ToList();

	[Fact]
	public async Task RunIndexing_IndexesVideoExtensionsOnly_IgnoringCase()
	{
		string mp4 = CreateFile("a.mp4");
		string mkv = CreateFile(Path.Combine("sub", "b.MKV"));
		CreateFile("notes.txt");

		await IndexAsync();

		Assert.Equal(new[] { mp4, mkv }.Order(), await GetIndexedPathsAsync());
	}

	[Fact]
	public async Task RunIndexing_StoresSizeAndUnreadableMediaInfo()
	{
		string path = CreateFile("a.mp4", size: 1234);

		await IndexAsync();

		var video = Assert.Single(await GetAllVideosAsync());
		Assert.Equal(path, video.FullPath);
		Assert.Equal(1234, video.FileSizeOriginal);
		Assert.Equal(CompressionStatus.Original, video.Status);
		Assert.Equal(SanityStatus.OriginalKo, video.Sanity);
		Assert.Equal(VideoFile.BIT_DEPTH_UNKNOWN, video.BitDepth);
	}

	[Fact]
	public async Task RunIndexing_Twice_DoesNotDuplicate()
	{
		CreateFile("a.mp4");

		await IndexAsync();
		await IndexAsync();

		Assert.Single(await GetAllVideosAsync());
	}

	[Fact]
	public async Task RunIndexing_SkipsIgnoredFolders_IgnoringCase()
	{
		string kept = CreateFile("a.mp4");
		CreateFile(Path.Combine("@eaDir", "thumb.mp4"));
		Settings.IgnoreFolders = ["@EADIR"];

		await IndexAsync();

		Assert.Equal([kept], await GetIndexedPathsAsync());
	}

	[Fact]
	public async Task RunIndexing_RemovesVideosOfFolderIgnoredLater()
	{
		string kept = CreateFile("a.mp4");
		CreateFile(Path.Combine("Extras", "b.mp4"));
		await IndexAsync();

		Settings.IgnoreFolders = ["Extras"];
		await IndexAsync();

		Assert.Equal([kept], await GetIndexedPathsAsync());
	}

	[Fact]
	public async Task RunIndexing_RemovesDeletedFiles()
	{
		string kept = CreateFile("a.mp4");
		string deleted = CreateFile("b.mp4");
		await IndexAsync();

		File.Delete(deleted);
		await IndexAsync();

		Assert.Equal([kept], await GetIndexedPathsAsync());
	}

	[Fact]
	public async Task RunIndexing_MovedFile_KeepsItsHistory()
	{
		string original = CreateFile(Path.Combine("old", "a.mp4"), size: 50);
		await IndexAsync();
		var indexed = Assert.Single(await GetAllVideosAsync());
		await VideoDbContext.SetStatusAsync([indexed.Id], CompressionStatus.Kept);

		string moved = Path.Combine(_root, "new", "a.mp4");
		Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
		File.Move(original, moved);
		await IndexAsync();

		var video = Assert.Single(await GetAllVideosAsync());
		Assert.Equal(indexed.Id, video.Id);
		Assert.Equal(moved, video.FullPath);
		Assert.Equal(CompressionStatus.Kept, video.Status);
	}

	[Fact]
	public async Task RunIndexing_TwoFilesCouldBeTheMovedOne_IndexesAsNew()
	{
		string original = CreateFile(Path.Combine("old", "a.mp4"), size: 50);
		await IndexAsync();
		var indexed = Assert.Single(await GetAllVideosAsync());

		File.Delete(original);
		CreateFile(Path.Combine("x", "a.mp4"), size: 50);
		await AddVideosAsync(new VideoFile { DirectoryPath = Path.Combine(_root, "gone"), Filename = "a.mp4", FileSizeOriginal = 50 });
		await IndexAsync();

		var video = Assert.Single(await GetAllVideosAsync());
		Assert.NotEqual(indexed.Id, video.Id);
	}

	/// <summary>
	/// A folder that cannot be read is not proof that its videos were deleted, so their history must stay.
	/// </summary>
	[Fact]
	public async Task RunIndexing_UnreadableFolder_KeepsItsVideos()
	{
		if (!OperatingSystem.IsLinux())
			return;

		string locked = Path.Combine(_root, "locked");
		string video = CreateFile(Path.Combine("locked", "a.mp4"));
		string other = CreateFile("b.mp4");
		await IndexAsync();

		File.SetUnixFileMode(locked, UnixFileMode.None);
		try
		{
			// Running as root, the folder stays readable and there is nothing to test
			if (CanList(locked))
				return;

			await IndexAsync();
		}
		finally
		{
			File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}

		Assert.Equal(new[] { video, other }.Order(), await GetIndexedPathsAsync());
	}

	/// <summary>
	/// Videos of a folder whose name starts like the indexed folder, such as /videos2 for /videos, are not inside it.
	/// </summary>
	[Fact]
	public async Task RunIndexing_KeepsVideosOfSiblingFolderWithSamePrefix()
	{
		string sibling = _root + "2";
		Directory.CreateDirectory(sibling);
		File.WriteAllBytes(Path.Combine(sibling, "a.mp4"), new byte[10]);
		await AddVideosAsync(new VideoFile { DirectoryPath = sibling, Filename = "a.mp4", FileSizeOriginal = 10 });

		await IndexAsync();

		Assert.Equal([Path.Combine(sibling, "a.mp4")], await GetIndexedPathsAsync());
	}

	[Fact]
	public async Task RunIndexing_MissingIndexerPath_ChangesNothing()
	{
		await AddVideosAsync(new VideoFile { DirectoryPath = _root, Filename = "a.mp4" });
		Settings.IndexerPath = Path.Combine(TempDir, "missing");

		await IndexAsync();

		Assert.Single(await GetAllVideosAsync());
	}

	[Fact]
	public async Task RunIndexing_FillsMissingBitDepthAndSanity()
	{
		string path = CreateFile("a.mp4");
		await AddVideosAsync(new VideoFile
		{
			DirectoryPath = _root,
			Filename = "a.mp4",
			FileSizeOriginal = 10,
			BitDepth = null,
			Sanity = SanityStatus.Unknown,
			Status = CompressionStatus.Compressed,
		});

		await IndexAsync();

		var video = Assert.Single(await GetAllVideosAsync());
		Assert.Equal(path, video.FullPath);
		Assert.Equal(VideoFile.BIT_DEPTH_UNKNOWN, video.BitDepth);
		Assert.Equal(SanityStatus.EncodedKo, video.Sanity);
	}

	private static bool CanList(string directory)
	{
		try
		{
			_ = Directory.EnumerateFileSystemEntries(directory).Any();
			return true;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}
}
