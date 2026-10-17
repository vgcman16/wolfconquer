using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyItemRefineRecordPacket(
    uint Type,
    uint Points,
    uint Unknown,
    uint Unlocked)
{
    private const int TypeField = 1;
    private const int PointsField = 2;
    private const int UnknownField = 3;
    private const int UnlockedField = 4;

    public const ushort Opcode = WorldPacketOpcodes.LegacyItemRefineRecord;

    public static LegacyItemRefineRecordPacket DefaultResponse()
    {
        return new LegacyItemRefineRecordPacket(Type: 0, Points: 54, Unknown: 0, Unlocked: 0);
    }

    public static bool TryParse(PacketFrame frame, out LegacyItemRefineRecordPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var type = 0U;
        var points = 0U;
        var unknown = 0U;
        var unlocked = 0U;
        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            if (wireType != LegacyProtoWire.VarintWireType)
            {
                if (!reader.TrySkipField(wireType))
                {
                    return false;
                }

                continue;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (!reader.TryReadUInt32(out type))
                    {
                        return false;
                    }

                    break;
                case PointsField:
                    if (!reader.TryReadUInt32(out points))
                    {
                        return false;
                    }

                    break;
                case UnknownField:
                    if (!reader.TryReadUInt32(out unknown))
                    {
                        return false;
                    }

                    break;
                case UnlockedField:
                    if (!reader.TryReadUInt32(out unlocked))
                    {
                        return false;
                    }

                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        packet = new LegacyItemRefineRecordPacket(type, points, unknown, unlocked);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, TypeField, Type);
        LegacyProtoWire.WriteVarintField(payload, PointsField, Points);
        LegacyProtoWire.WriteVarintField(payload, UnknownField, Unknown);
        if (Unlocked != 0)
        {
            LegacyProtoWire.WriteVarintField(payload, UnlockedField, Unlocked);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }
}

public sealed record LegacyRankMemberShowPacket(
    uint Type,
    IReadOnlyList<LegacyRankMemberShowEntry> Items)
{
    private const int TypeField = 1;
    private const int ItemsField = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyRankMemberShow;
    public const uint ViewerType = 1;

    public static LegacyRankMemberShowPacket EmptyResponse(uint type)
    {
        return new LegacyRankMemberShowPacket(
            type,
            type == ViewerType ? [LegacyRankMemberShowEntry.Empty] : []);
    }

    public static bool TryParse(PacketFrame frame, out LegacyRankMemberShowPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var type = 0U;
        var items = new List<LegacyRankMemberShowEntry>();
        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out type))
                    {
                        return false;
                    }

                    break;
                case ItemsField:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var itemPayload) ||
                        !TryParseEntry(itemPayload, out var item))
                    {
                        return false;
                    }

                    items.Add(item);
                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        packet = new LegacyRankMemberShowPacket(type, items);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Items);

        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, TypeField, Type);
        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            LegacyProtoWire.WriteVarintField(itemPayload, 1, item.Type);
            LegacyProtoWire.WriteVarintField(itemPayload, 2, item.Rank);
            LegacyProtoWire.WriteVarintField(itemPayload, 3, item.EntityUid);
            LegacyProtoWire.WriteStringField(itemPayload, 4, item.Name);
            LegacyProtoWire.WriteStringField(itemPayload, 5, item.GuildName);
            LegacyProtoWire.WriteVarintField(itemPayload, 6, item.Mesh);
            LegacyProtoWire.WriteVarintField(itemPayload, 7, item.HairStyle);
            LegacyProtoWire.WriteVarintField(itemPayload, 8, item.Head);
            LegacyProtoWire.WriteVarintField(itemPayload, 9, item.Garment);
            LegacyProtoWire.WriteVarintField(itemPayload, 10, item.LeftWeapon);
            LegacyProtoWire.WriteVarintField(itemPayload, 11, item.LeftWeaponAccessory);
            LegacyProtoWire.WriteVarintField(itemPayload, 12, item.RightWeapon);
            LegacyProtoWire.WriteVarintField(itemPayload, 13, item.RightWeaponAccessory);
            LegacyProtoWire.WriteVarintField(itemPayload, 14, item.MountArmor);
            LegacyProtoWire.WriteVarintField(itemPayload, 15, item.Armor);
            LegacyProtoWire.WriteVarintField(itemPayload, 16, item.Wing);
            LegacyProtoWire.WriteVarintField(itemPayload, 17, item.WingPlus);
            LegacyProtoWire.WriteVarintField(itemPayload, 18, item.Title);
            LegacyProtoWire.WriteVarintField(itemPayload, 19, item.Flag);
            LegacyProtoWire.WriteVarintField(itemPayload, 20, item.Profession);
            LegacyProtoWire.WriteVarintField(itemPayload, 21, item.WeaponWar);
            LegacyProtoWire.WriteBytesField(payload, ItemsField, itemPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseEntry(ReadOnlySpan<byte> payload, out LegacyRankMemberShowEntry entry)
    {
        entry = LegacyRankMemberShowEntry.Empty;
        var reader = new LegacyProtoWire.Reader(payload);
        var values = new uint[19];
        var name = string.Empty;
        var guildName = string.Empty;
        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            if (fieldNumber == 4)
            {
                if (wireType != LegacyProtoWire.LengthDelimitedWireType || !reader.TryReadString(out name))
                {
                    return false;
                }

                continue;
            }

            if (fieldNumber == 5)
            {
                if (wireType != LegacyProtoWire.LengthDelimitedWireType || !reader.TryReadString(out guildName))
                {
                    return false;
                }

                continue;
            }

            if (fieldNumber is >= 1 and <= 3 or >= 6 and <= 21)
            {
                if (wireType != LegacyProtoWire.VarintWireType ||
                    !reader.TryReadUInt32(out var value))
                {
                    return false;
                }

                var valueIndex = fieldNumber <= 3 ? fieldNumber - 1 : fieldNumber - 3;
                values[valueIndex] = value;
                continue;
            }

            if (!reader.TrySkipField(wireType))
            {
                return false;
            }
        }

        entry = new LegacyRankMemberShowEntry(
            values[0],
            values[1],
            values[2],
            name,
            guildName,
            values[3],
            values[4],
            values[5],
            values[6],
            values[7],
            values[8],
            values[9],
            values[10],
            values[11],
            values[12],
            values[13],
            values[14],
            values[15],
            values[16],
            values[17],
            values[18]);
        return true;
    }
}

