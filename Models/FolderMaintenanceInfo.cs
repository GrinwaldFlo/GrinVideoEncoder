namespace GrinVideoEncoder.Models;

public class FolderMaintenanceInfo
{
	public string Name { get; set; } = string.Empty;
	public string Path { get; set; } = string.Empty;
	public long SizeBytes { get; set; }
	public int FileCount { get; set; }
	public List<FileInfo> Files { get; set; } = [];

	public string SizeFormatted => SizeBytes switch
	{
		< 1024 => $"{SizeBytes} B",
		< 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
		< 1024 * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
		_ => $"{SizeBytes / (1024.0 * 1024 * 1024):F2} GB"
	};
}
