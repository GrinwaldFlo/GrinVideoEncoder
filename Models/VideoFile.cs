namespace GrinVideoEncoder.Models;

public enum CompressionStatus
{
	Original = 0,
	Compressed = 1,
	FailedToCompress = 2,
	Bigger = 3,
	Removed = 4,
	ToProcess = 5,
	Processing = 6,
	Kept = 7
}

public partial class VideoFile
{
	public double? CompressionFactor => FileSizeCompressed.HasValue && FileSizeOriginal > 0
		? (((double)FileSizeOriginal / FileSizeCompressed.Value) - 1) * 100.0
		: null;

	public string DirectoryPath { get; set; } = string.Empty;

	/// <summary>
	/// Duration of the video in seconds.
	/// </summary>
	public long? DurationSeconds { get; set; }

	public string Filename { get; set; } = string.Empty;
	public long? FileSizeCompressed { get; set; }
	public long FileSizeOriginal { get; set; }
	public double? Fps { get; set; }
	public string FullPath => Path.GetFullPath(Path.Combine(DirectoryPath, Filename));

	/// <summary>
	/// Video height in pixels.
	/// </summary>
	public int? Height { get; set; }

	public Guid Id { get; set; } = Guid.NewGuid();
	public DateTime IndexedAt { get; set; } = DateTime.UtcNow;

	public DateTime LastModified { get; set; }

	public double? QualityRatio => FileSizeCompressed.HasValue
			? QualityRatioCompressed
			: QualityRatioOriginal;

	/// <summary>
	/// Quality ratio for compressed file normalized by resolution (bytes per pixel per second).
	/// Lower values indicate more compression.
	/// </summary>
	public double? QualityRatioCompressed => FileSizeCompressed.HasValue && DurationSeconds.HasValue && Fps.HasValue && DurationSeconds > 0 && TotalPixels.HasValue
		? (double)FileSizeCompressed.Value / DurationSeconds.Value / TotalPixels.Value * 1000.0 * (30.0 / Fps.Value)
		: null;

	/// <summary>
	/// Quality ratio for original file normalized by resolution (bytes per pixel per second).
	/// Higher values may indicate over-quality suitable for re-encoding.
	/// </summary>
	public double? QualityRatioOriginal => DurationSeconds.HasValue && DurationSeconds > 0 && Fps.HasValue && TotalPixels.HasValue
		? (double)FileSizeOriginal / DurationSeconds.Value / TotalPixels.Value * 1000.0 * (30.0 / Fps.Value)
		: null;

	/// <summary>
	/// Indicates whether the video is original, compressed, or failed to compress.
	/// </summary>
	public CompressionStatus Status { get; set; } = CompressionStatus.Original;

	/// <summary>
	/// Total pixels per frame (Width × Height).
	/// </summary>
	public long? TotalPixels => Width.HasValue && Height.HasValue && Width > 0 && Height > 0
		? (long)Width.Value * Height.Value
		: null;

	/// <summary>
	/// Video width in pixels.
	/// </summary>
	public int? Width { get; set; }
}