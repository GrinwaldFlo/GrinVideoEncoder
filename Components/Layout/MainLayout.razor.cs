using Microsoft.AspNetCore.Components;

namespace GrinVideoEncoder.Components.Layout;

public partial class MainLayout : IDisposable
{
	[Inject] private CommunicationService Comm { get; set; } = null!;

	private bool _disposedValue;
	private Func<object, EventArgs, Task>? _statusChangedHandler;

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		_statusChangedHandler = async (sender, args) => await InvokeAsync(StateHasChanged);
		Comm.Status.StatusChangedAsync += _statusChangedHandler;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
#pragma warning disable S1066 // Mergeable "if" statements should be combined
				if (_statusChangedHandler != null)
				{
					Comm.Status.StatusChangedAsync -= _statusChangedHandler;
				}
#pragma warning restore S1066 // Mergeable "if" statements should be combined
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