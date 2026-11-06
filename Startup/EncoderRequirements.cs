using Xabe.FFmpeg;

namespace GrinVideoEncoder.Startup;

/// <summary>
/// Startup check of the encoding setup. There is no CPU fallback, so the application must not start without it.
/// </summary>
public static class EncoderRequirements
{
	private const string DRI_FOLDER = "/dev/dri";
	private static readonly TimeSpan _commandTimeout = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Checks the setup and configures FFmpeg to use it. If the setup is not met, prints how to fix it.
	/// </summary>
	/// <returns>True if the application can start.</returns>
	public static async Task<bool> EnsureMetAsync()
	{
		if (!OperatingSystem.IsLinux())
			return true;

		var check = new VaapiRequirementsCheck(GlobUtils.FindExecutableInPath, FindRenderDevices, RunCommandAsync);
		var result = await check.RunAsync();

		if (!result.IsMet)
		{
			PrintProblem(result.Problem!);
			return false;
		}

		FFmpeg.SetExecutablesPath(result.FfmpegDirectory!);
		GpuDetector.VaapiDevice = result.Device!;
		Console.WriteLine($"Encoding setup OK: hevc_vaapi on {result.Device}, FFmpeg from {result.FfmpegDirectory}");
		return true;
	}

	private static IEnumerable<string> FindRenderDevices() =>
		Directory.Exists(DRI_FOLDER) ? Directory.GetFiles(DRI_FOLDER, "renderD*").Order() : [];

	private static Task<CommandResult> RunCommandAsync(string fileName, string arguments) =>
		ProcessRunner.RunAsync(fileName, arguments, _commandTimeout);

	private static void PrintProblem(RequirementProblem problem)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.Error.WriteLine();
		Console.Error.WriteLine($"Encoding setup not met: {problem.Title}");
		Console.ResetColor();
		Console.Error.WriteLine(problem.Detail);
		Console.Error.WriteLine();

		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.Error.WriteLine("How to fix it:");
		Console.ResetColor();
		foreach (string line in problem.Fix)
			Console.Error.WriteLine(line);

		Console.Error.WriteLine();
		Console.Error.WriteLine("GrinVideoEncoder needs hevc_vaapi on Linux and will now close. Start it again once this is fixed.");
	}
}
