namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgUpdate
{
    internal static byte[] PrepareSageArchivePayload(uint uid, uint timer)
    {
        var proto = new UpdateProto
        {
            UID = uid,
            Updates = [new UpdateQuery
            {
                Type = DataType.ArchiveSkill,
                Flag = (uint)Flags.SageSpeedMode,
                Time = timer,
            }],
        };
        using var serialized = new MemoryStream();
        SerializeTypedUpdateProto(serialized, proto);
        return serialized.ToArray();
    }
}
