using Serilog;

namespace GrinVideoEncoder.Services;

public class MainBackgroundService : BackgroundService
{
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
		_log.Information("Checking for existing files in {InputPath}", _settings.InputPath);
		do
		{
			file = inputDir.GetFiles().OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
			if (file != null)
			{
				await ProcessVideo(file.FullName);
				await Task.Delay(10000, stoppingToken);
			}
		} while (file != null && !stoppingToken.IsCancellationRequested);

		_watcher.Created += async (sender, e) => await ProcessVideo(e.FullPath);

		Log.Information("Waiting for new files in {InputPath}", _settings.InputPath);
		while (!stoppingToken.IsCancellationRequested)
		{
			await Task.Delay(10000, stoppingToken);
			CleanTrash();
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
	/// Clean trash folder but keep last 2 files
	/// </summary>
	private void CleanTrash()
	{
		if (!Directory.Exists(_settings.TrashPath))
			return;
		DirectoryInfo trashDir = new(_settings.TrashPath);
		var files = trashDir.GetFiles("*.*", SearchOption.AllDirectories)
		.OrderByDescending(f => f.LastWriteTime)
		.Skip(100)
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
		CleanEmptySubFolders(_settings.TrashPath);
	}

	private async Task ProcessVideo(string filePath)
	{
		_communication.VideoProcessToken = new CancellationTokenSource();
		await _videoProcessor.ProcessVideo(filePath, _communication);
	}
}