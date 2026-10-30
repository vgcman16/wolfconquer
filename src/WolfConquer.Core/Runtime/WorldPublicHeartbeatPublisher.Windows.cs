using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Core.Runtime;

public sealed partial class WorldPublicHeartbeatPublisher
{
    // FILE_INFO_BY_HANDLE_CLASS.FileRenameInfoEx, with REPLACE_IF_EXISTS |
    // POSIX_SEMANTICS. Unlike MoveFileEx replacement, this retains open readers
    // of the old image while subsequent opens resolve the complete new image.
    // https://learn.microsoft.com/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information
    private const int FileRenameInfoEx = 22;
    private const uint RenameReplaceExistingAndPosix = 3;

    private unsafe void PublishCompletedFile(string temporary)
    {
        RequireOrdinaryPath(temporary); RequireOrdinaryPath(options.FilePath);
        if (!OperatingSystem.IsWindows())
        {
            File.Move(temporary, options.FilePath, overwrite: true);
            return;
        }

        // OPEN_EXISTING, DELETE | FILE_READ_ATTRIBUTES, all sharing modes,
        // OPEN_REPARSE_POINT. The flushed temp is never recreated or truncated.
        using var source = OpenHeartbeatRenameSource(@"\\?\" + Path.GetFullPath(temporary),
            0x00010080, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (source.IsInvalid) throw HeartbeatRenameFailure(Marshal.GetLastPInvokeError());
        if ((File.GetAttributes(source) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new IOException("Public heartbeat rename source is not an ordinary file.");

        byte[] name = new UnicodeEncoding(false, false, true).GetBytes(options.FilePath);
        var header = new HeartbeatRenameInformation(checked((uint)name.Length));
        int nameOffset = Marshal.OffsetOf<HeartbeatRenameInformation>(nameof(HeartbeatRenameInformation.FileName)).ToInt32();
        byte[] buffer = new byte[checked(nameOffset + name.Length + sizeof(ushort))];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(
            Marshal.OffsetOf<HeartbeatRenameInformation>(nameof(HeartbeatRenameInformation.Flags)).ToInt32()), header.Flags);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(
            Marshal.OffsetOf<HeartbeatRenameInformation>(nameof(HeartbeatRenameInformation.FileNameLength)).ToInt32()), header.FileNameLength);
        name.CopyTo(buffer.AsSpan(nameOffset)); // Zero root handle, padding and final WCHAR come from the new array.
        RequireOrdinaryPath(temporary); RequireOrdinaryPath(options.FilePath);
        fixed (byte* information = buffer)
            if (!RenameHeartbeatFile(source, FileRenameInfoEx, information, checked((uint)buffer.Length)))
                throw HeartbeatRenameFailure(Marshal.GetLastPInvokeError());
    }

    private static IOException HeartbeatRenameFailure(int error) => new(
        "Public heartbeat atomic rename unavailable.", unchecked((int)(0x80070000U | ((uint)error & 0xFFFFU))));

    // Sequential native alignment derives both offsets for the current process;
    // no x64-only offsets or one-byte BOOLEAN union layout are assumed.
    [StructLayout(LayoutKind.Sequential)]
    private struct HeartbeatRenameInformation
    {
        internal uint Flags;
        internal IntPtr RootDirectory;
        internal uint FileNameLength;
        internal ushort FileName;
        internal HeartbeatRenameInformation(uint length)
        { Flags = RenameReplaceExistingAndPosix; RootDirectory = IntPtr.Zero; FileNameLength = length; FileName = 0; }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial SafeFileHandle OpenHeartbeatRenameSource(string path, uint access, uint sharing,
        IntPtr security, uint disposition, uint attributes, IntPtr template);

    [LibraryImport("kernel32.dll", EntryPoint = "SetFileInformationByHandle", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool RenameHeartbeatFile(SafeFileHandle source, int informationClass,
        byte* information, uint size);
}
