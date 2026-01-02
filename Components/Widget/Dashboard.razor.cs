using System.Reactive.Disposables;
using GrinVideoEncoder.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Components.Widget;

public partial class Dashboard : IDisposable
{
	private readonly CompositeDisposable _disposables = [];
	private bool _disposedValue;
	private int _nbVideos = 0;
	private long _sumByteCompressed = 0;
	private long _sumByteOriginal = 0;
	private long _sumByteToReEncode = 0;
	[Inject] private CommunicationService Comm { get; set; } = null!;
	private double CompressionRate => (_sumByteOriginal / (double)_sumByteCompressed - 1.0) * 100.0;

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
			_disposables.Add(Comm.Status.Filename.Subscribe(async _ => await RefreshDb()));
			await RefreshDb();
		}
	}

	private async Task RefreshDb()
	{
		await using var context = new VideoDbContext();
		context.ChangeTracker.Clear();

		_nbVideos = await context.VideoFiles.CountAsync();
		_sumByteOriginal = await context.VideoFiles.SumAsync(x => x.FileSizeOriginal);
		_sumByteCompressed = await context.VideoFiles.SumAsync(x => x.FileSizeCompressed ?? x.FileSizeOriginal);
		var toEncode = await context.GetVideosWithHighQualityRatioAsync(ReEncode.Threshold);
		_sumByteToReEncode = toEncode.Sum(x => x.FileSizeOriginal);

		

		await InvokeAsync(StateHasChanged);
	}
}