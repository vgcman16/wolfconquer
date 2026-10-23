using System;
using System.Linq;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeMsgShowHandTable
{
    public static LegacyNativePacket CreateShowHandTable(this LegacyNativePacket stream, LegacyNativePokerTableRuntime table)
    {
        stream.InitWriter();
        stream.Write(table.Id);
        stream.ZeroFill(8);
        stream.Write(table.X);
        stream.Write(table.Y);
        stream.Write(table.Mesh);
        stream.Write((ushort)0);
        stream.Write(table.Number);
        stream.Write(table.UnLimited ? 1 : 0);
        stream.Write(table.IsCPs ? 1 : 0);
        stream.Write((ulong)table.MinBet);
        stream.Write(0);
        stream.Write((byte)table.State);
        stream.Write(table.TotalPot);
        stream.Write((byte)table.Players.Count);
        foreach (LegacyNativePokerPlayer value in table.Players.Values)
        {
            stream.Write(value.Uid);
            stream.Write(value.Seat);
            stream.Write((byte)1);
        }
        stream.Finalize(2015);
        return stream;
    }
}
