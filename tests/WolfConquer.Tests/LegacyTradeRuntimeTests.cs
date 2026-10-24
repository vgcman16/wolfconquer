using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Managed handler + connection send delegates + disposable files, not D13 visual-client proof.
public sealed partial class LegacyTradeRuntimeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-trade-" + Guid.NewGuid().ToString("N"));
    private readonly WorldSessionStore sessions = new();
    private readonly LegacyTradeStateStore trades = new();
    private readonly StringWriter log = new();
    private readonly PacketHandlerRegistry<WorldPacketHandlerContext> handlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Theory]
    [InlineData("1234,0,0", false)]
    [InlineData("malformed-pin,0,0", false)]
    [InlineData("1234,0,0", true)]
    public async Task ConfiguredOrMalformedSecondaryPasswordCannotRequestAcceptOfferOrDebitWithoutVerification(
        string securityPass, bool forgedCheckedAttribute)
    {
        var left = AddPlayer(1_293_731, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_732, 2_000, 60, [Item(20)]);
        SetSecurityPass(left, securityPass);
        if (forgedCheckedAttribute)
        {
            // A copied scalar is not the native verification issuer's owned state.
            var current = Current(left);
            var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["IsCheckedPass"] = "true",
            };
            sessions.Set(left.Connection, current with { Character = current.Character with { Attributes = attributes } });
        }
        var leftBefore = Current(left).PlayerItems;
        var rightBefore = Current(right).PlayerItems;
        await Send(left, new(0) { UID = right.Uid });
        await Send(right, new(1) { UID = left.Uid });
        await Send(left, new(1) { UID = right.Uid });
        await Send(left, new(6) { ItemUID = 10 });
        await Send(left, new(7) { Money = 300 });
        await Send(left, new(12) { CPS = 10 });
        await Send(left, new(9));
        await Send(right, new(9));
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
        AssertInventory(left, 10);
        AssertInventory(right, 20);
        Assert.Same(leftBefore, Current(left).PlayerItems);
        Assert.Same(rightBefore, Current(right).PlayerItems);
        Assert.DoesNotContain(TradeFrames(left), packet => packet.Action == 3);
        Assert.DoesNotContain(TradeFrames(right), packet => packet.Action == 3);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0,0,0")]
    public async Task RequestAloneDoesNotMarkTradeActiveAndMissingOrZeroPinCanThenAccept(string? securityPass)
    {
        var left = AddPlayer(1_293_741, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_742, 2_000, 60, [Item(20)]);
        if (securityPass is not null) SetSecurityPass(left, securityPass);
        await Send(left, new(0) { UID = right.Uid });
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        Assert.DoesNotContain(TradeFrames(left), packet => packet.Action == 3);
        Assert.DoesNotContain(TradeFrames(right), packet => packet.Action == 3);
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
        await Send(right, new(1) { UID = left.Uid });
        Assert.True(Current(left).LegacyTradeActive);
        Assert.True(Current(right).LegacyTradeActive);
        await Send(left, new(2));
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Fact]
    public async Task BothConfirmationsTransferItemsAndEscrowInBothDirectionsAndPersistAllContainers()
    {
        var left = AddPlayer(1_293_601, 10_000, 1_000, [Item(10), Item(11)]);
        var right = AddPlayer(1_293_602, 20_000, 2_000, [Item(20), Item(21)]);
        var leftBefore = Current(left);
        var rightBefore = Current(right);
        await Open(left, right);
        await Send(left, new(6) { ItemUID = 10 });
        await Send(right, new(6) { ItemUID = 20 });
        await Send(left, new(7) { Money = 400 });
        await Send(right, new(7) { Money = 700 });
        await Send(left, new(12) { CPS = 30 });
        await Send(right, new(12) { CPS = 50 });
        AssertWallet(left, 9_600, 970);
        AssertWallet(right, 19_300, 1_950);
        AssertInventory(left, 10, 11);
        AssertInventory(right, 20, 21);
        Assert.Contains(TradeFrames(right), packet => packet.Action == 8 && packet.Money == 400);
        Assert.Contains(TradeFrames(right), packet => packet.Action == 11 && packet.CPS == 30);
        await Send(left, new(9));
        AssertInventory(left, 10, 11);
        AssertInventory(right, 20, 21);
        AssertWallet(left, 9_600, 970);
        AssertWallet(right, 19_300, 1_950);
        await Send(right, new(9));
        AssertInventory(left, 11, 20);
        AssertInventory(right, 10, 21);
        AssertItemUnchanged(leftBefore.PlayerItems.InventoryItems.Single(item => item.Uid == 10),
            Current(right).PlayerItems.InventoryItems.Single(item => item.Uid == 10));
        AssertItemUnchanged(rightBefore.PlayerItems.InventoryItems.Single(item => item.Uid == 20),
            Current(left).PlayerItems.InventoryItems.Single(item => item.Uid == 20));
        AssertWallet(left, 10_300, 1_020);
        AssertWallet(right, 19_700, 1_980);
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        AssertOtherContainers(leftBefore.PlayerItems, Current(left).PlayerItems);
        AssertOtherContainers(rightBefore.PlayerItems, Current(right).PlayerItems);
        AssertPersisted(left);
        AssertPersisted(right);
        Assert.Contains(TradeFrames(left), packet => packet.Action == 4);
        Assert.Contains(TradeFrames(right), packet => packet.Action == 4);
        // A replayed confirmation cannot repeat credits or resurrect the closed trade.
        await Send(right, new(9));
        AssertWallet(left, 10_300, 1_020);
        AssertWallet(right, 19_700, 1_980);
        AssertInventory(left, 11, 20);
        AssertInventory(right, 10, 21);
    }

    [Fact]
    public async Task CloseRefundsAccumulatedMoneyAndConquerPointsExactlyOnceWithoutMovingItems()
    {
        var left = AddPlayer(1_293_611, 10_000, 1_000, [Item(10)]);
        var right = AddPlayer(1_293_612, 20_000, 2_000, [Item(20)]);
        var leftBefore = Current(left).PlayerItems;
        var rightBefore = Current(right).PlayerItems;
        await Open(left, right);
        await Send(left, new(6) { ItemUID = 10 });
        await Send(left, new(7) { Money = 300 });
        await Send(left, new(7) { Money = 200 });
        await Send(left, new(12) { CPS = 10 });
        await Send(left, new(12) { CPS = 20 });
        await Send(right, new(7) { Money = 700 });
        AssertWallet(left, 9_500, 970);
        AssertWallet(right, 19_300, 2_000);
        AssertPersisted(left);
        AssertPersisted(right);
        await Send(left, new(2));
        await Send(right, new(2));
        AssertWallet(left, 10_000, 1_000);
        AssertWallet(right, 20_000, 2_000);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(leftBefore),
            PlayerItemSnapshotLoader.ToLegacyBytes(Current(left).PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(rightBefore),
            PlayerItemSnapshotLoader.ToLegacyBytes(Current(right).PlayerItems));
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Theory]
    [InlineData(1, 0, 0, 1_088_001U)]
    [InlineData(0, 1, 0, 1_088_001U)]
    [InlineData(0, 0, 1, 1_088_001U)]
    [InlineData(0, 0, 0, 750_000U)]
    public async Task BoundInscribedLockedNonpartnerAndProhibitedItemsNeverTransfer(
        byte bound, ushort inscribed, byte locked, uint itemId)
    {
        var forbidden = new PlayerItemSnapshotItem
        {
            Uid = 10, ItemId = itemId, Bound = bound, Inscribed = inscribed, Locked = locked,
            Durability = 5, MaximumDurability = 10, StackSize = 1,
        };
        var left = AddPlayer(1_293_621, 10_000, 1_000, [forbidden]);
        var right = AddPlayer(1_293_622, 20_000, 2_000, [Item(20)]);
        var before = Current(left).PlayerItems;
        await Open(left, right);
        await Send(left, new(6) { ItemUID = forbidden.Uid });
        Assert.Contains(TradeFrames(left), packet => packet.Action == 10);
        await Send(left, new(9));
        await Send(right, new(9));
        AssertInventory(left, 10);
        AssertInventory(right, 20);
        AssertWallet(left, 10_000, 1_000);
        AssertWallet(right, 20_000, 2_000);
        AssertOtherContainers(before, Current(left).PlayerItems);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Fact]
    public async Task NegativeAndOverdrawnCurrencyOffersCannotCreateOrEscrowAssets()
    {
        var left = AddPlayer(1_293_631, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_632, 2_000, 60, [Item(20)]);
        await Open(left, right);
        await Send(left, new(7) { Money = -1 });
        await Send(left, new(7) { Money = 1_001 });
        await Send(left, new(12) { CPS = 51 });
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
        await Send(left, new(2));
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Fact]
    public async Task FullPartnerInventoryRejectsItemBeforeEitherConfirmation()
    {
        var left = AddPlayer(1_293_641, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_642, 2_000, 60,
            Enumerable.Range(100, 40).Select(value => Item((uint)value)).ToArray());
        await Open(left, right);
        await Send(left, new(6) { ItemUID = 10 });
        Assert.Contains(TradeFrames(left), packet => packet.Action == 10);
        await Send(left, new(9));
        await Send(right, new(9));
        AssertInventory(left, 10);
        Assert.Equal(40, Current(right).PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(Current(right).PlayerItems.InventoryItems, item => item.Uid == 10);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Fact]
    public async Task StaleOfferedItemAbortsAndRefundsRatherThanMintingAnItemForPartner()
    {
        var left = AddPlayer(1_293_651, 1_000, 50, [Item(10), Item(11)]);
        var right = AddPlayer(1_293_652, 2_000, 60, [Item(20)]);
        var leftBefore = Current(left).PlayerItems;
        var rightBefore = Current(right).PlayerItems;
        await Open(left, right);
        await Send(left, new(6) { ItemUID = 10 });
        await Send(left, new(7) { Money = 300 });
        ReplaceInventory(left, [Item(11)]);
        var ownerFrameCountBeforeCancellation = left.Sent.Count;
        await Send(left, new(9));
        await Send(right, new(9));
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
        AssertInventory(left, 11);
        AssertInventory(right, 20);
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        AssertOtherContainers(leftBefore, Current(left).PlayerItems);
        AssertOtherContainers(rightBefore, Current(right).PlayerItems);
        Assert.DoesNotContain(left.Sent.Skip(ownerFrameCountBeforeCancellation).Select(ReadFrame), frame =>
            frame.Header.Opcode == WorldPacketOpcodes.LegacyGameItem &&
            LegacyGameItemPacket.TryParse(frame, out var item) && item.Uid == 10);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Fact]
    public async Task UnrepresentableChangedItemPreviewCannotRetainAnAlreadyRefundedEscrowClaim()
    {
        var left = AddPlayer(1_293_751, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_752, 2_000, 60, [Item(20)]);
        await Open(left, right);
        await Send(left, new(6) { ItemUID = 10 });
        await Send(left, new(7) { Money = 300 });
        // The legacy save holds a uint Effect, but this client's preview requires
        // a byte. A changed stale item can be saved while its preview is invalid.
        ReplaceInventory(left, [new PlayerItemSnapshotItem
        {
            Uid = 10, ItemId = 1_088_001, Effect = 256, StackSize = 1,
        }]);

        await Send(left, new(2));
        await Send(left, new(2));
        await Send(right, new(2));

        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        Assert.Equal(256U, Assert.Single(Current(left).PlayerItems.InventoryItems).Effect);
        Assert.Single(TradeFrames(left), packet => packet.Action == 5);
        Assert.Single(TradeFrames(right), packet => packet.Action == 5);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Fact]
    public async Task CapacityLostAfterOfferingAbortsBothSidesAndRefundsEscrow()
    {
        var left = AddPlayer(1_293_661, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_662, 2_000, 60, [Item(20)]);
        await Open(left, right);
        await Send(left, new(6) { ItemUID = 10 });
        await Send(left, new(7) { Money = 300 });
        ReplaceInventory(right, Enumerable.Range(100, 40).Select(value => Item((uint)value)).ToArray());
        await Send(left, new(9));
        await Send(right, new(9));
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
        AssertInventory(left, 10);
        Assert.Equal(40, Current(right).PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(Current(right).PlayerItems.InventoryItems, item => item.Uid == 10);
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Fact]
    public async Task ThirdPartyCannotAcceptOrConfirmAnExistingPairOrRedirectItsAssets()
    {
        var left = AddPlayer(1_293_671, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_672, 2_000, 60, [Item(20)]);
        var attacker = AddPlayer(1_293_673, 3_000, 70, [Item(30)]);
        await Open(left, right);
        await Send(left, new(6) { ItemUID = 10 });
        await Send(left, new(7) { Money = 300 });
        await Send(attacker, new(1) { UID = left.Uid });
        await Send(attacker, new(9) { UID = left.Uid });
        await Send(attacker, new(0) { UID = right.Uid });
        await Send(attacker, new(6) { ItemUID = 10 });
        AssertWallet(attacker, 3_000, 70);
        AssertInventory(attacker, 30);
        AssertInventory(right, 20);
        await Send(left, new(9));
        AssertInventory(right, 20);
        await Send(right, new(9));
        AssertInventory(left);
        AssertInventory(right, 10, 20);
        AssertInventory(attacker, 30);
        AssertWallet(left, 700, 50);
        AssertWallet(right, 2_300, 60);
        AssertWallet(attacker, 3_000, 70);
        AssertPersisted(left);
        AssertPersisted(right);
        AssertPersisted(attacker);
    }

    [Fact]
    public async Task ChangingOfferClearsEarlierConfirmationBeforeEitherSideCanFinish()
    {
        var left = AddPlayer(1_293_691, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_692, 2_000, 60, [Item(20)]);
        await Open(left, right);
        await Send(left, new(6) { ItemUID = 10 });
        await Send(left, new(9));
        await Send(right, new(7) { Money = 200 });
        await Send(right, new(9));
        AssertInventory(left, 10);
        AssertInventory(right, 20);
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 1_800, 60);
        Assert.True(Current(left).LegacyTradeActive);
        Assert.True(Current(right).LegacyTradeActive);
        await Send(left, new(9));
        AssertInventory(left);
        AssertInventory(right, 10, 20);
        AssertWallet(left, 1_200, 50);
        AssertWallet(right, 1_800, 60);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Fact]
    public async Task LockedPersistenceFileCannotPublishAnInMemoryTransferOrDuplicateAssets()
    {
        var left = AddPlayer(1_293_681, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_682, 2_000, 60, [Item(20)]);
        var leftBefore = Current(left).PlayerItems;
        var rightBefore = Current(right).PlayerItems;
        await Open(left, right);
        await Send(left, new(6) { ItemUID = 10 });
        await Send(right, new(6) { ItemUID = 20 });
        await Send(left, new(7) { Money = 300 });
        await Send(right, new(12) { CPS = 10 });
        await Send(left, new(9));
        using (new FileStream(ItemPath(right), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Send(right, new(9));
            AssertInventory(left, 10);
            AssertInventory(right, 20);
            // The same blocked transaction cannot durably refund either. Retain
            // the escrow claim and live pair instead of publishing a false refund.
            AssertWallet(left, 700, 50);
            AssertWallet(right, 2_000, 50);
            Assert.True(Current(left).LegacyTradeActive);
            Assert.True(Current(right).LegacyTradeActive);
            AssertOtherContainers(leftBefore, Current(left).PlayerItems);
            AssertOtherContainers(rightBefore, Current(right).PlayerItems);
        }
        await Send(left, new(2));
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
        AssertInventory(left, 10);
        AssertInventory(right, 20);
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Fact]
    public async Task DisconnectCleanupRefundsOnceAndNeverWritesToTheDepartingConnection()
    {
        var left = AddPlayer(1_293_701, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_702, 2_000, 60, [Item(20)]);
        await Open(left, right);
        await Send(left, new(6) { ItemUID = 10 });
        await Send(left, new(7) { Money = 300 });
        await Send(right, new(12) { CPS = 10 });
        var leftFrameCount = left.Sent.Count;
        Assert.True(await WorldPacketHandlers.CancelLegacyTradeForDisconnectAsync(Context(left)));
        Assert.True(await WorldPacketHandlers.CancelLegacyTradeForDisconnectAsync(Context(left)));
        Assert.Equal(leftFrameCount, left.Sent.Count);
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
        AssertInventory(left, 10);
        AssertInventory(right, 20);
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        Assert.Contains(TradeFrames(right), packet => packet.Action == 5);
        AssertPersisted(left);
        AssertPersisted(right);
    }

    [Fact]
    public void PairCommitRejectsStaleSecondSessionBeforeCallingPersistence()
    {
        var left = AddPlayer(1_293_711, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_712, 2_000, 60, [Item(20)]);
        var expectedLeft = Current(left);
        var expectedRight = Current(right);
        var replacementRight = expectedRight with { LegacyTradeActive = true };
        sessions.Set(right.Connection, replacementRight);
        var persisted = false;
        Assert.False(sessions.TryCommitPair(left.Connection, expectedLeft,
            expectedLeft with { LegacyTradeActive = true }, right.Connection, expectedRight,
            expectedRight with { LegacyTradeActive = true }, () => { persisted = true; return true; }));
        Assert.False(persisted);
        Assert.Same(expectedLeft, Current(left));
        Assert.Same(replacementRight, Current(right));
    }

    [Fact]
    public void PairCommitPersistenceFailureLeavesBothOriginalSessionsPublished()
    {
        var left = AddPlayer(1_293_721, 1_000, 50, [Item(10)]);
        var right = AddPlayer(1_293_722, 2_000, 60, [Item(20)]);
        var expectedLeft = Current(left);
        var expectedRight = Current(right);
        var attempts = 0;
        Assert.False(sessions.TryCommitPair(left.Connection, expectedLeft,
            expectedLeft with { LegacyTradeActive = true }, right.Connection, expectedRight,
            expectedRight with { LegacyTradeActive = true }, () => { attempts++; return false; }));
        Assert.Equal(1, attempts);
        Assert.Same(expectedLeft, Current(left));
        Assert.Same(expectedRight, Current(right));
        AssertPersisted(left);
        AssertPersisted(right);
    }

    public void Dispose()
    {
        if (itemClosureCleanupHandled) return; // New closure cases preserve primary + cleanup errors themselves.
        log.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "PlayersItems");
    private string UserPath(Player player) => Path.Combine(UsersRoot, player.Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
    private string ItemPath(Player player) => Path.Combine(ItemsRoot, player.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");

    private Player AddPlayer(uint uid, long money, long cps, IReadOnlyList<PlayerItemSnapshotItem> inventory,
        Action<byte[]>? beforeSend = null)
    {
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (bytes, _) =>
            {
                var frame = bytes.ToArray();
                beforeSend?.Invoke(frame); // Observation/failure only; never a transaction authority.
                sent.Add(frame);
                return ValueTask.CompletedTask;
            });
        var player = new Player(uid, connection, sent);
        var text = $$"""
            [Character]
            UID={{uid}}
            Name=Trader{{uid}}
            Body=1008
            Face=154
            Haire=27
            Class=10
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Level=100
            Experience=0
            Map=1002
            X=430
            Y=380
            PMap=1002
            PMapX=430
            PMapY=380
            Strength=10
            Agility=10
            Vitaliti=10
            Spirit=10
            Atributes=0
            MinHitPoints=1000
            MinMana=100
            Money={{money}}
            ConquerPoints={{cps}}
            BoundConquerPoints=333
            BankCps=444
            PkPoints=0
            Online=1
            TradeTestSentinel=preserved
            [Unrelated]
            Sentinel=untouched
            """;
        File.WriteAllText(UserPath(player), text);
        var items = new PlayerItemSnapshot(inventory,
            RelicFuseItems: [Item(uid + 100, position: 213)],
            MythsoulBagItems: [Item(uid + 200, position: 217)],
            EquipmentItems: [Item(uid + 300, position: 4)],
            RuneItems: [Item(uid + 400, position: 211)],
            WardrobeItems: [Item(uid + 500, warehouse: 100)])
        {
            WarehouseItems = [Item(uid + 600, warehouse: 1000)],
        };
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, items);
        sessions.Set(connection, new WorldSession(uid, "fixture" + uid,
            CharacterSnapshotParser.ParseText(text, UserPath(player)), items,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: UsersRoot));
        return player;
    }

    private async Task Open(Player left, Player right)
    {
        await Send(left, new(0) { UID = right.Uid });
        await Send(right, new(1) { UID = left.Uid });
        Assert.True(Current(left).LegacyTradeActive);
        Assert.True(Current(right).LegacyTradeActive);
        Assert.Contains(TradeFrames(left), packet => packet.Action == 3);
        Assert.Contains(TradeFrames(right), packet => packet.Action == 3);
    }

    private async Task Send(Player player, LegacyTradePacket packet)
    {
        await handlers.HandleAsync(Context(player), ReadFrame(packet.ToNativeFrameBytes()));
    }

    private WorldPacketHandlerContext Context(Player player) => new(player.Connection, log, Current(player),
        Sessions: sessions, PlayerItemsRoot: ItemsRoot) { Trades = trades };

    private WorldSession Current(Player player)
    {
        Assert.True(sessions.TryGet(player.Connection, out var current));
        return Assert.IsType<WorldSession>(current);
    }

    private void ReplaceInventory(Player player, IReadOnlyList<PlayerItemSnapshotItem> inventory)
    {
        var current = Current(player);
        var items = current.PlayerItems with { InventoryItems = inventory };
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(player.Uid, items);
        sessions.Set(player.Connection, current with { PlayerItems = items });
    }

    private void SetSecurityPass(Player player, string value)
    {
        var current = Current(player);
        var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["SecurityPass"] = value,
        };
        sessions.Set(player.Connection, current with { Character = current.Character with { Attributes = attributes } });
    }

    private void AssertInventory(Player player, params uint[] expected) => Assert.Equal(
        expected.OrderBy(uid => uid), Current(player).PlayerItems.InventoryItems.Select(item => item.Uid).OrderBy(uid => uid));

    private void AssertWallet(Player player, long money, long cps)
    {
        var current = Current(player).Character;
        Assert.Equal(money, current.Money);
        Assert.Equal(cps, current.ConquerPoints);
        Assert.Equal(333, current.BoundConquerPoints);
        Assert.Equal(444, current.BankConquerPoints);
    }

    private void AssertPersisted(Player player)
    {
        var current = Current(player);
        var character = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(player)));
        Assert.Equal(current.Character.Money, character.Money);
        Assert.Equal(current.Character.ConquerPoints, character.ConquerPoints);
        Assert.Equal("preserved", character.Attributes["TradeTestSentinel"]);
        Assert.Contains("Sentinel=untouched", File.ReadAllText(UserPath(player)));
        var items = new PlayerItemSnapshotLoader(ItemsRoot).LoadByCharacterUid(player.Uid);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(items));
    }

    private static void AssertOtherContainers(PlayerItemSnapshot before, PlayerItemSnapshot after)
    {
        Assert.Same(before.RelicFuseItems, after.RelicFuseItems);
        Assert.Same(before.MythsoulBagItems, after.MythsoulBagItems);
        Assert.Same(before.EquipmentItems, after.EquipmentItems);
        Assert.Same(before.RuneItems, after.RuneItems);
        Assert.Same(before.WardrobeItems, after.WardrobeItems);
        Assert.Same(before.WarehouseItems, after.WarehouseItems);
    }

    private static void AssertItemUnchanged(PlayerItemSnapshotItem before, PlayerItemSnapshotItem after)
    {
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([before], [], [], [], [])),
            PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([after], [], [], [], [])));
    }

    private static PlayerItemSnapshotItem Item(uint uid, ushort position = 0, uint warehouse = 0) => new()
    {
        Uid = uid, ItemId = 1_088_001, Position = position, WarehouseId = warehouse,
        Durability = 5, MaximumDurability = 10, StackSize = 1,
        OwnerUid = uid, OwnerName = "OriginalOwner", Signature = "trade fixture",
    };

    private static LegacyTradePacket[] TradeFrames(Player player) => player.Sent
        .Select(ReadFrame).Where(frame => frame.Header.Opcode == WorldPacketOpcodes.LegacyTrade)
        .Select(frame => { Assert.True(LegacyTradePacket.TryParse(frame, out var packet)); return packet; }).ToArray();

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record Player(uint Uid, ConquerTcpConnection Connection, List<byte[]> Sent);
}
