using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Three complete native Achievement primitive codecs, not the modern startup projection.
internal static class LegacyNativeAchievementPacket
{
    public static void GetAchievement(this LegacyNativePacket stream, out LegacyNativeClientAchievement.TypeAchievement action, out uint UID, out uint Flag)
    {
        action = (LegacyNativeClientAchievement.TypeAchievement)stream.ReadUInt32();
        UID = stream.ReadUInt32();
        stream.ReadUInt32();
        Flag = stream.ReadUInt32();
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
