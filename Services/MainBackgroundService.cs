﻿namespace GrinVideoEncoder.Services;

public class MainBackgroundService : BackgroundService
{
	private readonly string _inputPath = string.Empty;
	private readonly VideoProcessorService _videoProcessor;
	private readonly FileSystemWatcher _watcher;

	public MainBackgroundService(IConfiguration config, VideoProcessorService videoProcessor)
	{
		_videoProcessor = videoProcessor;
		_inputPath = config["Folders:Input"] ?? throw new Exception("Folders:Input not defined");

		_watcher = new FileSystemWatcher(_inputPath)
		{
			EnableRaisingEvents = true,
			NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
		};
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_watcher.Created += async (sender, e) => await _videoProcessor.ProcessVideo(e.FullPath, stoppingToken);

		while (!stoppingToken.IsCancellationRequested)
		{
			await Task.Delay(1000, stoppingToken);
		}
	}
}