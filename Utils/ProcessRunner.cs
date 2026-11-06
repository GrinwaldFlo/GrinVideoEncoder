using System.ComponentModel;
using System.Diagnostics;

namespace GrinVideoEncoder.Utils;

/// <summary>
/// Result of an external command.
/// </summary>
public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError = "")
{
	/// <summary>
	/// Standard output followed by standard error.
	/// </summary>
	public string Output => StandardOutput + StandardError;
}

/// <summary>
/// Runs external commands. Both output streams are read at the same time, so a command that writes a lot to
/// one of them cannot block on a full pipe.
/// </summary>
public static class ProcessRunner
{
	/// <summary>
	/// Runs a command with its arguments given as one string, parsed by the platform rules.
	/// </summary>
	public static Task<CommandResult> RunAsync(string fileName, string arguments, TimeSpan? timeout = null)
	{
		var startInfo = CreateStartInfo(fileName);
		startInfo.Arguments = arguments;
		return RunAsync(startInfo, timeout);
	}

	/// <summary>
	/// Runs a command with each argument passed as is, so paths containing spaces or quotes stay intact.
	/// </summary>
	public static Task<CommandResult> RunAsync(string fileName, IEnumerable<string> arguments, TimeSpan? timeout = null)
	{
		var startInfo = CreateStartInfo(fileName);
		foreach (string argument in arguments)
			startInfo.ArgumentList.Add(argument);
		return RunAsync(startInfo, timeout);
	}

	private static ProcessStartInfo CreateStartInfo(string fileName) => new(fileName)
	{
		UseShellExecute = false,
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		CreateNoWindow = true
	};

	private static async Task<CommandResult> RunAsync(ProcessStartInfo startInfo, TimeSpan? timeout)
	{
		using var process = new Process { StartInfo = startInfo };
		using var timeoutCts = timeout is null ? new CancellationTokenSource() : new CancellationTokenSource(timeout.Value);
		try
		{
			process.Start();
			var output = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
			var error = process.StandardError.ReadToEndAsync(timeoutCts.Token);
			await process.WaitForExitAsync(timeoutCts.Token);
			return new CommandResult(process.ExitCode, await output, await error);
		}
		catch (OperationCanceledException)
		{
			process.Kill(entireProcessTree: true);
			return new CommandResult(-1, string.Empty, $"{startInfo.FileName} did not answer within {timeout?.TotalSeconds:F0} seconds.");
		}
		catch (Win32Exception ex)
		{
			return new CommandResult(-1, string.Empty, ex.Message);
		}
	}
}
