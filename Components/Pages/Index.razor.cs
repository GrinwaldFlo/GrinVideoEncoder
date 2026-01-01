using System.Reactive.Disposables;
using Microsoft.AspNetCore.Components;

namespace GrinVideoEncoder.Components.Pages;

public partial class Index : IDisposable
{
	private EventConsole? _consoleMain;
	private EventConsole? _consoleFfmpeg;
	private bool _disposedValue;
	[Inject] private IAppSettings Settings { get; set; } = null!;
	[Inject] private CommunicationService Comm { get; set; } = null!;

	[Inject] private LogMain LogMain { get; set; } = null!;
	[Inject] private LogFfmpeg LogFfmpeg { get; set; } = null!;

	private readonly CompositeDisposable _disposables = [];

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if(firstRender)
		{ 
			FillConsole(LogMain, _consoleMain);
			FillConsole(LogFfmpeg, _consoleFfmpeg);

			_disposables.Add(LogMain.LastLine.Subscribe(newLine => _consoleMain?.Log(newLine)));
			_disposables.Add(LogFfmpeg.LastLine.Subscribe(newLine => _consoleFfmpeg?.Log(newLine)));
		}
	}

	private static void FillConsole(GrinLogBase v, EventConsole? console)
	{
		if (console == null) return;

		foreach (string line in v.History)
		{
			console.Log(line);
		}
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

    private async Task CancelTask(Microsoft.AspNetCore.Components.Web.MouseEventArgs args)
    {
		await Comm.VideoProcessToken.CancelAsync();
	}
}