using System.Reactive.Disposables;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Components.Pages;

public partial class Index : IDisposable
{
	private readonly CompositeDisposable _disposables = [];
	private EventConsole? _consoleFfmpeg;
	private EventConsole? _consoleMain;
	private bool _disposedValue;
	[Inject] private CommunicationService Comm { get; set; } = null!;
	[Inject] private LogFfmpeg LogFfmpeg { get; set; } = null!;
	[Inject] private LogMain LogMain { get; set; } = null!;
	[Inject] private IAppSettings Settings { get; set; } = null!;

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
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

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (firstRender)
		{
			await FillConsole(LogMain, _consoleMain);
			await FillConsole(LogFfmpeg, _consoleFfmpeg);

			_disposables.Add(LogMain.LastLine.Subscribe(async newLine => await _consoleMain!.LogAsync(newLine)));
			_disposables.Add(LogFfmpeg.LastLine.Subscribe(async newLine => await _consoleFfmpeg!.LogAsync(newLine)));
		}
	}

	private static void CloseApplication()
	{
		Environment.Exit(0);
	}

	private static async Task FillConsole(GrinLogBase v, EventConsole? console)
	{
		if (console == null) return;

		foreach (string line in v.History.ToList())
		{
			await console.LogAsync(line);
		}
	}

	private async Task CancelTask(Microsoft.AspNetCore.Components.Web.MouseEventArgs args)
	{
		await Comm.VideoProcessToken.CancelAsync();
	}
}