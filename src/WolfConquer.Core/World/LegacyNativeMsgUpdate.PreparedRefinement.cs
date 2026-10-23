namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgUpdate
{
    internal static byte[] PrepareRefinementScalarPayload(uint uid, bool stamina)
    {
        var proto = new UpdateProto
        {
            UID = uid,
            Updates = [new UpdateQuery
            {
                Type = stamina ? DataType.Stamina : DataType.XPList,
                Value = stamina ? 100u : 1u,
            }],
        };
        using var serialized = new MemoryStream();
        SerializeTypedUpdateProto(serialized, proto);
        return serialized.ToArray();
    }
}
