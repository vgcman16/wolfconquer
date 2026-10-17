using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class PlayerProficiencySnapshotLoaderTests : IDisposable
{
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-player-profs-" + Guid.NewGuid());

    [Fact]
    public void PlayerProficiencySnapshotLoaderReadsLegacyRecords()
    {
        var snapshot = PlayerProficiencySnapshotLoader.Parse(
            BuildPlayerProficiencies(
            [
                Entry(410, level: 12, previousLevel: 8, experience: 123_456, neededExperience: 99),
                Entry(420, level: 5, previousLevel: 0, experience: 44, neededExperience: 55),
            ]),
            "1293600.bin");

        Assert.Equal(
        [
            Entry(410, level: 12, previousLevel: 8, experience: 123_456, neededExperience: 99),
            Entry(420, level: 5, previousLevel: 0, experience: 44, neededExperience: 55),
        ], snapshot.Proficiencies);
    }

    [Fact]
    public void PlayerProficiencySnapshotLoaderSerializesEmptySnapshotAsZeroCount()
    {
        var bytes = PlayerProficiencySnapshotLoader.ToLegacyBytes(PlayerProficiencySnapshot.Empty);

        Assert.Equal(new byte[] { 0, 0, 0, 0 }, bytes);
    }

    [Fact]
    public void PlayerProficiencySnapshotLoaderSerializesMultipleEntriesThatRoundTrip()
    {
        var proficiencies = new[]
        {
            Entry(410, level: 12, previousLevel: 8, experience: 123_456, neededExperience: 99),
            Entry(420, level: 5, previousLevel: 0, experience: 44, neededExperience: 55),
        };

        var bytes = PlayerProficiencySnapshotLoader.ToLegacyBytes(new PlayerProficiencySnapshot(proficiencies));
        var snapshot = PlayerProficiencySnapshotLoader.Parse(bytes, "1293600.bin");

        Assert.Equal(BuildPlayerProficiencies(proficiencies), bytes);
        Assert.Equal(proficiencies, snapshot.Proficiencies);
    }

    [Fact]
    public void PlayerProficiencySnapshotLoaderMissingFileReturnsEmptySnapshot()
    {
        Directory.CreateDirectory(_root);
        var loader = new PlayerProficiencySnapshotLoader(_root);

        var snapshot = loader.LoadByCharacterUid(1_293_600);

        Assert.Empty(snapshot.Proficiencies);
    }

    [Fact]
    public void PlayerProficiencySnapshotLoaderSaveByCharacterUidCreatesExpectedFile()
    {
        var loader = new PlayerProficiencySnapshotLoader(_root);
        var proficiencies = new[]
        {
            Entry(410, level: 12, previousLevel: 8, experience: 123_456, neededExperience: 99),
            Entry(420, level: 5, previousLevel: 0, experience: 44, neededExperience: 55),
        };

        loader.SaveByCharacterUid(1_293_600, new PlayerProficiencySnapshot(proficiencies));

        var path = Path.Combine(_root, "1293600.bin");
        Assert.True(File.Exists(path));
        Assert.Equal(BuildPlayerProficiencies(proficiencies), File.ReadAllBytes(path));
        Assert.Equal(proficiencies, loader.LoadByCharacterUid(1_293_600).Proficiencies);
    }

    [Fact]
    public void PlayerProficiencySnapshotLoaderSaveByCharacterUidOverwritesStaleRecords()
    {
        Directory.CreateDirectory(_root);
        var loader = new PlayerProficiencySnapshotLoader(_root);
        var path = Path.Combine(_root, "1293600.bin");
        File.WriteAllBytes(
            path,
            BuildPlayerProficiencies(
            [
                Entry(410, level: 12),
                Entry(420, level: 5),
            ]));

        var replacement = new[]
        {
            Entry(601, level: 9, previousLevel: 3, experience: 777, neededExperience: 888),
        };
        loader.SaveByCharacterUid(1_293_600, new PlayerProficiencySnapshot(replacement));

        Assert.Equal(BuildPlayerProficiencies(replacement), File.ReadAllBytes(path));
        Assert.Equal(replacement, loader.LoadByCharacterUid(1_293_600).Proficiencies);
    }

    [Fact]
    public async Task PlayerProficiencySnapshotLoaderLoadAndSaveSerializeWithEquivalentPathLock()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var equivalentPath = Path.Combine(_root, ".", CharacterUid + ".bin");
        var original = BuildPlayerProficiencies([Entry(410, level: 12)]);
        File.WriteAllBytes(path, original);
        var loader = new PlayerProficiencySnapshotLoader(_root);
        var replacement = new PlayerProficiencySnapshot(
        [
            Entry(601, level: 9, previousLevel: 3, experience: 777, neededExperience: 888),
        ]);

        await AssertWaitsForFileLock(
            equivalentPath,
            () => Assert.Single(loader.LoadByCharacterUid(CharacterUid).Proficiencies));
        await AssertWaitsForFileLock(
            equivalentPath,
            () => loader.SaveByCharacterUid(CharacterUid, replacement));

        Assert.Equal(PlayerProficiencySnapshotLoader.ToLegacyBytes(replacement), File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task PlayerProficiencySnapshotLoaderSaveRetriesTransientDestinationContention()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var original = BuildPlayerProficiencies([Entry(410, level: 12)]);
        File.WriteAllBytes(path, original);
        var loader = new PlayerProficiencySnapshotLoader(_root);
        var replacement = new PlayerProficiencySnapshot(
        [
            Entry(601, level: 9, previousLevel: 3, experience: 777, neededExperience: 888),
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
        Assert.Equal(PlayerProficiencySnapshotLoader.ToLegacyBytes(replacement), File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void PlayerProficiencySnapshotLoaderSavePersistentContentionPreservesOriginalAndThrows()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var original = BuildPlayerProficiencies([Entry(410, level: 12)]);
        File.WriteAllBytes(path, original);
        var loader = new PlayerProficiencySnapshotLoader(_root);
        var replacement = new PlayerProficiencySnapshot(
        [
            Entry(601, level: 9, previousLevel: 3, experience: 777, neededExperience: 888),
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
    public void PlayerProficiencySnapshotLoaderSaveCreatesMissingFileAtomicallyWithExactLegacyBytes()
    {
        var loader = new PlayerProficiencySnapshotLoader(_root);
        var snapshot = new PlayerProficiencySnapshot(
        [
            Entry(410, level: 12, previousLevel: 8, experience: 123_456, neededExperience: 99),
            Entry(601, level: 9, previousLevel: 3, experience: 777, neededExperience: 888),
        ]);
        var expected = PlayerProficiencySnapshotLoader.ToLegacyBytes(snapshot);

        loader.SaveByCharacterUid(CharacterUid, snapshot);

        Assert.True(Directory.Exists(_root));
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(_root, CharacterUid + ".bin")));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void CharacterSnapshotLoaderDerivesLegacyPlayerProficienciesSiblingRoot()
    {
        var usersRoot = Path.Combine(_root, "Users");
        var profsRoot = Path.Combine(_root, "PlayersProfs");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(profsRoot);
        File.WriteAllBytes(
            Path.Combine(profsRoot, "1293600.bin"),
            BuildPlayerProficiencies([Entry(410, level: 12)]));

        var loader = new CharacterSnapshotLoader(usersRoot);
        var snapshot = loader.LoadPlayerProficienciesByCharacterUid(1_293_600);

        Assert.Equal(profsRoot, loader.PlayerProficienciesRoot);
        Assert.Equal([Entry(410, level: 12)], snapshot.Proficiencies);
    }

    [Fact]
    public void PlayerProficiencySnapshotLoaderRejectsTruncatedFiles()
    {
        var ex = Assert.Throws<FormatException>(
            () => PlayerProficiencySnapshotLoader.Parse([1, 0, 0, 0, 0], "bad.bin"));

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
                    throw new TimeoutException("Timed out waiting to release the player proficiency file lock.");
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
            "*.player-proficiency-file-*",
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

    public static PlayerProficiencySnapshotEntry Entry(
        uint id,
        uint level,
        byte previousLevel = 0,
        uint experience = 0,
        uint neededExperience = 0)
    {
        return new PlayerProficiencySnapshotEntry(id, level, previousLevel, experience, neededExperience);
    }

    public static byte[] BuildPlayerProficiencies(IReadOnlyList<PlayerProficiencySnapshotEntry> proficiencies)
    {
        var bytes = new byte[sizeof(int) + (proficiencies.Count * PlayerProficiencySnapshotLoader.LegacyProficiencySize)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, sizeof(int)), proficiencies.Count);

        var offset = sizeof(int);
        foreach (var proficiency in proficiencies)
        {
            var record = bytes.AsSpan(offset, PlayerProficiencySnapshotLoader.LegacyProficiencySize);
            BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(0, sizeof(uint)), proficiency.Id);
            BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(4, sizeof(uint)), proficiency.Level);
            record[8] = proficiency.PreviousLevel;
            BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(12, sizeof(uint)), proficiency.Experience);
            BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(16, sizeof(uint)), proficiency.NeededExperience);
            offset += PlayerProficiencySnapshotLoader.LegacyProficiencySize;
        }

        return bytes;
    }
}
