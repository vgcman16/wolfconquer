using System;
using System.Linq;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgRouletteOpenGui
{
    public enum Color : byte
    {
        Blue = 0,
        Yellow = 1,
        Green = 2,
        Mauve = 3,
        Gray = 4,
        None = 5,
        Watch = 99
    }
}
