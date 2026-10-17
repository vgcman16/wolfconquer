using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyTitleStorageAction : uint
{
    UpdateScore = 0,
    UseTitle = 1,
    RemoveTitle = 3,
    Equip = 4,
    UnEquip = 5,
    FullLoad = 6,
}

public sealed record LegacyTitleStorageTitle(
    uint Id,
    uint SubId,
    uint DwParam1 = 0,
    uint DwParam2 = 0);

public readonly record struct LegacyActiveSpecialTitleState(
    ulong SpecialTitleId,
    ulong SpecialWingId,
    ulong SpecialTitleScore);

public sealed record LegacyTitleStoragePacket(
    LegacyTitleStorageAction Action,
    uint DwParam1,
    uint DwParam2,
    uint DwParam3,
    LegacyTitleStorageTitle? Title = null)
{
    private const int ActionField = 1;
    private const int DwParam1Field = 2;
    private const int DwParam2Field = 3;
    private const int DwParam3Field = 4;
    private const int TitleField = 5;
    private const int TitleIdField = 1;
    private const int TitleSubIdField = 2;
    private const int TitleDwParam1Field = 3;
    private const int TitleDwParam2Field = 4;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    private static readonly uint[] ConditionalRemovalOrder =
    [
        2003, // Conqueror
        2004, // Talent
        6001, // WingsofInfernal
    ];

    private static readonly uint[] VipRemovalOrder =
    [
        2031, // Paladin
        2022, // Beauty
        6009, // WingsofPlanet
        2029, // GloryKnight
    ];

    private static readonly uint[] WardrobeRemovalOrder =
    [
        2006, // SwiftChaser
        2013, // MonkeyRider
        2016, // SaintRider
        2015, // LunarRider
        2014, // SolarRider
        2050, // ImmortalFate
    ];

    public const ushort Opcode = WorldPacketOpcodes.LegacyMsgTitleStorage;

    public static IReadOnlyList<LegacyTitleStoragePacket> CreateLegacyStartupCheckUpRemovals(
        CharacterSnapshot snapshot,
        string? usersRoot,
        PlayerItemSnapshot? playerItems = null,
        IReadOnlyDictionary<int, CoatStorageType>? coatStorageTypesByItemTypeId = null)
    {
        return TryCreateLegacyStartupState(snapshot, usersRoot, playerItems, coatStorageTypesByItemTypeId, out var state)
            ? state.RemovalPackets
            : [];
    }

    public static IReadOnlyList<LegacyTitleStoragePacket> CreateLegacyStartupFullLoad(
        CharacterSnapshot snapshot,
        string? usersRoot,
        PlayerItemSnapshot? playerItems = null,
        IReadOnlyDictionary<int, CoatStorageType>? coatStorageTypesByItemTypeId = null)
    {
        if (!TryCreateLegacyStartupState(snapshot, usersRoot, playerItems, coatStorageTypesByItemTypeId, out var state))
        {
            return [];
        }

        var packets = new List<LegacyTitleStoragePacket>();
        foreach (var titleId in state.ActiveTitles)
        {
            if (!state.TitleTypes.TryGetValue(titleId, out var title))
            {
                continue;
            }

            var nested = new LegacyTitleStorageTitle(title.Id, title.SubId);
            packets.Add(new LegacyTitleStoragePacket(
                LegacyTitleStorageAction.UpdateScore,
                state.Score,
                title.Id,
                title.SubId,
                nested));
            packets.Add(new LegacyTitleStoragePacket(
                Action: LegacyTitleStorageAction.FullLoad,
                DwParam1: 100,
                DwParam2: title.Id,
                DwParam3: title.SubId,
                Title: nested));
        }

        foreach (var titleId in ReadActiveTitleIds(snapshot, state.TitleTypes))
        {
            if (!state.TitleTypes.TryGetValue(titleId, out var title))
            {
                continue;
            }

            packets.Add(new LegacyTitleStoragePacket(
                LegacyTitleStorageAction.UpdateScore,
                state.Score,
                title.Id,
                title.SubId,
                new LegacyTitleStorageTitle(title.Id, title.SubId, DwParam1: 100)));
        }

        return packets;
    }

    public static LegacyActiveSpecialTitleState ResolveActiveSpecialTitleState(
        CharacterSnapshot snapshot,
        string? usersRoot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var resolved = default(LegacyActiveSpecialTitleState);
        var dbRoot = TryDeriveDbRoot(usersRoot);
        if (dbRoot is not null)
        {
            var titleTypes = ReadTitleTypes(Path.Combine(dbRoot, "title_type.txt"));
            if (titleTypes.Count != 0)
            {
                resolved = ResolveActiveSpecialTitleState(snapshot, titleTypes);
            }
        }

        return ApplyExplicitActiveTitleAttributes(snapshot, resolved);
    }

    private static bool TryCreateLegacyStartupState(
        CharacterSnapshot snapshot,
        string? usersRoot,
        PlayerItemSnapshot? playerItems,
        IReadOnlyDictionary<int, CoatStorageType>? coatStorageTypesByItemTypeId,
        out LegacyTitleStartupState state)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        state = LegacyTitleStartupState.Empty;

        if (!snapshot.Attributes.TryGetValue("SpecialTitles", out var rawSpecialTitles))
        {
            return false;
        }

        var dbRoot = TryDeriveDbRoot(usersRoot);
        if (dbRoot is null)
        {
            return false;
        }

        var titleTypes = ReadTitleTypes(Path.Combine(dbRoot, "title_type.txt"));
        if (titleTypes.Count == 0)
        {
            return false;
        }

        var activeTitles = ParseSpecialTitles(rawSpecialTitles);
        var score = ComputeScore(activeTitles, titleTypes);
        var packets = new List<LegacyTitleStoragePacket>();

        void RemoveTitle(uint titleId, bool onlyWhenPresent = false)
        {
            if (onlyWhenPresent && !activeTitles.Contains(titleId))
            {
                return;
            }

            if (!titleTypes.TryGetValue(titleId, out var title))
            {
                return;
            }

            if (activeTitles.Remove(titleId))
            {
                score = score >= title.Score
                    ? score - title.Score
                    : 0;
            }

            var nested = new LegacyTitleStorageTitle(title.Id, title.SubId);
            packets.Add(new LegacyTitleStoragePacket(
                LegacyTitleStorageAction.UnEquip,
                DwParam1: 0,
                title.Id,
                title.SubId));
            packets.Add(new LegacyTitleStoragePacket(
                LegacyTitleStorageAction.UnEquip,
                score,
                title.Id,
                title.SubId,
                nested));
            packets.Add(new LegacyTitleStoragePacket(
                LegacyTitleStorageAction.UpdateScore,
                score,
                title.Id,
                title.SubId,
                nested));
            packets.Add(new LegacyTitleStoragePacket(
                LegacyTitleStorageAction.RemoveTitle,
                score,
                title.Id,
                title.SubId,
                nested));
        }

        void AddTitle(uint titleId)
        {
            if (activeTitles.Contains(titleId) ||
                !titleTypes.TryGetValue(titleId, out var title))
            {
                return;
            }

            var nested = new LegacyTitleStorageTitle(title.Id, title.SubId);
            packets.Add(new LegacyTitleStoragePacket(
                LegacyTitleStorageAction.UpdateScore,
                score,
                title.Id,
                title.SubId,
                nested));
            packets.Add(new LegacyTitleStoragePacket(
                Action: LegacyTitleStorageAction.FullLoad,
                DwParam1: 100,
                DwParam2: title.Id,
                DwParam3: title.SubId,
                Title: nested));
            activeTitles.Add(titleId);
            score += title.Score;
        }

        if (activeTitles.Contains(2002))
        {
            RemoveTitle(6002);
            RemoveTitle(2002);
        }

        foreach (var titleId in ConditionalRemovalOrder)
        {
            RemoveTitle(titleId, onlyWhenPresent: true);
        }

        RemoveTitle(2005);
        RemoveTitle(6025, onlyWhenPresent: true);

        if (ReadVipLevel(snapshot) != 6)
        {
            foreach (var titleId in VipRemovalOrder)
            {
                RemoveTitle(titleId);
            }
        }

        foreach (var titleId in WardrobeRemovalOrder)
        {
            RemoveTitle(titleId);
        }

        if (CountWardrobeItemsAtLeastStars(playerItems, coatStorageTypesByItemTypeId, garment: true, stars: 5) < 1)
        {
            RemoveTitle(6003);
        }

        if (CountWardrobeItemsAtLeastStars(playerItems, coatStorageTypesByItemTypeId, garment: false, stars: 5) < 1)
        {
            RemoveTitle(6004);
        }

        if (IsLegacyProjectTitleAccount(snapshot))
        {
            foreach (var title in titleTypes.Values)
            {
                AddTitle(title.Id);
            }
        }

        state = new LegacyTitleStartupState(
            titleTypes,
            activeTitles,
            score,
            packets);
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyTitleStoragePacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var action = LegacyTitleStorageAction.UpdateScore;
        var dwParam1 = 0u;
        var dwParam2 = 0u;
        var dwParam3 = 0u;
        LegacyTitleStorageTitle? title = null;
        var hasAction = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            switch (fieldNumber)
            {
                case ActionField:
                    if (wireType != VarintWireType ||
                        !reader.TryReadVarint(out var actionValue) ||
                        actionValue > uint.MaxValue)
                    {
                        return false;
                    }

                    action = (LegacyTitleStorageAction)(uint)actionValue;
                    hasAction = true;
                    break;

                case DwParam1Field:
                    if (!TryReadUInt32Field(ref reader, wireType, out dwParam1))
                    {
                        return false;
                    }

                    break;

                case DwParam2Field:
                    if (!TryReadUInt32Field(ref reader, wireType, out dwParam2))
                    {
                        return false;
                    }

                    break;

                case DwParam3Field:
                    if (!TryReadUInt32Field(ref reader, wireType, out dwParam3))
                    {
                        return false;
                    }

                    break;

                case TitleField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadBytes(out var titleBytes) ||
                        !TryParseTitle(titleBytes, out title))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }
        }

        if (!hasAction)
        {
            return false;
        }

        packet = new LegacyTitleStoragePacket(action, dwParam1, dwParam2, dwParam3, title);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, ActionField, (uint)Action);
        WriteVarintField(payload, DwParam1Field, DwParam1);
        WriteVarintField(payload, DwParam2Field, DwParam2);
        WriteVarintField(payload, DwParam3Field, DwParam3);
        if (Title is not null)
        {
            var titlePayload = new List<byte>();
            WriteVarintField(titlePayload, TitleIdField, Title.Id);
            WriteVarintField(titlePayload, TitleSubIdField, Title.SubId);
            WriteVarintField(titlePayload, TitleDwParam1Field, Title.DwParam1);
            WriteVarintField(titlePayload, TitleDwParam2Field, Title.DwParam2);
            WriteBytes(payload, TitleField, titlePayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryReadUInt32Field(ref ProtoReader reader, int wireType, out uint value)
    {
        value = 0;
        if (wireType != VarintWireType ||
            !reader.TryReadVarint(out var raw) ||
            raw > uint.MaxValue)
        {
            return false;
        }

        value = (uint)raw;
        return true;
    }

    private static bool TryParseTitle(byte[] payload, out LegacyTitleStorageTitle title)
    {
        var reader = new ProtoReader(payload);
        var id = 0u;
        var subId = 0u;
        var dwParam1 = 0u;
        var dwParam2 = 0u;
        var hasId = false;
        var hasSubId = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                title = default!;
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            switch (fieldNumber)
            {
                case TitleIdField:
                    if (!TryReadUInt32Field(ref reader, wireType, out id))
                    {
                        title = default!;
                        return false;
                    }

                    hasId = true;
                    break;

                case TitleSubIdField:
                    if (!TryReadUInt32Field(ref reader, wireType, out subId))
                    {
                        title = default!;
                        return false;
                    }

                    hasSubId = true;
                    break;

                case TitleDwParam1Field:
                    if (!TryReadUInt32Field(ref reader, wireType, out dwParam1))
                    {
                        title = default!;
                        return false;
                    }

                    break;

                case TitleDwParam2Field:
                    if (!TryReadUInt32Field(ref reader, wireType, out dwParam2))
                    {
                        title = default!;
                        return false;
                    }

                    break;

                default:
                    title = default!;
                    return false;
            }
        }

        if (!hasId || !hasSubId)
        {
            title = default!;
            return false;
        }

        title = new LegacyTitleStorageTitle(id, subId, dwParam1, dwParam2);
        return true;
    }

    private static List<uint> ParseSpecialTitles(string rawSpecialTitles)
    {
        var tokens = rawSpecialTitles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 ||
            !uint.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            return [];
        }

        var titles = new List<uint>();
        for (var index = 0; index < count; index++)
        {
            var tokenIndex = 1 + index * 2;
            if (tokenIndex >= tokens.Length)
            {
                break;
            }

            if (uint.TryParse(tokens[tokenIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var titleId))
            {
                titles.Add(titleId);
            }
        }

        return titles;
    }

    private static Dictionary<uint, LegacyTitleType> ReadTitleTypes(string path)
    {
        var titles = new Dictionary<uint, LegacyTitleType>();
        if (!File.Exists(path))
        {
            return titles;
        }

        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split(["@@", " "], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 8 ||
                    !uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
                    !uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var subId) ||
                    !uint.TryParse(parts[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
                {
                    continue;
                }

                titles[id] = new LegacyTitleType(id, subId, score);
            }
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        return titles;
    }

    private static uint ComputeScore(IEnumerable<uint> specialTitles, IReadOnlyDictionary<uint, LegacyTitleType> titleTypes)
    {
        uint score = 0;
        foreach (var titleId in specialTitles)
        {
            if (titleTypes.TryGetValue(titleId, out var title))
            {
                score += title.Score;
            }
        }

        return score;
    }

    private static uint CountWardrobeItemsAtLeastStars(
        PlayerItemSnapshot? playerItems,
        IReadOnlyDictionary<int, CoatStorageType>? coatStorageTypesByItemTypeId,
        bool garment,
        int stars)
    {
        if (playerItems is null || coatStorageTypesByItemTypeId is null)
        {
            return 0;
        }

        uint count = 0;
        foreach (var item in EnumerateLegacyWardrobeItems(playerItems))
        {
            if (item.ItemId > int.MaxValue ||
                !coatStorageTypesByItemTypeId.TryGetValue((int)item.ItemId, out var coatStorageType))
            {
                continue;
            }

            var parameters = coatStorageType.Parameters;
            if (parameters.Count < 6)
            {
                continue;
            }

            var isGarment = parameters[0] == 1;
            if (isGarment != garment)
            {
                continue;
            }

            if (parameters[5] >= stars)
            {
                count++;
            }
        }

        return count;
    }

    private static IEnumerable<PlayerItemSnapshotItem> EnumerateLegacyWardrobeItems(PlayerItemSnapshot playerItems)
    {
        foreach (var item in playerItems.WardrobeItems)
        {
            yield return item;
        }

        foreach (var item in playerItems.EquipmentItems)
        {
            if (item.WarehouseId == 100)
            {
                yield return item;
            }
        }
    }

    private static bool IsLegacyProjectTitleAccount(CharacterSnapshot snapshot)
    {
        return snapshot.Name.Contains("[PM]", StringComparison.OrdinalIgnoreCase) ||
               snapshot.Name.Contains("[GM]", StringComparison.OrdinalIgnoreCase) ||
               (snapshot.Attributes.TryGetValue("ProjectManager", out var rawProjectManager) &&
                (string.Equals(rawProjectManager, "true", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rawProjectManager, "1", StringComparison.Ordinal)));
    }

    private static ulong ReadVipLevel(CharacterSnapshot snapshot)
    {
        foreach (var key in new[] { "VipLevel", "VIPLevel" })
        {
            if (snapshot.Attributes.TryGetValue(key, out var raw) &&
                ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return 0;
    }

    private static LegacyActiveSpecialTitleState ResolveActiveSpecialTitleState(
        CharacterSnapshot snapshot,
        IReadOnlyDictionary<uint, LegacyTitleType> titleTypes)
    {
        ulong activeTitle = 0;
        ulong activeWing = 0;
        ulong activeTitleScore = 0;
        if (snapshot.Attributes.TryGetValue("SpecialTitles", out var rawSpecialTitles))
        {
            var tokens = rawSpecialTitles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length > 0 &&
                uint.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                for (var index = 0; index < count; index++)
                {
                    var tokenIndex = 1 + index * 2;
                    var flagIndex = tokenIndex + 1;
                    if (flagIndex >= tokens.Length)
                    {
                        break;
                    }

                    if (!uint.TryParse(tokens[tokenIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var titleId) ||
                        !uint.TryParse(tokens[flagIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var activeFlag) ||
                        activeFlag != 1 ||
                        !titleTypes.TryGetValue(titleId, out var title))
                    {
                        continue;
                    }

                    if (title.Id < 4001)
                    {
                        activeTitle = ((ulong)title.Id * 10_000) + title.SubId;
                        activeTitleScore = title.Score;
                    }
                    else
                    {
                        activeWing = ((ulong)title.Id * 10_000) + title.SubId;
                    }
                }
            }
        }

        return ApplyExplicitActiveTitleAttributes(
            snapshot,
            new LegacyActiveSpecialTitleState(activeTitle, activeWing, activeTitleScore));
    }

    private static LegacyActiveSpecialTitleState ApplyExplicitActiveTitleAttributes(
        CharacterSnapshot snapshot,
        LegacyActiveSpecialTitleState fallback)
    {
        var specialTitleId = ReadPackedActiveTitleAttribute(snapshot, "SpecialTitleID");
        var specialWingId = ReadPackedActiveTitleAttribute(snapshot, "SpecialWingID");
        var specialTitleScore = ReadAttributeUInt64(snapshot.Attributes, "SpecialTitleScore");

        return fallback with
        {
            SpecialTitleId = specialTitleId == 0 ? fallback.SpecialTitleId : specialTitleId,
            SpecialWingId = specialWingId == 0 ? fallback.SpecialWingId : specialWingId,
            SpecialTitleScore = specialTitleScore == 0 ? fallback.SpecialTitleScore : specialTitleScore,
        };
    }

    private static IEnumerable<uint> ReadActiveTitleIds(
        CharacterSnapshot snapshot,
        IReadOnlyDictionary<uint, LegacyTitleType> titleTypes)
    {
        var activeState = ResolveActiveSpecialTitleState(snapshot, titleTypes);
        var activeTitle = UnpackTitleId(activeState.SpecialTitleId);
        var activeWing = UnpackTitleId(activeState.SpecialWingId);

        if (activeTitle != 0)
        {
            yield return activeTitle;
        }

        if (activeWing != 0)
        {
            yield return activeWing;
        }
    }

    private static uint UnpackTitleId(ulong packed)
    {
        return packed >= 10_000 && packed / 10_000 <= uint.MaxValue
            ? (uint)(packed / 10_000)
            : 0;
    }

    private static ulong ReadPackedActiveTitleAttribute(CharacterSnapshot snapshot, string key)
    {
        var packed = ReadAttributeUInt64(snapshot.Attributes, key);
        return packed >= 10_000 ? packed : 0;
    }

    private static ulong ReadAttributeUInt64(IReadOnlyDictionary<string, string> attributes, string key)
    {
        return attributes.TryGetValue(key, out var raw) &&
               ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static string? TryDeriveDbRoot(string? usersRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(usersRoot);
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        return string.Equals(Path.GetFileName(trimmed), "Users", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(trimmed)
            : null;
    }

    private static void WriteOptionalVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        if (value != 0)
        {
            WriteVarintField(payload, fieldNumber, value);
        }
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteBytes(List<byte> payload, int fieldNumber, IReadOnlyCollection<byte> value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)value.Count);
        payload.AddRange(value);
    }

    private static void WriteVarint(List<byte> payload, ulong value)
    {
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }

            payload.Add(next);
        }
        while (value != 0);
    }

    private sealed record LegacyTitleStartupState(
        IReadOnlyDictionary<uint, LegacyTitleType> TitleTypes,
        IReadOnlyList<uint> ActiveTitles,
        uint Score,
        IReadOnlyList<LegacyTitleStoragePacket> RemovalPackets)
    {
        public static LegacyTitleStartupState Empty { get; } = new(
            new Dictionary<uint, LegacyTitleType>(),
            [],
            0,
            []);
    }

    private readonly record struct LegacyTitleType(uint Id, uint SubId, uint Score);

    private ref struct ProtoReader(ReadOnlySpan<byte> payload)
    {
        private readonly ReadOnlySpan<byte> _payload = payload;
        private int _offset;

        public readonly bool End => _offset >= _payload.Length;

        public bool TryReadVarint(out ulong value)
        {
            value = 0;

            for (var shift = 0; shift < 64; shift += 7)
            {
                if (_offset >= _payload.Length)
                {
                    return false;
                }

                var current = _payload[_offset++];
                value |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryReadBytes(out byte[] value)
        {
            value = [];

            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset > _payload.Length - (int)length)
            {
                return false;
            }

            value = _payload.Slice(_offset, (int)length).ToArray();
            _offset += (int)length;
            return true;
        }
    }
}
