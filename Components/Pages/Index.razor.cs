using System.Reactive.Disposables;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

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
	[Inject] private VideoIndexerDbContext Context { get; set; } = null!;

	private readonly CompositeDisposable _disposables = [];

	private int _nbVideos = 0;
	private long _sumByteOriginal = 0;
	private long _sumByteCompressed = 0;

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if(firstRender)
		{ 
			await FillConsole(LogMain, _consoleMain);
			await FillConsole(LogFfmpeg, _consoleFfmpeg);

			_disposables.Add(LogMain.LastLine.Subscribe(async newLine => await _consoleMain!.LogAsync(newLine)));
			_disposables.Add(LogFfmpeg.LastLine.Subscribe(async newLine => await _consoleFfmpeg!.LogAsync(newLine)));

			await RefreshDb();
		}
	}

	private async Task RefreshDb()
	{
		Context.ChangeTracker.Clear();

		_nbVideos = await Context.VideoFiles.CountAsync();
		_sumByteOriginal = await Context.VideoFiles.SumAsync(x => x.FileSizeOriginal);
		_sumByteCompressed = await Context.VideoFiles.SumAsync(x => x.FileSizeCompressed ??  x.FileSizeOriginal);

		await InvokeAsync(StateHasChanged);
	}

	private static async Task FillConsole(GrinLogBase v, EventConsole? console)
	{
		if (console == null) return;

		foreach (string line in v.History)
		{
			await console.LogAsync(line);
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
    private static void CloseApplication()
    {
        Environment.Exit(0);
    }
}