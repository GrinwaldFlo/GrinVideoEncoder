using GrinVideoEncoder.Models;

namespace GrinVideoEncoder.Startup;

/// <summary>
/// Startup check that the indexed folder still exists. If it was moved, asks for its new location,
/// checks that a few indexed videos are there, and moves the indexed videos to it in the database.
/// </summary>
/// <param name="input">Where the answers are read from.</param>
/// <param name="output">Where the questions are written to.</param>
/// <param name="directoryExists">Returns true if a folder exists.</param>
/// <param name="fileExists">Returns true if a file exists.</param>
/// <param name="getRandomVideos">Returns up to the given number of random indexed videos.</param>
/// <param name="relocate">Moves the indexed videos from the old root to the new root and returns how many were updated.</param>
public class IndexerPathCheck(
	TextReader input,
	TextWriter output,
	Func<string, bool> directoryExists,
	Func<string, bool> fileExists,
	Func<int, Task<List<VideoFile>>> getRandomVideos,
	Func<string, string, Task<int>> relocate)
{
	internal const int SAMPLE_SIZE = 5;

	/// <summary>
	/// Videos read from the database to find <see cref="SAMPLE_SIZE"/> inside the old root.
	/// </summary>
	private const int SAMPLE_QUERY_SIZE = SAMPLE_SIZE * 4;

	/// <summary>
	/// Checks the indexed folder and asks for its new location if it is missing.
	/// </summary>
	/// <returns>The folder to index, which is <paramref name="indexerPath"/> if it was not moved, or null if the application must close.</returns>
	public async Task<string?> RunAsync(string indexerPath)
	{
		if (string.IsNullOrWhiteSpace(indexerPath) || directoryExists(indexerPath))
			return indexerPath;

		output.WriteLine();
		output.WriteLine($"The indexed folder was not found: {indexerPath}");
		output.WriteLine("It may have been moved, renamed, or be on a drive that is not connected.");

		var sample = await GetSampleAsync(indexerPath);

		while (true)
		{
			output.Write("Enter the new location of the indexed folder, or leave empty to close: ");
			string? answer = input.ReadLine();
			string newPath = CleanPath(answer);
			if (newPath.Length == 0)
			{
				output.WriteLine("No new location given. GrinVideoEncoder will now close.");
				return null;
			}

			newPath = Path.GetFullPath(newPath);
			if (!directoryExists(newPath))
			{
				output.WriteLine($"Folder not found: {newPath}");
				continue;
			}

			if (!ConfirmVideosFound(indexerPath, newPath, sample))
				continue;

			int updated = await relocate(indexerPath, newPath);
			output.WriteLine($"Moved {updated} indexed videos to {newPath}");
			return newPath;
		}
	}

	private async Task<List<VideoFile>> GetSampleAsync(string indexerPath)
	{
		var videos = await getRandomVideos(SAMPLE_QUERY_SIZE);
		return [.. videos
			.Where(v => GlobUtils.IsInsideDirectory(indexerPath, v.DirectoryPath))
			.Take(SAMPLE_SIZE)];
	}

	/// <summary>
	/// Looks for the sampled videos in the new folder, and asks to confirm if only some of them are there.
	/// </summary>
	/// <returns>True if the new folder can be used.</returns>
	private bool ConfirmVideosFound(string oldRoot, string newRoot, List<VideoFile> sample)
	{
		if (sample.Count == 0)
			return true;

		int found = sample.Count(v => fileExists(Path.Combine(GlobUtils.RebaseDirectory(oldRoot, newRoot, v.DirectoryPath)!, v.Filename)));
		output.WriteLine($"Found {found} of {sample.Count} sampled indexed videos in {newRoot}");

		if (found == sample.Count)
			return true;

		if (found == 0)
		{
			output.WriteLine("None of them are there, so this is probably not the indexed folder.");
			return false;
		}

		output.Write("Use this folder anyway? Indexed videos that are not found will be removed at the next indexing. [y/N]: ");
		string? answer = input.ReadLine()?.Trim();
		return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Removes the spaces and the quotes that a pasted or dropped path may have.
	/// </summary>
	private static string CleanPath(string? path) =>
		(path ?? string.Empty).Trim().Trim('"', '\'').Trim();
}
