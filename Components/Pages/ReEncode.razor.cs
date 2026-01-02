using GrinVideoEncoder.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Components.Pages;

public partial class ReEncode
{
	[Inject] private VideoIndexerDbContext Context { get; set; } = null!;
	[Inject] private CommunicationService CommunicationService { get; set; } = null!;

	private double _threshold = 900.0;
	private List<VideoFile> _videos = null!;

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();
		await RefreshDb();
	}


	private async Task StartReencoding()
	{
		await RefreshDb();
		CommunicationService.VideoProcessToken = new CancellationTokenSource();
		
		if (_videos.Count > 0)
		{
			foreach (var item in _videos)
			{
				CommunicationService.VideoToProcess.Push(item.Id);
			}
		}
		await InvokeAsync(StateHasChanged);
	}

	private async Task RefreshDb()
	{
		Context.ChangeTracker.Clear();
		_videos = await Context.GetVideosWithHighQualityRatioAsync(_threshold);
		await InvokeAsync(StateHasChanged);
	}
}
