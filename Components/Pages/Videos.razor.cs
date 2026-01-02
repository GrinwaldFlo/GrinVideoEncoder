using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using GrinVideoEncoder.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;

namespace GrinVideoEncoder.Components.Pages;

public partial class Videos : IDisposable
{
	enum CMenuAction
	{
		CopyDir,
		OpenDir,
		ResetError
	}


	[Inject] private VideoIndexerDbContext Context { get; set; } = null!;
	[Inject] ContextMenuService ContextMenu { get; set; } = null!;
	[Inject] private IJSRuntime JS { get; set; } = null!;
	[Inject] private CommunicationService Comm { get; set; } = null!;
	[Inject] private NotificationService Notification { get; set; } = null!;

	IList<VideoFile> _videos = [];
	private bool _disposedValue;
	private readonly CompositeDisposable _disposables = [];

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();
		await RefreshDb();
		_disposables.Add(Comm.Status.Filename.Subscribe(async _ => await RefreshDb()));
	}

	private async Task RefreshDb()
	{
		Context.ChangeTracker.Clear();
		_videos = await Context.VideoFiles.AsNoTracking().ToListAsync();
		await InvokeAsync(StateHasChanged);
	}

	private void OnContextMenu(DataGridCellMouseEventArgs<VideoFile> args)
	{
		var curItem = args.Data;

		ContextMenu.Open(args,
		   [
				new ContextMenuItem(){ Text = "Copy dir", Value = CMenuAction.CopyDir, Icon = "edit" },
				new ContextMenuItem(){ Text = "Open dir", Value = CMenuAction.OpenDir, Icon = "delete" },
				new ContextMenuItem(){ Text = "Reset error", Value = CMenuAction.ResetError, Icon = "content_copy" , Disabled = curItem.Status != CompressionStatus.FailedToCompress},
		   ],
		   async (e) => await ContextMenuAction(curItem, (CMenuAction)e.Value));
	}

	private async Task ContextMenuAction(VideoFile video, CMenuAction action)
	{
		switch (action)
		{
			case CMenuAction.CopyDir:
				try
				{
					await JS.InvokeVoidAsync("navigator.clipboard.writeText", video.DirectoryPath);
					Notification.Notify(NotificationSeverity.Success, $"{video.DirectoryPath} copied to clipboard");
				}
				catch (Exception ex)
				{
					Notification.Notify(NotificationSeverity.Error,  ex.Message);
				}
				break;
			case CMenuAction.OpenDir:
				try
				{
					if (Directory.Exists(video.DirectoryPath))
					{
						Process.Start(new ProcessStartInfo
						{
							FileName = video.DirectoryPath,
							UseShellExecute = true,
							Verb = "open"
						});
					}
				}
				catch (Exception ex)
				{
					Notification.Notify(NotificationSeverity.Error, ex.Message);
				}
				break;
			case CMenuAction.ResetError:
				var videoToReset = await Context.VideoFiles.FirstOrDefaultAsync(x => x.Id == video.Id);
				if (videoToReset != null)
				{
					videoToReset.Status = CompressionStatus.Original;
					await Context.SaveChangesAsync();
					await InvokeAsync(StateHasChanged);
				}
				break;
			default:
				break;
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
}