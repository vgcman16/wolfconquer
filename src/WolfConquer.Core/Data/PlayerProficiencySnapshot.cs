using System.Buffers.Binary;
using System.Globalization;

namespace WolfConquer.Core.Data;

public sealed record PlayerProficiencySnapshot(IReadOnlyList<PlayerProficiencySnapshotEntry> Proficiencies)
{
    public static PlayerProficiencySnapshot Empty { get; } = new([]);
}

public sealed record PlayerProficiencySnapshotEntry(
    uint Id,
    uint Level,
    byte PreviousLevel,
    uint Experience,
    uint NeededExperience);

public sealed class PlayerProficiencySnapshotLoader
{
    public const int LegacyProficiencySize = 20;

    private const int IdOffset = 0;
    private const int LevelOffset = IdOffset + sizeof(uint);
    private const int PreviousLevelOffset = LevelOffset + sizeof(uint);
    private const int ExperienceOffset = 12;
    private const int NeededExperienceOffset = ExperienceOffset + sizeof(uint);

    private readonly string _playerProficienciesRoot;

    public PlayerProficiencySnapshotLoader(string playerProficienciesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerProficienciesRoot);
        _playerProficienciesRoot = playerProficienciesRoot;
    }

    public string PlayerProficienciesRoot => _playerProficienciesRoot;

    public PlayerProficiencySnapshot LoadByCharacterUid(long characterUid)
    {
        var path = GetCharacterSnapshotPath(characterUid);
        return SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                var bytes = SnapshotFilePersistence.ReadAllBytesIfExists(path);
                return bytes is null
                    ? PlayerProficiencySnapshot.Empty
                    : Parse(bytes, Path.GetFileName(path));
            });
    }

    public void SaveByCharacterUid(long characterUid, PlayerProficiencySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var path = GetCharacterSnapshotPath(characterUid);
        var bytes = ToLegacyBytes(snapshot);
        SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                SnapshotFilePersistence.WriteAllBytesAtomically(
                    path,
                    bytes,
                    ".player-proficiency-file-");
                return true;
            });
    }

    public static byte[] ToLegacyBytes(PlayerProficiencySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return ToLegacyBytes(snapshot.Proficiencies);
    }

    public static byte[] ToLegacyBytes(IReadOnlyList<PlayerProficiencySnapshotEntry> proficiencies)
    {
        ArgumentNullException.ThrowIfNull(proficiencies);

        var bytes = new byte[checked(sizeof(int) + (proficiencies.Count * LegacyProficiencySize))];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, sizeof(int)), proficiencies.Count);

        var offset = sizeof(int);
        foreach (var proficiency in proficiencies)
        {
            WriteLegacyRecord(bytes.AsSpan(offset, LegacyProficiencySize), proficiency);
            offset += LegacyProficiencySize;
        }

        return bytes;
    }

    public static PlayerProficiencySnapshot Parse(ReadOnlySpan<byte> bytes, string sourceFile = "")
    {
        if (bytes.Length < sizeof(int))
        {
            throw new FormatException($"Player proficiency snapshot '{sourceFile}' is missing the proficiency count.");
        }

        var proficiencyCount = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (proficiencyCount < 0)
        {
            throw new FormatException(
                $"Player proficiency snapshot '{sourceFile}' has negative proficiency count {proficiencyCount}.");
        }

        var expectedLength = checked(sizeof(int) + (proficiencyCount * LegacyProficiencySize));
        if (bytes.Length < expectedLength)
        {
            throw new FormatException($"Player proficiency snapshot '{sourceFile}' is truncated.");
        }

        if (bytes.Length != expectedLength)
        {
            throw new FormatException(
                $"Player proficiency snapshot '{sourceFile}' has {bytes.Length - expectedLength} trailing bytes.");
        }

        var offset = sizeof(int);
        var proficiencies = new PlayerProficiencySnapshotEntry[proficiencyCount];
        for (var i = 0; i < proficiencyCount; i++)
        {
            proficiencies[i] = new PlayerProficiencySnapshotEntry(
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + IdOffset, sizeof(uint))),
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + LevelOffset, sizeof(uint))),
                bytes[offset + PreviousLevelOffset],
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + ExperienceOffset, sizeof(uint))),
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + NeededExperienceOffset, sizeof(uint))));
            offset += LegacyProficiencySize;
        }

        return new PlayerProficiencySnapshot(proficiencies);
    }

    private string GetCharacterSnapshotPath(long characterUid)
    {
        return Path.Combine(
            _playerProficienciesRoot,
            characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
    }

    private static void WriteLegacyRecord(Span<byte> record, PlayerProficiencySnapshotEntry proficiency)
    {
        record.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(IdOffset, sizeof(uint)), proficiency.Id);
        BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(LevelOffset, sizeof(uint)), proficiency.Level);
        record[PreviousLevelOffset] = proficiency.PreviousLevel;
        BinaryPrimitives.WriteUInt32LittleEndian(
            record.Slice(ExperienceOffset, sizeof(uint)),
            proficiency.Experience);
        BinaryPrimitives.WriteUInt32LittleEndian(
            record.Slice(NeededExperienceOffset, sizeof(uint)),
            proficiency.NeededExperience);
    }
}
