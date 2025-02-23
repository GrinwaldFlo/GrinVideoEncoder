namespace GrinVideoEncoder.Data;

public class VideoProcessingStatus
{
	public string Filename { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public bool IsRunning { get; set; }
}
