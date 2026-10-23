using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

internal partial struct LegacyNativeTime32
{
    public static LegacyNativeTime32 Now
    {
        get
        {
            NativeNowPrelude.smethod_0();
            return timeGetTime();
        }
    }

    [DllImport("winmm.dll")]
    public static extern LegacyNativeTime32 timeGetTime();

    // Actual reached Class1.Class0 dependency, not a configurable callback or
    // invented stub. Original smethod_0 IL is exactly nop;ret (RVA0x3ce0).
    // Its beforefieldinit .cctor only writes bool_0=false (RVA0x41e0).
    // Uncalled socket/remote-compilation methods are intentionally not imported.
    internal sealed class NativeNowPrelude
    {
        internal static bool bool_0 = false;

        internal static void smethod_0()
        {
        }
    }
}
