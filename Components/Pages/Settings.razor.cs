using System.Diagnostics;
using GrinVideoEncoder.Models;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace GrinVideoEncoder.Components.Pages;

public partial class Settings
{
	[Inject] private AppSettings AppSettings { get; set; } = null!;
	[Inject] private MaintenanceService Maintenance { get; set; } = null!;

	[Inject] private CommunicationService Communication { get; set; } = null!;

	private string _statusMessage = string.Empty;
	private BadgeStyle _statusStyle = BadgeStyle.Success;
	private List<FolderMaintenanceInfo> _folderInfos = [];
	private string _originalIndexerPath = string.Empty;
	private List<string> _originalExtensions = [];
	private List<string> _originalIgnoreFolders = [];

	protected override void OnInitialized()
	{
		_originalIndexerPath = AppSettings.IndexerPath;
		_originalExtensions = [.. AppSettings.VideoExtensions];
		_originalIgnoreFolders = [.. AppSettings.IgnoreFolders];
		RefreshFolders();
	}

	private void OnExtensionsChanged(string value)
	{
		AppSettings.VideoExtensions = value
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList();
	}

	private void OnIgnoreFoldersChanged(string value)
	{
		AppSettings.IgnoreFolders = value
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList();
	}

	private void Save()
	{
		try
		{
			ConfigManager.SaveConfig(AppSettings);

			bool indexerPathChanged = !string.Equals(_originalIndexerPath, AppSettings.IndexerPath, StringComparison.Ordinal);
			bool extensionsChanged = !_originalExtensions.SequenceEqual(AppSettings.VideoExtensions);
			bool ignoreFoldersChanged = !_originalIgnoreFolders.SequenceEqual(AppSettings.IgnoreFolders);

			if (indexerPathChanged || extensionsChanged || ignoreFoldersChanged)
			{
				Communication.AskReIndex = true;
				_originalIndexerPath = AppSettings.IndexerPath;
				_originalExtensions = [.. AppSettings.VideoExtensions];
				_originalIgnoreFolders = [.. AppSettings.IgnoreFolders];
				_statusMessage = "Settings saved — indexing will restart";
				_statusStyle = BadgeStyle.Info;
				return;
			}

			_statusMessage = "Settings saved";
			_statusStyle = BadgeStyle.Success;
		}
		catch (Exception ex)
		{
			_statusMessage = $"Error: {ex.Message}";
			_statusStyle = BadgeStyle.Danger;
		}
	}

	private void RefreshFolders()
	{
		_folderInfos = Maintenance.GetFolderInfos();
	}

	private void OpenFolder(string path)
	{
		try
		{
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			Process.Start(new ProcessStartInfo
			{
				FileName = path,
				UseShellExecute = true,
				Verb = "open"
			});
		}
		catch (Exception ex)
		{
			_statusMessage = $"Error: {ex.Message}";
			_statusStyle = BadgeStyle.Danger;
		}
	}

	private void ClearFolder(FolderMaintenanceInfo folder)
	{
		try
		{
			MaintenanceService.ClearFolder(folder.Path);
			_statusMessage = $"{folder.Name} cleared";
			_statusStyle = BadgeStyle.Success;
			RefreshFolders();
		}
		catch (Exception ex)
		{
			_statusMessage = $"Error clearing {folder.Name}: {ex.Message}";
			_statusStyle = BadgeStyle.Danger;
		}
	}
}
