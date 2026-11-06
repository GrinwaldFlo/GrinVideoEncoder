using GrinVideoEncoder.Utils;

namespace Tests;

/// <summary>
/// These tests run real commands through /bin/sh, so they only run on Linux, like the CI.
/// </summary>
public class ProcessRunnerTests
{
	private const string SHELL = "/bin/sh";

	[Fact]
	public async Task RunAsync_ArgumentList_KeepsQuotesAndSpaces()
	{
		if (!OperatingSystem.IsLinux())
			return;

		const string path = "/videos/my \"cut\" video.mkv";
		var result = await ProcessRunner.RunAsync(SHELL, ["-c", "printf %s \"$1\"", "sh", path]);

		Assert.Equal(0, result.ExitCode);
		Assert.Equal(path, result.StandardOutput);
	}

	[Fact]
	public async Task RunAsync_LargeStandardError_DoesNotBlock()
	{
		if (!OperatingSystem.IsLinux())
			return;

		// More than the 64 KB pipe buffer on stderr before anything is written to stdout
		var result = await ProcessRunner.RunAsync(SHELL, ["-c", "head -c 300000 /dev/zero | tr '\\0' x >&2; printf done"], TimeSpan.FromSeconds(20));

		Assert.Equal(0, result.ExitCode);
		Assert.Equal("done", result.StandardOutput);
		Assert.Equal(300000, result.StandardError.Length);
	}

	[Fact]
	public async Task RunAsync_ReturnsExitCodeAndBothStreams()
	{
		if (!OperatingSystem.IsLinux())
			return;

		var result = await ProcessRunner.RunAsync(SHELL, "-c \"printf out; printf err >&2; exit 3\"");

		Assert.Equal(3, result.ExitCode);
		Assert.Equal("out", result.StandardOutput);
		Assert.Equal("err", result.StandardError);
		Assert.Equal("outerr", result.Output);
	}

	[Fact]
	public async Task RunAsync_Timeout_KillsAndReturnsError()
	{
		if (!OperatingSystem.IsLinux())
			return;

		var result = await ProcessRunner.RunAsync(SHELL, ["-c", "sleep 30"], TimeSpan.FromMilliseconds(200));

		Assert.Equal(-1, result.ExitCode);
		Assert.Contains("did not answer", result.StandardError);
	}

	[Fact]
	public async Task RunAsync_MissingExecutable_ReturnsError()
	{
		var result = await ProcessRunner.RunAsync("grin-command-that-does-not-exist", ["--version"]);

		Assert.Equal(-1, result.ExitCode);
		Assert.False(string.IsNullOrEmpty(result.StandardError));
	}
}
