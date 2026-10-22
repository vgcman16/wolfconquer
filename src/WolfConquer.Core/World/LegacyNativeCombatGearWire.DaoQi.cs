#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCombatGearWire
{
    internal static byte[] Encode(LegacyNativeMsgDaoQi message, ushort opcode)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return null;
        using MemoryStream output = new MemoryStream();
        Serialize(output, message);
        return WrapObjectPayload(output, opcode);
    }

    // Complete CQ_COMBAT_GEAR93..109 registered contract. The pinned serializer sorts tags,
    // although the native registration lists MasteryPoints7 after Data8/9/10.
    private static void Serialize(Stream output, LegacyNativeMsgDaoQi message)
    {
        if (message is null)
            return;
        Field(output, 1, unchecked((ulong)(long)(int)message.Type));
        Field(output, 2, message.ItemID);
        Field(output, 3, message.TaoistReiki);
        Field(output, 4, message.LOTTERY);
        Field(output, 5, message.Data5);
        Field(output, 6, message.Data6);
        Field(output, 7, message.MasteryPoints);
        Field(output, 8, message.Data8);
        Field(output, 9, message.PirateExp);
        Field(output, 10, message.Data10);
        Field(output, 11, message.Position);
        Field(output, 12, message.Done);
        if (message.Items is not null)
        {
            foreach (LegacyNativeMsgDaoQi.Item item in message.Items)
            {
                using MemoryStream nested = new MemoryStream();
                // Native LIST entries emit an empty submessage when null, unlike ARRAY rows.
                if (item is not null)
                    Field(nested, 1, item.Id);
                Nested(output, 13, nested);
            }
        }
        if (message.Positions is not null)
        {
            foreach (uint position in message.Positions)
                Field(output, 14, position); // Unpacked repeated uint32, including zero values.
        }
    }
}
