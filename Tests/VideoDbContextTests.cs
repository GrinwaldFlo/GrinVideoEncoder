using GrinVideoEncoder.Data;
using GrinVideoEncoder.Models;

namespace Tests;

[Collection(DatabaseCollection.NAME)]
public class VideoDbContextTests() : TestDatabase("DbContext")
{
	private static readonly TimeSpan _oneHour = TimeSpan.FromHours(1);

	/// <summary>
	/// A video with a quality ratio of 1000 that passes every filter of <see cref="VideoDbContext.GetReEncodeCandidatesAsync"/>.
	/// </summary>
	private static VideoFile Candidate(string name) => new()
	{
		DirectoryPath = "/videos",
		Filename = name,
		FileSizeOriginal = 1920L * 1080 * 60,
		DurationSeconds = 60,
		Width = 1920,
		Height = 1080,
		Fps = 30,
		BitDepth = 8,
		LastModified = DateTime.UtcNow.AddDays(-2),
	};

	private static async Task<List<string>> GetCandidateNamesAsync(double threshold = 500, double minSizeByte = 0, TimeSpan? minAge = null, bool includeHighBitDepth = false)
	{
		await using var context = new VideoDbContext();
		var videos = await context.GetReEncodeCandidatesAsync(threshold, minSizeByte, minAge ?? _oneHour, includeHighBitDepth);
		return videos.Select(v => v.Filename).Order().ToList();
	}

	[Fact]
	public async Task CountVideosWithStatusAsync_CountsOnlyThatStatus()
	{
		await AddVideosAsync(
			new VideoFile { Filename = "a", Status = CompressionStatus.ToProcess },
			new VideoFile { Filename = "b", Status = CompressionStatus.ToProcess },
			new VideoFile { Filename = "c", Status = CompressionStatus.Original });

		Assert.Equal(2, await VideoDbContext.CountVideosWithStatusAsync(CompressionStatus.ToProcess));
		Assert.Equal(0, await VideoDbContext.CountVideosWithStatusAsync(CompressionStatus.Processing));
		Assert.Equal(3, await VideoDbContext.CountVideosAsync());
	}

	[Fact]
	public async Task GetVideosWithStatusAsync_ReturnsOnlyThatStatus()
	{
		await AddVideosAsync(
			new VideoFile { Filename = "a", Status = CompressionStatus.Processing },
			new VideoFile { Filename = "b", Status = CompressionStatus.Compressed });

		var videos = await VideoDbContext.GetVideosWithStatusAsync(CompressionStatus.Processing);

		Assert.Equal("a", Assert.Single(videos).Filename);
	}

	[Fact]
	public async Task SetStatusAsync_ChangesOnlyTheGivenVideos()
	{
		var a = new VideoFile { Filename = "a" };
		var b = new VideoFile { Filename = "b" };
		var c = new VideoFile { Filename = "c" };
		await AddVideosAsync(a, b, c);

		int changed = await VideoDbContext.SetStatusAsync([a.Id, b.Id], CompressionStatus.ToProcess);

		Assert.Equal(2, changed);
		Assert.Equal(CompressionStatus.ToProcess, (await GetVideoAsync(a.Id)).Status);
		Assert.Equal(CompressionStatus.ToProcess, (await GetVideoAsync(b.Id)).Status);
		Assert.Equal(CompressionStatus.Original, (await GetVideoAsync(c.Id)).Status);
	}

	[Fact]
	public async Task SetStatusAsync_NoIds_ChangesNothing()
	{
		await AddVideosAsync(new VideoFile { Filename = "a" });

		Assert.Equal(0, await VideoDbContext.SetStatusAsync([], CompressionStatus.ToProcess));
	}

	[Fact]
	public async Task PopNextVideoToProcess_MarksTheVideoProcessing()
	{
		var video = new VideoFile { Filename = "a", Status = CompressionStatus.ToProcess };
		await AddVideosAsync(video, new VideoFile { Filename = "b", Status = CompressionStatus.Original });

		await using var context = new VideoDbContext();
		var popped = await context.PopNextVideoToProcess();

		Assert.Equal(video.Id, popped?.Id);
		Assert.Equal(CompressionStatus.Processing, (await GetVideoAsync(video.Id)).Status);
	}

	[Fact]
	public async Task PopNextVideoToProcess_EmptyQueue_ReturnsNull()
	{
		await AddVideosAsync(new VideoFile { Filename = "a", Status = CompressionStatus.Processing });

		await using var context = new VideoDbContext();

		Assert.Null(await context.PopNextVideoToProcess());
	}

