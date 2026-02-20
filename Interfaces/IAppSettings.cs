namespace GrinVideoEncoder.Interfaces;

public interface IAppSettings
{
	string ConfigName { get; set; }
	string ConfigFilePath { get; }
	string WorkPath { get; }
	string InputPath { get; }
	string ProcessingPath { get; }
	string OutputPath { get; }
	string FailedPath { get; }
	string TempPath { get; }
	string TrashPath { get; }
	string LogPath { get; }

	bool ForceCpu { get; set; }

	/// <summary>
	/// Quality level for encoding (CRF/CQ value).
	/// Lower values = better quality, larger files.
	/// Recommended range: 18-28. Default: 23.
	/// </summary>
	int QualityLevel { get; set; }

	/// <summary>
	/// Path to the directory to index for video files.
	/// </summary>
	string IndexerPath { get; set; }

	/// <summary>
	/// Path to the SQLite database file for the video indexer.
	/// </summary>
	string DatabasePath { get; }

	/// <summary>
	/// List of video file extensions to index (e.g., ".mp4", ".mkv").
	/// </summary>
	List<string> VideoExtensions { get; set; }

	/// <summary>
	/// Minimum file size in MB for a video to be indexed.
	/// </summary>
	int MinFileSizeMB { get; set; }
	List<string> IgnoreFolders { get; set; }

	double EncodingThreshold { get; set; }
	double MinFileSizeMb { get; set; }
	double MinFileAgeH { get; set; }
}
