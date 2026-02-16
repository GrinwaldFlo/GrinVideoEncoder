using System.Runtime.InteropServices;

namespace GrinVideoEncoder.Utils;

/// <summary>
/// Utility class to prevent the computer from going to sleep during long-running operations.
/// Uses the modern PowerCreateRequest API which is more reliable than SetThreadExecutionState.
/// </summary>
public static class PowerManagement
{
	private static IntPtr _powerRequest = IntPtr.Zero;

	/// <summary>
	/// Power request type for system required (prevents sleep).
	/// </summary>
	private const uint PowerRequestTypeSystemRequired = 0;

	/// <summary>
	/// Power request type for display required (keeps display on).
	/// </summary>
	private const uint PowerRequestTypeDisplayRequired = 1;

	/// <summary>
	/// Allows the system to enter sleep mode normally.
	/// Call this when the long-running operation is complete.
	/// </summary>
	public static void AllowSleep()
	{
		if (_powerRequest != IntPtr.Zero)
		{
			PowerClearRequest(_powerRequest, PowerRequestTypeSystemRequired);
			PowerClearRequest(_powerRequest, PowerRequestTypeDisplayRequired);
			CloseHandle(_powerRequest);
			_powerRequest = IntPtr.Zero;
		}
	}

	/// <summary>
	/// Prevents the system from entering sleep mode.
	/// Call this when starting a long-running operation.
	/// This uses the modern PowerCreateRequest API which is more reliable than SetThreadExecutionState.
	/// </summary>
	public static void PreventSleep()
	{
		if (_powerRequest == IntPtr.Zero)
		{
			_powerRequest = PowerCreateRequest(new REASON_CONTEXT { Reason = 0 });
		}

		if (_powerRequest != IntPtr.Zero)
		{
			PowerSetRequest(_powerRequest, PowerRequestTypeSystemRequired);
		}
	}

	/// <summary>
	/// Prevents the system from entering sleep mode and keeps the display on.
	/// Call this when starting a long-running operation that requires display.
	/// This uses the modern PowerCreateRequest API which is more reliable than SetThreadExecutionState.
	/// </summary>
	public static void PreventSleepAndDisplay()
	{
		if (_powerRequest == IntPtr.Zero)
		{
			_powerRequest = PowerCreateRequest(new REASON_CONTEXT { Reason = 0 });
		}

		if (_powerRequest != IntPtr.Zero)
		{
			PowerSetRequest(_powerRequest, PowerRequestTypeSystemRequired);
			PowerSetRequest(_powerRequest, PowerRequestTypeDisplayRequired);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct REASON_CONTEXT
	{
		public uint Version;
		public uint Reason;
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr PowerCreateRequest(REASON_CONTEXT context);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool PowerSetRequest(IntPtr powerRequest, uint powerRequestType);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool PowerClearRequest(IntPtr powerRequest, uint powerRequestType);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(IntPtr handle);
}