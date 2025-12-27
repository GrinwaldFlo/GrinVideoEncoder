namespace GrinVideoEncoder.Models;

public class VideoFile
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string DirectoryPath { get; set; } = string.Empty;
	public string Filename { get; set; } = string.Empty;
	public long FileSizeOriginal { get; set; }
	public long? FileSizeCompressed { get; set; }
	public TimeSpan Duration { get; set; }
	public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
	public DateTime LastModified { get; set; }

	public string FullPath => Path.Combine(DirectoryPath, Filename);
}