	[Fact]
	public async Task PopNextVideoToProcess_CalledTwice_ReturnsEachVideoOnce()
	{
		await AddVideosAsync(
			new VideoFile { Filename = "a", Status = CompressionStatus.ToProcess },
			new VideoFile { Filename = "b", Status = CompressionStatus.ToProcess });

		await using var first = new VideoDbContext();
		await using var second = new VideoDbContext();
		var a = await first.PopNextVideoToProcess();
		var b = await second.PopNextVideoToProcess();

		Assert.NotNull(a);
		Assert.NotNull(b);
		Assert.NotEqual(a.Id, b.Id);
	}

	[Fact]
	public async Task ResetErrorAsync_SetsOriginal()
	{
		var video = new VideoFile { Filename = "a", Status = CompressionStatus.FailedToCompress };
		await AddVideosAsync(video);

		await VideoDbContext.ResetErrorAsync(video.Id);

		Assert.Equal(CompressionStatus.Original, (await GetVideoAsync(video.Id)).Status);
	}

	[Fact]
	public async Task MarkKept_SetsKept()
	{
		var video = new VideoFile { Filename = "a" };
		await AddVideosAsync(video);

		await VideoDbContext.MarkKept(video.Id);

		Assert.Equal(CompressionStatus.Kept, (await GetVideoAsync(video.Id)).Status);
	}

	[Fact]
	public async Task ResetErrorAndMarkKept_UnknownId_DoNotThrow()
	{
		await VideoDbContext.ResetErrorAsync(Guid.NewGuid());
		await VideoDbContext.MarkKept(Guid.NewGuid());
	}

	[Fact]
	public async Task GetReEncodeCandidatesAsync_ReturnsOnlyOriginalVideos()
	{
		var compressed = Candidate("compressed");
		compressed.Status = CompressionStatus.Compressed;
		var kept = Candidate("kept");
		kept.Status = CompressionStatus.Kept;
		await AddVideosAsync(Candidate("original"), compressed, kept);

		Assert.Equal(["original"], await GetCandidateNamesAsync());
	}

	[Fact]
	public async Task GetReEncodeCandidatesAsync_FiltersThresholdAndSize()
	{
		var small = Candidate("small");
		small.FileSizeOriginal /= 10;
		await AddVideosAsync(Candidate("big"), small);

		Assert.Equal(["big", "small"], await GetCandidateNamesAsync(threshold: 50));
		Assert.Equal(["big"], await GetCandidateNamesAsync(threshold: 500));
		Assert.Equal(["big"], await GetCandidateNamesAsync(threshold: 50, minSizeByte: small.FileSizeOriginal));
	}

	[Fact]
	public async Task GetReEncodeCandidatesAsync_LeavesOutVideosWithoutQualityRatio()
	{
		var noDuration = Candidate("noDuration");
		noDuration.DurationSeconds = null;
		await AddVideosAsync(noDuration);

		Assert.Empty(await GetCandidateNamesAsync(threshold: 0));
	}

	/// <summary>
	/// The indexer stores <see cref="VideoFile.LastModified"/> in UTC: a cutoff in local time moved it by the UTC offset.
	/// </summary>
	[Fact]
	public async Task GetReEncodeCandidatesAsync_LeavesOutVideosModifiedSinceMinAge_InUtc()
	{
		var recent = Candidate("recent");
		recent.LastModified = DateTime.UtcNow.AddMinutes(-30);
		var old = Candidate("old");
		old.LastModified = DateTime.UtcNow.AddMinutes(-90);
		await AddVideosAsync(recent, old);

		Assert.Equal(["old"], await GetCandidateNamesAsync(minAge: _oneHour));
		Assert.Equal(["old", "recent"], await GetCandidateNamesAsync(minAge: TimeSpan.FromMinutes(10)));
	}

	[Fact]
	public async Task GetReEncodeCandidatesAsync_HighBitDepthOnlyWhenIncluded()
	{
		var tenBit = Candidate("tenBit");
		tenBit.BitDepth = 10;
		await AddVideosAsync(Candidate("eightBit"), tenBit);

		Assert.Equal(["eightBit"], await GetCandidateNamesAsync(includeHighBitDepth: false));
		Assert.Equal(["eightBit", "tenBit"], await GetCandidateNamesAsync(includeHighBitDepth: true));
	}

	[Fact]
	public async Task GetReEncodeCandidatesAsync_ZeroFps_IsNotACandidate()
	{
		var zeroFps = Candidate("zeroFps");
		zeroFps.Fps = 0;
		await AddVideosAsync(zeroFps);

		Assert.Empty(await GetCandidateNamesAsync(threshold: 0));
	}
}
