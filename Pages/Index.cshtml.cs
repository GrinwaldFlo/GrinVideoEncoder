using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GrinVideoEncoder.Pages;

public class IndexModel(IAppSettings settings) : PageModel
{
	private const int MAX_LINES = 1000;
	private readonly string _logDirectory = settings.LogPath;

	public string LogContent { get; set; } = string.Empty;

	public void OnGet()
	{
		string? logFile = GetLatestLogFile();
		if (logFile != null)
		{
			LogContent = ReadLastLines(logFile, MAX_LINES);
		}
	}

	public IActionResult OnGetLogs()
	{
		string? logFile = GetLatestLogFile();
		return Content(logFile != null ? ReadLastLines(logFile, MAX_LINES) : "No logs available");
	}

	private static string ReadLastLines(string filePath, int maxLines)
	{
		try
		{
			using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using StreamReader reader = new(fs);

			List<string> lines = new(maxLines);
			string? line;

			while ((line = reader.ReadLine()) != null)
			{
				lines.Add(line);
				if (lines.Count > maxLines)
					lines.RemoveAt(0);
			}

			// Reverse for newest-first display
			lines.Reverse();
			return string.Join("\n", lines);
		}
		catch
		{
			return "Unable to read log file";
		}
	}

	private string? GetLatestLogFile()
	{
		try
		{
			if (!Directory.Exists(_logDirectory))
				return null;

			return new DirectoryInfo(_logDirectory)
				.GetFiles("*.log")
				.OrderByDescending(f => f.LastWriteTime)
				.FirstOrDefault()?.FullName;
		}
		catch
		{
			return null;
		}
	}
}