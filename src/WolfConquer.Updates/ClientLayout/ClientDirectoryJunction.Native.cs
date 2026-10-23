using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace WolfConquer.Updates.ClientLayout;

internal static partial class ClientDirectoryJunction
{
    // Windows SDK 10.0.26100.0 winioctl.h, winnt.h, winbase.h.
    private const uint MountPointTag = 0xA0000003, GetReparsePoint = 0x000900A8,
        SetReparsePoint = 0x000900A4, DeleteReparsePoint = 0x000900AC;
    private const int MaximumReparseBytes = 16 * 1024;
    private const uint ReadAttributes = 0x80, ListDirectory = 0x1, GenericWrite = 0x40000000,
        DeleteAccess = 0x10000, ShareRead = 1, ShareWrite = 2;

    private static string NativePath(string path) => @"\\?\" + path;

    private static SafeFileHandle OpenDirectory(string path, uint access, uint share) =>
        TryOpenDirectory(path, access, share) ?? throw new DirectoryNotFoundException("An exact junction directory or required parent is missing.");

    private static SafeFileHandle? TryOpenDirectory(string path, uint access, uint share)
    {
        var handle = CreateFileW(NativePath(path), access, share, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        var error = Marshal.GetLastWin32Error();
        handle.Dispose();
        if (error is 2 or 3)
        {
            return null;
        }
        throw NativeError("Cannot open the exact no-follow directory handle.", error);
    }

    private static ByHandleFileInformation ReadInformation(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var information)) throw NativeError("Cannot query directory handle attributes.");
        return information;
    }

    private static void RemoveMountPointData(SafeFileHandle handle)
    {
        var header = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header, MountPointTag);
        if (!DeviceIoControl(handle, DeleteReparsePoint, header, (uint)header.Length, null, 0, out _, IntPtr.Zero))
            throw NativeError("Cannot remove the verified mount-point tag.");
    }

    private static void MarkEmptyDirectoryForDeletion(SafeFileHandle handle)
    {
        // FILE_DISPOSITION_INFO.DeleteFile is BOOLEAN (one byte), not Win32 BOOL.
        // This cannot recursively delete children; a nonempty ordinary directory fails.
        if (!SetFileInformationByHandle(handle, 4, new byte[] { 1 }, 1))
            throw NativeError("Cannot mark the exact empty directory handle for nonrecursive deletion.");
    }

    private static IOException NativeError(string message, int? error = null) =>
        new(message, new Win32Exception(error ?? Marshal.GetLastWin32Error()));

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public FileAttributes Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryW(string path, IntPtr security);

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, [In] byte[]? input, uint inputBytes,
        [Out] byte[]? output, uint outputBytes, out uint returned, IntPtr overlapped);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation information);

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint characters, uint flags);

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationByHandleW(SafeFileHandle handle, IntPtr volumeName, uint volumeCharacters,
        IntPtr serial, IntPtr maximumComponent, out uint flags, StringBuilder fileSystem, uint fileSystemCharacters);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, [In] byte[] information, uint bytes);
}
