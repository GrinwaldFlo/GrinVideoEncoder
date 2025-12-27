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
}
