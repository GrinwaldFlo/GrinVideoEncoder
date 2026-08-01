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
}