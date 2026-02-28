using GrinVideoEncoder.Models;

namespace GrinVideoEncoder.Services;

public class MaintenanceService(AppSettings appSettings)
{
	private readonly (string Name, Func<string> PathGetter)[] _folders =
	[
		("Processing", () => appSettings.ProcessingPath),
		("Trash", () => appSettings.TrashPath),
		("Log", () => appSettings.LogPath),
		("Failed", () => appSettings.FailedPath),
		("Temp", () => appSettings.TempPath),
	];

	public List<FolderMaintenanceInfo> GetFolderInfos()
	{
		var result = new List<FolderMaintenanceInfo>();

		foreach (var (name, pathGetter) in _folders)
		{
			var path = pathGetter();
			var info = new FolderMaintenanceInfo { Name = name, Path = path };

			if (Directory.Exists(path))
			{
				var dirInfo = new DirectoryInfo(path);
				var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
				info.FileCount = files.Length;
				info.SizeBytes = files.Sum(f => f.Length);
				info.Files = [.. files];
			}

			result.Add(info);
		}

		return result;
	}

	public static void ClearFolder(string folderPath)
	{
		if (!Directory.Exists(folderPath))
			return;

		var dirInfo = new DirectoryInfo(folderPath);

		foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
			file.Delete();

		foreach (var dir in dirInfo.GetDirectories())
			dir.Delete(true);
	}
}