public sealed record LegacyRankMemberShowEntry(
    uint Type,
    uint Rank,
    uint EntityUid,
    string Name,
    string GuildName,
    uint Mesh,
    uint HairStyle,
    uint Head,
    uint Garment,
    uint LeftWeapon,
    uint LeftWeaponAccessory,
    uint RightWeapon,
    uint RightWeaponAccessory,
    uint MountArmor,
    uint Armor,
    uint Wing,
    uint WingPlus,
    uint Title,
    uint Flag,
    uint Profession,
    uint WeaponWar)
{
    public static LegacyRankMemberShowEntry Empty { get; } = new(
        Type: 0,
        Rank: 0,
        EntityUid: 0,
        Name: string.Empty,
        GuildName: string.Empty,
        Mesh: 0,
        HairStyle: 0,
        Head: 0,
        Garment: 0,
        LeftWeapon: 0,
        LeftWeaponAccessory: 0,
        RightWeapon: 0,
        RightWeaponAccessory: 0,
        MountArmor: 0,
        Armor: 0,
        Wing: 0,
        WingPlus: 0,
        Title: 0,
        Flag: 0,
        Profession: 0,
        WeaponWar: 0);
}

public enum LegacyInnerPowerAction : byte
{
    InfoStage = 0,
    UpdateGong = 3,
    UnlockStage = 4,
    OpenInner = 5,
    TransferInnerPowers = 6,
}

public readonly record struct LegacyInnerPowerOptPacket(
    LegacyInnerPowerAction Action,
    uint Parameter,
    uint Parameter2)
{
    public const ushort Opcode = WorldPacketOpcodes.LegacyInnerStrengthOpt;
    public const int PayloadLength = sizeof(byte) + sizeof(uint) + sizeof(uint);

    public static bool TryParse(PacketFrame frame, out LegacyInnerPowerOptPacket packet)
    {
        packet = default;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new LegacyInnerPowerOptPacket(
            (LegacyInnerPowerAction)payload[0],
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(1, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(5, sizeof(uint))));
        return true;
    }
}

public readonly record struct LegacyInnerPowerGuiEntry(
    byte Id,
    uint Score);

