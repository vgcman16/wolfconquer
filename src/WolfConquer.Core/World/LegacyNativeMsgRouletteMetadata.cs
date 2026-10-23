using System;
using System.Linq;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Sequential, Size = 1)]
internal partial struct LegacyNativeMsgRouletteCheck
{
    public class Item
    {
        public byte Number;

        public uint BetPrice;
    }
}
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal partial struct LegacyNativeMsgRouletteSignUp
{
    public enum ActionJoin : byte
    {
        Join = 0,
        Watch = 4,
        InfoTable = 5,
        Quit = 7
    }
}
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal partial struct LegacyNativeMsgRouletteAction
{
    public enum Action : byte
    {
        Join = 3,
        RemovePlayer = 2,
        ShowSpectators = 1,
        Record = 0
    }
}
