using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class CombatGearSnapshotLoaderTests : IDisposable
{
    private const long CharacterUid = 1_293_600;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-combat-gear-" + Guid.NewGuid());

    [Fact]
    public void CombatGearSnapshotLoaderReadsLegacyPlayersCombatGearIni()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "1293600.ini");
        File.WriteAllText(
            path,
            """
            [CombatGear]
            MasteryPoints=11
            TaoistReiki=22
            PirateExp=33
            Lottery=12
            Count=2
            [AirPowers]
            SenseLevel=9
            ArmedLevel=8
            OverLoadLevel=7
            Points=92305
            SenseAipPower=10000
            ArmedAipPower=9000
            OverLoadAipPower=8000
            [Item0]
            ItemID=701
            Level=4
            Exp=123
            Equip=0
            Transform=2
            MasteryScore=928000
            Animas[0]0=101
            Animas[0]1=102
            Animas[0]2=103
            Animas[0]3=104
            Animas[0]4=105
            Animas[0]5=106
            Animas[0]6=107
            Animas[1]0=201
            Animas[1]1=202
            Animas[1]2=203
            Animas[1]3=204
            Animas[1]4=205
            Animas[1]5=206
            Animas[1]6=207
            BlackJades0=16907
            BlackJades1=1907
            BlackJades2=19907
            BlackJades3=14907
            BlackJades4=3907
            BlackJades5=2907
            [Item1]
            ItemID=702
            Level=3
            Exp=456
            Equip=1
            Transform=1
            MasteryScore=344
            Animas[0]0=301
            Animas[0]1=302
            Animas[0]2=303
            Animas[0]3=304
            Animas[0]4=305
            Animas[0]5=306
            Animas[0]6=307
            Animas[1]0=401
            Animas[1]1=402
            Animas[1]2=403
            Animas[1]3=404
            Animas[1]4=405
            Animas[1]5=406
            Animas[1]6=407
            BlackJades0=6907
            BlackJades1=5907
            BlackJades2=8907
            BlackJades3=7907
            BlackJades4=15907
            BlackJades5=22907
            [BlackJadeBag]
            Count=3
            BlackJade0=18907
            BlackJade1=8907
            BlackJade2=25907
            """);

        var snapshot = new CombatGearSnapshotLoader(_root).LoadByCharacterUid(1_293_600);

        Assert.Equal(11U, snapshot.MasteryPoints);
        Assert.Equal(22U, snapshot.TaoistReiki);
        Assert.Equal(33U, snapshot.PirateExp);
        Assert.Equal(12U, snapshot.Lottery);
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(9U, snapshot.AirPowers.SenseLevel);
        Assert.Equal(8U, snapshot.AirPowers.ArmedLevel);
        Assert.Equal(7U, snapshot.AirPowers.OverLoadLevel);
        Assert.Equal(92305U, snapshot.AirPowers.Points);
        Assert.Equal(10000U, snapshot.AirPowers.SenseAipPower);
        Assert.Equal(9000U, snapshot.AirPowers.ArmedAipPower);
        Assert.Equal(8000U, snapshot.AirPowers.OverLoadAipPower);
        Assert.Equal(2, snapshot.Items.Count);

        var first = snapshot.Items[0];
        Assert.Equal(701U, first.ItemID);
        Assert.Equal(4U, first.Level);
        Assert.Equal(123U, first.Exp);
        Assert.Equal(0U, first.Equip);
        Assert.Equal(2U, first.Transform);
        Assert.Equal(928000U, first.MasteryScore);
        Assert.Equal([101U, 102U, 103U, 104U, 105U, 106U, 107U], first.Animas[0]);
        Assert.Equal([201U, 202U, 203U, 204U, 205U, 206U, 207U], first.Animas[1]);
        Assert.Equal([16907U, 1907U, 19907U, 14907U, 3907U, 2907U], first.BlackJades);

        var second = snapshot.Items[1];
        Assert.Equal(702U, second.ItemID);
        Assert.Equal(3U, second.Level);
        Assert.Equal(456U, second.Exp);
        Assert.Equal(1U, second.Equip);
        Assert.Equal(1U, second.Transform);
        Assert.Equal(344U, second.MasteryScore);
        Assert.Equal([301U, 302U, 303U, 304U, 305U, 306U, 307U], second.Animas[0]);
        Assert.Equal([401U, 402U, 403U, 404U, 405U, 406U, 407U], second.Animas[1]);
        Assert.Equal([6907U, 5907U, 8907U, 7907U, 15907U, 22907U], second.BlackJades);

        Assert.Equal(3, snapshot.BlackJadeBag.Count);
        Assert.Equal([18907U, 8907U, 25907U], snapshot.BlackJadeBag.BlackJades);
    }

    [Fact]
    public void CombatGearSnapshotLoaderReturnsEmptyForMissingFile()
    {
        Directory.CreateDirectory(_root);

        var snapshot = new CombatGearSnapshotLoader(_root).LoadByCharacterUid(1_293_600);

        Assert.Same(CombatGearSnapshot.Empty, snapshot);
        Assert.Equal(0U, snapshot.MasteryPoints);
        Assert.Equal(0, snapshot.Count);
        Assert.Empty(snapshot.Items);
        Assert.Empty(snapshot.BlackJadeBag.BlackJades);
    }

    [Fact]
    public void CombatGearSnapshotLoaderSaveByCharacterUidCreatesRoundTrippableLegacyIni()
    {
        var loader = new CombatGearSnapshotLoader(_root);
        var snapshot = new CombatGearSnapshot(
            MasteryPoints: 11,
            TaoistReiki: 22,
            PirateExp: 33,
            Lottery: 12,
            Count: 0,
            AirPowers: new CombatGearAirPowersSnapshot(
                SenseLevel: 9,
                ArmedLevel: 8,
                OverLoadLevel: 7,
                Points: 92305,
                SenseAipPower: 10000,
                ArmedAipPower: 9000,
                OverLoadAipPower: 8000),
            Items:
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 4,
                    Exp: 123,
                    Equip: 0,
                    Transform: 2,
                    MasteryScore: 928000,
                    Animas:
                    [
                        [101U, 102U, 103U, 104U, 105U, 106U, 107U],
                        [201U, 202U, 203U, 204U, 205U, 206U, 207U],
                    ],
                    BlackJades: [16907U, 1907U, 19907U, 14907U, 3907U, 2907U]),
            ],
            BlackJadeBag: new CombatGearBlackJadeBagSnapshot(Count: 0, BlackJades: [18907U, 8907U]));

        loader.SaveByCharacterUid(1_293_600, snapshot);

        var roundTrip = loader.LoadByCharacterUid(1_293_600);
        Assert.Equal(11U, roundTrip.MasteryPoints);
        Assert.Equal(33U, roundTrip.PirateExp);
        Assert.Equal(92305U, roundTrip.AirPowers.Points);
        var item = Assert.Single(roundTrip.Items);
        Assert.Equal(701U, item.ItemID);
        Assert.Equal([101U, 102U, 103U, 104U, 105U, 106U, 107U], item.Animas[0]);
        Assert.Equal([16907U, 1907U, 19907U, 14907U, 3907U, 2907U], item.BlackJades);
        Assert.Equal(2, roundTrip.BlackJadeBag.Count);
        Assert.Equal([18907U, 8907U], roundTrip.BlackJadeBag.BlackJades);
    }

    [Fact]
    public void CombatGearSnapshotLoaderReturnsEmptyForMalformedNumericValues()
    {
        var snapshot = CombatGearSnapshotLoader.Parse(
            """
            [CombatGear]
            MasteryPoints=not-a-number
            Count=0
            """);

        Assert.Same(CombatGearSnapshot.Empty, snapshot);
    }

    [Fact]
    public async Task CombatGearSnapshotLoaderLoadAndSaveSerializeWithEquivalentPathLock()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".ini");
        var equivalentPath = Path.Combine(_root, ".", CharacterUid + ".ini");
        var original = CreateSnapshot(masteryPoints: 11);
        File.WriteAllText(path, CombatGearSnapshotLoader.ToLegacyText(original));
        var loader = new CombatGearSnapshotLoader(_root);
        var replacement = CreateSnapshot(masteryPoints: 99);

        await AssertWaitsForFileLock(
            equivalentPath,
            () => Assert.Equal(11U, loader.LoadByCharacterUid(CharacterUid).MasteryPoints));
        await AssertWaitsForFileLock(
            equivalentPath,
            () => loader.SaveByCharacterUid(CharacterUid, replacement));

        Assert.Equal(CombatGearSnapshotLoader.ToLegacyText(replacement), File.ReadAllText(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task CombatGearSnapshotLoaderSaveRetriesTransientDestinationContention()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".ini");
        var originalText = CombatGearSnapshotLoader.ToLegacyText(CreateSnapshot(masteryPoints: 11));
        File.WriteAllText(path, originalText);
        var loader = new CombatGearSnapshotLoader(_root);
        var replacement = CreateSnapshot(masteryPoints: 99);
        await SnapshotPersistenceContentionAssert.RetriesAsync(
            path, File.ReadAllBytes(path), ".combat-gear-file-",
            () => loader.SaveByCharacterUid(CharacterUid, replacement));
        Assert.Equal(CombatGearSnapshotLoader.ToLegacyText(replacement), File.ReadAllText(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void CombatGearSnapshotLoaderSavePersistentContentionPreservesOriginalAndThrows()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".ini");
        var originalText = CombatGearSnapshotLoader.ToLegacyText(CreateSnapshot(masteryPoints: 11));
        File.WriteAllText(path, originalText);
        var loader = new CombatGearSnapshotLoader(_root);
        var replacement = CreateSnapshot(masteryPoints: 99);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<IOException>(() => loader.SaveByCharacterUid(CharacterUid, replacement));
            Assert.Equal(originalText, File.ReadAllText(path));
            AssertNoTransactionArtifacts();
        }

        Assert.Equal(originalText, File.ReadAllText(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void CombatGearSnapshotLoaderSaveCreatesMissingFileAtomicallyWithExactLegacyTextAndEncoding()
    {
        var loader = new CombatGearSnapshotLoader(_root);
        var snapshot = CreateSnapshot(masteryPoints: 99);
        var expectedText = CombatGearSnapshotLoader.ToLegacyText(snapshot);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedText);

        loader.SaveByCharacterUid(CharacterUid, snapshot);

        var path = Path.Combine(_root, CharacterUid + ".ini");
        Assert.True(Directory.Exists(_root));
        Assert.Equal(expectedText, File.ReadAllText(path));
        Assert.Equal(expectedBytes, File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CombatGearSnapshot CreateSnapshot(uint masteryPoints)
    {
        return new CombatGearSnapshot(
            MasteryPoints: masteryPoints,
            TaoistReiki: 22,
            PirateExp: 33,
            Lottery: 12,
            Count: 0,
            AirPowers: new CombatGearAirPowersSnapshot(
                SenseLevel: 9,
                ArmedLevel: 8,
                OverLoadLevel: 7,
                Points: 92305,
                SenseAipPower: 10000,
                ArmedAipPower: 9000,
                OverLoadAipPower: 8000),
            Items:
            [
                new CombatGearSnapshotItem(
                    ItemID: 701,
                    Level: 4,
                    Exp: 123,
                    Equip: 0,
                    Transform: 2,
                    MasteryScore: 928000,
                    Animas:
                    [
                        [101U, 102U, 103U, 104U, 105U, 106U, 107U],
                        [201U, 202U, 203U, 204U, 205U, 206U, 207U],
                    ],
                    BlackJades: [16907U, 1907U, 19907U, 14907U, 3907U, 2907U]),
            ],
            BlackJadeBag: new CombatGearBlackJadeBagSnapshot(Count: 0, BlackJades: [18907U, 8907U]));
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
                    throw new TimeoutException("Timed out waiting to release the combat gear file lock.");
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
            "*.combat-gear-file-*",
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
}
