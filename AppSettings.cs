using System.Text.Json.Serialization;

namespace GrinVideoEncoder;

public class AppSettings : IAppSettings
{
	private static readonly string RoamingRoot = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
		"GrinVideoEncoder");

	private static readonly string LocalRoot = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"GrinVideoEncoder");

	[JsonIgnore]
	public string ConfigName { get; set; } = string.Empty;

	[JsonIgnore]
	public string ConfigFilePath => Path.Combine(RoamingRoot, ConfigName, "config.json");

	[JsonIgnore]
	public string DatabasePath => Path.Combine(RoamingRoot, ConfigName, "videoindex.db");

	[JsonIgnore]
	public string WorkPath { get; set; } = string.Empty;

	[JsonIgnore]
	public string InputPath => Path.GetFullPath(Path.Combine(WorkPath, "Input"));

	[JsonIgnore]
	public string TrashPath => Path.GetFullPath(Path.Combine(WorkPath, "Trash"));

	[JsonIgnore]
	public string ProcessingPath => Path.GetFullPath(Path.Combine(WorkPath, "Processing"));

	[JsonIgnore]
	public string OutputPath => Path.GetFullPath(Path.Combine(WorkPath, "Output"));

	[JsonIgnore]
	public string FailedPath => Path.GetFullPath(Path.Combine(WorkPath, "Failed"));

	[JsonIgnore]
	public string TempPath => Path.GetFullPath(Path.Combine(WorkPath, "Temp"));

	[JsonIgnore]
	public string LogPath => Path.GetFullPath(Path.Combine(WorkPath, "Log"));

	public bool ForceCpu { get; set; } = false;

	/// <inheritdoc/>
	public int QualityLevel { get; set; } = 23;

	/// <inheritdoc/>
	public string IndexerPath { get; set; } = string.Empty;

	/// <inheritdoc/>
	public List<string> VideoExtensions { get; set; } = [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm"];

	/// <inheritdoc/>
	public int MinFileSizeMB { get; set; } = 100;

	public List<string> IgnoreFolders { get; set; } = [];

	public double EncodingThreshold { get; set; } = 900;

	public double MinFileSizeMb { get; set; } = 100;

	public double MinFileAgeH { get; set; } = 100;

	/// <summary>
	/// Initializes computed paths based on ConfigName. Must be called after ConfigName is set.
	/// </summary>
	public void InitializePaths()
	{
		WorkPath = Path.Combine(LocalRoot, ConfigName);
	}

	/// <summary>
	/// Returns the roaming root folder for GrinVideoEncoder configs.
	/// </summary>
	public static string GetRoamingRoot() => RoamingRoot;
}