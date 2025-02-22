using System.Management;

namespace GrinVideoEncoder.Utils;

public class GpuDetector
{
	public enum GpuVendor
	{
		Nvidia,
		AMD,
		Intel,
		Unknown
	}

	public static GpuVendor DetectGpuVendor()
	{
		if (!OperatingSystem.IsWindows())
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
