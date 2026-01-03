
namespace GrinVideoEncoder.Services;

public class PreventSleep(CommunicationService comm) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		bool lastPreventSleep = comm.PreventSleep;
		while (!stoppingToken.IsCancellationRequested)
		{
			if(comm.PreventSleep)
			{
				PowerManagement.PreventSleep();
			}
			if (!comm.PreventSleep && lastPreventSleep)
			{
				PowerManagement.AllowSleep();
			}
			lastPreventSleep = comm.PreventSleep;
			await Task.Delay(30000, stoppingToken);
		}
	}
}
