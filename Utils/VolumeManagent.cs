using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.AspNetCore.Routing.Constraints;

namespace GrinVideoEncoder.Utils;

public static partial class VolumeManagent
{
	[LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetDiskFreeSpaceExW(
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

		if (GetDiskFreeSpaceExW(fullPath, out ulong freeBytes, out _, out _))
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