public readonly record struct LegacyInnerPowerGuiPacket(
    IReadOnlyList<LegacyInnerPowerGuiEntry> Gongs)
{
    private const int CountOffset = 0;
    private const int EntriesOffset = CountOffset + sizeof(ushort);
    private const int EntryLength = sizeof(byte) + sizeof(uint);

    public const ushort Opcode = 2113;

    public byte[] ToFrameBytes()
    {
        if (Gongs.Count > ushort.MaxValue)
        {
            throw new InvalidOperationException("Legacy InnerPowerGui cannot contain more than 65535 gongs.");
        }

        var writer = new PacketWriter(Opcode, checked(EntriesOffset + (Gongs.Count * EntryLength)));
        writer.WriteUInt16(CountOffset, checked((ushort)Gongs.Count));

        var offset = EntriesOffset;
        foreach (var gong in Gongs)
        {
            writer.Payload[offset] = gong.Id;
            writer.WriteUInt32(offset + sizeof(byte), gong.Score);
            offset += EntryLength;
        }

        return writer.ToArray();
    }

    public static bool TryParse(PacketFrame frame, out LegacyInnerPowerGuiPacket packet)
    {
        packet = default;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < EntriesOffset)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CountOffset, sizeof(ushort)));
        var expectedPayloadLength = EntriesOffset + (count * EntryLength);
        if (payload.Length != expectedPayloadLength)
        {
            return false;
        }

        var entries = new LegacyInnerPowerGuiEntry[count];
        var offset = EntriesOffset;
        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = new LegacyInnerPowerGuiEntry(
                payload[offset],
                BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset + sizeof(byte), sizeof(uint))));
            offset += EntryLength;
        }

        packet = new LegacyInnerPowerGuiPacket(entries);
        return true;
    }
}

public enum LegacyInnerPowerStageInfoAction : ushort
{
    UpdateStage = 1,
    UpdateScore = 2,
}

public readonly record struct LegacyInnerPowerStageGongInfo(
    ushort Id,
    byte Level,
    byte Score,
    bool Complete);

public readonly record struct LegacyInnerPowerStageAttributeInfo(
    ushort GongId,
    byte AttributeType,
    uint Value);

public readonly record struct LegacyInnerPowerStageInfoPacket(
    uint CharacterId,
    uint Score,
    LegacyInnerPowerStageInfoAction Action,
    IReadOnlyList<LegacyInnerPowerStageGongInfo> Gongs,
    IReadOnlyList<LegacyInnerPowerStageAttributeInfo> Attributes)
{
    private const int CharacterIdOffset = 0;
    private const int ScoreOffset = CharacterIdOffset + sizeof(uint);
    private const int ReservedOffset = ScoreOffset + sizeof(uint);
    private const int ActionOffset = ReservedOffset + sizeof(uint);
    private const int GongCountOffset = ActionOffset + sizeof(ushort);
    private const int AttributeCountOffset = GongCountOffset + sizeof(ushort);
    private const int EntriesOffset = AttributeCountOffset + sizeof(ushort);
    private const int GongEntryLength = sizeof(ushort) + sizeof(byte) + sizeof(byte) + sizeof(byte);
    private const int AttributeEntryLength = sizeof(ushort) + sizeof(byte) + sizeof(uint);

    public const ushort Opcode = 2147;
    public const int HeaderPayloadLength = EntriesOffset;

    public byte[] ToFrameBytes()
    {
        if (Gongs.Count > ushort.MaxValue)
        {
            throw new InvalidOperationException("Legacy InnerPowerStageInfo cannot contain more than 65535 gongs.");
        }

        if (Attributes.Count > ushort.MaxValue)
        {
            throw new InvalidOperationException("Legacy InnerPowerStageInfo cannot contain more than 65535 attributes.");
        }

        var writer = new PacketWriter(
            Opcode,
            checked(HeaderPayloadLength + (Gongs.Count * GongEntryLength) + (Attributes.Count * AttributeEntryLength)));
        writer.WriteUInt32(CharacterIdOffset, CharacterId);
        writer.WriteUInt32(ScoreOffset, Score);
        writer.WriteUInt32(ReservedOffset, 0);
        writer.WriteUInt16(ActionOffset, (ushort)Action);
        writer.WriteUInt16(GongCountOffset, checked((ushort)Gongs.Count));
        writer.WriteUInt16(AttributeCountOffset, checked((ushort)Attributes.Count));

        var offset = EntriesOffset;
        foreach (var gong in Gongs)
        {
            writer.WriteUInt16(offset, gong.Id);
            writer.Payload[offset + sizeof(ushort)] = gong.Level;
            writer.Payload[offset + sizeof(ushort) + sizeof(byte)] = gong.Score;
            writer.Payload[offset + sizeof(ushort) + sizeof(byte) + sizeof(byte)] = gong.Complete ? (byte)1 : (byte)0;
            offset += GongEntryLength;
        }

        foreach (var attribute in Attributes)
        {
            writer.WriteUInt16(offset, attribute.GongId);
            writer.Payload[offset + sizeof(ushort)] = attribute.AttributeType;
            writer.WriteUInt32(offset + sizeof(ushort) + sizeof(byte), attribute.Value);
            offset += AttributeEntryLength;
        }

        return writer.ToArray();
    }

    public static bool TryParse(PacketFrame frame, out LegacyInnerPowerStageInfoPacket packet)
    {
        packet = default;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < HeaderPayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var gongCount = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(GongCountOffset, sizeof(ushort)));
        var attributeCount = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(AttributeCountOffset, sizeof(ushort)));
        var expectedPayloadLength = HeaderPayloadLength +
            (gongCount * GongEntryLength) +
            (attributeCount * AttributeEntryLength);
        if (payload.Length != expectedPayloadLength)
        {
            return false;
        }

        var gongs = new LegacyInnerPowerStageGongInfo[gongCount];
        var offset = EntriesOffset;
        for (var index = 0; index < gongs.Length; index++)
        {
            gongs[index] = new LegacyInnerPowerStageGongInfo(
                BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, sizeof(ushort))),
                payload[offset + sizeof(ushort)],
                payload[offset + sizeof(ushort) + sizeof(byte)],
                payload[offset + sizeof(ushort) + sizeof(byte) + sizeof(byte)] != 0);
            offset += GongEntryLength;
        }

        var attributes = new LegacyInnerPowerStageAttributeInfo[attributeCount];
        for (var index = 0; index < attributes.Length; index++)
        {
            attributes[index] = new LegacyInnerPowerStageAttributeInfo(
                BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, sizeof(ushort))),
                payload[offset + sizeof(ushort)],
                BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset + sizeof(ushort) + sizeof(byte), sizeof(uint))));
            offset += AttributeEntryLength;
        }

        packet = new LegacyInnerPowerStageInfoPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(CharacterIdOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ScoreOffset, sizeof(uint))),
            (LegacyInnerPowerStageInfoAction)BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(ActionOffset, sizeof(ushort))),
            gongs,
            attributes);
        return true;
    }
}

