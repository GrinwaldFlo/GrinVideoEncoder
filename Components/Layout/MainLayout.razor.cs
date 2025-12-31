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

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		_disposables.Add(Comm.Status.IsRunning.Subscribe(async _ => await Refresh()));
		_disposables.Add(Comm.Status.Status.Subscribe(async _ => await Refresh()));
		_disposables.Add(LogMain.LastLine.Subscribe(async _ => await Refresh()));
		_disposables.Add(LogFfmpeg.LastLine.Subscribe(async _ => await Refresh()));
	}

	private async Task Refresh()
	{
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