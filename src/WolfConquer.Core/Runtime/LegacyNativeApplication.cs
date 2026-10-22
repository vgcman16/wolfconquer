using System.Runtime.InteropServices;
using System.Text;

namespace WolfConquer.Core.Runtime;

// .NET9 replacement for the original Framework WinForms StartupPath dependency.
// The installed Framework Application/UnsafeNativeMethods bodies were read:
// same cached process-module directory and literal long-path growth algorithm.
// Framework CAS PathDiscovery.Demand has no .NET9/full-trust equivalent.
internal static class LegacyNativeApplication
{
    private static string? startupPath;

    internal static string? StartupPath
    {
        get
        {
            if (startupPath == null)
            {
                StringBuilder moduleFileNameLongPath = GetModuleFileNameLongPath(IntPtr.Zero);
                startupPath = Path.GetDirectoryName(moduleFileNameLongPath.ToString());
            }
            return startupPath;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetModuleFileNameW")]
    private static extern int GetModuleFileName(IntPtr hModule, StringBuilder buffer, int length);

    private static StringBuilder GetModuleFileNameLongPath(IntPtr hModule)
    {
        StringBuilder stringBuilder = new StringBuilder(260);
        int num = 1;
        int num2 = 0;
        while ((num2 = GetModuleFileName(hModule, stringBuilder, stringBuilder.Capacity)) == stringBuilder.Capacity &&
               Marshal.GetLastWin32Error() == 122 && stringBuilder.Capacity < 32767)
        {
            num += 2;
            int capacity = ((num * 260 < 32767) ? (num * 260) : 32767);
            stringBuilder.EnsureCapacity(capacity);
        }
        stringBuilder.Length = num2;
        return stringBuilder;
    }
}
