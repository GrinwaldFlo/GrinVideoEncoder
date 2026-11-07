using Serilog;

namespace GrinVideoEncoder.Services;

public abstract class GrinLogBase(string path, string name)
{
	public BehaviorSubject<string> LastLine { get; } = new(string.Empty);
	private readonly Serilog.Core.Logger _log = new LoggerConfiguration()
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
			.MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
			.WriteTo.File(Path.Combine(path, $"{name}.log"), rollingInterval: RollingInterval.Day)
			.WriteTo.Console()
			.CreateLogger();

	/// <summary>
	/// Number of lines kept by <see cref="GetHistory"/>.
	/// </summary>
	public const int HISTORY_SIZE = 5000;

	// Written by the background services and FFmpeg at the same time, read by the UI
	private readonly Lock _historyLock = new();
	private readonly Queue<string> _history = new();

	/// <returns>A copy of the last <see cref="HISTORY_SIZE"/> lines, oldest first.</returns>
	public List<string> GetHistory()
	{
		lock (_historyLock)
			return [.. _history];
	}

	/// <summary>
	/// Replaces the Serilog placeholders of <paramref name="template"/> by <paramref name="args"/>, in order, keeping their format such as {Size:F2}.
	/// Never throws: a template that does not match its arguments is returned with the arguments appended, as Serilog also accepts it.
	/// </summary>
	internal static string FormatLine(string template, object?[]? args)
	{
		if (args is null || args.Length == 0)
			return template;

		string formatted = template;
		for (int i = 0; i < args.Length; i++)
		{
			string? placeholder = GetSerilogPlaceholder(template, i);
			if (placeholder is null)
				break;
			int formatStart = placeholder.IndexOf(':');
			string format = formatStart < 0 ? string.Empty : placeholder[formatStart..];
			formatted = formatted.Replace("{" + placeholder + "}", "{" + i + format + "}");
		}

		try
		{
			return string.Format(formatted, args);
		}
		catch (FormatException)
		{
			return $"{template} [{string.Join(", ", args)}]";
		}
	}

	/// <returns>The text between the braces of the placeholder at <paramref name="index"/>, or null if there is none.</returns>
	private static string? GetSerilogPlaceholder(string template, int index)
	{
		int count = -1;
		int start = 0;
		while (true)
		{
			int open = template.IndexOf('{', start);
			if (open == -1)
				return null;
			int close = template.IndexOf('}', open);
			if (close == -1)
				return null;
			count++;
			if (count == index)
				return template.Substring(open + 1, close - open - 1);
			start = close + 1;
		}
	}

	private void AddLine(string line, params object?[]? args)
	{
		string text = $"{DateTime.Now} - {FormatLine(line, args)}";
		lock (_historyLock)
		{
			_history.Enqueue(text);
			if (_history.Count > HISTORY_SIZE)
				_history.Dequeue();
		}
		LastLine.OnNext(text);
	}

	public void Information(string messageTemplate, params object?[]? args)
	{
		_log.Information(messageTemplate, args);
		AddLine(messageTemplate, args);
	}

	public void Debug(string messageTemplate, params object[] args)
	{
		_log.Debug(messageTemplate, args);
		AddLine(messageTemplate, args);
	}

	public void Warning(string messageTemplate, params object[] args)
	{
		_log.Warning(messageTemplate, args);
		AddLine(messageTemplate, args);
	}

	public void Error(string messageTemplate, params object[] args)
	{
		_log.Error(messageTemplate, args);
		AddLine(messageTemplate, args);
	}

	public void Error(Exception ex, string messageTemplate, params object[] args)
	{
		_log.Error(ex, messageTemplate, args);
		AddLine(ex.Message + " " + messageTemplate, args);
	}

	public void Fatal(string messageTemplate, params object[] args)
	{
		_log.Fatal(messageTemplate, args);
		AddLine(messageTemplate, args);
	}

	public void Fatal(Exception ex, string messageTemplate, params object[] args)
	{
		_log.Fatal(ex, messageTemplate, args);
		AddLine(ex.Message + " " + messageTemplate, args);
	}
}