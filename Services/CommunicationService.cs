namespace GrinVideoEncoder.Services;

public class CommunicationService
{
	public CancellationTokenSource VideoProcessToken { get; set; } = new();
	public VideoProcessingStatus Status { get; set; } = new();

	public Stack<Guid> VideoToProcess { get; set; } = new();
}
