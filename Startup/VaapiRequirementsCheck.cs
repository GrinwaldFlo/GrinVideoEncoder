namespace GrinVideoEncoder.Startup;

public sealed record RequirementProblem(string Title, string Detail, IReadOnlyList<string> Fix);

/// <summary>
/// Outcome of the check: either a problem to report, or the ffmpeg folder and VA-API device to use.
/// </summary>
public sealed record VaapiCheckResult(string? FfmpegDirectory, string? Device, RequirementProblem? Problem)
{
	public bool IsMet => Problem == null;
}

/// <summary>
/// Verifies that this Linux machine can encode with hevc_vaapi. The last step really encodes one frame,
/// because only that proves the ffmpeg build, the driver and the GPU work together.
/// </summary>
/// <param name="findExecutable">Returns the full path of an executable, or null if it is not installed.</param>
/// <param name="findRenderDevices">Returns the GPU render nodes, such as /dev/dri/renderD128.</param>
/// <param name="runCommand">Runs an executable with arguments and returns its exit code and combined output.</param>
public class VaapiRequirementsCheck(
	Func<string, string?> findExecutable,
	Func<IEnumerable<string>> findRenderDevices,
	Func<string, string, Task<CommandResult>> runCommand)
{
	private const int MAX_DETAIL_LINES = 3;

	private static readonly string[] _enableRpmFusion =
	[
		"Fedora: enable RPM Fusion if it is not enabled yet:",
		"  sudo dnf install https://mirrors.rpmfusion.org/free/fedora/rpmfusion-free-release-$(rpm -E %fedora).noarch.rpm",
	];

	private static readonly string[] _installFfmpeg =
	[
		.. _enableRpmFusion,
		"Fedora: install the full ffmpeg build:",
		"  sudo dnf install ffmpeg --allowerasing",
		"Other distributions: install an ffmpeg build with VA-API support, and make sure ffmpeg and ffprobe are in the same PATH folder.",
	];

	private static readonly string[] _installDriver =
	[
		.. _enableRpmFusion,
		"Fedora with an AMD GPU: install the driver with H.264/HEVC support:",
		"  sudo dnf install mesa-va-drivers-freeworld",
		"Fedora with an Intel GPU:",
		"  sudo dnf install libva-intel-media-driver",
		"Other distributions: install a VA-API driver with HEVC encoding support.",
		"NVIDIA GPUs cannot encode through VA-API and are not supported on Linux.",
		"To verify, look for a line with VAProfileHEVCMain and VAEntrypointEncSlice (vainfo is in libva-utils):",
		"  vainfo | grep HEVC",
	];

	private static readonly string[] _grantDeviceAccess =
	[
		"Add your user to the GPU groups, then log out and back in:",
		"  sudo usermod -aG render,video \"$USER\"",
	];

	private static readonly string[] _loadGpuDriver =
	[
		"Check that the GPU is detected and that its kernel driver is loaded:",
		"  lspci -k | grep -EA3 'VGA|3D'",
		"  ls -l /dev/dri",
	];

	internal static string BuildTestEncodeArguments(string device) =>
		$"-hide_banner -nostdin -loglevel error -vaapi_device {device} -f lavfi -i testsrc2=size=640x360:rate=24 "
		+ "-frames:v 1 -vf format=nv12,hwupload -c:v hevc_vaapi -f null -";

	public async Task<VaapiCheckResult> RunAsync()
	{
		string? ffmpeg = findExecutable("ffmpeg");
		string? ffprobe = findExecutable("ffprobe");
		if (ffmpeg == null || ffprobe == null)
		{
			string missing = ffmpeg == null ? "ffmpeg" : "ffprobe";
			return Failed("FFmpeg is not installed", $"'{missing}' was not found in PATH.", _installFfmpeg);
		}

		string? ffmpegDirectory = Path.GetDirectoryName(ffmpeg);
		if (ffmpegDirectory != Path.GetDirectoryName(ffprobe))
		{
			return Failed("FFmpeg is not installed correctly", $"ffmpeg ({ffmpeg}) and ffprobe ({ffprobe}) are in different folders.", _installFfmpeg);
		}

		var encoders = await runCommand(ffmpeg, "-hide_banner -encoders");
		if (encoders.ExitCode != 0 || !encoders.Output.Contains("hevc_vaapi", StringComparison.Ordinal))
		{
			return Failed("FFmpeg has no hevc_vaapi encoder", $"{ffmpeg} was built without VA-API HEVC encoding.", _installFfmpeg);
		}

		string[] devices = [.. findRenderDevices()];
		if (devices.Length == 0)
		{
			return Failed("No GPU render device found", "/dev/dri contains no renderD* device.", _loadGpuDriver);
		}

		bool accessDenied = false;
		string errorOutput = string.Empty;
		foreach (string device in devices)
		{
			var testEncode = await runCommand(ffmpeg, BuildTestEncodeArguments(device));
			if (testEncode.ExitCode == 0)
			{
				return new VaapiCheckResult(ffmpegDirectory, device, null);
			}

			accessDenied |= testEncode.Output.Contains("Permission denied", StringComparison.OrdinalIgnoreCase);
			if (errorOutput.Length == 0)
			{
				errorOutput = testEncode.Output;
			}
		}

		string detail = $"Test encode failed on {string.Join(", ", devices)}:{Environment.NewLine}{FirstLines(errorOutput)}";
		return accessDenied
			? Failed("No permission to use the GPU", detail, _grantDeviceAccess)
			: Failed("The GPU driver cannot encode HEVC", detail, _installDriver);
	}

	private static VaapiCheckResult Failed(string title, string detail, string[] fix) =>
		new(null, null, new RequirementProblem(title, detail, fix));

	private static string FirstLines(string text) =>
		string.Join(Environment.NewLine, text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(MAX_DETAIL_LINES));
}
