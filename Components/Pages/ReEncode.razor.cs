using System.Reactive.Disposables;
using GrinVideoEncoder.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Components.Pages;

public partial class ReEncode : IDisposable
{
	private List<VideoFile> _videos = null!;
	private bool _disposedValue;

	public static double Threshold { get; set; } = 900.0;
	[Inject] private CommunicationService Comm { get; set; } = null!;

	private readonly CompositeDisposable _disposables = [];

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();
		_disposables.Add(Comm.Status.IsRunning.Subscribe(async _ => await InvokeAsync(StateHasChanged)));
		await RefreshDb();
	}

	private async Task RefreshDb()
	{
		using var Context = new VideoDbContext();
		_videos = await Context.GetVideosWithHighQualityRatioAsync(Threshold);
		await InvokeAsync(StateHasChanged);
	}

	private async Task StartReencoding()
	{
		await RefreshDb();
		Comm.VideoProcessToken = new CancellationTokenSource();

		int nb = await VideoDbContext.SetStatusAsync(_videos.Select(x => x.Id), CompressionStatus.ToProcess);
		Comm.AskTreatFiles = nb > 0;
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