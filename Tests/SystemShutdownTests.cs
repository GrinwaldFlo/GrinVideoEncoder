using GrinVideoEncoder.Utils;

namespace Tests;

public class SystemShutdownTests
{
	[Fact]
	public void GetArguments_Windows_UsesWindowsSyntax()
	{
		string[] args = SystemShutdown.GetArguments(isWindows: true);

		Assert.Equal(["/s", "/t", "60", "/c"], args[..4]);
	}

	[Fact]
	public void GetArguments_Linux_PowersOffInOneMinute()
	{
		string[] args = SystemShutdown.GetArguments(isWindows: false);

		Assert.Equal(["-h", "+1"], args[..2]);
		Assert.DoesNotContain(args, a => a.StartsWith('/'));
	}
}
