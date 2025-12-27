namespace GrinVideoEncoder.Models;

public class VideoFile
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string DirectoryPath { get; set; } = string.Empty;
	public string Filename { get; set; } = string.Empty;
	public long FileSizeOriginal { get; set; }
	public long? FileSizeCompressed { get; set; }
	/// <summary>
	/// Duration of the video in seconds.
	/// </summary>
	public long? DurationSeconds { get; set; }
	public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
	public DateTime LastModified { get; set; }

	public string FullPath => Path.Combine(DirectoryPath, Filename);

	public double? CompressionFactor => FileSizeCompressed.HasValue && FileSizeOriginal > 0
		? (double)FileSizeOriginal * 100.0 / FileSizeCompressed.Value
		: null;

	public double? QualityRatioCompressed => FileSizeCompressed.HasValue && DurationSeconds.HasValue && DurationSeconds > 0
		? (double)FileSizeCompressed.Value / DurationSeconds.Value
		: null;

	public double? QualityRatioOriginal => DurationSeconds.HasValue && DurationSeconds > 0
	? (double)FileSizeOriginal / DurationSeconds.Value
	: null;

	public double? QualityRatio => FileSizeCompressed.HasValue
		? QualityRatioCompressed
		: QualityRatioOriginal;
}