public readonly record struct LegacyLeagueOptPacket(
    ushort Action,
    uint Id,
    uint Uid,
    uint Index,
    IReadOnlyList<string> Texts)
{
    private const int ActionOffset = 0;
    private const int IdOffset = ActionOffset + sizeof(ushort);
    private const int UidOffset = IdOffset + sizeof(uint);
    private const int IndexOffset = UidOffset + sizeof(uint);
    private const int TextsOffset = IndexOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyLeagueOpt;
    public const ushort ListAction = 4;
    public const int MinimumPayloadLength = TextsOffset;

    public static bool TryParse(PacketFrame frame, out LegacyLeagueOptPacket packet)
    {
        packet = default;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < MinimumPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var texts = TryReadStringList(payload.AsSpan(TextsOffset), out var parsedTexts)
            ? parsedTexts
            : [];
        packet = new LegacyLeagueOptPacket(
            reader.ReadUInt16(ActionOffset),
            reader.ReadUInt32(IdOffset),
            reader.ReadUInt32(UidOffset),
            reader.ReadUInt32(IndexOffset),
            texts);
        return true;
    }

    private static bool TryReadStringList(ReadOnlySpan<byte> payload, out IReadOnlyList<string> texts)
    {
        texts = [];
        if (payload.IsEmpty)
        {
            return true;
        }

        var count = payload[0];
        var offset = 1;
        var values = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            if (offset > payload.Length - sizeof(ushort))
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            if (offset > payload.Length - length)
            {
                return false;
            }

            values.Add(System.Text.Encoding.UTF8.GetString(payload.Slice(offset, length)));
            offset += length;
        }

        if (offset != payload.Length)
        {
            return false;
        }

        texts = values;
        return true;
    }
}

public readonly record struct LegacyLeagueAllegianceEntry(
    ulong Fund,
    uint Uid,
    uint GoldBricks,
    string Name,
    string LeaderName,
    string RecruitDeclaration);

