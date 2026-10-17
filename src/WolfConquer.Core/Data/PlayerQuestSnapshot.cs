using System.Buffers.Binary;
using System.Globalization;

namespace WolfConquer.Core.Data;

public sealed record PlayerQuestSnapshot(IReadOnlyList<PlayerQuestSnapshotEntry> Quests)
{
    public static PlayerQuestSnapshot Empty { get; } = new([]);

    public IReadOnlyList<PlayerQuestSnapshotEntry> AcceptedQuests { get; } =
        Quests.Where(quest => quest.Status == PlayerQuestStatus.Accepted).ToArray();

    public bool ContainsQuest(uint questUid)
    {
        return Quests.Any(quest => quest.Uid == questUid);
    }

    public bool TryGetQuest(uint questUid, out PlayerQuestSnapshotEntry quest)
    {
        foreach (var entry in Quests)
        {
            if (entry.Uid == questUid)
            {
                quest = entry;
                return true;
            }
        }

        quest = default!;
        return false;
    }

    public bool IsAccepted(uint questUid)
    {
        return AcceptedQuests.Any(quest => quest.Uid == questUid);
    }

    public PlayerQuestSnapshot WithQuest(PlayerQuestSnapshotEntry quest)
    {
        var updated = false;
        var quests = new PlayerQuestSnapshotEntry[Quests.Count + (ContainsQuest(quest.Uid) ? 0 : 1)];
        var index = 0;
        foreach (var entry in Quests)
        {
            if (entry.Uid == quest.Uid)
            {
                quests[index++] = quest;
                updated = true;
            }
            else
            {
                quests[index++] = entry;
            }
        }

        if (!updated)
        {
            quests[index] = quest;
        }

        return new PlayerQuestSnapshot(quests);
    }

    public PlayerQuestSnapshot WithoutQuest(uint questUid)
    {
        return ContainsQuest(questUid)
            ? new PlayerQuestSnapshot(Quests.Where(quest => quest.Uid != questUid).ToArray())
            : this;
    }
}

public sealed record PlayerQuestSnapshotEntry(
    uint Uid,
    PlayerQuestStatus Status,
    uint Time,
    IReadOnlyList<uint> Intentions);

public enum PlayerQuestStatus : uint
{
    Accepted = 0,
    Finished = 1,
    Available = 2,
}

public sealed class PlayerQuestSnapshotLoader
{
    public const int LegacyQuestHeaderSize = 16;

    private const int UidOffset = 0;
    private const int StatusOffset = UidOffset + sizeof(uint);
    private const int TimeOffset = StatusOffset + sizeof(uint);
    private const int IntentionCountOffset = TimeOffset + sizeof(uint);

    private readonly string _playerQuestsRoot;

    public PlayerQuestSnapshotLoader(string playerQuestsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerQuestsRoot);
        _playerQuestsRoot = playerQuestsRoot;
    }

    public string PlayerQuestsRoot => _playerQuestsRoot;

    public PlayerQuestSnapshot LoadByCharacterUid(long characterUid)
    {
        var path = Path.Combine(
            _playerQuestsRoot,
            characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        return SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                var bytes = SnapshotFilePersistence.ReadAllBytesIfExists(path);
                return bytes is null
                    ? PlayerQuestSnapshot.Empty
                    : Parse(bytes, Path.GetFileName(path));
            });
    }

    public void SaveByCharacterUid(long characterUid, PlayerQuestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var path = Path.Combine(
            _playerQuestsRoot,
            characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var bytes = Serialize(snapshot);
        SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                SnapshotFilePersistence.WriteAllBytesAtomically(path, bytes, ".player-quest-file-");
                return true;
            });
    }

    public static byte[] Serialize(PlayerQuestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var byteCount = sizeof(int);
        foreach (var quest in snapshot.Quests)
        {
            byteCount = checked(byteCount + LegacyQuestHeaderSize + (quest.Intentions.Count * sizeof(uint)));
        }

        var bytes = new byte[byteCount];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, snapshot.Quests.Count);
        var offset = sizeof(int);
        foreach (var quest in snapshot.Quests)
        {
            var header = bytes.AsSpan(offset, LegacyQuestHeaderSize);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(UidOffset, sizeof(uint)), quest.Uid);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(StatusOffset, sizeof(uint)), (uint)quest.Status);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(TimeOffset, sizeof(uint)), quest.Time);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(IntentionCountOffset, sizeof(int)), quest.Intentions.Count);
            offset += LegacyQuestHeaderSize;

            foreach (var intention in quest.Intentions)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), intention);
                offset += sizeof(uint);
            }
        }

        return bytes;
    }

    public static PlayerQuestSnapshot Parse(ReadOnlySpan<byte> bytes, string sourceFile = "")
    {
        if (bytes.Length < sizeof(int))
        {
            throw new FormatException($"Player quest snapshot '{sourceFile}' is missing the quest count.");
        }

        var questCount = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (questCount < 0)
        {
            throw new FormatException($"Player quest snapshot '{sourceFile}' has negative quest count {questCount}.");
        }

        var offset = sizeof(int);
        var quests = new PlayerQuestSnapshotEntry[questCount];
        for (var i = 0; i < questCount; i++)
        {
            EnsureAvailable(bytes, offset, LegacyQuestHeaderSize, sourceFile);
            var uid = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + UidOffset, sizeof(uint)));
            var status = (PlayerQuestStatus)BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.Slice(offset + StatusOffset, sizeof(uint)));
            var time = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + TimeOffset, sizeof(uint)));
            var intentionCount = BinaryPrimitives.ReadInt32LittleEndian(
                bytes.Slice(offset + IntentionCountOffset, sizeof(int)));
            if (intentionCount < 0)
            {
                throw new FormatException(
                    $"Player quest snapshot '{sourceFile}' has negative intention count {intentionCount}.");
            }

            offset += LegacyQuestHeaderSize;
            var intentionsLength = checked(intentionCount * sizeof(uint));
            EnsureAvailable(bytes, offset, intentionsLength, sourceFile);

            var intentions = new uint[intentionCount];
            for (var intentionIndex = 0; intentionIndex < intentions.Length; intentionIndex++)
            {
                intentions[intentionIndex] =
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));
                offset += sizeof(uint);
            }

            quests[i] = new PlayerQuestSnapshotEntry(uid, status, time, intentions);
        }

        if (offset != bytes.Length)
        {
            throw new FormatException(
                $"Player quest snapshot '{sourceFile}' has {bytes.Length - offset} trailing bytes.");
        }

        return new PlayerQuestSnapshot(quests);
    }

    private static void EnsureAvailable(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length,
        string sourceFile)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new FormatException($"Player quest snapshot '{sourceFile}' is truncated.");
        }
    }
}
