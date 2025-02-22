using Microsoft.AspNetCore.Mvc.RazorPages;

public class IndexModel() : PageModel
{
	public string LogContent { get; set; } = string.Empty;

	public void OnGet()
	{
		string logPath = Path.Combine(Directory.GetCurrentDirectory(), "logs/log.txt");
		if (System.IO.File.Exists(logPath))
		{
			LogContent = System.IO.File.ReadAllText(logPath);
		}
	}
}