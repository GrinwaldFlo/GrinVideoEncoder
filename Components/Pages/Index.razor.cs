using System.Reactive.Disposables;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace GrinVideoEncoder.Components.Pages;

public partial class Index : IDisposable
{
	private readonly CompositeDisposable _disposables = [];
	private EventConsole? _consoleFfmpeg;
	private EventConsole? _consoleMain;
	private bool _disposedValue;
	private DateTime? _shutdownTime = DateTime.Today.AddHours(23);
	[Inject] private CommunicationService Comm { get; set; } = null!;
	[Inject] private LogFfmpeg LogFfmpeg { get; set; } = null!;
	[Inject] private LogMain LogMain { get; set; } = null!;
	[Inject] private IAppSettings Settings { get; set; } = null!;
	[Inject] private NotificationService Notification { get; set; } = null!;

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

			_disposables.Add(Comm.Status.IsRunning.Subscribe(async _ => await InvokeAsync(StateHasChanged)));
			_disposables.Add(LogMain.LastLine.Subscribe(async newLine => await _consoleMain!.LogAsync(newLine)));
			_disposables.Add(LogFfmpeg.LastLine.Subscribe(async newLine => await _consoleFfmpeg!.LogAsync(newLine)));
		}
	}

	private void CloseApplication()
	{
		Comm.AskClose = true;
		Notification.Notify(NotificationSeverity.Info, "Application will close when current file is done");
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
		await InvokeAsync(StateHasChanged);
	}

	private async Task OnShutdownToggled(bool enabled)
	{
		Comm.ScheduledShutdownEnabled = enabled;
	}

	private void OnShutdownTimeChanged(DateTime? value)
	{
		if (value.HasValue)
		{
			_shutdownTime = value;
			Comm.ScheduledShutdownTime = value.Value;			
		}
	}
}