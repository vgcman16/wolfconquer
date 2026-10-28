using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    private readonly WorldSessionStore store;
    private readonly ConquerTcpConnection connection;
    private readonly WorldSession expected;
    private readonly object identity;
    private readonly LegacyOrdinarySpellBinding? binding;
    private readonly LegacySavedSpellImage baseline;
    private readonly byte[]? before;
    private readonly PlayerSpellSnapshotEntry[] savedRows;
    private LegacySavedSpellImage? prepared;
    private byte[]? after;
    private bool committed;
    internal readonly LegacyTradeRecoveryState Recovery;
    internal readonly string UsersRoot, SpellsRoot, SpellsPath;
    private string CharacterPath => Path.Combine(UsersRoot, expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
    internal byte[]? CopyBeforeImage() => before?.ToArray();
    internal byte[] CopyAfterImage() => after?.ToArray() ?? throw Unavailable();

    private LegacyPreparedSpellFileWrite(WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected,
        object identity, LegacyOrdinarySpellBinding? binding, LegacyTradeRecoveryState recovery, string usersRoot, string spellsRoot)
    {
        this.store = store; this.connection = connection; this.expected = expected; this.identity = identity; this.binding = binding;
        Recovery = recovery;
        if (expected.AccountId is <= 0 or > uint.MaxValue || expected.Character.Uid is <= 0 or > uint.MaxValue ||
            !Path.IsPathFullyQualified(usersRoot) || !Path.IsPathFullyQualified(spellsRoot)) throw Unavailable();
        UsersRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(usersRoot));
        SpellsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(spellsRoot));
        if (!recovery.IsReadyForCharacterSpells(UsersRoot, SpellsRoot) || recovery.IsBlocked ||
            !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected.UsersRoot!)), UsersRoot, PathComparison) ||
            binding is not null && (!ReferenceEquals(binding.Recovery, recovery) ||
                !string.Equals(binding.UsersRoot, UsersRoot, PathComparison) || !string.Equals(binding.SpellsRoot, SpellsRoot, PathComparison)))
            throw Unavailable();
        SpellsPath = Path.Combine(SpellsRoot, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        before = SnapshotFileLockCoordinator.ExecuteLocked(SpellsPath, () => ReadImage(SpellsPath));
        baseline = binding?.SavedSpells ?? new LegacySavedSpellImage(before);
        if (!baseline.Matches(before)) throw Unavailable();
        savedRows = before is null ? [] : PlayerSpellSnapshotLoader.Parse(before).Spells.ToArray();
        // Unbound managed profiles have no prior issued saved-image anchor.
        // Their actual disk must still match current membership at capture.
        if (binding is null && !PlayerSpellSnapshotLoader.ToLegacyBytes(expected.PlayerSpells).AsSpan()
                .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(savedRows)))) throw Unavailable();
        if (!OwnerIsCurrentPlain()) throw Unavailable();
    }

    internal static LegacyPreparedSpellFileWrite Capture(WorldSessionStore store, ConquerTcpConnection connection,
        WorldSession expected, object identity, LegacyOrdinarySpellBinding? binding, LegacyTradeRecoveryState recovery,
        string usersRoot, string spellsRoot) => new(store, connection, expected, identity, binding, recovery, usersRoot, spellsRoot);

    internal void PrepareImage(PlayerSpellSnapshot proposed)
    {
        if (prepared is not null || committed) throw Unavailable();
        var frozen = LegacyNativeSpellStorage.FreezeSessionSnapshot(proposed);
        after = PlayerSpellSnapshotLoader.ToLegacyBytes(frozen);
        var current = LegacyNativeSpellStorage.FreezeSessionSnapshot(expected.PlayerSpells).Spells.ToDictionary(row => row.Id);
        foreach (var row in frozen.Spells)
            if (!current.TryGetValue(row.Id, out var prior) || prior != row)
                _ = LegacySpellPacket.FromSnapshot(row).ToFrameBytes();
        var offsets = savedRows.Select((row, index) => (row.Id, Offset: sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize))
            .ToDictionary(row => row.Id, row => row.Offset);
        for (var index = 0; index < frozen.Spells.Count; index++)
            if (before is not null && offsets.TryGetValue(frozen.Spells[index].Id, out var original))
                after[sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize + 11] = before[original + 11];
        prepared = new LegacySavedSpellImage(after);
    }

    internal LegacyTradeJournalChange CaptureUnchangedCharacter()
    {
        return SnapshotFileLockCoordinator.ExecuteLocked(CharacterPath, () =>
        {
            if (!CharacterSnapshotFileBaseline.TryReadBytes(CharacterPath, "ordinarySpellSave", out var bytes, out _)) throw Unavailable();
            var row = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(bytes, out _));
            if (row.Uid != expected.Character.Uid) throw Unavailable();
            return new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Character, expected.AccountId, bytes, bytes);
        });
    }

    // I/O preflight only; consume native raw witnesses AFTER this method.
    internal bool DiskImageIsCurrent() => OwnerIsCurrentPlain() &&
        Recovery.IsReadyForCharacterSpells(UsersRoot, SpellsRoot) &&
        SnapshotFileLockCoordinator.ExecuteLocked(SpellsPath, () => baseline.Matches(ReadImage(SpellsPath)));

    internal bool MatchesBeforeImage(byte[]? image) => baseline.Matches(image);
    internal bool OwnerIsCurrentPlain() => !committed && !Recovery.IsBlocked &&
        store.IsSpellFileWriteOwnerCurrent(connection, expected, identity, binding) &&
        (binding is null || ReferenceEquals(binding.SavedSpells, baseline));

    private void PublishCommittedEvidencePlain()
    {
        binding?.PublishSavedSpells(prepared!);
        committed = true;
    }
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static InvalidOperationException Unavailable() => new("ordinarySpellSavedImageRequiresCurrentOwner");

    private static byte[]? ReadImage(string path)
    {
        try
        {
            var file = new FileInfo(Path.GetFullPath(path)); file.Refresh();
            if (file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0) throw Unavailable();
            for (var parent = file.Directory; parent is not null; parent = parent.Parent)
            {
                parent.Refresh();
                if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw Unavailable();
            }
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw Unavailable();
            var value = new byte[checked((int)stream.Length)]; stream.ReadExactly(value);
            if (stream.ReadByte() != -1 || stream.Length != value.Length) throw Unavailable();
            return value;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
}
