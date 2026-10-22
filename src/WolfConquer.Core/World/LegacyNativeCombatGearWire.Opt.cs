#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCombatGearWire
{
    internal static byte[] Encode(LegacyNativeMsgCombatGearOpt message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        using MemoryStream output = new MemoryStream();
        Serialize(output, message);
        return WrapObjectPayload(output, opcode);
    }

    // CQ_COMBAT_GEAR Opt contracts: required scalars, unpacked U and a message ARRAY.
    private static void Serialize(Stream output, LegacyNativeMsgCombatGearOpt message)
    {
        if (message is null)
            return;
        Field(output, 1, unchecked((ulong)(long)(int)message.Type));
        Field(output, 2, unchecked((ulong)(long)(int)(uint)message.ItemID));
        Field(output, 3, message.Transform);
        Field(output, 4, message.UID);
        Field(output, 5, message.Exp);
        if (message.U is not null)
        {
            foreach (uint value in message.U)
                Field(output, 6, value);
        }
        if (message.SelectMaterial is null)
            return;
        foreach (LegacyNativeMsgCombatGearOpt.Items item in message.SelectMaterial)
        {
            if (item is null)
                continue; // Pinned array behavior differs from null LIST entries.
            using MemoryStream nested = new MemoryStream();
            Field(nested, 1, item.UID);
            Field(nested, 2, item.Size);
            Nested(output, 7, nested);
        }
    }
}
