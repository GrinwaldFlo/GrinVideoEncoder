namespace GrinVideoEncoder;

public class AppSettings : IAppSettings
{
	public string InputPath { get; set; } = string.Empty;
	public string TrashPath { get; set; } = string.Empty;
	public string ProcessingPath { get; set; } = string.Empty;
	public string OutputPath { get; set; } = string.Empty;
	public string FailedPath { get; set; } = string.Empty;
	public string TempPath { get; set; } = string.Empty;
	public string LogPath { get; set; } = string.Empty;
	public bool ForceCpu { get; set; } = false;

	//<inheritdoc/>
	public int BitrateKbS { get; set; } = 3000;

	/// <inheritdoc/>
	public int QualityLevel { get; set; } = 23;

	/// <inheritdoc/>
	public string IndexerPath { get; set; } = string.Empty;

	/// <inheritdoc/>
	public string DatabasePath { get; set; } = "videoindex.db";

	/// <inheritdoc/>
	public string[] VideoExtensions { get; set; } = [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm"];

	/// <inheritdoc/>
	public int MinFileSizeMB { get; set; } = 100;
}