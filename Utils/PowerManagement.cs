using System.Runtime.InteropServices;

namespace GrinVideoEncoder.Utils;

/// <summary>
/// Utility class to prevent the computer from going to sleep during long-running operations.
/// </summary>
public static class PowerManagement
{
	private const uint ES_CONTINUOUS = 0x80000000;

	private const uint ES_DISPLAY_REQUIRED = 0x00000002;

	private const uint ES_SYSTEM_REQUIRED = 0x00000001;

	/// <summary>
	/// Allows the system to enter sleep mode normally.
	/// Call this when the long-running operation is complete.
	/// </summary>
	public static void AllowSleep()
	{
		_ = SetThreadExecutionState(ES_CONTINUOUS);
	}

	/// <summary>
	/// Prevents the system from entering sleep mode.
	/// Call this when starting a long-running operation.
	/// </summary>
	public static void PreventSleep()
	{
		_ = SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
	}

	/// <summary>
	/// Prevents the system from entering sleep mode and keeps the display on.
	/// Call this when starting a long-running operation that requires display.
	/// </summary>
	public static void PreventSleepAndDisplay()
	{
		_ = SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern uint SetThreadExecutionState(uint esFlags);
}