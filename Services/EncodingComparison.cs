namespace GrinVideoEncoder.Services;

/// <summary>
/// Media values of an original video and of its encoding, which must match before the original is replaced.
/// A null value could not be read.
/// </summary>
/// <param name="ExpectedBitDepth">Bit depth the encoding must reach, or null if it is not checked, see <see cref="VideoEncoder.GetExpectedOutputBitDepth"/>.</param>
public readonly record struct EncodingComparison(
	double? OriginalDuration, double? EncodedDuration,
	long? OriginalPixels, long? EncodedPixels,
	double? OriginalFps, double? EncodedFps,
	int OriginalBitDepth, int EncodedBitDepth, int? ExpectedBitDepth)
{
	/// <summary>
	/// Largest accepted duration difference, in seconds.
	/// </summary>
	public const double DURATION_TOLERANCE = 0.5;

	/// <summary>
	/// Largest accepted FPS difference, and smallest FPS range of a variable frame rate video.
	/// </summary>
	public const double FPS_TOLERANCE = 2;

	/// <summary>
	/// True if the FPS changed: the original must then be scanned to know whether it has a variable frame rate.
	/// An unreadable FPS is reported by <see cref="FindProblem"/>, it must not trigger the slow full-file scan.
	/// </summary>
	public bool NeedsFpsScan => OriginalFps != null && EncodedFps != null && Math.Abs(OriginalFps.Value - EncodedFps.Value) > FPS_TOLERANCE;

	/// <summary>
	/// True if the FPS range scanned in a video shows a variable frame rate.
	/// </summary>
	public static bool IsVariableFrameRate((double min, double max) fpsRange) => fpsRange.max - fpsRange.min > FPS_TOLERANCE;

	/// <param name="originalFpsRange">FPS range of the original, scanned only if <see cref="NeedsFpsScan"/>. (0, 0) if the scan failed.</param>
	/// <returns>Why the encoding cannot replace the original, or null if it can.</returns>
	public string? FindProblem((double min, double max) originalFpsRange)
	{
		if (OriginalDuration == null || EncodedDuration == null || OriginalFps == null || EncodedFps == null || OriginalPixels == null || EncodedPixels == null)
			return "Failed to read media info";

		double durationDiff = Math.Abs(OriginalDuration.Value - EncodedDuration.Value);
		if (durationDiff > DURATION_TOLERANCE)
			return $"Duration difference is too big {durationDiff:F2} [s]";

		if (OriginalPixels != EncodedPixels)
			return $"Resolution has changed {OriginalPixels} / {EncodedPixels}";

		// A variable frame rate original can change its average FPS, a constant one must not
		if (NeedsFpsScan && originalFpsRange.max - originalFpsRange.min < FPS_TOLERANCE)
			return $"FPS has changed {OriginalFps.Value:F2} / {EncodedFps.Value:F2}";

		if (ExpectedBitDepth != null && EncodedBitDepth < ExpectedBitDepth)
			return $"Bit depth has been reduced {OriginalBitDepth} / {EncodedBitDepth}, expected {ExpectedBitDepth}";

		return null;
	}
}
