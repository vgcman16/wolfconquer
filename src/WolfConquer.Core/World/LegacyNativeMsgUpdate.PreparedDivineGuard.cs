namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgUpdate
{
    internal static byte[] PrepareDivineGuardPayload(uint uid)
    {
        var proto = new UpdateProto
        {
            UID = uid,
            Updates = [new UpdateQuery
            {
                Type = DataType.AzureShield,
                Flag = (uint)Flags.DivineGuard,
                Time = 15,
            }],
        };
        using var serialized = new MemoryStream();
        SerializeTypedUpdateProto(serialized, proto);
        return serialized.ToArray();
    }
}
