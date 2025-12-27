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
	/// <summary>
	/// Video width in pixels.
	/// </summary>
	public int? Width { get; set; }
	/// <summary>
	/// Video height in pixels.
	/// </summary>
	public int? Height { get; set; }
	public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
	public DateTime LastModified { get; set; }

	public string FullPath => Path.Combine(DirectoryPath, Filename);

	/// <summary>
	/// Total pixels per frame (Width × Height).
	/// </summary>
	public long? TotalPixels => Width.HasValue && Height.HasValue && Width > 0 && Height > 0
		? (long)Width.Value * Height.Value
		: null;

	public double? CompressionFactor => FileSizeCompressed.HasValue && FileSizeOriginal > 0
		? (double)FileSizeOriginal * 100.0 / FileSizeCompressed.Value
		: null;

	/// <summary>
	/// Quality ratio for compressed file normalized by resolution (bytes per pixel per second).
	/// Lower values indicate more compression.
	/// </summary>
	public double? QualityRatioCompressed => FileSizeCompressed.HasValue && DurationSeconds.HasValue && DurationSeconds > 0 && TotalPixels.HasValue
		? (double)FileSizeCompressed.Value / DurationSeconds.Value / TotalPixels.Value * 1000.0
		: null;

	/// <summary>
	/// Quality ratio for original file normalized by resolution (bytes per pixel per second).
	/// Higher values may indicate over-quality suitable for re-encoding.
	/// </summary>
	public double? QualityRatioOriginal => DurationSeconds.HasValue && DurationSeconds > 0 && TotalPixels.HasValue
		? (double)FileSizeOriginal / DurationSeconds.Value / TotalPixels.Value * 1000.0
		: null;

	public double? QualityRatio => FileSizeCompressed.HasValue
		? QualityRatioCompressed
		: QualityRatioOriginal;
}
