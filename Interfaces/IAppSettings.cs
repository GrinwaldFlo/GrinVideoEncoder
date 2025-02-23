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
	/// Bitrate in kilobits per second
	/// </summary>
	int BitrateKbS { get; set; }
}
