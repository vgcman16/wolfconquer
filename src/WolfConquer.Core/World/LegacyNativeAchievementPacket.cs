using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Three complete native Achievement primitive codecs, not the modern startup projection.
internal static class LegacyNativeAchievementPacket
{
    public static void GetAchievement(this LegacyNativePacket stream, out LegacyNativeClientAchievement.TypeAchievement action, out uint UID, out uint Flag)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prepared = stream.ReadPrepared(static packet =>
        {
            var parsedAction = (LegacyNativeClientAchievement.TypeAchievement)packet.ReadUInt32();
            uint parsedUid = packet.ReadUInt32();
            packet.ReadUInt32();
            uint parsedFlag = packet.ReadUInt32();
            return (Action: parsedAction, Uid: parsedUid, Flag: parsedFlag);
        });
        action = prepared.Action;
        UID = prepared.Uid;
        Flag = prepared.Flag;
    }

    public static LegacyNativePacket AchievementCreate(this LegacyNativePacket stream,
        LegacyNativeClientAchievement.TypeAchievement action, uint UID, uint[] Flags)
    {
        stream.InitWriter();
        stream.Write((uint)action);
        stream.Write(UID);
        stream.Write(0u);
        stream.Write(Flags.Length);
        for (int i = 0; i < Flags.Length; i++)
        {
            stream.Write(Flags[i]);
        }
        stream.Finalize(2307);
        return stream;
    }

    public static LegacyNativePacket AchievementCreate(this LegacyNativePacket stream,
        LegacyNativeClientAchievement.TypeAchievement action, uint UID, uint Flag)
    {
        stream.InitWriter();
        stream.Write((uint)action);
        stream.Write(UID);
        stream.Write(0u);
        stream.Write(Flag);
        stream.Write(0u);
        stream.Finalize(2307);
        return stream;
    }
}
