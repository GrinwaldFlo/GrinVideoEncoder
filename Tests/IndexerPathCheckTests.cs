using GrinVideoEncoder.Models;
using GrinVideoEncoder.Startup;

namespace Tests;

public class IndexerPathCheckTests
{
	private static readonly string _oldRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GrinTests_OldRoot"));
	private static readonly string _newRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GrinTests_NewRoot"));

	private sealed class Fixture
	{
		public HashSet<string> Directories { get; } = [];
		public HashSet<string> Files { get; } = [];
		public List<VideoFile> Videos { get; } = [];
		public List<(string OldRoot, string NewRoot)> Relocations { get; } = [];
		public StringWriter Output { get; } = new();

		public Task<string?> RunAsync(string indexerPath, params string[] answers)
		{
			var check = new IndexerPathCheck(
				new StringReader(string.Join(Environment.NewLine, answers)),
				Output,
				Directories.Contains,
				Files.Contains,
				count => Task.FromResult(Videos.Take(count).ToList()),
				(oldRoot, newRoot) =>
				{
					Relocations.Add((oldRoot, newRoot));
					return Task.FromResult(Videos.Count);
				});
			return check.RunAsync(indexerPath);
		}

		/// <summary>
		/// Adds indexed videos in the old root, and puts the first <paramref name="presentInNewRoot"/> of them in the new root.
		/// </summary>
		public void AddVideos(int count, int presentInNewRoot)
		{
			for (int i = 0; i < count; i++)
			{
				string subFolder = $"folder{i}";
				Videos.Add(new VideoFile { DirectoryPath = Path.Combine(_oldRoot, subFolder), Filename = $"video{i}.mp4" });
				if (i < presentInNewRoot)
					Files.Add(Path.Combine(_newRoot, subFolder, $"video{i}.mp4"));
			}
		}
	}

	[Fact]
	public async Task RunAsync_PathExists_ReturnsItWithoutAsking()
	{
		var fixture = new Fixture();
		fixture.Directories.Add(_oldRoot);

		string? result = await fixture.RunAsync(_oldRoot);

		Assert.Equal(_oldRoot, result);
		Assert.Empty(fixture.Output.ToString());
		Assert.Empty(fixture.Relocations);
	}

	[Fact]
	public async Task RunAsync_PathNotConfigured_ReturnsItWithoutAsking()
	{
		var fixture = new Fixture();

		string? result = await fixture.RunAsync(string.Empty);

		Assert.Equal(string.Empty, result);
		Assert.Empty(fixture.Output.ToString());
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task RunAsync_PathMissingAndEmptyAnswer_ReturnsNullToClose(string answer)
	{
		var fixture = new Fixture();

		string? result = await fixture.RunAsync(_oldRoot, answer);

		Assert.Null(result);
		Assert.Empty(fixture.Relocations);
	}

	[Fact]
	public async Task RunAsync_PathMissingAndNoInput_ReturnsNullToClose()
	{
		var fixture = new Fixture();

		Assert.Null(await fixture.RunAsync(_oldRoot));
	}

	[Fact]
	public async Task RunAsync_AllSampledVideosFound_RelocatesAndReturnsNewPath()
	{
		var fixture = new Fixture();
		fixture.Directories.Add(_newRoot);
		fixture.AddVideos(10, 10);

		string? result = await fixture.RunAsync(_oldRoot, _newRoot);

		Assert.Equal(_newRoot, result);
		Assert.Equal([(_oldRoot, _newRoot)], fixture.Relocations);
		Assert.Contains($"Found {IndexerPathCheck.SAMPLE_SIZE} of {IndexerPathCheck.SAMPLE_SIZE}", fixture.Output.ToString());
	}

	[Fact]
	public async Task RunAsync_QuotedAnswer_RemovesQuotes()
	{
		var fixture = new Fixture();
		fixture.Directories.Add(_newRoot);

		string? result = await fixture.RunAsync(_oldRoot, $"  \"{_newRoot}\"  ");

		Assert.Equal(_newRoot, result);
	}

	[Fact]
	public async Task RunAsync_NoIndexedVideos_AcceptsExistingFolder()
	{
		var fixture = new Fixture();
		fixture.Directories.Add(_newRoot);

		string? result = await fixture.RunAsync(_oldRoot, _newRoot);

		Assert.Equal(_newRoot, result);
		Assert.Single(fixture.Relocations);
	}

	[Fact]
	public async Task RunAsync_NewFolderMissing_AsksAgain()
	{
		var fixture = new Fixture();
		fixture.Directories.Add(_newRoot);
		string wrong = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GrinTests_Wrong"));

		string? result = await fixture.RunAsync(_oldRoot, wrong, _newRoot);

		Assert.Equal(_newRoot, result);
		Assert.Contains($"Folder not found: {wrong}", fixture.Output.ToString());
	}

	[Fact]
	public async Task RunAsync_NoSampledVideoFound_RefusesAndAsksAgain()
	{
		var fixture = new Fixture();
		fixture.Directories.Add(_newRoot);
		fixture.AddVideos(3, 0);

		string? result = await fixture.RunAsync(_oldRoot, _newRoot, string.Empty);

		Assert.Null(result);
		Assert.Empty(fixture.Relocations);
		Assert.Contains("Found 0 of 3", fixture.Output.ToString());
	}

	[Theory]
	[InlineData("y", true)]
	[InlineData("YES", true)]
	[InlineData("n", false)]
	[InlineData("", false)]
	public async Task RunAsync_SomeSampledVideosFound_AsksToConfirm(string confirmation, bool expectRelocated)
	{
		var fixture = new Fixture();
		fixture.Directories.Add(_newRoot);
		fixture.AddVideos(4, 2);

		string? result = await fixture.RunAsync(_oldRoot, _newRoot, confirmation, string.Empty);

		Assert.Equal(expectRelocated ? _newRoot : null, result);
		Assert.Equal(expectRelocated ? 1 : 0, fixture.Relocations.Count);
		Assert.Contains("Found 2 of 4", fixture.Output.ToString());
	}

	[Fact]
	public async Task RunAsync_VideosOutsideOldRoot_AreNotSampled()
	{
		var fixture = new Fixture();
		fixture.Directories.Add(_newRoot);
		fixture.Videos.Add(new VideoFile { DirectoryPath = Path.Combine(Path.GetTempPath(), "GrinTests_Elsewhere"), Filename = "other.mp4" });
		fixture.AddVideos(2, 2);

		string? result = await fixture.RunAsync(_oldRoot, _newRoot);

		Assert.Equal(_newRoot, result);
		Assert.Contains("Found 2 of 2", fixture.Output.ToString());
	}
}
