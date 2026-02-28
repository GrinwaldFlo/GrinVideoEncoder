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
		if (string.IsNullOrEmpty(path))
			return 0;

		string fullPath = Path.GetFullPath(path);

		string? root = Path.GetPathRoot(fullPath);

		return !string.IsNullOrEmpty(root) && !root.StartsWith("\\\\")
			? new DriveInfo(root).AvailableFreeSpace
			: GetDiskFreeSpaceExW(fullPath, out ulong freeBytes, out _, out _) ? (long)freeBytes : 0;
	}

	public static long GetDirectorySize(string path)
	{
		var dir = new DirectoryInfo(path);
		return dir.GetFiles("*", SearchOption.AllDirectories).Sum(x => x.Length);
	}
}
