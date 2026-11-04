using System.Management;
using System.Runtime.InteropServices;

namespace GrinVideoEncoder.Utils;

public class GpuDetector
{
	public enum GpuVendor
	{
		Nvidia,
		AMD,
		Intel,
		/// <summary>Linux: any GPU used through VA-API, validated at startup by EncoderRequirements.</summary>
		Vaapi,
		Unknown
	}

	/// <summary>
	/// The render device validated at startup, used by hevc_vaapi.
	/// </summary>
	public static string VaapiDevice { get; set; } = "/dev/dri/renderD128";

	public static GpuVendor DetectGpuVendor()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return DetectGpuVendorWindows();
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return GpuVendor.Vaapi;
		}

		return GpuVendor.Unknown;
	}

	private static GpuVendor DetectGpuVendorWindows()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return GpuVendor.Unknown;
		}
		try
		{
			using ManagementObjectSearcher searcher = new("SELECT * FROM Win32_VideoController");
			foreach (var obj in searcher.Get().Cast<ManagementObject>())
			{
				string name = obj["Name"]?.ToString() ?? "";
				string adapterCompatibility = obj["AdapterCompatibility"]?.ToString() ?? "";

				// Check by manufacturer
				if (adapterCompatibility.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
					return GpuVendor.Nvidia;

				if (adapterCompatibility.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) ||
adapterCompatibility.Contains("AMD", StringComparison.OrdinalIgnoreCase))
				{
					return GpuVendor.AMD;
				}

				if (adapterCompatibility.Contains("Intel", StringComparison.OrdinalIgnoreCase))
					return GpuVendor.Intel;

				// Check by device name if manufacturer wasn't clear
				if (name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
					return GpuVendor.AMD;

				if (name.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
					name.Contains("Quadro", StringComparison.OrdinalIgnoreCase))
				{
					return GpuVendor.Nvidia;
				}
			}
		}
		catch
		{
			// Handle exceptions if WMI isn't available
		}

		return GpuVendor.Unknown;
	}
}
