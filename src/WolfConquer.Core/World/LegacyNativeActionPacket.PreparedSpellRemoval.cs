namespace WolfConquer.Core.World;

internal static partial class LegacyNativeActionPacket
{
    internal static byte[] PrepareSpellRemovalFrame(uint uid, ushort id, string? seal)
    {
        var query = new LegacyNativeActionQuery { ObjId = uid, dwParam = id, Type = 109 };
        using var payload = new MemoryStream();
        Serialize(payload, query);
        return LegacyNativePreparedPacketFrames.Frame(2046, payload.ToArray(), seal);
    }
}
