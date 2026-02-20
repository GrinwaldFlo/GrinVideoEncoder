using Microsoft.AspNetCore.Components;
using Radzen;

namespace GrinVideoEncoder.Components.Pages;

public partial class SettingsPage
{
	[Inject] private AppSettings Settings { get; set; } = null!;

	private string _statusMessage = string.Empty;
	private BadgeStyle _statusStyle = BadgeStyle.Success;

	private void OnExtensionsChanged(string value)
	{
		Settings.VideoExtensions = value
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList();
	}

	private void OnIgnoreFoldersChanged(string value)
	{
		Settings.IgnoreFolders = value
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList();
	}

	private void Save()
	{
		try
		{
			ConfigManager.SaveConfig(Settings);
			_statusMessage = "Settings saved";
			_statusStyle = BadgeStyle.Success;
		}
		catch (Exception ex)
		{
			_statusMessage = $"Error: {ex.Message}";
			_statusStyle = BadgeStyle.Danger;
		}
	}
}
