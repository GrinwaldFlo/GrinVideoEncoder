namespace GrinVideoEncoder.Interfaces;

public interface IAppSettings
{
	string InputPath { get; set; }
	string ProcessingPath { get; set; }
	string OutputPath { get; set; }
	string FailedPath { get; set; }
	string TempPath { get; set; }
	string TrashPath { get; set; }

	string LogPath { get; set; }

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
	string DatabasePath { get; set; }

	/// <summary>
	/// List of video file extensions to index (e.g., ".mp4", ".mkv").
	/// </summary>
	string[] VideoExtensions { get; set; }

	/// <summary>
	/// Minimum file size in MB for a video to be indexed.
	/// </summary>
	int MinFileSizeMB { get; set; }
}
