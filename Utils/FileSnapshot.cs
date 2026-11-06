namespace GrinVideoEncoder.Utils;

/// <summary>
/// Size and last write time of a file. Two equal snapshots taken some time apart mean nothing wrote to the file in between.
/// </summary>
public readonly record struct FileSnapshot(long Length, DateTime LastWriteTimeUtc)
{
	/// <returns>The snapshot, or null if the file does not exist.</returns>
	public static FileSnapshot? TryRead(string filePath)
	{
		var info = new FileInfo(filePath);
		return info.Exists ? new FileSnapshot(info.Length, info.LastWriteTimeUtc) : null;
	}
}
