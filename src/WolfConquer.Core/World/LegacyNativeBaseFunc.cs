using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Literal BaseFunc storage/import/RandGet projection. Other BaseFunc methods are not ported here.
public static class LegacyNativeBaseFunc
{
    private static byte[] _SteedSpeed = new byte[13]
    {
        0, 5, 10, 15, 20, 30, 40, 50, 65, 85,
        90, 95, 100
    };

    private static uint[] proficiencyLevelExperience = new uint[21]
    {
        0u, 1200u, 68000u, 250000u, 640000u, 1600000u, 4000000u, 10000000u, 22000000u, 40000000u,
        90000000u, 95000000u, 142500000u, 213750000u, 320625000u, 480937500u, 721406250u, 1082109375u, 1623164062u, 2100000000u,
        3200000000u
    };

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern long srand(ulong seed);

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern long rand();

    public static int RandGet(int nMax, bool bRealRand)
    {
        if (nMax <= 0)
        {
            nMax = 1;
        }
        if (bRealRand)
        {
            srand((ulong)DateTime.Now.Value());
        }
        long num = rand();
        return (int)(num % nMax);
    }
}
