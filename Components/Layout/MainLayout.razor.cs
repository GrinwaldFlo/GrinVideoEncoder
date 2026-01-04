using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using Microsoft.AspNetCore.Components;

namespace GrinVideoEncoder.Components.Layout;

public partial class MainLayout : IDisposable
{
	[Inject] private CommunicationService Comm { get; set; } = null!;
	[Inject] private LogMain LogMain { get; set; } = null!;
	[Inject] private LogFfmpeg LogFfmpeg { get; set; } = null!;

	private bool _disposedValue;

	private readonly CompositeDisposable _disposables = [];

	private int _videoToProcessCount = 0;

	private double _encodingPercent = 100;
	private bool _progressShowValue = false;
	private Radzen.ProgressBarMode _encodingProgressMode = Radzen.ProgressBarMode.Indeterminate;

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		_disposables.Add(Comm.Status.IsRunning.Subscribe(async _ => await Refresh()));
		_disposables.Add(Comm.Status.Status.Subscribe(async _ => await Refresh()));
		_disposables.Add(Comm.Status.EncodingPercent.Subscribe(async _ => await Refresh()));
		_disposables.Add(LogMain.LastLine.Subscribe(async _ => await Refresh()));
		_disposables.Add(LogFfmpeg.LastLine.Subscribe(async _ => await Refresh()));
	}

	private async Task Refresh()
	{
		_encodingPercent = Comm.Status.EncodingPercent.Value == null ? 100 : Math.Round(Comm.Status.EncodingPercent.Value.Value);
		_encodingProgressMode = Comm.Status.EncodingPercent.Value == null ? Radzen.ProgressBarMode.Indeterminate : Radzen.ProgressBarMode.Determinate;
		_videoToProcessCount = await VideoDbContext.CountVideosWithStatusAsync(Models.CompressionStatus.ToProcess);
		_progressShowValue = Comm.Status.EncodingPercent.Value != null;
		await InvokeAsync(StateHasChanged);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_disposables.Dispose();
			}
			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}