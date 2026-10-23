namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgUpdate
{
    // Same complete native typed2176 serializer; private prepared bytes do not
    // mutate a caller packet or require an accepting transport callback.
    internal static byte[] PrepareStatusFlagPayload(uint uid, uint[] words)
    {
        var values = new ulong[words.Length];
        for (int index = 0; index < words.Length; index++) values[index] = words[index];
        var proto = new UpdateProto
        {
            UID = uid,
            Updates = [new UpdateQuery { Type = DataType.StatusFlag, Values = values }],
        };
        using var serialized = new MemoryStream();
        SerializeTypedUpdateProto(serialized, proto);
        return serialized.ToArray();
    }
}
