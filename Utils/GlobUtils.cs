namespace GrinVideoEncoder.Utils;

public static class GlobUtils
{
	/// <summary>
	/// Checks that no other process holds the file open, by opening it exclusively.
	/// An empty file can be unlocked: whether it is still being written is told by <see cref="FileSnapshot"/>.
	/// </summary>
	/// <param name="filePath">The path to the file to check.</param>
	/// <returns>True if the file exists and could be opened exclusively, otherwise false.</returns>
	public static bool IsFileUnlocked(string filePath)
	{
		try
		{
			using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static string EnsureUniqueFilename(string filePath)
	{
		string directory = Path.GetDirectoryName(filePath) ?? "";

		Directory.CreateDirectory(directory);

		string fileName = Path.GetFileNameWithoutExtension(filePath);
		string extension = Path.GetExtension(filePath);
		string newFilePath = filePath;
		int i = 1;
		while (File.Exists(newFilePath))
		{
			newFilePath = Path.Combine(directory, $"{fileName} ({i}){extension}");
			i++;
		}
		return newFilePath;
	}

	public static async Task CopyFileWithProgressAsync(string sourcePath, string destinationPath, IObserver<double?> progressObserver, CancellationToken cancellationToken)
	{
		const int bufferSize = 1024 * 1024; // 1 MB buffer
		var fileInfo = new FileInfo(sourcePath);
		long fileSize = fileInfo.Length;
		long copiedBytes = 0;
		double lastReportedPercent = -1;

		using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
		using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
		byte[] buffer = new byte[bufferSize];
		int bytesRead;

		while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
		{
			await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
			copiedBytes += bytesRead;

			double currentPercent = copiedBytes * 100.0 / fileSize;
			int percentInt = (int)currentPercent;
			if (percentInt != (int)lastReportedPercent)
			{
				progressObserver.OnNext(currentPercent);
				lastReportedPercent = currentPercent;
			}
		}
	}

	/// <summary>
	/// Gets the trash folder of a video, keeping its folder structure relative to the indexed root.
	/// </summary>
	/// <exception cref="InvalidOperationException">The video is not inside the indexed root, so its trash folder would be outside the trash.</exception>
	public static string GetTrashDirectory(string trashRoot, string indexerPath, string videoDirectory) =>
		RebaseDirectory(indexerPath, trashRoot, videoDirectory)
			?? throw new InvalidOperationException($"{videoDirectory} is not inside the indexed folder {indexerPath}");

	/// <summary>
	/// Moves <paramref name="directory"/> from <paramref name="oldRoot"/> to <paramref name="newRoot"/>, keeping its folder structure relative to the root.
	/// </summary>
	/// <returns>The directory under the new root, or null if <paramref name="directory"/> is not inside <paramref name="oldRoot"/>.</returns>
	public static string? RebaseDirectory(string oldRoot, string newRoot, string directory)
	{
		string? relativePath = GetRelativePathInside(oldRoot, directory);
		return relativePath is null ? null : Path.GetFullPath(Path.Combine(newRoot, relativePath));
	}

	/// <summary>
	/// Returns true if <paramref name="directory"/> is <paramref name="root"/> or one of its subfolders.
	/// </summary>
	public static bool IsInsideDirectory(string root, string directory) =>
		GetRelativePathInside(root, directory) is not null;

	/// <summary>
	/// Returns true for a path written on Windows, such as <c>V:\Videos</c> or <c>\\server\share</c>.
	/// </summary>
	internal static bool IsWindowsStylePath(string path) =>
		path.StartsWith(@"\\", StringComparison.Ordinal)
		|| (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':' && (path.Length == 2 || path[2] is '\\' or '/'));

	/// <returns>The path of <paramref name="directory"/> relative to <paramref name="root"/>, or null if it is not inside it.</returns>
	private static string? GetRelativePathInside(string root, string directory)
	{
		if (!OperatingSystem.IsWindows() && (IsWindowsStylePath(root) || IsWindowsStylePath(directory)))
			return GetWindowsRelativePathInside(root, directory);

		string relativePath = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(directory));
		bool isOutside = Path.IsPathRooted(relativePath) || relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
		return isOutside ? null : relativePath;
	}

	/// <summary>
	/// <see cref="GetRelativePathInside"/> for paths written on Windows, such as a database indexed on Windows and opened
	/// on Linux, where <see cref="Path"/> does not see <c>\</c> as a separator nor ignore the case.
	/// </summary>
	/// <returns>The relative path with the separators of this system, or null if <paramref name="directory"/> is not inside <paramref name="root"/>.</returns>
	private static string? GetWindowsRelativePathInside(string root, string directory)
	{
		string[] rootSegments = SplitWindowsPath(root);
		string[] directorySegments = SplitWindowsPath(directory);

		if (directorySegments.Length < rootSegments.Length
			|| directorySegments.Contains("..")
			|| !rootSegments.SequenceEqual(directorySegments.Take(rootSegments.Length), StringComparer.OrdinalIgnoreCase))
			return null;

		return Path.Combine(directorySegments[rootSegments.Length..]);
	}

	private static string[] SplitWindowsPath(string path) =>
		path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
			.Where(segment => segment != ".")
			.ToArray();

	/// <summary>
	/// Finds an executable in the folders of the PATH environment variable.
	/// </summary>
	/// <returns>The full path of the executable, or null if it was not found.</returns>
	public static string? FindExecutableInPath(string name)
	{
		string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
			.Select(folder => Path.Combine(folder, name))
			.FirstOrDefault(File.Exists);
	}

	/// <summary>
	/// Formats a file size for reports, also when the file does not exist.
	/// </summary>
	public static string FormatFileSizeMb(string filePath) =>
		File.Exists(filePath) ? $"{new FileInfo(filePath).Length / (1024 * 1024.0):F2} MB" : "n/a";

	public static string GetFfmpegExecutablePath()
	{
		string ffmpegPath = Path.Combine(Xabe.FFmpeg.FFmpeg.ExecutablesPath, "ffmpeg");
		if (OperatingSystem.IsWindows())
		{
			ffmpegPath += ".exe";
		}
		return ffmpegPath;
	}

	public static string GetFfprobeExecutablePath()
	{
		string ffprobePath = Path.Combine(Xabe.FFmpeg.FFmpeg.ExecutablesPath, "ffprobe");
		if (OperatingSystem.IsWindows())
		{
			ffprobePath += ".exe";
		}
		return ffprobePath;
	}
}