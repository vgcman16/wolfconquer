using WolfConquer.Core.Packets;
using MsgItemExtra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Core.World;

// MsgItemExtra.CreateArray over its real mutable value types, not snapshot DTOs.
// PacketWriter provides the frame header; the connection owns any transport seal.
internal static class LegacyNativeItemExtraFrame
{
    internal static byte[] CreateArray(
        IReadOnlyList<MsgItemExtra.Purification> purifications,
        IReadOnlyList<MsgItemExtra.Refinery> refinerys,
        bool tointer = false)
    {
        const int purificationEntryLength = 32;
        const int refineryEntryLength = 28;
        int payloadLength = checked(sizeof(uint) +
            purifications.Count * purificationEntryLength +
            refinerys.Count * refineryEntryLength);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyItemExtra, payloadLength);
        writer.WriteUInt32(0, (uint)(refinerys.Count + purifications.Count));
        int offset = sizeof(uint);

        foreach (MsgItemExtra.Purification purification in purifications)
        {
            writer.WriteUInt32(offset, purification.ItemUID);
            writer.WriteUInt32(offset + 4, (uint)purification.Typ);
            writer.WriteUInt32(offset + 8, purification.PurificationItemID);
            writer.WriteUInt32(offset + 12, purification.PurificationLevel);
            writer.WriteUInt32(offset + 16, purification.EffectPercent2);
            writer.WriteUInt32(offset + 20, purification.PurificationDuration);
            writer.WriteInt32(offset + 24, purification.SecoundsLeft);
            writer.WriteUInt32(offset + 28, 0u);
            offset += purificationEntryLength;
        }

        foreach (MsgItemExtra.Refinery refinery in refinerys)
        {
            writer.WriteUInt32(offset, refinery.ItemUID);
            writer.WriteUInt32(offset + 4, (uint)refinery.Typ);
            uint effectId = refinery.EffectID;
            if (!tointer &&
                ((effectId >= 724440 && effectId <= 724444) ||
                 effectId == 3004140 ||
                 (effectId >= 3006165 && effectId <= 3006170)))
            {
                effectId = 301u;
            }

            writer.WriteUInt32(offset + 8, effectId);
            writer.WriteUInt32(offset + 12, refinery.EffectLevel);
            writer.WriteUInt32(offset + 16, refinery.EffectPercent);
            writer.WriteUInt32(offset + 20, refinery.EffectPercent2);
            writer.WriteInt32(offset + 24, refinery.SecoundsLeft);
            offset += refineryEntryLength;
        }

        return writer.ToArray();
    }
}
