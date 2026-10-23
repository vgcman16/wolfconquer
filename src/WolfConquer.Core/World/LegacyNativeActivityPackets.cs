using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeActivityPackets
{
    // Whole native MsgBuilder631..679. Source widths/order/fault prefixes retained.
    public static void GetActivityClaims(this LegacyNativePacket stream, out uint IDClaim)
    {
        IDClaim = stream.ReadUInt8();
    }

    public static LegacyNativePacket ActivityClaimsCreate(this LegacyNativePacket stream, uint IDClaim)
    {
        stream.InitWriter();
        stream.Write((byte)IDClaim);
        stream.Finalize(2105);
        return stream;
    }

    public static LegacyNativePacket ActivityRewardsInfoCreate(this LegacyNativePacket stream, LegacyNativeMsgActivityRewardsInfo.Action mode, uint ActivityPoints)
    {
        stream.InitWriter();
        stream.Write((byte)mode);
        stream.Write(2u);
        stream.Write(1u);
        stream.Write(ActivityPoints);
        stream.Write(2u);
        stream.Write(2u);
        stream.Finalize(2174);
        return stream;
    }

    public static LegacyNativePacket ActivityTasksCreate(this LegacyNativePacket stream,
        LegacyNativeMsgActivityTasks.Action mode, LegacyNativeActivenessRuntime.Task[]? Items = null)
    {
        stream.InitWriter();
        stream.Write((byte)mode);
        if (Items != null)
        {
            stream.Write((byte)Items.Length);
            for (int i = 0; i < Items.Length; i++)
            {
                stream.Write(Items[i].ID);
                stream.Write(Items[i].Completed);
                stream.Write(Items[i].Progres);
            }
        }
        else
        {
            stream.Write((byte)0);
        }
        stream.Finalize(2316);
        return stream;
    }
}
