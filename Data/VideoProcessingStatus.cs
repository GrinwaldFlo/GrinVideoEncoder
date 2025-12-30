namespace GrinVideoEncoder.Data;

public class VideoProcessingStatus
{
	private string _filename = string.Empty;
	private bool _isRunning;
	private string _status = string.Empty;
    private string _logFfmpeg = string.Empty;
    private string _logMain = string.Empty;

	public event Func<object, EventArgs, Task>? StatusChangedAsync;

	public string Filename => _filename;
	public bool IsRunning => _isRunning;
	public string Status => _status;
    public string LogFfmpeg => _logFfmpeg;
    public string LogMain => _logMain;

	public async Task SetFilenameAsync(string value)
	{
		if (_filename != value)
		{
			_filename = value;
			await OnStatusChangedAsync();
		}
	}

	public async Task SetIsRunningAsync(bool value)
	{
		if (_isRunning != value)
		{
			_isRunning = value;
			await OnStatusChangedAsync();
		}
	}

	public async Task SetStatusAsync(string value)
	{
		if (_status != value)
		{
			_status = value;
			await OnStatusChangedAsync();
		}
	}

    public async Task SetLogFfmpegAsync(string value)
    {
        if (_logFfmpeg != value)
        {
            _logFfmpeg = value;
            await OnStatusChangedAsync();
        }
    }

    public async Task SetLogMainAsync(string value)
    {
        if (_logMain != value)
        {
            _logMain = value;
            await OnStatusChangedAsync();
        }
    }

	protected virtual async Task OnStatusChangedAsync()
	{
		if (StatusChangedAsync != null)
		{
			var invocationList = StatusChangedAsync.GetInvocationList();
			var tasks = invocationList
				.Select(d => ((Func<object, EventArgs, Task>)d)(this, EventArgs.Empty));
			await Task.WhenAll(tasks);
		}
	}
}
