using GrinVideoEncoder.Startup;
using GrinVideoEncoder.Utils;

namespace Tests;

public class VaapiRequirementsCheckTests
{
	private const string FFMPEG = "/usr/bin/ffmpeg";
	private const string FFPROBE = "/usr/bin/ffprobe";
	private const string ENCODERS_OK = " V....D hevc_vaapi           H.265/HEVC (VAAPI) (codec hevc)";
	private const string DEVICE = "/dev/dri/renderD128";
	private const string DEVICE2 = "/dev/dri/renderD129";

	private static VaapiRequirementsCheck CreateCheck(
		string? ffmpeg = FFMPEG,
		string? ffprobe = FFPROBE,
		CommandResult? encoders = null,
		string[]? devices = null,
		Func<string, CommandResult>? testEncode = null,
		Func<string, CommandResult>? testEncode10Bit = null)
	{
		encoders ??= new CommandResult(0, ENCODERS_OK);
		devices ??= [DEVICE];
		testEncode ??= _ => new CommandResult(0, string.Empty);
		testEncode10Bit ??= testEncode;

		return new VaapiRequirementsCheck(
			name => name == "ffmpeg" ? ffmpeg : ffprobe,
			() => devices,
			(_, arguments) => Task.FromResult(arguments.Contains("-encoders")
				? encoders
				: arguments.Contains("format=p010")
					? testEncode10Bit(devices.First(arguments.Contains))
					: testEncode(devices.First(arguments.Contains))));
	}

	[Fact]
	public async Task RunAsync_EverythingAvailable_ReturnsDeviceAndFfmpegFolder()
	{
		var result = await CreateCheck().RunAsync();

		Assert.True(result.IsMet);
		Assert.Equal(DEVICE, result.Device);
		Assert.Equal("/usr/bin", result.FfmpegDirectory);
		Assert.True(result.SupportsMain10);
	}

	[Fact]
	public async Task RunAsync_TenBitEncodeFails_StillMetWithoutMain10()
	{
		var result = await CreateCheck(testEncode10Bit: _ => new CommandResult(218, "[hevc_vaapi] No usable encoding profile found.")).RunAsync();

		Assert.True(result.IsMet);
		Assert.Equal(DEVICE, result.Device);
		Assert.False(result.SupportsMain10);
	}

	[Fact]
	public async Task RunAsync_TenBitTestedOnTheDeviceThatWorks()
	{
		var result = await CreateCheck(
			devices: [DEVICE, DEVICE2],
			testEncode: device => new CommandResult(device == DEVICE ? 1 : 0, string.Empty),
			testEncode10Bit: device => new CommandResult(device == DEVICE2 ? 0 : 1, string.Empty)).RunAsync();

		Assert.Equal(DEVICE2, result.Device);
		Assert.True(result.SupportsMain10);
	}

	[Theory]
	[InlineData(null, FFPROBE, "'ffmpeg'")]
	[InlineData(FFMPEG, null, "'ffprobe'")]
	public async Task RunAsync_ExecutableMissing_NamesItAndExplainsInstall(string? ffmpeg, string? ffprobe, string expectedDetail)
	{
		var result = await CreateCheck(ffmpeg, ffprobe).RunAsync();

		Assert.False(result.IsMet);
		Assert.Contains(expectedDetail, result.Problem!.Detail);
		Assert.Contains(result.Problem.Fix, line => line.Contains("dnf install ffmpeg"));
	}

	[Fact]
	public async Task RunAsync_FfprobeInOtherFolder_Fails()
	{
		var result = await CreateCheck(ffprobe: "/opt/other/ffprobe").RunAsync();

		Assert.False(result.IsMet);
		Assert.Contains("different folders", result.Problem!.Detail);
	}

	[Fact]
	public async Task RunAsync_FfmpegWithoutHevcVaapi_ExplainsFfmpegInstall()
	{
		var result = await CreateCheck(encoders: new CommandResult(0, " V....D libx265 ")).RunAsync();

		Assert.False(result.IsMet);
		Assert.Contains("hevc_vaapi", result.Problem!.Title);
		Assert.Contains(result.Problem.Fix, line => line.Contains("dnf install ffmpeg"));
	}

	[Fact]
	public async Task RunAsync_EncoderListCommandFails_Fails()
	{
		var result = await CreateCheck(encoders: new CommandResult(1, ENCODERS_OK)).RunAsync();

		Assert.False(result.IsMet);
	}

	[Fact]
	public async Task RunAsync_NoRenderDevice_ExplainsGpuCheck()
	{
		var result = await CreateCheck(devices: []).RunAsync();

		Assert.False(result.IsMet);
		Assert.Contains("render device", result.Problem!.Title);
	}

	[Fact]
	public async Task RunAsync_DriverWithoutHevc_ExplainsDriverInstallAndShowsError()
	{
		var result = await CreateCheck(testEncode: _ => new CommandResult(218, "[hevc_vaapi] No usable encoding profile found.\n")).RunAsync();

		Assert.False(result.IsMet);
		Assert.Contains("No usable encoding profile found", result.Problem!.Detail);
		Assert.Contains(result.Problem.Fix, line => line.Contains("dnf install mesa-va-drivers-freeworld"));
	}

	[Fact]
	public async Task RunAsync_PermissionDenied_ExplainsGroups()
	{
		var result = await CreateCheck(testEncode: _ => new CommandResult(1, "Failed to open /dev/dri/renderD128: Permission denied")).RunAsync();

		Assert.False(result.IsMet);
		Assert.Contains(result.Problem!.Fix, line => line.Contains("usermod"));
		Assert.DoesNotContain(result.Problem.Fix, line => line.Contains("freeworld"));
	}

	[Fact]
	public async Task RunAsync_FirstDeviceFailsSecondWorks_UsesSecond()
	{
		var result = await CreateCheck(
			devices: [DEVICE, DEVICE2],
			testEncode: device => new CommandResult(device == DEVICE ? 1 : 0, string.Empty)).RunAsync();

		Assert.True(result.IsMet);
		Assert.Equal(DEVICE2, result.Device);
	}

	[Fact]
	public async Task RunAsync_LongError_KeepsOnlyFirstLines()
	{
		var result = await CreateCheck(testEncode: _ => new CommandResult(1, "one\ntwo\nthree\nfour\nfive")).RunAsync();

		Assert.Contains("three", result.Problem!.Detail);
		Assert.DoesNotContain("four", result.Problem.Detail);
	}

	[Fact]
	public void BuildTestEncodeArguments_UsesDeviceAndHevcVaapi()
	{
		string arguments = VaapiRequirementsCheck.BuildTestEncodeArguments(DEVICE2);

		Assert.Contains($"-vaapi_device {DEVICE2}", arguments);
		Assert.Contains("-c:v hevc_vaapi", arguments);
		Assert.Contains("-frames:v 1", arguments);
	}

	[Theory]
	[InlineData(false, "format=nv12,hwupload")]
	[InlineData(true, "format=p010,hwupload")]
	public void BuildTestEncodeArguments_UploadsPixelFormatOfBitDepth(bool tenBit, string expected)
	{
		Assert.Contains(expected, VaapiRequirementsCheck.BuildTestEncodeArguments(DEVICE, tenBit));
	}
}
