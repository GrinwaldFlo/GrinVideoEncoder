

namespace GrinVideoEncoder.Components.Pages;

public partial class Index : IDisposable
{
	private EventConsole _consoleMain;
	private EventConsole _consoleFfmpeg;
	private bool _disposedValue;

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				// TODO: dispose managed state (managed objects)
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