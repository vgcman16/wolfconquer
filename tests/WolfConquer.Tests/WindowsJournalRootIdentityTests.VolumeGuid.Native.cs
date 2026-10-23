using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Tests;

public sealed partial class WindowsJournalRootIdentityTests
{
    // Independent read-only native observation of THIS owned directory, not an
    // invented GUID, provider, mapping or production handle/path export API.
    private static string ObservedVolumeGuidPath(string ordinaryOwnedDirectory)
    {
        using var handle = OpenAliasDirectory(ordinaryOwnedDirectory, 0x80, 1, IntPtr.Zero,
            3, 0x02000000 | 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new IOException("Cannot inspect the owned volume alias.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        var path = new StringBuilder(32_768);
        var length = ReadAliasDirectoryPath(handle, path, (uint)path.Capacity, 1); // VOLUME_NAME_GUID | FILE_NAME_NORMALIZED
        if (length == 0)
            throw new IOException("Cannot read the owned volume alias.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        if (length >= path.Capacity) throw new IOException("Owned alias exceeds the bounded native buffer.");
        var actual = path.ToString();
        Assert.StartsWith(@"\\?\Volume{", actual, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(ordinaryOwnedDirectory, actual);
        return actual;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle OpenAliasDirectory(string path, uint access, uint sharing,
        IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint ReadAliasDirectoryPath(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);
}
