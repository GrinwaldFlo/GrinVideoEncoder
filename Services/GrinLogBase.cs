using Serilog;

namespace GrinVideoEncoder.Services;

public abstract class GrinLogBase(string path, string name)
{
	public BehaviorSubject<string> LastLine { get; } = new(string.Empty);
	private readonly Serilog.ILogger _log = new LoggerConfiguration()
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
			.MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
			.WriteTo.File(Path.Combine(path, $"{name}.log"), rollingInterval: RollingInterval.Day)
			.WriteTo.Console()
			.CreateLogger();

	public List<string> History { get; } = [];

	private void AddLine(string line, params object?[]? args)
	{
		string str = args == null ? line : string.Format(line, args);

		History.Add(str);
		if (History.Count > 5000)
			History.RemoveAt(0);
		LastLine.OnNext(str);
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