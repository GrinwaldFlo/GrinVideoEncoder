namespace GrinVideoEncoder;

public class AppSettings : IAppSettings
{
	public string InputPath => Path.GetFullPath(Path.Combine(WorkPath, "Input"));
	public string TrashPath => Path.GetFullPath(Path.Combine(WorkPath, "Trash"));
	public string ProcessingPath => Path.GetFullPath(Path.Combine(WorkPath, "Processing"));
	public string OutputPath => Path.GetFullPath(Path.Combine(WorkPath, "Output"));
	public string FailedPath => Path.GetFullPath(Path.Combine(WorkPath, "Failed"));
	public string TempPath => Path.GetFullPath(Path.Combine(WorkPath, "Temp"));
	public string LogPath => Path.GetFullPath(Path.Combine(WorkPath, "Log"));
	public string DatabasePath => Path.GetFullPath(Path.Combine(WorkPath, "videoindex.db"));

	public bool ForceCpu { get; set; } = false;

	/// <inheritdoc/>
	public int QualityLevel { get; set; } = 23;

	/// <inheritdoc/>
	public string IndexerPath { get; set; } = string.Empty;

	/// <inheritdoc/>
	public List<string> VideoExtensions { get; set; } = [];

	/// <inheritdoc/>
	public int MinFileSizeMB { get; set; } = 100;
	public string WorkPath { get; set; } = "Data";

	public List<string> IgnoreFolders { get; set; } = [];
}