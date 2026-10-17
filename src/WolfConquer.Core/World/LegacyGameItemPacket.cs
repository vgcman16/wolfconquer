using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyGameItemPacket(
    uint Uid,
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
    uint RemainingTime,
    uint TimeLeftInMinutes,
    ushort StackSize,
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
    private const int ItemIdOffset = UidOffset + sizeof(uint);
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
    private const int Reserved35Offset = SuspiciousOffset + sizeof(byte);
    private const int LockedOffset = Reserved35Offset + sizeof(byte);
    private const int Reserved37Offset = LockedOffset + sizeof(byte);
    private const int PlusProgressOffset = Reserved37Offset + sizeof(byte);
    private const int RemainingTimeOffset = PlusProgressOffset + sizeof(uint);
    private const int TimeLeftInMinutesOffset = RemainingTimeOffset + sizeof(uint);
    private const int StackSizeOffset = TimeLeftInMinutesOffset + sizeof(uint);
    private const int PerfectionLevelOffset = StackSizeOffset + sizeof(ushort);
    private const int PerfectionProgressOffset = PerfectionLevelOffset + sizeof(uint);
    private const int OwnerUidOffset = PerfectionProgressOffset + sizeof(uint);
    private const int OwnerNameOffset = OwnerUidOffset + sizeof(uint);
    private const int SignatureOffset = OwnerNameOffset + FixedOwnerNameLength;
    private const int Reserved160Offset = SignatureOffset + FixedSignatureLength;
    private const int RuneExperienceOffset = Reserved160Offset + sizeof(ushort);
    private const int Attribute1Offset = RuneExperienceOffset + sizeof(uint);
    private const int Attribute2Offset = Attribute1Offset + sizeof(uint);
    private const int Attribute3Offset = Attribute2Offset + sizeof(uint);
    private const int Attribute4Offset = Attribute3Offset + sizeof(uint);
    private const int Attribute5Offset = Attribute4Offset + sizeof(uint);
    private const int AnimaItemIdOffset = Attribute5Offset + sizeof(uint);
    private const int Reserved190Offset = AnimaItemIdOffset + sizeof(uint);
    private const int MythSoulOffset = Reserved190Offset + sizeof(uint);
    private const int MythSoulPointsOffset = MythSoulOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyGameItem;
    public const ushort AddItemMode = 1;
    public const ushort UpdateItemMode = 3;
    public const ushort AddItemReturnedMode = 8;
    public const ushort ChatItemMode = 9;
    public const int FixedOwnerNameLength = 32;
    public const int FixedSignatureLength = 64;
    public const int PayloadLength = MythSoulPointsOffset + sizeof(uint);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public const byte NoEffect = 0;
    public const byte RuneEffect = 1;

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

    public static LegacyGameItemPacket CreateChatItem(uint itemId, ushort durability)
    {
        return new LegacyGameItemPacket(
            Uid: 1,
            ItemId: itemId,
            Durability: durability,
            MaximumDurability: durability,
            Mode: ChatItemMode,
            Position: 0,
            SocketProgress: 0,
            SocketOne: 0,
            SocketTwo: 0,
            SpellId: 0,
            Effect: 0,
            Plus: 0,
            Bless: 0,
            Bound: 0,
            Enchant: 0,
            ProgressGreen: 0,
            Suspicious: 0,
            Locked: 0,
            PlusProgress: 0,
            RemainingTime: 0,
            TimeLeftInMinutes: 0,
            StackSize: 0,
            PerfectionLevel: 0,
            PerfectionProgress: 0,
            OwnerUid: 0,
            OwnerName: string.Empty,
            Signature: string.Empty,
            RuneExperience: 0,
            Attribute1: 0,
            Attribute2: 0,
            Attribute3: 0,
            Attribute4: 0,
            Attribute5: 0,
            AnimaItemId: 0,
            MythSoul: 0,
            MythSoulPoints: 0);
    }

    public static LegacyGameItemPacket FromSnapshotItem(
        PlayerItemSnapshotItem item,
        RuneSpellOverride? runeSpellOverride = null,
        ushort mode = AddItemMode)
    {
        ArgumentNullException.ThrowIfNull(item);

        var spellId = runeSpellOverride?.SpellId ?? 0;
        var effect = runeSpellOverride?.Effect ?? CheckedByte(item.Effect, nameof(item.Effect));

        return new LegacyGameItemPacket(
            item.Uid,
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
            RemainingTime: 0,
            TimeLeftInMinutes: item.Activate == 0 ? item.TimeLeftInMinutes : 0,
            item.StackSize,
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

    public static bool TryParse(PacketFrame frame, out LegacyGameItemPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.Payload[Reserved35Offset] != 0 ||
            reader.Payload[Reserved37Offset] != 0 ||
            reader.ReadUInt16(Reserved160Offset) != 0 ||
            reader.ReadUInt32(Reserved190Offset) != 0)
        {
            return false;
        }

        packet = new LegacyGameItemPacket(
            reader.ReadUInt32(UidOffset),
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
            reader.ReadUInt32(RemainingTimeOffset),
            reader.ReadUInt32(TimeLeftInMinutesOffset),
            reader.ReadUInt16(StackSizeOffset),
            reader.ReadUInt32(PerfectionLevelOffset),
            reader.ReadUInt32(PerfectionProgressOffset),
            reader.ReadUInt32(OwnerUidOffset),
            reader.ReadCString(OwnerNameOffset, FixedOwnerNameLength, Encoding.UTF8),
            reader.ReadCString(SignatureOffset, FixedSignatureLength, Encoding.UTF8),
            reader.ReadUInt32(RuneExperienceOffset),
            reader.ReadUInt32(Attribute1Offset),
            reader.ReadUInt32(Attribute2Offset),
            reader.ReadUInt32(Attribute3Offset),
            reader.ReadUInt32(Attribute4Offset),
            reader.ReadUInt32(Attribute5Offset),
            reader.ReadUInt32(AnimaItemIdOffset),
            reader.ReadUInt32(MythSoulOffset),
            reader.ReadUInt32(MythSoulPointsOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOffset, Uid);
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
        writer.WriteUInt32(RemainingTimeOffset, RemainingTime);
        writer.WriteUInt32(TimeLeftInMinutesOffset, TimeLeftInMinutes);
        writer.WriteUInt16(StackSizeOffset, StackSize);
        writer.WriteUInt32(PerfectionLevelOffset, PerfectionLevel);
        writer.WriteUInt32(PerfectionProgressOffset, PerfectionProgress);
        writer.WriteUInt32(OwnerUidOffset, OwnerUid);
        WriteFixedUtf8(writer.Payload.Slice(OwnerNameOffset, FixedOwnerNameLength), OwnerName);
        WriteFixedUtf8(writer.Payload.Slice(SignatureOffset, FixedSignatureLength), Signature);
        writer.WriteUInt32(RuneExperienceOffset, RuneExperience);
        writer.WriteUInt32(Attribute1Offset, Attribute1);
        writer.WriteUInt32(Attribute2Offset, Attribute2);
        writer.WriteUInt32(Attribute3Offset, Attribute3);
        writer.WriteUInt32(Attribute4Offset, Attribute4);
        writer.WriteUInt32(Attribute5Offset, Attribute5);
        writer.WriteUInt32(AnimaItemIdOffset, AnimaItemId);
        writer.WriteUInt32(MythSoulOffset, MythSoul);
        writer.WriteUInt32(MythSoulPointsOffset, MythSoulPoints);
        return writer.ToArray();
    }

    private static void WriteFixedUtf8(Span<byte> destination, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var count = Math.Min(value.Length, destination.Length);
        bytes.AsSpan(0, Math.Min(bytes.Length, count)).CopyTo(destination);
    }

    private static byte CheckedByte(ushort value, string name)
    {
        if (value > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy item field exceeds byte range.");
        }

        return (byte)value;
    }

    private static byte CheckedByte(uint value, string name)
    {
        if (value > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy item field exceeds byte range.");
        }

        return (byte)value;
    }

    private static bool IsRuneItem(uint itemId)
    {
        var runeGroup = itemId / 10_000;
        return runeGroup is >= 401 and <= 403;
    }
}
