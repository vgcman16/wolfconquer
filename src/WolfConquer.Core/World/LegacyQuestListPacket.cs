using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyQuestListMode : ushort
{
    AcceptQuest = 1,
    QuitQuest = 2,
    Review = 3,
    FinishQuest = 4,
}

public enum LegacyQuestStatus : uint
{
    Accepted = 0,
    Finished = 1,
    Available = 2,
}

public readonly record struct LegacyQuestListItem(uint QuestId, LegacyQuestStatus Status, uint Time);

public readonly record struct LegacyQuestListFrame(
    LegacyQuestListMode Mode,
    ushort Count,
    ulong Reserved1,
    ulong Reserved2,
    IReadOnlyList<LegacyQuestListItem> Items)
{
    public byte[] ToFrameBytes()
    {
        return LegacyQuestListPacket.CreateFrameBytes(Mode, Count, Reserved1, Reserved2, Items);
    }
}

public readonly record struct LegacyQuestListPacket(
    LegacyQuestListMode Mode,
    ushort Count,
    ulong Reserved1,
    ulong Reserved2,
    uint QuestId,
    LegacyQuestStatus Status,
    uint Time)
{
    public const uint LegacyDefaultStarterQuestId = 804;
    public const uint LegacyLeeStarterQuestId = 6_435;

    private static readonly uint[] DailyResetInitialQuestIds = [6_126];

    private static readonly uint[] DailyResetFollowupQuestIds =
    [
        35_024,
        35_007,
        35_025,
        35_028,
        35_034,
        6_390,
        6_329,
        6_245,
        6_049,
        6_366,
        6_014,
        2_375,
        6_126,
    ];

    private const int ModeOffset = 0;
    private const int CountOffset = ModeOffset + sizeof(ushort);
    private const int Reserved1Offset = CountOffset + sizeof(ushort);
    private const int Reserved2Offset = Reserved1Offset + sizeof(ulong);
    private const int QuestIdOffset = Reserved2Offset + sizeof(ulong);
    private const int StatusOffset = QuestIdOffset + sizeof(uint);
    private const int TimeOffset = StatusOffset + sizeof(uint);

    private const string DayAttribute = "Day";

    public const ushort Opcode = WorldPacketOpcodes.LegacyQuestList;
    public const int QueryPayloadLength = sizeof(ushort) + sizeof(ushort) + sizeof(uint) + sizeof(uint) + sizeof(uint);
    public const int HeaderPayloadLength = Reserved2Offset + sizeof(ulong);
    public const int ItemPayloadLength = sizeof(uint) + sizeof(uint) + sizeof(uint);
    public const int MaxReviewItemsPerFrame = 80;
    public const int PayloadLength = HeaderPayloadLength + ItemPayloadLength;
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static bool IsLegacyLeeClass(long characterClass)
    {
        return characterClass is >= 8_000 and <= 8_051;
    }

    public static uint GetLegacyReviewStarterQuestId(long characterClass)
    {
        return IsLegacyLeeClass(characterClass) ? LegacyLeeStarterQuestId : LegacyDefaultStarterQuestId;
    }

    public static PlayerQuestSnapshot EnsureLegacyReviewStarterQuest(
        PlayerQuestSnapshot snapshot,
        long characterClass,
        out uint starterQuestId,
        out bool added)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        starterQuestId = GetLegacyReviewStarterQuestId(characterClass);
        if (snapshot.ContainsQuest(starterQuestId))
        {
            added = false;
            return snapshot;
        }

        added = true;
        return snapshot.WithQuest(new PlayerQuestSnapshotEntry(
            starterQuestId,
            PlayerQuestStatus.Accepted,
            Time: 0,
            [0U]));
    }

    public static LegacyQuestListPacket CreateReviewAvailable(uint questId)
    {
        return new LegacyQuestListPacket(
            LegacyQuestListMode.Review,
            Count: 1,
            Reserved1: 0,
            Reserved2: 0,
            questId,
            LegacyQuestStatus.Available,
            Time: 0);
    }

    public static IReadOnlyList<LegacyQuestListPacket> CreateDailyResetInitialReviews(
        CharacterSnapshot snapshot,
        DateTime now)
    {
        return ShouldSendDailyReset(snapshot, now) ? CreateMany(DailyResetInitialQuestIds) : [];
    }

    public static IReadOnlyList<LegacyQuestListPacket> CreateDailyResetFollowupReviews(
        CharacterSnapshot snapshot,
        DateTime now)
    {
        return ShouldSendDailyReset(snapshot, now) ? CreateMany(DailyResetFollowupQuestIds) : [];
    }

    public static bool ShouldSendDailyReset(CharacterSnapshot snapshot, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.Attributes.TryGetValue(DayAttribute, out var raw) ||
            !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var savedDay))
        {
            savedDay = 0;
        }

        return savedDay != now.DayOfYear;
    }

    public static bool TryParse(PacketFrame frame, out LegacyQuestListPacket packet)
    {
        packet = default;

        if (!TryParseReviewFrame(frame, out var reviewFrame) ||
            reviewFrame.Count != 1 ||
            reviewFrame.Items.Count != 1)
        {
            return false;
        }

        var item = reviewFrame.Items[0];
        packet = new LegacyQuestListPacket(
            reviewFrame.Mode,
            reviewFrame.Count,
            reviewFrame.Reserved1,
            reviewFrame.Reserved2,
            item.QuestId,
            item.Status,
            item.Time);
        return true;
    }

    public static bool TryParseReviewFrame(PacketFrame frame, out LegacyQuestListFrame packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < HeaderPayloadLength ||
            (frame.Payload.Length - HeaderPayloadLength) % ItemPayloadLength != 0 ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var count = reader.ReadUInt16(CountOffset);
        var itemCount = (int)((frame.Payload.Length - HeaderPayloadLength) / ItemPayloadLength);
        if (count != itemCount)
        {
            return false;
        }

        var items = new LegacyQuestListItem[itemCount];
        var offset = HeaderPayloadLength;
        for (var index = 0; index < items.Length; index++)
        {
            items[index] = new LegacyQuestListItem(
                reader.ReadUInt32(offset),
                (LegacyQuestStatus)reader.ReadUInt32(offset + sizeof(uint)),
                reader.ReadUInt32(offset + sizeof(uint) + sizeof(uint)));
            offset += ItemPayloadLength;
        }

        packet = new LegacyQuestListFrame(
            (LegacyQuestListMode)reader.ReadUInt16(ModeOffset),
            count,
            reader.ReadUInt64(Reserved1Offset),
            reader.ReadUInt64(Reserved2Offset),
            items);
        return true;
    }

    public static bool TryParseQuery(PacketFrame frame, out LegacyQuestListPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < QueryPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyQuestListPacket(
            (LegacyQuestListMode)reader.ReadUInt16(ModeOffset),
            reader.ReadUInt16(CountOffset),
            Reserved1: 0,
            Reserved2: 0,
            reader.ReadUInt32(4),
            (LegacyQuestStatus)reader.ReadUInt32(8),
            reader.ReadUInt32(12));
        return true;
    }

    public static byte[] CreateEmptyReviewFrame()
    {
        return CreateFrameBytes(LegacyQuestListMode.Review, count: 0, reserved1: 0, reserved2: 0, []);
    }

    public static byte[] CreateSingleQuestFrame(LegacyQuestListMode mode, LegacyQuestListItem item)
    {
        return CreateFrameBytes(mode, count: 1, reserved1: 0, reserved2: 0, [item]);
    }

    public static IReadOnlyList<byte[]> CreateReviewFramesFromPlayerQuests(PlayerQuestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var items = new LegacyQuestListItem[snapshot.Quests.Count];
        for (var index = 0; index < items.Length; index++)
        {
            items[index] = ToLegacyQuestListItem(snapshot.Quests[index]);
        }

        return CreateReviewFrames(items);
    }

    public static IReadOnlyList<byte[]> CreateReviewFramesFromQuestCatalog(
        PlayerQuestSnapshot snapshot,
        IReadOnlyList<QuestMission> questCatalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(questCatalog);

        var items = new List<LegacyQuestListItem>(questCatalog.Count);
        var seenQuestIds = new HashSet<uint>();
        foreach (var mission in questCatalog)
        {
            if (mission.MissionId < 0)
            {
                continue;
            }

            var questId = checked((uint)mission.MissionId);
            if (!seenQuestIds.Add(questId))
            {
                continue;
            }

            items.Add(snapshot.TryGetQuest(questId, out var quest)
                ? ToLegacyQuestListItem(quest)
                : new LegacyQuestListItem(questId, LegacyQuestStatus.Available, Time: 0));
        }

        return CreateReviewFrames(items);
    }

    private static IReadOnlyList<byte[]> CreateReviewFrames(IReadOnlyList<LegacyQuestListItem> items)
    {
        if (items.Count == 0)
        {
            return [CreateEmptyReviewFrame()];
        }

        var frames = new List<byte[]>();
        for (var offset = 0; offset < items.Count; offset += MaxReviewItemsPerFrame)
        {
            var count = Math.Min(MaxReviewItemsPerFrame, items.Count - offset);
            var chunk = new LegacyQuestListItem[count];
            for (var index = 0; index < chunk.Length; index++)
            {
                chunk[index] = items[offset + index];
            }

            frames.Add(CreateFrameBytes(
                LegacyQuestListMode.Review,
                checked((ushort)chunk.Length),
                reserved1: 0,
                reserved2: 0,
                chunk));
        }

        return frames;
    }

    public byte[] ToFrameBytes()
    {
        return CreateFrameBytes(
            Mode,
            Count,
            Reserved1,
            Reserved2,
            [new LegacyQuestListItem(QuestId, Status, Time)]);
    }

    internal static byte[] CreateFrameBytes(
        LegacyQuestListMode mode,
        ushort count,
        ulong reserved1,
        ulong reserved2,
        IReadOnlyList<LegacyQuestListItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (count != items.Count)
        {
            throw new ArgumentException("Quest list count must match item count.", nameof(count));
        }

        if (count > MaxReviewItemsPerFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Legacy quest review frames are capped at 80 items.");
        }

        var writer = new PacketWriter(Opcode, HeaderPayloadLength + (items.Count * ItemPayloadLength));
        writer.WriteUInt16(ModeOffset, (ushort)mode);
        writer.WriteUInt16(CountOffset, count);
        writer.WriteUInt64(Reserved1Offset, reserved1);
        writer.WriteUInt64(Reserved2Offset, reserved2);

        var offset = HeaderPayloadLength;
        foreach (var item in items)
        {
            writer.WriteUInt32(offset, item.QuestId);
            writer.WriteUInt32(offset + sizeof(uint), (uint)item.Status);
            writer.WriteUInt32(offset + sizeof(uint) + sizeof(uint), item.Time);
            offset += ItemPayloadLength;
        }

        return writer.ToArray();
    }

    private static IReadOnlyList<LegacyQuestListPacket> CreateMany(IReadOnlyList<uint> questIds)
    {
        var packets = new LegacyQuestListPacket[questIds.Count];
        for (var index = 0; index < questIds.Count; index++)
        {
            packets[index] = CreateReviewAvailable(questIds[index]);
        }

        return packets;
    }

    private static LegacyQuestListItem ToLegacyQuestListItem(PlayerQuestSnapshotEntry quest)
    {
        return new LegacyQuestListItem(
            quest.Uid,
            (LegacyQuestStatus)quest.Status,
            quest.Time);
    }
}
