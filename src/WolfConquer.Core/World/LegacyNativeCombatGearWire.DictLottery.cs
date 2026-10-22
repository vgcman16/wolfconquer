#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCombatGearWire
{
    internal static byte[] Encode(LegacyNativeMsgDictLottery message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        using MemoryStream output = new MemoryStream();
        Serialize(output, message);
        return WrapObjectPayload(output, opcode);
    }

    // CQ_COMBAT_GEAR124..130: every scalar is required, including false/zero.
    // List null entries follow the pinned 2.0.0.668 empty-submessage behavior.
    private static void Serialize(Stream output, LegacyNativeMsgDictLottery message)
    {
        if (message is null)
            return;
        Field(output, 1, message.Type);
        Field(output, 2, message.Done);
        if (message.Items2 is null)
            return;
        foreach (LegacyNativeMsgDictLottery.Lottery item in message.Items2)
        {
            using MemoryStream nested = new MemoryStream();
            if (item is not null)
            {
                Field(nested, 1, item.Type);
                Field(nested, 2, item.Exp ? 1u : 0u);
            }
            Nested(output, 3, nested);
        }
    }
}
