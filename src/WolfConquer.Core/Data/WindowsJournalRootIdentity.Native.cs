using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Core.Data;

internal sealed partial class WindowsJournalRootIdentity
{
    private const uint ReadDirectoryAndAttributes = 0x1 | 0x80;
    private const uint ShareReadWrite = 1 | 2;
    private const uint OpenExisting = 3;
    private const uint DirectoryHandleFlags = 0x02000000 | 0x00200000;
    private const int AttributeTagInfo = 9;
    private const int FileIdInfo = 18;

    private static SafeFileHandle OpenOrdinaryDirectory(string path)
    {
        // FILE_LIST_DIRECTORY requests actual read access so sharing is enforced;
        // a metadata-only FILE_READ_ATTRIBUTES open did not prevent an ordinary
        // rename in the owned fixture. No delete sharing is still NOT a namespace
        // lock over children, a retained journal lifetime, or an ACL proof.
        // Share writes explicitly: ordinary child replacement must remain usable.
        // OPEN_REPARSE_POINT applies to the opened component. It does not prove
        // that the supplied spelling had no intermediate reparse traversal.
        var handle = CreateFileW(path, ReadDirectoryAndAttributes, ShareReadWrite, IntPtr.Zero,
            OpenExisting, DirectoryHandleFlags, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw NativeFailure("Cannot retain a configured directory", error);
        }
        try { RequireOrdinaryDirectory(handle); return handle; }
        catch { handle.Dispose(); throw; }
    }

    private static void RequireOrdinaryDirectory(SafeFileHandle handle)
    {
        if (!ReadAttributeTag(handle, AttributeTagInfo, out var value, (uint)Marshal.SizeOf<AttributeTag>()))
            throw NativeFailure("Cannot inspect retained directory attributes", Marshal.GetLastWin32Error());
        if ((value.Attributes & FileAttributes.Directory) == 0 ||
            (value.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("A retained object is not an ordinary non-reparse directory.");
    }

    private static FileIdentity ReadIdentity(SafeFileHandle handle)
    {
        if (!ReadFileId(handle, FileIdInfo, out var value, (uint)Marshal.SizeOf<NativeFileId>()))
            throw NativeFailure("Cannot inspect retained directory identity", Marshal.GetLastWin32Error());
        return new(value.VolumeSerial, value.Low, value.High);
    }

    private static string ReadFinalVolumePath(SafeFileHandle handle)
    {
        // Win32 extended path limit; one bounded buffer, no silent truncation or
        // fallback to a drive-name/string identity comparison.
        var buffer = new StringBuilder(32_768);
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 1);
        if (length == 0) throw NativeFailure("Cannot resolve a retained directory", Marshal.GetLastWin32Error());
        if (length >= buffer.Capacity) throw new IOException("The retained directory path exceeds the prototype bound.");
        return buffer.ToString();
    }

    private static IOException NativeFailure(string operation, int error) =>
        new($"{operation} (Win32 {error}).", new Win32Exception(error));

    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeTag { internal FileAttributes Attributes; internal uint Tag; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileId { internal ulong VolumeSerial; internal ulong Low; internal ulong High; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint sharing,
        IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadAttributeTag(SafeFileHandle handle, int informationClass, out AttributeTag value, uint size);
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFileId(SafeFileHandle handle, int informationClass, out NativeFileId value, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);
}
