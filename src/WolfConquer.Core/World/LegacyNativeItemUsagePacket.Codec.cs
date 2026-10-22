#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeItemUsagePacket
{
    public static void GetUsageItem(this LegacyNativePacket msg, out ItemUsuageID action, out uint id,
        out ulong dwParam, out uint timestamp, out uint dwParam2, out uint dwParam3, out uint dwparam4,
        out List<uint> args, out byte LoaderMessage)
    {
        id = msg.ReadUInt32();
        dwParam = msg.ReadUInt64();
        action = (ItemUsuageID)msg.ReadInt16();
        timestamp = msg.ReadUInt32();
        dwParam2 = msg.ReadUInt32();
        dwParam3 = msg.ReadUInt32();
        dwparam4 = msg.ReadUInt32();
        msg.SeekForward(12);
        msg.SeekForward(2);
        LoaderMessage = msg.ReadUInt8();
        msg.SeekForward(2);
        msg.SeekForward(32);
        args = new List<uint>();
        if (dwParam2 != 0 && dwParam2 < 50)
        {
            msg.SeekForward(8);
            for (uint num = 0u; num < dwParam2; num++)
            {
                args.Add(msg.ReadUInt32());
            }
        }
    }

    public static LegacyNativePacket ItemUsageCreate(this LegacyNativePacket msg, ItemUsuageID action,
        uint id, ulong dwParam1, uint timestamp, uint dwParam2, uint dwParam3, uint dwparam4,
        List<uint> args = null)
    {
        msg.InitWriter();
        msg.Write(id);
        msg.Write(dwParam1);
        msg.Write((ushort)action);
        msg.Write((timestamp != 0) ? timestamp : ((uint)DateTime.Now.Value()));
        msg.Write(dwParam2);
        msg.Write(dwParam3);
        msg.Write(dwparam4);
        msg.SeekForward(48);
        msg.SeekForward(8);
        msg.SeekForward(1);
        if (args != null)
        {
            foreach (uint arg in args)
            {
                msg.Write(arg);
            }
        }
        msg.Finalize(2137);
        return msg;
    }
}
