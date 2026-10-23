using System;
using System.Linq;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal class LegacyNativeMsgRouletteTable
{
    public enum TableType : uint
    {
        Money = 1u,
        ConquerPoints
    }

    public uint UID;

    public uint TableNumber;

    public TableType MoneyType;

    public ushort X;

    public ushort Y;

    public ushort Mesh;

    public byte PlayersCount;
}
