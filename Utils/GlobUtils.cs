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
}