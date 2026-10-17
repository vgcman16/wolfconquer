using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class PlayerSpellSnapshotLoaderTests : IDisposable
{
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-player-spells-" + Guid.NewGuid());

    [Fact]
    public void PlayerSpellSnapshotLoaderReadsSpellIdsFromLegacyRecords()
    {
        var snapshot = PlayerSpellSnapshotLoader.Parse(
            BuildPlayerSpellRecords(
            [
                Entry(1000, level: 1, experience: 50, previousLevel: 0, soulLevel: 2, useSpellSoul: 1),
                Entry(10311, level: 3, experience: 75, previousLevel: 1, soulLevel: 4, useSpellSoul: 2),
                Entry(2000, level: 0),
            ]),
            "1293600.bin");

        Assert.Equal([1000U, 10311U, 2000U], snapshot.SpellIds.ToArray());
        Assert.Equal(
        [
            Entry(1000, level: 1, experience: 50, previousLevel: 0, soulLevel: 2, useSpellSoul: 1),
            Entry(10311, level: 3, experience: 75, previousLevel: 1, soulLevel: 4, useSpellSoul: 2),
            Entry(2000, level: 0),
        ], snapshot.Spells);
        Assert.True(snapshot.ContainsSpell(10311));
    }

    [Fact]
    public void PlayerSpellSnapshotLoaderSerializesEmptySnapshotAsZeroCount()
    {
        var bytes = PlayerSpellSnapshotLoader.ToLegacyBytes(PlayerSpellSnapshot.Empty);

        Assert.Equal(new byte[] { 0, 0, 0, 0 }, bytes);
    }

    [Fact]
    public void PlayerSpellSnapshotLoaderSerializesMultipleEntriesThatRoundTrip()
    {
        var spells = new[]
        {
            Entry(1000, level: 1, experience: 50, previousLevel: 0, soulLevel: 2, useSpellSoul: 1),
            Entry(10311, level: 3, experience: 75, previousLevel: 1, soulLevel: 4, useSpellSoul: 2),
            Entry(2000, level: 0),
        };

        var bytes = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(spells));
        var snapshot = PlayerSpellSnapshotLoader.Parse(bytes, "1293600.bin");

        Assert.Equal(BuildPlayerSpellRecords(spells), bytes);
        Assert.Equal(spells, snapshot.Spells);
    }

    [Fact]
    public void PlayerSpellSnapshotLoaderMissingFileReturnsEmptySnapshot()
    {
        Directory.CreateDirectory(_root);
        var loader = new PlayerSpellSnapshotLoader(_root);

        var snapshot = loader.LoadByCharacterUid(1_293_600);

        Assert.Empty(snapshot.SpellIds);
        Assert.False(snapshot.ContainsSpell(10311));
    }

    [Fact]
    public void PlayerSpellSnapshotLoaderSaveByCharacterUidCreatesExpectedFile()
    {
        var loader = new PlayerSpellSnapshotLoader(_root);
        var spells = new[]
        {
            Entry(10311, level: 3, experience: 75, previousLevel: 1, soulLevel: 4, useSpellSoul: 2),
            Entry(2000, level: 0),
        };

        loader.SaveByCharacterUid(1_293_600, new PlayerSpellSnapshot(spells));

        var path = Path.Combine(_root, "1293600.bin");
        Assert.True(File.Exists(path));
        Assert.Equal(BuildPlayerSpellRecords(spells), File.ReadAllBytes(path));
        Assert.Equal(spells, loader.LoadByCharacterUid(1_293_600).Spells);
    }

    [Fact]
    public void PlayerSpellSnapshotLoaderSaveByCharacterUidOverwritesStaleRecords()
    {
        Directory.CreateDirectory(_root);
        var loader = new PlayerSpellSnapshotLoader(_root);
        var path = Path.Combine(_root, "1293600.bin");
        File.WriteAllBytes(
            path,
            BuildPlayerSpellRecords(
            [
                Entry(1000, level: 1),
                Entry(10311, level: 3),
            ]));

        var replacement = new[]
        {
            Entry(2000, level: 0, experience: 125),
        };
        loader.SaveByCharacterUid(1_293_600, new PlayerSpellSnapshot(replacement));

        Assert.Equal(BuildPlayerSpellRecords(replacement), File.ReadAllBytes(path));
        Assert.Equal(replacement, loader.LoadByCharacterUid(1_293_600).Spells);
    }

    [Fact]
    public async Task PlayerSpellSnapshotLoaderLoadAndSaveSerializeWithEquivalentPathLock()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var equivalentPath = Path.Combine(_root, ".", CharacterUid + ".bin");
        var original = BuildPlayerSpellRecords([Entry(1000, level: 1)]);
        File.WriteAllBytes(path, original);
        var loader = new PlayerSpellSnapshotLoader(_root);
        var replacement = new PlayerSpellSnapshot(
        [
            Entry(10311, level: 3, experience: 75, previousLevel: 1, soulLevel: 4, useSpellSoul: 2),
        ]);

        await AssertWaitsForFileLock(
            equivalentPath,
            () => Assert.Single(loader.LoadByCharacterUid(CharacterUid).Spells));
        await AssertWaitsForFileLock(
            equivalentPath,
            () => loader.SaveByCharacterUid(CharacterUid, replacement));

        Assert.Equal(PlayerSpellSnapshotLoader.ToLegacyBytes(replacement), File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task PlayerSpellSnapshotLoaderSaveRetriesTransientDestinationContention()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var original = BuildPlayerSpellRecords([Entry(1000, level: 1)]);
        File.WriteAllBytes(path, original);
        var loader = new PlayerSpellSnapshotLoader(_root);
        var replacement = new PlayerSpellSnapshot(
        [
            Entry(10311, level: 3, experience: 75, previousLevel: 1, soulLevel: 4, useSpellSoul: 2),
        ]);
        using var persistenceStarted = new ManualResetEventSlim();

        Task persistence;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            persistence = StartLongRunning(() =>
            {
                persistenceStarted.Set();
                loader.SaveByCharacterUid(CharacterUid, replacement);
                return true;
            });
            Assert.True(persistenceStarted.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(50);
            Assert.False(persistence.IsCompleted);
        }

        await persistence.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(PlayerSpellSnapshotLoader.ToLegacyBytes(replacement), File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void PlayerSpellSnapshotLoaderSavePersistentContentionPreservesOriginalAndThrows()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var original = BuildPlayerSpellRecords([Entry(1000, level: 1)]);
        File.WriteAllBytes(path, original);
        var loader = new PlayerSpellSnapshotLoader(_root);
        var replacement = new PlayerSpellSnapshot(
        [
            Entry(10311, level: 3, experience: 75, previousLevel: 1, soulLevel: 4, useSpellSoul: 2),
        ]);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<IOException>(() => loader.SaveByCharacterUid(CharacterUid, replacement));
            Assert.Equal(original, File.ReadAllBytes(path));
            AssertNoTransactionArtifacts();
        }

        Assert.Equal(original, File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void PlayerSpellSnapshotLoaderSaveCreatesMissingFileAtomicallyWithExactLegacyBytes()
    {
        var loader = new PlayerSpellSnapshotLoader(_root);
        var snapshot = new PlayerSpellSnapshot(
        [
            Entry(1000, level: 1, experience: 50, soulLevel: 2, useSpellSoul: 1),
            Entry(10311, level: 3, experience: 75, previousLevel: 1, soulLevel: 4, useSpellSoul: 2),
        ]);
        var expected = PlayerSpellSnapshotLoader.ToLegacyBytes(snapshot);

        loader.SaveByCharacterUid(CharacterUid, snapshot);

        Assert.True(Directory.Exists(_root));
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(_root, CharacterUid + ".bin")));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void CharacterSnapshotLoaderDerivesLegacyPlayerSpellsSiblingRoot()
    {
        var usersRoot = Path.Combine(_root, "Users");
        var spellsRoot = Path.Combine(_root, "PlayersSpells");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(spellsRoot);
        File.WriteAllBytes(
            Path.Combine(spellsRoot, "1293600.bin"),
            BuildPlayerSpells([10311]));

        var loader = new CharacterSnapshotLoader(usersRoot);
        var snapshot = loader.LoadPlayerSpellsByCharacterUid(1_293_600);

        Assert.Equal(spellsRoot, loader.PlayerSpellsRoot);
        Assert.Equal([10311U], snapshot.SpellIds.ToArray());
    }

    [Fact]
    public void PlayerSpellSnapshotLoaderRejectsTruncatedFiles()
    {
        var ex = Assert.Throws<FormatException>(
            () => PlayerSpellSnapshotLoader.Parse([1, 0, 0, 0, 0], "bad.bin"));

        Assert.Contains("truncated", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task AssertWaitsForFileLock(string path, Action action)
    {
        using var lockEntered = new ManualResetEventSlim();
        using var releaseLock = new ManualResetEventSlim();
        using var operationStarted = new ManualResetEventSlim();

        var lockHolder = StartLongRunning(() => SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                lockEntered.Set();
                if (!releaseLock.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Timed out waiting to release the player spell file lock.");
                }

                return true;
            }));
        Assert.True(lockEntered.Wait(TimeSpan.FromSeconds(5)));

        var operation = StartLongRunning(() =>
        {
            operationStarted.Set();
            action();
            return true;
        });
        Assert.True(operationStarted.Wait(TimeSpan.FromSeconds(5)));

        try
        {
            await Task.Delay(250);
            Assert.False(operation.IsCompleted);
        }
        finally
        {
            releaseLock.Set();
        }

        Assert.True(await lockHolder.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(await operation.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private void AssertNoTransactionArtifacts()
    {
        Assert.Empty(Directory.EnumerateFiles(
            _root,
            "*.player-spell-file-*",
            SearchOption.TopDirectoryOnly));
    }

    private static Task<TResult> StartLongRunning<TResult>(Func<TResult> action)
    {
        return Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public static byte[] BuildPlayerSpells(IReadOnlyList<uint> spellIds)
    {
        var spells = new PlayerSpellSnapshotEntry[spellIds.Count];
        for (var i = 0; i < spellIds.Count; i++)
        {
            spells[i] = Entry(checked((ushort)spellIds[i]), level: 0);
        }

        return BuildPlayerSpellRecords(spells);
    }

    public static PlayerSpellSnapshotEntry Entry(
        ushort id,
        ushort level,
        int experience = 0,
        byte previousLevel = 0,
        byte soulLevel = 0,
        byte useSpellSoul = 0)
    {
        return new PlayerSpellSnapshotEntry(id, level, experience, previousLevel, soulLevel, useSpellSoul);
    }

    public static byte[] BuildPlayerSpellRecords(IReadOnlyList<PlayerSpellSnapshotEntry> spells)
    {
        var bytes = new byte[sizeof(int) + (spells.Count * PlayerSpellSnapshotLoader.LegacySpellSize)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, sizeof(int)), spells.Count);

        var offset = sizeof(int);
        foreach (var spell in spells)
        {
            var record = bytes.AsSpan(offset, PlayerSpellSnapshotLoader.LegacySpellSize);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(0, sizeof(ushort)), spell.Id);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(2, sizeof(ushort)), spell.Level);
            BinaryPrimitives.WriteInt32LittleEndian(record.Slice(4, sizeof(int)), spell.Experience);
            record[8] = spell.PreviousLevel;
            record[9] = spell.SoulLevel;
            record[10] = spell.UseSpellSoul;
            offset += PlayerSpellSnapshotLoader.LegacySpellSize;
        }

        return bytes;
    }
}
