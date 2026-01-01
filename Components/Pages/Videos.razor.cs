using GrinVideoEncoder.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GrinVideoEncoder.Components.Pages;

public partial class Videos
{
	[Inject] private VideoIndexerDbContext Context { get; set; } = null!;

	IList<VideoFile> _videos = null!;

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		_videos = await Context.VideoFiles.ToListAsync();
	}
}