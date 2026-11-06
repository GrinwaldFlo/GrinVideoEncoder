namespace GrinVideoEncoder.Utils;

public static class GlobUtils
{
	/// <summary>
	/// Checks if the specified file is ready for reading.
	/// </summary>
	/// <param name="filePath">The path to the file to check.</param>
	/// <returns>True if the file is ready for reading, otherwise false.</returns>
	public static bool IsFileReady(string filePath)
	{
		try
		{
			using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
			if (stream.Length > 0)
				return true;
		}
		catch
		{
			// Do nothing
		}
		return false;
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
	public static string GetTrashDirectory(string trashRoot, string indexerPath, string videoDirectory)
	{
		string relativePath = Path.GetRelativePath(Path.GetFullPath(indexerPath), Path.GetFullPath(videoDirectory));
		if (Path.IsPathRooted(relativePath) || relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
			throw new InvalidOperationException($"{videoDirectory} is not inside the indexed folder {indexerPath}");

		return Path.GetFullPath(Path.Combine(trashRoot, relativePath));
	}

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