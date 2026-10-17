using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVendorPurchasePersistenceTests : IDisposable
{
    private const long BuyerAccountId = 101;
    private const long BuyerCharacterUid = 1_001;
    private const long SellerAccountId = 202;
    private const long SellerCharacterUid = 2_002;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyVendorPurchasePersistenceTests-" + Guid.NewGuid().ToString("N"));
    private readonly string _usersRoot;
    private readonly string _playerItemsRoot;

    public LegacyVendorPurchasePersistenceTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _playerItemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_playerItemsRoot);
    }

    [Fact]
    public void PersistsBothCharactersAndItemSnapshotsWhenItemFilesWereMissing()
    {
        WriteCharacter(BuyerAccountId, BuyerCharacterUid, money: 1_000, conquerPoints: 50);
        WriteCharacter(SellerAccountId, SellerCharacterUid, money: 2_000, conquerPoints: 75);
        var buyerItem = Item(10, 410_001);
        var soldItem = Item(20, 420_002);
        var originalBuyer = Session(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50,
            Items(buyerItem));
        var originalSeller = Session(
            SellerAccountId,
            SellerCharacterUid,
            money: 2_000,
            conquerPoints: 75,
            Items(soldItem));
        var updatedBuyer = originalBuyer with
        {
            Character = originalBuyer.Character with { Money = 900, ConquerPoints = 45 },
            PlayerItems = Items(buyerItem, soldItem),
        };
        var updatedSeller = originalSeller with
        {
            Character = originalSeller.Character with { Money = 2_100, ConquerPoints = 80 },
            PlayerItems = PlayerItemSnapshot.Empty,
        };

        Assert.True(
            LegacyVendorPurchasePersistence.TryPersist(
                originalBuyer,
                updatedBuyer,
                originalSeller,
                updatedSeller,
                _playerItemsRoot,
                out var reason),
            reason);

        Assert.Equal((900L, 45L), ReadBalances(BuyerAccountId));
        Assert.Equal((2_100L, 80L), ReadBalances(SellerAccountId));
        AssertItemBytesEqual(updatedBuyer.PlayerItems, BuyerCharacterUid);
        AssertItemBytesEqual(updatedSeller.PlayerItems, SellerCharacterUid);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void FourthTargetFailureRollsBackCommittedTargetsAndCleansArtifacts()
    {
        var buyerCharacterPath = WriteCharacter(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50);
        var sellerCharacterPath = WriteCharacter(
            SellerAccountId,
            SellerCharacterUid,
            money: 2_000,
            conquerPoints: 75);
        var originalBuyerItems = Items(Item(10, 410_001));
        var originalSellerItems = Items(Item(20, 420_002));
        var buyerItemPath = WriteItems(BuyerCharacterUid, originalBuyerItems);
        var sellerItemPath = ItemPath(SellerCharacterUid);
        Directory.CreateDirectory(sellerItemPath);
        var buyerCharacterBytes = File.ReadAllBytes(buyerCharacterPath);
        var sellerCharacterBytes = File.ReadAllBytes(sellerCharacterPath);
        var buyerItemBytes = File.ReadAllBytes(buyerItemPath);
        var originalBuyer = Session(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50,
            originalBuyerItems);
        var originalSeller = Session(
            SellerAccountId,
            SellerCharacterUid,
            money: 2_000,
            conquerPoints: 75,
            originalSellerItems);
        var updatedBuyer = originalBuyer with
        {
            Character = originalBuyer.Character with { Money = 900, ConquerPoints = 45 },
            PlayerItems = Items(Item(10, 410_001), Item(20, 420_002)),
        };
        var updatedSeller = originalSeller with
        {
            Character = originalSeller.Character with { Money = 2_100, ConquerPoints = 80 },
            PlayerItems = PlayerItemSnapshot.Empty,
        };

        Assert.False(
            LegacyVendorPurchasePersistence.TryPersist(
                originalBuyer,
                updatedBuyer,
                originalSeller,
                updatedSeller,
                _playerItemsRoot,
                out var reason));

        Assert.StartsWith("transactionWriteFailed:", reason, StringComparison.Ordinal);
        Assert.Equal(buyerCharacterBytes, File.ReadAllBytes(buyerCharacterPath));
        Assert.Equal(sellerCharacterBytes, File.ReadAllBytes(sellerCharacterPath));
        Assert.Equal(buyerItemBytes, File.ReadAllBytes(buyerItemPath));
        Assert.True(Directory.Exists(sellerItemPath));
        AssertNoTransactionArtifacts();
    }

    [Theory]
    [InlineData("buyer")]
    [InlineData("seller")]
    [InlineData("items")]
    public void MissingRootRejectsBeforeAnyWrite(string missingRoot)
    {
        var buyerCharacterPath = WriteCharacter(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50);
        var sellerCharacterPath = WriteCharacter(
            SellerAccountId,
            SellerCharacterUid,
            money: 2_000,
            conquerPoints: 75);
        var buyerItems = Items(Item(10, 410_001));
        var sellerItems = Items(Item(20, 420_002));
        var buyerItemPath = WriteItems(BuyerCharacterUid, buyerItems);
        var sellerItemPath = WriteItems(SellerCharacterUid, sellerItems);
        var expectedFiles = new Dictionary<string, byte[]>
        {
            [buyerCharacterPath] = File.ReadAllBytes(buyerCharacterPath),
            [sellerCharacterPath] = File.ReadAllBytes(sellerCharacterPath),
            [buyerItemPath] = File.ReadAllBytes(buyerItemPath),
            [sellerItemPath] = File.ReadAllBytes(sellerItemPath),
        };
        var originalBuyer = Session(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50,
            buyerItems);
        var originalSeller = Session(
            SellerAccountId,
            SellerCharacterUid,
            money: 2_000,
            conquerPoints: 75,
            sellerItems);
        var updatedBuyer = originalBuyer with
        {
            Character = originalBuyer.Character with { Money = 900 },
        };
        var updatedSeller = originalSeller with
        {
            Character = originalSeller.Character with { Money = 2_100 },
        };
        string? playerItemsRoot = _playerItemsRoot;
        switch (missingRoot)
        {
            case "buyer":
                originalBuyer = originalBuyer with { UsersRoot = null };
                updatedBuyer = updatedBuyer with { UsersRoot = null };
                break;
            case "seller":
                originalSeller = originalSeller with { UsersRoot = null };
                updatedSeller = updatedSeller with { UsersRoot = null };
                break;
            case "items":
                playerItemsRoot = null;
                break;
        }

        Assert.False(
            LegacyVendorPurchasePersistence.TryPersist(
                originalBuyer,
                updatedBuyer,
                originalSeller,
                updatedSeller,
                playerItemsRoot,
                out var reason));

        Assert.Contains("RootMissing", reason, StringComparison.Ordinal);
        foreach (var expected in expectedFiles)
        {
            Assert.Equal(expected.Value, File.ReadAllBytes(expected.Key));
        }

        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void WrongCharacterUidInAccountIniRejectsBeforeAnyWrite()
    {
        var buyerCharacterPath = WriteCharacter(
            BuyerAccountId,
            characterUid: 9_999,
            money: 1_000,
            conquerPoints: 50);
        var sellerCharacterPath = WriteCharacter(
            SellerAccountId,
            SellerCharacterUid,
            money: 2_000,
            conquerPoints: 75);
        var buyerCharacterBytes = File.ReadAllBytes(buyerCharacterPath);
        var sellerCharacterBytes = File.ReadAllBytes(sellerCharacterPath);
        var originalBuyer = Session(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50,
            Items(Item(10, 410_001)));
        var originalSeller = Session(
            SellerAccountId,
            SellerCharacterUid,
            money: 2_000,
            conquerPoints: 75,
            Items(Item(20, 420_002)));

        Assert.False(
            LegacyVendorPurchasePersistence.TryPersist(
                originalBuyer,
                originalBuyer with { Character = originalBuyer.Character with { Money = 900 } },
                originalSeller,
                originalSeller with { Character = originalSeller.Character with { Money = 2_100 } },
                _playerItemsRoot,
                out var reason));

        Assert.Equal("buyerUidMismatch", reason);
        Assert.Equal(buyerCharacterBytes, File.ReadAllBytes(buyerCharacterPath));
        Assert.Equal(sellerCharacterBytes, File.ReadAllBytes(sellerCharacterPath));
        Assert.Empty(Directory.EnumerateFiles(_playerItemsRoot));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void SameCharacterRejectsBeforeAnyWrite()
    {
        var buyerCharacterPath = WriteCharacter(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50);
        var sellerCharacterPath = WriteCharacter(
            SellerAccountId,
            BuyerCharacterUid,
            money: 2_000,
            conquerPoints: 75);
        var buyerCharacterBytes = File.ReadAllBytes(buyerCharacterPath);
        var sellerCharacterBytes = File.ReadAllBytes(sellerCharacterPath);
        var buyer = Session(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50,
            PlayerItemSnapshot.Empty);
        var seller = Session(
            SellerAccountId,
            BuyerCharacterUid,
            money: 2_000,
            conquerPoints: 75,
            PlayerItemSnapshot.Empty);

        Assert.False(
            LegacyVendorPurchasePersistence.TryPersist(
                buyer,
                buyer with { Character = buyer.Character with { Money = 900 } },
                seller,
                seller with { Character = seller.Character with { Money = 2_100 } },
                _playerItemsRoot,
                out var reason));

        Assert.Equal("buyerSellerCharacterNotDistinct", reason);
        Assert.Equal(buyerCharacterBytes, File.ReadAllBytes(buyerCharacterPath));
        Assert.Equal(sellerCharacterBytes, File.ReadAllBytes(sellerCharacterPath));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void ExistingTargetsLeaveNoBackupsAndPreserveLegacyCharacterEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var windows1252 = Encoding.GetEncoding(1252);
        var buyerCharacterPath = WriteCharacter(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50,
            name: "Jos\u00E9",
            encoding: windows1252);
        WriteCharacter(SellerAccountId, SellerCharacterUid, money: 2_000, conquerPoints: 75);
        var buyerItems = Items(Item(10, 410_001));
        var sellerItems = Items(Item(20, 420_002));
        WriteItems(BuyerCharacterUid, buyerItems);
        WriteItems(SellerCharacterUid, sellerItems);
        var originalBuyer = Session(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50,
            buyerItems);
        var originalSeller = Session(
            SellerAccountId,
            SellerCharacterUid,
            money: 2_000,
            conquerPoints: 75,
            sellerItems);

        Assert.True(
            LegacyVendorPurchasePersistence.TryPersist(
                originalBuyer,
                originalBuyer with { Character = originalBuyer.Character with { Money = 900 } },
                originalSeller,
                originalSeller with { Character = originalSeller.Character with { Money = 2_100 } },
                _playerItemsRoot,
                out var reason),
            reason);

        var bytes = File.ReadAllBytes(buyerCharacterPath);
        Assert.Contains((byte)0xE9, bytes);
        Assert.Equal(-1, bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes("\u00E9")));
        Assert.Contains("Name=Jos\u00E9", windows1252.GetString(bytes), StringComparison.Ordinal);
        AssertNoTransactionArtifacts();
    }

    [Theory]
    [InlineData("buyer-character")]
    [InlineData("seller-character")]
    [InlineData("buyer-item")]
    [InlineData("seller-item")]
    public async Task TransactionBlocksBehindEveryHeldTargetLockThenSucceeds(string heldTarget)
    {
        var buyerCharacterPath = WriteCharacter(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50);
        var sellerCharacterPath = WriteCharacter(
            SellerAccountId,
            SellerCharacterUid,
            money: 2_000,
            conquerPoints: 75);
        var buyerItems = Items(Item(10, 410_001));
        var sellerItems = Items(Item(20, 420_002));
        var buyerItemPath = WriteItems(BuyerCharacterUid, buyerItems);
        var sellerItemPath = WriteItems(SellerCharacterUid, sellerItems);
        var originalFiles = new Dictionary<string, byte[]>
        {
            [buyerCharacterPath] = File.ReadAllBytes(buyerCharacterPath),
            [sellerCharacterPath] = File.ReadAllBytes(sellerCharacterPath),
            [buyerItemPath] = File.ReadAllBytes(buyerItemPath),
            [sellerItemPath] = File.ReadAllBytes(sellerItemPath),
        };
        var originalBuyer = Session(
            BuyerAccountId,
            BuyerCharacterUid,
            money: 1_000,
            conquerPoints: 50,
            buyerItems);
        var originalSeller = Session(
            SellerAccountId,
            SellerCharacterUid,
            money: 2_000,
            conquerPoints: 75,
            sellerItems);
        var updatedBuyer = originalBuyer with
        {
            Character = originalBuyer.Character with { Money = 900, ConquerPoints = 45 },
            PlayerItems = Items(Item(10, 410_001), Item(20, 420_002)),
        };
        var updatedSeller = originalSeller with
        {
            Character = originalSeller.Character with { Money = 2_100, ConquerPoints = 80 },
            PlayerItems = PlayerItemSnapshot.Empty,
        };
        var heldPath = heldTarget switch
        {
            "buyer-character" => buyerCharacterPath,
            "seller-character" => sellerCharacterPath,
            "buyer-item" => buyerItemPath,
            "seller-item" => sellerItemPath,
            _ => throw new ArgumentOutOfRangeException(nameof(heldTarget)),
        };
        var equivalentHeldPath = Path.Combine(
            Path.GetDirectoryName(heldPath)!,
            ".",
            Path.GetFileName(heldPath));
        using var heldLockEntered = new ManualResetEventSlim();
        using var releaseHeldLock = new ManualResetEventSlim();
        using var persistenceStarted = new ManualResetEventSlim();

        var heldLock = StartLongRunning(() => SnapshotFileLockCoordinator.ExecuteLocked(
            equivalentHeldPath,
            () =>
            {
                heldLockEntered.Set();
                if (!releaseHeldLock.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Timed out waiting to release the held target lock.");
                }

                return true;
            }));
        Assert.True(heldLockEntered.Wait(TimeSpan.FromSeconds(5)));

        var persistence = StartLongRunning(() =>
        {
            persistenceStarted.Set();
            var succeeded = LegacyVendorPurchasePersistence.TryPersist(
                originalBuyer,
                updatedBuyer,
                originalSeller,
                updatedSeller,
                _playerItemsRoot,
                out var reason);
            return (Succeeded: succeeded, Reason: reason);
        });

        var completedWhileHeld = false;
        var filesChangedWhileHeld = false;
        var artifactsCreatedWhileHeld = false;
        try
        {
            Assert.True(persistenceStarted.Wait(TimeSpan.FromSeconds(5)));
            completedWhileHeld = await Task.WhenAny(
                persistence,
                Task.Delay(TimeSpan.FromMilliseconds(250))) == persistence;
            filesChangedWhileHeld = originalFiles.Any(
                expected => !expected.Value.SequenceEqual(File.ReadAllBytes(expected.Key)));
            artifactsCreatedWhileHeld = Directory.EnumerateFileSystemEntries(
                    _root,
                    "*.vendor-purchase-*",
                    SearchOption.AllDirectories)
                .Any();
        }
        finally
        {
            releaseHeldLock.Set();
        }

        Assert.True(await heldLock.WaitAsync(TimeSpan.FromSeconds(5)));
        var result = await persistence.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(completedWhileHeld);
        Assert.False(filesChangedWhileHeld);
        Assert.False(artifactsCreatedWhileHeld);
        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal((900L, 45L), ReadBalances(BuyerAccountId));
        Assert.Equal((2_100L, 80L), ReadBalances(SellerAccountId));
        AssertItemBytesEqual(updatedBuyer.PlayerItems, BuyerCharacterUid);
        AssertItemBytesEqual(updatedSeller.PlayerItems, SellerCharacterUid);
        AssertNoTransactionArtifacts();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WorldSession Session(
        long accountId,
        long characterUid,
        long money,
        long conquerPoints,
        PlayerItemSnapshot items)
    {
        return new WorldSession(
            AccountId: accountId,
            Username: "vendor-" + accountId.ToString(CultureInfo.InvariantCulture),
            Character: new CharacterSnapshot(
                Uid: characterUid,
                Name: "Vendor" + characterUid.ToString(CultureInfo.InvariantCulture),
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 10,
                FirstClass: 10,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: 1002,
                X: 430,
                Y: 380,
                PreviousMapId: 1002,
                PreviousMapX: 430,
                PreviousMapY: 380,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: money,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: accountId.ToString(CultureInfo.InvariantCulture) + ".ini",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: items,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: _usersRoot);
    }

    private string WriteCharacter(
        long accountId,
        long characterUid,
        long money,
        long conquerPoints,
        string name = "Vendor",
        Encoding? encoding = null)
    {
        var path = Path.Combine(
            _usersRoot,
            accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{characterUid}}
            Body=1003
            Face=0
            Name={{name}}
            Class=10
            Map=1002
            X=430
            Y=380
            FirstClass=10
            SecoundeClass=0
            Reborn=0
            Level=120
            Haire=0
            Experience=0
            PMap=1002
            PMapX=430
            PMapY=380
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            ConquerPoints={{conquerPoints}}
            BankCps=0
            BoundConquerPoints=0
            Money={{money}}
            PkPoints=0
            Online=1

            [PaidNobility]
            """,
            encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private string WriteItems(long characterUid, PlayerItemSnapshot items)
    {
        var path = ItemPath(characterUid);
        File.WriteAllBytes(path, PlayerItemSnapshotLoader.ToLegacyBytes(items));
        return path;
    }

    private string ItemPath(long characterUid)
    {
        return Path.Combine(
            _playerItemsRoot,
            characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
    }

    private void AssertItemBytesEqual(PlayerItemSnapshot expected, long characterUid)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            File.ReadAllBytes(ItemPath(characterUid)));
    }

    private void AssertNoTransactionArtifacts()
    {
        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                _root,
                "*.vendor-purchase-*",
                SearchOption.AllDirectories));
    }

    private (long Money, long ConquerPoints) ReadBalances(long accountId)
    {
        var path = Path.Combine(
            _usersRoot,
            accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        var character = IniDocument.Parse(File.ReadAllText(path)).GetSection("Character");
        return (
            long.Parse(character["Money"], CultureInfo.InvariantCulture),
            long.Parse(character["ConquerPoints"], CultureInfo.InvariantCulture));
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] items)
    {
        return new PlayerItemSnapshot(items, [], [], [], []);
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 100,
            MaximumDurability = 100,
            Position = 0,
            StackSize = 1,
        };
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
