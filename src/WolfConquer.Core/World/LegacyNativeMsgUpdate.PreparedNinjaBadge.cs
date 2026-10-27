namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgUpdate
{
    internal static byte[] PrepareNinjaBadgeFrame(uint uid, byte badge, string? seal)
    {
        var proto = new UpdateProto { UID = uid, Updates = [new UpdateQuery { Type = DataType.NiniaP0, Value = badge }] };
        using var payload = new MemoryStream();
        SerializeTypedUpdateProto(payload, proto);
        return LegacyNativePreparedPacketFrames.Frame(2176, payload.ToArray(), seal);
    }
}
