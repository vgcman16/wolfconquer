#nullable disable
using ProtoBuf;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCombatGearWire
{
    // Native Packet.TQServer(object, ushort). The gate precedes serializer/model/null
    // handling; genuine pinned-library behavior and failures are not approximated.
    internal static byte[] EncodeObject(object message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        using MemoryStream output = new MemoryStream();
        Serializer.Serialize((Stream)output, message); // Statically object, as in native Packet.
        return WrapObjectPayload(output, opcode);
    }
}
