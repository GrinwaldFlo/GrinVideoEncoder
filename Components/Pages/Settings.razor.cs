using Microsoft.AspNetCore.Components;
using Radzen;

namespace GrinVideoEncoder.Components.Pages;

public partial class Settings
{
	[Inject] private AppSettings AppSettings { get; set; } = null!;

	private string _statusMessage = string.Empty;
	private BadgeStyle _statusStyle = BadgeStyle.Success;

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
