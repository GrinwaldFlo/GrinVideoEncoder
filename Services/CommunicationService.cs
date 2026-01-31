namespace GrinVideoEncoder.Services;

public class CommunicationService
{
	public CancellationTokenSource VideoProcessToken { get; set; } = new();
	public VideoProcessingStatus Status { get; set; } = new();

	/// <summary>
	/// Managed by background task, prevent computer from sleeping
	/// </summary>
	public bool PreventSleep {  get; set; } = false;

	public bool AskTreatFiles { get; set; } = false;
}
