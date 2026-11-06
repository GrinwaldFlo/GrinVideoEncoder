using System.Text.RegularExpressions;
using GrinVideoEncoder.Models;
using Xabe.FFmpeg;

namespace GrinVideoEncoder.Utils;

public static partial class MediaInfoExtensions
{
	private static readonly int[] _componentDepths = [9, 10, 12, 14, 16];

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

	/// <summary>
	/// Bits per color component of the first video stream.
	/// </summary>
	/// <returns>The bit depth, or <see cref="VideoFile.BIT_DEPTH_UNKNOWN"/> if there is no video stream or pixel format.</returns>
	public static int GetBitDepth(this IMediaInfo mediaInfo) =>
		ParseBitDepth(mediaInfo.VideoStreams.FirstOrDefault()?.PixelFormat);

	/// <summary>
	/// Reads the bit depth from an FFmpeg pixel format name. High bit depth formats end with the depth and the byte order,
	/// such as yuv420p10le or p010le; formats without that suffix, such as yuv420p or nv12, are 8 bits.
	/// Packed RGB formats give the whole pixel size (rgb48le), and 48 or 64 bits mean 16 bits per component.
	/// </summary>
	internal static int ParseBitDepth(string? pixelFormat)
	{
		if (string.IsNullOrWhiteSpace(pixelFormat))
			return VideoFile.BIT_DEPTH_UNKNOWN;

		var match = HighBitDepthSuffixRegex().Match(pixelFormat.Trim().ToLowerInvariant());
		if (!match.Success)
			return 8;

		int bits = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
		if (bits is 48 or 64)
			return 16;

		return _componentDepths.Contains(bits) ? bits : 8;
	}

	[GeneratedRegex(@"(\d{1,2})(?:le|be)$")]
	private static partial Regex HighBitDepthSuffixRegex();
}
