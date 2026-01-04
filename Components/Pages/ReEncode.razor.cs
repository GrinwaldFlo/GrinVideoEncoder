using System.Reactive.Disposables;
using GrinVideoEncoder.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Radzen;
using Radzen.Blazor;

namespace GrinVideoEncoder.Components.Pages;

public partial class ReEncode : IDisposable
{
	private readonly CompositeDisposable _disposables = [];
	private bool _disposedValue;
	private RadzenDataGrid<VideoFile>? _grid;
	private List<VideoFile> _selectedVideos = null!;
	private List<VideoFile> _videos = null!;
	public static double Threshold { get; set; } = 900.0;
	private int _selectedVideoCount = -1;
	[Inject] private CommunicationService Comm { get; set; } = null!;

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

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();
		_disposables.Add(Comm.Status.IsRunning.Subscribe(async _ => await InvokeAsync(StateHasChanged)));
		await RefreshDb();
	}

	private async Task OnGridViewChanged(DataGridColumnFilterEventArgs<VideoFile> args)
	{
		await UpdateCount();
		await InvokeAsync(StateHasChanged);
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);
		if(firstRender)
		{
			await UpdateCount();
			await InvokeAsync(StateHasChanged);
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6966:Awaitable method should be used", Justification = "<Pending>")]
	private async Task UpdateCount()
	{
		if (_grid?.View == null)
			return;
		await Task.Delay(100);
		_selectedVideoCount = _grid.View.Count();
	}

	private async Task RefreshDb()
	{
		using var Context = new VideoDbContext();
		_videos = await Context.GetVideosWithHighQualityRatioAsync(Threshold);
		await UpdateCount();
		await InvokeAsync(StateHasChanged);
	}

	private async Task StartReencoding()
	{
		if (_grid?.View == null)
			return;

		_selectedVideos = [.. _grid.View];
		Comm.VideoProcessToken = new CancellationTokenSource();

		int nb = await VideoDbContext.SetStatusAsync(_selectedVideos.Select(x => x.Id), CompressionStatus.ToProcess);
		Comm.AskTreatFiles = nb > 0;
		await RefreshDb();
		await InvokeAsync(StateHasChanged);
	}
}