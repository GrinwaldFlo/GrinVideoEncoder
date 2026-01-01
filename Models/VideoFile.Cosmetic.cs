namespace GrinVideoEncoder.Models;

public partial class VideoFile
{
	public string StatusColor => Status switch
	{
		CompressionStatus.Original => "#6c757d",      // Gray
		CompressionStatus.Compressed => "#28a745",    // Green
		CompressionStatus.FailedToCompress => "#dc3545", // Red
		CompressionStatus.Bigger => "#ffc107",        // Amber
		CompressionStatus.Removed => "#6c757d",       // Gray
		CompressionStatus.ToProcess => "#007bff",     // Blue
		CompressionStatus.Processing => "#17a2b8",    // Cyan
		CompressionStatus.Kept => "#20c997",          // Teal
		_ => "#6c757d"                                 // Default Gray
	};

	public string StatusDisplayName => Status switch
	{
		CompressionStatus.Original => "Original",
		CompressionStatus.Compressed => "Compressed",
		CompressionStatus.FailedToCompress => "Failed",
		CompressionStatus.Bigger => "Bigger",
		CompressionStatus.Removed => "Removed",
		CompressionStatus.ToProcess => "To Process",
		CompressionStatus.Processing => "Processing",
		CompressionStatus.Kept => "Kept",
		_ => "Unknown"
	};

	public string StatusIcon => Status switch
	{
		CompressionStatus.Original => "backup",
		CompressionStatus.Compressed => "compress",
		CompressionStatus.FailedToCompress => "error",
		CompressionStatus.Bigger => "trending_up",
		CompressionStatus.Removed => "delete",
		CompressionStatus.ToProcess => "schedule",
		CompressionStatus.Processing => "hourglass_bottom",
		CompressionStatus.Kept => "check_circle",
		_ => "help"
	};

	/// <summary>
	/// Format FileSizeOriginal as human-readable string (e.g., "123.45 MB")
	/// </summary>
	public string FileSizeOriginalFormatted => FormatBytes(FileSizeOriginal);

	/// <summary>
	/// Format FileSizeCompressed as human-readable string (e.g., "45.67 MB")
	/// </summary>
	public string FileSizeCompressedFormatted => FormatBytes(FileSizeCompressed ?? 0);

	public string DurationFormatted => FormatDuration(DurationSeconds);

	public string Resolution => FormatResolution(Width, Height);

	/// <summary>
	/// Formats bytes to human-readable format (B, KB, MB, GB, TB)
	/// </summary>
	private static string FormatBytes(long bytes)
	{
		string[] sizes = { "B", "KB", "MB", "GB", "TB" };
		double len = bytes;
		int order = 0;

		while (len >= 1024 && order < sizes.Length - 1)
		{
			order++;
			len /= 1024;
		}

		return $"{len:0.##} {sizes[order]}";
	}

	private static string FormatDuration(long? durationSeconds)
	{
		if (!durationSeconds.HasValue)
			return "-";

		var totalSeconds = durationSeconds.Value;
		var hours = totalSeconds / 3600;
		var minutes = (totalSeconds % 3600) / 60;
		var seconds = totalSeconds % 60;

		if (hours >= 1)
			return $"{hours}:{minutes:D2}:{seconds:D2}";
		return $"{minutes}:{seconds:D2}";
	}

	private static string FormatResolution(int? width, int? height)
	{
		if (!width.HasValue || !height.HasValue)
			return "-";
		return $"{width}×{height}";
	}

}
