using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Routing.Constraints;

namespace GrinVideoEncoder.Utils;

public static class VolumeManagent
{
	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
	private static extern bool GetDiskFreeSpaceEx(
		string lpDirectoryName,
		out ulong lpFreeBytesAvailable,
		out ulong lpTotalNumberOfBytes,
		out ulong lpTotalNumberOfFreeBytes);

	public static long GetFreeSpaceByte(string path)
	{
		string fullPath = Path.GetFullPath(path);

		string? root = Path.GetPathRoot(fullPath);
		if (!string.IsNullOrEmpty(root) && !root.StartsWith("\\\\"))
		{
			return new DriveInfo(root).AvailableFreeSpace;
		}

		if (GetDiskFreeSpaceEx(fullPath, out ulong freeBytes, out _, out _))
		{
			return (long)freeBytes;
		}

		return 0;
	}

	public static long GetDirectorySize(string path)
	{
		var dir = new DirectoryInfo(path);
		return dir.GetFiles("*", SearchOption.AllDirectories).Sum(x => x.Length);
	}
}
