using System.ComponentModel;
using System.Diagnostics;
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

	private static async Task<CommandResult> RunCommandAsync(string fileName, string arguments)
	{
		using var process = new Process();
		process.StartInfo.FileName = fileName;
		process.StartInfo.Arguments = arguments;
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.RedirectStandardOutput = true;
		process.StartInfo.RedirectStandardError = true;
		process.StartInfo.CreateNoWindow = true;

		using var timeout = new CancellationTokenSource(_commandTimeout);
		try
		{
			process.Start();
			var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
			var error = process.StandardError.ReadToEndAsync(timeout.Token);
			await process.WaitForExitAsync(timeout.Token);
			return new CommandResult(process.ExitCode, await output + await error);
		}
		catch (OperationCanceledException)
		{
			process.Kill(entireProcessTree: true);
			return new CommandResult(-1, $"{fileName} did not answer within {_commandTimeout.TotalSeconds:F0} seconds.");
		}
		catch (Win32Exception ex)
		{
			return new CommandResult(-1, ex.Message);
		}
	}

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
