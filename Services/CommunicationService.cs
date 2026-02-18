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

	/// <summary>
	/// When enabled, the computer will shut down at the scheduled time
	/// after the current video finishes encoding.
	/// </summary>
	public bool ScheduledShutdownEnabled { get; set; }

	/// <summary>
	/// The time at which the computer should shut down.
	/// </summary>
	public DateTime ScheduledShutdownTime { get; set; } = DateTime.MaxValue;

	public bool AskClose { get; set; } = false;

}
