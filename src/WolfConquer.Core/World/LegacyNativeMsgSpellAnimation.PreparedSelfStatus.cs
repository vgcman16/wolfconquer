namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgSpellAnimation
{
    // Same complete contract projection and typed serializer, without touching
    // caller storage or borrowing a possibly unbounded native writer.
    internal byte[] PrepareAnimationPayload()
    {
        var proto = CreateAnimationValue(Targets);
        using var bytes = new MemoryStream();
        SerializeTypedSpellUseProto(bytes, proto);
        return bytes.ToArray();
    }
}