public readonly record struct LegacyLeagueAllegianceListPacket(
    uint Count,
    ushort Page,
    ushort PageCount,
    byte DwParam,
    IReadOnlyList<LegacyLeagueAllegianceEntry> Entries)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int CountOffset = 0;
    private const int PageOffset = CountOffset + sizeof(uint);
    private const int PageCountOffset = PageOffset + sizeof(ushort);
    private const int DwParamOffset = PageCountOffset + sizeof(ushort);
    private const int EntriesOffset = DwParamOffset + sizeof(byte);
    public const int NameLength = 32;
    public const int LeaderNameLength = 32;
    public const int RecruitDeclarationLength = 256;
    private const int EntryFundOffset = 0;
    private const int EntryUidOffset = EntryFundOffset + sizeof(ulong);
    private const int EntryGoldBricksOffset = EntryUidOffset + sizeof(uint);
    private const int EntryNameOffset = EntryGoldBricksOffset + sizeof(uint);
    private const int EntryLeaderNameOffset = EntryNameOffset + NameLength;
    private const int EntryRecruitDeclarationOffset = EntryLeaderNameOffset + LeaderNameLength;

    public const ushort Opcode = 2032;
    public const int HeaderPayloadLength = EntriesOffset;
    public const int EntryPayloadLength = EntryRecruitDeclarationOffset + RecruitDeclarationLength;

    public static LegacyLeagueAllegianceListPacket Empty(ushort page)
    {
        return Create(count: 0, page: page, entries: []);
    }

    public static LegacyLeagueAllegianceListPacket Create(
        uint count,
        ushort page,
        IReadOnlyList<LegacyLeagueAllegianceEntry> entries,
        byte dwParam = 1)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(entries), "League allegiance page entry count cannot exceed UInt16.");
        }

        return new LegacyLeagueAllegianceListPacket(
            count,
            page,
            checked((ushort)entries.Count),
            dwParam,
            entries);
    }

    public static bool TryParse(PacketFrame frame, out LegacyLeagueAllegianceListPacket packet)
    {
        packet = default;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < HeaderPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var pageCount = reader.ReadUInt16(PageCountOffset);
        var expectedPayloadLength = HeaderPayloadLength + (pageCount * EntryPayloadLength);
        if (frame.Payload.Length != expectedPayloadLength)
        {
            return false;
        }

        var entries = new LegacyLeagueAllegianceEntry[pageCount];
        for (var index = 0; index < entries.Length; index++)
        {
            var offset = EntriesOffset + (index * EntryPayloadLength);
            entries[index] = new LegacyLeagueAllegianceEntry(
                reader.ReadUInt64(offset + EntryFundOffset),
                reader.ReadUInt32(offset + EntryUidOffset),
                reader.ReadUInt32(offset + EntryGoldBricksOffset),
                reader.ReadCString(offset + EntryNameOffset, NameLength, NameEncoding),
                reader.ReadCString(offset + EntryLeaderNameOffset, LeaderNameLength, NameEncoding),
                reader.ReadCString(offset + EntryRecruitDeclarationOffset, RecruitDeclarationLength, NameEncoding));
        }

        packet = new LegacyLeagueAllegianceListPacket(
            reader.ReadUInt32(CountOffset),
            reader.ReadUInt16(PageOffset),
            pageCount,
            frame.Payload.ToArray()[DwParamOffset],
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(
            Opcode,
            checked(HeaderPayloadLength + (Entries.Count * EntryPayloadLength)));
        writer.WriteUInt32(CountOffset, Count);
        writer.WriteUInt16(PageOffset, Page);
        writer.WriteUInt16(PageCountOffset, PageCount);
        writer.Payload[DwParamOffset] = DwParam;

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var offset = EntriesOffset + (index * EntryPayloadLength);
            writer.WriteUInt64(offset + EntryFundOffset, entry.Fund);
            writer.WriteUInt32(offset + EntryUidOffset, entry.Uid);
            writer.WriteUInt32(offset + EntryGoldBricksOffset, entry.GoldBricks);
            WriteFixedString(writer.Payload, offset + EntryNameOffset, NameLength, entry.Name);
            WriteFixedString(writer.Payload, offset + EntryLeaderNameOffset, LeaderNameLength, entry.LeaderName);
            WriteFixedString(
                writer.Payload,
                offset + EntryRecruitDeclarationOffset,
                RecruitDeclarationLength,
                entry.RecruitDeclaration);
        }

        return writer.ToArray();
    }

    private static void WriteFixedString(Span<byte> payload, int offset, int length, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var bytes = NameEncoding.GetBytes(value);
        var count = Math.Min(bytes.Length, length);
        bytes.AsSpan(0, count).CopyTo(payload.Slice(offset, count));
    }
}
