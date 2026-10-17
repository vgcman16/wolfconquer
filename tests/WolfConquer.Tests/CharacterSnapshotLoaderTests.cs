using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class CharacterSnapshotLoaderTests : IDisposable
{
    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-users-" + Guid.NewGuid());

    [Fact]
    public void LoadsCharacterSnapshotByAccountIdAndMapsLegacyKeys()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 1293600);

        var snapshot = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);

        Assert.NotNull(snapshot);
        Assert.Equal(1293600, snapshot.Uid);
        Assert.Equal("HeroOne", snapshot.Name);
        Assert.Equal(27, snapshot.Hair);
        Assert.Equal(7051, snapshot.Class);
        Assert.Equal(0, snapshot.FirstClass);
        Assert.Equal(7050, snapshot.SecondClass);
        Assert.Equal(140, snapshot.Level);
        Assert.Equal(1015, snapshot.MapId);
        Assert.Equal(830, snapshot.X);
        Assert.Equal(584, snapshot.Y);
        Assert.Equal(1000, snapshot.PreviousMapId);
        Assert.Equal(500, snapshot.PreviousMapX);
        Assert.Equal(650, snapshot.PreviousMapY);
        Assert.Equal(447, snapshot.Vitality);
        Assert.Equal(12, snapshot.AttributePoints);
        Assert.Equal(5_000, snapshot.MinMana);
        Assert.Equal(5_000, snapshot.MaxMana);
        Assert.Equal(98_602_439_956, snapshot.Money);
        Assert.Equal(639_778_904, snapshot.ConquerPoints);
        Assert.Equal(7, snapshot.Online);
        Assert.Equal("7050", snapshot.Attributes["SecoundeClass"]);
        Assert.Equal("447", snapshot.Attributes["Vitaliti"]);
        Assert.Equal("12", snapshot.Attributes["Atributes"]);
        Assert.Equal("King", snapshot.PaidNobility["Rank"]);
        Assert.Equal("1293600.ini", snapshot.SourceFile);
    }

    [Fact]
    public void LoadsOptionalMaxManaWhenSnapshotProvidesIt()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 1293600, maxMana: 7_000);

        var snapshot = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);

        Assert.NotNull(snapshot);
        Assert.Equal(5_000, snapshot.MinMana);
        Assert.Equal(7_000, snapshot.MaxMana);
    }

    [Fact]
    public void PersistsLegacyCharacterScalarFieldsAndPreservesOtherSections()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 1293600);
        var snapshotPath = Path.Combine(_usersRoot, "1293600.ini");
        File.AppendAllText(
            snapshotPath,
            """

            [Custom]
            Note=keep-me
            """);
        var original = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);
        Assert.NotNull(original);
        var updated = original with
        {
            Body = 1003,
            Face = 47,
            Hair = 330,
            Class = 135,
            FirstClass = 15,
            SecondClass = 25,
            Reborn = 2,
            MapId = 1002,
            X = 410,
            Y = 350,
            PreviousMapId = 1015,
            PreviousMapX = 830,
            PreviousMapY = 584,
            Level = 141,
            Experience = 987654,
            Strength = 11,
            Agility = 22,
            Vitality = 33,
            Spirit = 44,
            AttributePoints = 55,
            MinHitPoints = 1234,
            MinMana = 4321,
            Money = 5555,
            ConquerPoints = 99,
            BoundConquerPoints = 88,
            BankConquerPoints = 77,
            PkPoints = 6,
            Online = 1,
            ExpBallUsed = 3,
            MaxMana = 8765,
            Attributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["StageEpicTrojanQuest"] = "30",
                ["EpicTrojanMrMirrorPrograss"] = "18",
                ["EpicTrijanKillGhostReaver"] = "1",
                ["PinCodeAnima"] = "abc123",
            },
        };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out var path, out var reason),
            reason);

        Assert.Equal(snapshotPath, path);
        var persistedText = File.ReadAllText(snapshotPath);
        var persisted = CharacterSnapshotParser.ParseText(persistedText);
        Assert.Equal(1003, persisted.Body);
        Assert.Equal(47, persisted.Face);
        Assert.Equal(330, persisted.Hair);
        Assert.Equal(135, persisted.Class);
        Assert.Equal(15, persisted.FirstClass);
        Assert.Equal(25, persisted.SecondClass);
        Assert.Equal(2, persisted.Reborn);
        Assert.Equal(1002, persisted.MapId);
        Assert.Equal(410, persisted.X);
        Assert.Equal(350, persisted.Y);
        Assert.Equal(1015, persisted.PreviousMapId);
        Assert.Equal(830, persisted.PreviousMapX);
        Assert.Equal(584, persisted.PreviousMapY);
        Assert.Equal(141, persisted.Level);
        Assert.Equal(987654, persisted.Experience);
        Assert.Equal(11, persisted.Strength);
        Assert.Equal(22, persisted.Agility);
        Assert.Equal(33, persisted.Vitality);
        Assert.Equal(44, persisted.Spirit);
        Assert.Equal(55, persisted.AttributePoints);
        Assert.Equal(1234, persisted.MinHitPoints);
        Assert.Equal(4321, persisted.MinMana);
        Assert.Equal(8765, persisted.MaxMana);
        Assert.Equal(5555, persisted.Money);
        Assert.Equal(99, persisted.ConquerPoints);
        Assert.Equal(88, persisted.BoundConquerPoints);
        Assert.Equal(77, persisted.BankConquerPoints);
        Assert.Equal(6, persisted.PkPoints);
        Assert.Equal(1, persisted.Online);
        Assert.Equal(3, persisted.ExpBallUsed);
        Assert.Equal("30", persisted.Attributes["StageEpicTrojanQuest"]);
        Assert.Equal("18", persisted.Attributes["EpicTrojanMrMirrorPrograss"]);
        Assert.False(persisted.Attributes.ContainsKey("EpicTrijanKillGhostReaver"));
        Assert.DoesNotContain("EpicTrijanKillGhostReaver=", persistedText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("abc123", persisted.Attributes["PinCodeAnima"]);
        Assert.Equal("King", persisted.PaidNobility["Rank"]);
        Assert.Contains("Note=keep-me", persistedText);
    }

    [Fact]
    public void PersistsWarehouseMoneyAttributeForCrossSessionEconomyReload()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 1293600);
        var original = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);
        Assert.NotNull(original);

        var updatedAttributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["WHMoney"] = "250000",
        };
        var updated = original with
        {
            Money = original.Money - 250_000,
            Attributes = updatedAttributes,
        };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out _, out var reason),
            reason);

        var reloaded = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);
        Assert.NotNull(reloaded);
        Assert.Equal(original.Money - 250_000, reloaded.Money);
        Assert.Equal("250000", reloaded.Attributes["WHMoney"]);
    }

    [Fact]
    public void LoadsUtf8CharacterNameWithoutMojibake()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 1293600, name: "艾哈迈德[PM]");

        var snapshot = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);

        Assert.NotNull(snapshot);
        Assert.Equal("艾哈迈德[PM]", snapshot.Name);
    }

    [Fact]
    public void LoadsLegacyWindows1252CharacterNameWhenUtf8ValidationFails()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 1293600, name: "André");
        var snapshotPath = Path.Combine(_usersRoot, "1293600.ini");
        RewriteAsWindows1252(snapshotPath);

        var snapshot = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);

        Assert.NotNull(snapshot);
        Assert.Equal("André", snapshot.Name);
    }

    [Fact]
    public void ScalarPersistencePreservesUtf8WithoutBomAndCharacterName()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 1293600, name: "艾哈迈德[PM]");
        var snapshotPath = Path.Combine(_usersRoot, "1293600.ini");
        var original = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);
        Assert.NotNull(original);

        var updated = original with { Money = original.Money + 1 };
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out _, out var reason),
            reason);

        var bytes = File.ReadAllBytes(snapshotPath);
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Contains("Name=艾哈迈德[PM]", strictUtf8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal("艾哈迈德[PM]", new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600)?.Name);
    }

    [Fact]
    public void ScalarPersistencePreservesLegacyWindows1252AndCharacterName()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 1293600, name: "André");
        var snapshotPath = Path.Combine(_usersRoot, "1293600.ini");
        RewriteAsWindows1252(snapshotPath);
        var original = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);
        Assert.NotNull(original);

        var updated = original with { Money = original.Money + 1 };
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out _, out var reason),
            reason);

        var bytes = File.ReadAllBytes(snapshotPath);
        var windows1252 = GetWindows1252();
        Assert.Contains("Name=André", windows1252.GetString(bytes), StringComparison.Ordinal);
        Assert.Contains((byte)0xE9, bytes);
        Assert.Throws<DecoderFallbackException>(
            () => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes));
        Assert.Equal("André", new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600)?.Name);
    }

    [Fact]
    public void ScalarPersistencePreservesUtf8Bom()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 1293600, name: "艾哈迈德[PM]");
        var snapshotPath = Path.Combine(_usersRoot, "1293600.ini");
        var text = File.ReadAllText(snapshotPath, Encoding.UTF8);
        File.WriteAllText(
            snapshotPath,
            text,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true));
        var original = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);
        Assert.NotNull(original);

        var updated = original with { Money = original.Money + 1 };
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out _, out var reason),
            reason);

        var bytes = File.ReadAllBytes(snapshotPath);
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal("艾哈迈德[PM]", new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600)?.Name);
    }

    [Fact]
    public void MissingSnapshotReturnsNull()
    {
        Directory.CreateDirectory(_usersRoot);

        var snapshot = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);

        Assert.Null(snapshot);
    }

    [Fact]
    public void DerivesLegacySidecarRootsFromDbServerAncestorWhenUsersRootIsNotNamedUsers()
    {
        var dbServerRoot = Path.Combine(_usersRoot, "GameServer", "DBServer");
        var usersRoot = Path.Combine(dbServerRoot, "Users.Live");
        var itemsRoot = Path.Combine(dbServerRoot, "PlayersItems");
        var combatGearRoot = Path.Combine(dbServerRoot, "PlayersCombatGear");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(combatGearRoot);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                [PlayerItemSnapshotLoaderTests.DbItem(uid: 1001, itemId: 3324226, position: 0)],
                []));
        File.WriteAllText(
            Path.Combine(combatGearRoot, "1293600.ini"),
            """
            [CombatGear]
            MasteryPoints=11
            TaoistReiki=22
            PirateExp=33
            Lottery=12
            Count=0
            [AirPowers]
            SenseLevel=9
            ArmedLevel=8
            OverLoadLevel=7
            Points=92305
            SenseAipPower=10000
            ArmedAipPower=9000
            OverLoadAipPower=8000
            [BlackJadeBag]
            Count=0
            """);

        var loader = new CharacterSnapshotLoader(usersRoot);
        var playerItems = loader.LoadPlayerItemsByCharacterUid(1_293_600);
        var combatGear = loader.LoadCombatGearByCharacterUid(1_293_600);

        Assert.Equal(itemsRoot, loader.PlayerItemsRoot);
        Assert.Equal(combatGearRoot, loader.PlayersCombatGearRoot);
        Assert.Single(playerItems.InventoryItems);
        Assert.Equal(11U, combatGear.MasteryPoints);
        Assert.Equal(9U, combatGear.AirPowers.SenseLevel);
    }

    [Fact]
    public void ResolvesLegacyUsersAndSidecarRootsWhenConfiguredRootIsDbServer()
    {
        var dbServerRoot = Path.Combine(_usersRoot, "GameServer", "DBServer");
        var usersRoot = Path.Combine(dbServerRoot, "Users");
        var itemsRoot = Path.Combine(dbServerRoot, "PlayersItems");
        var combatGearRoot = Path.Combine(dbServerRoot, "PlayersCombatGear");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        Directory.CreateDirectory(combatGearRoot);
        WriteSnapshot(usersRoot, 1_293_600, uid: 1_293_600);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                [PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 500109, position: 4)],
                []));
        File.WriteAllText(
            Path.Combine(combatGearRoot, "1293600.ini"),
            """
            [CombatGear]
            MasteryPoints=17
            TaoistReiki=0
            PirateExp=0
            Lottery=0
            Count=0
            [AirPowers]
            SenseLevel=0
            ArmedLevel=0
            OverLoadLevel=0
            Points=0
            SenseAipPower=0
            ArmedAipPower=0
            OverLoadAipPower=0
            [BlackJadeBag]
            Count=0
            """);

        var loader = new CharacterSnapshotLoader(dbServerRoot);
        var character = loader.LoadByAccountId(1_293_600);
        var playerItems = loader.LoadPlayerItemsByCharacterUid(1_293_600);
        var combatGear = loader.LoadCombatGearByCharacterUid(1_293_600);

        Assert.Equal(usersRoot, loader.UsersRoot);
        Assert.Equal(itemsRoot, loader.PlayerItemsRoot);
        Assert.Equal(combatGearRoot, loader.PlayersCombatGearRoot);
        Assert.NotNull(character);
        Assert.Single(playerItems.EquipmentItems);
        Assert.Equal(17U, combatGear.MasteryPoints);
    }

    [Fact]
    public void SnapshotFileMustMatchCharacterUid()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 999);

        var ex = Assert.Throws<FormatException>(
            () => new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600));

        Assert.Contains("expected 1293600", ex.Message);
    }

    [Fact]
    public void TryLoadByAccountIdReportsUidMismatchWithSnapshotPath()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 999);

        var result = new CharacterSnapshotLoader(_usersRoot).TryLoadByAccountId(1293600);

        Assert.Equal(CharacterSnapshotLoadStatus.UidMismatch, result.Status);
        Assert.Null(result.Snapshot);
        Assert.Equal(1293600, result.AccountId);
        Assert.Equal(Path.Combine(_usersRoot, "1293600.ini"), result.Path);
        Assert.Equal("uidMismatch", result.Reason);
        Assert.Contains("UID 999", result.Detail);
        Assert.Contains("expected 1293600", result.Detail);
    }

    [Fact]
    public void ExpiredActivePaidNobilityRestoresLastDonationAndClearsPaidFields()
    {
        Directory.CreateDirectory(_usersRoot);
        var expiredPeriodTime = DateTime.Now.AddDays(-1).ToBinary();
        WriteSnapshot(
            1293600,
            uid: 1293600,
            name: "艾哈迈德[PM]",
            paidNobilityLines:
                $"""
                Rank=King
                Identifier=1293600
                LastNobilityDonation=250000000
                PaidRank=12
                PeriodTime={expiredPeriodTime}
                IsActive=True
                Position=0
                """);

        var snapshot = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);

        Assert.NotNull(snapshot);
        Assert.Equal("250000000", snapshot.Attributes["DonationNobility"]);
        Assert.Equal("250000000", snapshot.Attributes["LastNobilityDonation"]);
        Assert.Equal("5", snapshot.Attributes["NobilityRank"]);
        Assert.Equal("-1", snapshot.Attributes["NobilityPosition"]);
        Assert.Equal("0", snapshot.PaidNobility["Identifier"]);
        Assert.Equal("0", snapshot.PaidNobility["LastNobilityDonation"]);
        Assert.Equal("0", snapshot.PaidNobility["PaidRank"]);
        Assert.Equal("0", snapshot.PaidNobility["PeriodTime"]);
        Assert.Equal(bool.FalseString, snapshot.PaidNobility["IsActive"]);
        Assert.Equal("艾哈迈德[PM]", snapshot.Name);

        var persistedBytes = File.ReadAllBytes(Path.Combine(_usersRoot, "1293600.ini"));
        var persistedText = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(persistedBytes);
        Assert.Contains("Name=艾哈迈德[PM]", persistedText, StringComparison.Ordinal);
        var persisted = CharacterSnapshotParser.ParseText(persistedText);
        Assert.Equal("250000000", persisted.Attributes["DonationNobility"]);
        Assert.Equal("250000000", persisted.Attributes["LastNobilityDonation"]);
        Assert.Equal("5", persisted.Attributes["NobilityRank"]);
        Assert.Equal("-1", persisted.Attributes["NobilityPosition"]);
        Assert.Equal("0", persisted.PaidNobility["PaidRank"]);
        Assert.Equal(bool.FalseString, persisted.PaidNobility["IsActive"]);
    }

    [Fact]
    public async Task ExpiredPaidNobilityAndConcurrentScalarPersistencePreserveBothUpdates()
    {
        Directory.CreateDirectory(_usersRoot);
        var expiredPeriodTime = DateTime.Now.AddDays(-1).ToBinary();
        WriteSnapshot(
            1293600,
            uid: 1293600,
            paidNobilityLines:
                $"""
                Rank=King
                Identifier=1293600
                LastNobilityDonation=250000000
                PaidRank=12
                PeriodTime={expiredPeriodTime}
                IsActive=True
                Position=0
                """);
        var path = Path.Combine(_usersRoot, "1293600.ini");
        var scalarSnapshot = CharacterSnapshotParser.ParseText(File.ReadAllText(path)) with
        {
            Money = 123_456,
        };
        using var lockEntered = new ManualResetEventSlim();
        using var releaseLock = new ManualResetEventSlim();

        var blocker = Task.Factory.StartNew(
            () => CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
            {
                lockEntered.Set();
                if (!releaseLock.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Timed out waiting to release the snapshot file lock.");
                }

                return true;
            }),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        Assert.True(lockEntered.Wait(TimeSpan.FromSeconds(5)));

        var expiryStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scalarStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expiry = Task.Run(() =>
        {
            expiryStarted.SetResult(true);
            return new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);
        });
        var scalar = Task.Run(() =>
        {
            scalarStarted.SetResult(true);
            var succeeded = CharacterSnapshotScalarPersistence.TryPersist(
                _usersRoot,
                scalarSnapshot,
                out _,
                out var reason);
            return (Succeeded: succeeded, Reason: reason);
        });

        var expiryCompletedWhileLocked = false;
        var scalarCompletedWhileLocked = false;
        try
        {
            await Task.WhenAll(expiryStarted.Task, scalarStarted.Task).WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(250);
            expiryCompletedWhileLocked = expiry.IsCompleted;
            scalarCompletedWhileLocked = scalar.IsCompleted;
        }
        finally
        {
            releaseLock.Set();
        }

        Assert.True(await blocker.WaitAsync(TimeSpan.FromSeconds(5)));
        var expired = await expiry.WaitAsync(TimeSpan.FromSeconds(5));
        var scalarResult = await scalar.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(expiryCompletedWhileLocked);
        Assert.False(scalarCompletedWhileLocked);
        Assert.True(scalarResult.Succeeded, scalarResult.Reason);
        Assert.NotNull(expired);

        var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(path));
        Assert.Equal(123_456, persisted.Money);
        Assert.Equal("250000000", persisted.Attributes["DonationNobility"]);
        Assert.Equal("250000000", persisted.Attributes["LastNobilityDonation"]);
        Assert.Equal("0", persisted.PaidNobility["Identifier"]);
        Assert.Equal("0", persisted.PaidNobility["LastNobilityDonation"]);
        Assert.Equal("0", persisted.PaidNobility["PaidRank"]);
        Assert.Equal("0", persisted.PaidNobility["PeriodTime"]);
        Assert.Equal(bool.FalseString, persisted.PaidNobility["IsActive"]);
        Assert.Empty(Directory.EnumerateFiles(_usersRoot, "*.character-file-*"));
    }

    [Fact]
    public void FutureActivePaidNobilityKeepsPaidFieldsUnchanged()
    {
        Directory.CreateDirectory(_usersRoot);
        var futurePeriodTime = DateTime.Now.AddDays(1).ToBinary();
        WriteSnapshot(
            1293600,
            uid: 1293600,
            donationNobility: 0,
            paidNobilityLines:
                $"""
                Rank=Prince
                Identifier=1293600
                LastNobilityDonation=250000000
                PaidRank=9
                PeriodTime={futurePeriodTime}
                IsActive=True
                Position=4
                """);

        var snapshot = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(1293600);

        Assert.NotNull(snapshot);
        Assert.Equal("0", snapshot.Attributes["DonationNobility"]);
        Assert.Equal("250000000", snapshot.PaidNobility["LastNobilityDonation"]);
        Assert.Equal("9", snapshot.PaidNobility["PaidRank"]);
        Assert.Equal(futurePeriodTime.ToString(), snapshot.PaidNobility["PeriodTime"]);
        Assert.Equal(bool.TrueString, snapshot.PaidNobility["IsActive"]);
    }

    [Fact]
    public void CreatesLegacyFirstCharacterSnapshotWithSaneLevelOneCoreFields()
    {
        var snapshot = new CharacterSnapshotLoader(_usersRoot).CreateLegacyFirstCharacterSnapshot(
            1_293_604,
            "Demon4",
            body: 1003,
            classCode: 10_000);

        Assert.Equal(1, snapshot.Level);
        Assert.NotEqual(100, snapshot.Level);
        Assert.Equal(0, snapshot.Experience);
        Assert.Equal(0, snapshot.Strength);
        Assert.Equal(2, snapshot.Agility);
        Assert.Equal(3, snapshot.Vitality);
        Assert.Equal(5, snapshot.Spirit);
        Assert.Equal(0, snapshot.AttributePoints);
        Assert.Equal(93, snapshot.MinHitPoints);
        Assert.Equal(25, snapshot.MinMana);
        Assert.Equal(25, snapshot.MaxMana);
        Assert.Equal(100_000, snapshot.Money);
        Assert.NotEqual(10_000_000, snapshot.Money);
        Assert.Equal(0, snapshot.ConquerPoints);
        Assert.NotEqual(50_000, snapshot.ConquerPoints);
        var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(Path.Combine(_usersRoot, "1293604.ini")));
        Assert.Equal(1, persisted.Level);
        Assert.Equal("1", persisted.Attributes["Level"]);
        Assert.Equal(0, persisted.Strength);
        Assert.Equal("0", persisted.Attributes["Strength"]);
        Assert.Equal(2, persisted.Agility);
        Assert.Equal("2", persisted.Attributes["Agility"]);
        Assert.Equal(3, persisted.Vitality);
        Assert.Equal("3", persisted.Attributes["Vitaliti"]);
        Assert.Equal(5, persisted.Spirit);
        Assert.Equal("5", persisted.Attributes["Spirit"]);
        Assert.Equal(0, persisted.AttributePoints);
        Assert.Equal("0", persisted.Attributes["Atributes"]);
        Assert.Equal(93, persisted.MinHitPoints);
        Assert.Equal("93", persisted.Attributes["MinHitPoints"]);
        Assert.Equal(25, persisted.MinMana);
        Assert.Equal("25", persisted.Attributes["MinMana"]);
        Assert.Equal(25, persisted.MaxMana);
        Assert.Equal("25", persisted.Attributes["MaxMana"]);
        Assert.Equal(100_000, persisted.Money);
        Assert.Equal("100000", persisted.Attributes["Money"]);
        Assert.Equal(0, persisted.ConquerPoints);
        Assert.Equal("0", persisted.Attributes["ConquerPoints"]);
    }

    [Fact]
    public void CreatesLegacyFirstCharacterSnapshotAsUtf8WithoutBom()
    {
        var snapshot = new CharacterSnapshotLoader(_usersRoot).CreateLegacyFirstCharacterSnapshot(
            1_293_604,
            "艾哈迈德",
            body: 1003,
            classCode: 10_000);

        var bytes = File.ReadAllBytes(Path.Combine(_usersRoot, "1293604.ini"));
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal("艾哈迈德", snapshot.Name);
        Assert.Contains(
            "Name=艾哈迈德",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes),
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private void WriteSnapshot(
        long accountId,
        long uid,
        string name = "HeroOne",
        long? maxMana = null,
        ulong donationNobility = 0,
        string? paidNobilityLines = null)
    {
        WriteSnapshot(_usersRoot, accountId, uid, name, maxMana, donationNobility, paidNobilityLines);
    }

    private static void WriteSnapshot(
        string usersRoot,
        long accountId,
        long uid,
        string name = "HeroOne",
        long? maxMana = null,
        ulong donationNobility = 0,
        string? paidNobilityLines = null)
    {
        var maxManaLine = maxMana is null ? "" : $"MaxMana={maxMana.Value}\r\n";
        paidNobilityLines ??= """
            Rank=King
            Identifier=1293600
            """;
        File.WriteAllText(
            Path.Combine(usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name={{name}}
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
            {{maxManaLine}}
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            PkPoints=0
            Online=7
            DonationNobility={{donationNobility}}
            EpicTrojan=0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/

            [PaidNobility]
            {{paidNobilityLines}}
            """);
    }

    private static void RewriteAsWindows1252(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        File.WriteAllBytes(path, GetWindows1252().GetBytes(text));
    }

    private static Encoding GetWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
