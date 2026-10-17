using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyItemViewActionMode : ushort
{
    Gold = 1,
    CPs = 3,
    ViewEquip = 4,
    Rune = 7,
}

public sealed record LegacyItemViewPacket(
    uint Uid,
    uint PlayerUid,
    long Cost,
    uint ItemId,
    ushort Durability,
    ushort MaximumDurability,
    ushort Mode,
    byte Position,
    uint SocketProgress,
    byte SocketOne,
    byte SocketTwo,
    uint SpellId,
    byte Effect,
    byte Plus,
    byte Bless,
    byte Bound,
    byte Enchant,
    uint ProgressGreen,
    byte Suspicious,
    byte Locked,
    uint PlusProgress,
    uint Activate,
    uint RemainingTime,
    ushort StackSize,
    uint PurificationItemId,
    uint PerfectionLevel,
    uint PerfectionProgress,
    uint OwnerUid,
    string OwnerName,
    string Signature,
    uint RuneExperience,
    uint Attribute1,
    uint Attribute2,
    uint Attribute3,
    uint Attribute4,
    uint Attribute5,
    uint AnimaItemId,
    uint MythSoul,
    uint MythSoulPoints)
{
    private const int UidOffset = 0;
    private const int PlayerUidOffset = UidOffset + sizeof(uint);
    private const int CostOffset = PlayerUidOffset + sizeof(uint);
    private const int ItemIdOffset = CostOffset + sizeof(long);
    private const int DurabilityOffset = ItemIdOffset + sizeof(uint);
    private const int MaximumDurabilityOffset = DurabilityOffset + sizeof(ushort);
    private const int ModeOffset = MaximumDurabilityOffset + sizeof(ushort);
    private const int PositionOffset = ModeOffset + sizeof(ushort);
    private const int SocketProgressOffset = PositionOffset + sizeof(byte);
    private const int SocketOneOffset = SocketProgressOffset + sizeof(uint);
    private const int SocketTwoOffset = SocketOneOffset + sizeof(byte);
    private const int SpellIdOffset = SocketTwoOffset + sizeof(byte);
    private const int EffectOffset = SpellIdOffset + sizeof(uint);
    private const int PlusOffset = EffectOffset + sizeof(byte);
    private const int BlessOffset = PlusOffset + sizeof(byte);
    private const int BoundOffset = BlessOffset + sizeof(byte);
    private const int EnchantOffset = BoundOffset + sizeof(byte);
    private const int ProgressGreenOffset = EnchantOffset + sizeof(byte);
    private const int SuspiciousOffset = ProgressGreenOffset + sizeof(uint);
    private const int Reserved47Offset = SuspiciousOffset + sizeof(byte);
    private const int LockedOffset = Reserved47Offset + sizeof(byte);
    private const int Reserved49Offset = LockedOffset + sizeof(byte);
    private const int PlusProgressOffset = Reserved49Offset + sizeof(byte);
    private const int ConditionalTimeOffset = PlusProgressOffset + sizeof(uint);

    private const int StackSizeTailOffset = 0;
    private const int PurificationItemIdTailOffset = StackSizeTailOffset + sizeof(ushort);
    private const int PurificationItemIdRepeatTailOffset = PurificationItemIdTailOffset + sizeof(uint);
    private const int PerfectionLevelTailOffset = PurificationItemIdRepeatTailOffset + sizeof(uint);
    private const int PerfectionProgressTailOffset = PerfectionLevelTailOffset + sizeof(uint);
    private const int OwnerUidTailOffset = PerfectionProgressTailOffset + sizeof(uint);
    private const int OwnerNameTailOffset = OwnerUidTailOffset + sizeof(uint);
    private const int SignatureTailOffset = OwnerNameTailOffset + FixedOwnerNameLength;
    private const int RuneExperienceTailOffset = SignatureTailOffset + FixedSignatureLength;
    private const int Attribute1TailOffset = RuneExperienceTailOffset + sizeof(uint);
    private const int Attribute2TailOffset = Attribute1TailOffset + sizeof(uint);
    private const int Attribute3TailOffset = Attribute2TailOffset + sizeof(uint);
    private const int Attribute4TailOffset = Attribute3TailOffset + sizeof(uint);
    private const int Attribute5TailOffset = Attribute4TailOffset + sizeof(uint);
    private const int AnimaItemIdTailOffset = Attribute5TailOffset + sizeof(uint);
    private const int ReservedAnimaTailOffset = AnimaItemIdTailOffset + sizeof(uint);
    private const int MythSoulTailOffset = ReservedAnimaTailOffset + sizeof(uint);
    private const int MythSoulPointsTailOffset = MythSoulTailOffset + sizeof(uint);
    private const int TailLength = MythSoulPointsTailOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyItemView;
    public const ushort GoldMode = (ushort)LegacyItemViewActionMode.Gold;
    public const ushort CPsMode = (ushort)LegacyItemViewActionMode.CPs;
    public const ushort ViewEquipMode = (ushort)LegacyItemViewActionMode.ViewEquip;
    public const ushort RuneMode = (ushort)LegacyItemViewActionMode.Rune;
    public const int FixedOwnerNameLength = 32;
    public const int FixedSignatureLength = 64;
    public const int PayloadLengthWithoutActivate = ConditionalTimeOffset + sizeof(uint) + TailLength;
    public const int PayloadLengthWithActivate = ConditionalTimeOffset + sizeof(uint) + sizeof(uint) + TailLength;
    public const int FrameLengthWithoutActivate = PacketHeader.Size + PayloadLengthWithoutActivate;
    public const int FrameLengthWithActivate = PacketHeader.Size + PayloadLengthWithActivate;

    public const byte NoEffect = LegacyGameItemPacket.NoEffect;
    public const byte RuneEffect = LegacyGameItemPacket.RuneEffect;

    public readonly record struct RuneSpellOverride(uint SpellId, byte Effect);

    public static RuneSpellOverride? CreateRuneSpellOverride(uint itemId, uint runeSpellId)
    {
        if (!IsRuneItem(itemId))
        {
            return null;
        }

        return runeSpellId == itemId
            ? new RuneSpellOverride(runeSpellId - runeSpellId % 100 + 1, RuneEffect)
            : new RuneSpellOverride(runeSpellId, NoEffect);
    }

    public static RuneSpellOverride? CreateRuneSpellOverride(
        PlayerItemSnapshotItem item,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (itemsById is null || item.ItemId > int.MaxValue)
        {
            return null;
        }

        return itemsById.TryGetValue((int)item.ItemId, out var itemTypes)
            ? CreateRuneSpellOverride(item.ItemId, itemTypes.FirstOrDefault()?.RuneSpellId ?? 0)
            : null;
    }

    public static LegacyItemViewPacket FromSnapshotItem(
        uint playerUid,
        long cost,
        PlayerItemSnapshotItem item,
        ushort mode,
        RuneSpellOverride? runeSpellOverride = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        var spellId = runeSpellOverride?.SpellId ?? 0;
        var effect = runeSpellOverride?.Effect ?? CheckedByte(item.Effect, nameof(item.Effect));

        return new LegacyItemViewPacket(
            item.Uid,
            playerUid,
            cost,
            item.ItemId,
            item.Durability,
            item.MaximumDurability,
            mode,
            CheckedByte(item.Position, nameof(item.Position)),
            item.SocketProgress,
            item.SocketOne,
            item.SocketTwo,
            spellId,
            effect,
            item.Plus,
            item.Bless,
            item.Bound,
            item.Enchant,
            ProgressGreen: 0,
            item.Suspicious,
            item.Locked,
            item.PlusProgress,
            item.Activate,
            RemainingTime: 0,
            item.StackSize,
            item.PurificationItemId,
            item.PerfectionLevel,
            item.PerfectionProgress,
            item.OwnerUid,
            item.OwnerName,
            item.Signature,
            item.RuneExperience,
            item.Attribute1,
            item.Attribute2,
            item.Attribute3,
            item.Attribute4,
            item.Attribute5,
            item.AnimaItemId,
            item.MythSoul,
            item.MythSoulPoints);
    }

    public static LegacyItemViewPacket FromSnapshotItem(
        uint playerUid,
        long cost,
        PlayerItemSnapshotItem item,
        LegacyItemViewActionMode mode,
        RuneSpellOverride? runeSpellOverride = null)
    {
        return FromSnapshotItem(playerUid, cost, item, (ushort)mode, runeSpellOverride);
    }

    public static LegacyItemViewPacket FromSnapshotItem(
        uint playerUid,
        long cost,
        PlayerItemSnapshotItem item,
        LegacyItemViewActionMode mode,
        LegacyGameItemPacket.RuneSpellOverride? runeSpellOverride)
    {
        return FromSnapshotItem(playerUid, cost, item, (ushort)mode, ConvertRuneSpellOverride(runeSpellOverride));
    }

    public static LegacyItemViewPacket Create(
        uint playerUid,
        uint monsterUid,
        PlayerItemSnapshotItem item,
        LegacyItemViewActionMode mode,
        LegacyGameItemPacket.RuneSpellOverride? runeSpellOverride = null)
    {
        return FromSnapshotItem(playerUid, monsterUid, item, mode, runeSpellOverride);
    }

    public static bool TryParse(PacketFrame frame, out LegacyItemViewPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.Payload.Length is not PayloadLengthWithActivate and not PayloadLengthWithoutActivate ||
            reader.Payload[Reserved47Offset] != 0 ||
            reader.Payload[Reserved49Offset] != 0)
        {
            return false;
        }

        var includesActivate = reader.Payload.Length == PayloadLengthWithActivate;
        var remainingTimeOffset = GetRemainingTimeOffset(includesActivate);
        var remainingTime = reader.ReadUInt32(remainingTimeOffset);
        if (includesActivate != (remainingTime == 0))
        {
            return false;
        }

        var tailOffset = GetTailOffset(includesActivate);
        var purificationItemId = reader.ReadUInt32(tailOffset + PurificationItemIdTailOffset);
        if (reader.ReadUInt32(tailOffset + PurificationItemIdRepeatTailOffset) != purificationItemId ||
            reader.ReadUInt32(tailOffset + ReservedAnimaTailOffset) != 0)
        {
            return false;
        }

        packet = new LegacyItemViewPacket(
            reader.ReadUInt32(UidOffset),
            reader.ReadUInt32(PlayerUidOffset),
            reader.ReadInt64(CostOffset),
            reader.ReadUInt32(ItemIdOffset),
            reader.ReadUInt16(DurabilityOffset),
            reader.ReadUInt16(MaximumDurabilityOffset),
            reader.ReadUInt16(ModeOffset),
            reader.Payload[PositionOffset],
            reader.ReadUInt32(SocketProgressOffset),
            reader.Payload[SocketOneOffset],
            reader.Payload[SocketTwoOffset],
            reader.ReadUInt32(SpellIdOffset),
            reader.Payload[EffectOffset],
            reader.Payload[PlusOffset],
            reader.Payload[BlessOffset],
            reader.Payload[BoundOffset],
            reader.Payload[EnchantOffset],
            reader.ReadUInt32(ProgressGreenOffset),
            reader.Payload[SuspiciousOffset],
            reader.Payload[LockedOffset],
            reader.ReadUInt32(PlusProgressOffset),
            includesActivate ? reader.ReadUInt32(ConditionalTimeOffset) : 0,
            remainingTime,
            reader.ReadUInt16(tailOffset + StackSizeTailOffset),
            purificationItemId,
            reader.ReadUInt32(tailOffset + PerfectionLevelTailOffset),
            reader.ReadUInt32(tailOffset + PerfectionProgressTailOffset),
            reader.ReadUInt32(tailOffset + OwnerUidTailOffset),
            reader.ReadCString(tailOffset + OwnerNameTailOffset, FixedOwnerNameLength, Encoding.UTF8),
            reader.ReadCString(tailOffset + SignatureTailOffset, FixedSignatureLength, Encoding.UTF8),
            reader.ReadUInt32(tailOffset + RuneExperienceTailOffset),
            reader.ReadUInt32(tailOffset + Attribute1TailOffset),
            reader.ReadUInt32(tailOffset + Attribute2TailOffset),
            reader.ReadUInt32(tailOffset + Attribute3TailOffset),
            reader.ReadUInt32(tailOffset + Attribute4TailOffset),
            reader.ReadUInt32(tailOffset + Attribute5TailOffset),
            reader.ReadUInt32(tailOffset + AnimaItemIdTailOffset),
            reader.ReadUInt32(tailOffset + MythSoulTailOffset),
            reader.ReadUInt32(tailOffset + MythSoulPointsTailOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var includesActivate = RemainingTime == 0;
        var writer = new PacketWriter(Opcode, GetPayloadLength(includesActivate));
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(PlayerUidOffset, PlayerUid);
        writer.WriteInt64(CostOffset, Cost);
        writer.WriteUInt32(ItemIdOffset, ItemId);
        writer.WriteUInt16(DurabilityOffset, Durability);
        writer.WriteUInt16(MaximumDurabilityOffset, MaximumDurability);
        writer.WriteUInt16(ModeOffset, Mode);
        writer.Payload[PositionOffset] = Position;
        writer.WriteUInt32(SocketProgressOffset, SocketProgress);
        writer.Payload[SocketOneOffset] = SocketOne;
        writer.Payload[SocketTwoOffset] = SocketTwo;
        writer.WriteUInt32(SpellIdOffset, SpellId);
        writer.Payload[EffectOffset] = Effect;
        writer.Payload[PlusOffset] = Plus;
        writer.Payload[BlessOffset] = Bless;
        writer.Payload[BoundOffset] = Bound;
        writer.Payload[EnchantOffset] = Enchant;
        writer.WriteUInt32(ProgressGreenOffset, ProgressGreen);
        writer.Payload[SuspiciousOffset] = Suspicious;
        writer.Payload[LockedOffset] = Locked;
        writer.WriteUInt32(PlusProgressOffset, PlusProgress);
        if (includesActivate)
        {
            writer.WriteUInt32(ConditionalTimeOffset, Activate);
        }

        writer.WriteUInt32(GetRemainingTimeOffset(includesActivate), RemainingTime);

        var tailOffset = GetTailOffset(includesActivate);
        writer.WriteUInt16(tailOffset + StackSizeTailOffset, StackSize);
        writer.WriteUInt32(tailOffset + PurificationItemIdTailOffset, PurificationItemId);
        writer.WriteUInt32(tailOffset + PurificationItemIdRepeatTailOffset, PurificationItemId);
        writer.WriteUInt32(tailOffset + PerfectionLevelTailOffset, PerfectionLevel);
        writer.WriteUInt32(tailOffset + PerfectionProgressTailOffset, PerfectionProgress);
        writer.WriteUInt32(tailOffset + OwnerUidTailOffset, OwnerUid);
        WriteFixedUtf8(writer.Payload.Slice(tailOffset + OwnerNameTailOffset, FixedOwnerNameLength), OwnerName);
        WriteFixedUtf8(writer.Payload.Slice(tailOffset + SignatureTailOffset, FixedSignatureLength), Signature);
        writer.WriteUInt32(tailOffset + RuneExperienceTailOffset, RuneExperience);
        writer.WriteUInt32(tailOffset + Attribute1TailOffset, Attribute1);
        writer.WriteUInt32(tailOffset + Attribute2TailOffset, Attribute2);
        writer.WriteUInt32(tailOffset + Attribute3TailOffset, Attribute3);
        writer.WriteUInt32(tailOffset + Attribute4TailOffset, Attribute4);
        writer.WriteUInt32(tailOffset + Attribute5TailOffset, Attribute5);
        writer.WriteUInt32(tailOffset + AnimaItemIdTailOffset, AnimaItemId);
        writer.WriteUInt32(tailOffset + MythSoulTailOffset, MythSoul);
        writer.WriteUInt32(tailOffset + MythSoulPointsTailOffset, MythSoulPoints);
        return writer.ToArray();
    }

    private static int GetPayloadLength(bool includesActivate)
    {
        return includesActivate ? PayloadLengthWithActivate : PayloadLengthWithoutActivate;
    }

    private static int GetRemainingTimeOffset(bool includesActivate)
    {
        return includesActivate ? ConditionalTimeOffset + sizeof(uint) : ConditionalTimeOffset;
    }

    private static int GetTailOffset(bool includesActivate)
    {
        return GetRemainingTimeOffset(includesActivate) + sizeof(uint);
    }

    private static void WriteFixedUtf8(Span<byte> destination, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }

    private static RuneSpellOverride? ConvertRuneSpellOverride(LegacyGameItemPacket.RuneSpellOverride? runeSpellOverride)
    {
        return runeSpellOverride is { } value
            ? new RuneSpellOverride(value.SpellId, value.Effect)
            : null;
    }

    private static byte CheckedByte(ushort value, string name)
    {
        if (value > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy item-view field exceeds byte range.");
        }

        return (byte)value;
    }

    private static byte CheckedByte(uint value, string name)
    {
        if (value > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy item-view field exceeds byte range.");
        }

        return (byte)value;
    }

    private static bool IsRuneItem(uint itemId)
    {
        var runeGroup = itemId / 10_000;
        return runeGroup is >= 401 and <= 403;
    }
}
