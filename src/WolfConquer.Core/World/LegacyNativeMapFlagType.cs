using System;

namespace WolfConquer.Core.World;

[Flags]
internal enum LegacyNativeMapFlagType : byte
{
    None = 0,
    Valid = 1,
    Monster = 2,
    Item = 4,
    Player = 8,
    Npc = 0x10
}
