using ProtoBuf;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgNinja
{
    internal static byte[] PrepareNinjaUserFrame(MsgNinjaUser obj, string? seal)
    {
        ArgumentNullException.ThrowIfNull(obj);
        // Real scalar message value only. No replacement owner, callback or
        // live-client state is manufactured during serializer preparation.
        var snapshot = new MsgNinjaUser
        {
            Type = obj.Type, UID = obj.UID, Flag = obj.Flag, Points = obj.Points,
            Fire = obj.Fire, Water = obj.Water, Earth = obj.Earth, Wind = obj.Wind, Lightning = obj.Lightning,
            FireNew = obj.FireNew, WaterNew = obj.WaterNew, EarthNew = obj.EarthNew, WindNew = obj.WindNew, LightningNew = obj.LightningNew,
            Score = obj.Score, FireMastery = obj.FireMastery, WaterMastery = obj.WaterMastery, EarthMastery = obj.EarthMastery,
            WindMastery = obj.WindMastery, LightningMastery = obj.LightningMastery, UnlockMastery = obj.UnlockMastery,
        };
        using var payload = new MemoryStream();
        // Preserve the actual primitive's object-typed protobuf-net invocation.
        Serializer.Serialize((Stream)payload, (object)snapshot);
        return LegacyNativePreparedPacketFrames.Frame(MsgNinjaUser_ID, payload.ToArray(), seal);
    }
}
