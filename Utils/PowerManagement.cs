using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GrinVideoEncoder.Utils;

/// <summary>
/// Utility class to prevent the computer from going to sleep during long-running operations.
/// Uses the modern PowerCreateRequest API which is more reliable than SetThreadExecutionState.
/// </summary>
public static class PowerManagement
{
	private static readonly Lock SyncRoot = new();
	private static IntPtr _powerRequest = IntPtr.Zero;
	private static Process? _linuxInhibitProcess;

	/// <summary>
	/// Power request type for system required (prevents sleep).
	/// </summary>
	private const uint POWER_REQUEST_TYPE_SYSTEM_REQUIRED = 0;

	/// <summary>
	/// Power request type for display required (keeps display on).
	/// </summary>
	private const uint POWER_REQUEST_TYPE_DISPLAY_REQUIRED = 1;

	/// <summary>
	/// Allows the system to enter sleep mode normally.
	/// Call this when the long-running operation is complete.
	/// </summary>
	public static void AllowSleep()
	{
		lock (SyncRoot)
		{
			if (OperatingSystem.IsWindows())
			{
				if (_powerRequest != IntPtr.Zero)
				{
					PowerClearRequest(_powerRequest, POWER_REQUEST_TYPE_SYSTEM_REQUIRED);
					PowerClearRequest(_powerRequest, POWER_REQUEST_TYPE_DISPLAY_REQUIRED);
					CloseHandle(_powerRequest);
					_powerRequest = IntPtr.Zero;
				}
			}
			else if (OperatingSystem.IsLinux())
			{
				StopLinuxInhibit();
			}
		}
	}

	/// <summary>
	/// Prevents the system from entering sleep mode.
	/// Call this when starting a long-running operation.
	/// This uses the modern PowerCreateRequest API which is more reliable than SetThreadExecutionState.
	/// </summary>
	public static void PreventSleep()
	{
		lock (SyncRoot)
		{
			if (OperatingSystem.IsWindows())
			{
				if (_powerRequest == IntPtr.Zero)
				{
					_powerRequest = PowerCreateRequest(new ReasonContext { Reason = 0 });
				}

				if (_powerRequest != IntPtr.Zero)
				{
					PowerSetRequest(_powerRequest, POWER_REQUEST_TYPE_SYSTEM_REQUIRED);
				}
			}
			else if (OperatingSystem.IsLinux())
			{
				StartLinuxInhibit();
			}
		}
	}

	/// <summary>
	/// Prevents the system from entering sleep mode and keeps the display on.
	/// Call this when starting a long-running operation that requires display.
	/// This uses the modern PowerCreateRequest API which is more reliable than SetThreadExecutionState.
	/// </summary>
	public static void PreventSleepAndDisplay()
	{
		lock (SyncRoot)
		{
			if (OperatingSystem.IsWindows())
			{
				if (_powerRequest == IntPtr.Zero)
				{
					_powerRequest = PowerCreateRequest(new ReasonContext { Reason = 0 });
				}

				if (_powerRequest != IntPtr.Zero)
				{
					PowerSetRequest(_powerRequest, POWER_REQUEST_TYPE_SYSTEM_REQUIRED);
					PowerSetRequest(_powerRequest, POWER_REQUEST_TYPE_DISPLAY_REQUIRED);
				}
			}
			else if (OperatingSystem.IsLinux())
			{
				StartLinuxInhibit();
			}
		}
	}

	private static void StartLinuxInhibit()
	{
		if (_linuxInhibitProcess is { HasExited: false })
		{
			return;
		}

		StopLinuxInhibit();

		try
		{
			_linuxInhibitProcess = Process.Start(new ProcessStartInfo
			{
				FileName = "systemd-inhibit",
				// tail ends with this process, so the inhibition cannot outlive a crash or a kill
				Arguments = $"--what=sleep:idle --why=\"GrinVideoEncoder long-running operation\" tail --pid={Environment.ProcessId} -f /dev/null",
				UseShellExecute = false,
				CreateNoWindow = true
			});
		}
		catch
		{
			_linuxInhibitProcess = null;
		}
	}

	private static void StopLinuxInhibit()
	{
		if (_linuxInhibitProcess is null)
		{
			return;
		}

		try
		{
			if (!_linuxInhibitProcess.HasExited)
			{
				_linuxInhibitProcess.Kill(entireProcessTree: true);
				_linuxInhibitProcess.WaitForExit(2000);
			}
		}
		catch
		{
			// Ignore cleanup failures.
		}
		finally
		{
			_linuxInhibitProcess.Dispose();
			_linuxInhibitProcess = null;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ReasonContext
	{
		public uint Version;
		public uint Reason;
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr PowerCreateRequest(ReasonContext context);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool PowerSetRequest(IntPtr powerRequest, uint powerRequestType);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool PowerClearRequest(IntPtr powerRequest, uint powerRequestType);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(IntPtr handle);
}