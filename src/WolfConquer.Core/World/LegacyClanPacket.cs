using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyClanPacket(uint Mode, uint ClanId, uint Parameter, IReadOnlyList<string> Values)
{
    private const int HeaderPayloadLength = 13;
    private const string EmptyClanRelationList = "0 0 0 0 0 0 0";
    private const string EmptyClanWarStatus = EmptyClanRelationList;

    public const uint InfoMode = 1;
    public const uint MembersMode = 4;
    public const uint JoinMode = 11;
    public const uint AcceptJoinRequestMode = 12;
    public const uint TransferLeaderMode = 21;
    public const uint KickMemberMode = 22;
    public const uint QuitMode = 23;
    public const uint AnnounceMode = 24;
    public const uint SetAnnouncementMode = 25;
    public const uint DedicateMode = 26;
    public const uint MyClanMode = 29;

    public const ushort Opcode = WorldPacketOpcodes.LegacyClan;

    public static bool TryCreateStartup(CharacterSnapshot snapshot, string? usersRoot, out LegacyClanPacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        packet = default!;
        if (!TryReadClanForSnapshot(snapshot, usersRoot, out var clanId, out var clan))
        {
            return false;
        }

        if (!TryReadAttribute(snapshot, out var rank, "ClanRank"))
        {
            rank = 200;
        }

        if (!TryReadAttribute(snapshot, out var memberDonation, "ClanDonation"))
        {
            memberDonation = 0;
        }

        var memberCount = CountClanMembers(usersRoot, clanId);
        var stats = string.Join(
            ' ',
            clanId,
            memberCount,
            0,
            clan.Donation,
            clan.Level,
            rank,
            0,
            clan.BattlePower,
            0,
            0,
            0,
            memberDonation);

        packet = new LegacyClanPacket(
            InfoMode,
            clanId,
            Parameter: 0,
            [stats, clan.Name, clan.LeaderName, EmptyClanRelationList, string.Empty, string.Empty]);
        return true;
    }

    public static LegacyClanPacket CreateMyClanWarStatus(uint clanId)
    {
        return new LegacyClanPacket(MyClanMode, clanId, Parameter: 0, [EmptyClanWarStatus]);
    }

    public static LegacyClanPacket CreateCallback(LegacyClanPacket request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new LegacyClanPacket(request.Mode, request.ClanId, request.Parameter, request.Values.ToArray());
    }

    public static LegacyClanPacket CreateQuitCallback(uint clanId, uint parameter, IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new LegacyClanPacket(QuitMode, clanId, parameter, values.ToArray());
    }

    public static LegacyClanPacket CreateDedicateCallback(uint clanId, uint parameter, IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new LegacyClanPacket(DedicateMode, clanId, parameter, values.ToArray());
    }

    public static bool TryCreateMembers(
        CharacterSnapshot snapshot,
        string? usersRoot,
        out LegacyClanMemberListPacket packet,
        WorldSessionStore? sessions = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        packet = default!;
        if (!TryReadClanForSnapshot(snapshot, usersRoot, out var clanId, out _))
        {
            return false;
        }

        packet = new LegacyClanMemberListPacket(CreateClanMemberEntries(snapshot, usersRoot, clanId, sessions));
        return true;
    }

    public static bool IsSourceNoOpMode(uint mode)
    {
        return mode is 5 or 6 or 7 or 8 or 13 or 16 or 19 or 27 or 28;
    }

    public static bool TryCreateBulletin(CharacterSnapshot snapshot, string? usersRoot, out LegacyClanPacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        packet = default!;
        if (!TryReadClanForSnapshot(snapshot, usersRoot, out var clanId, out var clan) ||
            string.Equals(clan.Bulletin, "None", StringComparison.Ordinal))
        {
            return false;
        }

        packet = new LegacyClanPacket(SetAnnouncementMode, clanId, Parameter: 0, [clan.Bulletin]);
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyClanPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode || frame.Payload.Length < HeaderPayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var mode = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4));
        var clanId = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4, 4));
        var parameter = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(8, 4));
        var count = payload[12];
        var offset = HeaderPayloadLength;
        var values = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            if (offset > payload.Length - sizeof(ushort))
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            if (offset > payload.Length - length)
            {
                return false;
            }

            values.Add(Encoding.UTF8.GetString(payload.AsSpan(offset, length)));
            offset += length;
        }

        if (offset != payload.Length)
        {
            return false;
        }

        packet = new LegacyClanPacket(mode, clanId, parameter, values);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        if (Values.Count > byte.MaxValue)
        {
            throw new InvalidOperationException("Legacy clan string-list count must fit one byte.");
        }

        var encoded = Values.Select(Encoding.UTF8.GetBytes).ToArray();
        var payloadLength = HeaderPayloadLength + encoded.Sum(value => sizeof(ushort) + value.Length);
        var writer = new PacketWriter(Opcode, payloadLength);
        writer.WriteUInt32(0, Mode);
        writer.WriteUInt32(4, ClanId);
        writer.WriteUInt32(8, Parameter);
        writer.Payload[12] = (byte)encoded.Length;

        var offset = HeaderPayloadLength;
        foreach (var value in encoded)
        {
            if (value.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException("Legacy clan string must fit UInt16 length.");
            }

            BinaryPrimitives.WriteUInt16LittleEndian(writer.Payload.Slice(offset, sizeof(ushort)), (ushort)value.Length);
            offset += sizeof(ushort);
            value.CopyTo(writer.Payload.Slice(offset, value.Length));
            offset += value.Length;
        }

        return writer.ToArray();
    }

    private static bool TryReadAttribute(CharacterSnapshot snapshot, out uint value, params string[] keys)
    {
        return TryReadAttribute(snapshot.Attributes, out value, keys);
    }

    private static bool TryReadAttribute(IReadOnlyDictionary<string, string> attributes, out uint value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var raw) &&
                uint.TryParse(raw, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static string? TryDeriveSiblingRoot(string? usersRoot, string siblingName)
    {
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(usersRoot);
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        if (!string.Equals(Path.GetFileName(trimmed), "Users", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dbRoot = Path.GetDirectoryName(trimmed);
        return string.IsNullOrEmpty(dbRoot)
            ? null
            : Path.Combine(dbRoot, siblingName);
    }

    private static bool TryReadClanForSnapshot(
        CharacterSnapshot snapshot,
        string? usersRoot,
        out uint clanId,
        out LegacyClanFile clan)
    {
        clanId = 0;
        clan = default;
        if (!TryReadAttribute(snapshot, out clanId, "ClanID", "ClanUID") || clanId == 0)
        {
            return false;
        }

        var clanRoot = TryDeriveSiblingRoot(usersRoot, "Clans");
        if (clanRoot is null)
        {
            return false;
        }

        var clanPath = Path.Combine(clanRoot, clanId.ToString() + ".txt");
        return File.Exists(clanPath) && TryReadClanFile(clanPath, out clan);
    }

    private static bool TryReadClanFile(string clanPath, out LegacyClanFile clan)
    {
        clan = default;
        string[] lines;
        try
        {
            lines = File.ReadAllLines(clanPath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        var clanLine = lines.FirstOrDefault(line => !line.StartsWith("Count=", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(clanLine))
        {
            return false;
        }

        var parts = clanLine.Split('/', StringSplitOptions.None);
        if (parts.Length < 7 ||
            !uint.TryParse(parts[0], out var id) ||
            !uint.TryParse(parts[4], out var donation) ||
            !byte.TryParse(parts[3], out var level) ||
            !byte.TryParse(parts[6], out var battlePower))
        {
            return false;
        }

        clan = new LegacyClanFile(id, parts[1], parts[2], level, donation, parts[5], battlePower);
        return true;
    }

    private static uint CountClanMembers(string? usersRoot, uint clanId)
    {
        if (string.IsNullOrWhiteSpace(usersRoot) || !Directory.Exists(usersRoot))
        {
            return 1;
        }

        uint count = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(usersRoot, "*.ini"))
            {
                if (File.ReadLines(file).Any(line =>
                    line.StartsWith("ClanID=", StringComparison.OrdinalIgnoreCase) &&
                    uint.TryParse(line["ClanID=".Length..].Trim(), out var currentClanId) &&
                    currentClanId == clanId))
                {
                    count++;
                }
            }
        }
        catch (IOException)
        {
            return Math.Max(count, 1u);
        }
        catch (UnauthorizedAccessException)
        {
            return Math.Max(count, 1u);
        }

        return Math.Max(count, 1u);
    }

    private static IReadOnlyList<LegacyClanMemberListEntry> CreateClanMemberEntries(
        CharacterSnapshot snapshot,
        string? usersRoot,
        uint clanId,
        WorldSessionStore? sessions)
    {
        var members = new List<LegacyClanMemberListEntry>();
        if (string.IsNullOrWhiteSpace(usersRoot) || !Directory.Exists(usersRoot))
        {
            if (TryCreateMemberEntry(
                    snapshot.Attributes,
                    clanId,
                    snapshot.Uid,
                    new HashSet<long> { snapshot.Uid },
                    out var current))
            {
                members.Add(current);
            }

            return members;
        }

        var activeUids = sessions?.Snapshot()
                .Select(active => active.Session.Character.Uid)
                .Append(snapshot.Uid)
                .ToHashSet()
            ?? new HashSet<long> { snapshot.Uid };

        try
        {
            foreach (var file in Directory.EnumerateFiles(usersRoot, "*.ini"))
            {
                if (TryReadCharacterAttributes(file, out var attributes) &&
                    TryCreateMemberEntry(attributes, clanId, snapshot.Uid, activeUids, out var member))
                {
                    members.Add(member);
                }
            }
        }
        catch (IOException)
        {
            if (members.Count == 0 &&
                TryCreateMemberEntry(snapshot.Attributes, clanId, snapshot.Uid, activeUids, out var current))
            {
                members.Add(current);
            }
        }
        catch (UnauthorizedAccessException)
        {
            if (members.Count == 0 &&
                TryCreateMemberEntry(snapshot.Attributes, clanId, snapshot.Uid, activeUids, out var current))
            {
                members.Add(current);
            }
        }

        if (members.All(member => member.Uid != snapshot.Uid) &&
            TryCreateMemberEntry(snapshot.Attributes, clanId, snapshot.Uid, activeUids, out var currentSnapshot))
        {
            members.Add(currentSnapshot);
        }

        return members
            .OrderByDescending(member => member.Online)
            .ThenBy(member => member.Rank)
            .ThenBy(member => member.Level)
            .ThenBy(member => member.Uid)
            .ToArray();
    }

    private static bool TryReadCharacterAttributes(string path, out IReadOnlyDictionary<string, string> attributes)
    {
        attributes = default!;
        try
        {
            attributes = IniDocument.Parse(File.ReadAllText(path)).GetSection("Character");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private static bool TryCreateMemberEntry(
        IReadOnlyDictionary<string, string> attributes,
        uint clanId,
        long currentUid,
        IReadOnlySet<long> activeUids,
        out LegacyClanMemberListEntry member)
    {
        member = default!;
        if (!TryReadAttribute(attributes, out var currentClanId, "ClanID", "ClanUID") ||
            currentClanId != clanId ||
            !TryReadAttribute(attributes, out var uid, "UID") ||
            uid == 0)
        {
            return false;
        }

        member = new LegacyClanMemberListEntry(
            uid,
            ReadAttribute(attributes, "Name", "None"),
            ReadByteAttribute(attributes, defaultValue: 0, "Level"),
            ReadUInt16Attribute(attributes, defaultValue: 200, "ClanRank"),
            uid == currentUid || activeUids.Contains(uid),
            ReadUInt32Attribute(attributes, defaultValue: 0, "Class"),
            ReadUInt32Attribute(attributes, defaultValue: 0, "ClanDonation"));
        return true;
    }

    private static string ReadAttribute(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        string defaultValue)
    {
        return attributes.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : defaultValue;
    }

    private static byte ReadByteAttribute(
        IReadOnlyDictionary<string, string> attributes,
        byte defaultValue,
        string key)
    {
        return attributes.TryGetValue(key, out var raw) &&
            byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    private static ushort ReadUInt16Attribute(
        IReadOnlyDictionary<string, string> attributes,
        ushort defaultValue,
        string key)
    {
        return attributes.TryGetValue(key, out var raw) &&
            ushort.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    private static uint ReadUInt32Attribute(
        IReadOnlyDictionary<string, string> attributes,
        uint defaultValue,
        string key)
    {
        return attributes.TryGetValue(key, out var raw) &&
            uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    private readonly record struct LegacyClanFile(
        uint Id,
        string Name,
        string LeaderName,
        byte Level,
        uint Donation,
        string Bulletin,
        byte BattlePower);
}

public sealed record LegacyClanMemberListPacket(IReadOnlyList<LegacyClanMemberListEntry> Members)
{
    private const int ModeOffset = 0;
    private const int ClanIdOffset = ModeOffset + sizeof(uint);
    private const int ParameterOffset = ClanIdOffset + sizeof(uint);
    private const int CountOffset = ParameterOffset + sizeof(uint);
    private const int EntriesOffset = CountOffset + sizeof(uint);

    private const int EntryNameOffset = 0;
    private const int EntryLevelOffset = EntryNameOffset + FixedNameLength;
    private const int EntryRankOffset = EntryLevelOffset + sizeof(uint);
    private const int EntryOnlineOffset = EntryRankOffset + sizeof(ushort);
    private const int EntryClassOffset = EntryOnlineOffset + sizeof(ushort);
    private const int EntryUnknownOffset = EntryClassOffset + sizeof(uint);
    private const int EntryDonationOffset = EntryUnknownOffset + sizeof(uint);

    public const int FixedNameLength = 32;
    public const int EntryPayloadLength = EntryDonationOffset + sizeof(uint);

    public static bool TryParse(PacketFrame frame, out LegacyClanMemberListPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != LegacyClanPacket.Opcode)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        if (payload.Length < EntriesOffset ||
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ModeOffset, sizeof(uint))) != LegacyClanPacket.MembersMode)
        {
            return false;
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(CountOffset, sizeof(uint)));
        if (count > (payload.Length - EntriesOffset) / EntryPayloadLength)
        {
            return false;
        }

        var entryCount = (int)count;
        var expectedLength = EntriesOffset + entryCount * EntryPayloadLength;
        if (payload.Length != expectedLength)
        {
            return false;
        }

        var members = new List<LegacyClanMemberListEntry>(entryCount);
        for (var index = 0; index < entryCount; index++)
        {
            var offset = EntriesOffset + index * EntryPayloadLength;
            members.Add(new LegacyClanMemberListEntry(
                Uid: 0,
                Name: ReadFixedUtf8(payload.AsSpan(offset + EntryNameOffset, FixedNameLength)),
                Level: CheckedByte(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset + EntryLevelOffset, sizeof(uint)))),
                Rank: BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset + EntryRankOffset, sizeof(ushort))),
                Online: BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset + EntryOnlineOffset, sizeof(ushort))) != 0,
                Class: BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset + EntryClassOffset, sizeof(uint))),
                Donation: BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset + EntryDonationOffset, sizeof(uint)))));
        }

        packet = new LegacyClanMemberListPacket(members);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payloadLength = EntriesOffset + checked(Members.Count * EntryPayloadLength);
        var writer = new PacketWriter(LegacyClanPacket.Opcode, payloadLength);
        writer.WriteUInt32(ModeOffset, LegacyClanPacket.MembersMode);
        writer.WriteUInt32(ClanIdOffset, 0);
        writer.WriteUInt32(ParameterOffset, 0);
        writer.WriteUInt32(CountOffset, checked((uint)Members.Count));

        for (var index = 0; index < Members.Count; index++)
        {
            var member = Members[index];
            var offset = EntriesOffset + index * EntryPayloadLength;
            WriteFixedUtf8(writer.Payload.Slice(offset + EntryNameOffset, FixedNameLength), member.Name);
            writer.WriteUInt32(offset + EntryLevelOffset, member.Level);
            writer.WriteUInt16(offset + EntryRankOffset, member.Rank);
            writer.WriteUInt16(offset + EntryOnlineOffset, member.Online ? (ushort)1 : (ushort)0);
            writer.WriteUInt32(offset + EntryClassOffset, member.Class);
            writer.WriteUInt32(offset + EntryUnknownOffset, 1);
            writer.WriteUInt32(offset + EntryDonationOffset, member.Donation);
        }

        return writer.ToArray();
    }

    private static void WriteFixedUtf8(Span<byte> destination, string value)
    {
        destination.Clear();
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }

    private static string ReadFixedUtf8(ReadOnlySpan<byte> source)
    {
        var length = source.IndexOf((byte)0);
        if (length < 0)
        {
            length = source.Length;
        }

        return Encoding.UTF8.GetString(source[..length]);
    }

    private static byte CheckedByte(uint value)
    {
        return value > byte.MaxValue ? byte.MaxValue : (byte)value;
    }
}

public sealed record LegacyClanMemberListEntry(
    uint Uid,
    string Name,
    byte Level,
    ushort Rank,
    bool Online,
    uint Class,
    uint Donation);
