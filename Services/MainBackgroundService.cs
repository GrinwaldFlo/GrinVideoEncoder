using Serilog;

namespace GrinVideoEncoder.Services;

public class MainBackgroundService : BackgroundService
{
	private readonly CommunicationService _communication;
	private readonly IAppSettings _settings;
	private readonly LogMain _log;
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

	/// <summary>
	/// Clean trash folder but keep last 2 files
	/// </summary>
	private void CleanTrash()
	{
		if (!Directory.Exists(_settings.TrashPath))
			return;
		DirectoryInfo trashDir = new(_settings.TrashPath);
		var files = trashDir.GetFiles()
		.OrderByDescending(f => f.LastWriteTime)
		.Skip(2)
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
	}

	private async Task ProcessVideo(string filePath)
	{
		_communication.VideoProcessToken = new CancellationTokenSource();
		await _videoProcessor.ProcessVideo(filePath, _communication);
	}
}