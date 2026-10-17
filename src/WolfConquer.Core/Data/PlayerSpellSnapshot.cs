using System.Buffers.Binary;
using System.Globalization;

namespace WolfConquer.Core.Data;

public sealed record PlayerSpellSnapshot(IReadOnlyList<PlayerSpellSnapshotEntry> Spells)
{
    public static PlayerSpellSnapshot Empty { get; } = new([]);

    public IReadOnlyList<uint> SpellIds { get; } = Spells.Select(spell => (uint)spell.Id).ToArray();

    public bool ContainsSpell(uint spellId)
    {
        return Spells.Any(spell => spell.Id == spellId);
    }
}

public sealed record PlayerSpellSnapshotEntry(
    ushort Id,
    ushort Level,
    int Experience,
    byte PreviousLevel,
    byte SoulLevel,
    byte UseSpellSoul);

public sealed class PlayerSpellSnapshotLoader
{
    public const int LegacySpellSize = 12;

    private const int SpellIdOffset = 0;
    private const int LevelOffset = SpellIdOffset + sizeof(ushort);
    private const int ExperienceOffset = LevelOffset + sizeof(ushort);
    private const int PreviousLevelOffset = ExperienceOffset + sizeof(int);
    private const int SoulLevelOffset = PreviousLevelOffset + sizeof(byte);
    private const int UseSpellSoulOffset = SoulLevelOffset + sizeof(byte);

    private readonly string _playerSpellsRoot;

    public PlayerSpellSnapshotLoader(string playerSpellsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerSpellsRoot);
        _playerSpellsRoot = playerSpellsRoot;
    }

    public string PlayerSpellsRoot => _playerSpellsRoot;

    public PlayerSpellSnapshot LoadByCharacterUid(long characterUid)
    {
        var path = GetCharacterSnapshotPath(characterUid);
        return SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                var bytes = SnapshotFilePersistence.ReadAllBytesIfExists(path);
                return bytes is null
                    ? PlayerSpellSnapshot.Empty
                    : Parse(bytes, Path.GetFileName(path));
            });
    }

    public void SaveByCharacterUid(long characterUid, PlayerSpellSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var path = GetCharacterSnapshotPath(characterUid);
        var bytes = ToLegacyBytes(snapshot);
        SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                SnapshotFilePersistence.WriteAllBytesAtomically(path, bytes, ".player-spell-file-");
                return true;
            });
    }

    public static byte[] ToLegacyBytes(PlayerSpellSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return ToLegacyBytes(snapshot.Spells);
    }

    public static byte[] ToLegacyBytes(IReadOnlyList<PlayerSpellSnapshotEntry> spells)
    {
        ArgumentNullException.ThrowIfNull(spells);

        var bytes = new byte[checked(sizeof(int) + (spells.Count * LegacySpellSize))];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, sizeof(int)), spells.Count);

        var offset = sizeof(int);
        foreach (var spell in spells)
        {
            WriteLegacyRecord(bytes.AsSpan(offset, LegacySpellSize), spell);
            offset += LegacySpellSize;
        }

        return bytes;
    }

    public static PlayerSpellSnapshot Parse(ReadOnlySpan<byte> bytes, string sourceFile = "")
    {
        if (bytes.Length < sizeof(int))
        {
            throw new FormatException($"Player spell snapshot '{sourceFile}' is missing the spell count.");
        }

        var spellCount = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (spellCount < 0)
        {
            throw new FormatException($"Player spell snapshot '{sourceFile}' has negative spell count {spellCount}.");
        }

        var expectedLength = checked(sizeof(int) + (spellCount * LegacySpellSize));
        if (bytes.Length < expectedLength)
        {
            throw new FormatException($"Player spell snapshot '{sourceFile}' is truncated.");
        }

        if (bytes.Length != expectedLength)
        {
            throw new FormatException(
                $"Player spell snapshot '{sourceFile}' has {bytes.Length - expectedLength} trailing bytes.");
        }

        var offset = sizeof(int);
        var spells = new PlayerSpellSnapshotEntry[spellCount];
        for (var i = 0; i < spellCount; i++)
        {
            spells[i] = new PlayerSpellSnapshotEntry(
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + SpellIdOffset, sizeof(ushort))),
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + LevelOffset, sizeof(ushort))),
                BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + ExperienceOffset, sizeof(int))),
                bytes[offset + PreviousLevelOffset],
                bytes[offset + SoulLevelOffset],
                bytes[offset + UseSpellSoulOffset]);
            offset += LegacySpellSize;
        }

        return new PlayerSpellSnapshot(spells);
    }

    private string GetCharacterSnapshotPath(long characterUid)
    {
        return Path.Combine(
            _playerSpellsRoot,
            characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
    }

    private static void WriteLegacyRecord(Span<byte> record, PlayerSpellSnapshotEntry spell)
    {
        record.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(SpellIdOffset, sizeof(ushort)), spell.Id);
        BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(LevelOffset, sizeof(ushort)), spell.Level);
        BinaryPrimitives.WriteInt32LittleEndian(record.Slice(ExperienceOffset, sizeof(int)), spell.Experience);
        record[PreviousLevelOffset] = spell.PreviousLevel;
        record[SoulLevelOffset] = spell.SoulLevel;
        record[UseSpellSoulOffset] = spell.UseSpellSoul;
    }
}
