#nullable disable
using ProtoBuf;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCombatGearWire
{
    // Native Packet.TQServer(object, ushort). The inactive gate precedes required
    // state/model checks; valid messages retain actual pinned-library serialization.
    internal static byte[] EncodeObject(object message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        ArgumentNullException.ThrowIfNull(message);
        using MemoryStream output = new MemoryStream();
        Serializer.Serialize((Stream)output, message); // Statically object, as in native Packet.
        return WrapObjectPayload(output, opcode);
    }
}
