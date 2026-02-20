namespace GrinVideoEncoder.Services;

public class VideoValidator(CommunicationService comm) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		bool lastPreventSleep = comm.PreventSleep;
		while (!stoppingToken.IsCancellationRequested)
		{
			// Only call when state changes, not every iteration
			if (comm.PreventSleep && !lastPreventSleep)
			{
				PowerManagement.PreventSleep();
			}
			else if (!comm.PreventSleep && lastPreventSleep)
			{
				PowerManagement.AllowSleep();
			}

			lastPreventSleep = comm.PreventSleep;
			await Task.Delay(1000, stoppingToken);
		}
	}
}