namespace GrinVideoEncoder.Data;

public class VideoProcessingStatus
{
	public BehaviorSubject<string> Filename { get; } = new BehaviorSubject<string>("...");
	public BehaviorSubject<bool> IsRunning { get; } = new BehaviorSubject<bool>(false);
	public BehaviorSubject<string> Status { get; } = new BehaviorSubject<string>("...");
}