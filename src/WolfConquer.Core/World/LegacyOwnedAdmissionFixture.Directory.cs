using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

public static partial class LegacyOwnedAdmissionFixture
{
    // Directory.CreateDirectory adopts an existing directory. The offline
    // writer must instead claim a NEW stage or refuse, including a create race.
    private static void CreateNewDirectory(string path)
    {
        bool created;
        if (OperatingSystem.IsWindows()) created = CreateDirectoryWindows(path, IntPtr.Zero);
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) created = CreateDirectoryUnix(path, 448) == 0; //0700
        else throw new PlatformNotSupportedException("Exclusive synthetic fixture directory creation is unavailable.");
        if (!created)
            throw new IOException("Could not exclusively create the new synthetic fixture stage.",
                new Win32Exception(Marshal.GetLastPInvokeError()));
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode,
        SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryWindows(string path, IntPtr securityAttributes);

    [DllImport("libc", EntryPoint = "mkdir", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
    private static extern int CreateDirectoryUnix(string path, uint mode);
}
