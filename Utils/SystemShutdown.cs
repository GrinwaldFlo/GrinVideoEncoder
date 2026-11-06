namespace GrinVideoEncoder.Utils;

/// <summary>
/// Asks the operating system to power off the computer one minute from now.
/// </summary>
public static class SystemShutdown
{
	private const string REASON = "GrinVideoEncoder scheduled shutdown";
	private static readonly TimeSpan _commandTimeout = TimeSpan.FromSeconds(30);

	/// <summary>
	/// The arguments of the <c>shutdown</c> command, which differ between Windows and Linux.
	/// </summary>
	internal static string[] GetArguments(bool isWindows) => isWindows
		? ["/s", "/t", "60", "/c", REASON]
		: ["-h", "+1", REASON];

	/// <summary>
	/// Schedules the shutdown. The command returns once the shutdown is scheduled, so a non-zero exit code means it was refused.
	/// </summary>
	public static Task<CommandResult> RequestAsync() =>
		ProcessRunner.RunAsync("shutdown", GetArguments(OperatingSystem.IsWindows()), _commandTimeout);
}
