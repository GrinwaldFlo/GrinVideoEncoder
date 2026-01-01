using GrinVideoEncoder.Models;
using Microsoft.AspNetCore.Components;

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
		await LoadVideos();
	}

	private async Task LoadVideos()
	{
		_videos = await Context.GetVideosWithHighQualityRatioAsync(_threshold);
	}

	private async Task StartReencoding()
	{
		await LoadVideos();
		CommunicationService.VideoProcessToken = new CancellationTokenSource();
		
		if (_videos.Count > 0)
		{
			foreach (var item in _videos)
			{
				CommunicationService.VideoToProcess.Push(item.Id);
			}
		}
	}
}
