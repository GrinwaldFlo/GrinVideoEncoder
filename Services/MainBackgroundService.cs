namespace GrinVideoEncoder.Services;

public class MainBackgroundService : BackgroundService
{
	/// <summary>
	/// Number of files <see cref="CleanTrash"/> keeps in the trash.
	/// </summary>
	private const int TRASH_KEEP_COUNT = 100;

	private readonly CommunicationService _communication;
	private readonly LogMain _log;
	private readonly IAppSettings _settings;
	private readonly VideoProcessorService _videoProcessor;
	private readonly FileSystemWatcher _watcher;

	public MainBackgroundService(IAppSettings settings, LogMain log, VideoProcessorService videoProcessor, CommunicationService communication)
	{
		Directory.CreateDirectory(settings.InputPath);
		_settings = settings;
		_log = log;
		_videoProcessor = videoProcessor;
		_communication = communication;
		_watcher = new FileSystemWatcher(settings.InputPath)
		{
			EnableRaisingEvents = true,
			NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
		};
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		DirectoryInfo inputDir = new(_settings.InputPath);
		FileInfo? file = null;
		// A file that could not be moved out of the input folder must not be picked again forever
		var triedFiles = new HashSet<string>(StringComparer.Ordinal);
		_log.Information("Checking for existing files in {InputPath}", _settings.InputPath);
		do
		{
			file = inputDir.GetFiles()
				.Where(f => !triedFiles.Contains(f.FullName))
				.OrderByDescending(f => f.LastWriteTime)
				.FirstOrDefault();
			if (file != null)
			{
				triedFiles.Add(file.FullName);
				await ProcessVideo(file.FullName);
				await Task.Delay(10000, stoppingToken);
			}
		} while (file != null && !stoppingToken.IsCancellationRequested);

		_watcher.Created += async (sender, e) => await ProcessVideo(e.FullPath);

		_log.Information("Waiting for new files in {InputPath}", _settings.InputPath);
		while (!stoppingToken.IsCancellationRequested)
		{
			await Task.Delay(10000, stoppingToken);
			CleanTrash(_settings.TrashPath, TRASH_KEEP_COUNT);
		}
	}

	private static void CleanEmptySubFolders(string trashPath)
	{
		if (!Directory.Exists(trashPath))
			return;

		try
		{
			foreach (string directory in Directory.GetDirectories(trashPath))
			{
				CleanEmptySubFolders(directory);
				if (!Directory.EnumerateFileSystemEntries(directory).Any())
				{
					try
					{
						Directory.Delete(directory, false);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Failed to delete directory {directory}: {ex.Message}");
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to clean empty subfolders in {trashPath}: {ex.Message}");
		}
	}

	/// <summary>
	/// Deletes the oldest files of the trash, keeping the <paramref name="keepCount"/> newest, then its empty folders.
	/// </summary>
	internal static void CleanTrash(string trashPath, int keepCount)
	{
		if (!Directory.Exists(trashPath))
			return;
		DirectoryInfo trashDir = new(trashPath);
		var files = trashDir.GetFiles("*.*", SearchOption.AllDirectories)
		.OrderByDescending(f => f.LastWriteTime)
		.Skip(keepCount)
		.ToArray();

		foreach (var file in files)
		{
			try
			{
				file.Delete();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Failed to delete file {file.FullName}: {ex.Message}");
			}
		}
		CleanEmptySubFolders(trashPath);
	}

	/// <summary>
	/// Never throws: it runs in an async void event handler, where an exception would close the application.
	/// </summary>
	private async Task ProcessVideo(string filePath)
	{
		try
		{
			_communication.VideoProcessToken = new CancellationTokenSource();
			await _videoProcessor.ProcessVideo(filePath, _communication);
		}
		catch (Exception ex)
		{
			_log.Error(ex, "Failed to process {FilePath}", filePath);
		}
	}
}