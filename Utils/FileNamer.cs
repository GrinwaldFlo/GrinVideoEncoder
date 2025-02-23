namespace GrinVideoEncoder.Utils;

/// <summary>
/// Generates all full paths for a processed file.
/// </summary>
public class FileNamer
{
	public const string NEW_FILE_PREFIX = "_recoded.mp4";

	public FileNamer(IAppSettings settings, string originalPath)
	{
		FileInfo _fileInfo = new(originalPath);
		InputPath = originalPath;

		string filenameNoExt = Path.GetFileNameWithoutExtension(originalPath);

		NewFileName = $"{filenameNoExt}{NEW_FILE_PREFIX}";
		FailedPath = GlobUtils.EnsureUniqueFilename(Path.Combine(settings.FailedPath, _fileInfo.Name));
		ProcessingPath = GlobUtils.EnsureUniqueFilename(Path.Combine(settings.ProcessingPath, _fileInfo.Name));
		OutputPath = GlobUtils.EnsureUniqueFilename(Path.Combine(settings.OutputPath, NewFileName));
		TempPath = GlobUtils.EnsureUniqueFilename(Path.Combine(settings.TempPath, _fileInfo.Name));
		TempFirstPassPath = GlobUtils.EnsureUniqueFilename(Path.Combine(settings.TempPath, $"{filenameNoExt}_firstPass.mp4"));
		TrashPath = GlobUtils.EnsureUniqueFilename(Path.Combine(settings.TrashPath, _fileInfo.Name));
		StatFileName = GlobUtils.EnsureUniqueFilename(Path.Combine(settings.LogPath, "Stats", $"{filenameNoExt}_stat.log"));
	}

	/// <summary>
	/// Path to the file that failed to process.
	/// </summary>
	public string FailedPath { get; private set; }
	/// <summary>
	/// Orignal file path.
	/// </summary>
	public string InputPath { get; private set; }

	/// <summary>
	/// New file name with <see cref="NEW_FILE_PREFIX"/>.
	/// </summary>
	public string NewFileName { get; private set; }

	/// <summary>
	/// Final output path
	/// </summary>
	public string OutputPath { get; private set; }

	/// <summary>
	/// Orignal file while being processed.
	/// </summary>
	public string ProcessingPath { get; private set; }
	/// <summary>
	/// Path to the stats file.
	/// </summary>
	public string StatFileName { get; private set; }

	/// <summary>
	/// Partial file while being processed.
	/// </summary>
	public string TempPath { get; private set; }
	public string TempFirstPassPath { get; private set; }
	/// <summary>
	/// Orginal processed file after successful processing.
	/// </summary>
	public string TrashPath { get; private set; }
}