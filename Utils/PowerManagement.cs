using System.Runtime.InteropServices;

namespace GrinVideoEncoder.Utils;

/// <summary>
/// Utility class to prevent the computer from going to sleep during long-running operations.
/// </summary>
public static class PowerManagement
{
	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern uint SetThreadExecutionState(uint esFlags);

	private const uint ES_CONTINUOUS = 0x80000000;
	private const uint ES_SYSTEM_REQUIRED = 0x00000001;
	private const uint ES_DISPLAY_REQUIRED = 0x00000002;

	/// <summary>
	/// Prevents the system from entering sleep mode.
	/// Call this when starting a long-running operation.
	/// </summary>
	public static void PreventSleep()
	{
		SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
	}

	/// <summary>
	/// Prevents the system from entering sleep mode and keeps the display on.
	/// Call this when starting a long-running operation that requires display.
	/// </summary>
	public static void PreventSleepAndDisplay()
	{
		SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
	}

	/// <summary>
	/// Allows the system to enter sleep mode normally.
	/// Call this when the long-running operation is complete.
	/// </summary>
	public static void AllowSleep()
	{
		SetThreadExecutionState(ES_CONTINUOUS);
	}
}
