using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyItemExtraPacket(
    IReadOnlyList<LegacyItemExtraPurification> Purifications,
    IReadOnlyList<LegacyItemExtraRefinery> Refineries)
{
    private const int CountOffset = 0;
    private const int EntryItemUidOffset = 0;
    private const int EntryTypeOffset = EntryItemUidOffset + sizeof(uint);
    private const int PurificationItemIdOffset = EntryTypeOffset + sizeof(uint);
    private const int PurificationLevelOffset = PurificationItemIdOffset + sizeof(uint);
    private const int PurificationEffectPercent2Offset = PurificationLevelOffset + sizeof(uint);
    private const int PurificationDurationOffset = PurificationEffectPercent2Offset + sizeof(uint);
    private const int PurificationSecondsLeftOffset = PurificationDurationOffset + sizeof(uint);
    private const int PurificationReservedOffset = PurificationSecondsLeftOffset + sizeof(int);
    private const int RefineryEffectIdOffset = EntryTypeOffset + sizeof(uint);
    private const int RefineryEffectLevelOffset = RefineryEffectIdOffset + sizeof(uint);
    private const int RefineryEffectPercentOffset = RefineryEffectLevelOffset + sizeof(uint);
    private const int RefineryEffectPercent2Offset = RefineryEffectPercentOffset + sizeof(uint);
    private const int RefinerySecondsLeftOffset = RefineryEffectPercent2Offset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyItemExtra;
    public const uint RefinaryAddingType = 2;
    public const uint PermanentRefineryType = 3;
    public const uint PurificationAddingType = 6;
    public const uint StabilizationType = 8;
    public const int PurificationEntryLength = PurificationReservedOffset + sizeof(uint);
    public const int RefineryEntryLength = RefinerySecondsLeftOffset + sizeof(int);

    public int PayloadLength =>
        sizeof(uint) +
        Purifications.Count * PurificationEntryLength +
        Refineries.Count * RefineryEntryLength;

    public static bool TryCreate(PlayerItemSnapshotItem item, DateTime now, out LegacyItemExtraPacket packet)
    {
        ArgumentNullException.ThrowIfNull(item);

        var purifications = new List<LegacyItemExtraPurification>(capacity: 1);
        var refineries = new List<LegacyItemExtraRefinery>(capacity: 1);

        if (TryCreatePurification(item, now, out var purification))
        {
            purifications.Add(purification);
        }

        if (TryCreateRefinery(item, now, out var refinery))
        {
            refineries.Add(refinery);
        }

        packet = new LegacyItemExtraPacket(purifications, refineries);
        return purifications.Count + refineries.Count > 0;
    }

    public static bool TryParse(PacketFrame frame, out LegacyItemExtraPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < sizeof(uint) ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var expectedCount = reader.ReadUInt32(CountOffset);
        var offset = sizeof(uint);
        var purifications = new List<LegacyItemExtraPurification>();
        var refineries = new List<LegacyItemExtraRefinery>();

        for (var i = 0; i < expectedCount; i++)
        {
            if (offset > reader.Payload.Length - sizeof(uint) * 2)
            {
                return false;
            }

            var type = reader.ReadUInt32(offset + EntryTypeOffset);
            if (IsPurificationType(type))
            {
                if (offset > reader.Payload.Length - PurificationEntryLength ||
                    reader.ReadUInt32(offset + PurificationReservedOffset) != 0)
                {
                    return false;
                }

                purifications.Add(new LegacyItemExtraPurification(
                    reader.ReadUInt32(offset + EntryItemUidOffset),
                    type,
                    reader.ReadUInt32(offset + PurificationItemIdOffset),
                    reader.ReadUInt32(offset + PurificationLevelOffset),
                    reader.ReadUInt32(offset + PurificationEffectPercent2Offset),
                    reader.ReadUInt32(offset + PurificationDurationOffset),
                    reader.ReadInt32(offset + PurificationSecondsLeftOffset)));
                offset += PurificationEntryLength;
            }
            else if (IsRefineryType(type))
            {
                if (offset > reader.Payload.Length - RefineryEntryLength)
                {
                    return false;
                }

                refineries.Add(new LegacyItemExtraRefinery(
                    reader.ReadUInt32(offset + EntryItemUidOffset),
                    type,
                    reader.ReadUInt32(offset + RefineryEffectIdOffset),
                    reader.ReadUInt32(offset + RefineryEffectLevelOffset),
                    reader.ReadUInt32(offset + RefineryEffectPercentOffset),
                    reader.ReadUInt32(offset + RefineryEffectPercent2Offset),
                    reader.ReadInt32(offset + RefinerySecondsLeftOffset)));
                offset += RefineryEntryLength;
            }
            else
            {
                return false;
            }
        }

        if (offset != reader.Payload.Length)
        {
            return false;
        }

        packet = new LegacyItemExtraPacket(purifications, refineries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(CountOffset, checked((uint)(Purifications.Count + Refineries.Count)));
        var offset = sizeof(uint);

        foreach (var purification in Purifications)
        {
            writer.WriteUInt32(offset + EntryItemUidOffset, purification.ItemUid);
            writer.WriteUInt32(offset + EntryTypeOffset, purification.Type);
            writer.WriteUInt32(offset + PurificationItemIdOffset, purification.PurificationItemId);
            writer.WriteUInt32(offset + PurificationLevelOffset, purification.PurificationLevel);
            writer.WriteUInt32(offset + PurificationEffectPercent2Offset, purification.EffectPercent2);
            writer.WriteUInt32(offset + PurificationDurationOffset, purification.Duration);
            writer.WriteInt32(offset + PurificationSecondsLeftOffset, purification.SecondsLeft);
            offset += PurificationEntryLength;
        }

        foreach (var refinery in Refineries)
        {
            writer.WriteUInt32(offset + EntryItemUidOffset, refinery.ItemUid);
            writer.WriteUInt32(offset + EntryTypeOffset, refinery.Type);
            writer.WriteUInt32(offset + RefineryEffectIdOffset, refinery.EffectId);
            writer.WriteUInt32(offset + RefineryEffectLevelOffset, refinery.EffectLevel);
            writer.WriteUInt32(offset + RefineryEffectPercentOffset, refinery.EffectPercent);
            writer.WriteUInt32(offset + RefineryEffectPercent2Offset, refinery.EffectPercent2);
            writer.WriteInt32(offset + RefinerySecondsLeftOffset, refinery.SecondsLeft);
            offset += RefineryEntryLength;
        }

        return writer.ToArray();
    }

    private static bool TryCreatePurification(
        PlayerItemSnapshotItem item,
        DateTime now,
        out LegacyItemExtraPurification purification)
    {
        purification = default;

        if (item.PurificationDuration == 0)
        {
            if (item.PurificationItemId <= 1000)
            {
                return false;
            }

            purification = new LegacyItemExtraPurification(
                item.Uid,
                StabilizationType,
                item.PurificationItemId,
                item.PurificationLevel,
                EffectPercent2: 0,
                Duration: 0,
                SecondsLeft: 0);
            return true;
        }

        if (!TryCalculateSecondsLeft(item.PurificationAddedOn, item.PurificationDuration, now, out var secondsLeft))
        {
            return false;
        }

        purification = new LegacyItemExtraPurification(
            item.Uid,
            PurificationAddingType,
            item.PurificationItemId,
            item.PurificationLevel,
            EffectPercent2: 0,
            item.PurificationDuration,
            secondsLeft);
        return true;
    }

    private static bool TryCreateRefinery(
        PlayerItemSnapshotItem item,
        DateTime now,
        out LegacyItemExtraRefinery refinery)
    {
        refinery = default;

        if (item.RefineryEffectDuration == 0)
        {
            if (item.RefineryEffectId <= 1000)
            {
                return false;
            }

            refinery = new LegacyItemExtraRefinery(
                item.Uid,
                PermanentRefineryType,
                MapClientRefineryEffect(item.RefineryEffectId),
                item.RefineryEffectLevel,
                item.RefineryEffectPercent,
                item.RefineryEffectPercent2,
                SecondsLeft: 0);
            return true;
        }

        if (!TryCalculateSecondsLeft(item.RefineryEffectAddedOn, item.RefineryEffectDuration, now, out var secondsLeft))
        {
            return false;
        }

        refinery = new LegacyItemExtraRefinery(
            item.Uid,
            RefinaryAddingType,
            MapClientRefineryEffect(item.RefineryEffectId),
            item.RefineryEffectLevel,
            item.RefineryEffectPercent,
            item.RefineryEffectPercent2,
            secondsLeft);
        return true;
    }

    private static bool TryCalculateSecondsLeft(long addedOn, uint duration, DateTime now, out int secondsLeft)
    {
        secondsLeft = 0;

        try
        {
            var expires = DateTime.FromBinary(addedOn).AddSeconds(duration);
            var totalSeconds = (int)(expires - now).TotalSeconds;
            if (totalSeconds <= 0)
            {
                return false;
            }

            secondsLeft = totalSeconds;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static uint MapClientRefineryEffect(uint effectId)
    {
        return effectId is >= 724440 and <= 724444 or 3004140 or >= 3006165 and <= 3006170
            ? 301
            : effectId;
    }

    private static bool IsPurificationType(uint type)
    {
        return type is 5 or PurificationAddingType or StabilizationType or 9;
    }

    private static bool IsRefineryType(uint type)
    {
        return type is RefinaryAddingType or PermanentRefineryType or 4;
    }
}

public readonly record struct LegacyItemExtraPurification(
    uint ItemUid,
    uint Type,
    uint PurificationItemId,
    uint PurificationLevel,
    uint EffectPercent2,
    uint Duration,
    int SecondsLeft);

public readonly record struct LegacyItemExtraRefinery(
    uint ItemUid,
    uint Type,
    uint EffectId,
    uint EffectLevel,
    uint EffectPercent,
    uint EffectPercent2,
    int SecondsLeft);
