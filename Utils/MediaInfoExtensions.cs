using Xabe.FFmpeg;

namespace GrinVideoEncoder.Utils;

public static class MediaInfoExtensions
{
	/// <summary>
	/// Determines whether the video is healthy: has a positive duration,
	/// at least one video stream with width, height, and framerate all above 0.
	/// </summary>
	public static bool VideoIsSane(this IMediaInfo mediaInfo)
	{
		if (mediaInfo.Duration.TotalSeconds <= 0)
			return false;

		var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
		return videoStream is not null && videoStream.Width > 0
			&& videoStream.Height > 0
			&& videoStream.Framerate > 0;
	}
}
