using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class PlayerQuestSnapshotLoaderTests : IDisposable
{
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-player-quests-" + Guid.NewGuid());

    [Fact]
    public void PlayerQuestSnapshotLoaderReadsAcceptedQuestWithTwoIntentions()
    {
        var snapshot = PlayerQuestSnapshotLoader.Parse(AcceptedQuestFixture(), "1293600.bin");

        var quest = Assert.Single(snapshot.Quests);
        Assert.Equal(35_024U, quest.Uid);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal(123_456U, quest.Time);
        Assert.Equal([7U, 9U], quest.Intentions.ToArray());
        Assert.Same(quest, Assert.Single(snapshot.AcceptedQuests));
        Assert.True(snapshot.ContainsQuest(35_024));
        Assert.True(snapshot.IsAccepted(35_024));
    }

    [Fact]
    public void PlayerQuestSnapshotLoaderMissingFileReturnsEmptySnapshot()
    {
        Directory.CreateDirectory(_root);
        var loader = new PlayerQuestSnapshotLoader(_root);

        var snapshot = loader.LoadByCharacterUid(1_293_600);

        Assert.Same(PlayerQuestSnapshot.Empty, snapshot);
        Assert.Empty(snapshot.Quests);
        Assert.Empty(snapshot.AcceptedQuests);
    }

    [Fact]
    public void PlayerQuestSnapshotLoaderSaveByCharacterUidCreatesRoundTrippableLegacyFile()
    {
        var loader = new PlayerQuestSnapshotLoader(_root);
        var initial = new PlayerQuestSnapshot([
            new PlayerQuestSnapshotEntry(35_024, PlayerQuestStatus.Accepted, 123_456, [7, 9]),
        ]);
        var replacement = new PlayerQuestSnapshot([
            new PlayerQuestSnapshotEntry(2_001, PlayerQuestStatus.Finished, 12, [3]),
            new PlayerQuestSnapshotEntry(5, PlayerQuestStatus.Available, 99, []),
        ]);

        loader.SaveByCharacterUid(1_293_600, initial);
        loader.SaveByCharacterUid(1_293_600, replacement);

        var reloaded = loader.LoadByCharacterUid(1_293_600);
        Assert.Equal(replacement.Quests.Select(QuestKey).ToArray(), reloaded.Quests.Select(QuestKey).ToArray());
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(replacement), File.ReadAllBytes(Path.Combine(_root, "1293600.bin")));
    }

    [Fact]
    public async Task PlayerQuestSnapshotLoaderLoadAndSaveSerializeWithEquivalentPathLock()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var equivalentPath = Path.Combine(_root, ".", CharacterUid + ".bin");
        File.WriteAllBytes(path, AcceptedQuestFixture());
        var loader = new PlayerQuestSnapshotLoader(_root);
        var replacement = new PlayerQuestSnapshot(
        [
            new PlayerQuestSnapshotEntry(2_001, PlayerQuestStatus.Finished, 12, [3]),
        ]);

        await AssertWaitsForFileLock(
            equivalentPath,
            () => Assert.Single(loader.LoadByCharacterUid(CharacterUid).Quests));
        await AssertWaitsForFileLock(
            equivalentPath,
            () => loader.SaveByCharacterUid(CharacterUid, replacement));

        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(replacement), File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task PlayerQuestSnapshotLoaderSaveRetriesTransientDestinationContention()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var original = AcceptedQuestFixture();
        File.WriteAllBytes(path, original);
        var loader = new PlayerQuestSnapshotLoader(_root);
        var replacement = new PlayerQuestSnapshot(
        [
            new PlayerQuestSnapshotEntry(2_001, PlayerQuestStatus.Finished, 12, [3]),
        ]);
        var retryObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownership = StartLongRunning(() =>
        {
            // Keep the lock/release phase off xUnit's async continuation queue.
            using var persistenceStarted = new ManualResetEventSlim();
            using var watcher = new FileSystemWatcher(_root, CharacterUid + ".bin.player-quest-file-*.tmp")
            {
                NotifyFilter = NotifyFilters.FileName,
            };
            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            // Removing a real transaction temp while this lock is held proves a failed
            // replace attempt reached production cleanup, rather than merely starting Save.
            watcher.Deleted += (_, _) => retryObserved.TrySetResult(true);
            watcher.EnableRaisingEvents = true;
            var persistence = StartLongRunning(() =>
            {
                persistenceStarted.Set();
                loader.SaveByCharacterUid(CharacterUid, replacement);
                return true;
            });
            bool started;
            bool sawRetry;
            bool completedWhileLocked;
            byte[] whileLocked;
            bool saved;
            try
            {
                started = persistenceStarted.Wait(TimeSpan.FromSeconds(5));
                sawRetry = retryObserved.Task.Wait(TimeSpan.FromSeconds(5));
                completedWhileLocked = persistence.IsCompleted;
                whileLocked = File.ReadAllBytes(path);
            }
            finally
            {
                locked.Dispose();
                // Drain the owned writer even when an observation fails, before fixture cleanup.
                saved = persistence.GetAwaiter().GetResult();
            }

            Assert.True(started);
            Assert.True(sawRetry, "A failed real replace attempt must delete its transaction temp.");
            Assert.False(completedWhileLocked);
            Assert.Equal(original, whileLocked);
            return saved;
        });

        Assert.True(await ownership.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(replacement), File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void PlayerQuestSnapshotLoaderSavePersistentContentionPreservesOriginalAndThrows()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var original = AcceptedQuestFixture();
        File.WriteAllBytes(path, original);
        var loader = new PlayerQuestSnapshotLoader(_root);
        var replacement = new PlayerQuestSnapshot(
        [
            new PlayerQuestSnapshotEntry(2_001, PlayerQuestStatus.Finished, 12, [3]),
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
    public void PlayerQuestSnapshotLoaderSaveCreatesMissingFileAtomicallyWithExactLegacyBytes()
    {
        var loader = new PlayerQuestSnapshotLoader(_root);
        var snapshot = new PlayerQuestSnapshot(
        [
            new PlayerQuestSnapshotEntry(35_024, PlayerQuestStatus.Accepted, 123_456, [7, 9]),
            new PlayerQuestSnapshotEntry(2_001, PlayerQuestStatus.Finished, 12, [3]),
            new PlayerQuestSnapshotEntry(5, PlayerQuestStatus.Available, 99, []),
        ]);
        var expected = PlayerQuestSnapshotLoader.Serialize(snapshot);

        loader.SaveByCharacterUid(CharacterUid, snapshot);

        Assert.True(Directory.Exists(_root));
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(_root, CharacterUid + ".bin")));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void CharacterSnapshotLoaderDerivesLegacyPlayerQuestsSiblingRootAndAttachesToSnapshot()
    {
        var usersRoot = Path.Combine(_root, "Users");
        var questsRoot = Path.Combine(_root, "Quests");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(questsRoot);
        WriteSnapshot(usersRoot, 1_293_600, uid: 1_293_600);
        File.WriteAllBytes(Path.Combine(questsRoot, "1293600.bin"), AcceptedQuestFixture());

        var loader = new CharacterSnapshotLoader(usersRoot);
        var snapshot = loader.LoadByAccountId(1_293_600);

        Assert.NotNull(snapshot);
        Assert.Equal(questsRoot, loader.PlayerQuestsRoot);
        var quest = Assert.Single(snapshot.PlayerQuests.Quests);
        Assert.Equal(35_024U, quest.Uid);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal([7U, 9U], quest.Intentions.ToArray());
    }

    [Fact]
    public void PlayerQuestSnapshotLoaderRejectsTruncatedFiles()
    {
        var ex = Assert.Throws<FormatException>(
            () => PlayerQuestSnapshotLoader.Parse([1, 0, 0, 0, 0], "bad.bin"));

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
                    throw new TimeoutException("Timed out waiting to release the player quest file lock.");
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
            "*.player-quest-file-*",
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

    private static byte[] AcceptedQuestFixture()
    {
        return Convert.FromHexString(
            "01000000" +
            "D0880000" +
            "00000000" +
            "40E20100" +
            "02000000" +
            "07000000" +
            "09000000");
    }

    public static byte[] BuildPlayerQuests(IReadOnlyList<PlayerQuestSnapshotEntry> quests)
    {
        var intentionCount = quests.Sum(quest => quest.Intentions.Count);
        var bytes = new byte[
            sizeof(int) +
            (quests.Count * PlayerQuestSnapshotLoader.LegacyQuestHeaderSize) +
            (intentionCount * sizeof(uint))];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, sizeof(int)), quests.Count);

        var offset = sizeof(int);
        foreach (var quest in quests)
        {
            var header = bytes.AsSpan(offset, PlayerQuestSnapshotLoader.LegacyQuestHeaderSize);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, sizeof(uint)), quest.Uid);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, sizeof(uint)), (uint)quest.Status);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, sizeof(uint)), quest.Time);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, sizeof(int)), quest.Intentions.Count);
            offset += PlayerQuestSnapshotLoader.LegacyQuestHeaderSize;

            foreach (var intention in quest.Intentions)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), intention);
                offset += sizeof(uint);
            }
        }

        return bytes;
    }

    private static (uint Uid, PlayerQuestStatus Status, uint Time, string Intentions) QuestKey(
        PlayerQuestSnapshotEntry quest)
    {
        return (quest.Uid, quest.Status, quest.Time, string.Join(",", quest.Intentions));
    }

    private static void WriteSnapshot(string usersRoot, long accountId, long uid)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            PkPoints=0
            Online=7
            """);
    }
}
